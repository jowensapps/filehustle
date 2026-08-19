using System.Text.Json;

namespace FileHustle.Data;

/// Tracks whether the privacy policy has been shown. Mirrors TutorialState's
/// load/cache/save pattern.
public static class PrivacyPolicyState
{
    private class State
    {
        public bool HasSeenPrivacyPolicy { get; set; }
    }

    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "FileHustle", "privacy_policy_state.json");

    private static State? _cached;

    private static State Load()
    {
        if (_cached != null) return _cached;

        if (File.Exists(SettingsPath))
        {
            try
            {
                var loaded = JsonSerializer.Deserialize<State>(File.ReadAllText(SettingsPath));
                if (loaded != null)
                {
                    _cached = loaded;
                    return loaded;
                }
            }
            catch
            {
                // Fall through and treat as not-yet-seen.
            }
        }

        var fresh = new State();
        _cached = fresh;
        return fresh;
    }

    private static void Save(State state)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(state));
    }

    public static bool HasSeenPrivacyPolicy
    {
        get => Load().HasSeenPrivacyPolicy;
        set
        {
            var state = Load();
            state.HasSeenPrivacyPolicy = value;
            Save(state);
            _cached = state;
        }
    }
}
