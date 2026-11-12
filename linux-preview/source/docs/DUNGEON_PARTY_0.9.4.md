# Dungeon party continuity — AscendAPP 0.9.4

## Cause and change

The old decoder treated every new zone counter as a complete world reset. It erased self, all entity mappings and the party roster; the desktop then reset the encounter. A dungeon door or stage teleport could therefore leave the app waiting indefinitely for records the game need not repeat.

The new behavior separates instance movement, world-local entity IDs and confirmed group membership:

| Situation | Behavior |
|---|---|
| Internal move/revival in the same classified instance on the same world connection | Keep current player/party bindings, target mappings and encounter totals. Continue observing despawns, appearances, own records and replacement rosters. |
| Entering or moving between classified instances | Keep confirmed roster names/server/class/level. Clear world IDs and target data; accept only freshly linked actors. |
| New world TCP socket | Carry only a verified profile, observed ticket provenance and confirmed roster when transition metadata supports continuity. Refresh IDs/HP/targets; never copy old combat IDs. |
| Old socket closes first | Keep waiting roster rows and a small transfer anchor for up to two minutes. No old entity dictionary is retained. |
| Fresh own-character record | Establish current self as before. A different name/server clears old party and ticket ownership. |
| Fresh server-bearing appearance after an allowed transition/despawn | Rebind self only when name/server exactly match an earlier own-character record and the current entity match is unique. A bare nickname cannot grant self. |
| Own entity despawn, contradictory player identity or ID reused by an NPC | Revoke that old self binding immediately. Other current party actors can continue while self refreshes. |
| Exit to a classified open map | Clear carried party scope; require a new actual party list to authorize others again. Self can still refresh normally. |
| Solo/replacement roster or capture gap | Revoke departed/unconfirmed players immediately. A later internal move cannot resurrect the old roster. |
| Unknown zone kind | Avoid assuming an instance. A changed unknown zone does not inherit dungeon-only continuity. |

A real world/zone transition archives the previous encounter in History; this release does not merge targets with recycled IDs across worlds. Internal movement keeps the current encounter, subject to the existing normal idle/boss lifecycle. Capture remains passive; no character queries, estimated hits or background uploads were introduced. Foreground/topmost settings remain independent from packet ingestion.

## Protocol provenance

Reviewed SkeeveAN/Aion-DPS-Meter at MIT revision 3db5c905044d466858c13083c7049cae6dd60239 (2026-10-23):

- Client/Aion2/Protocol/Aion2FrameDecoder.cs DecodeZone distinguishes repeat zone movement from a new zone.
- Client/Aion2/Aion2EntityDirectory.cs NoteZone and NoteParty keep membership separate; a replacement roster including only self represents disband/leave.
- Client/assets/aion2/zones/zones.json and Protocol/Aion2Zones.cs provide numeric zone kinds and the documented 6xxxxx instance fallback.

Source: https://github.com/SkeeveAN/Aion-DPS-Meter/tree/3db5c905044d466858c13083c7049cae6dd60239 . WorldZoneFacts.cs contains numeric ID/kind facts only, with attribution. The MIT notice remains distributed. AscendAPP independently implements the transfer/state policy, conservative exact-profile binding, UI behavior and regression tests. No configured-name guessing, class-only actor inference, map coordinates, artwork or damage estimates were copied.

## Tests and in-game check

351 core checks and 79 native WPF checks pass; zero build warnings/errors. New tests exercise the Ethernet/TCP/framer/decoder/filter/engine path across a complete synthetic dungeon visit, including duplicate notices, outsiders, wrong-server identities, nickname-only identities, retired connections, FIN-first transfer, expired transfer, stage changes, dungeon exit, roster replacement, a character switch, capture gaps, NPC ID reuse and player despawn/reappearance. Native checks use the actual MeterController pipeline and compact view model. Shared profile migration/reload and previous capture/healing/timer tests remain included.

No new real dungeon fixture was supplied. Try a party dungeon with capture already running before entry, select Self and party, and use an internal gate without restarting the meter. Confirm that both player rows and new damage continue, then leave and check that old carried rows are removed until the game supplies the current party list. Existing 0.9.3 shared settings apply automatically; a saved Self only selection is intentionally preserved.

If tracking still stops, export diagnostics immediately before and after the gate, recording the dungeon name, whether there was a loading screen, whether rows vanished and whether packet/frame counters continued. Reports now include InternalMoves, RetainedParties, ProfileRebindings and PartyMembers. These are counts and known metadata, not raw packets or endpoints.

For deeper decoding, the minimum anonymized real fixture is the ordered decoded server frames from about five seconds before to ten seconds after the failing gate, preserving timestamps, stream/direction labels, opcode, payload structure and consistent pseudonyms/IDs. Include 0x2136 zone records, 0x3336 own records if present, 0x0297 roster/replacement records, 0x048d appearances, 0x4536 nicknames, 0x4236 despawns, target 0x4136 spawns, relevant 0x0438/0x0538 damage, and whether the TCP stream closed/reopened. Preserve the original byte lengths when pseudonymizing names and remove unrelated login/auth/chat traffic. Never substitute invented packets for a missing observed message. A small controlled capture should be sanitized before sharing; automatic raw logging is not enabled by this release.
