# Protocol port provenance

Selected algorithms and numeric protocol facts are adapted from:
https://github.com/SkeeveAN/Aion-DPS-Meter/tree/53033afadf3395dd47708abb308346734d8b1a8a

Copyright (c) 2026 SkeeveTV. MIT. The complete notice is in SkeeveAN-MIT.txt and accompanies the compiled app.

Referenced upstream files:
- Client/Aion2/Protocol/Aion2FrameDecoder.cs: selected character, appearance, nickname, direct-hit, DOT, NPC spawn/owner and HP readers. AscendAPP's port is substantially reduced and uses its own domain/event types.
- Client/Aion2/Protocol/Aion2Tickets.cs: masked ticket-list/update readers and Odyle ticket ID; adapted with strict bounds, exact payload consumption and character/session binding.
- Client/Aion2/Protocol/Aion2FrameDecoder.cs additionally supplies selected local-party roster shapes and combat-state/start fields. AscendAPP independently implements rate/forecast logic and conservative unknown states.
- Client/assets/aion2/npcs/boss_rage.json: explicit NPC duration entries, stored in data/boss_berserk.json. The upstream default-300 behavior is deliberately omitted; these durations are not independently verified against Global.
- Client/Aion2/Protocol/Aion2SkillNames.cs: numeric class-code/prefix classification and skill-family filtering logic.
- Client/assets/aion2/protocol/opcodes.json: opcode constants and the synchronization list; upstream uses big-endian opcode pairs and VarInt length bias -4.
- Client/assets/aion2/skills/heal_skill_families.json: numeric heal-family bases.
- Client/assets/aion2/skills/rdps_watchlist.json: only numeric non-damage buff-family IDs, excluding target debuffs. No damage-amplification percentages were copied.
- Client/Aion2/Capture/TcpReassembler.cs: synchronization/TLS-handling reference. AscendAPP's bounded TCP reassembler and framer are retained and extended.

Changed files with adapted implementation/data: ExperimentalGlobalDecoder.cs, OdyleEnergyReader.cs, data/boss_berserk.json, GlobalProtocolFacts.cs and the framing synchronization/TLS extension in AionFrameReader.cs. No upstream hit estimation, guessed ownership, remote services, executable, game art or English skill-name table is included. GPL alternatives were researched previously but no code from them is part of this release.

Upstream comments report calibration against captures. The v0.6 base was reported working by the user in game. The new v0.7 fields have synthetic fixtures only and are not independently calibrated against live traffic.

## Unmodified binary dependencies

- SharpPcap 6.3.1: MIT, dotpcap/sharppcap revision bfedf297e7410ffcf44e05d14f7fbef304f20895. License: SharpPcap-MIT.txt. Source: https://github.com/dotpcap/sharppcap/tree/bfedf297e7410ffcf44e05d14f7fbef304f20895
- PacketDotNet 1.4.8 (SharpPcap dependency): Mozilla Public License 2.0, copyright Chris Morgan and contributors. License: PacketDotNet-MPL-2.0.txt. Its unmodified corresponding source is freely available at https://github.com/dotpcap/packetnet/tree/690707ce56d6e9c266daf6236c4f76ac5035334c
- K4os.Compression.LZ4 1.3.8: MIT, copyright (c) 2017 Milosz Krajewski. License: K4os-LZ4-MIT.txt. Source: https://github.com/MiloszKrajewski/K4os.Compression.LZ4/tree/1.3.8

These libraries are distributed as separate unmodified DLLs. Npcap and the .NET Desktop Runtime are separate installations, not bundled in the framework-dependent ZIP.

For v0.8, data/ticket_names.json contains English count-ticket labels from Client/assets/aion2/tickets.json at the same pinned MIT revision. Caps, refill rules and time-ticket interpretation are not imported. Generic ticket snapshots retain only the decoded count/bonus values and receipt time.

For v0.8.1, data/targets.json and the legacy data/boss_names.json use explicit NPC IDs and English labels from Client/assets/aion2/npcs/boss_npcs.json and Client/assets/aion2/npcs/npc_names.json at the same pinned revision above. The NPC-name blob SHA is 8a96f2b06cbbffcd45bc1c9c664e1a8389960530. Only boss identity/name pairs and 51 explicitly named dummy/scarecrow/punching-bag identity/name pairs are included. No HP estimates, party scaling, drop rates, numeric-range classification or runtime substring matching is imported. Training Scarecrow ID 2400032 is present in that source; this is catalogue provenance, not independent verification against new live Global captures.

For v0.8.2, the 0x2136 zone message prefix (counter/zone ID) was referenced from Client/Aion2/Protocol/Aion2FrameDecoder.cs at the same pinned MIT revision. Client/Aion2/Aion2EntityDirectory.cs SetLocalCharacter was consulted to confirm the explicit own-record identity boundary. AscendAPP independently implements the zone lifecycle, strict pending-identity state, capture-surface handover, retired-stream suppression and diagnostics. No upstream configured-name or stream-statistic self inference was imported. Synthetic fixtures do not verify the live Global ordering of zone and character records.

For v0.9.0, selected 0x0191 field-boss list layout handling is adapted from Client/Aion2/Aion2FieldBosses.cs at the same pinned MIT revision. data/field_bosses.json includes only the explicit Verteron map ID, selected NPC names/IDs and place numbers from Client/assets/aion2/bosses/field_bosses.json. data/recurring_events.json condenses selected UTC start/cadence/window facts from Client/assets/aion2/schedule/events.json (Shugo Festival, Spacetime Rift and possible Beritra/invasion slots). No upstream map-learning, assumed respawn cycle, remote service, coordinates, art, or dated Abyss schedule was imported. AscendAPP implements its own bounded server-separated cache, window toggling and timer UI. Table schedules are estimates; these new packet fields have synthetic regression fixtures but no supplied real Global timer calibration.

For v0.9.1, the same pinned MIT DecodeRoster/HomeServerBehindName, DecodeVarintDot, DecodeHp and Aion2EntityDirectory NoteParty/NoteIdentity/server alias behavior were consulted. The roster snapshot lifecycle and optional home-server prefix are adapted; AscendAPP independently implements conservative unique-nickname matching, UI roster rows, diagnostics and HP-based healing reconciliation. No upstream class-only ID inference was copied. Initial HoT announcements are excluded following the documented tick shape. No verified effective-heal wire field is claimed. See docs/PARTY_HEALING_0.9.1.md for limits and required real fixtures.

For v0.9.4, WorldZoneFacts.cs contains only numeric zone IDs/kinds from Client/assets/aion2/zones/zones.json and the documented 6xxxxx fallback from Client/Aion2/Protocol/Aion2Zones.cs at MIT revision 3db5c905044d466858c13083c7049cae6dd60239 (2026-10-23). Aion2FrameDecoder DecodeZone and Aion2EntityDirectory NoteZone/NoteParty were consulted for lifecycle behavior. Source: https://github.com/SkeeveAN/Aion-DPS-Meter/tree/3db5c905044d466858c13083c7049cae6dd60239 . AscendAPP independently implements bounded transfer anchors, strict exact verified-profile rebinding, exit invalidation and diagnostics. No map coordinates, artwork, name-based configuration, class-only player inference or damage estimates were copied. Synthetic transitions do not independently verify the user's live dungeon packet order.

## 0.10.0 PvP radar additions

Selected protocol facts additionally reference the MIT-licensed revision [8c14501afabb109f9c30963e1c6259a3479b5189](https://github.com/SkeeveAN/Aion-DPS-Meter/tree/8c14501afabb109f9c30963e1c6259a3479b5189), inspected on 2026-10-24. Copyright (c) 2026 SkeeveTV; the unchanged full MIT notice in SkeeveAN-MIT.txt accompanies source and compiled packages.

- Client/Aion2/Protocol/Aion2FrameDecoder.cs: selected varint-relative movement offsets and coordinate bounds for 1A37/1B37/1C37/1E37/2037/2837/2937/2A37, 1F37/2F37 and own 3C37 tag 3; explicit zone XY prefix and character/nickname faction bytes. Adapted into RadarPacketReader.cs and selected ExperimentalGlobalDecoder.cs fields/events. AscendAPP payloads exclude the two opcode bytes, and the port accounts for that offset difference.
- Client/Aion2/Aion2EntityDirectory.cs: centimetre XY coordinate scale and explicit local/remote-position semantics were consulted. Environment-derived origin and self-position estimation are deliberately not imported.
- Client/Aion2/Protocol/Aion2Servers.cs: only numeric known faction/region/slot blocks are used by RadarTracker.ServerFaction; no server-name table was copied. The reference's explicit arena IDs supplement AscendAPP's existing pinned zone facts solely to avoid guessing team allegiance from faction.
- Client/assets/aion2/protocol/opcodes.json: movement and local-position opcode constants checked against the existing framer synchronization list.

RadarTracker, bounded/freshness-limited contacts, conservative unknown allegiance, separate WPF radar drawing/window, shared settings and controls are implemented in AscendAPP. No upstream position prediction, map tiles, artwork, memory access, injection or remote lookup service is included. These additions have synthetic protocol/render checks only; actual Global movement scale, orientation and ordering remain unverified. Existing capture licensing and notices above are unchanged.

## 0.10.1 radar continuity and own-position additions

RadarPacketReader.TryEffectPosition additionally adapts the explicit local-caster XYZ tail and apply/renew header checks from DecodeBuff (opcodes 0x2A38/0x2B38) in Client/Aion2/Protocol/Aion2FrameDecoder.cs at the same MIT revision 8c14501afabb109f9c30963e1c6259a3479b5189 above. Copyright (c) 2026 SkeeveTV; SkeeveAN-MIT.txt is unchanged. Only a verified own caster can supply an origin. Ground-aim coordinates, environment estimates and guessed current locations are not imported. Retention, observed-travel rotation, short trails, cache eviction, WPF display-frame coalescing, markers and click tracking are independently implemented. Live accuracy of this additional packet shape is unverified without a real fixture. SharpPcap remains an unmodified separate DLL; its existing configuration API is used to request immediate delivery.

## 0.10.2 player lifecycle and facing additions

The same pinned MIT revision 8c14501afabb109f9c30963e1c6259a3479b5189 supplies selected TryReadAim/DecodeWindup facing fields for 0x0238/0x0938 and DecodeAppearance's known-victim/nonzero-killing-skill prefix. Its DecodeHp zero-HP/revival and DecodeNpcSpawn/DecodeNpcRemove lifecycle comments were consulted. Client/Ui/ReplayView.cs was consulted solely for the public trigonometric convention that facing uses cos(angle) for +X and sin(angle) for +Y. Full copyright (c) 2026 SkeeveTV / MIT notice remains in SkeeveAN-MIT.txt and accompanies both packages. Continuous camera yaw is not claimed. AscendAPP independently implements proof flags, corpse suppression, fresh-position revival, manual force references, priority, drawing and separate appearance controls. No other-party list is assumed to identify the local force, and no screen/camera-memory reader is imported.


## 0.10.7 radar orientation and handshake validation

The existing pinned MIT protocol facts above remain the basis for 0x1036, the RSA public-key field and known trailer. AscendAPP independently parses DER with .NET RSA and adds SYN-anchored reassembly, counter-only diagnostics and a shared fresh-direction frame. NC’s HUD notice (https://lounge.plaync.com/feed/62397) and the public README at https://github.com/NoRhythmDEV/aion2-map-overlay were consulted as UI/context references only. The latter had no root license when checked on 2026-10-24; no code, assets or map data were imported. Its screen-only view-cone reader is not included. All new visual previews are AscendAPP-rendered synthetic fixtures. Existing full licenses and class-artwork provenance remain unchanged.


## 0.10.8 login compatibility

The existing lunixxx37 MIT revision fe2122f0e54e1de854c11e91e3c7bf51916a4f52 was rechecked: tools/aion2_client_crypto.py and docs/C2S_RESEARCH.md document the key field, specific reference trailer and post-handshake cipher boundary. AscendAPP independently separates metadata from provisional RSA envelope recognition, supports standard PKCS#1/SPKI imports via .NET, and emits shape-only diagnostics. SPKI or differing metadata are compatibility tests, not verified observations of this user's client. Matching setup and two clocks remain required. No additional upstream code or artwork was imported; the existing full MIT notice is retained.


## 0.10.9 resumable session lookup

The existing fe2122f MIT constructor/owner/paired-state facts remain unchanged. SessionOwnerSearch, bounded cursor retention, interleaved owner-region scheduling, generation/process/module cache scope and progress exports are independently implemented by AscendAPP. The original supported owner arena and structural validation remain in use; the reference's broader fallback was not imported. No new unlicensed code, artwork, protection bypass or revision-3528 offsets were copied or assumed. The full MIT notice is retained.


## 0.10.10 structured-owner fallback and flight continuity

The same MIT lunixxx37 revision fe2122f0e54e1de854c11e91e3c7bf51916a4f52 was rechecked. SessionKeyFacts.SlotsKey/LooksLikeSlots and bounded search use its locate_states_via_owner_arena owner_arena_pattern facts: adjacent shared state/control pointers, complete RC4 permutations and matching 214-byte keys. The owner vtable marker is no longer the only candidate path. ClientMovementReader additionally adapts documented 0237 and 0A37 flag/marker/XYZ/heading/velocity layouts from protocol/opcodes.json and docs/C2S_RESEARCH.md. Full copyright/MIT notice remains in Aion2ProtocolResearch-MIT.txt. Stationary-hold lifecycle, source provenance, held-cone UI, world-height display and tests are independently implemented. The full-private-memory fallback, motion prediction and camera-memory access were not imported; no revision-3528 success is claimed.


## 0.10.16 automatic score readers

AutomaticRatingReader.cs independently implements numeric/structural wire facts described in taengu/A2Tools-DPS-Meter at 836f8c07d277c07ca4926c71ac30ae626909ce64 (own 0x3336 and party 0x0297). No GPL implementation, fixture bytes, binaries or assets are incorporated. The implementation and synthetic fixtures are written for AscendAPP. See docs/AUTOMATIC_SCORES_0.10.16.md for primary references and validation limits. Abyss public documentation was inspected; its repository provides no decoder source and no Abyss source or artwork was copied. Existing MIT adapted portions and all license notices above remain unchanged.

## 0.10.18 selected target skill bar

The pinned MIT revision 729cb910494a1a0a7b837b8914c98921ce512fd4 of SkeeveAN/Aion-DPS-Meter was consulted for docs/protocol/{skill-frames,combat-frames,cooldown-frames}.md and Backend/src/{portraitService,ncCharacter}.ts. Only explicit selected-target and skill-use header facts and public official-service URL shapes are used. TargetSkillTracker, TargetProfileClient, the bounded public HTML parser, HTTP orchestration and WPF overlay are independently implemented. The SkeeveTV MIT notice remains included. No Shugo code or private API is copied. Numeric cooldown references are read from public skill pages at runtime and credited in the UI/tooltips. Public profile/skill art belongs to NCSOFT; no real character response is distributed. The previous radar and native session lookup implementations have been removed; earlier sections above are provenance history.

PublishedCooldownRanks.cs independently represents numerical rank/duration facts for Templar Defiance from the rendered public https://shugo.gg/skills/templar/12260000 table (data dated 2026-10-22, read 2026-10-27). The linear 60s..21s reference is guarded by matching class, skill ID/name, base duration and reduction immunity. No site implementation or descriptive text is copied. This published reference still requires current Global in-game calibration.

## 0.10.19 focus continuity

Target focus retention, bounded per-player state, identity-safe rebinding, asynchronous focus-generation checks, uncertainty states and new WPF controls are independently implemented. Existing protocol/public-service provenance and artwork/dependency notices above remain applicable. No additional third-party implementation or assets were imported.

## 0.10.20 cooldown source audit

CooldownEffectText and PassiveCooldownAudit independently parse explicitly formatted numerical effects from the official character, unlocked-board and character-specific equipped-item responses. Official public character JavaScript was consulted solely to verify item endpoint/parameter shapes; none of its implementation is distributed. Existing pinned SkeeveTV MIT portraitService/ncCharacter and Daevanion stat-token references were consulted, with no new catalog/database copied. Shugo title pages were checked for equipped/owned distinctions; no Shugo code, private API or assets are imported. Existing dependency, protocol and game-art notices remain applicable. Actual research responses and website scripts are excluded from release packages.


## 0.10.24 slim meter

The shared map, its server, map artwork and position-only readers were removed. Original class PNGs were verified against pinned SkeeveAN revision 03026182c4ae2059b41804189852af6e945e365a using Git blob SHA-1; per-file SHA-256 provenance ships with source. Class artwork remains NCSOFT copyright. Icons are now embedded in the desktop assembly; English class aliases select the same original artwork.


## 0.10.25 meter and events

The target-skill tracker, public character/profile requests, public cooldown-page parser, bonus audit and manual skill presets were removed. Feature research used the official public Abyss documentation/repository and NotMeter public website/tutorial and repository metadata. No desktop implementation source was available from those inspected distributions. No competitor code, artwork, private endpoints, cache databases or binaries were incorporated. Search, same-target encounter comparison and independent event appearance controls are independently implemented. Sources and scope are recorded in docs/COMPETITOR_REVIEW_0.10.25.md and docs/RESEARCH_SOURCES_0.10.25.json. Existing protocol/dependency notices remain applicable.


## 0.10.26 local history and status

The bounded local archive, corruption/lease handling, combat strip and capture-health presentation are independently implemented using existing .NET/WPF facilities. No new third-party package, competitor code, artwork or protocol source was added. Existing notices remain applicable.

## FooMeter 0.10.27 pet tracking

Pet wire facts/readers reference MIT projects elkaida/AION2-pet-tracker and MartinLVila/aion2-pet-overlay. See PetTracker-MIT.txt and docs/PET_TRACKER_0.10.27.md for provenance. English pet name/ID facts are from the former project’s data/names.json, credited upstream to MetaBot.GG; game names remain NCSOFT property. No map, spawn, drop-rate or pet artwork was imported. The pink flame identity is original FooMeter artwork. Existing class-icon notices remain applicable.

## FooMeter 0.10.28 appearance and pet portraits

Live color settings, compact event presentation, popup counter and sync diagnostics are independently implemented. 207 small pet portraits are bundled separately from MIT source code; see Pet-Artwork.txt and data/pet_art_sources.json for NCSOFT attribution and exact provenance. No competitor source or artwork was copied for the theme or pickup UI. No new runtime package was added.

## FooMeter 0.10.29 loot-row recognition

A local, bounded, foreground-game OCR reader uses the official Tesseract 5.2.0 NuGet package and tessdata_fast English model. See Tesseract-Components.txt, Tessdata-Apache-2.0.txt and Leptonica-License.txt. User-supplied screenshots/animation frames used for validation are not redistributed. OCR never changes collection totals. No Kisk implementation is included.
