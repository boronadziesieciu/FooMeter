# Shared settings — AscendAPP 0.9.3

## Upgrade

1. Close the old AscendAPP, keeping its folder for the first launch of the new version.
2. Extract the complete Windows ZIP into a fresh folder and run AscendAPP.exe. Do not merge the optional Source ZIP with it.
3. First launch imports the old profile when discovered. Future 0.9.3+ builds automatically use the same Windows user profile.
4. In gear → General → Saved data, inspect the active location or use Import older settings for an unusual old folder location. Choose its settings.json. Manual import replaces preferences, first backing up the current shared file. It leaves existing ticket/boss snapshots alone.

## Location and behavior

All three files use %LOCALAPPDATA%\AscendAPP for the same Windows account, independent of version and extraction location:

| File | Retained data |
|---|---|
| settings.json | Capture/display options, hotkeys, both overlay positions/locks, timer presets, reminders and other preferences |
| characters.json | Last observed character ticket counts and observation times |
| fieldbosses.json | Last observed server-specific field-boss map snapshots and timestamps |

This is local storage, not an account service or cloud sync. Old versions before 0.9.3 continue saving beside their executable. Their later changes will not overwrite the shared profile automatically. Close the previous version before opening a new one; concurrent instances can save competing preferences. The app does not migrate encounter history or turn stale observations into live data.

## Initial import

A valid shared settings file wins immediately, without scanning old folders. Otherwise the app checks its own executable folder first, then running older AscendAPP processes, then AscendAPP folders beside its parent, on the Windows Desktop and in Downloads, plus the normal per-user installer folder. Discovered candidates are ordered by settings modification time; invalid or oversized candidates are skipped. Folder discovery is bounded to three levels and 1,000 directories, does not descend unrelated trees and does not follow directory links. Very deeply nested or custom locations need manual import.

Settings are validated before import. Optional character/boss files must have array JSON roots within size limits; malformed optional files produce a notice without blocking preference migration. Existing shared snapshots are not replaced by old ones. Original old files are left untouched. Invalid shared settings are backed up with a settings.json.backup- timestamp and unique suffix before recovery.

Saves use unique temporary files and atomic replacement, serialize concurrent in-process saves and briefly retry transient file-sharing failures. No profile file is bundled in the release archives. Unknown future preference properties, including custom timer/reminder options, survive read/save cycles, although unsupported options naturally have no effect in an older build. This does not promise compatibility with hypothetical future schema/type changes.

## Recovery

Open gear → General → Saved data → Open saved data folder. Back up this folder before editing it manually. To restore one of the generated settings backups, close the app and copy the backup over settings.json. To reset preferences, close the app, back up shared settings, move old beside-app settings files out of discovery or rename them, then rename shared settings.json; otherwise initial import can recover an older copy instead of defaults. Tickets/boss observations can remain independently.

If the shared folder cannot be written, the app reports the error rather than claiming settings were saved elsewhere. The new build does not upload profiles or raw packets.

## Validation

320 core checks and 75 native WPF checks pass. New coverage includes initial import, shared-profile priority, latest valid candidate selection, bounded folder discovery, corrupted-file backup, size limits, optional malformed files, preserved unknown fields, manual replacement/backup, a profile appearing during discovery, concurrent atomic saves, failure cleanup, desktop preference/timer reload and subsequent-instance reuse. Test fixtures stay under the workspace build folders and are cleaned up. Native checks do not access the real shared profile, capture traffic or register global shortcuts.

No actual user profile was changed by the development tools. The automatic import is performed by the app on its first normal launch. This release changes profile storage and includes the previously tested optimization work; it adds no new live protocol claims.
