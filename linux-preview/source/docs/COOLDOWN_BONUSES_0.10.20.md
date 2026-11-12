# AscendAPP 0.10.20 — Cooldown source audit

Automatically inspects supported permanent cooldown effects from equipped titles, unlocked Daevanion nodes, and the character's actual equipment. The focus pin, explicit crosshair switching and observed timestamps from 0.10.19 are retained.

## Install and use

Close the previous app. Extract the complete Compact-Windows ZIP into a fresh folder and open AscendAPP.exe. Keep the optional Source ZIP separate. Existing shared settings, Npcap / .NET 10 Windows Desktop Runtime prerequisites and normal administrator prompt remain.

Open target skills with Ctrl+Shift+F6, select a player, and use the crosshair to switch the pinned focus when needed. Public profile lookup remains European Global. The public build appears first; skill references and bonus inspection load alongside each other through one serial, rate-limited client. Wait for the board/equipment check counts to finish.

**Hover the build summary line or a skill** to see the applied effects, percentages, source coverage and exclusions. The circular arrow refreshes the build and reruns inspection (15-second minimum). Enrichment recalculates remaining time from the original observed cast; it does not start a fresh timer or release the focus. The exported diagnostics report now includes the applied bonus breakdown and coverage.

## What changed

- **Titles:** read the equipped slot's actual effects. Duplicate responses cannot apply a title twice, and conflicting titles in one slot are excluded. Explicit owned effects of the equipped titles are included when returned. The full collection of unequipped owned titles is not exposed by this response and is not guessed.
- **Daevanion:** request details only for open boards with activated nodes. Use the active stat summary once, cross-checked against unlocked stat nodes. Closed nodes do not count, and the node list is not added a second time on top of its summary. A response for another board or inconsistent totals is rejected.
- **Equipment:** request character-specific item details using the actual character, home server, item ID, equipped slot and enchantment/breakthrough level. Read explicitly formatted permanent cooldown percentages from base/enchantment rows, random stats and magic stones. Identical item IDs in different equipped slots retain their individual rolls.
- **Sets:** inspect eligible equipped-count thresholds and deduplicate the same set across its pieces. Unconditional, unambiguous cooldown effects are supported. Conditional effects, disputed multi-tier cooldown stacking and inconsistent set snapshots are excluded and reported.
- **Units and totals:** use the official formatted percentages. Raw values such as +150 are not assumed to mean 150%, 1.5%, or any other rate. Illusion points already contribute to the character's published Illusion effect, so item or board Illusion points are not added again. Returned skill levels already contain their listed level bonuses; gear/board skill-level rows are not added again.
- **Proc timers:** a line such as Effect Activation Cooldown: 1s belongs to that proc; it is not general skill cooldown reduction. Temporary buffs, conditional resets and charges are not simulated.

Before applying newly collected effects, the client rereads the base profile and equipment and compares their snapshot signatures. If the build changed, the additional effects are withheld and Refresh is requested. Individual failed sources remain excluded with incomplete coverage. Requests use fixed official HTTPS endpoints, bounded responses, one active request, a 250 ms minimum interval and a 45-second bonus-audit budget. Hiding the module cancels its work. Completed audits share the existing five-minute profile cache.

## Verified example

The user-supplied random Templar profile was checked through the official services and the new C# client: five boards and 25 equipped slots, with a stable baseline. Its known passive subtotal was **10.7%**: **4.7% Illusion + 3% equipped utility title + 3% unlocked Nezekan board effects**. The board supplied two active 1.5% nodes and a matching 3% summary. The title's 3% was already supported; the board's contribution was missing before this update.

No additional unconditional gear cooldown percentage was exposed for that example. Its Mirror arcana contributed Illusion points, which are already represented by the character's Illusion total; adding them again would overcount. Other characters can expose direct equipment cooldown percentages, which are handled by the new reader. Positive gear, gem and set cases have synthetic tests; the sampled live equipment did not provide a positive direct gear-CD percentage.

The supplied example is not asserted to be the user's own character. No complete profile response, equipment/board dump, account/session material, website JavaScript or packet capture is distributed. The public example page is cited below as a source. The included public-service verification contains only sanitized aggregate results.

## Limits

The displayed percentage is a **known passive subtotal**, and the countdown remains an **additive estimate**. Modifier stacking, caps and effective live Global cooldowns have not been calibrated with real cast/cooldown fixtures. Base-only skill references remain explicit where rank-specific references are unavailable; the checked Templar Defiance rank table and reduction immunity are preserved.

Specialty Shard choices, class-passive effect interpretation, conditional skill reductions, resets, charges, temporary buffs, other owned-title effects, pets and wings are not fully available or simulated. Missing, inconsistent or unclassified data is not treated as a verified zero or an invented percentage. This release does not claim complete or exact cooldown prediction for every build.

Ordinary target loss and bonus updates preserve focus and elapsed cast time. Capture gaps continue to mark retained timers with *; after an uncertain timer expires, availability stays ?. Confirmed new world/session resets still clear old actor state.

## Verification

Release build: **0 warnings / 0 errors**. **538 core checks and 154 native UI checks passed.** Existing focus continuity, automatic GS/CP, dungeon, timer and input checks still pass.

New checks cover title ambiguity, active/closed boards, summary/node agreement, duplicated sources, character-specific slots, equal item IDs with different rolls, enchantment identity, base/extra/rolled/gem effects, socket and set deduplication, conditional effects, unsupported/raw units, absent stat data, immunity, invalid totals, progressive coverage, stable-snapshot recheck, changed builds, failed slots, cancellation, completed-audit caching, source tooltips and preservation of original cast timestamps. Synthetic previews are marked DEMO.

The public source inspection and client lookup were verified separately from the synthetic regression suites. New effective cooldown accuracy has not been verified in game. For a useful game check, wait for the source counts to finish, hover the summary, compare the listed title and board with the target's public profile, and observe a known cast. Any calibration should include the actual skill/rank, visible cooldown, current bonuses and timestamps; no login/session keys are needed.

## Primary sources and provenance

- [Shugo FAQ](https://shugo.gg/faq): profiles use the official character service; Specialty Shard choices are unavailable and Shugo has no public API.
- [Shugo title example](https://shugo.gg/titles/12010061): equipped/owned effect distinction for Through Hell and Back. The catalog exposes raw numerical values; AscendAPP uses the official formatted character percentages, not an assumed conversion of that catalog number.
- [Official character profile](https://aion2.plaync.com/en-us/characters/1318/9Eyzaq1VLW3iDdmGGffR-NmOZ0rq8v3msCQlrbqQV3E%3D?region=eu): public character information. Read-only services used: /api/character/info, /api/character/equipment, /api/character/daevanion/detail and /api/character/equipment/item. The official public character page confirms the item request parameters, including equipped slot and combined enchantment/breakthrough level.
- [MIT reference](https://github.com/SkeeveAN/Aion-DPS-Meter/tree/729cb910494a1a0a7b837b8914c98921ce512fd4): portraitService.ts and ncCharacter.ts were consulted for official board URL/data shapes; the Daevanion catalog was inspected for stat-token context. No database, formula implementation or artwork from that catalog was copied. Full existing notices remain in THIRD_PARTY_NOTICES.

The new audit, parsers, scope checks and UI are independently implemented. No Shugo private endpoint, login, cookie, game-memory access or new external dependency is used.
