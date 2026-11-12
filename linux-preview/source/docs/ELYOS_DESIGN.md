# Elyos visual system for AscendAPP

## Design principles

The expanded view borrows the **Elyos fantasy-faction mood**, not assets from an existing DPS meter: enamel-blue panels, ivory typography, polished gold borders, a custom vector wing sigil, and translucent ruby/azure class-style ranking bars. The in-game compact mode keeps the same visual vocabulary in a much smaller footprint.

## Implemented in WPF

- **Expanded overlay (558 × 632):** custom vector wing medallion, combat header, encounter name and state, party DPS, measured top hit, nine-palette ranking bar fills, DPS/damage/share, and footer.
- **Compact overlay (437 × 110):** gold-framed blue-black glass, two lines of combat information, subtle purple-blue result bar, toolbar, player footer.
- **Interactive controls:** drag, lock/unlock, toggle full/compact, copy player's actual data, launch dashboard, close.
- **Dashboard:** new Elyos palette, wing icon, gold typography and accents while keeping all current Npcap/demo/import/report controls.
- **Reduced UI churn:** `UiModel` now updates observable player row objects in place rather than clearing/recreating the collection on every 250 ms snapshot.
- **Actual top hit:** `CombatEngine` computes `EncounterSnapshot.TopHit` and `TopHitBy`, with headless regression checks.

## Accuracy & art boundaries

- The medallion is an original WPF vector, **not a boss portrait**. The boss name is the last damage target reported by combat events.
- Ranking colors are a **decorative nine-color palette**, not verified class-color mappings. The decoder currently does not send class IDs.
- **Boss HP is explicitly unavailable** in the expanded overlay. Never present a fake HP value or a simulated berserk timer as live data.
- Current `UnverifiedGlobalDecoder` is a protocol placeholder. Npcap capture works at a packet level; live damage decoding still needs verified game traffic schemas.
- Demo values are synthetic. `TopHit` is computed from normalized events, so it is accurate for that dataset but not proof of accurate Aion 2 packet decoding.
- No externally sourced art or fonts are bundled. Native Windows fonts, brushes, and vector geometry are used.

## Editing

- Overlay structure/colors: `src/AscendAPP.Desktop/OverlayWindow.xaml`.
- Overlay interaction: `src/AscendAPP.Desktop/OverlayWindow.xaml.cs`.
- Shared dashboard palette and vector mark: `src/AscendAPP.Desktop/App.xaml`.
- Player bars, color selection and model state: `src/AscendAPP.Desktop/ViewModels/UiModel.cs`.
- New top-hit metric: `src/AscendAPP.Core/Combat/CombatEngine.cs`.

## Validation

Static XML parsing, handler/resource/binding checks completed in the authoring environment. The WPF executable and screenshots still require a Windows .NET 10 SDK run. Use `./build.ps1` in PowerShell on Windows to restore, build and run the headless tests.
