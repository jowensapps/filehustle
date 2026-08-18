import Network
import Foundation
import os

private let logger = Logger(subsystem: "com.hustle.filehustleios", category: "NWConnection")

enum TransferError: Error {
    case connectionClosed
    case invalidHeader
}

extension NWConnection {
    func sendAsync(_ data: Data) async throws {
        try await withCheckedThrowingContinuation { (continuation: CheckedContinuation<Void, Error>) in
            self.send(content: data, completion: .contentProcessed { error in
                if let error {
                    continuation.resume(throwing: error)
                } else {
                    continuation.resume()
                }
            })
        }
    }

    /// Starts the connection and waits for it to become `.ready`. The state
    /// handler must be attached before `start` runs, so this owns both
    /// calls rather than leaving callers to sequence them correctly.
    func startAndWaitUntilReady(on queue: DispatchQueue) async throws {
        // Wrapped in withTaskCancellationHandler so a caller-imposed timeout
        // (e.g. TransferClient's withTimeout) can actually interrupt this —
        // otherwise, if the peer never responds, structured concurrency
        // still has to wait for this child task to finish before the
        // timeout's TaskGroup can return, hanging forever even after the
        // timeout "fires" internally. Cancelling the connection forces a
        // `.cancelled` state through, which resumes the continuation.
        try await withTaskCancellationHandler {
            try await withCheckedThrowingContinuation { (continuation: CheckedContinuation<Void, Error>) in
                // The connection keeps transitioning states for its whole
                // lifetime (e.g. .cancelled once the transfer finishes and
                // the caller cancels it) — a checked continuation may only
                // resume once, so this handler must stop reacting after its
                // first call.
                var didResume = false
                self.stateUpdateHandler = { state in
                    logger.debug("connection state: \(String(describing: state))")
                    guard !didResume else { return }
                    switch state {
                    case .ready:
                        didResume = true
                        continuation.resume()
                    case .failed(let error):
                        didResume = true
                        continuation.resume(throwing: error)
                    case .cancelled:
                        didResume = true
                        continuation.resume(throwing: TransferError.connectionClosed)
                    default:
                        break
                    }
                }
                self.start(queue: queue)
            }
        } onCancel: {
            self.cancel()
        }
    }
}
