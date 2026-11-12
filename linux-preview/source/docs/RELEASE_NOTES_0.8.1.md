# 0.8.1 — self/party and boss/dummy admission

The v0.8 defaults allowed nearby combat and all targets, while the boss-name file was empty. This patch removes those broad live options, upgrades saved defaults, and wires explicit English boss/dummy NPC metadata into the actual decoder and overlay path.

Self only or Self and party can be selected in Combat settings. Party membership requires a fresh roster and exact server/name identity; unknown actors are excluded. Damage to normal monsters, PvP and unclassified targets is excluded. Healing cannot start a town encounter. Demo/replay records remain explicitly labelled and complete.

NPC 2400032 is Training Scarecrow. There are 559 explicit boss names and 51 dummy IDs. Boss eligibility does not require a berserk duration. Dummies never get a boss countdown or defeat projection. Missing boss/dummy IDs remain excluded; they are not inferred from HP or runtime names.

Diagnostic reports now record player scope, target rule, rejected actors and rejected targets. Existing packet/framing/event/identity counters remain available.

No new real packet fixture was used. The tests cover synthetic wire decoding and admission, not a claim that every Global roster/boss record is calibrated. If self is missing, start before character entry. If a real party member or boss stays missing, export the diagnostic report from the overlay menu; the source includes synthetic regression fixtures and protocol provenance for follow-up.
