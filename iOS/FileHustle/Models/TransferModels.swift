import Foundation

// Mirrors docs/protocol.md — keep both in sync if either changes.

struct TransferItem: Codable, Hashable {
    let name: String
    let relativePath: String
    let size: Int
    let isFolder: Bool
}

/// A file or folder the user picked to send, before it's been prepared
/// (folders get zipped) into a `TransferItem` + the URL of the actual bytes
/// to stream — see `TransferClient.send`.
struct SendableItem {
    let url: URL
    let isFolder: Bool
}

struct TransferHeader: Codable {
    let senderId: String
    let senderName: String
    let totalBytes: Int
    let items: [TransferItem]
}

enum TransferDirection: String {
    case sent
    case received
}

enum TransferStatus: String {
    case completed
    case declined
    case timedOut = "timed_out"
    case failed
}

struct TransferHistoryEntry: Identifiable {
    let id = UUID()
    let peerName: String
    let itemNames: [String]
    let totalBytes: Int
    let direction: TransferDirection
    let timestamp: Date
    let status: TransferStatus
    /// Where each received item landed on disk, same order as `itemNames`.
    /// Empty for sent entries — there's nothing local to open/share.
    let itemPaths: [URL]
}
