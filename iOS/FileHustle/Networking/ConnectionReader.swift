import Network
import Foundation

/// Sequential reader over an `NWConnection`'s incoming byte stream — reads
/// arrive as arbitrarily-sized chunks, so this buffers and lets callers ask
/// for "a line" or "exactly N bytes" per docs/protocol.md's framing.
actor ConnectionReader {
    private let connection: NWConnection
    private var buffer = Data()

    init(connection: NWConnection) {
        self.connection = connection
    }

    private func receiveChunk() async throws {
        // `connection.receive`'s completion handler only fires on network
        // activity — if a caller wraps this in a timeout (e.g. the ack-wait
        // in TransferClient) and the peer never responds, plain Task
        // cancellation alone wouldn't do anything: the continuation would
        // never resume, and withThrowingTaskGroup would hang forever
        // waiting for this "cancelled" child to actually finish. Cancelling
        // the connection itself on cancellation forces `.cancelled` through
        // the receive callback so the continuation always resumes.
        let data: Data = try await withTaskCancellationHandler {
            try await withCheckedThrowingContinuation { continuation in
                connection.receive(minimumIncompleteLength: 1, maximumLength: 64 * 1024) { data, _, isComplete, error in
                    if let error {
                        continuation.resume(throwing: error)
                        return
                    }
                    if let data, !data.isEmpty {
                        continuation.resume(returning: data)
                        return
                    }
                    if isComplete {
                        continuation.resume(throwing: TransferError.connectionClosed)
                        return
                    }
                    continuation.resume(returning: Data())
                }
            }
        } onCancel: {
            connection.cancel()
        }
        buffer.append(data)
    }

    func readLine() async throws -> String {
        while true {
            if let newlineIndex = buffer.firstIndex(of: 0x0A) {
                let lineData = buffer[buffer.startIndex..<newlineIndex]
                let line = String(decoding: lineData, as: UTF8.self)
                buffer.removeSubrange(buffer.startIndex...newlineIndex)
                return line
            }
            try await receiveChunk()
        }
    }

    func readByte() async throws -> UInt8 {
        while buffer.isEmpty {
            try await receiveChunk()
        }
        return buffer.removeFirst()
    }

    /// Reads exactly `count` bytes, writing each chunk straight to `handle`
    /// rather than accumulating in memory — needed for large file bodies.
    func readExactly(_ count: Int, into handle: FileHandle) async throws {
        var remaining = count
        if !buffer.isEmpty {
            let take = min(remaining, buffer.count)
            handle.write(buffer.prefix(take))
            buffer.removeFirst(take)
            remaining -= take
        }
        while remaining > 0 {
            try await receiveChunk()
            let take = min(remaining, buffer.count)
            handle.write(buffer.prefix(take))
            buffer.removeFirst(take)
            remaining -= take
        }
    }
}
