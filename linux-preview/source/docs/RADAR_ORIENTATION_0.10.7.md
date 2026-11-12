# AscendAPP 0.10.7 — Radar direction and connection stages

## What your screenshot means

“Waiting for a new game connection handshake” together with “Origin last seen 70s ago” means the optional continuous source has not acquired a valid outgoing session. The displayed offsets use a seventy-second-old own location. A contact receiving new packets does not make that own location current. More rendering frames cannot repair missing source data.

This release fixes misleading presentation, an initial TCP ordering issue, and an unnecessarily fixed public-key encoding check. It does **not** establish which of those capture conditions affected your game. No new real capture or live process lookup was used during development.

## Install and test

1. Close the previous AscendAPP. Extract the **whole Compact-Windows ZIP** into a fresh folder, then run AscendAPP.exe. Keep the optional Source ZIP separate. Shared settings, maps, timers and positions carry over; do not merge releases.
2. Radar gear → PvP radar → **Prepare continuous capture**. This explicitly enables the optional read-only lookup and starts capture if needed. Errors opening capture remain visible. The option remains off for a new profile until enabled.
3. **Fully exit Aion and open it again while AscendAPP remains open and capturing.** Teleporting, returning to character selection or retrying the lookup can retain the existing connection and cannot recover the missing beginning of its cipher stream. Do not reset your character repeatedly or stop capture after logging in.
4. Watch the radar settings stages: **New connection: seen**, login **verified**, then **movement reports** increasing as you walk without skills. The live status also distinguishes a rejected login format from a stream waiting for a key or fresh movement. If the stages do not progress, use Settings → Debug → Save diagnostics report. It contains stage counters, not packet bodies, endpoint addresses or keys.
5. In the radar footer, choose **View → Face up** to put fresh observed character facing ahead, **Map up** for a fixed reference, or **Travel up** for observed movement direction. Walking backwards or strafing can make travel and facing differ. Camera yaw is not independently measured.

This version does not alter the running game, automate its controls or bypass protection. Native lookup compatibility remains unverified. The development/test paths prohibit native lookup and live capture.

## Reading the radar

- A larger solid own arrow and subtle cone indicate **fresh observed character facing**. The cone is a schematic direction aid, **not a measurement of camera field of view**. Facing alignment affects the own arrow, rotation, contact filtering and relative bearings consistently.
- A hollow own arrow means recent observed **travel**, not camera direction. Facing/travel are withheld once their reports or the own origin are over three seconds old. An unknown direction shows “?”.
- A contact’s label includes distance; click it for clock direction and metres. Old contacts show last-seen offsets instead of current clock bearings. Hover the selected line for the full text. Contact labels avoid the own marker and each other when there is room.
- Enemy markers are diamonds: their shape no longer implies we know the enemy’s facing. Party and force retain their larger cyan/gold rings. Verified-player filtering, defeated-player removal, leader assist and privacy settings remain in place.
- **LAST-KNOWN VIEW · Ns** means the entire map is anchored to an old own location. All contacts become faded rings, the fresh count becomes zero, clock bearings and animated leader glow stop, and details explain which old origin the offsets use. Retained observations can be useful context, but are not current relative positions.
- **MAP** is the displayed map reference, not a claim of geographic north. With an imported, aligned zone image it points toward the image’s top; otherwise it is the grid reference. Optional maps use the existing Add / align zone map workflow; map images are not bundled or fetched automatically.

## Capture fixes and diagnostics

The client collector now records an observed SYN and seeds expected TCP sequence after it. An out-of-order first payload is held until the missing first bytes arrive. A duplicate initial SYN does not discard a valid stream; a new SYN on a reused tuple does. Bounded buffering, exact adapter/tuple/world identity, two validated clocks and structural setup validation are preserved.

The clear 0x1036 login record now parses the length-prefixed RSA PKCS#1 public key as DER, requires a 2048-bit modulus and valid odd exponent, and verifies the known revision/trailer structure. Synthetic tests cover the former 268-byte/exponent-3 encoding and a 270-byte/exponent-65537 encoding. **The latter is a compatibility test, not evidence your client sends that variant.** Unknown shapes remain unsupported. The collector does not guess missing RC4 bytes or recover arbitrary midstream cipher state.

Safe diagnostics distinguish: no outgoing flow; connection start not observed; a new connection waiting for a recognized login; rejected login candidates; login verified but own-world binding missing; session lookup pending; clocks validating; decoded movement; incomplete TCP ordering; and stale movement. Counters include flows, SYN seen, payload segments, complete framed bodies, login candidates/rejections and byte length, clocks and movement report count. Progress remains visible before character detection completes. No keys, login tokens, raw/decrypted payloads or endpoint tuples are added to reports.

## Validation

**653 core checks and 217 native WPF checks passed; Release builds have zero warnings/errors.** Fixtures cover RSA forms/rejections, initial payload reorder, SYN retransmission/reuse, exact binding, wrong keys, clocks, gaps, stale/future directions, four facing quadrants, offset consistency, shared square filtering, last-known-origin rendering, privacy and explicit view controls. Existing DPS, party, healing, death, teleport, timer and settings checks remain included.

Rendered demo previews show Face up, Map up and a seventy-second-old origin. All previews use synthetic, visibly labelled data. Tests and previews use isolated fixture directories; the native game lookup is disabled. No game process or real profile is read/written by these paths.

## If the live source still does not work

Start with the safe diagnostics JSON. Depending on its stage, the remaining evidence is:

- **Connection start absent:** repeat the full-game restart with capture already running. No packet upload is needed to try this.
- **New connection, login rejected/unrecognized:** a locally sanitized fixture containing the client SYN sequence/flags and the clear 0x1036 record with relative times, exact frame prefix/length, RSA DER shape/exponent, revision and trailer. Substitute a synthetic public key of the same encoding/length if desired; omit all subsequent authentication/account/setup payloads. If the expected record never appears, a local opcode/length-only trace of the first client frames is sufficient to identify that gap, without publishing payloads.
- **Login recognized, key lookup unavailable:** movement fixtures cannot fix a changed executable owner/signature or access restriction. This requires a compatible appropriately licensed reference or independent local analysis; **do not provide a process dump or real session key**.
- **Stream validated but movement missing/wrong:** a locally sanitized **decoded movement-only** fixture with opcodes 0037/0137/0337/0B37/0F37/1037/1137/1837, relative timestamps, exact lengths/flags and the performed sequence (walk forward/back/strafe, stop, rotate while still, dash, teleport). Translate all XYZ coordinates by one common offset, preserving relative positions and types. Pair it with the visible minimap direction and control mode. A full framing/cipher reproduction must use a **new synthetic key** and synthetic setup/authentication strings before re-encryption, as described in CONTINUOUS_POSITION_0.10.5.md. Never send an unsanitized PCAP.

## Minimap research and licensing

[NC’s HUD editing notice](https://lounge.plaync.com/feed/62397) documents per-character HUD settings and minimap resizing. It does not establish packet-to-compass axes or continuous camera yaw for this Global client. We do not assume a fixed screen position/size or call +Y geographic north.

The developer’s [Aion 2 Map Overlay README](https://github.com/NoRhythmDEV/aion2-map-overlay) describes reading the in-game minimap view cone for experimental camera-relative resource guidance, and withholding the heading when unclear. This supports investigating a future screen-only heading source. That repository had no root code license when checked on 2026-10-24; **no implementation code or artwork was copied**. This release’s cone and modes are an independent presentation of our observed facing, not a new screen reader. A screenshot crop/coordinate calibration and a reliable confidence check would be required for an actual visual source.

The existing MIT protocol research and full notices remain included: [lunixxx37, pinned fe2122f](https://github.com/lunixxx37/aion2-protocol-research/tree/fe2122f0e54e1de854c11e91e3c7bf51916a4f52) and [SkeeveAN, pinned 0302618](https://github.com/SkeeveAN/Aion-DPS-Meter/tree/03026182c4ae2059b41804189852af6e945e365a). The DER parser, SYN handling, diagnostics and radar orientation/presentation changes are implemented independently here. Existing class-artwork notices are unchanged.
