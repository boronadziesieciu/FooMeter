# AscendAPP 0.10.19 — Target focus continuity

This release fixes reset paths in the target skill bar. Previously every game target-clear, capture gap and own scene-loss notice discarded the selected player, public build and cast timestamps. A repeated player identity with a missing server field could also clear a known target.

## Install and use

Close the older app. Extract the complete Compact-Windows ZIP into a fresh folder, then open AscendAPP.exe. Existing settings carry over. Keep the optional Source ZIP separate. The normal Windows administrator prompt, Npcap and .NET 10 Windows Desktop Runtime prerequisites remain.

Open target skills with Ctrl+Shift+F6 or the crossed swords. Select another player in-game once. **Keep target focused is ON by default**, including when upgrading from 0.10.18. The bar keeps that verified player when the game clears its target, switches to self for a skill, points at an NPC/unresolved entity, or automatically selects another player.

- **Pin:** keep the current player focused. Click it off to follow newly verified player selections. Temporary clear/self/NPC reports still retain the previous player in follow mode.
- **Crosshair / Track selected player:** explicitly switch focus to the verified player currently selected in-game, with the pin still active. Select that player in-game before pressing the button. An unknown target cannot replace the focused build.
- **Circular arrow:** refresh the focused player's public build. Existing observed cast times survive the refresh; changed reference durations may change the estimated remaining time.
- **Padlock:** window position / click-through, independently of player focus. Hold Alt to interact or drag while locked.
- **Settings → Target skills → Forget tracked target:** explicitly forget the current focus and its cached state.

When you switch away and return to a player in the same world session, their observed timestamps and references remain available. Up to 16 player states are retained in memory. Received casts for those cached, verified actors continue updating while the module is enabled. No new network request or profile cancellation is caused just by target-clear/self/NPC reports.

## Missing capture and out-of-view players

Capture interruption no longer empties the bar. It preserves the original observed times and marks affected timers with *****. Hover to see why. Countdown time keeps elapsing from the last observed cast; later casts may have been missed. After an uncertain timer expires, availability becomes **?**, rather than reporting estimated readiness. A new observed cast refreshes that skill's state.

A removal notice detaches the actor ID but retains the named focus, public build and historical times. Another player reusing that ID cannot alter those cooldowns. Reappearance can rebind only with verified exact name and home-server identity; a nickname alone is insufficient. The bar says out of view while detached.

Temporary own scene-loss notices invalidate actor bindings instead of deleting the bar. A confirmed world/connection reset or capture restart clears the old world state. Character changes cannot inherit another character's actors or timers. This release does not promise continuity across confirmed new world sessions.

Hiding the module still cancels pending public requests, stops its monitor/render timers and unsubscribes cast/selection processing. The focused state remains in bounded memory and returns with uncertainty markers because casts may have occurred while hidden. No radar, process-memory scan or background web polling was added.

## Verification and limits

Release build: **0 warnings and 0 errors**. **507 core checks and 152 native UI checks pass.** Evidence and synthetic previews are in validation/.

New checks cover clear/self/NPC reports, default focus mode and settings migration, explicit switching, return to cached timers, casts received for a cached player, incomplete identities, late asynchronous replies including A→B→A, capture gaps and unknown expiry, pause/resume, actor reuse and verified reappearance, bounded cache eviction, and native button/binding behavior. An own-removal fixture runs through Ethernet/TCP/framing/decoder/controller into the actual WPF skill bar. Existing automatic GS/CP, dungeon, timer and input regression suites still pass.

These are synthetic regressions and visual checks. This exact release has not been verified in a new live game session. The underlying cooldown calculations remain estimates, with the same incomplete rank/stat/charge/reset coverage described in TARGET_SKILLS_0.10.18.md. No live cooldown values have been fabricated or newly claimed exact. Public lookup behavior and sources are unchanged; no real character data is bundled.

For the in-game check: select player A, observe a skill cast, use self-targeted abilities and briefly lose selection, then select B and use the crosshair. Return to A with the crosshair and check that time has elapsed rather than restarting. If capture is interrupted, the bar should remain visible with * instead of silently resetting. If another reset occurs, export the diagnostics report and note whether it coincided with a teleport, reconnect or capture restart.
