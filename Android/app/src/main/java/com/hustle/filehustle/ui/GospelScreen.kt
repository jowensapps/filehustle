package com.hustle.filehustle.ui

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.pager.HorizontalPager
import androidx.compose.foundation.pager.rememberPagerState
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Close
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.SolidColor
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.graphics.vector.path
import androidx.compose.ui.text.font.FontStyle
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import kotlinx.coroutines.launch

// Latin cross icon (Material Icons has no cross glyph). Tint via Icon's tint parameter.
val CrossIcon: ImageVector by lazy {
    ImageVector.Builder(
        name = "Cross",
        defaultWidth = 24.dp,
        defaultHeight = 24.dp,
        viewportWidth = 24f,
        viewportHeight = 24f
    ).apply {
        path(fill = SolidColor(Color.Black)) {
            moveTo(10.5f, 3f)
            lineTo(13.5f, 3f)
            lineTo(13.5f, 8f)
            lineTo(18.5f, 8f)
            lineTo(18.5f, 11f)
            lineTo(13.5f, 11f)
            lineTo(13.5f, 21f)
            lineTo(10.5f, 21f)
            lineTo(10.5f, 11f)
            lineTo(5.5f, 11f)
            lineTo(5.5f, 8f)
            lineTo(10.5f, 8f)
            close()
        }
    }.build()
}

private data class GospelVerse(val text: String, val reference: String)

private data class GospelSheet(
    val title: String,
    val body: String,
    val verses: List<GospelVerse>
)

// Scripture quotations are from the World English Bible (public domain).
private val gospelSheets = listOf(
    GospelSheet(
        title = "God loves you",
        body = "That question isn't meant to scare you — it's the most important question anyone can ask. The Bible says you can know the answer. It starts with this: God made you, God loves you, and He wants you with Him forever.",
        verses = listOf(
            GospelVerse(
                "For God so loved the world, that he gave his one and only Son, that whoever believes in him should not perish, but have eternal life.",
                "John 3:16"
            )
        )
    ),
    GospelSheet(
        title = "The problem: sin",
        body = "But there's a problem, and it's universal. Every one of us has done wrong — lied, been selfish, ignored God. The Bible calls this sin, and it separates us from a perfect God. Sin carries a penalty, and it's more than physical death.",
        verses = listOf(
            GospelVerse(
                "For all have sinned, and fall short of the glory of God.",
                "Romans 3:23"
            ),
            GospelVerse(
                "For the wages of sin is death, but the free gift of God is eternal life in Christ Jesus our Lord.",
                "Romans 6:23"
            )
        )
    ),
    GospelSheet(
        title = "We can't earn it",
        body = "Most people hope their good outweighs their bad. But Heaven isn't earned by good deeds, religion, or effort — a perfect God can't be paid off with an imperfect record. If we could earn it, we wouldn't need a Savior.",
        verses = listOf(
            GospelVerse(
                "For by grace you have been saved through faith, and that not of yourselves; it is the gift of God, not of works, that no one would boast.",
                "Ephesians 2:8–9"
            )
        )
    ),
    GospelSheet(
        title = "God's answer: the cross",
        body = "So God did what we couldn't. Jesus Christ — God's Son — lived the perfect life we never could, then died on the cross taking the punishment our sin deserved. Then He rose from the dead, proving His payment was accepted.",
        verses = listOf(
            GospelVerse(
                "But God commends his own love toward us, in that while we were yet sinners, Christ died for us.",
                "Romans 5:8"
            ),
            GospelVerse(
                "Christ died for our sins according to the Scriptures… he was buried… he was raised on the third day according to the Scriptures.",
                "1 Corinthians 15:3–4"
            )
        )
    ),
    GospelSheet(
        title = "The only way",
        body = "Jesus didn't claim to be a way to God — He claimed to be the only way. Forgiveness isn't found in being good enough or religious enough. It's found in a Person.",
        verses = listOf(
            GospelVerse(
                "Jesus said to him, 'I am the way, the truth, and the life. No one comes to the Father, except through me.'",
                "John 14:6"
            )
        )
    ),
    GospelSheet(
        title = "How to respond",
        body = "So what do you do? Turn from your sin, and trust in Jesus alone — not your own goodness — to save you. It's not a ritual or a transaction. It's placing your whole confidence in what He did for you.",
        verses = listOf(
            GospelVerse(
                "If you will confess with your mouth that Jesus is Lord, and believe in your heart that God raised him from the dead, you will be saved.",
                "Romans 10:9"
            ),
            GospelVerse(
                "For, 'Whoever will call on the name of the Lord will be saved.'",
                "Romans 10:13"
            )
        )
    ),
    GospelSheet(
        title = "You can know",
        body = "If you're ready, you can tell God right now, in your own words: \"God, I know I've sinned. I believe Jesus died for my sins and rose again. I turn from my sin and trust Him alone to save me. Amen.\" If you meant that, the Bible says you can be sure — not hope, know.",
        verses = listOf(
            GospelVerse(
                "These things I have written to you who believe in the name of the Son of God, that you may know that you have eternal life.",
                "1 John 5:13"
            )
        )
    ),
    GospelSheet(
        title = "Where to go from here",
        body = "If you just put your trust in Christ — welcome to the family of God. Faith grows the same way it begins: by hearing from God. Get a Bible and start reading in the Gospel of John, talk to God honestly in prayer, and find a church that teaches the Bible, so you don't walk alone.",
        verses = listOf(
            GospelVerse(
                "So faith comes by hearing, and hearing by the word of God.",
                "Romans 10:17"
            )
        )
    )
)

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun GospelScreen(onDismiss: () -> Unit) {
    val pagerState = rememberPagerState { gospelSheets.size }
    val scope = rememberCoroutineScope()
    val isLastPage = pagerState.currentPage == gospelSheets.size - 1

    Surface(modifier = Modifier.fillMaxSize(), color = MaterialTheme.colorScheme.background) {
        Scaffold(
            topBar = {
                TopAppBar(
                    title = {},
                    actions = {
                        IconButton(onClick = onDismiss) {
                            Icon(Icons.Default.Close, contentDescription = "Close")
                        }
                    }
                )
            }
        ) { innerPadding ->
            Column(modifier = Modifier
                .fillMaxSize()
                .padding(innerPadding)
            ) {
                HorizontalPager(
                    state = pagerState,
                    modifier = Modifier.weight(1f)
                ) { page ->
                    val sheet = gospelSheets[page]
                    Column(
                        modifier = Modifier
                            .fillMaxSize()
                            .verticalScroll(rememberScrollState())
                            .padding(horizontal = 24.dp, vertical = 8.dp),
                        verticalArrangement = Arrangement.spacedBy(16.dp)
                    ) {
                        Text(
                            sheet.title,
                            style = MaterialTheme.typography.headlineSmall,
                            fontWeight = FontWeight.Bold
                        )
                        Text(sheet.body, style = MaterialTheme.typography.bodyLarge)
                        sheet.verses.forEach { verse ->
                            Surface(
                                color = MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.4f),
                                shape = MaterialTheme.shapes.medium,
                                modifier = Modifier.fillMaxWidth()
                            ) {
                                Column(
                                    modifier = Modifier.padding(12.dp),
                                    verticalArrangement = Arrangement.spacedBy(6.dp)
                                ) {
                                    Text(
                                        "“${verse.text}”",
                                        style = MaterialTheme.typography.bodyMedium,
                                        fontStyle = FontStyle.Italic
                                    )
                                    Text(
                                        "— ${verse.reference}",
                                        style = MaterialTheme.typography.labelMedium,
                                        fontWeight = FontWeight.SemiBold,
                                        color = MaterialTheme.colorScheme.primary
                                    )
                                }
                            }
                        }
                        if (page == gospelSheets.size - 1) {
                            Text(
                                "Scripture quotations are from the World English Bible.",
                                style = MaterialTheme.typography.labelSmall,
                                color = MaterialTheme.colorScheme.onSurfaceVariant
                            )
                        }
                    }
                }

                Row(
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(top = 12.dp),
                    horizontalArrangement = Arrangement.spacedBy(6.dp, Alignment.CenterHorizontally)
                ) {
                    repeat(gospelSheets.size) { index ->
                        Box(
                            modifier = Modifier
                                .size(8.dp)
                                .background(
                                    if (index == pagerState.currentPage) MaterialTheme.colorScheme.primary
                                    else MaterialTheme.colorScheme.outlineVariant,
                                    CircleShape
                                )
                        )
                    }
                }

                Row(
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(horizontal = 24.dp, vertical = 16.dp),
                    horizontalArrangement = Arrangement.SpaceBetween,
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    if (pagerState.currentPage > 0) {
                        TextButton(onClick = {
                            scope.launch { pagerState.animateScrollToPage(pagerState.currentPage - 1) }
                        }) {
                            Text("Back")
                        }
                    } else {
                        Spacer(Modifier.width(1.dp))
                    }
                    Button(onClick = {
                        if (isLastPage) onDismiss()
                        else scope.launch { pagerState.animateScrollToPage(pagerState.currentPage + 1) }
                    }) {
                        Text(if (isLastPage) "Close" else "Next")
                    }
                }
            }
        }
    }
}
