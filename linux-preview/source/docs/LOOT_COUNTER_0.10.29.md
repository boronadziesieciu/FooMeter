# FooMeter 0.10.29 · Counters beside game loot
3 November 2026

The reference animation shows progress numbers immediately to the left of each in-game soul-loot row. This release adds that presentation as **Beside loot rows**, instead of relying on a separate floating card.

## Use counters without the pet window
1. Close the old copy. Extract the new Windows ZIP into a fresh folder and run FooMeter.exe.
2. In the pet panel click **Use counters only · hide this window**, or Settings → Pet tracking → **Use counters only**.
3. Return to the game. The counter reads the selected loot-text area while the game is in front and places plain progress numbers beside recognized Soul: rows.
4. If your loot list is elsewhere, choose **Settings → Pet tracking → Choose loot text area**. Draw a box around the loot TEXT, keeping item icons just outside the left edge. Escape cancels.
5. **Preview row counters** shows labelled DEMO numbers beside that area for eight seconds. This does not change collection progress.
6. Use **Show pet window** or the meter's paw to reopen the full collection. Hiding it does not disable the counters or capture.

The default region targets the loot position in your supplied 1700×910 screenshot. Its coordinates are saved as proportions of the game client, so moving or resizing the game moves the region with it. Adjust **Space for item icons** if the numbers need a different horizontal gap.

Player bars also group the name, GS and CP tightly on the left; DPS and totals remain aligned on the right. Long names retain ellipsis and full tooltips.

The original **Floating card** remains an optional style. It is separate from the new loot-row mode.

## How it works
- Collection snapshots and decoded soul-gain messages remain the sources of progress values.
- Local English OCR locates item names and their vertical position. Reading an item name never increments a count or assumes a drop was consumed.
- Multiple supported gain events retain separate bounded snapshots; when several rows for one pet are visible, their observed progress values can be shown separately, such as 14/25 and 15/25.
- When no corresponding gain message has arrived, the counter can still show that pet's latest received collection progress. This explains why the new display can appear even when the old gain-triggered card did not.
- Saved/unconfirmed values use a different color. Missing totals stay unknown. These are latest received game values, not a new server query.
- Numbers move with recognized row positions and disappear when those rows are no longer recognized. No duplicate pet icon, name or large frame is drawn over the game's existing row.

## Privacy and performance
Only the configured area of the foreground Aion 2 game client is read. Focus and client bounds are checked around capture/recognition. Processing is local; no image or recognized text is saved or uploaded in normal operation.
The reader is paused when the game is not in front, when disabled, or when Floating card is selected. One background worker reads a bounded crop, uses one OCR engine and avoids overlapping jobs. The model/engine is released after idle time. The visible overlay is a narrow transparent click-through strip.
The framework-dependent Windows app now includes Tesseract 5.2.0, its native libraries, and the English tessdata_fast model; it does not require an online OCR account.

## What was verified, and limits
The local OCR pipeline recognized **Guardite** from the loot area in your supplied screenshot, including its partially scrolling name. Core and WPF checks cover separate pickup snapshots, numbers for repeated pet rows, click-through layout, counter-only preferences and the fact that OCR cannot alter collection totals.
The preview image is a clearly labelled synthetic placement example. No live game session was controlled or captured during development.

Text recognition can miss or misread faded, overlapping, low-contrast or rapidly scrolling names. Coins, ambiguous short fragments and unknown pet names do not produce guessed numeric progress. Names still need to be readable in English. If the counter is absent, keep the loot area uncovered and recalibrate its text region; the pet diagnostics include reader status and matching counters. This is not a claim of perfect matching on every frame or every UI scale.
The existing decoder still requires actual packet fixtures to verify unsupported live formats. A SYNCHRONIZED collection in the UI confirms a baseline was decoded; it does not certify every later gain message.

If the reader reports a native OCR load failure, install the current Microsoft Visual C++ x64 Redistributable from Microsoft's official support page: https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist . The usual .NET 10 Windows Desktop Runtime x64 and Npcap requirements remain.

## Sources and licenses
Tesseract .NET wrapper: https://www.nuget.org/packages/Tesseract/5.2.0 · Charles Weld · Apache-2.0.
Native Tesseract: https://github.com/tesseract-ocr/tesseract · Apache-2.0.
Leptonica: https://github.com/DanBloomberg/leptonica · BSD-style license.
English model: https://github.com/tesseract-ocr/tessdata_fast · Apache-2.0; Git blob bbef4675053b5b468cdb477053e28b1c698ba08e, SHA256 7d4322bd2a7749724879683fc3912cb542f19906c83bcc1a52132556427170b2.
The small pet-word glossary derives from the existing English name facts. Existing NCSOFT portrait/class-art notices remain applicable. No Kisk source or visual asset is redistributed; its supplied animation was used to inspect the requested placement.
