# AscendAPP 0.10.6 — Radar detection and score visibility fixes

## What the reported screenshot established

The meter was idle, where v0.10.5 showed GS/CP only in combat/party rows. The radar showed an aged character-facing report and no continuous-source status. A read-only check of the shared settings confirmed ContinuousSelfPosition=false. No settings were changed during that inspection, and the running game process was not read.

That setting leaves own tracking on the intermittent server/skill/dash path. It does not prove that the optional outgoing session-key lookup works with the current game executable.

## Install and enable

1. Close the older app. Extract the complete **Compact-Windows** ZIP into a fresh folder and launch AscendAPP.exe. The optional **Compact-Source** ZIP is separate. Shared settings and aligned zone maps carry over.
2. Open the radar. If it says **Skill reports only · enable continuous tracking**, click that line. This explicitly enables the optional read-only session lookup and opens Radar settings.
3. Keep capture running, then reconnect to the game once. A same-socket teleport cannot replace a missed connection handshake. After reconnecting, walk/run without using a skill.
4. Watch the visible status. The desired state while walking is **Client position · movement updates**. It may first say waiting for a verified character, session lookup or clock validation. A denied/unsupported lookup stays visibly unavailable. **Retry session lookup** retries discovery without discarding a valid cipher; missing bytes or a missed handshake still require a reconnect.

The feature remains optional and defaults off. It reads session data from the exact process owning the captured connection, without writes, injection, privilege adjustment or protection bypass. Game-protection and live client compatibility remain unverified. Do not disable protection for this feature. Previews/self-tests cannot invoke native game-process lookup.

## Fixes in this release

- **Always-visible source state:** the radar no longer hides the continuous-source status when the option is off. Click the line to enable or configure it.
- **Player proof from full profiles:** a full 0x4536 profile with a recognized class/faction and one valid level/GS anchor can establish a player identity. A short nickname or faction byte cannot. Known NPCs and summons remain excluded. This does not grant self identity or party DPS admission.
- **Large-map coordinates:** fully parsed other-player positions now share the own-position ±1,000,000 cm world bounds instead of a separate 200,000 cm cap. Relative range, finite-number validation, complete frame validation and player-only scope remain.
- **Fresh own pose priority:** delayed server motion and skill-facing callbacks can no longer overwrite a fresh outgoing own position/heading through a second callback.
- **Idle scores:** your own GS/CP fields remain visible below your name even without combat rows. Click them for the exact source/limitation. Party/encounter rows retain their inline scores.
- **Verified score continuity:** profile metadata can survive a world-ID change for up to two hours, but only a fresh verified exact name/server can rebind it. Old numeric actor IDs, unknown servers, ambiguous names and NPCs cannot inherit it. Observation time is not refreshed by teleports or UI updates. The cache is in memory only.
- **Diagnostics:** decoded full profiles, GS, CP and inspect GS are counted separately. Radar diagnostics distinguish named/unproven players, missing positions, unclassified contacts and defeats. Connection-owner mismatch and unsupported IPv4 access have distinct messages.

## What remains unknown

Own login (0x3336) currently has no verified GS/CP reader. The reference implementation reports that the usual nearby-player profile (0x4536) is sent for other characters, not for the local player. It was wrong to imply that adding row bindings would automatically produce your own real CP.

A verified inspect response (0x5036) can supply GS when it includes enough exact name/server/class information to join safely. Missing server metadata leaves it unbound. Nearby full player profiles can supply both GS and CP. Arena CP can be normalized by the game. Unknown or ambiguous values remain **—**; no damage-derived score, website item-level substitution or demo number is used in live mode.

Camera-only rotation while stationary remains unverified. The optional source provides actual outgoing movement and an inferred character-facing angle during supported movement. It cannot expose players the server has not sent, and it does not claim better detection than the in-game minimap. Unknown movement variants and expired/incomplete delta chains still wait for a new absolute anchor rather than guessing motion.

## Validation

Release build: zero warnings/errors. **611 core checks and 200 native WPF checks passed.** New checks cover full-profile player detection before skills, continued ordinary movement, large positive/negative world coordinates, NPC exclusion, ambiguous guild/server metadata, strict party-only DPS, score cache identity/age guards, actual TCP → decoder → radar detection, idle own-score bindings, inspect GS without fake CP, delayed-server callback priority and teleport score continuity.

The native checks run in preview mode with an explicit prohibition on game-process lookup. No new live packet fixture was provided. These are implementation checks using documented facts and synthetic bytes, not a claim of verified live precision or executable compatibility.

## If live tracking or scores remain unavailable

The new status makes the next diagnosis concrete. Save Settings → Debug → diagnostics and provide the status text if available. Reports contain counters and metadata, not packet bodies, session keys or endpoints.

For a score-layout mismatch, a sanitized decoded 0x4536 / 0x5036 fixture paired with the in-game GS/CP and a consistent invented name/server mapping is useful. For own CP, a reproducible, independently identified own-character profile/stat message containing the displayed CP is required before adding a reader; arbitrary integer scanning is not acceptable.

For movement/cipher fixtures, follow the safe sanitization instructions in CONTINUOUS_POSITION_0.10.5.md: movement-only decoded fixtures, or an offline re-encryption using a **new synthetic key** and synthetic setup strings. Do not share a real key, authentication payload, process dump or unsanitized PCAP.

## Sources

- [MIT Aion-DPS-Meter, pinned 0302618](https://github.com/SkeeveAN/Aion-DPS-Meter/blob/03026182c4ae2059b41804189852af6e945e365a/Client/Aion2/Protocol/Aion2FrameDecoder.cs): complete player profile, GS/CP, faction and inspect layouts; nearby nickname profiles are other-player data.
- [MIT Aion 2 Protocol Research, pinned fe2122f](https://github.com/lunixxx37/aion2-protocol-research/tree/fe2122f0e54e1de854c11e91e3c7bf51916a4f52): outgoing movement, cipher and bounded read-only session lookup. Existing full notices are preserved.
- NCSOFT class artwork retains its separate provenance notice. No unlicensed parser code was copied.
