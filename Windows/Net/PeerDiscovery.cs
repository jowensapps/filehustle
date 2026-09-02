using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
    private static Timer? _pruneTimer;

    // Keyed by the fully-qualified instance name (e.g. "<id>|<name>._filehustle._tcp.local").
    private static readonly Dictionary<string, (string Id, string Name)> PendingInstances = new();
    private static readonly Dictionary<string, string> TargetsByInstance = new();
    private static readonly Dictionary<string, int> PortsByInstance = new();

    // Keyed by peer Id. Written from the mDNS network-receive callback,
    // read from the prune timer's own thread — needs to be thread-safe,
    // unlike the dictionaries above which are only ever touched from one
    // effective flow at a time.
    private static readonly ConcurrentDictionary<string, DateTime> LastSeenByPeerId = new();

    // Same thread-safety need as LastSeenByPeerId. Tracks how many
    // consecutive query cycles a visible peer has failed to answer, so one
    // lost round trip (a real, expected occurrence — see RequeryDelays)
    // doesn't yank it off the list; only two in a row does.
    private static readonly ConcurrentDictionary<string, int> MissStreakByPeerId = new();
    private const int MaxMissedCyclesBeforePrune = 1;

    private static MulticastService Mdns => _mdns ??= new MulticastService();
    private static ServiceDiscovery Sd => _serviceDiscovery ??= new ServiceDiscovery(Mdns);

    // The same staleness risk that hit Android/iOS applies here: a
    // multicast socket that's been open a long time (sleep/wake, WiFi
    // adapter reset, VPN toggle) can silently stop sending/receiving with
    // no exception to react to. Restarting the whole discovery/advertising
    // cycle periodically works around it the same way. OnAnswerReceived
    // only ever adds/updates a peer — it has no ability to retroactively
    // prune one a stale session missed — so pruning happens separately,
    // via PruneStale below, rather than by blanking the list here.
    private static readonly TimeSpan RestartInterval = TimeSpan.FromSeconds(60);

    // Two devices launched around the same time end up with restart timers
    // in lockstep — every cycle, both sides tear down and rebind their
    // multicast socket (see Restart) within the same instant, so neither
    // side's re-queries land and both prune each other, forever, every
    // single RestartInterval. Jittering each reschedule desynchronizes the
    // two timers after the first collision instead of letting them stay
    // aligned indefinitely.
    private static readonly TimeSpan RestartJitterSpread = TimeSpan.FromSeconds(15);
    private static readonly Random RestartJitterRandom = new();

    private static TimeSpan NextRestartDelay() =>
        RestartInterval + TimeSpan.FromSeconds(RestartJitterRandom.NextDouble() * RestartJitterSpread.TotalSeconds * 2 - RestartJitterSpread.TotalSeconds);

    // A single mDNS query can silently vanish — dropped over WiFi, or
    // answered while this device's socket is mid-rebind right after
    // Mdns.Start() — with no exception to react to (this was previously
    // observed as peers dropping out of the list for up to RestartInterval
    // even though both devices were up: the restart cleared the list
    // immediately and relied on exactly one query/response round trip to
    // repopulate it). Re-querying a few times after restart, and only
    // pruning a peer that fails to answer any of them, absorbs an
    // occasional lost packet instead of surfacing it as a blank list.
    private static readonly TimeSpan[] RequeryDelays =
    {
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(5),
    };

    // Must clear the last RequeryDelays entry (5s) with room for the
    // answer's round trip, or a legitimately-present peer whose only
    // response was to that final retry gets pruned before it can land.
    private static readonly TimeSpan PruneGrace = TimeSpan.FromSeconds(10);

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
        AttachHandlers();
        ScheduleRestart();
    }

    // MulticastService.Stop() clears every event handler subscribed to that
    // instance (documented behavior, not a bug in that library) — so the
    // Restart() below can't just Stop()/Start() the same Mdns/Sd instances
    // the way it used to, or OnAnswerReceived permanently stops firing after
    // the very first restart and no peer can ever be (re)discovered again
    // for the rest of the process's life. Restart() instead disposes and
    // recreates fresh instances (see the Mdns/Sd lazy getters) and this
    // re-wires handlers onto whichever instance is current.
    private static void AttachHandlers()
    {
        Sd.ServiceInstanceDiscovered += OnServiceInstanceDiscovered;
        // mDNS "goodbye" packet (TTL=0) — fires when a peer shuts down
        // FileHustle cleanly, the direct equivalent of Android's
        // onServiceLost. Doesn't cover an ungraceful disappearance (crash,
        // dropping off WiFi), which is what RestartInterval is for.
        Sd.ServiceInstanceShutdown += OnServiceInstanceShutdown;
        Mdns.AnswerReceived += OnAnswerReceived;
        Mdns.NetworkInterfaceDiscovered += (_, _) => Sd.QueryServiceInstances(ServiceType);
    }

    private static void ScheduleRestart()
    {
        _restartTimer?.Dispose();
        _restartTimer = new Timer(_ =>
        {
            Restart();
            ScheduleRestart();
        }, null, NextRestartDelay(), Timeout.InfiniteTimeSpan);
    }

    // Previously the first active query only happened whenever
    // NetworkInterfaceDiscovered next fired (unreliable timing on Windows)
    // or, failing that, whenever the first periodic Restart() timer landed
    // — up to RestartInterval + RestartJitterSpread (75s) after launch.
    // Querying immediately once the socket is actually up removes that
    // wait for the common case; NetworkInterfaceDiscovered still covers
    // interfaces that come up later (e.g. Wi-Fi reconnect).
    public static void Start()
    {
        Mdns.Start();
        QueryNow();
    }

    public static void Stop()
    {
        _restartTimer?.Dispose();
        _restartTimer = null;
        _pruneTimer?.Dispose();
        _pruneTimer = null;
        try { Sd.Unadvertise(); } catch { /* best effort */ }
        Mdns.Stop();
        _advertisedPort = -1;
        _profile = null;
        LastSeenByPeerId.Clear();
        MissStreakByPeerId.Clear();
        Dispatcher.UIThread.Post(Peers.Clear);
    }

    private static void Restart()
    {
        try { if (_profile != null) Sd.Unadvertise(_profile); } catch { /* best effort */ }
        PendingInstances.Clear();
        TargetsByInstance.Clear();
        PortsByInstance.Clear();

        try { _serviceDiscovery?.Dispose(); } catch { /* best effort */ }
        try { _mdns?.Dispose(); } catch { /* best effort */ }
        _serviceDiscovery = null;
        _mdns = null;

        AttachHandlers();
        Mdns.Start();
        Advertise();

        QueryNow();
    }

    private static void QueryNow()
    {
        var queriedAt = DateTime.UtcNow;
        Sd.QueryServiceInstances(ServiceType);
        _ = RequeryWithRetriesAsync();

        // Give peers a grace window to answer one of the re-queries above
        // before pruning anyone who doesn't — see RequeryDelays/PruneGrace.
        _pruneTimer?.Dispose();
        _pruneTimer = new Timer(_ => PruneStale(queriedAt), null, PruneGrace, Timeout.InfiniteTimeSpan);
    }

    private static async Task RequeryWithRetriesAsync()
    {
        foreach (var delay in RequeryDelays)
        {
            await Task.Delay(delay);
            try { Sd.QueryServiceInstances(ServiceType); } catch { /* best effort */ }
        }
    }

    private static void PruneStale(DateTime queriedAt)
    {
        Dispatcher.UIThread.Post(() =>
        {
            for (var i = Peers.Count - 1; i >= 0; i--)
            {
                var id = Peers[i].Id;
                if (LastSeenByPeerId.TryGetValue(id, out var lastSeen) && lastSeen >= queriedAt)
                {
                    MissStreakByPeerId.TryRemove(id, out _);
                    continue;
                }

                var missed = MissStreakByPeerId.AddOrUpdate(id, 1, (_, prev) => prev + 1);
                if (missed <= MaxMissedCyclesBeforePrune) continue;

                Peers.RemoveAt(i);
                LastSeenByPeerId.TryRemove(id, out _);
                MissStreakByPeerId.TryRemove(id, out _);
            }
        });
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
        LastSeenByPeerId.TryRemove(decoded.Value.Id, out _);
        MissStreakByPeerId.TryRemove(decoded.Value.Id, out _);
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
            LastSeenByPeerId[peer.Id] = DateTime.UtcNow;
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
