package com.hustle.filehustle.net

import com.hustle.filehustle.data.DeviceIdentity
import com.hustle.filehustle.data.SendableItem
import com.hustle.filehustle.data.TransferDirection
import com.hustle.filehustle.data.TransferHeader
import com.hustle.filehustle.data.TransferHistoryStore
import com.hustle.filehustle.data.TransferItem
import com.hustle.filehustle.data.TransferStatus
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import kotlinx.serialization.encodeToString
import kotlinx.serialization.json.Json
import java.io.BufferedOutputStream
import java.io.EOFException
import java.io.File
import java.io.FileOutputStream
import java.io.OutputStream
import java.net.Socket
import java.net.SocketTimeoutException
import java.util.zip.ZipEntry
import java.util.zip.ZipOutputStream

/** Mirrors iOS's TransferClient. See docs/protocol.md. */
object TransferClient {
    class DeclinedException : Exception("declined")
    class TimedOutException : Exception("timed out")

    private const val ACK_TIMEOUT_MS = 60_000

    suspend fun send(items: List<SendableItem>, peer: Peer) = withContext(Dispatchers.IO) {
        val tempZips = mutableListOf<File>()
        try {
            val prepared = items.map { item ->
                if (item.isFolder) {
                    val zipFile = File.createTempFile("filehustle-", ".zip")
                    tempZips.add(zipFile)
                    zipFolder(item.file, zipFile)
                    TransferItem(name = item.file.name, relativePath = item.file.name, size = zipFile.length(), isFolder = true) to zipFile
                } else {
                    TransferItem(name = item.file.name, relativePath = item.file.name, size = item.file.length(), isFolder = false) to item.file
                }
            }

            val header = TransferHeader(
                senderId = DeviceIdentity.id,
                senderName = DeviceIdentity.name,
                totalBytes = prepared.sumOf { it.first.size },
                items = prepared.map { it.first }
            )

            try {
                Socket(peer.host, peer.port).use { socket ->
                    socket.soTimeout = ACK_TIMEOUT_MS
                    val output = socket.getOutputStream()
                    val input = socket.getInputStream()

                    val headerBytes = (Json.encodeToString(header) + "\n").toByteArray(Charsets.UTF_8)
                    output.write(headerBytes)
                    output.flush()

                    val ackByte = try {
                        input.read()
                    } catch (e: SocketTimeoutException) {
                        throw TimedOutException()
                    }
                    if (ackByte == -1) throw EOFException("connection closed before ack")
                    if (ackByte != 1) throw DeclinedException()

                    for ((_, streamFile) in prepared) {
                        streamFile.inputStream().use { fileInput -> fileInput.copyToStream(output) }
                    }
                }
                TransferHistoryStore.record(peer.name, header.items.map { it.name }, header.totalBytes, TransferDirection.SENT, TransferStatus.COMPLETED)
            } catch (e: TimedOutException) {
                TransferHistoryStore.record(peer.name, header.items.map { it.name }, header.totalBytes, TransferDirection.SENT, TransferStatus.TIMED_OUT)
                throw e
            } catch (e: DeclinedException) {
                TransferHistoryStore.record(peer.name, header.items.map { it.name }, header.totalBytes, TransferDirection.SENT, TransferStatus.DECLINED)
                throw e
            } catch (e: Exception) {
                TransferHistoryStore.record(peer.name, header.items.map { it.name }, header.totalBytes, TransferDirection.SENT, TransferStatus.FAILED)
                throw e
            }
        } finally {
            tempZips.forEach { it.delete() }
        }
    }

    /** No compression-method fanciness needed — just walk and zip every
     * regular file, path relative to the folder root, forward-slash
     * separated to match what TransferServer.receiveFolderItem expects. */
    private fun zipFolder(folder: File, destinationZip: File) {
        ZipOutputStream(BufferedOutputStream(FileOutputStream(destinationZip))).use { zos ->
            val basePath = folder.toPath()
            folder.walkTopDown().filter { it.isFile }.forEach { file ->
                val relativePath = basePath.relativize(file.toPath()).toString().replace(File.separatorChar, '/')
                zos.putNextEntry(ZipEntry(relativePath))
                file.inputStream().use { input -> input.copyTo(zos) }
                zos.closeEntry()
            }
        }
    }
}

private fun java.io.InputStream.copyToStream(out: OutputStream) {
    val buffer = ByteArray(64 * 1024)
    while (true) {
        val read = read(buffer)
        if (read == -1) break
        out.write(buffer, 0, read)
    }
}
