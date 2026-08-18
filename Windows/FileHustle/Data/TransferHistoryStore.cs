using System.Collections.ObjectModel;
using Avalonia.Threading;

namespace FileHustle.Data;

/// Shared by the receive path (TransferServer) and send path
/// (TransferClient) so both directions land in one list. Mirrors
/// iOS/Android's TransferHistoryStore.
///
/// Phase 4 TODO: persist this to disk instead of in-memory only.
public static class TransferHistoryStore
{
    public static ObservableCollection<TransferHistoryEntry> Entries { get; } = new();

    public static void Record(string peerName, List<string> itemNames, long totalBytes, TransferDirection direction, TransferStatus status, List<string>? itemPaths = null)
    {
        var entry = new TransferHistoryEntry
        {
            PeerName = peerName,
            ItemNames = itemNames,
            TotalBytes = totalBytes,
            Direction = direction,
            Status = status,
            ItemPaths = itemPaths ?? new List<string>(),
        };
        Dispatcher.UIThread.Post(() => Entries.Insert(0, entry));
    }
}
