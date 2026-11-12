# AscendAPP 0.6.0 Experimental — Elyos Edition

This build replaces the production decoder stub with a selective MIT protocol port so it can be tried in game immediately. No packet fixtures are required before trying it. Live Global compatibility has NOT been verified in an actual game session.

## Try it

1. Extract the entire Windows ZIP into a new folder. Keep all DLLs, JSON files, samples and notices together. Do not overwrite a running older copy.
2. Run **AscendAPP.exe**. This ZIP uses the installed **.NET 10 Desktop Runtime**. This computer already has that runtime with its SDK. Npcap must remain installed separately.
3. Keep **Auto-detect game connection on all adapters** checked and leave the TCP port blank. Click **Start capture**.
4. Enter the world from character selection, then open the overlay and attack a training target. If you started while already in the world, return to character selection and re-enter so the login identity record can be seen. Clocks can identify a mid-session stream, but cannot tell the meter which player is you.
5. The top badge should say **LIVE • EXPERIMENTAL**. Hover over the bottom status line for the full capture state. The compact overlay remains **422 × 92** and uses only explicitly identified self-player data.

Demo and Showcase remain synthetic and must be started explicitly; their values are not game data. Capture stops the demo before live processing begins. No raw packets are written to disk and there is no upload/update/network client in this app.

## Implemented for this experiment

- All-adapter Npcap capture, including loopback, with manual adapter/port fallback.
- Exactly one detected server-direction stream supplies combat; capture surfaces are isolated to prevent duplicate physical/loopback counts. Recognition uses a structurally valid character record or three plausible, increasing server-clock messages in ten seconds. This is protocol recognition, not process-based verification.
- Directional TCP overlap/retransmission handling; bounded gaps; SYN/FIN/RST lifecycle; known-opcode framing resynchronization; correct big-endian opcode interpretation; TLS-record skipping; bounded LZ4 bundles.
- Character login ID/name/class, nearby-player appearance/nickname identity, direct hits, DOT, direct healing families and heal-over-time fields.
- Explicit summon-owner ID mapping when both owner and spawn are known. Guessed owners and estimated hits are excluded.
- NPC spawn IDs and current/maximum target health. A decoded NPC is not automatically called a boss.
- Combat aggregation, ranking, skill-ID breakdown, history, replay/export and the English Elyos compact overlay.
- JSON report with stage counters, rejected-message count, self identity and a bounded opcode census. Reports have no packet payloads or endpoints, but combat/player metadata can contain character names.

## Limits you may see in game

This is a working implementation to test, not a promise of verified live DPS. Supported layouts were adapted from a pinned public repository, and tests use synthetic bytes. Patches, other regions and unexpected layouts may yield no events or incomplete/incorrect totals. Compare with a controlled skill use and the game's observed damage before relying on totals.

Skills display as **Skill #ID** and NPC targets as **NPC #ID**. An English name catalogue was not imported. The meter has no verified party-membership filter: **TRACKED DPS** includes decoded nearby-player events, not a guaranteed party total. Unnamed actors remain **#ID**, never guessed as self. Party roster inference, guessed pet owners, damage estimates, shield-share corrections, full PvP interpretation and buff/rDPS analysis are not included. Numeric class identification uses the upstream class-code/prefix tables.

Direct-hit records can represent multiple sub-hits. Their wire amount is added once, and their explicit hit count updates the hit/crit totals. TOP HIT is the largest decoded damage record, which may aggregate sub-hits.

Only one game stream is active at once; concurrent clients are unsupported. A second capture surface is eligible after ten seconds without traffic on the selected one. Old-connection closure archives the current encounter. A capture started mid-connection can miss initial identity and spawn packets. IP fragments and IPv6 extension headers remain unsupported.

If capture cannot open adapters, the status shows the Npcap error. If Npcap was installed with restricted access, launch with the access that installation requires. An encrypted VPN's physical interface may be unusable; automatic loopback capture can help only if an unencapsulated game flow is exposed there.

If it stays empty, the bottom state distinguishes no packets, no TCP payloads, no candidate frames, no decoded events and missing player identity. The JSON report is available for later troubleshooting, but is not required to try this build.

## Source, licensing and validation

Protocol port: SkeeveAN/Aion-DPS-Meter, commit **53033afadf3395dd47708abb308346734d8b1a8a**, MIT.
See **THIRD_PARTY_NOTICES/SkeeveAN-MIT.txt** and **THIRD_PARTY_NOTICES/README.md**.
No GPL parser implementation or upstream binary was incorporated. Unrelated upstream assets and estimation code are excluded.

Source build with .NET 10 SDK:

~~~powershell
dotnet build ./AscendAPP.slnx -c Release -m:1 -p:UseSharedCompilation=false
dotnet run --project ./tests/AscendAPP.SelfTest -c Release --no-build
~~~

For this delivered build: Release compilation succeeded with **zero warnings and zero errors**; **68 checks passed**, including 3,000 malformed-payload cases in one stress check. Cached dependency versions were used; the online NuGet vulnerability service was unavailable, so the local restore used NuGetAudit=false. That was a build invocation option, not a change to source audit settings.

No actual game capture, in-game session, native capture callback run, GUI interaction or installer was tested here. The Windows ZIP is the compiled framework-dependent app, not an installer. Historical v0.5 documentation describes the previous stub; this README supersedes it for v0.6.
