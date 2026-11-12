# AscendAPP 0.10.14 — Black Obsidian Flow

The selected compact design now uses black panels, quiet mint accents and softer class-coloured damage bars across the meter, radar, timers and settings. English UI and overlay-first startup remain.

## Install

1. Close the previous AscendAPP.
2. Extract the **entire Compact-Windows ZIP** into a fresh folder, then open **AscendAPP.exe**. The Source ZIP is optional and stays separate.
3. Approve the normal Windows administrator prompt. The existing .NET 10 Windows Desktop Runtime and Npcap prerequisites still apply.
4. Your settings, map setup and saved timer snapshots carry over under the same Windows account. No profile migration or reset is needed.
5. Adjust transparency in **Settings → Display → Overlay opacity**. Radar size and opacity remain independent in **PvP radar** settings.

## What changed

- Black/charcoal foundations and consistent mint controls across all companion windows, history and skill details.
- Larger class emblems, more room for names, and a clearer GS/CP line. Your own class emblem also appears in the footer.
- The nine bundled game emblems retain their original colours and aspect ratio. Each file was verified byte-for-byte against the existing pinned artwork source. Aliases Elementalist/Spiritmaster and Fighter/Brawler share the appropriate image.
- Settings, timers, radar and More actions remain in the header. **Copy summary and Encounter history moved into More actions**. Capture, reset, skills, import and exports remain there too.
- Radar/timer header buttons immediately indicate whether the corresponding overlay is enabled.
- Unidentified classes render safely before login. Tiny or non-square class badges no longer create invalid fallback geometry or stretch the artwork.
- Clicking a party member who has not dealt eligible damage now explains why combat details are unavailable.
- The own-score link is borderless, matching the footer. The shared button template now respects explicit border settings.
- Existing smooth number transitions, scale, opacity, click-through, focus/fade behaviour and shared preferences are preserved.

## Verification

- Release build: **0 warnings, 0 errors**.
- **802 core checks** and **306 native WPF checks** pass.
- Layout checks cover nine classes, long names and larger GS/CP values at 80%, 100% and 150% scale, damage/healing views, hidden totals and scrolling parties.
- Checks cover an unidentified character at startup, icon aliases, independent opacity/scale, overlay toggle feedback, moved menu actions and waiting roster interactions.
- Native meter, idle, healing, small-scale, radar, timers and settings previews rendered without binding warnings and were visually checked. Meter/radar sample values are explicitly demo data.
- Existing packet, combat filtering, conservative healing, timer continuity, rating freshness and radar lifecycle regressions still pass.
- Tests use isolated preview/fixture entry points: no live capture, game-process reads or changes to your shared settings.

## Live-data limits

This is a UI update with verified interface fixes. It does not add new packet fields or establish that live Global decoding, continuous flying movement or camera yaw are complete.

**GS/CP display is clearer, but values appear only when a supported character profile has been observed.** Keep capture running and open/refresh the relevant in-game character profile. Unsupported own-login CP and unbound guildless inspection records can still display a dash. Scores are never guessed. The prior [build-review guide](BUILD_REVIEW_0.10.13.md) explains exactly which anonymized profile fixtures would resolve those gaps.

Radar stale-position and missing-session diagnostics remain visible. No unobserved movement, enemy position, terrain or DPS is manufactured.

## Artwork and licensing

No new third-party code or images were copied for this update. Existing game artwork is retained with its notice; the artwork is not relicensed as MIT. Source and original notices are in THIRD_PARTY_NOTICES/NCSOFT-Class-Artwork.txt.

Pinned reference: [class artwork files](https://github.com/SkeeveAN/Aion-DPS-Meter/tree/03026182c4ae2059b41804189852af6e945e365a/Client/assets/classes/icons), revision 03026182c4ae2059b41804189852af6e945e365a. Verification records are included with the source in artifacts/validation/class-artwork-0.10.14.json.
