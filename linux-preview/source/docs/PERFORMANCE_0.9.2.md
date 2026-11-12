# Performance — AscendAPP 0.9.2

## What changed

- Player/roster rows retain identity and item containers between refreshes. Ranking changes move rows. Timer rows update existing properties. Unchanged class icons are no longer invalidated.
- Compact refreshes request totals and summary counts. Full skills and per-second timelines are built when opening analysis, exporting or archiving. They are retained, not sampled away.
- Boss forecast totals are aggregated by source and admission/self flags. Current queries inspect these groups; historical queries still use exact timestamps. Late identity/roster changes and the history-retention boundary remain covered.
- Party display snapshots, immutable custom dummy sets and repeated protocol labels are cached. Invalidation follows identity changes, roster replacement, despawn and reset. Label caches are bounded.
- Idle refreshes use max(configured interval, 1000 ms). The first scored combat event posts a coalesced UI wake after its state is committed. Active combat retains the configured update rate. Timers and enabled reminders keep updating.
- Optional **Smooth number transitions** in Settings → Display uses a subtle 120 ms opacity fade on changed player rates/totals, with a 30 fps scheduling hint. Exact current text appears immediately. No interpolated or invented DPS values. Hidden/unloaded controls and Windows reduced motion suppress transitions. Completed/disabled/hidden transitions remove their clock.
- Pending healing is no longer cleared on every active refresh. Rapid updates let the 500 ms HP reconciliation window finish, repairing a lifecycle bug without changing the conservative scoring policy.

Capture filters, adapter coverage, TCP, framing and combat admission keep their existing scope. No traffic downsampling was introduced.

## Measured results

Local synthetic managed-code/WPF-layout microbenchmark; .NET 10.0.13, 16 logical CPUs. Same harness and Release settings for 0.9.1 versus 0.9.2. Each scene is warmed up; medians of three passes are below. The long-fight fixture has 36,000 events over 15 minutes, four players and a manual timer.

| Scene | 0.9.1 ms/update | 0.9.2 ms/update | 0.9.1 temporary bytes/update | 0.9.2 temporary bytes/update |
|---|---:|---:|---:|---:|
| Idle | 0.0044 | 0.0036 | 2,161 | 1,353 |
| Long fight summary | 3.111 | 0.049 | 9,184,003 | 7,355 |
| Same fight + WPF layout | 11.415 | 0.155 | 9,642,871 | 11,983 |
| New hits + WPF layout | 11.216 | 1.658 | 9,854,328 | 92,349 |

The changing-data scene shows **85.2% less elapsed update time** and **99.1% less temporary allocation**. Both builds retain exactly **92,184,040** expected synthetic damage after the streaming passes. The improvement is not caused by silently dropping those events.

These percentages describe this update path, not whole-app CPU or RAM usage. Temporary allocation is not retained working-set RAM. The probe processes queued bindings/layout but has no visible game, live Npcap traffic or GPU composition. Its controls are unloaded, so active fades are outside these timings. In-game CPU/RAM/FPS impact varies with traffic, party size, update rate and hardware. Raw process-CPU counters have coarse resolution; elapsed time and allocated bytes are the primary comparison. The tiny idle microsecond timings are noisy; fewer idle wakeups are the practical idle improvement.

A separate native timing check runs a real WPF animation clock in an invisible window and verifies completion/detachment. This verifies cleanup, not GPU performance. Turn smooth transitions off in Display if preferred.

## Validation

Release build: zero warnings/errors. **296 core checks and 69 native WPF checks** pass. Added checks compare grouped forecasts with an independent linear calculation including historical timestamps, metadata changes and retention compaction; verify summary/full-detail agreement, cache invalidation, row/timer reuse, ranking identity, animation cleanup and pending healing across rapid refreshes.

Protocol fixtures remain synthetic. This optimization does not independently verify Global party/healing interpretation.

## Reproduce

The optional source ZIP includes tests/PerformanceProbe/PerformanceProbe.csproj and Program.cs. From the source root on Windows with .NET 10 SDK:

    dotnet build AscendAPP.slnx -c Release -m:1 -p:UseSharedCompilation=false
    $probeAppDir = (Resolve-Path src/AscendAPP.Desktop/bin/Release/net10.0-windows).Path
    dotnet build tests/PerformanceProbe/PerformanceProbe.csproj -c Release -m:1 -p:AppDir=$probeAppDir
    dotnet tests/PerformanceProbe/bin/Release/net10.0-windows/PerformanceProbe.dll performance.json

Use the same probe source with AppDir pointing at an extracted 0.9.1 Windows folder for comparison. Never combine Core/Desktop DLLs from different builds. The probe uses preview controllers, synthetic data and a hidden layout tree. It does not start capture or change personal settings. Raw results are in docs/performance/.
