package com.hustle.filehustle.ui.home

import android.content.ActivityNotFoundException
import android.content.ContentResolver
import android.content.Context
import android.content.Intent
import android.net.Uri
import android.provider.OpenableColumns
import android.webkit.MimeTypeMap
import android.widget.Toast
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.ExperimentalFoundationApi
import androidx.compose.foundation.combinedClickable
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Info
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.DropdownMenu
import androidx.compose.material3.DropdownMenuItem
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.ListItem
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.unit.dp
import androidx.compose.ui.window.Dialog
import androidx.compose.ui.window.DialogProperties
import androidx.core.content.FileProvider
import androidx.documentfile.provider.DocumentFile
import com.hustle.filehustle.BuildConfig
import com.hustle.filehustle.data.DeviceIdentity
import com.hustle.filehustle.data.SendableItem
import com.hustle.filehustle.data.TransferDirection
import com.hustle.filehustle.data.TransferHistoryEntry
import com.hustle.filehustle.data.TransferHistoryStore
import com.hustle.filehustle.data.TransferStatus
import com.hustle.filehustle.net.Peer
import com.hustle.filehustle.net.PeerDiscovery
import com.hustle.filehustle.net.TransferClient
import com.hustle.filehustle.net.TransferServer
import com.hustle.filehustle.ui.AboutScreen
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import java.io.File
import kotlin.math.ln
import kotlin.math.pow

@OptIn(ExperimentalFoundationApi::class)
@Composable
fun HomeScreen() {
    val context = LocalContext.current
    val scope = rememberCoroutineScope()
    var selectedPeer by remember { mutableStateOf<Peer?>(null) }
    var isSending by remember { mutableStateOf(false) }
    var errorMessage by remember { mutableStateOf<String?>(null) }
    var menuPeerId by remember { mutableStateOf<String?>(null) }
    var showAbout by remember { mutableStateOf(false) }

    fun sendItems(items: List<SendableItem>, peer: Peer) {
        isSending = true
        scope.launch {
            try {
                TransferClient.send(items, peer)
            } catch (e: TransferClient.DeclinedException) {
                errorMessage = "${peer.name} declined the transfer."
            } catch (e: TransferClient.TimedOutException) {
                errorMessage = "${peer.name} didn't respond in time."
            } catch (e: Exception) {
                errorMessage = e.message ?: "Something went wrong."
            } finally {
                isSending = false
            }
        }
    }

    fun sendFiles(files: List<File>, peer: Peer) {
        sendItems(files.map { SendableItem(it, isFolder = false) }, peer)
    }

    val filePicker = rememberLauncherForActivityResult(ActivityResultContracts.OpenMultipleDocuments()) { uris ->
        val peer = selectedPeer ?: return@rememberLauncherForActivityResult
        if (uris.isEmpty()) return@rememberLauncherForActivityResult
        val files = uris.map { copyToCacheFile(context.contentResolver, it, context.cacheDir) }
        sendFiles(files, peer)
    }

    val folderPicker = rememberLauncherForActivityResult(ActivityResultContracts.OpenDocumentTree()) { treeUri ->
        val peer = selectedPeer ?: return@rememberLauncherForActivityResult
        if (treeUri == null) return@rememberLauncherForActivityResult
        isSending = true
        scope.launch {
            try {
                val folder = withContext(Dispatchers.IO) { copyDocumentTreeToCache(context, treeUri, context.cacheDir) }
                sendItems(listOf(SendableItem(folder, isFolder = true)), peer)
            } catch (e: Exception) {
                isSending = false
                errorMessage = e.message ?: "Couldn't read that folder."
            }
        }
    }

    Scaffold { innerPadding ->
        Box(modifier = Modifier.fillMaxSize().padding(innerPadding)) {
            LazyColumn(modifier = Modifier.fillMaxSize().padding(horizontal = 16.dp)) {
                item {
                    Row(
                        modifier = Modifier.fillMaxWidth().padding(vertical = 24.dp),
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        Text(
                            "FileHustle — ${DeviceIdentity.name}",
                            style = MaterialTheme.typography.headlineMedium,
                            modifier = Modifier.weight(1f)
                        )
                        IconButton(onClick = { showAbout = true }) {
                            Icon(Icons.Default.Info, contentDescription = "About")
                        }
                    }
                }
                item {
                    Text(
                        "Nearby Devices",
                        style = MaterialTheme.typography.labelLarge,
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                }
                if (PeerDiscovery.peers.isEmpty()) {
                    item {
                        Text(
                            "Looking for devices on your WiFi network…",
                            color = MaterialTheme.colorScheme.onSurfaceVariant,
                            modifier = Modifier.padding(vertical = 12.dp)
                        )
                    }
                }
                items(PeerDiscovery.peers, key = { it.id }) { peer ->
                    Box {
                        ListItem(
                            headlineContent = { Text(peer.name) },
                            leadingContent = { Text("📱") },
                            modifier = Modifier.combinedClickable(
                                enabled = !isSending,
                                onClick = {
                                    selectedPeer = peer
                                    if (BuildConfig.DEBUG) {
                                        // Skips the system document picker,
                                        // which isn't drivable from a
                                        // headless UI test — exercises the
                                        // same send path with a generated
                                        // file. Mirrors iOS's debug hook.
                                        sendFiles(listOf(createDebugTestFile(context.cacheDir)), peer)
                                    } else {
                                        filePicker.launch(arrayOf("*/*"))
                                    }
                                },
                                onLongClick = { menuPeerId = peer.id }
                            )
                        )
                        DropdownMenu(
                            expanded = menuPeerId == peer.id,
                            onDismissRequest = { menuPeerId = null }
                        ) {
                            DropdownMenuItem(
                                text = { Text("Send Files…") },
                                onClick = {
                                    menuPeerId = null
                                    selectedPeer = peer
                                    filePicker.launch(arrayOf("*/*"))
                                }
                            )
                            DropdownMenuItem(
                                text = { Text("Send Folder…") },
                                onClick = {
                                    menuPeerId = null
                                    selectedPeer = peer
                                    folderPicker.launch(null)
                                }
                            )
                            if (BuildConfig.DEBUG) {
                                DropdownMenuItem(
                                    text = { Text("Send Test Folder") },
                                    onClick = {
                                        menuPeerId = null
                                        sendItems(listOf(SendableItem(createDebugTestFolder(context.cacheDir), isFolder = true)), peer)
                                    }
                                )
                            }
                        }
                    }
                }
                item { Spacer(Modifier.height(24.dp)) }
                item {
                    Text(
                        "Recent Transfers",
                        style = MaterialTheme.typography.labelLarge,
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                }
                if (TransferHistoryStore.entries.isEmpty()) {
                    item {
                        Text(
                            "No transfers yet",
                            color = MaterialTheme.colorScheme.onSurfaceVariant,
                            modifier = Modifier.padding(vertical = 12.dp)
                        )
                    }
                }
                items(TransferHistoryStore.entries, key = { it.id }) { entry ->
                    TransferHistoryRow(entry)
                }
            }

            if (isSending) {
                Surface(
                    modifier = Modifier.align(Alignment.Center),
                    shape = MaterialTheme.shapes.medium,
                    tonalElevation = 4.dp
                ) {
                    Row(modifier = Modifier.padding(16.dp), verticalAlignment = Alignment.CenterVertically) {
                        CircularProgressIndicator(modifier = Modifier.size(20.dp))
                        Spacer(Modifier.width(12.dp))
                        Text("Sending…")
                    }
                }
            }
        }
    }

    TransferServer.incomingRequest?.let { header ->
        AlertDialog(
            onDismissRequest = { TransferServer.respond(false) },
            title = { Text("${header.senderName} wants to send you") },
            text = {
                Column {
                    header.items.forEach { item -> Text((if (item.isFolder) "📁 " else "") + item.name) }
                    Spacer(Modifier.height(8.dp))
                    Text(formatByteCount(header.totalBytes), color = MaterialTheme.colorScheme.onSurfaceVariant)
                }
            },
            confirmButton = {
                TextButton(onClick = { TransferServer.respond(true) }) { Text("Accept") }
            },
            dismissButton = {
                TextButton(onClick = { TransferServer.respond(false) }) { Text("Decline") }
            }
        )
    }

    errorMessage?.let { message ->
        AlertDialog(
            onDismissRequest = { errorMessage = null },
            title = { Text("Couldn't send files") },
            text = { Text(message) },
            confirmButton = {
                TextButton(onClick = { errorMessage = null }) { Text("OK") }
            }
        )
    }

    if (showAbout) {
        Dialog(
            onDismissRequest = { showAbout = false },
            properties = DialogProperties(usePlatformDefaultWidth = false)
        ) {
            AboutScreen(onDismiss = { showAbout = false })
        }
    }
}

@Composable
private fun TransferHistoryRow(entry: TransferHistoryEntry) {
    val context = LocalContext.current
    Row(modifier = Modifier.fillMaxWidth().padding(vertical = 8.dp), verticalAlignment = Alignment.CenterVertically) {
        Text(if (entry.direction == TransferDirection.SENT) "↑" else "↓")
        Spacer(Modifier.width(12.dp))
        Column(modifier = Modifier.weight(1f)) {
            Text(entry.itemNames.joinToString(", "))
            val directionLabel = if (entry.direction == TransferDirection.SENT) "To" else "From"
            val statusSuffix = when (entry.status) {
                TransferStatus.COMPLETED -> ""
                TransferStatus.DECLINED -> " · Declined"
                TransferStatus.TIMED_OUT -> " · Timed out"
                TransferStatus.FAILED -> " · Failed"
            }
            Text(
                "$directionLabel ${entry.peerName} · ${formatByteCount(entry.totalBytes)}$statusSuffix",
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant
            )
        }
        // Received files land in the app's own sandbox with no way out
        // otherwise — this is the "download it" affordance, via a
        // FileProvider content:// URI (Android blocks handing out raw
        // file:// paths to other apps as of API 24+).
        if (entry.direction == TransferDirection.RECEIVED && entry.status == TransferStatus.COMPLETED && entry.itemFiles.isNotEmpty()) {
            TextButton(onClick = { openReceivedFile(context, entry.itemFiles.first()) }) {
                Text("Open")
            }
        }
    }
}

private fun openReceivedFile(context: Context, file: File) {
    if (file.isDirectory) {
        Toast.makeText(context, "Saved to ${file.absolutePath}", Toast.LENGTH_LONG).show()
        return
    }
    val uri = FileProvider.getUriForFile(context, "com.hustle.filehustle.fileprovider", file)
    val mime = MimeTypeMap.getSingleton().getMimeTypeFromExtension(file.extension) ?: "*/*"
    val intent = Intent(Intent.ACTION_VIEW).apply {
        setDataAndType(uri, mime)
        addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION)
        addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)
    }
    try {
        context.startActivity(intent)
    } catch (e: ActivityNotFoundException) {
        Toast.makeText(context, "No app found to open this file", Toast.LENGTH_SHORT).show()
    }
}

private fun formatByteCount(bytes: Long): String {
    if (bytes < 1024) return "$bytes bytes"
    val exp = (ln(bytes.toDouble()) / ln(1024.0)).toInt()
    val unit = "KMGTPE"[exp - 1]
    return String.format("%.1f %sB", bytes / 1024.0.pow(exp.toDouble()), unit)
}

private fun copyToCacheFile(resolver: ContentResolver, uri: Uri, cacheDir: File): File {
    val name = queryDisplayName(resolver, uri) ?: "file-${System.currentTimeMillis()}"
    val dest = File(cacheDir, name)
    resolver.openInputStream(uri)?.use { input ->
        dest.outputStream().use { output -> input.copyTo(output) }
    }
    return dest
}

private fun queryDisplayName(resolver: ContentResolver, uri: Uri): String? {
    resolver.query(uri, null, null, null, null)?.use { cursor ->
        val nameIndex = cursor.getColumnIndex(OpenableColumns.DISPLAY_NAME)
        if (nameIndex >= 0 && cursor.moveToFirst()) {
            return cursor.getString(nameIndex)
        }
    }
    return null
}

/** Copies a picked folder tree (content:// document tree) into a plain
 * local directory so TransferClient can zip it like any other File. */
private fun copyDocumentTreeToCache(context: Context, treeUri: Uri, cacheDir: File): File {
    val documentTree = DocumentFile.fromTreeUri(context, treeUri) ?: error("Could not open folder")
    val destRoot = File(cacheDir, documentTree.name ?: "folder-${System.currentTimeMillis()}")
    destRoot.mkdirs()
    copyDocumentFileRecursively(context, documentTree, destRoot)
    return destRoot
}

private fun copyDocumentFileRecursively(context: Context, doc: DocumentFile, destDir: File) {
    doc.listFiles().forEach { child ->
        if (child.isDirectory) {
            val childDir = File(destDir, child.name ?: "folder")
            childDir.mkdirs()
            copyDocumentFileRecursively(context, child, childDir)
        } else {
            val destFile = File(destDir, child.name ?: "file")
            context.contentResolver.openInputStream(child.uri)?.use { input ->
                destFile.outputStream().use { output -> input.copyTo(output) }
            }
        }
    }
}

private fun createDebugTestFile(cacheDir: File): File {
    val file = File(cacheDir, "filehustle-test-${System.currentTimeMillis()}.txt")
    file.writeText("Hello from FileHustle Android debug test, sent at ${java.util.Date()}.")
    return file
}

/** Bypasses the system folder picker (same reasoning as
 * createDebugTestFile) — builds a small nested folder so the zip/unzip path
 * can be exercised end-to-end without manual interaction. */
private fun createDebugTestFolder(cacheDir: File): File {
    val folder = File(cacheDir, "filehustle-test-folder-${System.currentTimeMillis()}")
    folder.mkdirs()
    File(folder, "one.txt").writeText("First file")
    File(folder, "two.txt").writeText("Second file")
    val nested = File(folder, "nested")
    nested.mkdirs()
    File(nested, "three.txt").writeText("Nested file")
    return folder
}
