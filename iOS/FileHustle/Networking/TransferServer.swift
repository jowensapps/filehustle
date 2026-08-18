import Network
import Foundation
import Observation
import ZIPFoundation
import os

private let logger = Logger(subsystem: "com.hustle.filehustleios", category: "TransferServer")

struct IncomingTransferRequest: Identifiable {
    let id = UUID()
    let header: TransferHeader
}

@Observable
@MainActor
final class TransferServer {
    private(set) var incomingRequest: IncomingTransferRequest?
    let history: TransferHistoryStore

    private var listener: NWListener?
    private var pendingDecision: CheckedContinuation<Bool, Never>?

    init(history: TransferHistoryStore) {
        self.history = history
    }

    let receivedFilesDirectory: URL = {
        let dir = FileManager.default.urls(for: .documentDirectory, in: .userDomainMask)[0]
            .appendingPathComponent("Received", isDirectory: true)
        try? FileManager.default.createDirectory(at: dir, withIntermediateDirectories: true)
        return dir
    }()

    func start() {
        do {
            let params = NWParameters.tcp
            params.includePeerToPeer = true
            let listener = try NWListener(using: params)

            var txt = NWTXTRecord()
            txt["id"] = DeviceIdentity.id
            txt["name"] = DeviceIdentity.name
            txt["ver"] = "1"
            let instanceName = BonjourInstanceName.encode(id: DeviceIdentity.id, name: DeviceIdentity.name)
            listener.service = NWListener.Service(name: instanceName, type: "_filehustle._tcp", txtRecord: txt)

            listener.newConnectionHandler = { [weak self] connection in
                Task { @MainActor in
                    self?.accept(connection)
                }
            }
            listener.stateUpdateHandler = { state in
                logger.debug("listener state: \(String(describing: state))")
            }
            listener.start(queue: .main)
            self.listener = listener
            logger.debug("listener.start() called, id=\(DeviceIdentity.id), name=\(DeviceIdentity.name)")
        } catch {
            logger.error("failed to start listener — \(String(describing: error))")
        }
    }

    func stop() {
        listener?.cancel()
        listener = nil
    }

    /// Called by the UI once the user acts on `incomingRequest`.
    func respond(accept: Bool) {
        pendingDecision?.resume(returning: accept)
        pendingDecision = nil
    }

    private func accept(_ connection: NWConnection) {
        connection.start(queue: .main)
        Task {
            let reader = ConnectionReader(connection: connection)
            // Hoisted so the catch block below can still record a history
            // entry for a transfer that started (header parsed, sender
            // known) but failed partway through — e.g. the sender's own
            // ack-wait timing out and closing the connection out from under
            // us right after we accept, which previously failed silently
            // with no trace in history on this end.
            var header: TransferHeader?
            do {
                let line = try await reader.readLine()
                guard let headerData = line.data(using: .utf8) else { throw TransferError.invalidHeader }
                let parsedHeader = try JSONDecoder().decode(TransferHeader.self, from: headerData)
                header = parsedHeader

                let accepted = await withCheckedContinuation { (continuation: CheckedContinuation<Bool, Never>) in
                    self.incomingRequest = IncomingTransferRequest(header: parsedHeader)
                    self.pendingDecision = continuation
                }
                self.incomingRequest = nil

                try await connection.sendAsync(Data([accepted ? 0x01 : 0x00]))

                if accepted {
                    let itemPaths = try await receiveBody(header: parsedHeader, reader: reader)
                    recordHistory(header: parsedHeader, status: .completed, itemPaths: itemPaths)
                } else {
                    recordHistory(header: parsedHeader, status: .declined)
                }
            } catch {
                self.incomingRequest = nil
                if let header {
                    recordHistory(header: header, status: .failed)
                }
                print("FileHustle: incoming transfer failed — \(error)")
            }
            connection.cancel()
        }
    }

    private func receiveBody(header: TransferHeader, reader: ConnectionReader) async throws -> [URL] {
        var itemPaths: [URL] = []
        for item in header.items {
            let destinationURL = receivedFilesDirectory.appendingPathComponent(sanitized(item.relativePath))
            if item.isFolder {
                try await receiveFolderItem(item, to: destinationURL, reader: reader)
            } else {
                try FileManager.default.createDirectory(
                    at: destinationURL.deletingLastPathComponent(),
                    withIntermediateDirectories: true
                )
                FileManager.default.createFile(atPath: destinationURL.path, contents: nil)
                let handle = try FileHandle(forWritingTo: destinationURL)
                try await reader.readExactly(item.size, into: handle)
                try handle.close()
            }
            itemPaths.append(destinationURL)
        }
        return itemPaths
    }

    /// Folder items arrive as a single zip stream (see TransferClient's
    /// `zipFolder`) — buffer it to a temp file, unzip into a directory
    /// named after the item, then discard the zip.
    private func receiveFolderItem(_ item: TransferItem, to destinationURL: URL, reader: ConnectionReader) async throws {
        let zipURL = FileManager.default.temporaryDirectory.appendingPathComponent("\(UUID().uuidString).zip")
        FileManager.default.createFile(atPath: zipURL.path, contents: nil)
        let handle = try FileHandle(forWritingTo: zipURL)
        try await reader.readExactly(item.size, into: handle)
        try handle.close()
        defer { try? FileManager.default.removeItem(at: zipURL) }

        try? FileManager.default.removeItem(at: destinationURL)
        try FileManager.default.createDirectory(at: destinationURL, withIntermediateDirectories: true)
        try FileManager.default.unzipItem(at: zipURL, to: destinationURL)
    }

    private func sanitized(_ relativePath: String) -> String {
        relativePath
            .split(separator: "/")
            .filter { $0 != ".." && !$0.isEmpty }
            .joined(separator: "/")
    }

    private func recordHistory(header: TransferHeader, status: TransferStatus, itemPaths: [URL] = []) {
        history.record(
            peerName: header.senderName,
            itemNames: header.items.map(\.name),
            totalBytes: header.totalBytes,
            direction: .received,
            status: status,
            itemPaths: itemPaths
        )
    }
}
