import SwiftUI

@main
struct FileHustleApp: App {
    @State private var history = TransferHistoryStore()
    @State private var peerBrowser = PeerBrowser()
    @State private var transferServer: TransferServer
    @State private var transferClient: TransferClient

    init() {
        let history = TransferHistoryStore()
        _history = State(initialValue: history)
        _transferServer = State(initialValue: TransferServer(history: history))
        _transferClient = State(initialValue: TransferClient(history: history))
    }

    var body: some Scene {
        WindowGroup {
            ContentView()
                .environment(history)
                .environment(peerBrowser)
                .environment(transferServer)
                .environment(transferClient)
                .onAppear {
                    peerBrowser.start()
                    transferServer.start()
                }
        }
    }
}
