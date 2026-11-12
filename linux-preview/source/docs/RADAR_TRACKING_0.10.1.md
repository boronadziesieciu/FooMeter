# AscendAPP 0.10.1 — Radar tracking

## Start and controls

Close the previous copy, extract the COMPLETE Windows ZIP into a fresh folder and run AscendAPP.exe. The optional Source ZIP stays separate. Existing shared preferences, tickets and boss snapshots load automatically. The existing .NET 10 Desktop Runtime and Npcap prerequisites remain. No tools modified your real saved profile during development or testing.

Open the radar with Ctrl+Shift+F6, the main radar icon, the tray or gear → PvP radar. Leave Auto enabled and walk in a small circle while watching a known stationary player. Mouse wheel or +/− zooms without resetting capture, character identity, selected player or stored coordinates. The rotation button switches to manual view; Auto re-enables automatic following. Click a marker to track its name/class, range and last-seen age; click it again or click empty space to clear selection. Global Hide player names masks tracked details and tooltips too.

Cyan is confirmed party, green same faction, coral opposing faction and grey unknown. Fresh enemies have a larger triangle; retained last-seen contacts have faded dashed rings. A short line shows only earlier received movement over the last two seconds, not a predicted path. Known arena teams remain unknown outside your party.

## Refresh and precision

The radar samples new received state on WPF composition/display frames, independently of the damage meter's 100–2000 ms setting. Packet bursts are coalesced to their latest coordinates, with no one-callback-per-packet backlog. Between packet changes, age/retention updates run every 100 ms. Hidden windows unsubscribe from composition events. The main idle meter keeps its one-second-or-slower cadence. This avoids a busy polling loop and repeated full-meter work during zoom.

Capture requests Immediate=true, MinToCopy=0 and ReadTimeout=50 ms through the already bundled SharpPcap 6.3.1 API. Unsupported optional low-latency settings are reported in Debug; capture can continue with the short timeout. No adapter/driver success or fixed FPS is asserted from an offline test. The configuration follows the pinned primary [DeviceConfiguration API](https://github.com/dotpcap/sharppcap/blob/bfedf297e7410ffcf44e05d14f7fbef304f20895/SharpPcap/DeviceConfiguration.cs) and [live-device implementation](https://github.com/dotpcap/sharppcap/blob/bfedf297e7410ffcf44e05d14f7fbef304f20895/SharpPcap/LibPcap/LibPcapLiveDevice.cs).

Refresh cannot exceed the rate of real movement reports, WPF composition or available system resources. Markers use the latest received coordinate directly; there is no extrapolation, fabricated update rate or extra smoothing delay.

Own position additionally reads the explicit local-caster XYZ tail of supported effect apply/renew messages (0x2A38 / 0x2B38). An unknown identity, someone else's caster or an effect target cannot become the own origin. Skill aim/ground-target coordinates are not treated as the caster's position. A position arriving before its same-world own identity can be bound once that identity is verified.

Auto rotation follows travel inferred from successive own-position samples, at least 0.75 m apart and no more than five seconds/100 m apart. World +Y is up at zero rotation; eastward travel rotates the map 90 degrees so the travel direction is up. Tiny coordinate changes do not spin the map. With no new direction, the last observed travel heading is held; without any heading it falls back to the manual angle. Camera heading and stationary character facing are NOT decoded. Turning only the camera therefore does not rotate this map.

## Retention

The old 30-second own-origin cutoff is removed. Your last confirmed origin remains within the current world context, with an age warning after three seconds. It is not automatically replaced by a centroid or an estimated moving position. If fresh own reports are absent, relative positions may be offset from your current location; retaining an old origin cannot correct missing data.

Players remain for five minutes by default, configurable from 30 seconds to 30 minutes. The existing freshness window (default 15 seconds) now controls fresh versus retained styling, not immediate disappearance. Raw coordinates remain independent of zoom; zooming out can reveal a retained contact again. The nearest 128 are drawn, with total in-range count if capped. Identity/position/history storage is bounded, and crowded caches evict individual old entries instead of resetting the own character.

Real despawns, recycled identity evidence, teleports, world handovers and detected capture gaps still invalidate relevant coordinates and selection/heading context. These are necessary spatial boundaries. They do not erase the separate server-based field-boss countdown cache. Position/contact history stays in memory; only radar preferences persist across app launches. Existing strict self/party boss/dummy DPS rules are unchanged.

## Verification and remaining live check

412 core and 117 native WPF checks passed; Release build has zero warnings/errors. Checks cover all four cardinal rotations, jitter, confirmed short trails, old origin/contact retention, zoom out/back, cache pressure, effect tails and malformed fields, actual TCP movement, rendered colours including unknown, actual Auto/zoom controls, selected-name privacy, packet bursts and an independent two-second damage-meter rate. Previews are clearly labelled synthetic contacts. The user reported partial live visibility in 0.10.0; this release has no newly supplied real packet fixture or in-game FPS measurement.

Debug/report metadata includes decoded/rejected position counts, own-origin age, observed movement heading, range, retention, auto mode and requested/fallback capture mode. No raw coordinates, contact list or packet bodies are added to exported diagnostics.

If relative positions remain wrong, the minimum useful sanitized fixture is a short ordered interval with your own 0x3336 identity, 0x2136 zone prefix, 0x3C37/other supported own movement and any 0x2A38/0x2B38 own-caster XYZ reports, plus one known nearby player's 0x048D/0x4536 identity and corresponding 0x..37 movement. Include walking straight, a turn, stopping, a camera-only turn, timestamps, direction/stream boundaries and approximate known relative distance/direction. Include relevant client-to-server movement messages if no server-to-client own updates arrive. Preserve full relevant frames after reassembly/decompression; a diagnostics report alone cannot verify missing coordinate layouts.

Use same-byte-length name aliases and consistent entity IDs without changing varint widths or frame lengths. Remove authentication, account identifiers and chat. If translating positions, apply the same offset to all XY in the interval. See PVP_RADAR_0.10.0.md for the movement opcode list and framing details; its old retention/refresh behaviour is superseded by this document. No complete unreviewed session capture is needed.

## Provenance

The extra effect-tail layout is adapted from DecodeBuff in the same pinned MIT [SkeeveAN/Aion-DPS-Meter revision 8c14501afabb109f9c30963e1c6259a3479b5189](https://github.com/SkeeveAN/Aion-DPS-Meter/blob/8c14501afabb109f9c30963e1c6259a3479b5189/Client/Aion2/Protocol/Aion2FrameDecoder.cs). Full copyright/license notices accompany both packages. AscendAPP's rotation, retention, bounded caches, display coalescing, drawing and selection are independently implemented. No environment-derived own-position inference or incompatible code is imported.
