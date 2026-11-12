# AscendAPP 0.10.8 — Login compatibility and exact rejection details

Your screenshot confirms a new connection and 428 framed client bodies, but the login record was rejected. With no validated outgoing movement, the radar can only retain the old own origin. The last-known view is the consequence, not a separate rendering failure. The screenshots do not reveal which field failed.

## What changed

0.10.7 treated the reference regional/version trailer as a mandatory authentication check. 0.10.8 separates the length-prefixed public key from that metadata. A complete 2048-bit RSA public key in PKCS#1 or SubjectPublicKeyInfo encoding, with a valid odd exponent and 4–64 remaining metadata bytes, can establish a **provisional** post-login boundary. Unrecognized metadata is marked explicitly and its contents are not interpreted or exported.

This does not authenticate a stream or produce movement by itself. The app still requires the exact capture adapter / socket / verified own-world binding, the correct 214-byte session key, the structural setup record and two valid client clocks. Wrong keys, missing bytes, wrong worlds and old movement remain blocked. Only actual supported movement messages update position. The session-setup layout and native lookup compatibility may still differ on your client; these changes are not a claim of a successful live fix.

Malformed/truncated public keys, unsupported RSA sizes, extra bytes inside DER, invalid length prefixes and out-of-bounds metadata remain rejected. Diagnostics now distinguish those cases and show body/key/trailer byte counts, key encoding/size/exponent and reference-metadata status. No key bytes, packet bytes, tokens, unknown metadata strings or endpoints are retained in the inspection.

## Test this build

1. Close the previous AscendAPP and extract the **entire Compact-Windows ZIP into a fresh folder**. Run AscendAPP.exe. Keep Source separate. Shared preferences/maps persist. This portable build uses your existing .NET 10 Windows Desktop Runtime and Npcap.
2. Radar gear → PvP radar → **Prepare continuous capture**. Fully exit and reopen Aion while AscendAPP remains open and capturing. A fresh connection is needed because the old rejected handshake was discarded; teleporting or retrying the lookup cannot recreate it.
3. Walk without skills. Look for increasing **movement reports** and **stream: validated**. “Key recognized (metadata differs)” identifies a provisional boundary; it does not mean tracking is working yet.
4. If it still stalls, click **Copy tracking diagnostics** on the same page and paste that text in this chat. This is a shape/counter report, not a packet capture. It includes the exact rejection code if the key layout differs. No character reset is needed.

If login is still rejected, that copied report is the next required evidence. If it identifies a genuinely unknown key envelope, a locally sanitized clear 0x1036 fixture may be needed: preserve length-prefix, public-key DER encoding/size, revision and metadata length/structure; replace the public key with a synthetic public key of the same encoding if desired. Omit subsequent authentication/setup traffic. Do not send an unsanitized PCAP, session key or process dump. Later setup/movement fixture requirements are in RADAR_ORIENTATION_0.10.7.md and CONTINUOUS_POSITION_0.10.5.md.

## Verification and limits

**694 core checks and 219 native WPF checks passed**, with zero Release warnings/errors. New fixtures exercise both RSA encodings, bounded metadata variants, malformed/unsupported key diagnostics, wrong keys, withheld movement before two clocks, exact world isolation, safe JSON copying and the actual TCP → controller path with a non-reference trailer. These are synthetic fixtures; no new real packet fixture was supplied, and no running game process was read during development/tests. Native fixture paths prohibit live capture/process lookup and isolate profile writes.

The compact radar appearance and modes, self/confirmed-party boss/dummy DPS filters, healing, party/timer continuity, class/rating display and shared settings are unchanged. Camera yaw and continuous movement compatibility still require a live test. An old origin remains visibly last-known.

## Sources and licensing

The clear 0x1036 key field, reference trailer and full-body post-handshake stream behavior are documented by the MIT-licensed [Aion 2 Protocol Research, pinned fe2122f](https://github.com/lunixxx37/aion2-protocol-research/tree/fe2122f0e54e1de854c11e91e3c7bf51916a4f52), particularly tools/aion2_client_crypto.py and docs/C2S_RESEARCH.md. Its repository remained at that revision when checked for this fix. The reference generator uses PKCS#1 and a specific regional trailer. **SubjectPublicKeyInfo and non-reference metadata here are independently implemented compatibility handling, not observed facts about your client's login.** Their acceptance remains provisional until stream validation succeeds. Existing full MIT notices, other protocol attributions and artwork provenance remain included. No unlicensed implementation was copied.
