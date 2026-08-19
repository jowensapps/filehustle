using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using Avalonia.Threading;
using FileHustle.Data;
using Makaretu.Dns;

namespace FileHustle.Net;

public record Peer(string Id, string Name, string Host, int Port)
{
    /// The peer's broadcast Name regenerates every time it restarts (see
    /// DeviceIdentity), so a user-assigned nickname — keyed by the stable
    /// Id — is what's actually recognizable across launches, when set.
    public string DisplayName => PeerNicknames.Nickname(Id) ?? Name;

    public bool HasNickname => PeerNicknames.Nickname(Id) != null;
}

/// mDNS-based mirror of iOS/Android's PeerBrowser + the advertising half of
/// TransferServer. See docs/protocol.md for the wire-level discovery
/// format — the Bonjour instance name (`<id>|<name>`) is the source of
/// truth for both id and display name, not TXT records.
public static class PeerDiscovery
{
    public static ObservableCollection<Peer> Peers { get; } = new();

    private const string ServiceType = "_filehustle._tcp";

    private static MulticastService? _mdns;
    private static ServiceDiscovery? _serviceDiscovery;
    private static ServiceProfile? _profile;
    private static int _advertisedPort = -1;
    private static Timer? _restartTimer;

    // Keyed by the fully-qualified instance name (e.g. "<id>|<name>._filehustle._tcp.local").
    private static readonly Dictionary<string, (string Id, string Name)> PendingInstances = new();
    private static readonly Dictionary<string, string> TargetsByInstance = new();
    private static readonly Dictionary<string, int> PortsByInstance = new();

    private static MulticastService Mdns => _mdns ??= new MulticastService();
    private static ServiceDiscovery Sd => _serviceDiscovery ??= new ServiceDiscovery(Mdns);

    // The same staleness risk that hit Android/iOS applies here: a
    // multicast socket that's been open a long time (sleep/wake, WiFi
    // adapter reset, VPN toggle) can silently stop sending/receiving with
    // no exception to react to. Restarting the whole discovery/advertising
    // cycle periodically works around it the same way. Peers are cleared
    // on restart because OnAnswerReceived only ever adds/updates — it has
    // no ability to retroactively prune a peer a stale session missed.
    private static readonly TimeSpan RestartInterval = TimeSpan.FromSeconds(60);

    public static void StartAdvertising(int port)
    {
        _advertisedPort = port;
        Advertise();
    }

    private static void Advertise()
    {
        if (_advertisedPort < 0) return;
        var instanceName = BonjourInstanceName.Encode(DeviceIdentity.Id, DeviceIdentity.Name);
        _profile = new ServiceProfile(instanceName, ServiceType, (ushort)_advertisedPort, MulticastService.GetIPAddresses());
        Sd.Advertise(_profile);
    }

    public static void StartBrowsing()
    {
        Sd.ServiceInstanceDiscovered += OnServiceInstanceDiscovered;
        // mDNS "goodbye" packet (TTL=0) — fires when a peer shuts down
        // FileHustle cleanly, the direct equivalent of Android's
        // onServiceLost. Doesn't cover an ungraceful disappearance (crash,
        // dropping off WiFi), which is what RestartInterval is for.
        Sd.ServiceInstanceShutdown += OnServiceInstanceShutdown;
        Mdns.AnswerReceived += OnAnswerReceived;
        Mdns.NetworkInterfaceDiscovered += (_, _) => Sd.QueryServiceInstances(ServiceType);
        _restartTimer = new Timer(_ => Restart(), null, RestartInterval, RestartInterval);
    }

    public static void Start() => Mdns.Start();

    public static void Stop()
    {
        _restartTimer?.Dispose();
        _restartTimer = null;
        try { Sd.Unadvertise(); } catch { /* best effort */ }
        Mdns.Stop();
        _advertisedPort = -1;
        _profile = null;
        Dispatcher.UIThread.Post(Peers.Clear);
    }

    private static void Restart()
    {
        try { if (_profile != null) Sd.Unadvertise(_profile); } catch { /* best effort */ }
        PendingInstances.Clear();
        TargetsByInstance.Clear();
        PortsByInstance.Clear();
        Dispatcher.UIThread.Post(Peers.Clear);

        Mdns.Stop();
        Mdns.Start();
        Advertise();
        Sd.QueryServiceInstances(ServiceType);
    }

    private static void OnServiceInstanceDiscovered(object? sender, ServiceInstanceDiscoveryEventArgs e)
    {
        var rawInstance = e.ServiceInstanceName.Labels[0];
        var decoded = BonjourInstanceName.Decode(rawInstance);
        if (decoded is null || decoded.Value.Id == DeviceIdentity.Id) return;

        PendingInstances[e.ServiceInstanceName.ToString()] = (decoded.Value.Id, decoded.Value.Name);
        Mdns.SendQuery(e.ServiceInstanceName, DnsClass.IN, DnsType.SRV);
    }

    private static void OnServiceInstanceShutdown(object? sender, ServiceInstanceShutdownEventArgs e)
    {
        var rawInstance = e.ServiceInstanceName.Labels[0];
        var decoded = BonjourInstanceName.Decode(rawInstance);
        if (decoded is null) return;

        var instanceKey = e.ServiceInstanceName.ToString();
        PendingInstances.Remove(instanceKey);
        TargetsByInstance.Remove(instanceKey);
        PortsByInstance.Remove(instanceKey);
        Dispatcher.UIThread.Post(() =>
        {
            for (var i = Peers.Count - 1; i >= 0; i--)
            {
                if (Peers[i].Id == decoded.Value.Id) Peers.RemoveAt(i);
            }
        });
    }

    private static void OnAnswerReceived(object? sender, MessageEventArgs e)
    {
        var records = e.Message.Answers.Concat(e.Message.AdditionalRecords).ToList();

        foreach (var record in records.OfType<SRVRecord>())
        {
            var instanceKey = record.Name.ToString();
            if (!PendingInstances.ContainsKey(instanceKey)) continue;
            TargetsByInstance[instanceKey] = record.Target.ToString();
            PortsByInstance[instanceKey] = record.Port;
            Mdns.SendQuery(record.Target, DnsClass.IN, DnsType.A);
        }

        foreach (var record in records.OfType<ARecord>())
        {
            var targetName = record.Name.ToString();
            var match = TargetsByInstance.FirstOrDefault(kv => kv.Value == targetName);
            if (match.Key is null) continue;
            if (!PendingInstances.TryGetValue(match.Key, out var info)) continue;
            if (!PortsByInstance.TryGetValue(match.Key, out var port)) continue;

            var peer = new Peer(info.Id, info.Name, record.Address.ToString(), port);
            Dispatcher.UIThread.Post(() =>
            {
                var existingIndex = -1;
                for (var i = 0; i < Peers.Count; i++)
                {
                    if (Peers[i].Id == peer.Id) { existingIndex = i; break; }
                }
                if (existingIndex >= 0) Peers[existingIndex] = peer;
                else Peers.Add(peer);
            });
        }
    }
}
