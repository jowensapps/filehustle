import SwiftUI
import UIKit

/// Developer credits — who made the app and how to reach them.
struct AboutView: View {
    @Environment(\.dismiss) private var dismiss
    @State private var showingGospelAlert = false
    @State private var showingGospel = false

    private var versionString: String {
        let version = Bundle.main.infoDictionary?["CFBundleShortVersionString"] as? String ?? "1.0"
        return "Version \(version)"
    }

    private var bugReportURL: URL? {
        var components = URLComponents()
        components.scheme = "mailto"
        components.path = "j.owens.apps@gmail.com"
        components.queryItems = [URLQueryItem(name: "subject", value: "FileHustle")]
        return components.url
    }

    var body: some View {
        NavigationStack {
            VStack(spacing: 20) {
                // No designed app icon yet — a plain transfer glyph stands in
                // for AboutView's usual real-icon header until one exists.
                Image(systemName: "arrow.up.arrow.down.circle.fill")
                    .resizable()
                    .scaledToFit()
                    .frame(width: 72, height: 72)
                    .foregroundStyle(Color.accentColor)
                    .padding(.top, 16)

                Text(versionString)
                    .font(.subheadline)
                    .foregroundStyle(.secondary)

                VStack(spacing: 4) {
                    Text("FileHustle was developed by")
                        .font(.body)
                    Text("Jared Owens")
                        .font(.title3)
                    Text("founder and solo developer of J. O. Apps.")
                        .font(.body)
                    PulsingCrossButton {
                        showingGospelAlert = true
                    }
                    .padding(.top, 2)
                }
                .multilineTextAlignment(.center)
                .padding(.horizontal, 24)

                Button {
                    if let bugReportURL {
                        UIApplication.shared.open(bugReportURL)
                    }
                } label: {
                    Label("Report a Bug", systemImage: "envelope.fill")
                        .font(.headline)
                        .frame(maxWidth: .infinity)
                        .padding(.vertical, 10)
                }
                .buttonStyle(.borderedProminent)
                .padding(.horizontal, 24)

                Spacer(minLength: 0)
            }
            .navigationTitle("About")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .confirmationAction) {
                    Button("Done") { dismiss() }
                }
            }
            .sheet(isPresented: $showingGospel) {
                GospelView()
            }
            .alert("An honest question", isPresented: $showingGospelAlert) {
                Button("Close", role: .cancel) {}
                Button("More") { showingGospel = true }
            } message: {
                Text("Do you know for sure that you will go to Heaven when you die?")
            }
        }
        .presentationDetents([.medium])
    }
}

#Preview {
    AboutView()
}
