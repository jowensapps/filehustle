import Foundation

extension Int {
    var formattedByteCount: String {
        ByteCountFormatter.string(fromByteCount: Int64(self), countStyle: .file)
    }
}
