# Compact HUD update

- Combat HUD rebuilt around the supplied A2tools screenshot: 380-pixel width,
  target toolbar, HP strip, 22-pixel ranked rows, damage bars and tiny status footer.
- Single player: 380 × 88. Two players: 380 × 112. Strip: 380 × 31 at 100% scale.
- Bounded party scroll area; diagnostic text moved to tooltips and capture window.
- Events: 300-pixel width, 24-pixel timer rows, section headings and small footer.
- Pets: 350-pixel width, original Windows-style portraits/count columns, compact
  search and pins, 235-pixel scroll area and status footer.
- Transparent rounded shells, toolbar glyphs, non-activating overlay windows.
- Scoped Hyprland float/resize commands for this app's own overlay clients.
- Geometry, clipping, one-/two-player, strip and compositor-isolation tests.

The expanded settings, history, skill details, capture controls, OCR, tray,
notifications and profile storage from the preceding Linux rebuild are retained.
The original Windows files describe that historical release. Current setup and
platform-specific limitations are documented in README-LINUX.md.
