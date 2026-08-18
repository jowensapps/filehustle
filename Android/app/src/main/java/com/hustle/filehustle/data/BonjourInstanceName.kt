package com.hustle.filehustle.data

/**
 * Encodes/decodes the mDNS service instance name as `<id>|<name>` so a
 * peer's id and display name are both available directly from NSD's
 * discovery results — no dependency on TXT record resolution. Mirrors
 * iOS's BonjourInstanceName; see docs/protocol.md for why this is the
 * authoritative discovery mechanism on every platform, not a workaround.
 */
object BonjourInstanceName {
    private const val SEPARATOR = '|'

    fun encode(id: String, name: String): String {
        val safeName = name.replace(SEPARATOR, ' ')
        return "$id$SEPARATOR$safeName"
    }

    fun decode(instanceName: String): Pair<String, String>? {
        val separatorIndex = instanceName.indexOf(SEPARATOR)
        if (separatorIndex < 0) return null
        val id = instanceName.substring(0, separatorIndex)
        val name = instanceName.substring(separatorIndex + 1)
        if (id.isEmpty() || name.isEmpty()) return null
        return id to name
    }
}
