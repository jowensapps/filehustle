using System.Text.Json.Serialization;

namespace FileHustle.Data;

// Mirrors docs/protocol.md — keep all three clients in sync if any change.

public class TransferItem
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("relativePath")] public string RelativePath { get; set; } = "";
    [JsonPropertyName("size")] public long Size { get; set; }
    [JsonPropertyName("isFolder")] public bool IsFolder { get; set; }
}

public class TransferHeader
{
    [JsonPropertyName("senderId")] public string SenderId { get; set; } = "";
    [JsonPropertyName("senderName")] public string SenderName { get; set; } = "";
    [JsonPropertyName("totalBytes")] public long TotalBytes { get; set; }
    [JsonPropertyName("items")] public List<TransferItem> Items { get; set; } = new();
}

public enum TransferDirection { Sent, Received }

public enum TransferStatus { Completed, Declined, TimedOut, Failed }

public class TransferHistoryEntry
{
    public string Id { get; } = Guid.NewGuid().ToString();
    public required string PeerName { get; init; }
    public required List<string> ItemNames { get; init; }
    public required long TotalBytes { get; init; }
    public required TransferDirection Direction { get; init; }
    public DateTime Timestamp { get; } = DateTime.Now;
    public required TransferStatus Status { get; init; }

    /// Where each received item landed on disk, same order as ItemNames.
    /// Empty for sent entries — there's nothing local to open/reveal.
    public List<string> ItemPaths { get; init; } = new();

    public bool CanOpen => Direction == TransferDirection.Received && Status == TransferStatus.Completed && ItemPaths.Count > 0;

    public string DirectionArrow => Direction == TransferDirection.Sent ? "↑" : "↓";

    public string ItemsSummary => string.Join(", ", ItemNames);

    public string DetailLine
    {
        get
        {
            var directionLabel = Direction == TransferDirection.Sent ? "To" : "From";
            var statusSuffix = Status switch
            {
                TransferStatus.Completed => "",
                TransferStatus.Declined => " · Declined",
                TransferStatus.TimedOut => " · Timed out",
                TransferStatus.Failed => " · Failed",
                _ => "",
            };
            return $"{directionLabel} {PeerName} · {ByteFormat.Format(TotalBytes)}{statusSuffix}";
        }
    }
}

/// A file or folder the user picked to send, before it's been prepared
/// (folders get zipped) into a TransferItem + the path whose bytes get
/// streamed — see TransferClient.SendAsync. Mirrors iOS/Android's
/// SendableItem.
public class SendableItem
{
    public required string Path { get; init; }
    public required bool IsFolder { get; init; }
}
