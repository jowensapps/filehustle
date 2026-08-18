using System.Collections.Generic;

namespace FileHustle.Views;

public record GospelVerse(string Text, string Reference);

public record GospelSheet(string Title, string Body, List<GospelVerse> Verses);

// Scripture quotations are from the World English Bible (public domain).
// Kept byte-identical to the iOS/Android gospel content.
public static class GospelData
{
    public static readonly List<GospelSheet> Sheets = new()
    {
        new GospelSheet(
            "God loves you",
            "That question isn't meant to scare you — it's the most important question anyone can ask. The Bible says you can know the answer. It starts with this: God made you, God loves you, and He wants you with Him forever.",
            new List<GospelVerse>
            {
                new("For God so loved the world, that he gave his one and only Son, that whoever believes in him should not perish, but have eternal life.", "John 3:16"),
            }),
        new GospelSheet(
            "The problem: sin",
            "But there's a problem, and it's universal. Every one of us has done wrong — lied, been selfish, ignored God. The Bible calls this sin, and it separates us from a perfect God. Sin carries a penalty, and it's more than physical death.",
            new List<GospelVerse>
            {
                new("For all have sinned, and fall short of the glory of God.", "Romans 3:23"),
                new("For the wages of sin is death, but the free gift of God is eternal life in Christ Jesus our Lord.", "Romans 6:23"),
            }),
        new GospelSheet(
            "We can't earn it",
            "Most people hope their good outweighs their bad. But Heaven isn't earned by good deeds, religion, or effort — a perfect God can't be paid off with an imperfect record. If we could earn it, we wouldn't need a Savior.",
            new List<GospelVerse>
            {
                new("For by grace you have been saved through faith, and that not of yourselves; it is the gift of God, not of works, that no one would boast.", "Ephesians 2:8–9"),
            }),
        new GospelSheet(
            "God's answer: the cross",
            "So God did what we couldn't. Jesus Christ — God's Son — lived the perfect life we never could, then died on the cross taking the punishment our sin deserved. Then He rose from the dead, proving His payment was accepted.",
            new List<GospelVerse>
            {
                new("But God commends his own love toward us, in that while we were yet sinners, Christ died for us.", "Romans 5:8"),
                new("Christ died for our sins according to the Scriptures… he was buried… he was raised on the third day according to the Scriptures.", "1 Corinthians 15:3–4"),
            }),
        new GospelSheet(
            "The only way",
            "Jesus didn't claim to be a way to God — He claimed to be the only way. Forgiveness isn't found in being good enough or religious enough. It's found in a Person.",
            new List<GospelVerse>
            {
                new("Jesus said to him, 'I am the way, the truth, and the life. No one comes to the Father, except through me.'", "John 14:6"),
            }),
        new GospelSheet(
            "How to respond",
            "So what do you do? Turn from your sin, and trust in Jesus alone — not your own goodness — to save you. It's not a ritual or a transaction. It's placing your whole confidence in what He did for you.",
            new List<GospelVerse>
            {
                new("If you will confess with your mouth that Jesus is Lord, and believe in your heart that God raised him from the dead, you will be saved.", "Romans 10:9"),
                new("For, 'Whoever will call on the name of the Lord will be saved.'", "Romans 10:13"),
            }),
        new GospelSheet(
            "You can know",
            "If you're ready, you can tell God right now, in your own words: \"God, I know I've sinned. I believe Jesus died for my sins and rose again. I turn from my sin and trust Him alone to save me. Amen.\" If you meant that, the Bible says you can be sure — not hope, know.",
            new List<GospelVerse>
            {
                new("These things I have written to you who believe in the name of the Son of God, that you may know that you have eternal life.", "1 John 5:13"),
            }),
        new GospelSheet(
            "Where to go from here",
            "If you just put your trust in Christ — welcome to the family of God. Faith grows the same way it begins: by hearing from God. Get a Bible and start reading in the Gospel of John, talk to God honestly in prayer, and find a church that teaches the Bible, so you don't walk alone.",
            new List<GospelVerse>
            {
                new("So faith comes by hearing, and hearing by the word of God.", "Romans 10:17"),
            }),
    };
}
