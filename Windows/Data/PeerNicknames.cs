using System.Text.Json;

namespace FileHustle.Data;

/// User-assigned nicknames for peers, keyed by their stable Id — lets you
/// recognize a specific device across launches even though its broadcast
/// Name now regenerates every time that device's app restarts (see
/// DeviceIdentity).
public static class PeerNicknames
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "FileHustle", "peer_nicknames.json");

    private static Dictionary<string, string>? _cached;

    private static Dictionary<string, string> Load()
    {
        if (_cached != null) return _cached;

        if (File.Exists(SettingsPath))
        {
            try
            {
                var loaded = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(SettingsPath));
                if (loaded != null)
                {
                    _cached = loaded;
                    return loaded;
                }
            }
            catch
            {
                // Fall through and start with an empty map.
            }
        }

        _cached = new Dictionary<string, string>();
        return _cached;
    }

    private static void Save(Dictionary<string, string> nicknames)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(nicknames));
    }

    public static string? Nickname(string peerId) => Load().GetValueOrDefault(peerId);

    public static void SetNickname(string peerId, string? nickname)
    {
        var trimmed = nickname?.Trim();
        var nicknames = Load();
        if (string.IsNullOrEmpty(trimmed))
        {
            nicknames.Remove(peerId);
        }
        else
        {
            nicknames[peerId] = trimmed;
        }
        Save(nicknames);
    }
}
