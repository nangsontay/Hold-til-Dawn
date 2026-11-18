# Independent review: centralized multiplayer migration (phases 1–5)

**Verdict: not ship-ready.** The local implementation is sound. I found no Critical defects in admission, credentials, worker supervision or pre-admission exposure, and every local suite I reran passed except the known `sim_check` flake. The public path has never run end to end, though. The native Godot SDK has never connected over WSS, which release clients require. No exported client has run, and the Linux systemd, polkit and TLS deployment has never executed. Six Medium issues should also be fixed or explicitly accepted before a public build.

Scope: uncommitted changes to `main.gd`, `game.gd`, `bots.gd` and `README.md`, plus the new `match-worker.gd`, `worker-control.gd`, `multiplayer/`, `multiplayer-server/` (src, deploy, test), `tests/`, `export_presets.cfg` and `addons/colyseus/` (skimmed for pinning and licensing only). Each was judged against plan.md, phase files 1–5 and their reports. I did not read the other reviewer's report.

## Tests rerun for this review

All runs were on macOS arm64 with Godot 4.7.2.stable.official.ed1daf0bf and Node 26.13.0. Every process I started exited, and UDP 7777–7780 and TCP 2567–2569 were free afterwards.

| Command | Result |
|---|---|
| `godot --headless --path . -s tests/geometry_check.gd` | Pass (`GEOMETRY ALL OK`) |
| `godot --headless --path . -s tests/match-worker-check.gd` | Pass, 0 failures, exit 0 |
| `godot --headless --path . -s tests/sim_check.gd` (2 runs, working tree) | Both exit 1, only on `bots gather resources by day` |
| The same check on a clean `git archive HEAD` copy after a fresh import (3 runs) | 2 passed, 1 failed on the same assertion. The flake predates this change. The diff leaves the demo-bot path unchanged; replacement-only branches are gated by `p.replacement`. |
| `python3 tests/match-worker-integration.py <godot>` | Pass, exit 0 |
| `npm test --prefix multiplayer-server` | Pass, 1/1 in 104 s, including the 60,277 ms empty exit and port reuse |
| `python3 tests/hosted-client-integration.py` | Pass, exit 0, all four stages. It printed the known Colyseus extension warning that 20 ObjectDB instances leaked at exit. |

I did not rerun `tests/release-verification.py`, the three-room load harness. Its figures in the phase 5 report are unverified by this review.

## What holds up (verified by reading and the passing suites)

- **Admission credentials.** Ticket secrets leave the worker only in the signed control `reservation` reply and the direct WSS `admission` message (`lobby-room.ts:47-50`). ENet carries only a challenge-bound HMAC over protocol, room, generation, ticket, peer ID, client nonce and server challenge (`match-worker.gd:251-252`). Expiry uses the worker's monotonic clock (`match-worker.gd:198`), and every challenge and admit runs `_expire_tickets` first. Each connection gets one challenge (`spent`), proofs are single-use (the reservation is consumed in `_release`), and the client checks the room and generation in the challenge before proving (`main.gd:450`). Tickets from a wrong room, an old generation or a reused port fail because tickets live only in that worker's memory and the proof binds the generation. The directory suite exercises expired, stale and old-generation tickets.
- **Final-seat race.** The directory never decrements capacity itself. Both requests reach the single-threaded worker ledger, and the test asserts exactly one `full` result (`manager.integration.test.ts:296-299`).
- **Pre-admission exposure.** Snapshots go by `rpc_id` only to `worker.admitted` (`main.gd:550-553`). `server_relay` is off (`main.gd:231`). Input and actions are gated on admitted membership (`match-worker.gd:313-314,354-355`), `s_snap` is authority-only, and no broadcast `.rpc(` call remains. Directory entries appear only after `creatorAdmitted` and contain no secrets.
- **Supervision and port reuse.** A port becomes free only when no process carries the slot marker or systemd reports the unit inactive, *and* a UDP bind probe succeeds (`worker-manager.ts:234-245`). No timer alone frees a port. Registration requires the persisted lease's room and generation, the observed owner PID and a fresh nonce challenge, and it rejects a second live session (`worker-manager.ts:110-139`). Manager death leaves detached or separate-unit workers running, which the tests cover.
- **Freeze and vote.** The simulation is not ticked while a timeout is open, and `GameSim.frozen` also blocks input and actions. Ties go to Continue and No bot (`match-worker.gd:389-402`). A stage closes early when every remaining human has voted. Votes are accepted only for the current stage before its deadline, and leavers' votes are erased. Resume increments the teleport serial, which drops pre-pause input.
- **Leader succession.** `mark_vacant` removes the leaver from `order`, so `leader()` advances to the next human. Replacement bots are never re-added to `order` and cannot lead. The room name follows `_report()`.
- **Secrets.** No credential value is committed. The only 64-hex strings in the tree are SDK and trace checksums. Test keys are generated per run in temp directories. `multiplayer-server/.runtime`, `dist` and `node_modules` are ignored. Logs carry only room, generation and port, and Colyseus `debugMessage` is disabled (`server.ts:9`).
- **Pinning and licensing.** The SDK is pinned to 0.18.10 (`addons/colyseus/version.json`) with its MIT `LICENSE`. The Node packages are exact-pinned in `package.json` and the lockfile, and `.nvmrc` pins Node 26.13.0.

## Findings

### Critical

None found in the code.

### High

**H1. The shipping client's only transport, native SDK over WSS, has never worked in any test.** `multiplayer/lobby-client.gd:35` accepts only `wss://` in release builds. The only Godot-SDK WSS attempt failed with `TlsInitializationFailed` (phase 1 report). Phase 3's WSS smoke used the Node test client, and the native SDK check (`tests/directory-client-check.gd`) ran over plain `ws://`. Even so, phase 3 line 41 ("Connect clients to the built-in WSS address") is ticked.
- *Failure scenario:* A release client cannot complete TLS on macOS or Windows, perhaps because of the CA store or SNI. Every public player sees **Server offline**.
- *Fix:* Before any further client work, run an exported release client against a trusted-certificate WSS endpoint, then against an untrusted and a mismatched certificate (phase 1 step 2). Untick phase 3 line 41 until that passes.

### Medium

**M1. A failed or slow ownership probe tears down a healthy worker's control session.** `ownership()` turns any `systemctl`/`ps` error, including its 1 s `execFileSync` timeout, into `{busy: true}` with no PID (`worker-process.ts:58,89-90`). `tick()` treats that as a PID mismatch for an online slot: it destroys the socket, marks the slot `recovering` and fails every pending reservation (`worker-manager.ts:220-225`). Re-registration then fails while the probe keeps failing, because `hello` requires `ownership().pid === data.pid`.
- *Failure scenario:* On a loaded 4-vCPU VM, `systemctl show` stalls for more than 1 s during a D-Bus hiccup. A healthy waiting room drops to "Recovering" and its in-flight joins fail. This repeats for as long as systemd stays slow.
- *Fix:* Return a distinct `unknown` result on probe errors. Keep an authenticated, heartbeating session online when the result is `unknown`, and destroy it only on a positive mismatch (a different marker PID, or the unit inactive).

**M2. Synchronous process probes block the directory event loop.** `tick()` runs every 200 ms and calls `execFileSync` (`ps` or `systemctl`) at least once per non-free slot (`worker-manager.ts:57-61,220`), plus again at `:238`, in `hello`/`register`, and in `stopWorker`. I measured `ps -axww` at about 29 ms on this Mac. With three owned rooms that is about 15 probes per second, roughly 435 ms of every second with the WSS loop blocked in local mode. The service CPU figure in phase 5 misses this because the time is spent waiting on a child process.
- *Failure scenario:* Directory pushes and admission replies are delayed by up to one probe batch per tick. Under VM contention, heartbeat reads slip toward the 1.5 s socket and heartbeat deadlines and trigger M1-style flaps.
- *Fix:* Use async `execFile`, probe each slot about once per second (more often only for `starting` or stopping slots), and cache the result per tick.

**M3. The client↔directory protocol has no version check, so incompatible clients use up worker capacity.** `lobby-client.gd:106` sends no build or protocol value, and `lobby-room.ts` checks none. A build mismatch is found only at ENet `challenge` (`match-worker.gd:237`), after the directory has reserved a seat or spawned a whole worker. Phase 4's rollback line and phase 5's rehearsal both call for verifying "client-directory" versions, which the code does not do. The directory↔worker contract is also duplicated: the TypeScript literals `protocol 1` and `'island-1'` (`worker-manager.ts:143`) are kept separately from the GDScript constants.
- *Failure scenario:* After a release, players on the old client press Create. Each press occupies one of the three ports until the creator TTL expires, and the result is "Game build does not match the worker." Current-build players see **Server full**.
- *Fix:* Send `{protocol, clientBuild}` in `create`/`join` and reject a mismatch in the lobby before calling `manager.admit`. Generate the TypeScript constants from one shared source, or check them against `match-worker.gd` in the package script.

**M4. Anonymous seat and room hoarding has no limit.** Each WSS session gets a fresh identity (`lobby-room.ts:32`), and the only limits are per socket (`maxMessagesPerSecond = 10`, `maxClients = 256`). Neither nginx (`deploy/nginx.conf.example`) nor the lobby limits per IP. Separately, any ENet peer can hold one of the 8 spare transport slots for 5 s without a ticket (`TRANSPORT_PEERS = 13`).
- *Failure scenario:* One script opens a few WSS sessions and creates three rooms, then admits a headless client to each or keeps re-reserving seats every 15 s. With a cap of three rooms, the service is full for everyone else indefinitely.
- *Fix:* Add `limit_conn`/`limit_req` per client IP in nginx, forward `X-Forwarded-For`, and cap concurrent reservations or creates per IP in the lobby. If the product accepts this risk, record that decision.

**M5. Treating 0.9 s of input silence as a permanent departure risks false ejections.** `ACTIVE_IDLE_MS = 900` (`match-worker.gd:12,362-369`) turns any 0.9 s upstream gap into a vacancy, and the player cannot rejoin. The peer is then force-dropped (`main.gd:536`). Burst loss (a Wi-Fi roam, a cellular handover) and OS throttling of an occluded or minimized client (macOS App Nap timer coalescing) are untested. The impaired run used only independent 2.5 % loss, with a worst observed update gap of 262 ms.
- *Failure scenario:* A player alt-tabs on macOS or roams between access points. Their character becomes a vacancy, the crew is forced into a vote, and the player is out of the match.
- *Fix:* Measure with burst-loss and background-window tests. Consider freezing at 0.9 s but finalizing the departure later, for example only when ENet times out or after about 3 s, while still meeting "freeze within 1 s".

**M6. Several ticked boxes cover Linux supervision that has never run.** Phase 3 lines 39, 70 and 71 are ticked, but the systemd/polkit path was never executed (phase 3 and 5 reports). Untested Linux-only assumptions include: the `Job` property parse (`worker-process.ts:72-75`); `MainPID` keeping the marker after `process.execve` (`unit-worker.ts:10`); polkit `action.lookup('verb')` under `NoNewPrivileges=true`; `mv -Tf`/`flock` in `activate-release.sh`; and the worker unit's `TimeoutStopSec=4s` against Godot's SIGTERM handling.
- *Fix:* Untick those boxes, or mark them "template only", until the clean-host gate in phase 5 runs.

### Low

- **L1. Cancelling after a ticket arrives keeps the seat until TTL.** `_back_to_menu` clears only client state (`main.gd:376`). The worker supports `cancel`, but the directory never sends it. For a creator, this holds one of three ports hidden for about 15 s. *Fix:* Add a `cancel` WSS message keyed by request ID that forwards the worker `cancel`.
- **L2. The empty-room timer ignores pending reservations.** At `match-worker.gd:403`, a joiner who reserves at second 55 of an empty waiting room has the worker exit under them. *Fix:* Pause the 60 s timer while `reservations` is non-empty.
- **L3. Forced disconnects bypass SceneMultiplayer cleanup.** `multiplayer.multiplayer_peer.disconnect_peer(peer, true)` (`main.gd:536,539`) skips the `peer_disconnected` signal, so SceneMultiplayer's peer table may keep a ghost entry for every timed-out handshake. This is plausible but unverified. *Fix:* Use `multiplayer.disconnect_peer(peer)`.
- **L4. The spawn-failure callback targets the slot rather than the lease.** At `worker-manager.ts:294`, `() => this.closeSlot(slot)` could close a later lease if the `error` event arrives late. *Fix:* Capture the lease and check `slot.lease === lease` before closing.
- **L5. The public client export ships development entry points.** Both desktop presets use `export_filter="all_resources"` and exclude only `node_modules` (`export_presets.cfg:5-7`). The client pack therefore includes `multiplayer-server/dev-handshake.tscn`, `tests/*.gd` and the benchmark trace generator. Phase 1 says to remove the throwaway fixture before shipping, and that item is unticked. *Fix:* Add `tests/*`, `plans/*` and `multiplayer-server/*` to `exclude_filter`, or delete the fixture.
- **L6. The heartbeat margin is thin.** The worker reports every 1,000 ms against a 1,500 ms manager deadline and socket idle timeout. Any main-loop stall over about 500 ms, such as `world.rebuild` in `_to_lobby` or a GC pause, drops the control session and fails pending joins. *Fix:* Raise the manager deadline to about 2.5–3 s, or measure worst-case worker frame time on the VM.
- **L7. Plan metadata is stale.** `plan.md:4` still says `status: pending`, and line 32 says "All implementation tasks are pending". `addons/colyseus/plugin.cfg` says `0.18.0` while `version.json` says `0.18.10`, and the Linux debug mapping points at the release `.so` (upstream layout). Per-binary SHA-256 values are not recorded in the repo; only the archive hash is, in the phase 1 report.

## Unverified gates

These are open release gates. Ticked boxes or report wording should not be read as evidence for them.

1. **Native SDK WSS with certificate validation** (trusted, untrusted, hostname mismatch) from exported macOS arm64 and Windows x86-64 builds. Export templates are not installed, so no export of any kind has run. The release-build gating (`OS.is_debug_build()` branches) has also never run in a release binary.
2. **The Linux release on a clean host:** systemd manager plus `hold-til-dawn-worker@` units, polkit, `unit-worker` `execve`, collision and map parity of the worker PCK on the Linux template, and port release on a whole-service stop.
3. **Public TLS, DNS and firewall exposure:** nginx with a real certificate, renewal while matches play, the RFC 2136 cold-boot IP change, and confirmation that TCP 2567/2569 are not publicly reachable.
4. **Capacity on the 4 vCPU / 4 GB Singapore VM:** three five-human rooms, both network profiles, the five-player 60-enemy LAN divergence target, and a full-duration five-night match with human input.
5. **The acceptance scenario with real people** (Alice/Bob/Dana/Carol) on exported clients across separate networks.
6. **The drain and rollback rehearsal** with two matched release bundles on Linux, including the failed-activation restore path.
7. **Robustness against burst loss and background windows** for the 0.9 s departure rule (M5).
8. **Third-party notices** for the native SDK binaries' bundled dependencies, and a signing/notarization decision.

## Unresolved questions

- Is anonymous room and seat hoarding (M4) an accepted product risk for the three-room public test, or must the first public build include per-IP limits?
- Should a player who loses upstream traffic for 1–3 s be treated as departed (current behavior) or only frozen until ENet confirms the loss (M5)? The GDD's "freeze within 1 s" requirement does not settle this.

Status: DONE_WITH_CONCERNS
Summary: The local implementation meets the plan's admission, isolation, supervision, freeze/vote and succession rules, and the local suites pass apart from the pre-existing `sim_check` flake. It is not ship-ready, because native-SDK WSS, exported clients and the Linux/hosted deployment are unverified, and six Medium issues remain.
Concerns/Blockers: H1 (native-SDK WSS has never succeeded), M1 to M6, and the unverified gates above.
