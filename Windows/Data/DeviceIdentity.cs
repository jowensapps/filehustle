using System.Text.Json;

namespace FileHustle.Data;

/// Stable per-install identity used as the mDNS instance-name id/name — see
/// docs/protocol.md. Not tied to any account. Mirrors iOS/Android's
/// DeviceIdentity.
public static class DeviceIdentity
{
    private class Identity
    {
        public string Id { get; set; } = "";
    }

    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "FileHustle", "identity.json");

    private static Identity? _cached;

    private static Identity Load()
    {
        if (_cached != null) return _cached;

        if (File.Exists(SettingsPath))
        {
            try
            {
                var loaded = JsonSerializer.Deserialize<Identity>(File.ReadAllText(SettingsPath));
                if (loaded != null && !string.IsNullOrEmpty(loaded.Id))
                {
                    _cached = loaded;
                    return loaded;
                }
            }
            catch
            {
                // Fall through and generate a fresh identity.
            }
        }

        var fresh = new Identity { Id = Guid.NewGuid().ToString() };
        Save(fresh);
        _cached = fresh;
        return fresh;
    }

    private static void Save(Identity identity)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(identity));
    }

    public static string Id => Load().Id;

    /// Defaults to a generated name rather than the real computer name —
    /// that broadcasts in mDNS discovery to everyone on the WiFi, not just
    /// people you actually transfer with. Regenerated fresh every process
    /// launch (not persisted, unlike Id) — same value for the lifetime of
    /// this run so mDNS advertising stays consistent, but a new name each
    /// time the app restarts, for stronger anonymity. Recognizing a
    /// specific device across launches goes through PeerNicknames instead,
    /// keyed by the stable Id above. Set in a static constructor (not a
    /// field initializer) so it runs after Adjectives/Nouns/Rng below are
    /// guaranteed initialized, regardless of field declaration order.
    public static readonly string Name;

    static DeviceIdentity()
    {
        Name = GenerateRandomName();
    }

    private static readonly string[] Adjectives =
    {
        "Quick", "Silent", "Amber", "Brave", "Calm", "Clever", "Cosmic",
        "Dusty", "Eager", "Fuzzy", "Gentle", "Golden", "Happy", "Hidden",
        "Jolly", "Keen", "Lucky", "Mellow", "Misty", "Nimble", "Plucky",
        "Quiet", "Rapid", "Sleepy", "Sunny", "Swift", "Tidy", "Vivid",
        "Witty", "Zesty",
    };

    private static readonly string[] Nouns =
    {
        "Otter", "Falcon", "Maple", "River", "Comet", "Badger", "Ember",
        "Fox", "Heron", "Ibis", "Jaguar", "Kestrel", "Lynx", "Marlin",
        "Newt", "Orca", "Panda", "Quail", "Raven", "Sparrow", "Toucan",
        "Urchin", "Viper", "Walrus", "Yak", "Zebra", "Pelican", "Osprey",
        "Wombat", "Puffin",
    };

    private static readonly Random Rng = new();

    private static string GenerateRandomName() =>
        $"{Adjectives[Rng.Next(Adjectives.Length)]} {Nouns[Rng.Next(Nouns.Length)]}";
}
