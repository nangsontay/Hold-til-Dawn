# Phase 5 release verification

**Release decision.** Hold the public release. The first unverified gate is the public WSS and UDP deployment on the target Singapore VM. There were zero public client connections, trusted-TLS probes or cold-boot IP changes in this run. The local build-only release produced a 572 KiB worker PCK and a compiled service. The packaged service, PCK and imported assets accepted source-based Godot clients on macOS.

The first local matrix failure was worker-death recovery. All five clients returned in 5,046 ms against the 5,000 ms limit. The client watchdog now allows 200 ms for polling and menu dispatch. The corrected run measured 4,778 ms. The independent simulation check failed its previously reproduced clean-HEAD `bots gather resources by day` assertion. The geometry check and managed-worker checks passed.

## Accepted risks and user decisions

- **Anonymous room and seat hoarding is an accepted risk for the three-room test (user decision).** The lobby applies no per-IP limits: nginx has no `limit_conn`/`limit_req`, and the directory caps only per-socket (10 messages/second, 256 clients). A few scripted sessions can fill all three rooms or hold seats indefinitely. Add per-IP limits only if the test is abused.
- **Lost upstream traffic freezes the match within 1 s, and a departure is final only after ENet confirms the loss (user decision).** A 900 ms input silence pauses the simulation; the player's seat is preserved until ENet's own timeout confirms the disconnect. If traffic returns first, the crew resumes with the same seats and no vote. The client keeps its 4,800 ms watchdog, inside the 5-second liveness budget.
- **Native-SDK WSS was proven locally only via a temporary CA** configured in `network/tls/certificate_bundle_override` (`python3 tests/match-worker-wss.py /path/to/Godot`), not yet against public certificates or exported clients. The public TLS gate remains open.

## Local matrix

The working tree was based on `1e09ad55911c`. The host was an Apple Silicon Mac16,10 with 10 logical cores, 24 GiB RAM and macOS 26.4.1. It ran Godot `4.7.2.stable.official.ed1daf0bf` and Node `26.13.0`. Workers used build `island-1-client-2`, worker content `island-1` and protocol 1.

| Gate | Result |
|---|---|
| Directory | The list appeared in 57 ms in both runs. The three clean admissions appeared in the observer's list within 7/49/49 ms. On impaired UDP they appeared within 0/0/28 ms. Create-to-publication, including a cold worker launch and admission, took 413–994 ms clean and 601–1,214 ms impaired. Three rooms filled; a fourth returned **Server full** with **Refresh** and a waiting room at capacity remained joinable if it had seats. |
| Admission and isolation | The directory integration exercised a concurrent last-seat race, ticket expiry, replay and stale generations. The ENet checks exercised pre-admission snapshot suppression, incorrect room/build/proof, and independent worker sessions. The packaged client flow exercised a corrupted credential and a blocked UDP port with WSS's local WS equivalent still reachable. The denied or stranded reservation expired and returned the seat. |
| Leaving | A leader's voluntary leave froze the crew in 63 ms. The same crew continued after a 3,002 ms countdown without an Engineer, then voted End after another departure, returned to the same room and received a common new rematch seed. A separate hosted client run verified Yes to bot takeover. The remaining room kept playing. |
| Recovery | The directory process was killed while all three matches were live. Every worker kept its PID and seed, and the directory recovered all fifteen seats without opening another slot. Maintenance mode rejected new admission while the workers played. Killing one worker returned only that room's five clients to the menu in 4,778 ms, removed its listing and freed its slot. A relayed one-way UDP outage froze the affected crew and showed the disconnected client its menu in 899 ms. |
| Cleanup | The local service and worker integration exercised the last-human result, continuous empty timeout measured at 60,193 ms after reoccupation and a second departure and port reuse with a fresh generation. The release runner stopped the local service, cleared all worker leases and released the bound ports. A scripted drain wait correctly refused to proceed while owned workers were active. |
| GDD rules | Managed checks covered frozen timers, projectiles, enemies and action channels, advancing input acknowledgements, early vote completion, a tie, empty 30-second stages and ineligible votes. They covered multi-vacancy takeover, preserved role/HP/equipment/position/inventory, dropping both inventories at their departure positions, boss/wave population, and replacement gathering, storage, door defense, healing, bandage, repair, the six-tile targeting limit, ineligible craft/build RPCs, the night-entry restriction and the 20-tile safe zone. Capped leader names and shared rematch seeds were asserted. The hosted suite injected a large predicted position error and observed server correction. |
| Five-night match | Deterministic checks advanced all five nights through the production progression rules, including the night 2 and 4 boss spawns and night 5 extraction. This fixture controls phase timers and boss defeat. A full-duration defended run with human input is unverified. |

## Three-room load

Each network ran for 20 measured seconds with 3 Godot worker processes and 15 real Godot ENet client peers in one headless process. Each room used the normal map, simulation, RPCs and full snapshots. The workload seeded up to 60 live enemies at night entry, then let production simulation run, including AI, combat, towers, movement and flask actions. Workers saw 58–60 enemies during measurement. The load harness routes through a userspace relay on localhost. The impaired run applied 2.5% independent UDP datagram loss and 15–45 ms of jittered one-way delay in each direction. Observed losses were 1,441/55,005 downlink datagrams and 189/7,037 uplink datagrams.

| Network | UDP port | Ticks/s | Snapshots/s | CPU p95, % of one core | Peak RSS, MiB | Tick cost p95/max, ms | Tick overruns | Up/down, Mbit/s |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| Clean | 7778 | 59.995 | 20.05 | 10.2 | 166.03 | 2.13/25.03 | 1 | 0.061/10.044 |
| Clean | 7779 | 59.980 | 20.00 | 10.3 | 165.84 | 2.15/24.72 | 1 | 0.062/10.088 |
| Clean | 7780 | 59.995 | 20.05 | 10.5 | 166.14 | 2.22/25.32 | 1 | 0.061/10.079 |
| Impaired | 7778 | 59.992 | 20.00 | 10.2 | 165.75 | 2.09/4.69 | 0 | 0.061/10.037 |
| Impaired | 7779 | 59.995 | 20.05 | 10.4 | 165.66 | 2.07/4.43 | 0 | 0.061/10.063 |
| Impaired | 7780 | 59.965 | 20.05 | 10.5 | 165.44 | 2.11/4.87 | 0 | 0.061/10.086 |

| Network | UDP port | Snapshot age p95/max, ms | Update gap p95/max, ms | Input ack p95/max, ms | Reliable action p95/max, ms | HP/door disagreement, max ms | Enemy position divergence, max tiles |
|---|---:|---:|---:|---:|---:|---:|---:|
| Clean | 7778 | 6.90/28.65 | 55.80/95.98 | 50.72/96.54 | 62.03/62.03 | 0 | 0.150 |
| Clean | 7779 | 8.23/33.57 | 56.17/103.84 | 62.53/110.74 | 47.86/47.86 | 0 | 0.081 |
| Clean | 7780 | 6.82/9.32 | 56.87/104.91 | 17.52/71.16 | 54.68/54.68 | 0 | 0.082 |
| Impaired | 7778 | 48.74/51.02 | 110.97/262.38 | 119.21/174.04 | 125.09/125.09 | 0 | 0.376 |
| Impaired | 7779 | 48.06/53.40 | 110.47/255.52 | 124.84/227.69 | 151.41/151.41 | 0 | 0.386 |
| Impaired | 7780 | 49.08/51.10 | 117.30/261.02 | 125.56/175.08 | 152.64/152.64 | 50 | 0.341 |

Bandwidth is offered relay traffic, including IPv4 and UDP headers and datagrams later dropped. Tick cost includes the worker physics callback and snapshot submission. Each clean worker had one tick interval over 25 ms. The service used 2.1%/2.5% CPU p95 and 115.86/115.39 MiB peak RSS across clean and impaired runs respectively. Aggregate worker plus service peak RSS was 613.88/611.73 MiB. Snapshot age is local wall-clock time from submission to receipt for matched simulation ticks. Enemy divergence compares the clients' latest received snapshots on a 10 ms grid with no renderer. Reliable action time measures the replicated flask decrement, with three actions per room. In a preliminary impaired run, a room reached 0.537 tiles of divergence. The GDD position target applies to a clean five-player LAN.

The final directory integration, managed-worker integration, packaged hosted-client integration and six-second clean gate rerun exited 0. After the last check all four test UDP ports 7777–7780 could be rebound. The first geometry run passed. The simulation failure remained isolated to the known demo-bot assertion.

## Packaging and operations

The local package compiled the service with locked production dependencies and zero platform-specific Node addons. All manifest checksums matched. The local Godot binary loaded the exported PCK from an isolated working directory. The release package can include the matching Linux engine, Linux Node distribution and both desktop exports. The desktop package recipe sets the bundled hostname and collects applicable project notices. Redistribution still needs upstream native dependency notice verification and signing decisions. The nginx template passed `nginx -t` with a disposable local certificate. The tested release runner used loopback WS. The Colyseus native extension again emitted its known ObjectDB warning when the test client exited.

The run used the README's maintenance model with a private state-directory flag and confirmed that active rooms were left alone. The Linux installer, atomic activation and rollback, boot DNS update and certificate renewal remain separate host gates.

## Unverified release gates and exact rehearsal

1. **Linux release package and supervision.** Supply the Godot 4.7.2 Linux x86-64 release template and pinned Linux Node archive to `package-release.sh`. On a clean Linux x86-64 host without the editor or SDK, install the release and unit/polkit templates, start the service, and admit clients to three independent workers. Check map and scenery collision parity, verify worker units can start and stop as `htd`, and verify a clean whole-service stop releases all UDP ports. The local package was build-only.
2. **Public WSS, UDP, cold-boot DNS and certificates.** Follow [the host provisioning and rehearsal steps](../../../README.md#dns-and-certificate-rehearsal). On the Singapore VM expose TCP 443 and UDP 7778–7780 through its public firewall, plus TCP 80 for HTTP-01. Confirm internal TCP 2567/2569 stay private. Use the RFC 2136 timer and journal to verify an A-record update after a real public IP change. From another network use the same exported binary and hostname to enter a match. Test accepted public trust, rejection of untrusted and hostname-mismatched certificates, and a Certbot renewal with nginx reload while matches play. The only native-SDK WSS proof so far is the local loopback fixture with a temporary CA (`python3 tests/match-worker-wss.py /path/to/Godot`); public certificates and exported clients are untested.
3. **Exported Windows and macOS and external player.** Set `PACKAGE_DESKTOP=1` with the exact export templates installed. Install the macOS arm64 and Windows x86-64 releases on their actual platforms. Verify the native SDK and imported map/assets, connect over the public hostname from separate networks, and run the Alice/Bob/Dana/Carol scenario through departure and resumption. Verify rendered positions and status during play.
4. **Production capacity and full-duration play.** Run three five-human rooms on the 4-vCPU/4-GB Singapore VM. Capture the same measurements on clean and impaired networks, including a five-player LAN with 60 enemies and a separate, full-duration five-night game ending at extraction or a natural result. Check the deadlines and rendered enemy position target. If hosted throughput falls short, profile simulation and snapshot costs before changing transport or proposing a smaller cap.
5. **Linux drain and rollback.** Stage two actual matched release bundles. With a live match, rehearse the compatible directory-only HUP restart and verify unchanged worker PIDs. Announce the maintenance window, drain and let players finish, wait for the empty leases, activate the alternate release, and run the client-directory, client-worker and signed registration tests on staging. Drain and restore the previous artifact set the same way, then reopen. Test a failed startup and confirm the previous healthy release restarts while admissions stay drained.

Commands and conditions are in the [operator runbook](../../../README.md#operate-the-hosted-service).
