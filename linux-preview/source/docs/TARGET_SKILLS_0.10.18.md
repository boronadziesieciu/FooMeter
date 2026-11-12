# AscendAPP 0.10.18 — Selected target skills (experimental)

The PvP radar has been removed from source and runtime: no position renderer, movement decoder, map setup, session-key lookup or game-memory scan remains. The compact DPS meter, automatic own/party GS and CP, timers, class emblems and shared settings remain. Old radar preference fields are retired during normalization; unrelated settings and saved user files are retained.

## Using the new overlay

1. Close the old app. Extract the complete Compact-Windows ZIP into a new folder. Keep the optional Source ZIP separate.
2. Start AscendAPP.exe; accept the standard Windows administrator prompt. Existing .NET 10 Windows Desktop Runtime and Npcap prerequisites still apply.
3. Capture before entering the game. Press Ctrl+Shift+F6, click the crossed swords in the meter, or use Settings → Target skills.
4. Select another player in-game after opening the skill bar. An explicit own target-change report plus a verified player name and home server is required. NPCs, nearby names and DPS targets cannot choose a player automatically.
5. The overlay retrieves that player's public active/equipped skills, learned ranks and original skill icons. This release supports European Global character lookup. It is a build view, not the player's exact hotbar arrangement.
6. Click the circular refresh arrow to reload the selected build and stats. It bypasses AscendAPP's five-minute profile cache and sends no-cache on the search/info/equipment requests. Refreshes have a 15-second minimum. The upstream service may still cache data; the displayed time means retrieved by AscendAPP, not a verified in-game update time.
7. Hover a skill to see rank, known bonus sources, cooldown reference and assumptions. A countdown starts only after an observed skill-use report from that selected verified player.

**?** means cast/availability unknown. **seen** means a cast was observed without readable cooldown metadata. **≈ seconds** and **≈ ready** are estimates, never server-confirmed enemy readiness. No observed cast means no timer. Windups, hits, DoTs, healing ticks and own-player cooldown lists cannot start another player's cooldown.

Use the six-dot grip/title bar to move it. Its padlock is independent; hold Alt to click or drag while locked. Ctrl+Shift+F10 / tray Show overlay and Ctrl+Shift+F12 recover input on all overlays. Toggle visibility independently of the meter and event timers.

## What the public data can and cannot establish

NCSOFT's public search, info and equipment endpoints provide name/home server, class, skill ranks, acquired/equipped states, icons and some explicit cooldown bonuses. Public refresh was verified through the new C# client using the user's example; the response had 16 usable skills and a 7.7% known reduction subtotal (4.7% Illusion and 3% equipped title). No real profile response, character identifier, screenshot or packet capture is bundled.

Cooldown references are independently parsed from public Shugo.GG skill pages, with title/name validation and bounded responses. The parser supports an exact rank table when published in HTML. **The real pages tested returned base cooldowns without rank tables. A separately checked public Templar Defiance table is included (rank 7 = 54 seconds, reduction-immune), with class/ID/base guards. Other affected skills use explicitly marked base-only estimates; learned ranks are displayed but rank-specific cooldown adjustments are not verified for those references.** Some attacks have no readable cooldown and remain unknown. Public references may also differ from the current Global patch.

Known explicit reductions are added as an estimate and applied to the available reference duration. This stacking formula has not been independently calibrated. Reduction-immune skills ignore the subtotal. Combat speed is not treated as cooldown reduction. Full gear/Daevanion effects, Specialty Shard choices, conditional buffs, cross-skill reductions, resets and charge behavior are incomplete. Exact cooldown prediction for every build is **not** delivered by this release.

Shugo's refresh button addresses stale profile data; it does not supply live skill use. [Shugo FAQ](https://shugo.gg/faq) describes official API sourcing, no public Shugo API, and unavailable skill-specialization choices. This implementation uses public pages and official endpoints, with no Shugo private endpoints, account login or copied website code.

## Resource and identity boundaries

The optional target module is off by default. Hidden/disabled mode cancels pending lookups, stops its monitor/render timers and unsubscribes selection/cast decoding. One public request at a time is bounded by timeout and response size. Memory caches are capped at 32 profiles, 512 cooldown rules and 128 decoded icons. Icons use only the fixed official HTTPS resource host. There is no network request per paint.

Changing/clearing target, recycled actor IDs, despawns, capture gaps and world transitions revoke inappropriate state. Generation, identity and cancellation checks reject late replies for a previous selection. Scene changes clear selection while preserving own identity where the established dungeon session remains valid. Shared GS/CP logic and encounter scope remain independent of this overlay.

## Evidence and remaining in-game check

Release build: zero warnings/errors. 481 core synthetic checks and 145 native WPF checks pass. Evidence is included in validation/. Counts are recorded after retired radar suites were removed, so they differ from the previous release. Checks cover strict selection, wrong identity/server rejection, profile cache/refresh headers, bounded responses, cancellation, rank parsing, immunity, unsupported bonuses, duplicate casts, unknown/expired states, input protection, UI bindings, existing automatic scores and timer/dungeon continuity. The preview is synthetic and explicitly marked DEMO.

The real public HTTP lookup and skill-reference/icon retrieval were tested separately. **Selected-target and cast decoding has not been verified against a new real Global capture.** Test with another visible player: change selection, observe that player's actual skill, then switch target/teleport and confirm old tiles clear. If no target/skill appears, export diagnostics from the meter menu. Settings → Debug shows selection-report and observed-player-cast counts plus the target status. Please start with those counters; full account/session data is unnecessary.

If wire calibration is needed, the minimal anonymized fixture set is complete reassembled server frame bodies for: (a) own 0x3138 target select and target clear, or own actor's 0x3238; (b) selected player's verified identity/name/home-server record; (c) that player's 0x0238 skill use, with timestamp and known skill name/rank; (d) an observed target change or scene transition. Use consistent substituted actor IDs/names/server labels, preserve framing/varint lengths and numeric skill fields, and omit client login/key material, credentials and unrelated traffic. Ground-truth skill cooldown plus rank/known bonuses is additionally required to calibrate predictions.

## Sources and provenance

- [NCSOFT character service](https://aion2.plaync.com/) and public search at api-search.plaync.com (read-only JSON, no authentication/cookies).
- [Shugo FAQ](https://shugo.gg/faq) and public per-skill reference pages, linked in each skill tooltip. Numeric game facts only; no Shugo implementation, logos or UI assets copied.
- [MIT protocol reference](https://github.com/SkeeveAN/Aion-DPS-Meter/tree/729cb910494a1a0a7b837b8914c98921ce512fd4): docs/protocol/skill-frames.md, combat-frames.md, cooldown-frames.md, Backend/src/portraitService.ts and ncCharacter.ts. Selected target/cast header facts only. Full MIT notice remains in THIRD_PARTY_NOTICES/SkeeveAN-MIT.txt.
- Repositories without a clear compatible license were investigated but no code or assets from them were copied.
- Original game class art remains attributed; runtime skill art comes from NCSOFT's official resource URLs. See THIRD_PARTY_NOTICES/NCSOFT-Class-Artwork.txt.
