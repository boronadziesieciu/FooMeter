# AscendAPP | Architecture decision record 001

## Scope

A standalone, English-language Windows combat analytics application for Aion 2. The desktop UI has a full dashboard and a compact, translucent overlay. The styling is inspired by examples of compact minimalist meters while remaining a separately authored design.

**Environment:** .NET 10, WPF, SharpPcap 6.3.1, external Npcap driver.

## Data pipeline

1. Npcap captures locally received TCP segments through SharpPcap.
2. `TcpPacketExtractor.TryExtract` validates Ethernet, VLAN and IPv4/IPv6 lengths, then emits directional payloads. It rejects unsupported fragmented IPv4 traffic and does not decrypt TLS.
3. `TcpReassembler` tracks direction-specific sequences, duplicates and bounded reordering. Overflow or gaps require frame resynchronization.
4. `AionFrameReader` attempts publicly described VarInt frame lengths, nested envelopes and LZ4. These remain unverified candidates, not authoritative game records.
5. `ICombatPacketDecoder` is intentionally a no-op for Global until packet layouts are confirmed. No guessed amounts or actor IDs are emitted.
6. `CombatEngine` ingests typed, validated `CombatEvent` entries, aggregates DPS/HPS and snapshots fights.
7. `UiModel` publishes English UI fields to WPF; `OverlayWindow` and `MainWindow` share it. Network callbacks do not update WPF objects directly.

## Compact overlay design

The default size is 422 × 92 WPF logical pixels at 100% display scaling. It contains:

- Header: green activity light, truncated encounter name, fight clock and minimal toolbar.
- Main strip: featured player name, outgoing damage, DPS and total-share percentage on a subtle plum gradient.
- Footer: identity hint and truthful session source (`DEMO`, `REPLAY`, `OFFLINE`, `NPCAP • LIVE`). No invented latency or gear score.
- Expand button: switches to an additional scrollable party ranking with a taller window.
- Position lock, copy-result and open-dashboard actions.

The engine currently cannot establish the true local player identity. The demo's `MyCharacter` is preferred; otherwise the top-damage entry is featured. These are temporary heuristics to replace after decoding actor identities.

## Concurrency, validation and privacy

- Capture/reassembly is synchronized at the packet pipeline boundary.
- The combat engine maintains its own lock and returns snapshots for UI consumption.
- Npcap traffic is held in memory by default, not written to disk and not uploaded.
- JSONL imports are bounded by size and record count and saved reports contain normalized events only.
- Third-party tool compatibility with the game is a separate policy question; do not bypass protection systems.

## Delivery gates

| Gate | Required outcome | Current status |
|---|---|---|
| M0 | Builds and runs on Windows; synthetic encounters render DPS/HPS | Source prepared, build pending |
| M1 | Capture fixture library from sanitized, consented Aion 2 EU/NA traffic | Not started |
| M2 | Verified opcodes, actor/target, skill ID, damage and healing extraction | Not started |
| M3 | Reliable boss detection, wipes, summons, party identity, damage attribution | Not started |
| M4 | Settings persistence, hotkeys, overlay click-through, accessibility | Not started |
| M5 | Installer, smoke tests and ToS/anti-cheat compliance review | Not started |

## Tests

`dotnet run --project tests/AscendAPP.SelfTest` executes headless checks of combat math, stream handling and JSONL. The current development environment has no .NET SDK or Windows WPF runtime, so these tests could not be executed here. Test builds and runtime verification should be performed before any release.
