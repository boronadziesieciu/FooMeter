# Teleport identity in 0.8.2

The app remains passive; no packet injection or automatic server requests are made. The zone message is 0x2136. The pinned MIT source reads a counter at full-frame byte 2 and zone ID at byte 6. AscendAPP stores the opcode separately, so those payload offsets are 0 and 4. A positive zone ID and complete 8-byte prefix are required. A new counter/zone pair triggers re-detection, including same-zone teleports; duplicate counter/zone pairs do not.

At that boundary the old self ID, entity metadata, summon ownership and party matches are invalidated. The controller archives/clears the encounter, filters and forecasts. A subsequent 0x3336 own-character record establishes the current identity, then announces it again to the strict capture filter and compact view model. Damage stays excluded while self is unknown; ordinary monsters and nearby players never become a fallback. A new roster is needed for new party hits. Same-name different-server identities remain distinct.

New world TCP connections on the same capture interface can replace an old one immediately on a validated character record, or on a validated zone message from the previously observed server source port. Other adapters and reverse directions do not preempt a recently active stream. Retired world connections cannot reclaim identity with late frames. SYN/FIN/RST handling and TCP de-duplication remain in NetworkPipeline.

Ticket counts are cached with verified name/server provenance, hidden while identity is unresolved, and discarded for a different character unless a fresh ticket list was received. Counts are observed snapshots, never refilled or guessed.

## Verification limits

The protocol shape comes from SkeeveAN/Aion-DPS-Meter revision 53033afadf3395dd47708abb308346734d8b1a8a (MIT). The tests use synthetic wire fixtures. No new real Global teleport capture was available. In particular, this release requires a supported own-character record after the zone boundary. If a teleport only sends that record before the boundary, sends another layout, or sends no record at all, the app cannot safely bind self from that sequence yet. It remains visibly unresolved instead of reusing stale IDs.

## If it remains refreshing in game

First export diagnostics from the overlay menu after one teleport. It contains zone/refresh/own-record counters and an opcode census without packet bodies or endpoints. Do not post settings.json, characters.json or a full unreviewed capture.

To resolve an unsupported sequence, the precise fixture needed is an ordered, anonymized frame sequence spanning about 3 seconds before to 10 seconds after ONE failed teleport, including:

- Every 0x2136 zone notice and 0x3336 own-character record in that interval, including any record before the zone boundary. Preserve payload bytes/lengths apart from anonymized names.
- Relevant 0x0036 clocks, 0x4536 nicknames and 0x048d appearance frames for the old/new self IDs. A missing 0x3336 must be recorded as absent rather than omitted accidentally.
- Stable connection labels (A/B), capture-surface labels, direction, relative timestamps and exact frame order. If diagnosing TCP handover, include sanitized TCP sequence/flags and segmentation as well.

Replace IPs, hardware IDs and endpoints with labels. Replace character/guild names with alphanumeric aliases of the same UTF-8 byte length, consistently across records; retain server codes and the relationships among combat IDs, including reused IDs. Ticket, inventory, chat, account/login credentials and unrelated packet bodies are unnecessary and should be omitted. These fixtures are only needed if the diagnostic counts show an unresolved sequence; no fixture is needed just to run the build.
