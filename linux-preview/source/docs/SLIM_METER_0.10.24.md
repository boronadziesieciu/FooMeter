# AscendAPP 0.10.24 — Slim meter and feature review

## Result

The shared Force Map is removed from the app, solution and current source package. There is no server project, WebSocket client, room lifecycle, shared-marker renderer, own-position event collection, map pack, map hotkey, settings page or tray/menu action. Former map keys are removed when the shared profile is next saved; the rest of the profile survives. Older downloaded ZIPs are historical and are not deleted. A separately started old map-server console can be closed; this app does not use it.

Damage and healing bars are 26px high with 3px gaps, previously 40px with 4px gaps. Each row contains the original class emblem, Name, GS, CP, rate and optional total. Long names trim instead of moving the ratings. GS/CP retain their existing automatic source and full-value/source tooltips. The footer is one 30px line instead of a 43px two-line footer. The same five-player native demo renders at 420×328 instead of 420×416: 88px (21.2%) shorter. Scrolling still respects the saved player-row limit.

## Review and improvements

- Verified the nine original class PNG files against Git blob SHA-1 at SkeeveAN/Aion-DPS-Meter revision 03026182c4ae2059b41804189852af6e945e365a. The existing artwork matched; no replacement guesses were needed. Per-file SHA-256 provenance is included in src/AscendAPP.Desktop/Assets/Classes/PROVENANCE.json. Icons are now assembly resources, so moving the app or missing a loose icon file cannot cause empty badges. Alias, case and whitespace handling selects the correct canonical emblem. Artwork remains NCSOFT copyright.
- Fixed an actual target-profile loading failure: HttpClient with ResponseHeadersRead only covers response headers with its normal timeout. A linked 15-second request budget now covers body reads too. A stalled body is cancelled, disposed and releases the serialized request slot; a later Refresh can retry. No fake profile or cooldown is substituted.
- Removed map connection/sharing tasks and roughly 3.9 MB of embedded map assets/code. Core DLL size fell from 4,230,656 to 327,168 bytes. This is a binary-size measurement, not a measured CPU/memory benchmark.
- Corrected the release workflow's stale 0.10.2 installer artifact path/version and added data/solution triggers. The optional installer script was inspected; the delivered build is the framework-dependent Windows ZIP, not a newly tested installer.
- Reviewed and regression-checked capture filtering, automatic own/party scores, HP-based healing reconciliation, dungeon/teleport party continuity, timer persistence, input recovery, and selected-target focus/cooldown bonus handling. No claim is made that every game packet variant has been verified.

## Validation

Release build: 0 warnings, 0 errors. 542 core checks and 169 native WPF checks passed (711 total).

New checks cover a stalled response body and successful retry, migration of old map preferences without losing other settings, embedded art and aliases, absence of compiled map types/resources, and native geometry with long names/large ratings at 80% and 150% scale. Existing tests cover automatic own and party scores delivered through Ethernet/TCP/framing into real footer/bar bindings, HP healing, teleports, timers and native locked-overlay input.

Native previews rendered and were visually inspected for damage, healing and smallest scale. Idle, settings, target skills and events also render without binding warnings. Synthetic previews are labelled Demo; their figures are not live results. No real game capture, native packet capture, public profile request or in-game play session was started for this review. The public network research only verified artwork provenance.

## Install

Exit the old app, extract Compact-Windows into a new folder and run AscendAPP.exe. Existing shared settings load from %LOCALAPPDATA%\AscendAPP. Keep the Windows package files together. .NET 10 Windows Desktop Runtime x64 and Npcap are required. The existing requireAdministrator manifest remains; Windows asks for consent.

The source ZIP is for development and does not get merged into the Windows app folder. No shared-map server package is needed. Keep target skills and timer overlays enabled as before; they are independent of the removed map.

## Limits

Unknown GS/CP still display — until a supported own/party record is observed. Cooldown estimates still depend on public reference coverage and observed casts. HP-based effective healing can undercount ambiguous or missing health updates. Global live protocol variants and anti-cheat/network conditions require actual in-game verification. This release does not reintroduce the removed PvP radar, process-memory scanner or continuous location tracking.
