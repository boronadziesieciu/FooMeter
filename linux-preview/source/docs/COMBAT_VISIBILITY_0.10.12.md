# AscendAPP 0.10.12 — Close-combat visibility

## Finding

The supplied screenshot counts two fresh enemy contacts but labels only Wesa. The renderer drew contacts first, then an opaque own-location disc and arrow. An opponent one metre away was completely covered; the four label positions could all intersect the reserved own-arrow rectangle and silently fail. A native WPF pixel regression reproduced the invisible enemy before the change and passes afterward. This does not prove which hidden contact was Paranous: that requires the actual fight's decoded frames.

## Changes

- Own marker and facing cone draw before contacts. Enemy diamonds keep their exact world-to-screen projection and receive a dark outline.
- Labels are placed after markers, with tracked/current-target/leader-target priority. Clear-space callouts and short clickable leader lines move only the name; names do not cover other markers or the scale. At most ten names are laid out.
- Existing decoded own selection reports now drive an independent target reticle and compact readout. No party-leader setup is required. A known player without coordinates is named with waiting for position; no marker is fabricated.
- When capture missed a selection report, a decoded positive own damage event can supply Last attacked for five seconds. Explicit selection/clearing takes priority over damage, including area hits. Heals, other attackers and known NPC hits cannot replace it.
- Recent PvP-opponent bookkeeping happens before the main meter's boss/dummy filter. That filter and effective-healing scoring remain unchanged.
- Copy tracking diagnostics adds anonymous target state/source/position-present fields. No player names, raw frames, positions, session keys or process addresses are added to that copied output. PositionReady means eligible observed coordinates exist; it does not claim the contact is in view or currently live.
- Capture gaps, scene transitions, despawns, identity reuse and defeat clear invalid focus. Selection and damage do not refresh a position timestamp, confer player proof, infer faction or revive a corpse.

## Check in game

1. Close the prior app, extract the whole Windows ZIP into a fresh folder and open AscendAPP.exe normally. Approve Windows UAC. Keep Source separate. Existing same-account shared settings/maps carry over. Existing .NET 10 Windows Desktop Runtime and Npcap are required.
2. Capture as usual. Select an opposing player at melee distance. The player diamond and name should remain visible over/beside your own marker. Target identifies a reported selection; Last attacked is a recent-hit fallback, not an inferred selection. Click the name callout to track it.
3. If the contact is missing, read the target line and copy tracking diagnostics from Radar settings. UnknownPlayer means player proof is missing; UnknownAllegiance means faction classification is unavailable; MissingPosition means no accepted coordinate exists; NoOrigin means your own origin is missing. Outside-view and last-seen explanations appear in the overlay itself.
4. The main meter staying idle during PvP is expected: it continues tracking only self/confirmed-party damage against bosses and training dummies.

This update does not fix an unvalidated continuous-position session. The reported session lookup was still continuing. Continuous movement requires an exact validated session; elevation alone does not establish it. Camera yaw and 100% live enemy detection remain unverified.

## Evidence needed if live decoding still omits the selected player

Start with the anonymous copied diagnostics and target-line text. A packet-level follow-up would need a short same-world sequence of decoded server frames: own identity (3336), opposing player proof/profile (048D and/or a complete 4536), own target change (3138), the opponent's absolute and subsequent movement notifications (xx37), and health/despawn notices if present. Include relative timestamps, opcode, exact payload length and consistent pseudonymous entity IDs. If selection was not captured, include the validated own-hit 0438 event identifying the opponent. Unsupported movement requires its exact frame shape, not guessed offsets.

A local sanitizer must rewrite names and variable-length prefixes consistently, translate all XYZ positions by the same offset and retain the relevant class/server/faction/flags. Keep login/authentication frames, session keys, process memory, real endpoints and unrelated traffic out. No full process dump or unsanitized PCAP is requested. No real fight fixture was available for this release; its packet tests are constructed fixtures.

## Reference

Existing 3138/3238 selection semantics were checked against the MIT-licensed pinned [protocol research](https://github.com/SkeeveAN/Aion-DPS-Meter/blob/03026182c4ae2059b41804189852af6e945e365a/docs/protocol/skill-frames.md) and [decoder](https://github.com/SkeeveAN/Aion-DPS-Meter/blob/03026182c4ae2059b41804189852af6e945e365a/Client/Aion2/Protocol/Aion2FrameDecoder.cs). This release does not loosen the existing wire reader or copy a radar implementation. Existing license notices remain included.

## Validation

Release build: zero warnings/errors. All 757 core and 249 native WPF checks pass, using constructed packet data. The native regression failed before the fix when the own marker completely covered a one-metre enemy. New checks cover exact marker projection, label visibility/clicking/privacy, target priority under crowd limits, missing identity/allegiance/coordinates, ageing, clearing, defeat, scene changes and actual synthetic Ethernet/TCP -> decoding -> radar binding. A PvP hit reaches the radar while boss/dummy combat totals stay zero. Preview tests use the DLL host with native lookup/capture disabled and isolated profiles; no game process was read. The EXE keeps the administrator manifest from 0.10.11. Live Global packet compatibility and continuous movement still require verification.
