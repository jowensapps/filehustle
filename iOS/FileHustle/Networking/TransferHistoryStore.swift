import Foundation
import Observation

/// Shared by `TransferServer` (received transfers) and `TransferClient`
/// (sent transfers) so both directions land in one list.
///
/// Phase 4 TODO: persist this with SwiftData instead of in-memory only.
@Observable
@MainActor
final class TransferHistoryStore {
    private(set) var entries: [TransferHistoryEntry] = []

    func record(peerName: String, itemNames: [String], totalBytes: Int, direction: TransferDirection, status: TransferStatus, itemPaths: [URL] = []) {
        let entry = TransferHistoryEntry(
            peerName: peerName,
            itemNames: itemNames,
            totalBytes: totalBytes,
            direction: direction,
            timestamp: Date(),
            status: status,
            itemPaths: itemPaths
        )
        entries.insert(entry, at: 0)
    }
}
