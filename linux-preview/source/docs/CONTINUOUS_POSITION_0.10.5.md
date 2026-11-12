# AscendAPP 0.10.5 — Continuous own position, class icons and ratings

## Install and try

1. Close the older AscendAPP. Extract the entire **Compact-Windows** ZIP into a new folder and launch AscendAPP.exe. The optional **Compact-Source** ZIP is separate; do not merge it into the runnable app.
2. Open the gear → **PvP radar** → enable **Continuous own position (experimental)**. This choice is off by default and retained in the shared profile.
3. Keep capture running, then reconnect to the game once. Starting capture after the world connection has already begun cannot reconstruct its cipher. A simple zone teleport may keep the same connection and is not a substitute for reconnecting.
4. Walk, run, stop, strafe and jump without using any skills. The source status should progress through session lookup and clock validation to **Client position · movement updates**. Watch the origin age and compare markers with landmarks. Flight is supported for the documented movement forms.
5. Follow can use Movement or Character facing. Outgoing movement includes a facing report, but that is still a character-facing proxy: camera-only rotation while idle is not established. Existing alignment offset, zoom, local zone maps and independent scale/opacity remain.

This version adds an alternative source to address intermittent own positions in the passive server stream. It does not increase what other players the server reveals, predict unseen enemies or promise better detection than the game minimap.

## Explicit optional process access

The optional source performs a **read-only lookup in the exact Aion process owning the captured IPv4 world connection on TCP 13328**. It locates a temporary session key, validates it against the captured connection and uses it to decode the game's outgoing movement. This is different from the earlier packet-only radar.

It uses query/read access, with no writes, injection, protection bypass, privilege adjustment or driver installation. Compatibility with the game's protection and the current executable layout has **not** been tested here. If access is denied or a signature changes, turn the option off and use the existing passive radar. Do not disable protection to enable this feature.

The supported constructor signature and owner arena are bounded. A lookup has a four-second deadline and a 64 MiB owner-scan budget, and unsuccessful attempts back off to at most one per minute. Once the key matches, routine movement uses captured packets; it does not poll process memory for coordinates. The game process name is the existing General / Overlay behaviour setting. An unexpected process cannot qualify merely by sharing a port.

Keys and decrypted setup data are never written to settings, logs, diagnostics, exports or packet fixtures. Temporary buffers are cleared after use. Disabling the source, either side of a TCP disconnect, capture reset or exit discards its cipher. Previews and desktop self-tests are explicitly barred from native process lookup, even when fixture preferences enable the option.

## Stream correctness

The collector keeps a separate ordered client-to-server stream. The clear RSA handshake must be captured, followed by the encrypted setup. Every subsequent encrypted message body advances the same cipher state, including unrecognized messages; clear length prefixes do not. There is no opcode scanning to regain a lost encrypted byte offset.

A key must match the full documented setup structure. Two plausible, non-regressing UTC clock messages are required before publishing movement. Documented lengths, flags, coordinate/facing bounds and client clocks are checked. Pending out-of-order data suppresses output until completed; permanent loss fails closed and asks for a reconnect. Buffers, connection counts and time windows are bounded.

The reverse connection tuple, capture adapter, verified own actor and radar world epoch bind the result. New worlds/characters discard old poses. A same-socket teleport keeps cipher order but requires a new movement observation. Hidden radar windows continue ingesting positions. No dead reckoning, interpolation delay or invented motion is added.

The fresh-source window is two seconds. When no new movement is sent (for example, stationary flight hover), the last real position remains age-labelled; it is not represented as a newly measured coordinate. Existing server observations remain available when the optional source is absent or stale.

## Meter rows

Nine in-game class emblems replace the generic geometric class marks. Elementalist uses the Spiritmaster emblem; an unknown class has a neutral fallback. Artwork is cached and reused.

**GS** and **CP** appear next to each player name, with complete values and observation information on hover:
- GS and CP can be read from the documented full player-appearance/nickname profile (0x4536).
- A validated inspect profile (0x5036) can update GS for an already verified exact name/server/class. Inspecting a player cannot create self identity or admit a stranger to the damage meter.
- CP is not inferred from GS, gear, damage, level or website item level. The game may normalize CP in PvP arenas.
- Missing, ambiguous or expired data shows **—**. Own ratings can remain unknown if the game does not send the required profile. An in-game inspection can supply GS when its server metadata is present; it does not guarantee an own CP value.
- Delayed updates, entity removal and world resets cannot reassign a cached score to a different actor. A GS-only inspection does not indefinitely refresh old CP.

The compact English UI, party/boss/dummy capture policy, effective-healing estimate, leader target glow, timers and shared settings/maps are preserved. Numbers in preview images are explicitly labelled **DEMO** and do not feed live sessions.

## Validation and live limitations

Release builds have zero warnings and errors. **589 core checks and 188 native WPF checks passed.** They include an independent published RC4 vector; synthetic outgoing sessions with split/reordered/retransmitted TCP; unknown encrypted bodies; wrong keys; missing bytes; stale clocks; reconnects and teleports; adapter/character isolation; fake-memory owner validation; rating ambiguity/provenance; packaged icons; default-off settings; hidden radar ingestion; and actual TCP → controller → radar bindings without skill messages.

**No new real capture was supplied, and no running game process was read during development.** These checks verify the implementation against documented facts and synthetic fixtures, not live compatibility or precision.

If your client remains unsupported:
1. Send Settings → Debug → **Save diagnostics report**, plus the exact short status shown in Radar settings. Reports exclude keys, packet bodies and endpoints.
2. For GS/CP, an anonymized decoded 0x4536 profile and (if inspecting) 0x5036 profile, paired with the GS/CP shown in the character window and a consistent invented character name/server mapping, will identify a layout mismatch.
3. For movement layout changes, provide a **locally sanitized decoded fixture** containing only movement opcodes 0037/0137/0337/0B37/0F37/1037/1137/1837, UTC-relative timestamps, and the movement sequence performed (walk four directions, strafe, stop, jump, flight, teleport). Translate all XYZ coordinates by the same offset if needed; preserve relative positions, lengths, flags and numeric types.
4. For framing/cipher errors, an offline fixture made with a **new synthetic key** must preserve frame lengths, ordering, segment sequence numbers and timing. Replace all authentication/account/setup strings with synthetic values before re-encryption. Include the clear public-key handshake, sanitized setup, two clocks and movement. Do not send the real session key, process dump, login/authentication payload or an unsanitized PCAP.

A changed native constructor/owner layout cannot be resolved from movement bytes alone; it needs an updated appropriately licensed public implementation or independent local analysis. Do not submit a process dump for this.

## Sources and licensing

- [Aion 2 Protocol Research — MIT, pinned fe2122f](https://github.com/lunixxx37/aion2-protocol-research/tree/fe2122f0e54e1de854c11e91e3c7bf51916a4f52): outgoing framing, session-key ownership/signature and documented movement. Locator facts were ported from its MIT Python tool; its full license is included.
- [Aion-DPS-Meter — MIT code, pinned 0302618](https://github.com/SkeeveAN/Aion-DPS-Meter/tree/03026182c4ae2059b41804189852af6e945e365a): observed player rating and inspect layouts.
- Class emblems are NCSOFT game artwork from the same pinned upstream asset directory, with separate provenance in THIRD_PARTY_NOTICES/NCSOFT-Class-Artwork.txt. The artwork is not relicensed under the code's MIT license.
- Aion-2-Helper was inspected as corroborating evidence. Its repository lacks a root license; no code from that implementation was copied.

Older release documents describe their historical limitations. The optional source described here supplements them.
