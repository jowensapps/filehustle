package com.hustle.filehustle.data

import android.content.Context
import android.content.SharedPreferences
import java.util.UUID

/**
 * Stable per-install identity used as the mDNS instance-name id/name — see
 * docs/protocol.md. Not tied to any account. Mirrors iOS's DeviceIdentity.
 */
object DeviceIdentity {
    private const val PREFS_NAME = "com.hustle.filehustle.device_identity"
    private const val ID_KEY = "device_id"
    private const val NAME_KEY = "device_name"

    private lateinit var prefs: SharedPreferences

    fun init(context: Context) {
        if (::prefs.isInitialized) return
        prefs = context.applicationContext.getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE)
    }

    val id: String
        get() {
            prefs.getString(ID_KEY, null)?.let { return it }
            val newId = UUID.randomUUID().toString()
            prefs.edit().putString(ID_KEY, newId).apply()
            return newId
        }

    /**
     * Defaults to a generated name rather than the real device name — that
     * broadcasts in mDNS discovery to everyone on the WiFi, not just people
     * you actually transfer with. Generated once and persisted, same as
     * `id`. Still user-editable via the setter for a future rename
     * affordance. Word lists match iOS's DeviceIdentity for consistency.
     */
    var name: String
        get() {
            prefs.getString(NAME_KEY, null)?.let { return it }
            val generated = generateRandomName()
            prefs.edit().putString(NAME_KEY, generated).apply()
            return generated
        }
        set(value) {
            prefs.edit().putString(NAME_KEY, value).apply()
        }

    private fun generateRandomName(): String {
        val adjectives = listOf(
            "Quick", "Silent", "Amber", "Brave", "Calm", "Clever", "Cosmic",
            "Dusty", "Eager", "Fuzzy", "Gentle", "Golden", "Happy", "Hidden",
            "Jolly", "Keen", "Lucky", "Mellow", "Misty", "Nimble", "Plucky",
            "Quiet", "Rapid", "Sleepy", "Sunny", "Swift", "Tidy", "Vivid",
            "Witty", "Zesty",
        )
        val nouns = listOf(
            "Otter", "Falcon", "Maple", "River", "Comet", "Badger", "Ember",
            "Fox", "Heron", "Ibis", "Jaguar", "Kestrel", "Lynx", "Marlin",
            "Newt", "Orca", "Panda", "Quail", "Raven", "Sparrow", "Toucan",
            "Urchin", "Viper", "Walrus", "Yak", "Zebra", "Pelican", "Osprey",
            "Wombat", "Puffin",
        )
        return "${adjectives.random()} ${nouns.random()}"
    }
}
