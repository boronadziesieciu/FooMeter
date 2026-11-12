# Party and effective healing — 0.9.1

## Changes and operation

Choose Settings → Combat → Capture players → Self and party. Start capture before joining the party or entering a zone. If the meter was already running, joining/re-forming the party or teleporting can provoke fresh game records. AscendAPP is passive and sends no requests to the server.

The 0297 roster replaces membership, including the local-only snapshot when the group disbands. It no longer expires arbitrarily after 30 seconds. A detected TCP gap, stream/world change, login transition or teleport invalidates it. A malformed roster is counted and does not replace the prior valid snapshot. 0197 lists other parties and is never local membership.

Roster rows exist independently of combat statistics. Names, class, level and server come from the decoded roster. A missing combat ID shows — / Waiting for character ID, not a fabricated zero-DPS event. Known nickname IDs join by unique name only when there are no same-name observed IDs and no contrary class evidence. Explicit name/server matching disambiguates namesakes; the documented wire/home server aliases are retained. Despawn removes the current binding so a new nickname ID can bind. No class-only guessing or nearby-player fallback is used. Damage still counts only on catalogued bosses and training dummies.

A roster-only row has no skill result yet. Hover to see its data and binding status. A combat row also includes available roster information in its tooltip. Old admitted damage remains a record of that encounter even if somebody later leaves; Reset removes the old encounter while retaining the current roster.

## Healing method and its limits

There is no verified Global effective-heal/overheal field implemented here. The scorer is a conservative observation-based estimate:
1. Keep raw heal amount separately. Do not count the 0538 flags 09 initial HoT announcement as an actual tick. For actual HoT ticks, use the heal field, never the remaining-future-heal amount.
2. Wait 500 ms after the heal's receipt timestamp for matching HP updates. Display refresh adds its configured interval.
3. Require a pre-heal HP observation strictly before the heal, no more than two seconds old. Use the last post-heal HP observation within 500 ms.
4. If pre-heal HP equals an explicitly reported maximum, there is no contradictory later HP and no intervening damage, score zero. This also handles full-health ticks with no changed-HP notification.
5. For an isolated heal with consistent HP maximum and no intervening damage, score only a nonnegative observed HP increase no larger than the raw amount.
6. Missing/stale HP, conflicting same-timestamp values, maximum changes, overlaps within 500 ms (even from an excluded outsider), damage within the health observation interval or unexplained gains remain unverified and score zero.

This does not prove causal attribution. Natural regeneration, unobserved effects, packet loss and live server ordering can still affect HP deltas. It deliberately undercounts ambiguous healing rather than declaring raw potency effective. A post-heal full-health sample on its own is never treated as evidence that the heal was all overheal. Direct heals and actual HoT ticks use the same rule. Actual damage-over-time remains DPS.

HP current/max are read from the existing 008d stat groups (0 and 7). No maximum is inferred from the highest observed HP. The pinned reader currently records stat 7 as a maximum; its older comment notes earlier uncertainty on player HP, so Global player-HP calibration remains needed. Summon healing stays excluded.

The core score, HPS, skills/max heal, history, summaries and CSV agree. Raw amount, EffectiveHealing and HealingAssessment survive JSONL export/replay. Player/skill snapshots add RawHealing, ExcludedHealing and UnverifiedHealing. Excluded is raw minus scored for a supported estimate; unverified is separate and is not claimed to be overheal. Zero/unverified heals do not start or prolong a fight. Delayed reconciled heals can update the current fight without moving its clock backward or reopening a closed encounter. Old logs with no assessment retain their original amounts; demos/replays are labelled.

## Diagnostics to inspect

Menu → Export diagnostics, or gear → Debug:
- Party roster count, linked combat IDs, last valid roster timestamp, parsed/rejected roster counts, scope, class/level/server and binding provenance.
- Rejected actors versus targets outside a boss/dummy encounter.
- Healing raw, scored, excluded, unverified and pending totals. The Healing footer reports encounter excluded/unverified amounts; hover rows for per-player totals.

Exported reports contain combat names/metadata. They contain no packet bodies or network endpoints; review/pseudonymize names before sharing. Normalized JSONL additionally contains combat IDs, skill IDs and raw/scored healing.

## If a live issue remains: exact anonymized fixtures

First export diagnostics after the issue. If that cannot resolve it, an ordered decoded-frame fixture (no complete network capture is required) should contain:
- One pseudonymous stream ID per connection; ordered sequence index; relative receive time in milliseconds; opcode hex; payload hex **excluding the two opcode bytes**; retain order when several frames share one timestamp.
- Party case: 3336 own record; the complete 0297 snapshots for join, unchanged group, leave/disband; 4536 nicknames and 048d appearances for each member; relevant 4236 despawns; 4136 boss/dummy spawn; several 0438 hits and 0538 ticks; 2136 plus fresh identity/roster if teleporting. Include 0197 only to demonstrate that other-party records are not local membership.
- Healing case: 008d HP/stat frames for the recipient starting at least two seconds before the heal and continuing at least one second after. Include all interleaved 0438/0538 effects, including incoming damage and other healers, preserving order. Supply isolated examples of full-health direct heal, full-health HoT (initial announcement plus three actual ticks), partial overheal, useful non-overheal, and two simultaneous healers. A short 10–20 second selection per scenario is sufficient.
- Sidecar notes: exact Global client build/patch date and region; pseudonym for self and each party member; class/server aliases; target NPC ID; which skill was used; expected HP before/after from the game and approximate action time. Do not change real NPC/skill/stat codes or numeric HP/heal values.

Replace character/account names with consistent pseudonyms, retaining UTF-8 byte-length correctness. Replace entity/account identifiers consistently without collapsing distinct characters, preserving valid varint encoding and recomputing frame lengths when needed. Keep server IDs valid and distinct, preserving equality/alias relationships. Omit unrelated authentication, chat, launcher traffic, external addresses and account credentials. Do not paste a whole unreviewed PCAP publicly. Redacting bytes to zero without maintaining field lengths/layout makes the fixture uninterpretable.

## Source and validation

Primary reference, MIT licensed, pinned to 53033afadf3395dd47708abb308346734d8b1a8a:
- [Aion2FrameDecoder.cs](https://github.com/SkeeveAN/Aion-DPS-Meter/blob/53033afadf3395dd47708abb308346734d8b1a8a/Client/Aion2/Protocol/Aion2FrameDecoder.cs): DecodeRoster/HomeServerBehindName; direct/HoT and HP layouts. The roster comment mentions periodic messages; the directory lifecycle below states membership is a replacement snapshot. AscendAPP no longer depends on an assumed resend interval.
- [Aion2EntityDirectory.cs](https://github.com/SkeeveAN/Aion-DPS-Meter/blob/53033afadf3395dd47708abb308346734d8b1a8a/Client/Aion2/Aion2EntityDirectory.cs): NoteParty, NoteIdentity, server/home-server metadata and ambiguity. No class-only party matching, guessed summon ownership or local-player inference was ported.
- [Aion2SkillNames.cs](https://github.com/SkeeveAN/Aion-DPS-Meter/blob/53033afadf3395dd47708abb308346734d8b1a8a/Client/Aion2/Protocol/Aion2SkillNames.cs): class mapping and heal-family classification.

Full MIT notice accompanies source and binary. HP reconciliation is AscendAPP's implementation, not an upstream effective-healing claim. All added wire fixtures are synthetic; they are not supplied live captures. The user reported prior timer behavior working in game. New party/healing live validation remains pending.
