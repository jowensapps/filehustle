import Network
import Foundation
import Observation
import ZIPFoundation

@Observable
@MainActor
final class TransferClient {
    enum SendError: Error {
        case declined
        case timedOut
    }

    private let history: TransferHistoryStore

    init(history: TransferHistoryStore) {
        self.history = history
    }

    func send(items: [SendableItem], to peer: Peer) async throws {
        var prepared: [(item: TransferItem, streamURL: URL)] = []
        var tempZipURLs: [URL] = []
        defer { tempZipURLs.forEach { try? FileManager.default.removeItem(at: $0) } }

        for item in items {
            if item.isFolder {
                let zipURL = try zipFolder(at: item.url)
                tempZipURLs.append(zipURL)
                let size = try FileManager.default.attributesOfItem(atPath: zipURL.path)[.size] as? Int ?? 0
                prepared.append((TransferItem(name: item.url.lastPathComponent, relativePath: item.url.lastPathComponent, size: size, isFolder: true), zipURL))
            } else {
                let size = try FileManager.default.attributesOfItem(atPath: item.url.path)[.size] as? Int ?? 0
                prepared.append((TransferItem(name: item.url.lastPathComponent, relativePath: item.url.lastPathComponent, size: size, isFolder: false), item.url))
            }
        }

        let header = TransferHeader(
            senderId: DeviceIdentity.id,
            senderName: DeviceIdentity.name,
            totalBytes: prepared.reduce(0) { $0 + $1.item.size },
            items: prepared.map(\.item)
        )

        // No includePeerToPeer here (unlike PeerBrowser/TransferServer) —
        // that flag opts into AWDL/Bluetooth path candidates meant for
        // Apple-to-Apple peer-to-peer scenarios, which real devices ignore
        // for a plain WiFi peer, but which was observed to leave the
        // connection stuck in `.preparing` indefinitely when connecting
        // out to an Android peer (2026-08-17) — Android has no AWDL/BT
        // path to offer, so don't ask Network.framework to consider one.
        let params = NWParameters.tcp
        let connection = NWConnection(to: peer.endpoint, using: params)

        do {
            try await withTimeout(seconds: 20) { try await connection.startAndWaitUntilReady(on: .main) }

            let headerData = try JSONEncoder().encode(header)
            try await connection.sendAsync(headerData + Data([0x0A]))

            let reader = ConnectionReader(connection: connection)
            let ackByte = try await withTimeout(seconds: 60) { try await reader.readByte() }
            guard ackByte == 0x01 else { throw SendError.declined }

            for (_, streamURL) in prepared {
                try await stream(fileAt: streamURL, over: connection)
            }
            connection.cancel()
            history.record(peerName: peer.name, itemNames: header.items.map(\.name), totalBytes: header.totalBytes, direction: .sent, status: .completed)
        } catch SendError.declined {
            connection.cancel()
            history.record(peerName: peer.name, itemNames: header.items.map(\.name), totalBytes: header.totalBytes, direction: .sent, status: .declined)
            throw SendError.declined
        } catch SendError.timedOut {
            connection.cancel()
            history.record(peerName: peer.name, itemNames: header.items.map(\.name), totalBytes: header.totalBytes, direction: .sent, status: .timedOut)
            throw SendError.timedOut
        } catch {
            connection.cancel()
            history.record(peerName: peer.name, itemNames: header.items.map(\.name), totalBytes: header.totalBytes, direction: .sent, status: .failed)
            throw error
        }
    }

    /// `shouldKeepParent: false` so the zip's top-level entries are the
    /// folder's contents directly — matches TransferServer.receiveBody,
    /// which creates a directory named after the item and unzips into it.
    private func zipFolder(at folderURL: URL) throws -> URL {
        let zipURL = FileManager.default.temporaryDirectory.appendingPathComponent("\(UUID().uuidString).zip")
        try FileManager.default.zipItem(at: folderURL, to: zipURL, shouldKeepParent: false)
        return zipURL
    }

    private func stream(fileAt url: URL, over connection: NWConnection) async throws {
        let handle = try FileHandle(forReadingFrom: url)
        defer { try? handle.close() }
        let chunkSize = 64 * 1024
        while true {
            let chunk = handle.readData(ofLength: chunkSize)
            if chunk.isEmpty { break }
            try await connection.sendAsync(chunk)
        }
    }

    private func withTimeout<T: Sendable>(seconds: TimeInterval, operation: @escaping @Sendable () async throws -> T) async throws -> T {
        try await withThrowingTaskGroup(of: T.self) { group in
            group.addTask { try await operation() }
            group.addTask {
                try await Task.sleep(nanoseconds: UInt64(seconds * 1_000_000_000))
                throw SendError.timedOut
            }
            guard let result = try await group.next() else { throw SendError.timedOut }
            group.cancelAll()
            return result
        }
    }
}
