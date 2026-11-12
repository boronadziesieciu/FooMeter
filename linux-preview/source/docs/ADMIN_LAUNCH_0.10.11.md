# AscendAPP 0.10.11 — Administrator launch

Double-click **AscendAPP.exe** normally. Its embedded Windows manifest requires administrator access, so Windows requests UAC approval or administrator credentials before opening the overlay. The requirement travels with the executable, including extraction to another folder and launches through shortcuts. No right-click or compatibility setting is necessary. Cancelling leaves the app closed; this does not bypass UAC.

Close the previous release, extract the whole Compact-Windows ZIP into a fresh folder and keep Source separate. Use the same Windows account to retain shared settings/maps. When credentials for a different administrator account are required, that account has a separate user-data profile. Existing .NET 10 Windows Desktop Runtime and Npcap prerequisites still apply.

The source sets ApplicationManifest to src/AscendAPP.Desktop/app.manifest, with requestedExecutionLevel=requireAdministrator and uiAccess=false. Both ordinary builds and published application hosts embed this manifest. The Inno Setup installer keeps its existing per-user installation scope; the installed application requests elevation on launch.

This change does not modify capture, session lookup, radar or combat decoding. Administrator access can resolve a permissions failure, but it does not prove protocol compatibility, camera heading or 100% live tracking. See FLIGHT_CONTINUITY_0.10.10.md for those limits.

Validation: Release build completed with zero warnings/errors. The compiled EXE and managed assembly were read as PE resources: both RT_MANIFEST #1 resources contain requireAdministrator with uiAccess=false. All 232 native WPF preview/self-checks passed through the DLL test host without elevation or reading a game process. The elevated interactive launch was not invoked during development. The 737 core checks are unchanged from 0.10.10 and were not repeated for this launch-only change.
