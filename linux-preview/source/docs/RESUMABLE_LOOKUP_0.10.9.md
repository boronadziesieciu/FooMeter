# AscendAPP 0.10.9 — Resumable session lookup

## Diagnosis from your 0.10.8 report

Your capture saw a new connection, 578 ordered complete client bodies and a recognized 283-byte login with a 2048-bit PKCS#1 key. It reported revision **3528** and different regional/version metadata. There were no pending TCP bytes. The lookup reached its bounded scan limit before validating a key; no clocks or movement could then be decoded.

In 0.10.8 every lookup rebuilt the candidate region list, scanned from the highest addresses, and stopped after 64 MiB. It discarded that search position before the next attempt. Retrying could repeatedly inspect the same prefix and never reach a valid owner below it. This is a concrete implementation problem; your report does not establish where revision 3528's actual owner lives.

## What changed

- Search keeps its position across timer attempts and Retry. It alternates the highest and lowest eligible allocator regions to cover both ends sooner. It repeats only an unfinished candidate if a budget expires during its follow-up reads.
- Each owner-search slice requests at most **8 MiB**, including structured follow-up reads, and yields between regions after a 150 ms target. Further slices are scheduled about 500 ms apart. The worker cannot overlap itself. The existing four-second per-call deadline remains a safeguard; individual Windows calls are not hard real-time operations.
- The executable signature is cached for an exact connection generation, process ID/creation time and module identity, and checked before reuse. Initial module recognition is a separate bounded phase; it is not included in the owner-slice byte counter. New sockets, reused tuples with a new handshake, process/module changes, disable and capture resets revoke stale search state. A late lookup cannot offer a key to a replacement connection.
- Progress reports the search stage, sweep number, regions checked/total, slice count, requested bytes, candidate owners and rejected keys. It contains **no process addresses, packet contents or key bytes**. Raw pages and temporary keys are cleared before a slice returns, including on cancellation/errors.
- An unfinished search says **Session lookup N/M regions · continuing**. A full supported search without a match says **Supported owner sweep complete · no matching session**. That distinguishes unfinished work from an unsupported layout. Completed searches back off before refreshing the candidate list for a later sweep.

This is **not a larger unrestricted memory scan**. Candidate owner pages remain limited to the previously documented arena 0x200000000–0x300000000 and eligible 64-KiB committed private writable regions. Structured owner/state/key pointers are validated as before. No new process permissions, writes, injection, debug-privilege changes or protection bypasses were added. The option remains off for a new profile until explicitly enabled.

## Install and test

1. Close the old app. Extract the whole **Compact-Windows ZIP into a fresh folder** and run AscendAPP.exe with the same administrator launch that cleared Windows 5. Keep the optional Source ZIP separate. Shared settings, maps and timers carry over. The portable build uses your existing .NET 10 Windows Desktop Runtime and Npcap.
2. Choose radar gear → PvP radar → **Prepare continuous capture**, then fully exit and reopen Aion while AscendAPP stays running and capturing. A new app cannot recover the discarded beginning of the previous encrypted connection.
3. Walk without using skills and let **N/M regions** advance. Retry preserves unfinished progress; do not repeatedly reset the character or restart capture. A large supported search may take several slices.
4. Success requires **stream: validated** and increasing **movement reports**. A found key alone is not proof of functioning movement. Exact world binding, correct setup, two valid clocks and supported fresh movement still gate the radar.
5. If a supported sweep completes without a match, or another error appears, click **Copy tracking diagnostics** and paste the text. The new Lookup block shows whether the candidate range was exhausted. An owner/state moved outside this supported layout needs a compatible licensed reference or independent local analysis; simply raising limits would not establish it. Do not send process dumps, session keys or unsanitized PCAPs.

Camera yaw remains unverified. Last-known positions remain labelled, and cannot be made current by faster rendering. DPS, party, boss timers, healing, radar player/death filters and appearance are unchanged.

## Validation

**710 core checks and 228 native WPF checks passed**, with zero Release warnings/errors. The new fake-memory case places a valid owner beyond the former 64-MiB prefix and finds it over resumed slices. Tests cover the per-slice total byte budget, alternating ends, no rescanning of completed pages, retrying a candidate at a budget boundary, yields, cancellation, exceptions, zeroed buffers/keys, exhausted ranges, rejected keys, restricted region shapes, cache lifetime and reused TCP connection generations. Native preview tests also check safe progress exports.

These tests use synthetic memory maps and packets. **No running game process or real profile was read during development/tests**, and no new packet fixture was supplied. The screenshot/report establish the old limit failure, not live revision 3528 compatibility. Optional native lookup remains unverified on that revision until you test this build.

## Sources and licensing

The existing MIT [Aion 2 Protocol Research, revision fe2122f](https://github.com/lunixxx37/aion2-protocol-research/tree/fe2122f0e54e1de854c11e91e3c7bf51916a4f52) remains the source of the constructor/owner/paired-state facts already attributed in SessionKeyFacts.cs. AscendAPP independently implements cursor retention, budgets, interleaving, scoped caching, lifecycle and progress reporting. The reference's wider general-state fallback was not imported. Numeric revision 3528 comes from your diagnostic report; no unsupported offset for it has been invented. Existing full licenses and class-artwork notices remain included.
