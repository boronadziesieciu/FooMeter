# FooMeter 0.10.27 · Pet souls and Rose Obsidian
Released 2 November 2026.

FooMeter is the new name for AscendAPP. FooMeter.exe opens the compact meter; the settings and saved-data format remain compatible. The existing shared user-data directory is deliberately retained so older settings, fights, tickets and boss observations continue to load. Run one copy at a time.

## Start and use
1. Close the older app. Extract the **Windows** ZIP into a new folder. Start **FooMeter.exe**.
2. If needed, install .NET 10 Windows Desktop Runtime x64 and Npcap with WinPcap compatibility. The application retains its administrator manifest; Windows will ask for elevation.
3. Open the paw button in the meter header, the tray menu, or Settings → Pet tracking.
4. Keep capture running, return to character selection and enter the world. The game sends the full collection on world entry; opening the app midway through a session cannot establish a missing total.
5. Search for a pet and pin its row. “Pinned only” keeps your farming list short. New soul gains rise to the top and show a six-second pickup notification.
6. Adjust the pet panel's scale and opacity in its settings. Drag its paw handle; while locked, hold Alt to click or drag. The meter, event timers and pet panel remain independent.

A counter such as **14/25** means 14 souls inside the current rank, with 11 needed for the next rank. The documented thresholds are 5 to unlock, 25 for rank 2 and 75 for rank 3; the current-rank counter resets at each transition. MAX indicates rank 3. Counts refer to souls actually reported, never inferred monster kills or drop probabilities.

The popup is in FooMeter's own movable panel. This release does not attach a counter to the game's individual loot rows and does not capture or recognize your screen.

## Honest synchronization states
- **SYNCED / Live collection:** a supported full-state message was decoded and linked to your character. Subsequent supported gains update it.
- **Saved collection / needs sync:** old values are shown as saved, not assumed to be your current total.
- **Waiting for collection:** no baseline exists for the identified character. Supported gains can show +N, but the total stays unknown.
- **Unsupported message / capture gap:** totals stop updating until another full collection arrives. Later gains alone do not repair an uncertain baseline.

“SYNCED” means the implemented message layouts and capture continuity checks succeeded. It is not proof that every current Global message is decoded. The pet layouts in this release have **not been verified with this user's live captures**.

Pet totals and settings are kept locally. Up to 32 character/server collections, 1,024 entries per collection and 128 pins are bounded. Names use 207 English name/ID facts; new IDs display “Pet #ID”. Collection files are limited to 2 MB. Malformed or future-format archives are preserved read-only, with an explanation in the panel. A second running instance cannot overwrite the archive. Pet saves are coalesced on the desktop refresh (at most every five seconds) and flushed on normal exit; a sudden crash can lose the latest unsaved gains.

## If counts do not match
Compare one pet's rank and progress in the game against FooMeter immediately after world entry, then after several actual soul gains and a rank transition. Also check a teleport, character switch and restart.
If no collection appears after re-entering the world, open Settings → Debug and export diagnostics. The **Pets:** line reports accepted collection messages, gains, unsupported messages and gains without identity. No raw packet bodies are in that report.

If those counters show an unresolved layout, the required packet fixtures are:
- Server-to-client framed **00 90** collection body from world entry, with the corresponding on-screen pet rank and current-rank soul count.
- Server-to-client **0d 90** gain bodies before/after one or more clearly labelled +N soul pickups, including a rank-up if the count differs there.
- Frame order relative to the own-character **33 36** record, plus TCP sequence metadata or intact LZ4 bundle framing if ordering/reassembly is the suspected fault.
Use consistent pseudonyms for character/server identity; remove account/login credentials, chat, unrelated inventory, network addresses and unrelated packet bodies. Keep pet IDs, amounts, field lengths and sequence relationships intact. Do not share an unreviewed full login dump. A narrow fixture export would need to be prepared for that specific failure; this release does not automatically collect or upload such recordings.

## Research and licensing
- Kisk describes server-packet collection counts plus Windows text recognition for aligning loot counters: https://kisk.app/addon . Read for feature research only; no Kisk code, artwork or executable was copied or run.
- Wire facts and bounded-reader reference: https://github.com/elkaida/AION2-pet-tracker/blob/main/docs/PROTOCOL.md .
- Source implementation reference: https://github.com/MartinLVila/aion2-pet-overlay/blob/main/pet_protocol.py (Git blob 9ba19a52326f4822f960c68fc7c49674e2c78aad), itself adapted from the elkaida MIT project. No Python dependency or capture worker is imported.
- English pet names/IDs: https://github.com/elkaida/AION2-pet-tracker/blob/main/data/names.json (Git blob 9f4de298daeef198565eeb53fa85fe7801cd0365). English mapping only, without maps, spawns, drop rates or imagery. Upstream credits MetaBot.GG for names/IDs and NCSOFT for game names.
- MIT notice: THIRD_PARTY_NOTICES/PetTracker-MIT.txt. No competitor binary, private service, authentication or updater was accessed.
- Pink flame SVG, application icon and interface styling are original FooMeter assets. Existing game class-emblem provenance and notices remain unchanged.

## Architecture and limits
The existing SharpPcap → TCP reassembly → framing → LZ4 → isolated Global decoder path handles both pet opcodes. There is no second capture loop, OCR worker, memory scan, radar, map, online profile lookup or outbound game request.
Snapshots received before identity are bound only after the same session's own-character record; up to 64 following gains can wait with them. A gap discards pending snapshots. Duplicate adapters and TCP retransmissions are covered by synthetic end-to-end checks.
Only known gain kind 1 is accepted; unknown kinds, truncated fields and changed layouts invalidate trust. Full snapshots validate bounds, duplicate IDs, levels and current-rank points; the documented uninterpreted extension tail is retained only as ignored bytes.

Build and verification results are in VERIFICATION_0.10.27.json. Tests use synthetic protocol fixtures and private temporary user profiles, not a live game connection.
