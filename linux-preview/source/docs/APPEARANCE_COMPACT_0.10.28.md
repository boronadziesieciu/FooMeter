# FooMeter 0.10.28
2 November 2026

## What changed
- Settings → Appearance: six accent presets, a color picker or RGB hex entry, a panel background, and an independently colored flame. “Logo follows app accent” links them. Text/controls adapt their contrast; class icons and capture-state meanings remain distinct.
- Colors apply immediately across the meter, settings, events, pet list and pickup popup. The running window/taskbar and tray use a recolored flame. The executable's fixed Windows Explorer icon remains the pink release icon; no executable files are rewritten to change a setting.
- Events are 300px wide at 100% scale, with single-line 24px rows. The same six default timers fit under 260px tall. Detailed descriptions and sync provenance remain in tooltips/settings. Existing separate event scale, opacity and locks carry forward.
- All 207 catalogued pets have ID-matched, offline portraits. A dedicated 76px count column uses larger text. Unknown pets keep a fallback image.
- A separate, headerless pickup counter shows the latest pet, soul gain and confirmed progress, then fades after roughly six seconds. It works even when the full pet list is hidden.
- Keep Alt held while the popup is visible to drag it beside the game's loot area. Settings → Pet tracking → Preview pickup counter shows a clearly labelled six-second DEMO card for positioning. It does not stop capture or write synthetic collection data.
- Pet color/position/visibility preferences are saved through the existing shared profile. No new account, server, overlay map or screen recognition is used.

## Start this version
Close the older copy. Extract the Windows ZIP into a new folder and run FooMeter.exe, keeping its DLLs and data beside it. Windows and Source ZIPs are separate packages; do not merge them.
.NET 10 Windows Desktop Runtime x64 and Npcap are still required. The administrator manifest is retained.
Your existing settings, history and observed balances carry over.

## Missing pet totals: what was fixed and what remains unverified
Your v0.10.27 screenshot showed zero published collections, zero published gains and no reported parse rejections. Those old counters did not tell us whether a raw collection frame was absent, held before character binding, or dropped by ordering.

A reproducible code defect has been fixed: when an own-character record and ticket list are in the same captured login packet, a later ticket-list frame could mark the session pending and indefinitely strand an otherwise parsed pet collection. Pet identity now retains the verified own-record anchor within that same packet. A later, separate login marker still requires fresh identity, so new collection data cannot silently attach to the previous character. A narrow synthetic regression covers both orders. Known world handovers retain the established identity eligibility.

This is an achievable ordering fix, **not proof that it explains every missing total in your live session**. We have not received a real pet packet fixture or verified your live collection counts.

The new diagnostics distinguish:
- PetStateFrames / PetGainFrames: raw supported-opcode frames observed.
- PetStates / PetGains: messages published to a verified character.
- PetRejected: unsupported or inconsistent layouts.
- PetUnbound: gains without usable collection identity.
- LastPetStateBytes / PetStateResult: body size and parsing/binding stage, with a specific rejected-field explanation.
- CharacterKnown / Trusted: own-character identification versus an authoritative collection baseline.

The pet panel's bug button, or Settings → Pet tracking → Copy pet diagnostics, copies these details. The normal exported report now includes a Pets section too. The earlier report omitted that section.

After starting this build, keep capture running and re-enter your character once. Compare a pet's rank and current-rank soul count in the game, then gain souls. If totals still show SYNC / NO TOTALS, paste the new copied pet diagnostics. No further blind relog loops are necessary:
- zero raw frames: investigate capture/framing or changed opcodes;
- raw frames but zero published collections: investigate pending identity/order;
- rejected frames: use the stated field/size reason to identify the changed layout.

Until a full collection is accepted, the small pickup counter shows only an actual observed +N gain with “Total waiting for collection”. It never invents a 0/25 baseline. Saved totals are marked as saved after interruptions.

If actual message layouts cannot be resolved from diagnostics, the exact fixture requirements remain in PET_TRACKER_0.10.27.md: narrow server-to-client collection and gain bodies with corresponding on-screen counts, their order relative to the own-character record, and TCP/LZ4 framing when needed. Preserve pet IDs, amounts, lengths and ordering while pseudonymizing character/server identity. Do not share unreviewed full login captures or credentials.

## Art and licensing
Portraits were matched by numeric ID against https://aion2.app/db/pets and its published portrait URLs, then stored as 64px PNGs. All 207 existing names have matching images; no new pets, acquisition claims, drop rates or map data were inferred. Game art is © NCSOFT / NC, separately attributed in THIRD_PARTY_NOTICES/Pet-Artwork.txt. Exact source pages, image URLs and original/thumbnail hashes are in data/pet_art_sources.json.
The Kisk reference was used as visual inspiration, without copying its code, assets or UI implementation. Only prior MIT protocol references remain in use. The 0Fox2Give/PetTracker public README/install notes were consulted for user-facing synchronization behavior; its GPL implementation was not copied or incorporated.

## Verification
See VERIFICATION_0.10.28.json. Validation covers saved color normalization, contrast, live updates on already-created controls, runtime icon generation, independent logo colors, compact event dimensions, all bundled portraits, unknown IDs, pickup counts/Alt behavior and the login-order regression. Native previews use synthetic DEMO values. No actual game capture, game process access or user-profile modification was performed during validation.
