# Validation — 0.9.1

Release build: zero warnings/errors. 274 deterministic core checks; 53 native WPF checks. New cases cover persistent/replaced rosters, UTF-8 names, nickname/server/class ambiguity, despawn/reappearance, capture gaps, full/partial/unknown healing, overlapping healers, damage, HP ordering, HoT announcements, late event lifecycle, JSONL and UI totals. Native previews were rendered for party/healing and the preserved event overlay and inspected. The staged Windows executable also passed all 53 native UI checks with exit code 0; its product/file versions are 0.9.1 / 0.9.1.0.

All new wire fixtures are synthetic. No new live Global party/healing capture was supplied. Healing is an HP-based estimate and may undercount or be affected by unobserved effects; no verified effective-heal field is claimed.
