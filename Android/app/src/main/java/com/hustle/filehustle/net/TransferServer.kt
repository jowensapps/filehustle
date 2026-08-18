package com.hustle.filehustle.net

import android.content.Context
import android.os.Handler
import android.os.Looper
import android.util.Log
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import com.hustle.filehustle.data.TransferDirection
import com.hustle.filehustle.data.TransferHeader
import com.hustle.filehustle.data.TransferHistoryStore
import com.hustle.filehustle.data.TransferItem
import com.hustle.filehustle.data.TransferStatus
import kotlinx.coroutines.CancellableContinuation
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch
import kotlinx.coroutines.suspendCancellableCoroutine
import kotlinx.serialization.decodeFromString
import kotlinx.serialization.json.Json
import java.io.BufferedInputStream
import java.io.BufferedOutputStream
import java.io.ByteArrayOutputStream
import java.io.EOFException
import java.io.File
import java.io.FileInputStream
import java.io.FileOutputStream
import java.io.IOException
import java.io.OutputStream
import java.net.ServerSocket
import java.net.Socket
import java.util.zip.ZipInputStream

/**
 * Mirrors iOS's TransferServer: advertises nothing itself (that's
 * PeerDiscovery's job) but owns the TCP listener, the accept/decline
 * handshake, and writing received files to disk. See docs/protocol.md.
 */
object TransferServer {
    var incomingRequest by mutableStateOf<TransferHeader?>(null)
        private set

    @Volatile
    private var pendingContinuation: CancellableContinuation<Boolean>? = null
    private var serverSocket: ServerSocket? = null
    private lateinit var appContext: Context
    private val mainHandler = Handler(Looper.getMainLooper())

    private val receivedFilesDir: File by lazy {
        File(appContext.filesDir, "Received").apply { mkdirs() }
    }

    fun init(context: Context) {
        appContext = context.applicationContext
    }

    /** Starts listening on an OS-assigned port and returns it. */
    fun start(scope: CoroutineScope): Int {
        val socket = ServerSocket(0)
        serverSocket = socket
        scope.launch(Dispatchers.IO) {
            while (isActive) {
                val client = try {
                    socket.accept()
                } catch (e: IOException) {
                    break
                }
                launch(Dispatchers.IO) { handleConnection(client) }
            }
        }
        return socket.localPort
    }

    fun stop() {
        serverSocket?.close()
        serverSocket = null
    }

    /** Called by the UI once the user acts on `incomingRequest`. */
    fun respond(accept: Boolean) {
        pendingContinuation?.resumeWith(Result.success(accept))
        pendingContinuation = null
    }

    private suspend fun handleConnection(socket: Socket) {
        socket.use {
            // Hoisted so the catch block can still record a history entry
            // for a transfer that started (header parsed, sender known) but
            // failed partway through, instead of vanishing without a trace.
            var header: TransferHeader? = null
            try {
                val input = BufferedInputStream(it.getInputStream())
                val output = it.getOutputStream()
                val headerLine = input.readHeaderLine()
                val parsedHeader = Json.decodeFromString<TransferHeader>(headerLine)
                header = parsedHeader

                val accepted = suspendCancellableCoroutine { cont ->
                    pendingContinuation = cont
                    mainHandler.post { incomingRequest = parsedHeader }
                }
                mainHandler.post { incomingRequest = null }

                output.write(if (accepted) 1 else 0)
                output.flush()

                if (accepted) {
                    val itemFiles = receiveBody(parsedHeader, input)
                    recordHistory(parsedHeader, TransferStatus.COMPLETED, itemFiles)
                } else {
                    recordHistory(parsedHeader, TransferStatus.DECLINED)
                }
            } catch (e: Exception) {
                Log.e("TransferServer", "receive failed", e)
                mainHandler.post { incomingRequest = null }
                header?.let { recordHistory(it, TransferStatus.FAILED) }
            }
        }
    }

    private fun receiveBody(header: TransferHeader, input: BufferedInputStream): List<File> {
        val itemFiles = mutableListOf<File>()
        for (item in header.items) {
            val dest = File(receivedFilesDir, sanitize(item.relativePath))
            if (item.isFolder) {
                receiveFolderItem(item, dest, input)
            } else {
                dest.parentFile?.mkdirs()
                FileOutputStream(dest).use { out -> input.readExactly(item.size, out) }
            }
            itemFiles.add(dest)
        }
        return itemFiles
    }

    /** Folder items arrive as a single zip stream (see TransferClient's
     * `zipFolder`) — buffer it to a temp file, unzip into a directory named
     * after the item, then discard the zip. Entry names are sanitized
     * against zip-slip (`../` escaping the destination directory). */
    private fun receiveFolderItem(item: TransferItem, dest: File, input: BufferedInputStream) {
        val zipFile = File.createTempFile("filehustle-", ".zip")
        try {
            BufferedOutputStream(FileOutputStream(zipFile)).use { out -> input.readExactly(item.size, out) }

            dest.deleteRecursively()
            dest.mkdirs()
            ZipInputStream(BufferedInputStream(FileInputStream(zipFile))).use { zis ->
                var entry = zis.nextEntry
                while (entry != null) {
                    val sanitizedName = sanitize(entry.name)
                    if (sanitizedName.isNotEmpty()) {
                        val outFile = File(dest, sanitizedName)
                        if (entry.isDirectory) {
                            outFile.mkdirs()
                        } else {
                            outFile.parentFile?.mkdirs()
                            FileOutputStream(outFile).use { fos -> zis.copyTo(fos) }
                        }
                    }
                    zis.closeEntry()
                    entry = zis.nextEntry
                }
            }
        } finally {
            zipFile.delete()
        }
    }

    private fun sanitize(relativePath: String): String =
        relativePath.split("/").filter { it.isNotEmpty() && it != ".." }.joinToString("/")

    private fun recordHistory(header: TransferHeader, status: TransferStatus, itemFiles: List<File> = emptyList()) {
        TransferHistoryStore.record(
            peerName = header.senderName,
            itemNames = header.items.map { it.name },
            totalBytes = header.totalBytes,
            direction = TransferDirection.RECEIVED,
            status = status,
            itemFiles = itemFiles
        )
    }
}

private fun BufferedInputStream.readHeaderLine(): String {
    val bytes = ByteArrayOutputStream()
    while (true) {
        val b = read()
        if (b == -1) throw EOFException("connection closed before header")
        if (b == '\n'.code) break
        bytes.write(b)
    }
    return bytes.toString(Charsets.UTF_8.name())
}

private fun BufferedInputStream.readExactly(count: Long, out: OutputStream) {
    val buffer = ByteArray(64 * 1024)
    var remaining = count
    while (remaining > 0) {
        val toRead = minOf(buffer.size.toLong(), remaining).toInt()
        val read = read(buffer, 0, toRead)
        if (read == -1) throw EOFException("connection closed early")
        out.write(buffer, 0, read)
        remaining -= read
    }
}
