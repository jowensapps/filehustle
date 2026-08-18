import SwiftUI

/// Matches Android's hand-drawn `CrossIcon` path exactly (neither Material Icons nor
/// SF Symbols has a true Latin cross glyph — `cross`/`cross.fill` render as a plus
/// sign). Vertical bar spans x 10.5–13.5 of a 24×24 viewport; horizontal bar spans
/// x 5.5–18.5, y 8–11.
private struct LatinCross: Shape {
    func path(in rect: CGRect) -> Path {
        var path = Path()
        let vX = rect.minX + rect.width * (10.5 / 24)
        let vWidth = rect.width * (3.0 / 24)
        let vY = rect.minY + rect.height * (3.0 / 24)
        let vHeight = rect.height * (18.0 / 24)
        path.addRect(CGRect(x: vX, y: vY, width: vWidth, height: vHeight))

        let hX = rect.minX + rect.width * (5.5 / 24)
        let hWidth = rect.width * (13.0 / 24)
        let hY = rect.minY + rect.height * (8.0 / 24)
        let hHeight = rect.height * (3.0 / 24)
        path.addRect(CGRect(x: hX, y: hY, width: hWidth, height: hHeight))
        return path
    }
}

/// The small cross next to developer attribution that opens the gospel alert/sheet.
/// Pulses gently to draw the eye, matching the scale animation on the Android
/// counterpart (1.0 → 1.25, 900ms ease-in-out, reversing forever).
struct PulsingCrossButton: View {
    var action: () -> Void
    @State private var isPulsing = false

    var body: some View {
        Button(action: action) {
            LatinCross()
                .fill(Color.accentColor)
                .frame(width: 14, height: 14)
                .scaleEffect(isPulsing ? 1.25 : 1.0)
        }
        .buttonStyle(.borderless)
        .accessibilityLabel("A question about eternity")
        .onAppear {
            withAnimation(.easeInOut(duration: 0.9).repeatForever(autoreverses: true)) {
                isPulsing = true
            }
        }
    }
}
