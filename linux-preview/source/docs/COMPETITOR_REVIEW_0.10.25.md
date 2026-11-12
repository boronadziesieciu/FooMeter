# AbyssDPS / NotMeter review for AscendAPP

Reviewed 2 November 2026. This is a public feature and architecture review, not binary decompilation. Neither competitor was installed or run. Public documentation describes intended behavior; it does not prove capture accuracy or current Global compatibility.

## What was actually available

[Abyss's official repository](https://github.com/karim-mo/aion2-abysslogs-dps-meter/tree/4aa713be8532ff16edc70ca0eb950d491aa409fe) contains a README and six screenshots, plus their directory: 8 tree entries, with no desktop implementation or licence file. Its [official site](https://abysslogs.com/) explains the service architecture: local capture, optional encounter uploads and web analysis/rankings. The public project is not reusable decoder source.

[NotMeter's desktop release repository](https://github.com/Not4You-Dev/NotMeter-Releases/tree/2c80079e7bb9df3d6821d35f891efa5d2c58647a) contains a README, with release binaries hosted separately. [NotMeter-Web](https://github.com/Not4You-Dev/NotMeter-Web/tree/65adc215095b3c86f3706fe9164a72315049d004) contains the public site, not a desktop decoder. The inspected API metadata identifies no licence for these repositories. Public visibility is not permission to copy implementation.

The [official NotMeter tutorial](https://notmeter.com/tutorial.html?lang=en&screen=en) was read through its published English data. The live shell identifies guide version 1.0.308; its English base text includes an older edition label, and a separate current-guide adapter is served. We therefore treat individual documented features as references, without claiming to have exercised their latest desktop behavior. Tutorial JavaScript was read as text/data and was not executed. Repository revisions and research-file hashes are in RESEARCH_SOURCES_0.10.25.json.

No competitor source, scripts, screenshots, art, private endpoints, ranking caches or binaries are included in AscendAPP. No authentication, updater, protection or paid feature was bypassed. The new code is written for AscendAPP.

## Useful patterns

| Area | Public evidence | AscendAPP decision |
|---|---|---|
| Compact combat display | Abyss documents party bars, GS/CP, boss health and forecasts. | Keep the existing slim bars and automatic observed scores. |
| Encounter review | Abyss documents searchable local history and detailed reports. NotMeter's guide documents saved attempts and combat details. | Add target/class/player search and same-target comparisons now. |
| Display overhead | NotMeter's guide exposes rendering/performance choices. | Remove the abandoned tracker services and keep idle backoff and stable row objects. |
| Event presentation | Both document event or boss alerts; NotMeter exposes appearance controls. | Add event-only size/transparency while preserving old appearance by default. |
| Advanced analytics | Abyss documents attack breakdowns and timelines. NotMeter exposes web statistics and build analysis. | Candidates requiring better recorded data; no invented values or imported formulas. |
| Online sharing | Abyss offers upload visibility choices; NotMeter has sharing/ranking services. | Leave out: this focused app has no hosted service, account workflow or shared reference dataset. |

Sources: [Abyss README](https://github.com/karim-mo/aion2-abysslogs-dps-meter), [NotMeter guide](https://notmeter.com/tutorial.html?lang=en&screen=en), [NotMeter website](https://notmeter.com/). This table distinguishes documented capability from verified behavior.

## Implemented in 0.10.25

1. Removed the entire selected-target skill tracker: overlay, hotkey, settings, profile HTTP client, Shugo cooldown parser, bonus audit, cast/selection readers and manual buff/skill timers. Automatic GS/CP still use the game's own/party records. Damage-by-skill analysis remains part of the DPS meter.
2. Rebuilt the history dialog in the black Obsidian palette. Search by target, class or visible player name. A hidden other-player name cannot be discovered through search. Empty searches disable breakdown actions.
3. Compare completed encounters against the same target. Known NPC IDs must agree; name-only matching is allowed only when neither record has an ID. Party and personal eDPS use full encounter duration, regardless of active-DPS display settings. A zero baseline yields unknown change rather than infinity. Personal comparisons require the same captured own name/class.
4. Added independent event overlay scale and opacity, plus Follow meter appearance. Old profiles initially retain their previous global appearance. These controls do not resize the damage meter.
5. Removed stale skill-tracker settings automatically while retaining meter/event settings, reminders, scores, tickets and boss snapshots. The app now has two overlays: meter and events.

Comparisons show recorded differences, not proof of improvement. Party, gear, boss phase and difficulty can differ; a target-name match is weaker than a known NPC ID. Home-server identity is not stored in these snapshots, so the personal match cannot prove cross-server identity. History remains the existing bounded session history, not a new on-disk archive.

## Recommended next improvements

1. **Bounded local encounter archive.** Completed fights should survive a restart. Store versioned summaries plus optional details, cap count/size, and expose Delete/Export. This is the highest-value addition after searchable history; it needs migration, corruption recovery and storage tests.
2. **One-line combat strip.** An optional compact presentation could show personal rate, party rate, target HP and fight clock, expanding on interaction. It can use existing observed fields without changing the decoder or creating another overlay.
3. **Clearer capture health.** A small indicator could distinguish driver/capture stopped, traffic received, recognized frames, decoded events and confirmed party scope, with a single diagnostics action. Most underlying counters already exist.
4. **Richer recorded-hit analysis.** Add verified hit-type proportions and incoming-damage/death recaps only when supported by explicit decoded fields. Validate with anonymized real packet fixtures; do not infer back attacks, buff uptime or deaths from missing damage.
5. **Per-event controls.** Enable/mute specific reminders, put the soonest event first and show observation age more clearly. New recurring schedules need current authoritative timing evidence, particularly after maintenance and regional changes.

Leaderboard percentiles, normalized DPS, stat-efficiency formulas and buff attribution require datasets or validated game rules that this app does not have. They should not be represented as working features based only on competitor labels.
