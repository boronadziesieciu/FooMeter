# AscendAPP 0.10.15 — Profile and radar fixes

Reviewed 26 October 2026. This update fixes reproducible implementation problems and improves recovery and diagnostics. Automatic own GS/CP and continuous live movement are still not confirmed working from the supplied data.

## Install

1. Close the old AscendAPP instance. Extract the entire **Compact-Windows ZIP** into a fresh folder and launch **AscendAPP.exe**. Keep the optional Source ZIP separate.
2. Approve the standard Windows administrator prompt. The existing .NET 10 Windows Desktop Runtime and Npcap prerequisites still apply.
3. Existing settings are reused under the same Windows account. No profile migration or settings reset is needed. The black compact UI and original game class emblems remain.

## What the supplied report established

The 0.10.14 report contained 118 full player profiles with recognized GS and CP, but no observed own-character score record and no party roster. This establishes that the nearby-profile path processed scores; it does not establish that the game sent your own scores or party membership. Nearby players are not added to the party meter merely because their profiles exist.

The radar had no captured connection start, no login handshake, no validated client clocks and zero continuous movement reports. The session lookup had therefore not started. Occasional server skill/dash positions remained available. Auto-rotation was also disabled. A timer or animation cannot reconstruct those missing movement observations.

## GS and CP

Open **Settings → Character**, or click your own GS/CP footer. This page now shows profile counters, current own-score status and a table of recently observed player scores. **Refresh displayed scores** only redraws observed data; it does not request a game profile.

The supported own login record has no verified GS/CP fields. The supported nearby full profile can supply both; matched inspect records can supply GS. The upstream implementation also documents that the local player does not receive the usual nearby-player appearance record. Opening your own character window is therefore not a guaranteed source. Website item level is not substituted for in-game gear score.

If you want your own scores visible now, enter the actual numbers from the game under **Optional manual fallback** and click **Save manual scores** after the app has identified your character and home server. A **`*`** suffix and tooltip mark each manual value. These values:

- apply only to that exact character name and home server;
- persist across releases through the existing shared settings;
- fill missing fields only; a fresh observed value takes priority;
- do not create a packet observation, a party member or a combat event;
- must be updated after gear changes. Blank fields or **Clear manual scores** remove them.

Other players still require supported observed profiles. The observed-profile table makes it possible to distinguish missing data from a display/association failure. The exported report now includes the visible score rows and score sources as well as the counters.

## Radar recovery

1. In **Settings → PvP radar**, click **Prepare continuous capture**.
2. Keep AscendAPP open and capturing, fully exit Aion, then reopen Aion and log in. A teleport or return to character selection may retain the same connection and will not recover a missing login handshake.
3. Click **Restore Face up orientation** to enable auto-rotation from character facing and clear old fixed rotation/facing offsets. This is an explicit setting change; the update does not silently overwrite your saved orientation.
4. Check the connection detail. Continuous tracking needs a recognized handshake, a successful supported session lookup and **stream: validated**, followed by movement reports. If those conditions do not appear, use **Copy tracking diagnostics**. Retry lookup cannot reconstruct bytes captured before the app started.

Encrypted outgoing world traffic is now routed only to the dedicated client-stream reader instead of also being scanned as ordinary incoming frames. The bounded flow cache protects an already bound world session from unrelated additional flows. Both fixes have regression coverage. Missing-handshake messages now explain the required full restart.

Freshness and defeated-player rules remain: a stale origin is shown as last known, not falsely presented as live. Face up follows reported character facing; independent camera yaw and 100% walking/flying uptime are not verified. No capture, process-memory lookup or game interaction was performed during these development tests.

## Damage forecast correction

A training dummy regenerating to full health could clear forecast damage while the main meter continued counting. The forecast now retains the dummy encounter damage, resets when the meter starts a new encounter, and freezes its denominator when that encounter ends. Real boss reset detection remains enabled. This fixes the reproduced mismatch behind inconsistent dummy DPS totals; it does not validate missing boss HP or berserk data.

## Verification

- Release build: **0 warnings, 0 errors**.
- **813 core checks** and **317 native WPF UI checks** passed.
- New checks cover manual-score identity/server isolation and observed-value precedence, settings persistence, protected client flows, outgoing stream routing, dummy regeneration, encounter rollover, frozen ended rates, and the recovery controls.
- Settings and compact-meter previews were rendered. Profile and radar settings were visually reviewed. All development fixtures are synthetic; no live DPS or movement success is claimed.
- Windows executable and assembly retain the administrator manifest. Normal Windows UAC remains in place.

## Remaining evidence needed

First send the new diagnostics after capture was prepared and the game fully restarted: **Copy tracking diagnostics** plus **Export report** from Debug. These contain shape/counter/status information, not packet bodies or private keys. The report can contain character names and server IDs; replace names consistently before sharing if desired.

To add unsupported automatic score fields, the required fixtures are short anonymized **decoded server-to-client frame sequences** with exact frame boundaries, opcode, payload length, build/revision and relative timestamps:

- Own login/character **0x3336**, nearby full profile **0x4536** and inspect response **0x5036**, where actually present.
- Opening your own character window and a party member's inspect window, with the GS and CP shown in game at the matching times.
- Two observations of the same character before and after one known equipment change, with the resulting GS/CP. Include a guildless character if that path fails and state the zone/normalized-PvP context.
- For missing party association, include the decoded party-list/identity frames around joining and teleporting, with consistent anonymous member IDs and home servers. Do not assign an unverified opcode.

For radar cases that remain after a validated stream, the useful fixture is a short decoded movement/target sequence covering standing, walking, turning, takeoff, flight and landing. Include relative timestamps, zone, own/target anonymous IDs, reported positions/headings and the corresponding in-game movement, plus death/despawn if that is the failing case. A screenshot alone cannot establish the missing coordinates.

Preserve byte lengths, field widths and cross-frame identity links during anonymization; replace names/guilds/IDs consistently and remove unrelated account/chat data. If a safe decoded fixture export is not available, start with the diagnostic report rather than hand-editing arbitrary bytes. No raw process dump, private key, session state or unrestricted traffic capture is requested. The package does not claim to contain a general anonymizing packet extractor.

## References and licensing

The current MIT-licensed primary reference was checked at [SkeeveAN/Aion-DPS-Meter commit 5641d279](https://github.com/SkeeveAN/Aion-DPS-Meter/blob/5641d279a6d0585610ccb3917ee4d25fe663a969/Client/Aion2/Protocol/Aion2FrameDecoder.cs). It documents the local-player appearance limitation and separates own login, nearby profiles and inspect handling. This update introduces no guessed score offsets or newly copied third-party assets. Existing attribution and licenses remain in THIRD_PARTY_NOTICES.

Older files under docs are historical release notes; this guide supersedes earlier suggestions that opening your own inspect window is sufficient to obtain own GS/CP.
