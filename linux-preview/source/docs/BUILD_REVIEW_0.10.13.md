# AscendAPP 0.10.13 — Build review and character scores

Reviewed 25 October 2026. This release fixes demonstrated code/fixture bugs. It has not been verified against a new live Global game capture.

## Install and check

1. Close the older AscendAPP instance. Extract the **entire Compact-Windows ZIP** into a fresh folder, then open **AscendAPP.exe**. Keep the optional Source ZIP separate.
2. Approve the normal Windows administrator prompt. The executable retains its requireAdministrator manifest; UAC is not bypassed. Existing .NET 10 Windows Desktop Runtime and Npcap are required.
3. Preferences, timers and map setup reuse the shared profile under the same Windows account. This release does not relocate or erase that profile.
4. Keep capture running and open/refresh your character's in-game inspect/profile window, then inspect a party member. GS/CP appear on a dedicated line beneath each name and in the own-character footer. Hover a score for its source and each field's actual age; click the own scores for Character settings.
5. Enter a boss/dummy encounter, teleport with the party, and confirm the correct party remains shown. Cached ratings require the exact name and home server; rates remain unknown until admitted combat occurs.

**Own CP may still be blank.** The supported own login (0x3336) and inspect (0x5036) paths do not contain a verified CP field in the reference implementation. A supported full player profile (0x4536) can supply it if received. Guildless/unsupported inspect records without a verified server remain unbound. The app does not guess scores or substitute website item level. Refresh displayed scores only redraws known data; it does not send a profile request.

## Fixed

- Initial TCP segments arriving out of order after a captured SYN no longer discard the first character/profile frames. SYN-only streams share the bounded flow limit; retransmission, wraparound and adapter separation are covered.
- A validated full profile can supply CP even when its GS is unavailable. Partial profile/inspect updates retain independently observed values without renewing their ages.
- Exact current party name/home-server/class can associate inspect GS before a world actor ID is available. The roster displays cached scores while identity refreshes; no combat IDs or damage are created.
- Newer exact cached inspection data supersedes an older actor-bound score. Same-name players on another server cannot inherit it.
- CP expires after 10 minutes without observation; GS after 2 hours. Each score tooltip states its own age. These are local freshness policies, not claims about game refresh cadence.
- GS/CP occupy a readable second line within the compact player row, leaving room for names and damage. Scale checks cover 80%, 100% and 150%.
- Removed/replaced own identities cannot retain the previous character's heading. Own death clears live orientation and displays a defeated last-location view; revival requires a fresh post-revival position. Delayed pre-revival reports cannot restore it.
- Coincident marker clicks select the visible topmost contact rather than a covered one.

## Verification

- Release build: **0 warnings, 0 errors**.
- **802 core checks** passed, including capture/reassembly, framing, protocol fixtures, combat admission, healing, settings, timer continuity, ratings and radar lifecycle.
- **267 native WPF checks** passed with no binding warnings, including actual synthetic TCP profile ingestion into the own footer/DPS row, roster cache recovery, score expiry, scale layout and radar interactions.
- Native compact-meter and radar previews rendered and visually reviewed. Their sample names, scores and damage are explicitly DEMO data, not captured measurements.
- Development checks run through the isolated preview/test entry points, with no live capture or game-process reads. A passing suite does not establish complete live packet decoding, correct camera yaw or 100% movement uptime.

## Remaining live-data limits and exact fixtures

If scores still show dashes, first export **Settings → Debug → diagnostics** after inspecting yourself and one party member. The profile counters distinguish full records, recognized GS, recognized CP and inspect GS; tell us which in-game character window you opened and whether that character has a legion/guild.

Completing unsupported own/guildless scores needs anonymized, **server-to-client decoded frame fixtures**, not a process dump or private session keys:

- Own login/character record **0x3336**, full player profile **0x4536**, and inspect response **0x5036**, where present, with exact frame boundaries, opcode, payload byte length, build/revision and relative timestamps.
- A short before/after sequence while opening your own inspect and one party member's inspect. Record the GS and CP displayed by the game at each timestamp; include one guildless case if that is failing.
- For field identification, two observations of the same character before and after one known equipment change, with the resulting in-game GS/CP. State the zone and whether it applies normalized PvP stats.
- Consistently replace names/guild names and actor IDs while preserving encoded lengths, field widths and links across frames. Keep class, faction and home-server identifiers needed for association; strip unrelated chat/account identifiers. If reliable byte-preserving anonymization is unavailable, do not hand-edit arbitrary packet bytes; start with the shape/counter diagnostics so a targeted fixture extractor can be prepared.

No credential material, RSA private keys, RC4/session state, full memory dump or unrestricted traffic capture is required. Frame layouts still need independent live confirmation; synthetic fixtures in this package only demonstrate the implemented interpretation.

## Protocol provenance

Existing MIT reference facts are retained from [SkeeveAN/Aion-DPS-Meter, pinned decoder](https://github.com/SkeeveAN/Aion-DPS-Meter/blob/03026182c4ae2059b41804189852af6e945e365a/Client/Aion2/Protocol/Aion2FrameDecoder.cs). This review introduces no guessed GS/CP offsets or newly copied third-party assets. Licenses and notices remain in THIRD_PARTY_NOTICES.

The compact English UI, administrator launch, self/confirmed-party boss/dummy meter policy, conservative HP-based healing and independent timer/radar overlays are preserved. Earlier release notes in docs are historical.
