import SwiftUI

struct IncomingTransferSheet: View {
    let request: IncomingTransferRequest
    let onDecision: (Bool) -> Void

    var body: some View {
        VStack(spacing: 20) {
            Image(systemName: "tray.and.arrow.down.fill")
                .font(.system(size: 44))
                .foregroundStyle(Color.accentColor)
                .padding(.top, 32)

            Text("\(request.header.senderName) wants to send you")
                .font(.headline)
                .multilineTextAlignment(.center)

            VStack(alignment: .leading, spacing: 6) {
                ForEach(request.header.items, id: \.relativePath) { item in
                    Label(item.name, systemImage: item.isFolder ? "folder" : "doc")
                        .lineLimit(1)
                }
            }
            .padding()
            .frame(maxWidth: .infinity, alignment: .leading)
            .background(.thinMaterial, in: RoundedRectangle(cornerRadius: 12))
            .padding(.horizontal)

            Text(request.header.totalBytes.formattedByteCount)
                .foregroundStyle(.secondary)

            Spacer()

            HStack(spacing: 16) {
                Button(role: .cancel) {
                    onDecision(false)
                } label: {
                    Text("Decline").frame(maxWidth: .infinity)
                }
                .buttonStyle(.bordered)

                Button {
                    onDecision(true)
                } label: {
                    Text("Accept").frame(maxWidth: .infinity)
                }
                .buttonStyle(.borderedProminent)
            }
            .padding(.horizontal)
            .padding(.bottom, 24)
        }
        .interactiveDismissDisabled()
    }
}
