package com.hustle.filehustle.ui

import android.content.Intent
import android.net.Uri
import androidx.compose.animation.core.EaseInOutSine
import androidx.compose.animation.core.RepeatMode
import androidx.compose.animation.core.animateFloat
import androidx.compose.animation.core.infiniteRepeatable
import androidx.compose.animation.core.rememberInfiniteTransition
import androidx.compose.animation.core.tween
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Close
import androidx.compose.material.icons.filled.Email
import androidx.compose.material.icons.filled.Send
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.TopAppBar
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.scale
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.compose.ui.window.Dialog
import androidx.compose.ui.window.DialogProperties
import com.hustle.filehustle.BuildConfig

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun AboutScreen(onDismiss: () -> Unit) {
    val context = LocalContext.current
    var showGospelDialog by remember { mutableStateOf(false) }
    var showGospel by remember { mutableStateOf(false) }

    Surface(modifier = Modifier.fillMaxSize(), color = MaterialTheme.colorScheme.background) {
        Scaffold(
            topBar = {
                TopAppBar(
                    title = { Text("About") },
                    actions = {
                        IconButton(onClick = onDismiss) {
                            Icon(Icons.Default.Close, contentDescription = "Close")
                        }
                    }
                )
            }
        ) { innerPadding ->
            Column(
                modifier = Modifier
                    .fillMaxSize()
                    .padding(innerPadding)
                    .padding(horizontal = 24.dp),
                horizontalAlignment = Alignment.CenterHorizontally,
                verticalArrangement = Arrangement.Top
            ) {
                Spacer(Modifier.height(24.dp))
                Icon(
                    Icons.Default.Send,
                    contentDescription = null,
                    tint = MaterialTheme.colorScheme.primary,
                    modifier = Modifier.size(72.dp)
                )
                Spacer(Modifier.height(12.dp))
                Text(
                    "Version ${BuildConfig.VERSION_NAME}",
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
                Spacer(Modifier.height(24.dp))

                Row(
                    verticalAlignment = Alignment.CenterVertically,
                    horizontalArrangement = Arrangement.Center
                ) {
                    Text(
                        "FileHustle was developed by Jared Owens, founder and solo developer of J. O. Apps.",
                        style = MaterialTheme.typography.bodyMedium,
                        textAlign = TextAlign.Center,
                        modifier = Modifier.weight(1f, fill = false)
                    )
                    val crossPulse = rememberInfiniteTransition(label = "crossPulse")
                    val crossScale by crossPulse.animateFloat(
                        initialValue = 1f,
                        targetValue = 1.25f,
                        animationSpec = infiniteRepeatable(
                            animation = tween(900, easing = EaseInOutSine),
                            repeatMode = RepeatMode.Reverse
                        ),
                        label = "crossScale"
                    )
                    IconButton(
                        onClick = { showGospelDialog = true },
                        modifier = Modifier.size(28.dp)
                    ) {
                        Icon(
                            CrossIcon,
                            contentDescription = "A question about eternity",
                            tint = MaterialTheme.colorScheme.primary,
                            modifier = Modifier
                                .size(14.dp)
                                .scale(crossScale)
                        )
                    }
                }

                Spacer(Modifier.height(20.dp))

                TextButton(
                    onClick = {
                        val intent = Intent(Intent.ACTION_SENDTO).apply {
                            data = Uri.parse("mailto:j.owens.apps@gmail.com")
                            putExtra(Intent.EXTRA_SUBJECT, "FileHustle Bug Report")
                        }
                        context.startActivity(intent)
                    }
                ) {
                    Icon(Icons.Default.Email, contentDescription = null, modifier = Modifier.size(16.dp))
                    Spacer(Modifier.width(6.dp))
                    Text("Submit a Bug Report")
                }
            }
        }
    }

    if (showGospelDialog) {
        AlertDialog(
            onDismissRequest = { showGospelDialog = false },
            title = { Text("An honest question") },
            text = { Text("Do you know for sure that you will go to Heaven when you die?") },
            confirmButton = {
                TextButton(onClick = {
                    showGospelDialog = false
                    showGospel = true
                }) { Text("More") }
            },
            dismissButton = {
                TextButton(onClick = { showGospelDialog = false }) { Text("Close") }
            }
        )
    }

    if (showGospel) {
        Dialog(
            onDismissRequest = { showGospel = false },
            properties = DialogProperties(usePlatformDefaultWidth = false)
        ) {
            GospelScreen(onDismiss = { showGospel = false })
        }
    }
}
