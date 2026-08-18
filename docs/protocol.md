# FileHustle Wire Protocol v1

Single source of truth both the iOS and Android clients implement against.
Neither client should special-case the other's platform — if a change is
needed, change this doc first, then both clients.

## Discovery

- Service type: `_filehustle._tcp.` (mDNS/Bonjour/DNS-SD)
- Every device that has FileHustle open advertises this service on the local
  network and browses for others advertising it.
- TXT record keys (published for any client that resolves them directly, but
  see the instance-name note below for the primary/reliable path):
  - `id` — stable UUID, generated once per install and persisted locally.
    Not tied to any account; purely a "have I seen this device before" key
    for the local trusted/known-devices list.
  - `name` — display name shown in the peer list. Defaults to a randomly
    generated two-word name (e.g. "Tidy Yak"), not the real OS device name —
    broadcasting a real name like "Jared's iPhone" to everyone on the WiFi
    (not just people you transfer with) is a privacy leak on any shared
    network. Generated once and persisted like `id`; user-editable in-app.
  - `ver` — protocol version, currently `1`. A client MUST ignore peers
    advertising a `ver` it doesn't understand rather than attempt a transfer.
- **Bonjour instance name is the primary source for id + name, not the TXT
  record.** Every advertiser MUST encode its instance name as `<id>|<name>`
  (see iOS's `BonjourInstanceName.encode`/`.decode`) and browsers MUST parse
  id/name from there rather than relying on TXT resolution. This isn't
  belt-and-suspenders: on iOS Simulator, `NWBrowser.Result.metadata` was
  observed to never resolve TXT records at all (always `.none`, confirmed
  2026-08-16 testing between an iPhone 11 Pro Max and iPad 10th gen simulator
  on iOS 26.5) even though the underlying mDNS TXT record was published and
  resolvable correctly via `dns-sd -L` at the OS level — browsing the peer
  list without the instance-name fallback showed a "Nearby device" fallback
  string in place of the real name. Real devices and Android's NsdManager
  are expected to resolve TXT more reliably, but implement the
  instance-name path as the primary mechanism on every platform regardless —
  not a workaround to delete once "real" TXT resolution seems to work,
  since it's simpler and has no resolution timing/race to reason about at
  all. The TXT record stays published for completeness/future use.
- The service's advertised port is the TCP port the same device listens on
  for incoming transfers (standard SRV record behavior — no separate port
  field needed in the TXT record).

## Transfer session

One TCP connection = one transfer session (one or more files/folders sent in
one batch, one direction). Sender opens the connection; receiver is always
the mDNS-advertising listener.

### 1. Header

Sender writes a single UTF-8 JSON object terminated by `\n` (newline-
delimited, not length-prefixed — headers are always small):

```json
{
  "senderId": "3F2A1B4C-...",
  "senderName": "Jared's iPhone",
  "totalBytes": 4831201,
  "items": [
    { "name": "vacation.jpg", "relativePath": "vacation.jpg", "size": 2044213, "isFolder": false },
    { "name": "notes", "relativePath": "notes", "size": 2786988, "isFolder": true }
  ]
}
```

- `relativePath` is always forward-slash-separated, relative to the transfer
  root — used unchanged as the write path on the receiving side (after
  sanitizing `..` segments, which a receiver MUST reject).
- A folder item (`isFolder: true`) is sent as a single zip stream under that
  `size`; the receiver unzips it after the transfer completes and discards
  the intermediate zip. This is the only container format in v1 — no
  incremental directory tree protocol.

### 2. Accept / decline

Receiver's app surfaces `senderName`, the list of item names, and
`totalBytes` (human-formatted) in an Accept/Decline prompt. **Every transfer
requires this prompt — there is no silent/trusted auto-accept in v1**, even
for a previously-seen `senderId`. A known `senderId` only changes how the
prompt displays the sender (friendly remembered name) and what gets logged.

Receiver writes exactly one byte back on the same connection:
- `0x01` — accepted, sender should proceed to step 3
- `0x00` — declined, sender closes the connection and marks the transfer
  `declined` in its own history

If the receiver doesn't respond within 60 seconds (user didn't act on the
prompt), the sender times out, closes the connection, and marks the transfer
`timed_out`.

### 3. Body

Sender streams each item's raw bytes back-to-back, in the exact order listed
in `items`, no delimiters between them — the receiver already knows each
item's `size` from the header and reads exactly that many bytes before
moving to the next item. Folder items are the raw bytes of the zip archive.

### 4. Completion

Connection closes cleanly after the last byte of the last item. Both sides
append a row to their local transfer history: peer name, item names,
`totalBytes`, direction, timestamp, and status (`completed` / `declined` /
`timed_out` / `failed`).

## Explicitly out of scope for v1

- No resume/retry of a partial transfer — a failed transfer is just marked
  `failed` and must be re-sent from scratch.
- No encryption beyond whatever the OS does at the network layer (this is a
  local-LAN protocol, not designed for hostile networks). Client isolation
  on public/guest WiFi will simply prevent discovery — this is a known
  limitation, not a bug to fix.
- No relay/cloud fallback if two devices can't reach each other on the LAN.
