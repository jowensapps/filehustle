import Foundation

/// Encodes/decodes the Bonjour service instance name as `<id>|<name>` so a
/// peer's id *and* display name are both available directly from
/// `NWBrowser.Result.endpoint` at browse time — no dependency on TXT record
/// resolution, which doesn't reliably resolve everywhere (observed always
/// `.none` on iOS Simulator; see docs/protocol.md).
enum BonjourInstanceName {
    private static let separator: Character = "|"

    static func encode(id: String, name: String) -> String {
        let safeName = name.replacingOccurrences(of: String(separator), with: " ")
        return "\(id)\(separator)\(safeName)"
    }

    static func decode(_ instanceName: String) -> (id: String, name: String)? {
        guard let separatorIndex = instanceName.firstIndex(of: separator) else { return nil }
        let id = String(instanceName[instanceName.startIndex..<separatorIndex])
        let name = String(instanceName[instanceName.index(after: separatorIndex)...])
        guard !id.isEmpty, !name.isEmpty else { return nil }
        return (id: id, name: name)
    }
}
