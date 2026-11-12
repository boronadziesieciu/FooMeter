# AscendAPP 0.10.0 — PvP radar

## Use

Close the previous app, extract the entire Windows ZIP into a new folder and run AscendAPP.exe. Do not merge the Windows and Source packages. The .NET 10 Desktop Runtime and Npcap remain separate prerequisites, as in the previous build. Existing preferences and timer snapshots load from %LOCALAPPDATA%\AscendAPP automatically.

Use the radar icon in the main compact overlay, Ctrl+Shift+F6, the tray menu, or Settings → PvP radar. Closing the radar hides only that window. The radar can remain open while the damage overlay is hidden. Its enabled state is saved.

- Cyan circle: confirmed party member.
- Green circle: player whose confirmed faction matches yours.
- Coral triangle: player of the opposing confirmed faction.
- Hollow grey circle: allegiance unknown or contradictory. Known arena zones leave non-party team allegiance unknown.
- Centre ring: your last reported position, never the average of other players.

Mouse-wheel or + / − changes radius from 25 to 500 metres. The rotation button turns the view 90 degrees; settings allow a manual angle. Labels are optional and limited to nearby contacts; hover shows observed distance and age. Global Hide player names also masks radar names and tooltips. The radar has its own position lock and saved placement. Shared UI scale, opacity, game focus and click-through apply. Ctrl+Shift+F12 restores click interaction; hotkeys can be changed in Keybinds. Reset radar position restores its default placement.

## What the first version knows

This is a radar-style minimap with distance rings, not a terrain map. World +Y is up at rotation zero. Geographic north, camera heading, terrain, altitude and arena team assignments are not decoded. Distance is planar XY distance using the reference protocol's centimetre scale, which still needs live verification.

A marker requires both a received position and player identity evidence from a server-bearing appearance, an explicit faction field, or a confirmed party link. A bare unidentified moving entity is excluded. Faction is read from explicit character/nickname data or recognized server blocks; conflicting evidence stays neutral. No configured character name or class-only identity inference is used.

Dots fade and expire after 15 seconds by default (configurable 5–60). Only the nearest 128 contacts are drawn; the full in-range count remains visible if capped. A missing or more-than-30-second-old own position hides relative contacts and explains why. Moving may produce a fresh report, but this is not guaranteed until the live packet layouts are checked. Stale own position is not silently held forever or extrapolated.

Every accepted zone/teleport clears old coordinates, including internal dungeon moves. Fresh explicit zone coordinates may establish a new origin. Eligible same-instance identity metadata can continue as before. Despawn, recycled identities, character changes, world socket handovers and capture gaps invalidate the corresponding spatial context. This does not erase the separate server-based field-boss cache. Position reports never create damage/healing events and do not relax the existing strict DPS filters.

Position/contact history is held in bounded memory and is not saved with settings. No upload service is added. Hidden radar retains no render loop; a visible idle radar refreshes at least every 500 ms, using the selected faster update rate when configured. These are update intervals, not measured in-game CPU/FPS claims.

## Verification and diagnostics

392 core and 108 native WPF checks passed with zero Release build warnings/errors. Synthetic fixtures exercise all 11 selected movement families, invalid tags/coordinates, actual framing and TCP retransmissions, explicit nickname faction, server/party identity, neutral arenas, stale origin/contact removal, teleport cleanup and ID reuse. Native checks cover rendered colours/name masking, real header/settings controls, zoom/rotation/lock and preserved field-boss deadlines. Preview pictures contain labelled synthetic contacts. No real PvP capture or live orientation/scale calibration was supplied.

Settings → Debug and the diagnostics report include decoded/rejected position counts, own-origin availability/age, status and contact count. A growing packet count with zero recognized frames is a capture/framing issue. Recognized frames with zero positions indicate unsupported/missing position layouts. Positions with Waiting for your position mean no fresh validated origin; a valid origin with no contacts means no fresh identified players in range. Unknown allegiance means classification is not established, not that the contact is hostile.

## Exact real fixtures needed if it stays empty or plots incorrectly

Provide a short **sanitized, ordered capture** spanning one zone entry, your walking, one nearby known ally and one known enemy walking, and one despawn/teleport. Include timestamps, packet direction, stable anonymized entity IDs and stream boundaries. We need the original frame lengths and bytes for these records, preferably already reassembled/decompressed frames:

1. Own-character 0x3336, preserving entity ID, numeric home server, class and faction byte.
2. Zone/teleport 0x2136, including counter, zone ID and its complete coordinate prefix. A truncated counter/zone-only excerpt cannot validate the origin.
3. Player appearance 0x048D and nickname 0x4536, preserving the faction byte and name-field length/structure.
4. Corresponding movement families actually observed: 0x1A37, 0x1B37, 0x1C37, 0x1E37, 0x2037, 0x2837, 0x2937, 0x2A37, 0x1F37, 0x2F37 and/or own 0x3C37. Supply the own report and nearby-player reports from the same interval; not every opcode must exist.
5. Despawn 0x4236 or the next zone boundary, plus the expected ally/enemy relationship and approximate in-game direction/distance.

Replace names with same-byte-length UTF-8 aliases and apply one consistent entity-ID mapping. Preserve coordinates, field order, varint encoding and frame lengths; if needed, translate all XY coordinates by the same offset in decoded fixtures. Exclude login/authentication, account identifiers and chat; a general full-session capture is unnecessary. If only encrypted/compressed fragments are available, packet segmentation/order and the relevant framing/compression envelope are also needed. Do not publish an unreviewed full packet capture. The app's metadata-only diagnostics report does not include packet bodies and cannot alone validate a missing layout.

## Source

Selected movement offsets, explicit faction bytes, coordinate scale, server-block facts and arena IDs reference the MIT-licensed SkeeveAN/Aion-DPS-Meter revision 8c14501afabb109f9c30963e1c6259a3479b5189. See THIRD_PARTY_NOTICES/README.md and SkeeveAN-MIT.txt. AscendAPP implements its own bounded tracker, WPF drawing and lifecycle integration. No upstream environment-derived origin, self-position estimation, terrain assets or unseen-player detection is imported.
