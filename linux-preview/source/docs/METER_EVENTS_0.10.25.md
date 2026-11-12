# AscendAPP 0.10.25 — Meter and events

## Changes

The separate selected-target skill tracker has been removed completely: window, menu and tray actions, shortcut, settings, profile HTTP requests, skill-icon downloads, cooldown parser, bonus audit, cast/selection readers and manual buff/skill presets. The two remaining overlays are the compact DPS meter and events/boss timers. Damage-by-skill breakdown is retained as combat analysis.

History now supports target/class/player search and same-target comparison in the black Obsidian palette. Known NPC IDs must match; no fallback to a target name is allowed when only one record has an ID. Name-only matching is used when both IDs are missing. Party eDPS is total damage divided by full duration. Own eDPS comparison requires the same captured name/class; it is not a verified home-server identity. Invalid/active/same-encounter baselines are rejected and zero baselines remain unknown. Party composition, gear, phase and difficulty may differ, so the UI labels the comparison as recorded differences rather than guaranteed improvement.

The event overlay has independent scale (80–150%) and opacity (20–100%). Existing profiles initially inherit the meter's values. Follow meter appearance clears the overrides. A custom ComboBox-template bug was fixed so selected display labels show correctly instead of an object's record representation.

Existing automatic GS/CP, original class art, self/confirmed-party filtering, boss/dummy restriction, effective-healing estimates, forecasts, tickets/Odyle, reminders, history and exports remain. Retired settings are removed on profile save; current meter and event settings, custom event reminders, tickets and saved boss observations remain intact. No map or skill tracker can be revived by loading old settings.

## Verification

Release solution build: 0 warnings, 0 errors. 453 core checks and 154 native WPF checks passed (607 total). Removed-feature checks were removed along with their implementations; a lower total than 0.10.24 is expected.

New tests cover history search/privacy, comparison mathematics and identity/target guards, zero/invalid baselines, real WPF search/baseline/empty controls, independent event appearance and inheritance reset, the corrected dropdown label, compiled absence of the retired implementations and old-settings migration. Retained tests include actual synthetic Ethernet/TCP/framing-to-GS/CP bindings, party admission, dungeon continuity, overheal reconciliation, boss timer continuity, settings persistence and native Alt/click-through recovery for the two overlays.

Native damage, healing, minimal-scale, event, history and settings previews rendered without binding warnings. Damage/history figures are explicitly synthetic Demo data. No live Npcap session, actual game fight or competitor binary was started in this review. Current Global packet accuracy still requires in-game testing.

## Research

Read COMPETITOR_REVIEW_0.10.25.md for findings from official AbyssDPS and NotMeter documentation/tutorials/public repository metadata. This was a public feature/architecture review, not binary decompilation. Their inspected public distributions did not provide reusable desktop decoder implementation. No competitor code, artwork, cache data or executable was incorporated.

The most useful next work is a bounded persistent local archive, a one-line combat strip and clearer capture-health status. Richer hit/death analysis needs verified recorded fields. Cloud uploads, rankings, normalized damage and damage formulas need separate data/services; no such values are fabricated.

## Upgrade

Close the previous app. Extract Compact-Windows into a new folder and run AscendAPP.exe. .NET 10 Windows Desktop Runtime x64 and Npcap are required; Windows asks for administrator consent through the existing manifest. Your shared settings load automatically. Compact-Source is for development and should not be merged into the Windows folder.

History remains the existing bounded session archive and does not survive app restart. Event schedule rows remain pinned timetable estimates; observed boss deadlines are saved by server. This build does not introduce new timing tables or new live decoder claims.
