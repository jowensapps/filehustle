namespace FileHustle.Data;

/// Encodes/decodes the mDNS service instance name as `<id>|<name>` so a
/// peer's id and display name are both available directly from discovery
/// results — no dependency on TXT record resolution. Mirrors iOS/Android's
/// BonjourInstanceName; see docs/protocol.md for why this is the
/// authoritative discovery mechanism on every platform, not a workaround.
public static class BonjourInstanceName
{
    private const char Separator = '|';

    public static string Encode(string id, string name)
    {
        var safeName = name.Replace(Separator, ' ');
        return $"{id}{Separator}{safeName}";
    }

    public static (string Id, string Name)? Decode(string instanceName)
    {
        var separatorIndex = instanceName.IndexOf(Separator);
        if (separatorIndex < 0) return null;
        var id = instanceName[..separatorIndex];
        var name = instanceName[(separatorIndex + 1)..];
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(name)) return null;
        return (id, name);
    }
}
