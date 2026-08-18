import Network
import Foundation
import Observation
import os

private let logger = Logger(subsystem: "com.hustle.filehustleios", category: "PeerBrowser")

struct Peer: Identifiable, Hashable {
    let id: String
    let name: String
    let endpoint: NWEndpoint

    static func == (lhs: Peer, rhs: Peer) -> Bool { lhs.id == rhs.id }
    func hash(into hasher: inout Hasher) { hasher.combine(id) }
}

@Observable
@MainActor
final class PeerBrowser {
    private(set) var peers: [Peer] = []
    private var browser: NWBrowser?

    func start() {
        let params = NWParameters()
        params.includePeerToPeer = true
        let browser = NWBrowser(for: .bonjour(type: "_filehustle._tcp", domain: nil), using: params)
        browser.stateUpdateHandler = { state in
            logger.debug("browser state: \(String(describing: state))")
        }
        browser.browseResultsChangedHandler = { [weak self] results, _ in
            logger.debug("browse results changed: \(results.count) raw results")
            Task { @MainActor in
                self?.update(results: results)
            }
        }
        browser.start(queue: .main)
        self.browser = browser
        logger.debug("browser.start() called, myId=\(DeviceIdentity.id)")
    }

    func stop() {
        browser?.cancel()
        browser = nil
        peers = []
    }

    private func update(results: Set<NWBrowser.Result>) {
        let myId = DeviceIdentity.id
        // The Bonjour instance name carries both id and display name (see
        // TransferServer.start(), which registers via
        // BonjourInstanceName.encode) — decode it from there rather than
        // from NWBrowser.Result.metadata's TXT record, which in practice
        // doesn't reliably resolve before results are delivered (observed
        // as always `.none` in Simulator).
        peers = results.compactMap { result -> Peer? in
            guard case .service(let rawInstanceName, _, _, _) = result.endpoint else { return nil }
            guard let decoded = BonjourInstanceName.decode(rawInstanceName) else { return nil }
            guard decoded.id != myId else { return nil }
            return Peer(id: decoded.id, name: decoded.name, endpoint: result.endpoint)
        }.sorted { $0.name < $1.name }
        logger.debug("update() produced \(self.peers.count) peers, myId=\(myId)")
    }
}
