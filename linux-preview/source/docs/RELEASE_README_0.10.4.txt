AscendAPP 0.10.4 — Leader target glow and movement fixes
====================================================
Close the earlier app. Extract the ENTIRE Compact-Windows ZIP into a fresh
folder, then run AscendAPP.exe. Keep the optional Compact-Source ZIP separate.
Existing settings and imported maps carry over automatically.

LEADER TARGET
Open radar gear > PvP radar > Party leader (select).
Choose the current leader and leave Glow on leader target enabled.
Or right-click a cyan party marker to select that member as leader.
Leadership is selected manually, not guessed from roster order.
A reported player target gets a warm glow and reticle. Switching or clearing
the target, defeat, party departure and teleport update the highlight.
NPCs remain excluded. Ordinary click-to-track stays independent.

MOVEMENT
Frequent supported movement increments now update enemy positions between
full reports. Variable flag offsets are corrected and uncertain layouts are
rejected. Capture continues while the radar is hidden and outside combat.
Last-seen/own-origin ages stay visible; no future positions are invented.

LIMIT
Routine own movement still does not reliably appear in the passive server
stream. Additional load/orientation/correction reports help when available,
but continuous self/camera tracking is not established. This build cannot
promise greater live detection range or precision than the in-game minimap.

CHECKS
527 core checks and 167 native Windows UI checks passed; zero Release warnings.
Tests use synthetic/public protocol examples, not a new in-game capture.
Read RADAR_ASSIST_0.10.4.md for controls, limits, references and useful fixtures.
