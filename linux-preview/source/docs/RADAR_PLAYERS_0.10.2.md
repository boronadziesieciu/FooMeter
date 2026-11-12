# AscendAPP 0.10.2 — Players, defeats and radar appearance

## Upgrade and appearance

Close the old copy, extract the complete Windows ZIP into a fresh folder and run AscendAPP.exe. Keep the optional Source ZIP separate. Existing .NET 10 Desktop Runtime/Npcap prerequisites remain. Shared settings, hotkeys, placement, tickets and boss observations load automatically. No tool modified your existing saved profile during development.

Press Ctrl+Shift+F6 or use the main radar icon. Under gear → PvP radar:

- Radar scale independently sizes the complete radar window from 50% to 200%.
- Radar opacity independently sets transparency from 20% to 100%.
- Range controls the world radius; it does not resize the window. Mouse-wheel and +/− still zoom without clearing observations.
- Follow enables the selected heading source. The rotation button switches to manual rotation.
- Marker size separately enlarges player symbols. Labels, position lock, retention and click tracking remain.

On first upgrade, independent scale/opacity inherit the existing global values. Later global edits no longer change them. All independent controls persist in the shared Windows profile. Focus rules and click-through remain shared; Ctrl+Shift+F12 restores interaction.

## Camera versus character facing

The current verified packet reference provides character-facing angles in own skill (0x0238) and windup (0x0938) notices. It does not provide a verified continuously updating camera-angle stream. A casting direction, travel direction and independently rotated camera must not be treated as the same signal without checking your control mode.

The user reports that their character rotates with the camera. To test that relationship, select Heading source → Character facing and enable Follow. Use a skill. The radar uses the explicit own facing angle, with world-angle convention +X = 0 degrees / +Y = 90 degrees; screen-up rotation is 90 minus that angle, plus the optional Facing alignment offset. The own marker can show the received character-facing direction independently of travel.

The footer explicitly says Facing · seen Ns. Turning while idle may not produce a skill notice, so the value can become stale and the radar can hold an old angle. This release does NOT claim continuous camera-only rotation tracking. Movement remains the default heading source; it is not silently presented as a camera signal. Facing data from other actors, non-finite/invalid angles and unverified own identity are rejected. Optional ground-aim XYZ is validated for frame structure but is never used as your position.

The selected layout and angle convention are referenced from the pinned MIT [TryReadAim/DecodeWindup source](https://github.com/SkeeveAN/Aion-DPS-Meter/blob/8c14501afabb109f9c30963e1c6259a3479b5189/Client/Aion2/Protocol/Aion2FrameDecoder.cs) and [ReplayView angle projection](https://github.com/SkeeveAN/Aion-DPS-Meter/blob/8c14501afabb109f9c30963e1c6259a3479b5189/Client/Ui/ReplayView.cs). Actual camera binding, live scale and orientation still require a real fixture. No mouse-yaw estimate, camera-memory reader, injection or on-screen compass reader has been added. A compass reader cannot be calibrated without the actual minimap/compass behaviour.

## Eligible players and groups

Only a verified player identity, or an exact unique link to the confirmed local roster, can create a player marker. Own records and server-bearing player appearance provide wire player proof. A bare nickname/faction byte is insufficient. Known NPCs and summons stay excluded even if a nickname or movement message follows their spawn. On NPC/unclassified-to-player ID reuse, a fresh coordinate is required after player proof; an old NPC position cannot be inherited.

Cyan outer rings identify the confirmed local party/group. Gold outer rings identify the manual force list. These group contacts take priority when a crowded view exceeds its 128-contact cap, and draw above ordinary players. Ordinary allies are green; opposing faction players are coral. Unclassified players are hidden by default. Show unclassified players adds grey verified-player markers; it never enables NPC markers. In known arenas, faction alone still does not identify opposing teams.

Automatic force membership beyond the local 0x0297 roster is not verified. The primary reference explicitly describes 0x0197 as a list of other parties, so it is not imported as your force. To highlight known force members now, use Force list (manual), one Name@server per line or comma-separated. The radar hover shows the observed S-number. Entries require exact name/server and matching known faction; duplicate/invalid entries are ignored, with a maximum of 48. A force-list entry cannot turn an enemy into an ally. The list persists as a local preference and affects radar highlighting only, never DPS capture or party authority. Update it when your force changes.

## Defeated targets

A decoded zero-HP report immediately removes the player's coordinate/trail and suppresses subsequent corpse movement. The validated 0x048D appearance prefix can also identify a known verified player defeated by a nonzero killing skill. This selected prefix follows the same pinned MIT protocol reference; it is not inferred from missing movement or an estimated damage total.

Repeated same-identity appearances cannot clear defeat. A newer positive-HP update followed by a fresh position restores the marker. Older positive HP is rejected. A genuinely different identity reusing an ID gets a clean lifecycle. Despawn removes identity/position; late anonymous movement cannot restore it. Defeating a tracked player clears the actual selection. Your own defeat keeps the last-location map and displays a defeated status; it does not claim your old origin is a fresh position.

Teleports, world changes and capture gaps still invalidate obsolete spatial context and angles. Boss-timer continuity and strict self/party boss/dummy DPS filters are unchanged. Normal last-seen retention remains five minutes by default; see RADAR_TRACKING_0.10.1.md for display coalescing and retention details. Its shared radar scale/opacity assumptions are superseded by this release.

## Validation and remaining fixtures

444 core checks and 126 native WPF checks passed with zero Release warnings/errors. Tests include actual TCP own-facing and HP-zero packets reaching the controller and real WPF overlay; NPC/nickname/ID reuse; false player proof; corpse motion; stale HP; revival; known-player kill credit; despawn; facing validation and angle conversion; force faction/name/server checks; group priority; rendered force rings; actual independent slider controls and saved preference round trips. Previews are labelled synthetic data. No real new packet capture, continuous camera calibration or live FPS measurement was supplied.

For continuous camera tracking, the minimum missing fixture is a short sanitized sequence that preserves both packet directions and timestamps:

1. Own 0x3336 identity, relevant 0x2136 zone prefix and known own entity ID.
2. Stand still and rotate camera/character through four quarter-turns, pausing about one second each. Include one known starting view direction or a visible compass reference.
3. Repeat while using a normal skill after each turn, including own 0x0238/0x0938 notices and any movement/rotation 0x..37 or other messages sent during the idle turns.
4. Walk straight, strafe and stop while rotating once, so travel heading can be separated from character/camera facing. A short matching video/cropped minimap sequence helps identify which displayed direction actually moves.

If angle reports arrive only with skills, those fields cannot deliver continuous idle camera sync. A different verified rotation message or a calibrated visual compass source is then required. Current metadata-only diagnostics show facing count/value/age but cannot expose an unknown packet layout.

For automatic force support, provide a sanitized join/update/leave interval for your own force, including known member name/server aliases and the relevant local/force roster messages. A list of unrelated parties alone is not sufficient membership evidence. For a missed defeat, include that player's appearance, HP/death/despawn sequence and any subsequent revival/position.

Replace names with same-byte-length aliases and keep consistent entity IDs without changing varint widths, frame lengths or relevant field ordering. Keep coordinates/angles consistent; if translating XY, use one offset for all actors. Exclude login/authentication, account identifiers and chat. Reassembled/decompressed relevant frames are preferred; retain the framing/compression envelope and segmentation if a parser problem is suspected. No full unreviewed account session is needed.
