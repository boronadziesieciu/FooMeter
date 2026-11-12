# AscendAPP 0.7.0 — Obsidian / Split Command

English Windows app and Elyos overlay. This release adds Odyle Energy and boss forecasts to the v0.6 capture path that you reported working in game. **The new energy, party-roster and boss-clock fields have synthetic tests, but have not yet been verified against your live Global session.**

## Try v0.7

1. Extract the entire Windows ZIP into a new folder. Keep the DLLs, data, samples and notices beside AscendAPP.exe.
2. Run AscendAPP.exe. This portable build uses the installed **.NET 10 Desktop Runtime** and a separately installed **Npcap**, as v0.6 did. It is not an installer.
3. Leave **Auto-detect game connection on all adapters** checked and the TCP port blank. Click **Start capture before entering the world from character selection**. Energy can arrive several seconds before the character record, so starting only after login may miss it.
4. Open the overlay. It starts in **Split Command (660 × 540)**; the toolbar can switch to the **422 × 92** compact Elyos strip. Drag its header to move it, or lock its position.
5. Check the displayed Odyle amount against the game, then check that consuming or gaining energy updates it. For timers, start capture before a boss pull and allow at least two decoded hits over three seconds. Hover over the energy or forecast card for its source/status.

## New features

- **Odyle Energy:** available total with regular + bonus breakdown. The list snapshot is buffered on its own TCP stream until the matching character is identified. Updates are absolute balances, so a gain is never added twice. Explicit zero displays as 0; missing data displays as —. Character/capture changes clear the old balance. The tooltip shows when it was last received.
- **Estimated defeat:** current decoded target HP divided by measured damage per second to that same target, averaged since its first recorded hit (or a damage reset). Healing and damage to adds are excluded from a known boss's estimate. This is a rate projection, not a prediction of phases, shields, movement or future burst.
- **Party scope:** a fresh local-party roster matches players by name **and server**; explicitly identified self is included. If the roster is missing or older than 30 seconds, the card says **Tracked ... DPS · party unknown** and uses decoded player damage to the target. That can include non-party players. Even an observed roster can be incomplete if packets or identity records were missed.
- **Berserk in:** explicit NPC-duration catalogue entry minus elapsed time from an observed combat-start message. An ordinary in-combat notice uses its receipt time and is approximate, especially if capture joined mid-pull. A timestamped start uses the validated server timestamp. Neither is presented as an actual observed berserk event.
- **Time margin:** berserk time remaining minus estimated defeat time. A negative margin is amber. All forecasts use ≈; expiry says **Expected now**.
- **Obsidian controls:** functional Damage/Healing tabs, skill breakdown, copy result, dashboard, reset damage, position lock, expand/compact and close. Resetting damage preserves an observed pull clock; an actual boss disengage, full-health reset or defeat invalidates it. Stopping capture freezes the displayed observation time.

## Honest unknown states

No timer is assumed for an unlisted NPC or a pull whose start was missed. In particular there is **no default 300-second berserk timer**. There is no assumed energy cap, refill schedule or dungeon entry cost, and no invented number of remaining dungeon runs.

Defeat projection stays unknown until there are at least two matching damage records over three seconds and current HP is known. It is withheld if the HP sample is over eight seconds old or no target damage was observed for twelve seconds. Cached HP attached to later hits does not refresh its age. Missing energy remains — rather than a demo balance.

NPCs and skills can still display numeric IDs. A catalogue-listed NPC is treated as a boss so damage to adds does not move the boss card. Other targets can have a target-based HP/DPS projection without a berserk countdown. Only one game connection is selected at once; capture started mid-session can miss identity, roster, spawn or timing records. New game patches or region-specific layouts may require corrections.

Demo and Showcase are explicitly synthetic. The included preview images are **DEMO**, not screenshots of live readings. Live capture never starts demo data automatically.

## Diagnostics

The status/report retains the separate stages: no packets, no TCP payloads, packets without candidate frames, frames without combat events, and events without known self identity. Reports include the latest character-bound energy snapshot and forecast/basis, plus a bounded opcode census. They contain no packet payloads or endpoints, but character names and other combat metadata can appear. No raw packets are saved and there is no upload or update client.

If energy stays unknown, first try capture before character entry. If only berserk stays unknown, that can mean no catalogue match or no start message; it does not mean capture failed. A report is available for troubleshooting; no fixture upload is required before trying this build.

## Source and licensing

Selected protocol readers and numeric data are adapted from the MIT-licensed [SkeeveAN/Aion-DPS-Meter revision 53033af](https://github.com/SkeeveAN/Aion-DPS-Meter/tree/53033afadf3395dd47708abb308346734d8b1a8a). The ticket reader, selected roster/combat-start readers and explicit boss-duration entries extend the v0.6 port. Full attribution is in THIRD_PARTY_NOTICES.

[Abyss's public repository](https://github.com/karim-mo/aion2-abysslogs-dps-meter) was used as a feature reference. Its public distribution did not provide reusable parser source/licensing for these features, so no Abyss parser, binary or visual asset was copied. AscendAPP uses its own UI and forecast implementation. No GPL parser code was incorporated.

## Build and validation

Use a Windows machine with the .NET 10 SDK:

~~~powershell
dotnet build ./AscendAPP.slnx -c Release -m:1 -p:UseSharedCompilation=false
dotnet run --project ./tests/AscendAPP.SelfTest -c Release --no-build
~~~

Release compilation: **0 warnings, 0 errors**. **109 checks passed**, including early-login energy through Ethernet/TCP/framing/diagnostics, character changes, snapshot/update parsing, party server matching, per-target rate calculations, stale HP, boss resets and malformed payload stress cases (10,000 ticket cases plus the existing 3,000 decoder cases).

The actual WPF overlay was rendered and visually inspected in expanded, compact and unknown-data states with no binding warnings. No new live game capture or installer run was performed. The local restore used cached dependencies with NuGetAudit=false because the online audit service was unavailable; this is not a permanent source setting.

A developer-only render check is available:

~~~powershell
dotnet ./src/AscendAPP.Desktop/bin/Release/net10.0-windows/AscendAPP.dll --render-preview ./artifacts/preview.png
# Optional --compact or --unknown. These use synthetic data and never start capture.
~~~

The packaging folder contains optional self-contained/installer build scripts. The delivered Windows ZIP is the compiled framework-dependent app. Older release and diagnosis documents are historical; this README supersedes them for v0.7.
