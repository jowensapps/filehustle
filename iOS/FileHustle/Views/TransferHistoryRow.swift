import SwiftUI

struct TransferHistoryRow: View {
    let entry: TransferHistoryEntry

    var body: some View {
        HStack {
            Image(systemName: entry.direction == .sent ? "arrow.up.circle.fill" : "arrow.down.circle.fill")
                .foregroundStyle(statusColor)
            VStack(alignment: .leading) {
                Text(entry.itemNames.joined(separator: ", "))
                    .lineLimit(1)
                Text("\(entry.direction == .sent ? "To" : "From") \(entry.peerName) · \(entry.totalBytes.formattedByteCount)")
                    .font(.caption)
                    .foregroundStyle(.secondary)
            }
            Spacer()
            if entry.status != .completed {
                Text(statusLabel)
                    .font(.caption)
                    .foregroundStyle(statusColor)
            } else if entry.direction == .received && !entry.itemPaths.isEmpty {
                // Received files land in the app's own sandbox with no way
                // out otherwise — this is the "download it" affordance
                // (Save to Files, AirDrop, Mail, etc). The sandbox is also
                // separately browsable under Files > On My iPhone/iPad now
                // (see Supplementary-Info.plist), but this is the one-tap path.
                ShareLink(items: entry.itemPaths) {
                    Image(systemName: "square.and.arrow.up")
                }
                .labelStyle(.iconOnly)
            }
        }
    }

    private var statusColor: Color {
        switch entry.status {
        case .completed: .green
        case .declined, .failed: .red
        case .timedOut: .orange
        }
    }

    private var statusLabel: String {
        switch entry.status {
        case .completed: "Done"
        case .declined: "Declined"
        case .timedOut: "Timed out"
        case .failed: "Failed"
        }
    }
}
