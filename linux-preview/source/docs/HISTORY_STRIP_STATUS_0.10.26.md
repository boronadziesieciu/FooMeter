# AscendAPP 0.10.26 — Saved fights, combat strip and capture status

## Saved fight history

Captured encounters now save automatically in %LOCALAPPDATA%\AscendAPP\history.json, alongside the existing shared settings. This path is independent of the release folder, so the archive survives app restarts and future updates. It stores combat summaries, player/class identities, skill breakdowns and damage timelines. It contains character names; it stores no packet bodies, network endpoints or game credentials and is never uploaded.

Completed segments are queued for one background writer; capture callbacks do not serialize files or wait for disk. Writes replace the previous file atomically and flush the new contents. Normal app shutdown drains capture, saves the current encounter as partial at its last admitted activity, and waits for queued writes. Manual reset and stopping capture also preserve partial segments. A partial record is not a confirmed boss kill.

Retention: latest 80 encounters, 16 MB for the archive, and 2 MB per encounter. Oversized/invalid records remain available in the current session but can be omitted from disk, with a limit message in history diagnostics. Save failures leave the current data in memory and show a notice/status. Sudden process/power failure can still lose an active fight or a just-queued write; normal exit is tested. History from sessions already lost before this version cannot be reconstructed.

Schema validation bounds players, skills, timeline, numbers and file size. Damaged JSON is copied to history.json.recovery before new recording replaces it; a file with mixed valid/invalid records keeps the valid records and preserves the original. Oversized archives and unknown future schema versions are kept read-only until explicitly cleared. A sidecar file lease prevents another running copy from overwriting this instance's archive; a second copy reports read-only history. Close it and restart after the other copy exits to regain write access.

More actions → Encounter history keeps the existing search and same-target comparison and now provides Export selected, Delete selected and Clear saved. Deletion asks for confirmation. Export writes a versioned snapshot JSON with full details, not an event JSONL replay. Clear current meter clears the live display while preserving saved history. Clearing history keeps the current fight and event timers; a later completed current fight can be saved as a new record.

Demo/showcase and imported-replay data remain temporary and are excluded from the persistent archive and the normal saved-fight browser. Existing JSONL replay/import remains available separately. No synthetic values are presented as captured results.

## One-line combat strip

The normal compact meter remains the default. Use the strip button in its header, More actions → One-line combat strip, Settings → Display, or Ctrl+Shift+F6 to switch views. The preference and shortcut persist across versions. The strip reuses the same overlay and live encounter; switching never resets damage or creates another window.

One row contains the move grip, capture indicator, your rate, party DPS, target, observed target HP, berserk estimate, fight clock and controls. Unknown self identity shows — instead of borrowing another player's DPS. Healing mode uses your HPS; the party value remains damage per full encounter duration. Missing HP/berserk remains unknown. The strip is about 680×42 logical pixels at 100%, scales with the UI and reduces scale if necessary to fit the work area. Its expand button returns to the compact bars.

The existing padlock/click-through behavior applies. Hold Alt to interact with a locked strip or drag its grip; releasing Alt restores protection. Events remain their own overlay with their own scale/opacity. The strip does not show the normal multiline notice/footer/custom-reminder rows; tray event alerts and the separate events overlay continue.

## Capture status

The header now has a colored, labelled status button. The strip has the corresponding status dot. Hover for the explanation and counts; click to open Settings → Debug.

- Stopped / capture error: explicit stop versus a failed start or unavailable Npcap.
- No packets: capture is open but nothing has arrived.
- No payloads: packets arrived without TCP data.
- No frames: TCP data arrived without recognized framing.
- Refreshing: own character data is being refreshed after a world change.
- No events: candidate frames arrived without decoded combat; this can be normal while idle.
- Character?: combat decoded but own identity is unknown.
- Filtered: combat decoded but none admitted to the current meter; only self/confirmed party and bosses/dummies count.
- Ready / in combat: decoded data and identity are available, with admitted encounter data.
- Traffic quiet: no TCP payload for more than 15 seconds; old totals remain visible.
- Demo / replay: synthetic or imported values, separate from live capture.

The tooltip distinguishes historical unknown actors from current identity, so an earlier unknown event does not permanently mark a recovered session as unknown. Counts describe this app's pipeline; unrelated TCP traffic, missing packets and unsupported Global messages remain possible. A green badge is not proof of complete decoder accuracy.

## Validation

Release build: 0 warnings, 0 errors. 479 core checks plus 178 native WPF checks passed, 657 total. Tests cover real file commits/reopens, skill/timeline retention, interrupted shutdown, identity-safe comparisons, deletion/export, corruption recovery, unknown schema, oversized data, read-only competing owners and failed writes. Native controller restart tests verify preferences/history load without replaying saved DPS into the live overlay, plus exclusion of demo data, strip geometry/scaling/Alt interaction and actual badge bindings. Existing capture, automatic GS/CP, party, healing and timer regressions remain covered.

Damage, strip, minimal strip, healing, history, event and settings previews render without final binding errors. Previews use explicitly labelled synthetic demo data. No actual game session or native packet capture was started, and no new live protocol interpretation is claimed.

## Upgrade

Exit the previous app, extract Compact-Windows into a new folder and run AscendAPP.exe. Existing shared preferences load automatically. Requires .NET 10 Windows Desktop Runtime x64 and Npcap; the existing administrator manifest still requests Windows consent. No server or extra overlay is added. Compact-Source is for development and should not be merged into the Windows folder.
