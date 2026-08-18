package com.hustle.filehustle.data

import androidx.compose.runtime.mutableStateListOf
import java.io.File

/**
 * Shared by the receive path (TransferServer) and send path (TransferClient)
 * so both directions land in one list. Mirrors iOS's TransferHistoryStore.
 *
 * Phase 4 TODO: persist this with Room instead of in-memory only.
 */
object TransferHistoryStore {
    val entries = mutableStateListOf<TransferHistoryEntry>()

    fun record(
        peerName: String,
        itemNames: List<String>,
        totalBytes: Long,
        direction: TransferDirection,
        status: TransferStatus,
        itemFiles: List<File> = emptyList()
    ) {
        entries.add(
            0,
            TransferHistoryEntry(
                peerName = peerName,
                itemNames = itemNames,
                totalBytes = totalBytes,
                direction = direction,
                status = status,
                itemFiles = itemFiles
            )
        )
    }
}
