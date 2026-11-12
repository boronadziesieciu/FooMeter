# AscendAPP 0.10.4 — Leader target glow and movement decoding

## Install and use
Close the earlier app, extract the entire Compact-Windows ZIP into a fresh folder, and run AscendAPP.exe. The optional source archive stays separate. Existing .NET 10 Desktop Runtime and Npcap requirements remain. Settings and imported zone maps continue in the shared profile; no development test read or modified the real profile.

Ctrl+Shift+F6 toggles the radar. Open its gear → PvP radar → **Party leader (select)**, choose your current leader, and leave **Glow on leader target** on. **Refresh party choices** reloads the menu after roster changes. Alternatively, right-click a cyan party marker and choose **Use as party leader for target highlight**. Clear the choice with Off or the context menu.

Leader identification is explicitly manual: decoded roster order is not proof of leadership. When leadership changes, select the new leader. Exact name/server, an unambiguous current identity, and confirmed party membership are required (verified self can also be selected). A stranger or enemy cannot become leader through a context-menu action. Old selections remain saved but have no authority outside the matching current party.

The selected member's explicitly reported player target receives a warm glow, ring, crosshair and LEADER TARGET tag. Its colour still indicates ally/enemy. Target switching and clearing are read from target-change messages, not inferred from skill use or damage. The tag follows the target; normal click tracking remains independent. Hover shows when the selection was reported. State changes persist until replaced, cleared, or invalidated; a quiet selection is not invented anew each refresh.

Death, despawn, identity reuse, party departure, a teleport, world switch or a capture gap removes obsolete authority/state. NPCs and unclassified non-players remain excluded. An out-of-view target is explained in the assist status instead of being placed at a guessed edge position. A retained last-seen player has a faded/static highlight and is labelled last seen. The pulse follows Windows animation preferences and runs only for a visible fresh target; the decoration switch can turn it off.

## Tracking fixes
The old movement reader used fixed offsets and treated 0x2037/0x2a37 as full XY floats. Current research distinguishes full positions from signed centimetre increments and describes flags that move fields. This could skip frequent updates or accept an incorrect plausible value.

The new reader:
- Validates complete known layouts and flag variants, including XYZ and relevant trailing fields.
- Reads absolute 1a37/1b37/1c37/1e37/1f37/2437/2837/2937/2f37/3037/2e37/3c37 formats.
- Applies documented 1d37/2037/2537/3237 signed movement increments against a recent absolute report.
- Re-anchors at every supported full report; bounds chains to three seconds, 64 increments and a 350 ms maximum report gap.
- Drops the base on malformed/unsupported movement, discontinuity, death, despawn, identity changes or teleport.
- Keeps uncertain 2a37/2d37 displacement and 2b37 path variants disabled; their bytes cannot be reinterpreted as floats.
- Handles explicit player removal 0x4736. An own departure clears spatial state while retaining the verified profile and party for the existing zone-handover rules.
- Uses load-complete 0x2336 own coordinates and own orientation/correction notices 0x4e36/0x3c37 when their exact forms are available.

A delta-derived marker shows its absolute-keyframe age in the tooltip. This is reconstruction of received increments, not prediction of a player's future motion. It may accumulate small protocol/rounding error until the next full report. Unknown flags wait for a supported report rather than scanning for plausible floats.

Npcap already requests immediate delivery with a 50 ms fallback timeout. Both network ingestion and the radar's stored observations remain active outside combat and while the radar is hidden, provided capture is running. Showing the window publishes the latest retained state. Visual refreshes follow display frames independently of the DPS meter; hiding the radar stops display work, not capture. There is no claim of a newly measured live FPS or server delivery rate.

## What is still limited
The reference's controlled server-to-client recordings report no routine own-position frames during walking, running or flying. Nearby actors can have frequent full reports/deltas while the local origin stays old. Relative position accuracy is then limited by that old origin; more rendering or retaining markers cannot recover it. The overlay retains the explicit age warning.

Load, skill/effect, orientation and correction notices add observations when the game sends them. They do **not** establish constant self-position or continuous camera yaw. Movement follow uses received own travel; Character facing uses intermittent own-angle observations and remains an experimental camera proxy.

The server also controls which remote actors it sends. This build cannot promise detection outside that interest range, reveal unreported enemies, or claim to outperform the game's minimap. It improves supported packet interpretation. There is no fabricated DPS, synthetic live enemy, guessed motion, memory reader, key recovery or input-driven position estimate in this release.

The separately researched client-to-server stream is encrypted. Public research describes live key recovery through process access; simply capturing a raw connection does not make it readable in this passive implementation. That implementation has not been added or copied.

## Validation
Release: **527 core checks and 167 native Windows/WPF checks**, zero build warnings/errors. Checks include public reference delta examples; signed increments; variable full-position offsets; rejection of malformed/uncertain frames; bounded bases; zero-skill TCP movement and target updates; retransmission handling; leader identity/server/roster authority; switch/clear/death/teleport lifecycle; target priority in crowds; actual rendered glow pixels; independent click selection; actual settings/context-menu actions; hidden-window ingestion and fresh display on reopening; and all earlier map, timer, combat and healing checks.

Fixtures are synthetic or small published protocol examples. No new real Global capture was supplied, and the new movement/target extensions have not been verified in the user's game. A more frequent enemy update in tests is not proof of higher live accuracy.

## Useful follow-up evidence
For live verification, a short anonymized server-to-client frame fixture should preserve timestamps, frame/opcode boundaries and consistent entity IDs, and contain:
1. Own 3336 identity and 2136/2336 origin, then 20–30 seconds standing, walking and turning without skills.
2. A verified enemy appearance and a sequence of absolute and delta reports while that player walks, stops and dashes.
3. A 0297 roster, a chosen leader's 3238 target changes between two known player IDs, clear-to-zero, and any 3138 own-target reports.
4. Defeat/removal, party leave/leader reassignment, and a teleport if that behaviour needs checking.

Use equal-byte-length pseudonyms consistently if redacting names; retain server IDs needed for faction/membership tests and the numeric coordinates needed to validate displacement. Include only the relevant decoded message families rather than unrelated account/chat payloads. A diagnostics report supplies counts and ages, but not the raw fields needed to establish a new position layout. If no plain server-side own-position messages exist, that fixture cannot by itself solve continuous local tracking; a separately verified data source would be needed.

## References and licence
Selected numeric wire facts and layouts are from the MIT [SkeeveAN/Aion-DPS-Meter](https://github.com/SkeeveAN/Aion-DPS-Meter/tree/03026182c4ae2059b41804189852af6e945e365a), pinned to revision 03026182c4ae2059b41804189852af6e945e365a:
- [Movement research and confidence labels](https://github.com/SkeeveAN/Aion-DPS-Meter/blob/03026182c4ae2059b41804189852af6e945e365a/docs/protocol/bewegungs-frames.md)
- [Own-position findings](https://github.com/SkeeveAN/Aion-DPS-Meter/blob/03026182c4ae2059b41804189852af6e945e365a/docs/protocol/position-frames.md)
- [Target-change and existing message reader](https://github.com/SkeeveAN/Aion-DPS-Meter/blob/03026182c4ae2059b41804189852af6e945e365a/Client/Aion2/Protocol/Aion2FrameDecoder.cs)
- [Session/load-complete research](https://github.com/SkeeveAN/Aion-DPS-Meter/blob/03026182c4ae2059b41804189852af6e945e365a/docs/protocol/sitzungs-frames.md)

Copyright (c) 2026 SkeeveTV; full MIT notice is retained in THIRD_PARTY_NOTICES/SkeeveAN-MIT.txt. Hypothetical displacement layouts are deliberately excluded. [Client-stream research](https://github.com/lunixxx37/aion2-protocol-research) was consulted only to understand the encryption constraint; none of its process-access/key-recovery implementation was copied.
