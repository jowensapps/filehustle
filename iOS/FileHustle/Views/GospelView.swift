import SwiftUI

private struct GospelVerse {
    let text: String
    let reference: String
}

private struct GospelSheet: Identifiable {
    let id: Int
    let title: String
    let body: String
    let verses: [GospelVerse]
}

// Scripture quotations are from the World English Bible (public domain).
private let gospelSheets: [GospelSheet] = [
    GospelSheet(
        id: 0,
        title: "God loves you",
        body: "That question isn't meant to scare you — it's the most important question anyone can ask. The Bible says you can know the answer. It starts with this: God made you, God loves you, and He wants you with Him forever.",
        verses: [
            GospelVerse(
                text: "For God so loved the world, that he gave his one and only Son, that whoever believes in him should not perish, but have eternal life.",
                reference: "John 3:16"
            )
        ]
    ),
    GospelSheet(
        id: 1,
        title: "The problem: sin",
        body: "But there's a problem, and it's universal. Every one of us has done wrong — lied, been selfish, ignored God. The Bible calls this sin, and it separates us from a perfect God. Sin carries a penalty, and it's more than physical death.",
        verses: [
            GospelVerse(
                text: "For all have sinned, and fall short of the glory of God.",
                reference: "Romans 3:23"
            ),
            GospelVerse(
                text: "For the wages of sin is death, but the free gift of God is eternal life in Christ Jesus our Lord.",
                reference: "Romans 6:23"
            )
        ]
    ),
    GospelSheet(
        id: 2,
        title: "We can't earn it",
        body: "Most people hope their good outweighs their bad. But Heaven isn't earned by good deeds, religion, or effort — a perfect God can't be paid off with an imperfect record. If we could earn it, we wouldn't need a Savior.",
        verses: [
            GospelVerse(
                text: "For by grace you have been saved through faith, and that not of yourselves; it is the gift of God, not of works, that no one would boast.",
                reference: "Ephesians 2:8–9"
            )
        ]
    ),
    GospelSheet(
        id: 3,
        title: "God's answer: the cross",
        body: "So God did what we couldn't. Jesus Christ — God's Son — lived the perfect life we never could, then died on the cross taking the punishment our sin deserved. Then He rose from the dead, proving His payment was accepted.",
        verses: [
            GospelVerse(
                text: "But God commends his own love toward us, in that while we were yet sinners, Christ died for us.",
                reference: "Romans 5:8"
            ),
            GospelVerse(
                text: "Christ died for our sins according to the Scriptures… he was buried… he was raised on the third day according to the Scriptures.",
                reference: "1 Corinthians 15:3–4"
            )
        ]
    ),
    GospelSheet(
        id: 4,
        title: "The only way",
        body: "Jesus didn't claim to be a way to God — He claimed to be the only way. Forgiveness isn't found in being good enough or religious enough. It's found in a Person.",
        verses: [
            GospelVerse(
                text: "Jesus said to him, 'I am the way, the truth, and the life. No one comes to the Father, except through me.'",
                reference: "John 14:6"
            )
        ]
    ),
    GospelSheet(
        id: 5,
        title: "How to respond",
        body: "So what do you do? Turn from your sin, and trust in Jesus alone — not your own goodness — to save you. It's not a ritual or a transaction. It's placing your whole confidence in what He did for you.",
        verses: [
            GospelVerse(
                text: "If you will confess with your mouth that Jesus is Lord, and believe in your heart that God raised him from the dead, you will be saved.",
                reference: "Romans 10:9"
            ),
            GospelVerse(
                text: "For, 'Whoever will call on the name of the Lord will be saved.'",
                reference: "Romans 10:13"
            )
        ]
    ),
    GospelSheet(
        id: 6,
        title: "You can know",
        body: "If you're ready, you can tell God right now, in your own words: \"God, I know I've sinned. I believe Jesus died for my sins and rose again. I turn from my sin and trust Him alone to save me. Amen.\" If you meant that, the Bible says you can be sure — not hope, know.",
        verses: [
            GospelVerse(
                text: "These things I have written to you who believe in the name of the Son of God, that you may know that you have eternal life.",
                reference: "1 John 5:13"
            )
        ]
    ),
    GospelSheet(
        id: 7,
        title: "Where to go from here",
        body: "If you just put your trust in Christ — welcome to the family of God. Faith grows the same way it begins: by hearing from God. Get a Bible and start reading in the Gospel of John, talk to God honestly in prayer, and find a church that teaches the Bible, so you don't walk alone.",
        verses: [
            GospelVerse(
                text: "So faith comes by hearing, and hearing by the word of God.",
                reference: "Romans 10:17"
            )
        ]
    )
]

struct GospelView: View {
    @Environment(\.dismiss) private var dismiss
    @State private var page = 0

    var body: some View {
        NavigationStack {
            VStack(spacing: 0) {
                TabView(selection: $page) {
                    ForEach(gospelSheets) { sheet in
                        ScrollView {
                            VStack(alignment: .leading, spacing: 16) {
                                Text(sheet.title)
                                    .font(.title2)
                                    .fontWeight(.bold)
                                Text(sheet.body)
                                    .font(.body)
                                ForEach(Array(sheet.verses.enumerated()), id: \.offset) { _, verse in
                                    VStack(alignment: .leading, spacing: 6) {
                                        Text("“\(verse.text)”")
                                            .font(.callout)
                                            .italic()
                                        Text("— \(verse.reference)")
                                            .font(.footnote)
                                            .fontWeight(.semibold)
                                            .foregroundStyle(Color.accentColor)
                                    }
                                    .padding(12)
                                    .frame(maxWidth: .infinity, alignment: .leading)
                                    .background(Color(.secondarySystemBackground))
                                    .clipShape(RoundedRectangle(cornerRadius: 12))
                                }
                                if sheet.id == gospelSheets.count - 1 {
                                    Text("Scripture quotations are from the World English Bible.")
                                        .font(.caption2)
                                        .foregroundStyle(.secondary)
                                }
                            }
                            .padding(24)
                        }
                        .tag(sheet.id)
                    }
                }
                .tabViewStyle(.page(indexDisplayMode: .never))

                HStack(spacing: 6) {
                    ForEach(0..<gospelSheets.count, id: \.self) { index in
                        Circle()
                            .fill(index == page ? Color.accentColor : Color(.systemGray4))
                            .frame(width: 8, height: 8)
                    }
                }
                .padding(.bottom, 12)

                HStack {
                    if page > 0 {
                        Button("Back") {
                            withAnimation { page -= 1 }
                        }
                    }
                    Spacer()
                    if page < gospelSheets.count - 1 {
                        Button("Next") {
                            withAnimation { page += 1 }
                        }
                        .buttonStyle(.borderedProminent)
                    } else {
                        Button("Close") {
                            dismiss()
                        }
                        .buttonStyle(.borderedProminent)
                    }
                }
                .padding(.horizontal, 24)
                .padding(.bottom, 16)
            }
            .toolbar {
                ToolbarItem(placement: .topBarTrailing) {
                    Button {
                        dismiss()
                    } label: {
                        Image(systemName: "xmark")
                    }
                }
            }
        }
    }
}

#Preview {
    GospelView()
}
