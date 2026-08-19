using System.Collections.ObjectModel;
using System.Linq;
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

    // Keyed by the fully-qualified instance name (e.g. "<id>|<name>._filehustle._tcp.local").
    private static readonly Dictionary<string, (string Id, string Name)> PendingInstances = new();
    private static readonly Dictionary<string, string> TargetsByInstance = new();
    private static readonly Dictionary<string, int> PortsByInstance = new();

    private static MulticastService Mdns => _mdns ??= new MulticastService();
    private static ServiceDiscovery Sd => _serviceDiscovery ??= new ServiceDiscovery(Mdns);

    public static void StartAdvertising(int port)
    {
        var instanceName = BonjourInstanceName.Encode(DeviceIdentity.Id, DeviceIdentity.Name);
        var profile = new ServiceProfile(instanceName, ServiceType, (ushort)port, MulticastService.GetIPAddresses());
        Sd.Advertise(profile);
    }

    public static void StartBrowsing()
    {
        Sd.ServiceInstanceDiscovered += OnServiceInstanceDiscovered;
        Mdns.AnswerReceived += OnAnswerReceived;
        Mdns.NetworkInterfaceDiscovered += (_, _) => Sd.QueryServiceInstances(ServiceType);
    }

    public static void Start() => Mdns.Start();

    public static void Stop()
    {
        try { Sd.Unadvertise(); } catch { /* best effort */ }
        Mdns.Stop();
        Dispatcher.UIThread.Post(Peers.Clear);
    }

    private static void OnServiceInstanceDiscovered(object? sender, ServiceInstanceDiscoveryEventArgs e)
    {
        var rawInstance = e.ServiceInstanceName.Labels[0];
        var decoded = BonjourInstanceName.Decode(rawInstance);
        if (decoded is null || decoded.Value.Id == DeviceIdentity.Id) return;

        PendingInstances[e.ServiceInstanceName.ToString()] = (decoded.Value.Id, decoded.Value.Name);
        Mdns.SendQuery(e.ServiceInstanceName, DnsClass.IN, DnsType.SRV);
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
