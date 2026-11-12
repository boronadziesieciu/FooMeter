# AscendAPP 0.8.0 — Compact / Obsidian

This release replaces the dashboard and expanded meter with **one adaptive compact overlay**. It opens on launch; the gear in its top bar opens Settings. The English UI uses a shared charcoal/mint palette and original vector icons throughout.

## Run it

1. Close your older AscendAPP copy. Extract the entire Windows ZIP into a new, writable folder.
2. Run **AscendAPP.exe**. As with v0.6/v0.7, the portable build uses the installed **.NET 10 Desktop Runtime** and a separate **Npcap** installation. Neither is bundled in this ZIP.
3. The compact overlay opens first. Capture starts automatically by default. Leave adapter compatibility enabled and the TCP port blank unless you need a manual filter.
4. Start before entering the world from character selection so the app can observe your identity and ticket snapshot. Start before the boss pull for timing data.
5. Use the **gear** for preferences, **history** for completed fights, and **menu** for capture, reset, timers, import and export. Click a player bar for damage/healing details. Click **Odyle** for saved character ticket snapshots.

The idle overlay is about **420 × 130**; combat adds compact player bars and target information. More players and timers scroll within the same window. There is no expanded meter or alternate dashboard. Scale, opacity, visible rows and position can be changed in Settings.

## Functional settings and tools

- **General:** automatic capture at launch, all-adapter/loopback compatibility capture, adapter selection, optional TCP port, start/stop and refresh. Region labels reports; it does not infer game schedules. English remains the UI language.
- **Combat:** configurable idle timeout, keeping known boss segments open through idle mechanics, finishing on observed defeat/disengage/full-health reset, strict party-only capture, known-boss/dummy targeting, explicit dummy NPC IDs and timed dummy parses.
- **Display:** encounter DPS or active DPS, damage-column visibility, party-rate and boss-forecast visibility, hidden other-player names, summary format, local time format, opacity, scale, row count and display refresh rate. Active DPS uses the observed span from a player's first to last damage record, with a minimum of one second. Completed fights retain that span so switching modes does not invent new damage.
- **Overlay behaviour:** focus mode reveals controls on hover, idle fade, faded opacity/delay, position lock, game-focused topmost behaviour, actual mouse click-through, remembering click-through, minimize to tray and game-close tray behaviour. Game focus/close behaviour uses the configured process name and is off by default.
- **Audio:** separate event and tracker volume, mute at zero, and preview buttons. A generated chime avoids external sound assets.
- **Buff tracking:** user-defined **manual** buff/cooldown timers, class-specific presets, duration, ready sound, visibility rules, start and cancel. Start them from Settings or the overlay menu. These are custom timers, not automatically decoded buff uptime.
- **Event tracking:** user-defined date/time reminders, repeats, several lead times (0–45 minutes), start alerts and visibility outside fights. Times preserve their UTC offset. No world-boss or event schedule is assumed from your region.
- **Keybinds:** capture and save global shortcuts for hide/restore, reset, clear, copy and click-through. Invalid, duplicated or unavailable registrations are reported. There is no expand-layout shortcut because only one compact layout exists.
- **Tickets:** observed count snapshots for Odyle, Nightmare, Daily Dungeon, Ascension Trial, Shugo/Invasion keys and other named count tickets. Saved characters show their server, counts and observation time. Missing tickets remain unknown. There are no assumed caps, refill schedules or costs.
- **Local analysis:** damage and healing views, class symbols, player/skill breakdown, critical/max-heal statistics from actual healing records, encounter history, single/multi-line copy, JSONL replay/export, CSV and diagnostic reports. Capture filters do not silently remove records from an imported replay or labelled demo.

Strict party mode matches **name and server** against a recent observed local roster. Without that membership, only explicit self damage is retained. It defaults off to preserve the tested v0.6 capture behaviour. Boss-only mode uses the explicit boss catalogue and the dummy NPC IDs you configure; unknown targets are not guessed to be bosses/dummies.

## Features that still need data or services

The screenshot categories are represented in Settings, with capability explanations rather than switches that imply support:

- Automatic buff/debuff and cooldown detection requires additional verified Global messages and skill/effect metadata. Manual timers work now.
- Death counts/death causes and combat-power/gear-score chips are not decoded yet.
- Account login, profile sync, leaderboard ranks, server nameplates and automatic uploads need a separate AscendAPP backend. None is configured or contacted.
- Automatic game-event/world-boss schedules and updater services are not provided. You can create local reminders and install updated ZIPs manually.

No Abyss application code, binary, icon, game art or account endpoint was copied. Your screenshots were visual/feature references. Existing MIT protocol provenance and separate-library notices remain in THIRD_PARTY_NOTICES. The additional count-ticket labels come from the same pinned MIT repository.

## Recovery and saved files

Default global shortcuts:

| Action | Shortcut |
|---|---|
| Hide / restore overlay | Ctrl+Shift+F10 |
| Reset encounter | Ctrl+Shift+F9 |
| Clear meter and history | Ctrl+Shift+F8 |
| Copy current damage/healing summary | Ctrl+Shift+F11 |
| Toggle click-through | Ctrl+Shift+F12 |

**Ctrl+Shift+F10 restores input when click-through is on.** The tray's **Show overlay** / **Disable click-through** actions also restore input. Click-through is only enabled when a tray recovery route exists. Shortcut conflicts appear in the notice and Debug page.

Preferences are stored beside the app in **settings.json**. Observed character ticket snapshots are in **characters.json**. They may contain your character names; they are local files, not uploads. Closing the overlay exits the app and capture; minimizing can send it to the tray. If needed, close the app and rename settings.json to restore defaults on the next launch. Corrupt settings recover to defaults; unavailable saved window positions are kept within the connected desktop bounds.

The hide-names option affects visible player rows and detail selectors. Copy and exports deliberately retain original character names, as described by that setting.

## Live data and forecasts

You reported v0.6 character/damage capture working in game. The newer energy, ticket, roster and boss-clock fields still need live Global verification. No new real packet capture was supplied or used for this release. Protocol tests construct synthetic bytes.

Defeat estimates use current observed HP / measured damage rate to that target. Party scope is explicitly shown; without a fresh roster the estimate is labelled tracked target/boss damage and can include other players. A berserk countdown needs both an explicit NPC-duration entry and an observed combat-start notice. Plain in-combat notices are approximate, especially if capture started mid-pull. There is no default 300-second timer. Missing or stale data shows —; no live DPS, ticket balance or clock is fabricated.

Reports distinguish no packets, no TCP payloads, traffic without recognized framing, frames without combat events and missing player identity. They contain counters, known metadata, current display calculation mode and a bounded opcode census, without raw packet bodies or endpoints. No raw packet files or automatic network uploads are created.

## Validation and source build

- Release build: **0 warnings, 0 errors**.
- **143 core checks** passed, including the original capture/framing tests, malformed payload cases, ticket parsing, preference persistence/recovery, strict membership filtering, both DPS modes, finished-fight recalculation, boss lifecycle, hotkeys and reminder scheduling.
- **28 headless native WPF checks** passed, exercising settings controls, name hiding, scaling, healing order/detail construction, custom timer/reminder creation, reset and clear. No capture, sound or global shortcuts run during these checks.
- The actual compact UI and all 13 settings/detail pages rendered without binding warnings. Idle, combat, healing and minimal display states were visually checked.
- The new live protocol fields, real game focus/process matching, OS shortcut conflicts, tray operation and audible output have not been manually verified in your game session. The Windows ZIP is a framework-dependent portable build, not a signed installer.

On Windows with .NET 10 SDK:

~~~powershell
dotnet build ./AscendAPP.slnx -c Release -m:1 -p:UseSharedCompilation=false
dotnet run --project ./tests/AscendAPP.SelfTest -c Release --no-build
# Native checks without starting capture, sound or hotkeys:
dotnet ./src/AscendAPP.Desktop/bin/Release/net10.0-windows/AscendAPP.dll --self-test-ui ./artifacts/ui-checks.json
# Native bitmap previews, explicitly synthetic:
dotnet ./src/AscendAPP.Desktop/bin/Release/net10.0-windows/AscendAPP.dll --render-preview ./artifacts/compact.png
# Optional: --unknown, --healing, --minimal, or --settings Display
~~~

The local restore used cached dependencies with NuGetAudit=false because the online audit service was unavailable. This is not a permanent source setting. Optional installer scripts are in packaging/. Older release/design documents describe historical versions; this README supersedes them for v0.8.
