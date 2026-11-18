---
title: "Phase 2: Match worker"
status: completed
phase: 2
priority: P1
dependencies: [1]
---

# Phase 2: Match worker

## Context Links

- GDD MEC-24, MEC-27, MEC-29; BAL-46 through BAL-48; TECH-NET: `GDD_Assignment1.pdf`, physical pages 17-19, 32, 66, and 71-72.
- Existing ENet bootstrap and unrestricted RPC path: `main.gd:133-254`.
- Simulation membership/leader rules: `game.gd:124-200`.
- Existing bot insertion/removal: `bots.gd:20-50`.
- Dependency: Phase 1 must pin the public WSS endpoint, private control endpoint, fixed worker ports 7778-7780, executable, and secret source.
- Downstream consumer: [Phase 3: Lobby and admission](./phase-03-lobby-and-admission.md).

## Overview

- **Priority:** P1
- **Status:** Locally verified
- **Objective:** Convert the headless Godot server into one persistent, authoritative ENet room worker. The worker owns room lobby, admission, five atomic slots, gameplay, disconnect freeze/votes, and its lifecycle independently of the lobby directory.
- **Scope:** Managed launch/configuration, authenticated loopback control, secure admission, Ready/start rules, targeted replication, disconnect/vote behavior, worker re-registration, and headless tests. Public WSS/listing belongs to Phase 3.

## Key Insights

- `c_join`, `c_input`, and `c_action` currently trust any connected peer (`main.gd:217-232`), and snapshots broadcast to every ENet peer (`main.gd:247-254`). Pre-admission peers can therefore observe match state.
- `GameSim.add_player()` counts only current players (`game.gd:128-134`). Pending reservations and replacement bots require one serialized worker ledger to prevent a sixth slot.
- The GDD requires room workers to survive a lobby-server crash and report state every second. Manager failure must not terminate a healthy match.
- Production bots are replacements for disconnected active players, not lobby-fill controls. A replacement preserves the leaver's role, HP, position, inventory, and kit.

## Requirements

### Worker identity and control

- [x] Start in managed mode with immutable room ID, current leader-derived room name, worker generation, fixed UDP port (7778, 7779, or 7780), private control endpoint, and host recovery credential supplied outside argv.
- [x] Bind ENet to the assigned UDP port and connect to private control on loopback only. Fail closed before readiness on invalid config or binding/authentication failure.
- [x] Authenticate framed control traffic with HMAC-SHA-256, canonical input, monotonic counters, bounded frames, constant-time comparison, room ID, port, and generation.
- [x] Send a signed heartbeat/state report every 1 second, including `maxFrameMs` (the longest interval between worker control updates since the previous heartbeat), and also on report changes. If the directory disappears, keep ENet and simulation running, reconnect, authenticate a re-registration claim, then negotiate a fresh control session without resetting room state.
- [x] Never stop solely because the manager/control channel is unavailable. Stop only on an authenticated close command, 60 seconds continuously empty, unrecoverable worker failure, or operator/service shutdown.

### Atomic slots and secure admission

- [x] Maintain one worker-owned invariant: `admitted humans + pending reservations + replacement bots <= 5`. Every reserve, cancel, expire, admit, disconnect decision, and bot conversion runs on the Godot main thread.
- [x] Reserve the creator first and reject non-creator reservations until the creator is admitted. Creator identity comes from the reservation, not ENet connection order.
- [x] Accept new reservations only in `waiting`, never during `playing`, technical timeout, result, or manager recovery. There is no active-match rejoin.
- [x] Store each one-time ticket's ID, expiry, intended identity, creator flag, and secret in memory. Ticket material is delivered publicly only by Phase 3 over WSS.
- [x] Never send the ticket secret over ENet. Accept ticket ID plus client nonce, issue a fresh random challenge, and verify `HMAC(secret, version || room-id || worker-generation || ticket-id || peer-id || client-nonce || challenge)`.
- [x] Bind challenges to one peer, attempt, generation, and short expiry. Reject replay, wrong secret/generation/peer, cancelled/expired tickets, malformed payloads, and proof reuse without altering occupancy.
- [x] Admit exactly once, consume the reservation atomically, then add the authenticated peer to `GameSim`. If insertion fails, converge the ledger and report an explicit failure.
- [x] Gate every input/action on admitted membership. Send snapshots with `rpc_id()` only to admitted peers; never broadcast while an unauthenticated peer can connect.

### Room lobby and gameplay lifecycle

- [x] Room name is the current leader's name truncated to 16 characters. The creator is leader first; when a leader leaves in waiting state, the next admitted human becomes leader and the reported room name changes with them.
- [x] Track Ready per admitted human. Start is server-authorized only when all connected humans are Ready, there are 3-5 humans, and at least one human is Engineer.
- [x] Changing any player's role, or the leader changing mode, atomically clears every Ready. Reject Ready/start from unauthenticated peers, bots, non-leaders where leader authority applies, and stale peers.
- [x] Disable production add/remove-bot lobby actions. Bots can appear only through the active-disconnect vote.
- [x] When an admitted player stops sending input during active play, pause the simulation within 1 second: timers, enemies, projectiles, channels, and player actions do not advance. A departure is final only after ENet confirms the loss (disconnect/timeout); if the player's traffic returns first, resume with their seat and no vote.
- [x] Run vote step 1 `Continue / End` for at most 30 seconds. If Continue wins, run step 2 `Bot / No bot` for at most 30 seconds. Only remaining humans vote; majority of votes cast wins; tie/no vote means Continue and No bot.
- [x] Cover all players who leave during the same timeout. One Bot/No-bot outcome applies to each vacancy. With Bot, convert each preserved character in place; with No bot, drop inventory and remove it. If no human remains, end immediately.
- [x] After a Continue outcome, run a 3-second frozen resume countdown, then resume once. `End` ends with no winner. Reject duplicate votes and votes from bots/leavers.
- [x] No disconnected player can reclaim an active slot. Later admission remains closed until the match returns to waiting after its result.
- [x] After a match, return remaining humans to the room lobby, remove replacement bots, clear Ready, and report `waiting`; close after 60 seconds continuously empty.

## Related Code Files

| Action | File | Responsibility |
|---|---|---|
| Modify | `/Users/nangsontay/Hold-til-Dawn/main.gd` | Managed startup, admission RPCs, Ready/vote routing, RPC authorization, targeted snapshots, peer cleanup, and local-playtest compatibility. |
| Modify | `/Users/nangsontay/Hold-til-Dawn/game.gd` | Explicit admission/removal results, Ready/start validation, full simulation freeze, vacancy resolution, post-match waiting reset, and replacement-state preservation. |
| Modify | `/Users/nangsontay/Hold-til-Dawn/bots.gd` | Remove production lobby-fill entry points and initialize replacement behavior from the disconnected character without resetting state. |
| Create | `/Users/nangsontay/Hold-til-Dawn/match-worker.gd` | Managed config, authenticated control/re-registration, heartbeat, slot ledger, ticket/challenge state, lifecycle coordinator, and empty-room timer. |
| Create | `/Users/nangsontay/Hold-til-Dawn/multiplayer/worker-control.schema.json` | Versioned commands/events, canonical signing input, limits, recovery handshake, heartbeat fields, and error codes consumed unchanged by Phase 3. |
| Create | `/Users/nangsontay/Hold-til-Dawn/tests/match-worker-check.gd` | Deterministic ledger, admission, Ready, freeze/vote, heartbeat, recovery, and lifecycle checks. |
| Create | `/Users/nangsontay/Hold-til-Dawn/tests/admission-client.gd` | Real ENet client harness for positive and negative admission integration cases. |

Phase 2 owns every file above. Phase 3 consumes the schema and harness without editing them.

- [x] Use a bounded pre-admission handshake timeout and bound connection attempts. Check protocol and map/content version at admission. On Start cancel outstanding reservations atomically and surface a room-started result. Record initial seed in the new match snapshot.
- [x] Make private control address ownership explicit: the directory listens on loopback, workers connect out and reconnect after restart. Send changes immediately and refresh the directory every second.
- [x] Compare replacement bot behavior against BAL-47, including population scaling, as specified in the final release tests.

## Test Matrix

| Level | Cases | Mechanical pass condition |
|---|---|---|
| Unit | Five mixed pending/admitted/replacement slots; sixth fails; expiry/cancel frees one; request replay is idempotent | `godot --headless --path . --script tests/match-worker-check.gd` exits 0 |
| Admission security | Wrong control MAC/counter/generation; wrong ticket secret; copied/replayed/expired proof; proof on another peer | Every attempt is rejected and occupancy remains unchanged |
| Lobby rules | Creator first; 2/3/5 humans; Ready toggles; role/mode reset; no Engineer; non-leader start/mode; production add-bot | Only 3-5 all-Ready humans with an Engineer start; invalid actions do nothing |
| Leakage | Unadmitted real ENet peer sends actions and waits for snapshots | No simulation mutation and zero snapshots before admission |
| Disconnect | One/multiple active leavers; step timeouts; ties/no votes; End; Bot/No bot; final human leaves | Freeze begins within 1 second; decisions and 30/30/3 timers match BAL-46; no ghost slots |
| Recovery | Kill/restart manager while worker plays; re-register; attempt stale-generation reservation | Simulation continues; state/timers do not reset; directory can reconstruct; stale request fails |
| Lifecycle | Post-match lobby, zero humans, human returns before 60 seconds, 60 seconds empty | Bots/Ready clear; timer resets on admission; worker exits only after continuous 60 seconds |
| Regression | Existing simulation and local playtest flows | Existing simulation/import/autohost checks remain green |

## Rollback

Stop admitting new rooms, let or deliberately stop existing workers, then revert the managed entry path and worker files while retaining the existing direct `--server` mode. No persistent data migration exists. Drain live workers before switching to an incompatible control protocol.

## Next Steps

Phase 3 may begin after signing vectors, creator-first admission, targeted snapshots, Ready/start checks, disconnect freeze/votes, and manager-loss recovery pass. Phase 3 must project worker state rather than duplicate capacity or match authority.

Local evidence and remaining release checks are in the [Phase 2 report](reports/phase-02-report.md).
