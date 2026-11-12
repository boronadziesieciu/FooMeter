# 0.8.2 — character refresh after teleport

Validated zone/teleport notices now start character re-detection, including same-zone teleports. New world connections can take over without waiting for the old socket to go quiet. Retired-world packets and duplicate adapters cannot steal the current identity. Old combat IDs, NPC/ownership metadata and party membership are cleared, and the previous encounter is archived. The next supported own-character record republishes name/server/class/current combat ID.

The compact overlay explains the waiting/recovered states. Reports include zone ID, refresh count and accepted character-record count. Strict self/party and boss/dummy filtering remains active throughout.

197 core checks and 36 native WPF checks pass. All new protocol fixtures are synthetic; actual Global teleport packet order is not independently verified. See TELEPORT_IDENTITY.md for the precise missing anonymized fixture if detection remains pending.
