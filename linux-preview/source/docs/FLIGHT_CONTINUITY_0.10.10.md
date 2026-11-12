# AscendAPP 0.10.10 — Flight and position continuity

## What the screenshots establish

The status “Supported owner sweep complete · no matching session” means the continuous outgoing source did not acquire its key. A dash can still refresh the passive fallback position; it then becomes last-known because ordinary own movement is not reliably present in that fallback. This is not evidence of a successful continuous source that randomly stopped. A moving/flying radar requires **stream: validated** and increasing decoded movement reports first.

## Lookup fix

Our previous port used only the expected owner vtable marker. The pinned MIT reference also implements an owner-slot pattern fallback: six structured pointers containing two state pointers and their shared-control counterparts, followed by full validation of both RC4 permutations and both matching 214-byte keys. That path was missing.

0.10.10 adds it inside the same supported allocator pages and resumable eight-MiB owner-search budget. The slots can be found without the old owner marker or a fixed offset from that marker. The correct key must still validate the captured setup for the exact connection/adapter/own world and two valid clocks before any movement is emitted. One state, mismatched keys, malformed permutations and slot-shaped data cannot establish a session. SlotCandidates appears in copied diagnostics. No unrestricted private-memory scan, new permissions, writes, injection, privilege changes or protection bypasses were added.

This may handle a changed owner marker/slot placement. **It is not proof that revision 3528 uses that layout or that its key is in the supported allocator range.** A completed sweep can still fail and will report that honestly.

## Flight path

The supported reader now includes the documented 40/41-byte 0A37 flight transitions and 40/41-byte 0237 airborne transitions, including observed descent/wing-exit markers. Existing 0B37 active flight, 0337 airborne movement, landing forms and position snapshots remain. All positions/heading come from supported actual messages; velocities are validated, not integrated into predicted motion. Unknown shapes remain rejected and their movement opcode is counted.

The radar stays centred on your character. With changing reported XY the grid/contacts move around that centre and the own arrow/cone follows reported facing. The header displays **Z …m** when own world height is available, making vertical movement visible even when XY stays fixed. Z is world-coordinate height, not a calibrated height above terrain; target height differences are not claimed.

## Stationary hover and direction

An explicit ground stop or a supported flight report with exactly zero velocity may confirm a stationary pose. While fresh validated client clocks continue, that pose can be held without inventing additional movement reports. A matching action-position snapshot can retain that confirmation; a changed position cannot. Unknown movement, missing clocks, TCP gaps, disconnects, scene/identity changes or disabling the source revoke the hold. A moving pose is never prolonged using keepalives alone.

A supported hold shows **STATIONARY HOLD · live stream**. Its actual position observation age remains available in details. A fresh reported direction gets the normal cone; an older direction retained during a live stationary hold gets a dimmer cone, hollow own arrow and **held heading** label. Clock bearings are withheld for that retained direction. This is last reported character facing, **not independently measured camera yaw or camera field of view**. It can differ from what your camera sees. An unreported camera turn cannot be reconstructed safely.

An unresolved source remains last-known. This release does not remove freshness checks or promise 100% live uptime. Continuous flight compatibility and camera behaviour still require an in-game check.

## Install and test

1. Close the previous app and extract the whole **Compact-Windows ZIP into a fresh folder**. Keep Source separate. Run with the administrator launch that cleared Windows 5. Existing shared settings/maps carry over. Uses your existing .NET 10 Windows Desktop Runtime and Npcap.
2. Radar gear → PvP radar → **Prepare continuous capture**. Fully exit and reopen Aion while AscendAPP remains open and capturing. The earlier cipher-stream beginning cannot be recovered after replacing the app.
3. Let the region count advance, then check for **stream: validated** and increasing **movement reports** before assessing precision. Walk, turn, fly forward, ascend/descend, hover and land without depending on a skill/dash. In copied diagnostics, FlightReports and AirborneReports distinguish accepted flight/airborne positions; UnsupportedMovementReports and LastUnsupportedMovementOpcode identify unsupported movement forms.
4. If lookup still completes without a match, use **Copy tracking diagnostics**. Its Lookup block now includes SlotCandidates and RejectedKeys. Those counts distinguish no compatible slot structure, rejected candidate keys and a missing later stream validation. Do not repeatedly reset your character.
5. If the stream validates but flight fails, supply the safe report and the specific action that failed. The remaining useful protocol evidence is a locally sanitized decoded movement-only fixture with exact opcode/length/flags, relative timestamps and the action sequence; translate all XYZ together if needed. Never send the real key, authentication/setup tokens, a process dump or an unsanitized PCAP. A changed native layout may need an updated appropriately licensed implementation or independent local analysis, rather than packet fixtures alone.

## Validation and limits

**737 core checks and 232 native WPF checks passed; Release builds have zero warnings/errors.** Added cases validate the slot fallback without a matching owner header, mismatched states/keys, budget continuation, flight/airborne transition lengths/flags, invalid velocities, two minutes of ten-Hz flight, sixty seconds of explicit hover on live clocks, hold cancellation, actual encrypted TCP → controller → own height/cone bindings and immediate unknown-movement expiry. The simulated flight sequence has no source dropouts; that is a synthetic result, not a measurement of your game.

No running game process or real profile was read during development/tests. Tests and labelled DEMO previews use synthetic packets/memory and isolated profile fixtures; native lookup/capture is prohibited in those paths. DPS remains self/confirmed party and known bosses/dummies only. Existing player filters, party/timer continuity, healing, independent appearance and shared settings remain.

## Sources and licenses

The [MIT Aion 2 Protocol Research, pinned fe2122f](https://github.com/lunixxx37/aion2-protocol-research/tree/fe2122f0e54e1de854c11e91e3c7bf51916a4f52) was rechecked and remained at that revision. The added fallback adapts locate_states_via_owner_arena's owner_arena_pattern and paired-state validation in tools/locate_session_key.py. Flight/airborne numeric layouts and zero velocity fields come from protocol/opcodes.json and the controlled action descriptions in docs/C2S_RESEARCH.md. Those source observations cover its investigated client revisions; no new revision-3528 offset or live success is assumed. Full MIT attribution remains in THIRD_PARTY_NOTICES/Aion2ProtocolResearch-MIT.txt.

AscendAPP independently implements bounded slot scheduling, provenance-labelled stationary holding, held direction presentation and continuity tests. The reference's broader full-private-memory fallback, physics-based prediction, process writes and camera-memory reading are not included. Existing other notices and class-artwork provenance are unchanged.
