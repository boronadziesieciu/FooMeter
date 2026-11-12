# AscendAPP 0.10.16 — Automatic GS and CP

Reviewed 26 October 2026. This release adds the own-character and party score fields missing from 0.10.15. The old statement that own login scores cannot be decoded is superseded by this investigation.

## Install and verify in game

1. Close the older AscendAPP instance. Extract the entire **Compact-Windows ZIP** into a fresh folder and open **AscendAPP.exe**. Keep the optional Source ZIP separate. Existing settings are reused under the same Windows account.
2. Start capture before entering your character. For a complete fresh observation, keep AscendAPP capturing, fully exit Aion, reopen it and log in.
3. Check the own-character footer while idle. It should populate from the supported own record without dealing damage or typing scores. The supplied screenshots show GS **2,724** and rounded CP **133.7K**; these numbers have not been hard-coded or reconstructed from the images. Compare the new reading against your current HUD, since equipment may have changed. Hover the score for full CP, source and observation age.
4. Join/form a party while capture is running, or have the party change once if the app started after it was formed. The party roster can update your scores and the other members' scores before combat. The compact placeholder rows become normal DPS rows when admitted boss/dummy combat arrives.
5. After a teleport, verify the same characters retain their exact home-server association. A new own record updates equipment scores automatically.
6. If a score is still blank, export the report from **Settings → Debug** after these steps. **Settings → Character** now separates own, roster, nearby-profile and inspect sources. A screenshot alone does not identify the packet layout that failed.

Automatic scores are now the only displayed source. The manual entry controls have been removed. Any old saved manual entries are ignored; they do not fill missing values. Other preferences carry over.

The normal Windows administrator prompt remains. Existing .NET 10 Windows Desktop Runtime and Npcap prerequisites apply. The app starts in the compact English overlay.

## Findings and implementation

Abyss's public documentation advertises class/GS/CP bars and a dungeon lobby displaying everybody's scores. Its public GitHub tree at the inspected revision contains a README and screenshots, with no decoder source. That establishes its advertised behavior, not its private implementation. AscendAPP does not claim to have copied or verified Abyss's internal method.

The previous review relied too narrowly on a different decoder, which does not decode own scores. Another primary open-source implementation documents the missing wire fields:

- **Own record, 0x3336:** after the verified name/server/class/faction/level, GS is a repeated u32. AscendAPP accepts it when both copies agree. CP is the first of two bounded u64 values preceding a five-entry table keyed 1 through 5. Variable equipment/legion sections are skipped by finding that structural table; multiple plausible locations leave CP unknown. The second value must be at least the first within the supported shape.
- **Party roster, 0x0297:** an occupied slot and account ID precede each name; its high u16 supplies the home server. Class, level and GS follow the name. A small world/tag section precedes u64 CP. Supported variable-width forms and a visited world differing from the home server are handled. Account IDs are never treated as world combat IDs.
- **Existing nearby/inspect paths:** remain available and merge independently observed fields. An inspect GS-only update does not renew an older CP's timestamp.

The new readers operate only within the corresponding framed record and established identity/session rules. They do not search arbitrary encrypted traffic, infer CP from a formula, substitute website item level, or send profile requests to the game. Own and party fields have separate diagnostic counters and sources. A later score update in the same TCP timestamp now correctly supersedes an earlier actor-bound value.

Party scores join by exact name and home server. A roster-only member can have scores without a world actor ID or invented damage. Later verified world identities receive the matching cached values. Existing dungeon/teleport identity rules and self/confirmed-party boss/dummy combat filtering remain.

## Freshness and remaining limits

These are **last observed game values**. Updates depend on the game sending the supported record; the refresh button redraws existing data. The existing cache limits remain: GS up to two hours and CP up to ten minutes after its own observation. Unknown, expired, structurally inconsistent or ambiguous fields remain dashes. Tooltips show each field's age and exact value. Arena values may be normalized by the game.

The new readers passed synthetic byte fixtures and actual WPF binding tests, including TCP ingestion. They have not yet been verified against a new live capture from your current Global build. A screenshot showing 133.7K is rounded and cannot establish the exact underlying integer or a wire offset.

This release does not newly validate radar movement, camera yaw or 100% tracking uptime. The 0.10.15 encrypted-stream routing, bound-flow retention, Face up recovery and dummy-forecast fixes remain. A missing radar handshake still requires a full game restart while capture runs.

## Verification

- Release build: **0 warnings, 0 errors**.
- **841 core checks** and **322 native WPF checks** passed.
- New checks cover own GS/CP, changed scores, inconsistent duplicates, zero/overflow/ambiguous CP, every truncated own-tail boundary, multibyte names, variable party tails, cross-world/home-server distinction, exact identity joins, same-timestamp updates and teleport rebinding.
- Actual synthetic TCP packets reach the idle own footer, party placeholder rows and active DPS rows, with full CP/source in tooltips. The native Character page has no manual inputs.
- Development checks use isolated fixture/preview entry points. No live capture, game-process lookup or game interaction was performed for these tests. The .NET dependencies were restored from the existing cache; the online vulnerability feed was unavailable, so this is not a fresh dependency security audit.
- Executable version, administrator manifest, artwork/license preservation and archive integrity are checked before delivery.

## Exact evidence if a field still fails

Start with the new diagnostics. Include the own record count, own GS/CP counts and **OwnScoreDetail**, plus party list/member and party GS/CP counts, current build/revision and which operation you performed. The report can contain player names; replace names consistently before sharing if desired. It contains no packet bodies or private keys.

If counters identify an unsupported shape, the targeted fixture needs are:

- A decoded **0x3336** payload with its exact boundary, byte length and relative timestamp, including the identity prefix, both GS fields and the candidate two-u64/five-entry CP table. Record the exact CP from the expanded in-game display if possible, rather than its rounded K form.
- A decoded **0x0297** roster around party formation/change, including occupied/vacant slots, account ID/home-server links, name/class/level/GS and the world/tag/CP tails. Include a member visiting another world if that is the failing case.
- For equipment updates, a short before/after sequence for one known equipment change, with the GS/CP shown in game and the matching relative timestamps.

Use consistent anonymous names and IDs, preserving encoded lengths, field widths and identity links. Remove unrelated account/chat data. If a reliable decoded fixture export is unavailable, send the diagnostic report first; do not hand-edit unrestricted packet bytes. No private keys, session cipher state, raw process dump or unrestricted traffic capture is requested. No general fixture extractor is bundled with this update.

## Primary references and licensing

- [Abyss Logs public README and repository at e0addfbd](https://github.com/karim-mo/aion2-abysslogs-dps-meter/tree/e0addfbd3a8a61ffb98a4e916d7790c3d88600c3): advertised GS/CP display and dungeon lobby; public tree is documentation/screenshots only. No Abyss source, binary or artwork was copied.
- [A2Tools stream protocol descriptions at 836f8c07](https://github.com/taengu/A2Tools-DPS-Meter/blob/836f8c07d277c07ca4926c71ac30ae626909ce64/src-tauri/src/capture/stream_processor.rs): own repeated GS, structurally anchored CP, occupied party fields and variable party tails. This repository is GPL-3.0. AscendAPP's new reader is independently written from the wire-field descriptions; no A2Tools implementation, test bytes or assets are incorporated.
- [SkeeveAN/Aion-DPS-Meter at 32bb8eb1](https://github.com/SkeeveAN/Aion-DPS-Meter/tree/32bb8eb19ea0276f295dd5718423932949a17eda): existing MIT nearby/inspect reference checked alongside the own/party findings. Existing copyright/license notices accompany both packages.

This guide supersedes older own-score limitations and manual fallback instructions in historical release notes.
