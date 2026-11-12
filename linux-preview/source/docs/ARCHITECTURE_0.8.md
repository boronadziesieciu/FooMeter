# AscendAPP 0.8 architecture

App creates MeterController, sets OverlayWindow as Application.MainWindow and opens it. Optional automatic capture starts after the first render. There is no dashboard/expanded meter class.

MeterController owns capture, decoder, encounter/forecast accumulators, strict capture membership filtering, local ticket cache, manual timers, reminders, UI refresh and saved preferences. SettingsWindow is an icon-led control surface; player/history windows provide local analysis.

Core contains platform-independent preference validation/storage, reminder scheduling, hotkey syntax, capture filters and combat calculations. DesktopIntegration handles the Windows tray, hotkey registration, click-through styles and foreground/process behaviour. Preview/UI-test modes never attach these native behaviours or start capture/audio.

New protocol fields are experimental and keep missing values explicit. Read README.md and docs/FEATURES_0.8.md for capability limits. Historical architecture documents predate this revision.
