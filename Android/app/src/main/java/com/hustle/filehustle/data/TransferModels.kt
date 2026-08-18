package com.hustle.filehustle.data

import kotlinx.serialization.Serializable
import java.io.File
import java.util.UUID

// Mirrors docs/protocol.md — keep both clients in sync if either changes.

@Serializable
data class TransferItem(
    val name: String,
    val relativePath: String,
    val size: Long,
    val isFolder: Boolean
)

@Serializable
data class TransferHeader(
    val senderId: String,
    val senderName: String,
    val totalBytes: Long,
    val items: List<TransferItem>
)

/**
 * A file or folder the user picked to send, before it's been prepared
 * (folders get zipped) into a [TransferItem] + the file whose bytes get
 * streamed — see `TransferClient.send`. Mirrors iOS's SendableItem.
 */
data class SendableItem(val file: File, val isFolder: Boolean)

enum class TransferDirection { SENT, RECEIVED }

enum class TransferStatus { COMPLETED, DECLINED, TIMED_OUT, FAILED }

data class TransferHistoryEntry(
    val id: String = UUID.randomUUID().toString(),
    val peerName: String,
    val itemNames: List<String>,
    val totalBytes: Long,
    val direction: TransferDirection,
    val timestamp: Long = System.currentTimeMillis(),
    val status: TransferStatus,
    /** Where each received item landed on disk, same order as [itemNames].
     * Empty for sent entries — there's nothing local to open/share. */
    val itemFiles: List<File> = emptyList()
)
