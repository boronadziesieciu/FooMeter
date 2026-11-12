# AscendAPP 0.5.1 — capture diagnosis and fixture request

## Verified outcome

This is a diagnostic build, not a working Aion 2 Global decoder. `UnverifiedGlobalDecoder.TryDecode` unconditionally returns false. Therefore v0.5 cannot collect character identity or live combat values, regardless of Npcap configuration. Launching the executable only verifies startup.

The source ZIP in Downloads matched all .cs/.csproj/.xaml files in the Desktop source folder supplied by the user. Changes were made in a workspace copy; the Desktop project was not overwritten.

## End-to-end inspection

| Stage | Finding | Change or remaining requirement |
|---|---|---|
| Npcap selection | One manually selected device; first enumerated device selected by default. No process/route discovery. Description alone could be ambiguous. | Device labels now include the Npcap name. Choose the adapter with actual game traffic; VPN/relay traffic may require loopback. No automatic selection is claimed. |
| SharpPcap | Opens promiscuous capture with a 1000 ms timeout; filter is `tcp` or a user-selected TCP port. Callback exceptions were swallowed. | Counts packets handed to the callback, unsupported/nonpayload packets, and callback errors. All counters reset on Start. Active capture is stopped before clearing combat state. Native driver drops and unsolicited capture termination are not monitored yet. |
| TCP extraction | Ethernet with up to two VLAN tags, IPv4, basic IPv6, Npcap NULL loopback and raw IP are supported. ACK-only packets are intentionally excluded from payload counts. | Packet count now distinguishes callback traffic from extracted payloads. IP fragments and IPv6 extension headers are unsupported. |
| TCP reassembly | Directional endpoint keys, overlap removal, duplicate suppression, up to 16 pending segments / 256 KiB, 64 streams. Starts at first observed payload. | Existing synthetic reorder/dedup test passes. Exposes gap counter. No SYN/FIN/RST lifecycle, idle expiry or gap timeout: reconnect reuse and persistent small gaps remain limitations. Start a fresh capture session after reconnect. |
| Framing | Candidate unsigned VarInt length with bias -4; scans invalid prefixes and assumes two-byte opcodes. Scans all selected TCP, including unrelated applications. | Fixed oversized length arithmetic before addition; exposes skips. A candidate frame is NOT proof of game traffic. No verified stream fingerprint or reliable midstream resynchronization. |
| Compression | Candidate FF FF + int32 little-endian plain length + LZ4 bundle, optional leading marker; size and recursion bounds. | Synthetic compressed round-trip and oversized output rejection pass. Incomplete nested frames and excessive depth now count as malformed bundles. Bundle format still needs real Global verification. |
| Protocol | Decoder is a TODO returning false for every frame. | UI now says DECODER UNAVAILABLE; no fabricated damage, no speculative live parser. |
| Identity | No packet-driven entity directory, self-login mapping, party mapping, despawn handling or summon ownership exists. CombatEvent merely carries supplied names/IDs/IsSelf. | Counts unresolved actor labels and distinguishes missing self identity. Removed UI inference from the name MyCharacter and fallback to highest damage player. Unknown events may still contribute to overall totals; they cannot populate the self card without IsSelf. |
| Event ingestion | A successful decoder would call OnCombat, resolve optional boss catalogue, call CombatEngine.Add and store exportable events. | Synthetic test decoder verifies pipeline callbacks and unknown/self identity diagnostics. Decoder exceptions are counted without aborting subsequent frames. Production decoder stays the stub. |
| Overlay | Overlay uses the same UiModel as the main window and refreshes every 250 ms; compact layout remains 422 x 92. | Preserved English UI and Elyos artwork/layout. FeaturedPlayer now requires explicit IsSelf. GUI interaction/native capture were not tested in a real game session. |

## Diagnostic states

Hover over the bottom status line to read the full summary. Counters are per capture session. A snapshot is internally consistent. Counts refer to capture-filter-matched traffic, not all network traffic.

- **No packets**: no callback packets yet. Check selected adapter, filter, Npcap installation/access and whether traffic is active. This can also be normal immediately after Start.
- **Packets without TCP payloads**: callbacks occurred, but the extractor found no supported TCP payload. Check link type, packet truncation and supported IP formats.
- **Packets without recognized frames**: TCP payloads exist, no complete candidate frame yet. Could be incomplete data, wrong stream, unknown framing, midstream start or unsupported transport.
- **Frames without decoded events**: candidate frames exist, no normalized combat events. This is expected with the installed stub, even on the correct adapter.
- **Events with unknown player**: events exist but self identity is absent or some events contain a missing/placeholder actor label. Unknown-actor count counts events, not distinct players. A named actor alone does not establish self identity.
- **Decoded events; player identity available**: plumbing has received an explicit self event. This is not a claim of protocol validation or completeness.

Skipped-byte and malformed-bundle counts cover currently retained framers; eviction at the 64-stream limit discards their per-reader totals. Broad TCP capture can hit that limit. A verified game connection filter is needed for reliable protocol analysis.

The existing JSON report action now writes `{ Decoder, LiveDecoderVerified, Capture, Combat }`. This changes its previous bare combat-snapshot schema. Capture diagnostics contain no packet bytes or endpoints. Combat can contain character names from demo/replay or future decoded events; review before sharing. JSONL export remains normalized events, not packet recording.

## Open-source research, 22 October 2026

No third-party decoder code was incorporated into this update. No upstream binary was run. Repository claims and comments are evidence to investigate, not independent live verification.

1. **SkeeveAN/Aion-DPS-Meter**, MIT. License and decoder inspected; pinned revision `53033afadf3395dd47708abb308346734d8b1a8a`.
   - License: https://github.com/SkeeveAN/Aion-DPS-Meter/blob/53033afadf3395dd47708abb308346734d8b1a8a/LICENSE
   - Layout: https://github.com/SkeeveAN/Aion-DPS-Meter/blob/53033afadf3395dd47708abb308346734d8b1a8a/Client/Aion2/Protocol/Aion2Protocol.cs
   - Decoder: https://github.com/SkeeveAN/Aion-DPS-Meter/blob/53033afadf3395dd47708abb308346734d8b1a8a/Client/Aion2/Protocol/Aion2FrameDecoder.cs
   - Layout comments describe VarInt framing with bias -4, consistent with this project's candidate reader. Opcode byte order is configurable upstream; its constants include damage 0x0438 whereas the existing synthetic AscendAPP fixture reads bytes 04 38 as 0x3804. An adapter must reconcile that explicitly.
   - Decoder depends on a protocol catalogue, entity directory and additional domain models. It also contains estimated-hit and guessed summon-ownership paths. A wholesale copy would conflict with this task's requirement to avoid invented live DPS. Any future port should isolate strictly decoded events, exclude estimates/guesses, verify provenance of each included file/table, and retain required MIT attribution.
2. **taengu/A2Tools-DPS-Meter**, GPL-3.0. README and license inspected: https://github.com/taengu/A2Tools-DPS-Meter and https://github.com/taengu/A2Tools-DPS-Meter/blob/main/LICENSE . It exposes a Rust capture implementation and parser provenance in its README. No parser code copied. Do not transplant it into an unknown-license AscendAPP distribution without resolving distribution/license obligations.
3. **cyberbadger6969/aion2-dps-meter**, GPL-3.0, and **Kuroukihime/AIon2-Dps-Meter**, GPL-3.0: additional Global-support claims, not independently verified here. No code copied.

MIT is a practical candidate for a narrowly scoped verified port; licensing alone does not establish correctness, safety or provenance of every dependency. AscendAPP's own intended license was not supplied in this package.

## Exact real fixtures needed

Provide game-only **PCAP or PCAPNG with complete packet bytes**, both directions, original packet ordering and relative timestamps. A combat JSONL, screenshot or truncated hex excerpt cannot validate capture/reassembly/framing. Retain the connection setup where possible and all game packets through each action. Use full snap length; do not export only displayed payload text.

Create these short clips (one continuous session with a timestamped action log is also fine):

1. **Character entry**: start before entering the world from character selection, continue 20–30 seconds after spawn without attacking. Include a zone transition if possible. Label self using a pseudonym and provide class, Global region and exact client build/patch date. This is needed for entity IDs and self mapping; do not intentionally record launcher/password entry or account authentication traffic.
2. **Single-skill baseline**: alone at a training target, wait 5 seconds; use one known skill once, wait for completion; repeat 3 times spaced apart; wait 10 seconds. Log relative time, skill identifier/name, target, exact displayed hit amounts, crit flags, number of hits and target HP if visible. Distinguish individual hits from the displayed skill total. This validates amounts and event multiplicity without DPS guessing.
3. **Multi-hit / DOT / heal**: separate short labeled actions for those available on your class, with displayed per-hit values and timestamps. Include only mechanics you can identify; missing ones remain unsupported.
4. **Identity and party**: one consenting party member joins/leaves, each performs a different labeled attack, then a zone change or relog. Preserve consistent pseudonyms and note whose action was whose. A summon clip is required before attributing pet damage to its owner.
5. **Failure comparison**: export the new capture diagnostics JSON after 30–60 seconds of the same session, with the selected adapter description, filter/port, Npcap version, VPN/relay use and whether Wireshark sees game TCP on that adapter. A PCAP from a working reference meter session plus the same observed hit log is especially useful.

Minimum to begin a strict decoder is clips 1 and 2 plus metadata and a diagnostics report. Party, DOT, healing and summons must remain unverified until their corresponding fixtures exist. No captures were supplied during this task.

### Capture and anonymization

Use Wireshark/Npcap on the interface carrying game traffic. Check activity while in world, identify the game's established remote connection(s), and retain only those streams in a separate exported capture. If a relay puts game traffic on loopback, capture the unencapsulated game flow there. Do not assume a port number. Record the chosen filter in the manifest. For this diagnostic app, leave its port blank initially or use the game TCP port you actually observed.

Remove unrelated traffic, pcapng interface comments containing personal details, and hostnames. Replace IP/MAC addresses consistently while preserving IPv4/IPv6, direction, ports, sequence/ack numbers, flags, payload bytes and packet timestamps relative to start. Recompute affected checksums. Keep the original privately so sanitization can be checked. Do not zero, truncate, search/replace names inside, or otherwise edit unknown/compressed application payloads: that can invalidate length fields, compression and identities. Packet payloads can still contain names or account identifiers even after address anonymization. If they cannot be sanitized correctly, share only a transport-anonymized consenting test-character clip privately, or first provide diagnostics and we can design a structure-aware local sanitizer. Do not present an address-only anonymized clip as fully anonymous.

Suggested bundle:

```
fixtures/
  character-entry.pcapng
  single-skill.pcapng
  mechanics.pcapng          # if available
  party.pcapng              # if available
  diagnostics.json
  actions.csv               # clip,relative_ms,actor_alias,skill,target_alias,amount,crit,notes
  manifest.txt              # client build/region, Npcap, adapter/filter, relay, sanitization steps
```

## Build and run

Requires .NET 10 SDK for source build. From the extracted source root:

```powershell
dotnet build .\AscendAPP.slnx -c Release
dotnet run --project .\tests\AscendAPP.SelfTest -c Release --no-build
```

The provided compiled ZIP is framework-dependent: install the Windows x64 **.NET 10 Desktop Runtime** if absent, extract the entire ZIP, then run AscendAPP.exe. Npcap remains separately installed. Choose an adapter, Start capture, wait during in-world activity, hover over status and export the JSON report. Demo and Showcase remain explicitly synthetic. Do not interpret their values as live results.

Validation: Release solution build succeeded with zero warnings/errors; 31 self-tests passed, including existing aggregation/reorder tests and new diagnostic stages, session reset, stub silence, decoder exceptions, LZ4 bounds and length-overflow rejection. Tests use synthetic protocol bytes. No real Npcap game session, actual Global decoder, visual interaction or installer was verified.
