# FileHustle session recap — 2026-09-23

Covers Android + iOS work from today's session. Windows wasn't touched — flagged below where it's worth checking for parity.

## Android

**Removed the foreground service entirely.**
- Deleted `net/TransferService.kt`, its `<service>` entry in `AndroidManifest.xml`, and the now-unused `POST_NOTIFICATIONS` / `FOREGROUND_SERVICE` / `FOREGROUND_SERVICE_DATA_SYNC` permissions.
- Removed the `ProcessLifecycleOwner` observer in `FileHustleApp.kt` that started/stopped it around background/foreground transitions.
- Net effect: Android now matches iOS — discovery and transfers only work while the app is foregrounded, no persistent notification keeping it alive in the background.

**Recent Transfers redesign** (`ui/home/HomeScreen.kt`)
- Wrapped the "Recent Transfers" list in a `Card` (matching the Nearby Devices card styling).
- Each `TransferHistoryRow` now defaults to one line (`maxLines = 1`, ellipsized) and expands to show full item names on tap — matches iOS's existing one-line-per-transfer behavior instead of listing every item name inline by default.

## iOS

**Fixed a real peer-list duplicate bug** (`Networking/PeerBrowser.swift`)
- `update()` was building `peers` straight from `NWBrowser`'s raw `Set<NWBrowser.Result>` with no dedup by decoded device id. Since `includePeerToPeer = true` is set, the same physical device can resolve via two separate result entries (e.g. WiFi + AWDL), producing two rows for one real device — this is what showed up as "Sleepy Fox" listed twice.
- Fixed by decoding all results first, then collapsing into a `[String: Peer]` keyed by id before sorting into the published list.
- **Windows already dedupes correctly** — `Net/PeerDiscovery.cs` keys peers by id in a dictionary and already has the equivalent fix (referenced in Android's code comments as "same bug fixed on Windows in 8433c65"). iOS was the one platform missing it; Android and Windows were already fine.

**Diagnosed a stale "ghost device" issue** (not a code bug — a dev-testing artifact)
- A leftover iOS Simulator, booted since the previous session and left running in the background, kept FileHustle's Bonjour advertisement alive under a random device name, making it look like a mystery 4th device on the network.
- Root cause of why it lingered even after killing the simulator: it was killed abruptly (`kill` + `simctl shutdown` from Terminal) rather than quit normally, so no Bonjour "goodbye" packet was sent. The stale record sat in the iPad's own on-device mDNS cache — below the app layer, untouched by the app's own 60s browser-restart cycle *and* by toggling WiFi off in Control Center (Apple's peer-to-peer/AWDL layer stays up through that toggle). Toggling Airplane Mode on/off forced a full radio reset and cleared it.
- In normal use (a user closing the app normally, not a simulator getting force-killed via Terminal), this doesn't happen — the OS sends the goodbye packet on a clean exit. No code change needed for this one; it was flagged as a dev-hygiene note (shut test simulators down cleanly going forward).

## Versions

- **Android**: 1.1.4 (7) → **1.1.5 (8)**. Release AAB cut: `Android/app/release/app-release_01.aab`, ready for Play Console.
- **iOS**: 1.0.1 (6) → **1.0.2 (7)** in `project.pbxproj`. Not yet archived/uploaded — that's being done manually through Xcode.

## Worth checking on Windows

- Confirm `PeerDiscovery.cs` sends a proper Bonjour de-registration ("goodbye") on normal app shutdown, given the iOS ghost-device root cause above — if Windows already does this cleanly, no action needed, just worth a quick spot-check since it wasn't directly verified this session.
- No equivalent of the Android foreground-service removal applies to Windows (desktop apps don't have this constraint), and no equivalent of the Recent Transfers card/expand redesign was requested for Windows — only mentioning both so nothing looks skipped.
