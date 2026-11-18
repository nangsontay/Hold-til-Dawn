---
phase: 5
title: "Verify concurrency, recovery and hosted rollout"
status: in-progress
priority: P1
dependencies: [4]
---
# Verify concurrency, recovery and hosted rollout

**Outcome.** One developer-operated host supports three independent matches. [GDD §7.3](../../GDD_Assignment1.pdf) targets a Singapore VM with 4 vCPU and 4 GB RAM. Test the intended host before publishing a build.

- [x] Build the local Colyseus and imported worker asset package, plus scripts and templates for Linux, cold-boot DNS, renewal, drain and rollback.
- [x] Run the repeatable local runner with real Godot workers and clients. Verify both phase 1 checks and record the simulation flake. Measure clean and impaired three-room load and exercise local recovery.
- [x] Update the [README](../../README.md) with player and operator paths, health, ports, DNS, releases, logs, recovery and rollback.
- [ ] Install the Linux release on a clean host and verify systemd, public TLS and DNS, cold-boot IP changes, and the Linux drain and rollback rehearsal.
- [ ] Run the Windows and macOS exports with an external player. Verify target-VM capacity and a full-duration five-night match.

[Local results and the outstanding release gates](reports/phase-05-report.md)

| Test | Pass condition |
|---|---|
| Directory | List appears within 3 s of Play and reflects changes within 1 s. Three simultaneous rooms work; a fourth Create returns Server full with Refresh. Join remains available for waiting rooms with seats. |
| Gameplay load | Three concurrent matches sustain 60 physics ticks/s per room. In a five-player LAN night with 60 enemies, base HP and door durability agree within 0.2 s and enemy position differences stay below 0.5 tiles. Exercise a full five-night match separately. |
| Admission and isolation | Concurrent claim on the last seat succeeds once. Unadmitted peers receive no match snapshots. Invalid, expired, replayed, wrong-room and old-generation credentials cannot join. One room’s failures leave the others unaffected. |
| Leaving | Freeze within 1 s, transfer leadership to next human and test voted end, bot takeover, inventory drop, tie/empty votes, multiple departures and last human leaving. Verify an ongoing team may continue without an Engineer. Return to the same room after a result and start a new seed. |
| Recovery | Kill only the lobby: each active match continues; restarted directory recovers existing occupancy and cannot allocate a live port twice. Kill one worker: only its clients return to menu with a message within 5 s. Verify dead listing removal, reservation invalidation and slot reuse. Test a silent one-way UDP outage during an active match and brief recoverable jitter. Block UDP while WSS works: release the reserved seat on expiry and offer retry. |
| Cleanup | Last human departure ends its match immediately. Empty rooms close after 60 s. A waiting room reoccupied before expiry remains open. Reuse the port with a new generation. |

**GDD test details.**

- Capture timers, enemies, projectiles and in-progress actions at timeout. They must remain frozen across both votes while network heartbeats and vote deadlines advance. Each stage closes when all remaining humans vote or at 30 s. Test ties, no votes and early completion; reject votes from a bot or departed player. Resume after 3 s.
- Drop two humans in the same timeout. One shared decision must replace both or drop both inventories at their last positions. For a takeover assert role, stats, kit, HP, position and inventory survive. Check BAL-47 replacement behavior: safe-zone gathering and storage by day, least-defended attacked door at night, targets within 6 tiles, bandage below 40% HP, Healer revive within 8 tiles, Engineer repairs the most damaged door. Check participation in wave/boss population and restrictions on voting, crafting, building, night entry and travel beyond 20 tiles.
- Assert the room name follows the current leader and is bounded to 16 characters. On rematch all clients receive the same new worker seed before processing the new match. Compare local predicted movement and server correction against the baseline.

**Rollback rehearsal.** On an incompatible release stop new admissions and wait for live rooms to drain. Keep the old directory managing its workers throughout the drain. Restore the previous matched artifacts together, verify client-directory, client-worker and directory-worker protocol versions, then reopen Create. Confirm unrelated live rooms survive a compatible lobby-only restart.

**Release decision.** Report the first failed gate and its measured value. If the host misses the three-room target, profile worker simulation and snapshot costs before changing transport or reducing capacity. A lower cap requires a product decision.
