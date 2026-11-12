# AscendAPP 0.4 Alpha: implementation notes

## UI

- Expanded Elyos overlay displays a boss identity when supplied by normalized events, the live party DPS, top hit, player ranking and an HP bar only when health values are available.
- Compact overlay remains a slim three-row HUD. Both layouts share the midnight-blue glass and gold trim.
- Skill breakdown window shows player totals and per-skill hit, crit, max-hit and damage data.
- A fight-history window shows archived encounters; idle encounters archive automatically after 12 seconds without new combat data.
- Main window can export JSONL normalized events, JSON combat reports and CSV rankings.

## In-memory engine and data

- `CombatEvent` supports optional class name, local-player marker, skill name, NPC code, boss flag and boss HP fields.
- Boss status is **explicitly** flagged, never guessed from damage size or creature names.
- The validated fixture has 324 synthetic events, nine characters, 40,480,000 damage, 49,520,000 remaining boss HP out of 90,000,000, and a 1,575,191 top hit.
- The `Showcase` button loads the fixture locally through the same normalized JSONL parser as user imports.
- `data/boss_names.json` is empty by default and can be populated with independently verified mappings. No game datasets are bundled.

## Testing

- XAML and project XML parse checks completed.
- All declared WPF click/selection/mouse event handlers found in code-behind.
- Standalone HTML preview smoke-tested in Chromium via Playwright: expanded, compact and skill tabs render without JavaScript errors.
- JSONL fixture checked for sorted timestamps and internally consistent HP accounting.
- **Not compiled or run under .NET/WPF on Windows here.** The C# self-test suite has been extended but is unexecuted.

## Blocker for reliable live use

`UnverifiedGlobalDecoder` intentionally emits no events. Implementing verified Aion 2 Global opcodes, payload offsets, actor resolution, server-to-client filtering and missing-data handling requires captures for the exact game build (or a separately integrated, audited, compatible implementation). Until then, Npcap start only tests network capture, not live combat analysis.
