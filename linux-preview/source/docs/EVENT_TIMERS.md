# Events & field bosses in 0.9.0

## Run and sync

Use the combat meter's bell button or Ctrl+Shift+F7. The tray and Event tracking settings also toggle the panel. It has its own position/lock/visibility and shares scale/opacity and game-focus topmost behavior with the combat meter. The timer close button only hides this panel. The combat meter remains the startup/main window; closing it exits the app and saves both sets of preferences.

Shugo Festival and Spacetime Rifts are selected initially. Dimensional Invasion possible slots can be enabled. The countdowns work offline from UTC table instants, displayed in your local time. A start is inclusive, an end exclusive, and the countdown switches from next start to scheduled-window end. These labels describe a table window, not live confirmation that an event is open.

The selected source's table gives Shugo every hour from 00:00 UTC, rifts every 180 minutes from 00:00 UTC, and possible invasion slots hourly at :30. The stored windows are ten minutes. Published community clocks disagree on Global rift phase and event duration. No official regional timetable was verified. A rift-only phase adjustment (-180..+180 minutes) is therefore exposed. Windows' daylight-saving changes alter the local display, not the UTC schedule. Report region does not silently choose another clock. Check the game's own countdown before relying on the schedule.

For boss times, keep capture active and obtain your own character/server identity. Open the in-game map's Field bosses list in Verteron. The 0x0191 list supplies absolute UTC timestamps; the four chosen NPC/slot identities are:

| Boss | Map | Place number | NPC |
|---|---:|---:|---:|
| Eternal Gartua | 1010 | 21 | 2101074 |
| Silent Dartan | 1010 | 22 | 2101120 |
| Soul Ruler Kashapa | 1010 | 23 | 2101122 |
| High Commander Lagta | 1010 | 24 | 2101131 |

No guessed map matching, respawn cycles, coordinate-based identity learning, distance estimates or screenshot deadlines are used. Altgard has a different slot order and is intentionally excluded from this Elyos panel. Packet decoding validates the whole list before publication, including optional extra bytes and bounded padding; malformed/truncated lists do not partially change timers. A valid list before own server detection is counted as unbound and not assigned to a previous server. Detect identity and reopen the map list.

## States and saved files

Unknown shows — and Waiting for map list. A dead entry with a nonzero deadline counts down to that precise timestamp. Once passed, it shows Due · refresh; it does not assert the boss spawned. An alive entry says Seen alive with an observation time and tooltip, rather than promising it remains alive. A dead entry with a zero timestamp has unknown respawn time. Reopen the map list to refresh stale information.

fieldbosses.json stores at most 128 selected-boss observations with numeric server/map/NPC IDs, alive/dead flag, timestamp and observation time. States from different servers are separate; old/out-of-order observations cannot overwrite a newer record. Cached observations can be shown while capture is stopped, with their original times. A new active capture waits for character/server identification before choosing a cache. Settings can forget these snapshots. No endpoint, position, packet body or character name is saved in this file. Ticket/character files remain separate.

Optional sound and local tray alerts are off initially. Selected timers use the configured lead minutes and event volume; due alerts say to check the game, not that a boss spawned. Alerts require the app to keep running and continue when the timer panel is hidden. Existing manual reminder behavior remains available in the main meter.

## Provenance and verification

Primary MIT source, pinned revision: [SkeeveAN/Aion-DPS-Meter](https://github.com/SkeeveAN/Aion-DPS-Meter/tree/53033afadf3395dd47708abb308346734d8b1a8a). Referenced: Client/Aion2/Aion2FieldBosses.cs, Client/assets/aion2/bosses/field_bosses.json and Client/assets/aion2/schedule/events.json. Only selected layout algorithms and factual English IDs/names/cadence were adapted. The full MIT notice accompanies the source and Windows app. The screenshot supplied names and a visual reference; its untimestamped countdowns were not used as anchors.

232 core checks and 45 native WPF checks passed. New tests construct synthetic payloads; no actual Global boss-list capture was supplied. OS hotkey conflicts, real tray notifications and the live map-list field order still need an in-game check. The UI was rendered from the actual WPF views, with no binding warnings.

If boss rows stay unknown after identity detection and map opening, export diagnostics. It includes FieldBossLists, UnboundFieldBossLists, current server and selected observed boss states, without packet bodies or endpoints. If parsing/mapping needs calibration, the exact missing fixture is an ordered anonymized 0x0191 map-list payload from Verteron with its receipt time, alongside the displayed map and which of the four bosses are alive/dead and the remaining time at that moment. Include a sanitized own 0x3336 record or just its validated server code to establish ownership. Preserve all place numbers, optional bytes, timestamps and lengths; remove endpoint/account data and replace character names with same-byte-length aliases. One living and one dead selected boss, if available, cover both layouts. No full unreviewed capture, chat, inventory or login credentials are required.
