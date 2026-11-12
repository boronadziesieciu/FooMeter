# Timer continuity — AscendAPP 0.9.5

The field-boss cache was not being erased on teleport. The timer panel set its selected server to null whenever live self identity disappeared, then requested a snapshot for no server. It also cleared the selected server when capture started. That turned previously observed boss rows into unknown rows despite valid stored observations.

The timer server now has an independent lifetime. Verified server notifications are retained safely across capture/UI threads, including character and map records followed by a teleport within one UI refresh interval. A valid own-character server selects it; a temporary missing identity leaves it selected. Shared preferences save WorldTimerServerId, so startup prefers your last confirmed timer server instead of whichever server has the newest cached observation. When no selected-server preference exists, the existing last snapshot server is still available as a fallback. Invalid saved server IDs are discarded.

## Behavior

- Teleports, ordinary zone changes, dungeon entry/exit and socket changes preserve the current server's absolute boss deadlines and original observation timestamps.
- Saved S… in the footer identifies retained server context while character detection is pending. This is cached information, not a fresh observation of boss state.
- A newly verified different server selects its own observations; it never inherits another server's countdowns. Switching back restores that server's original deadlines.
- A map list without current verified character/server identity remains unbound and does not overwrite the retained cache.
- Due deadlines remain Due · refresh. Seen alive means alive when last observed. The app never restarts an assumed respawn cycle.
- Shugo/rift schedules keep their UTC phase. Manual tracker timers remain active independently of zone/session resets.
- Forget boss snapshots remains the explicit way to remove cached observations. Reopening the in-game map provides a newer observation when available.

The v0.9.4 dungeon party behavior remains: party scope can clear on dungeon exit while independent world/event timers continue. A boss fight's defeat/berserk forecast still belongs to that encounter; it is not a permanent world-boss respawn timer.

## Validation

353 core checks and 93 native WPF checks pass; zero Release build warnings/errors. The new native tests send synthetic Ethernet/TCP frames through the actual production pipeline, decoder and MeterController, exercising field-boss row bindings. The timer server selector now runs independently of whether a native capture device is open, so these tests exercise the same identity-loss selection path as the live app rather than the old preview-only shortcut.

Coverage includes ordinary zone change with no self record, dungeon entry/internal movement/exit, new world socket, FIN-first closure, retained row identity, original timestamp/deadline preservation, selection of another server with no snapshot, restoration of an earlier server cache, rejection of unbound map updates, normal expiry, UTC event schedule phase, manual tracker continuity, explicit snapshot deletion, preference validation/persistence and startup preference priority over another server's newer snapshot. Tests do not access the actual user profile or start Npcap, sound or hotkeys.

No new real packet capture was supplied; live behavior still needs the user's game test. Existing observations remain local snapshots and may become stale as other players kill bosses. If a timer still disappears after a teleport, export diagnostics before and after, and note which timer disappeared and whether its row became unknown or its countdown restarted. No new packet interpretation is introduced by this release.

## Upgrade

Close the old app, extract the complete Windows ZIP into a fresh folder and launch AscendAPP.exe. Keep the optional Source ZIP separate. Shared settings, selected timer server and field-boss snapshots use %LOCALAPPDATA%\AscendAPP. Existing settings and snapshots are reused automatically. The app still needs the installed .NET 10 Desktop Runtime and Npcap.
