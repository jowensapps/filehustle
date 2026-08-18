import SwiftUI
import UniformTypeIdentifiers

struct ContentView: View {
    @Environment(PeerBrowser.self) private var peerBrowser
    @Environment(TransferServer.self) private var transferServer
    @Environment(TransferClient.self) private var transferClient
    @Environment(TransferHistoryStore.self) private var history

    @State private var selectedPeer: Peer?
    @State private var isPickingFiles = false
    @State private var isPickingFolder = false
    @State private var sendErrorMessage: String?
    @State private var isSending = false
    @State private var isShowingAbout = false

    var body: some View {
        NavigationStack {
            List {
                Section("Nearby Devices") {
                    if peerBrowser.peers.isEmpty {
                        Text("Looking for devices on your WiFi network…")
                            .foregroundStyle(.secondary)
                    }
                    ForEach(peerBrowser.peers) { peer in
                        Button {
                            selectedPeer = peer
                            #if DEBUG
                            // Skips the system document picker, which isn't
                            // drivable from a headless UI test — exercises
                            // the same send path with a generated file.
                            sendDebugTestFile(to: peer)
                            #else
                            isPickingFiles = true
                            #endif
                        } label: {
                            Label(peer.name, systemImage: "iphone.and.arrow.forward")
                        }
                        .disabled(isSending)
                        .contextMenu {
                            Button {
                                selectedPeer = peer
                                isPickingFiles = true
                            } label: {
                                Label("Send Files…", systemImage: "doc")
                            }
                            Button {
                                selectedPeer = peer
                                isPickingFolder = true
                            } label: {
                                Label("Send Folder…", systemImage: "folder")
                            }
                            #if DEBUG
                            Button {
                                sendDebugTestFolder(to: peer)
                            } label: {
                                Label("Send Test Folder", systemImage: "folder.badge.gearshape")
                            }
                            #endif
                        }
                    }
                }

                Section("Recent Transfers") {
                    if history.entries.isEmpty {
                        Text("No transfers yet")
                            .foregroundStyle(.secondary)
                    }
                    ForEach(history.entries) { entry in
                        TransferHistoryRow(entry: entry)
                    }
                }
            }
            .navigationTitle("FileHustle — \(DeviceIdentity.name)")
            .toolbar {
                ToolbarItem(placement: .topBarTrailing) {
                    Button {
                        isShowingAbout = true
                    } label: {
                        Image(systemName: "info.circle")
                    }
                }
            }
            .overlay {
                if isSending {
                    ProgressView("Sending…")
                        .padding()
                        .background(.regularMaterial, in: RoundedRectangle(cornerRadius: 12))
                }
            }
        }
        .sheet(isPresented: $isShowingAbout) {
            AboutView()
        }
        .fileImporter(isPresented: $isPickingFiles, allowedContentTypes: [.item], allowsMultipleSelection: true) { result in
            guard let peer = selectedPeer else { return }
            switch result {
            case .success(let urls):
                sendFiles(urls, to: peer)
            case .failure(let error):
                sendErrorMessage = error.localizedDescription
            }
        }
        .fileImporter(isPresented: $isPickingFolder, allowedContentTypes: [.folder]) { result in
            guard let peer = selectedPeer else { return }
            switch result {
            case .success(let url):
                sendFolder(url, to: peer)
            case .failure(let error):
                sendErrorMessage = error.localizedDescription
            }
        }
        .sheet(item: incomingRequestBinding) { request in
            IncomingTransferSheet(request: request) { accepted in
                transferServer.respond(accept: accepted)
            }
        }
        .alert(
            "Couldn't send files",
            isPresented: Binding(
                get: { sendErrorMessage != nil },
                set: { isPresented in if !isPresented { sendErrorMessage = nil } }
            ),
            presenting: sendErrorMessage
        ) { _ in
            Button("OK") { sendErrorMessage = nil }
        } message: { message in
            Text(message)
        }
    }

    private var incomingRequestBinding: Binding<IncomingTransferRequest?> {
        Binding(
            get: { transferServer.incomingRequest },
            set: { newValue in
                // Swipe-to-dismiss without tapping a button counts as a decline.
                if newValue == nil {
                    transferServer.respond(accept: false)
                }
            }
        )
    }

    private func sendFiles(_ urls: [URL], to peer: Peer) {
        isSending = true
        Task {
            defer { isSending = false }
            let accessedURLs = urls.filter { $0.startAccessingSecurityScopedResource() }
            defer { accessedURLs.forEach { $0.stopAccessingSecurityScopedResource() } }
            do {
                try await transferClient.send(items: urls.map { SendableItem(url: $0, isFolder: false) }, to: peer)
            } catch {
                sendErrorMessage = describeSendError(error, peerName: peer.name)
            }
        }
    }

    private func sendFolder(_ url: URL, to peer: Peer) {
        isSending = true
        Task {
            defer { isSending = false }
            let accessed = url.startAccessingSecurityScopedResource()
            defer { if accessed { url.stopAccessingSecurityScopedResource() } }
            do {
                try await transferClient.send(items: [SendableItem(url: url, isFolder: true)], to: peer)
            } catch {
                sendErrorMessage = describeSendError(error, peerName: peer.name)
            }
        }
    }

    private func describeSendError(_ error: Error, peerName: String) -> String {
        if let sendError = error as? TransferClient.SendError {
            switch sendError {
            case .declined: return "\(peerName) declined the transfer."
            case .timedOut: return "\(peerName) didn't respond in time."
            }
        }
        return error.localizedDescription
    }

    #if DEBUG
    /// Bypasses the system document picker (not automatable in a headless
    /// UI test) so discovery + transfer can be exercised end-to-end.
    private func sendDebugTestFile(to peer: Peer) {
        let url = FileManager.default.temporaryDirectory.appendingPathComponent("filehustle-test-\(Int(Date().timeIntervalSince1970)).txt")
        let contents = "Hello from FileHustle debug test, sent at \(Date())."
        try? contents.write(to: url, atomically: true, encoding: .utf8)
        sendFiles([url], to: peer)
    }

    /// Bypasses the system folder picker (same reasoning as
    /// sendDebugTestFile) — builds a small nested folder so the zip/unzip
    /// path can be exercised end-to-end without manual interaction.
    private func sendDebugTestFolder(to peer: Peer) {
        let folderURL = FileManager.default.temporaryDirectory.appendingPathComponent("filehustle-test-folder-\(Int(Date().timeIntervalSince1970))", isDirectory: true)
        try? FileManager.default.createDirectory(at: folderURL, withIntermediateDirectories: true)
        try? "First file".write(to: folderURL.appendingPathComponent("one.txt"), atomically: true, encoding: .utf8)
        try? "Second file".write(to: folderURL.appendingPathComponent("two.txt"), atomically: true, encoding: .utf8)
        let subfolder = folderURL.appendingPathComponent("nested", isDirectory: true)
        try? FileManager.default.createDirectory(at: subfolder, withIntermediateDirectories: true)
        try? "Nested file".write(to: subfolder.appendingPathComponent("three.txt"), atomically: true, encoding: .utf8)
        sendFolder(folderURL, to: peer)
    }
    #endif
}

#Preview {
    ContentView()
        .environment(PeerBrowser())
        .environment(TransferServer(history: TransferHistoryStore()))
        .environment(TransferClient(history: TransferHistoryStore()))
        .environment(TransferHistoryStore())
}
