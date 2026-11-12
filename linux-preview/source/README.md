# FooMeter 0.10.29
Compact combat analytics, event/boss timers and automatic pet-soul interpretation for Aion 2 Global on Windows.

- Settings → Appearance: choose an accent, panel background and flame color; changes apply live and persist across releases.
- Compact events: 300px wide, single-line rows; scale and opacity remain independent.
- Pet tracker: 207 bundled pet portraits, search/pins and clear soul counts.
- Loot-row counters: plain progress numbers beside visible Soul: rows, following their movement. Use counters only to hide the entire collection window. Choose the loot text area in Pet tracking settings.
- DPS, automatic GS/CP, saved fight history, combat strip, exports and capture-status diagnostics remain available.

Read **docs/LOOT_COUNTER_0.10.29.md** for setup and recognition limits. Progress comes from received game data. Unknown totals remain unknown.

Extract the **Windows** package into a new folder and run FooMeter.exe. The **Source** package is for developers. Close the older copy first. Existing shared settings and history carry over. Requires .NET 10 Windows Desktop Runtime x64 and Npcap. Windows asks for administrator access.

Build the existing AscendAPP.slnx solution in Release. Run the core checks with tests/AscendAPP.SelfTest/bin/Release/net10.0/AscendAPP.SelfTest.dll and the native UI checks with FooMeter.dll --self-test-ui artifacts/ui.json. Loot-row mode uses the bundled, offline Tesseract OCR engine and English model.

THIRD_PARTY_NOTICES contains protocol/dependency notices and separate NCSOFT class/pet artwork attribution. The retired radar, map and target cooldown tracker remain removed.
