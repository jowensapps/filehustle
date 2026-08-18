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
        public string Name { get; set; } = "";
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

        var fresh = new Identity { Id = Guid.NewGuid().ToString(), Name = GenerateRandomName() };
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
    /// people you actually transfer with. Word lists match iOS/Android's
    /// DeviceIdentity for consistency.
    public static string Name
    {
        get => Load().Name;
        set
        {
            var identity = Load();
            identity.Name = value;
            Save(identity);
            _cached = identity;
        }
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
