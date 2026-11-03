# FooMeter

A compact Windows companion for Aion 2: combat meter, event timers and pet-soul tracking, with a customizable flame theme.

## Download

[Get the latest FooMeter installer](https://github.com/boronadziesieciu/FooMeter/releases/latest)

Download **FooMeter-Setup-…-win-x64.exe** from the release assets and run it. The installer includes the .NET runtime. Windows 10/11 x64 is required.

Live packet capture needs [Npcap](https://npcap.com/#download), installed separately. FooMeter requests administrator access at launch for capture. Third-party notices are included in the installed app folder. Game updates may affect decoding; unknown data is shown as unknown.

## Updates

Starting with 0.10.31, FooMeter checks for updates at startup and every two hours. Choose **Update now** in the popup to download, verify, install and restart. Update prompts wait until the current fight ends. Required updates pause capture after the fight and offer Update now or Exit FooMeter. Offline checks do not interrupt capture.

Settings → General → Check for updates also checks on demand. Settings, history and saved progress stay in the existing Windows user-data folder when updating. Versions older than 0.10.31 need this installer once to enable the updater.

Release notices carry a FooMeter signature, and installers are verified against the signed size and SHA-256 before launch. This is separate from Windows code signing: Windows may show an unknown-publisher warning for the installer/application.

This repository hosts downloads and release notes. No account tokens, character captures, local settings or private signing keys are published here.
