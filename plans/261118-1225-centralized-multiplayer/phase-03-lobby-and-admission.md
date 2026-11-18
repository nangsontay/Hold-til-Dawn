---
title: "Phase 3: Lobby and admission"
status: pending
phase: 3
priority: P1
dependencies: [2]
---

# Phase 3: Lobby and admission

## Context Links

- GDD MEC-24, MEC-27, MEC-29; BAL-46 through BAL-48; TECH-NET: `GDD_Assignment1.pdf`, physical pages 17-19, 32, 66, and 71-72.
- Dependency: [Phase 2: Match worker](./phase-02-match-worker.md), especially `multiplayer/worker-control.schema.json` and `tests/admission-client.gd`.
- Existing leader semantics after admission: `game.gd:124-134`.
- Use a custom directory room with the 0.18-compatible versions verified in phase 1. The built-in LobbyRoom lists Colyseus matchmaking rooms. External Godot workers need an explicit registry. [Official LobbyRoom guide](https://docs.colyseus.io/matchmaker/lobby).

## Overview

- **Priority:** P1
- **Status:** Todo
- **Objective:** Add one developer-operated Node/TypeScript Colyseus directory that serves public WSS, manages the fixed three-port roster, spawns one Godot ENet worker per room, recovers surviving workers after a directory crash, and delivers one-time admission material securely.
- **Scope:** Public room directory, create/list/join, creator-first orchestration, fixed port ownership, worker recovery, WSS ticket delivery, service setup, and real-process tests. Gameplay authority, Ready, disconnect freeze/votes, and empty-room shutdown stay in the worker.
- **Non-goals:** Multiple directory hosts, persistent room database, active-match rejoin, automatic matchmaking, accounts, private rooms, and gameplay relay.

## Key Insights

- Colyseus WSS is the trusted delivery path for the ticket secret. ENet carries only Phase 2's challenge-bound proof.
- WSS presence and ENet match membership are independent. A lobby socket drop must not remove an admitted ENet player or stop a match.
- The worker is authoritative for capacity and state. The directory caches only a signed projection for display and routing.
- The GDD requires one worker per room on UDP 7778-7780, at most three rooms, 1-second state reports, waiting-only joins, a fourth Create refusal, and 60-second empty closure.
- Recovery is not fresh-start: workers survive manager failure, re-register with authentication, and rebuild the directory before any port is considered free.

## Requirements

### Public directory

- [ ] Create a dedicated Node/TypeScript server directory with one production entry point, one Colyseus lobby-room type, and a single in-process worker manager per host.
- [ ] Terminate TLS at the configured reverse proxy and expose public HTTPS/WSS plus UDP 7778-7780. Bind worker control to loopback only.
- [ ] Connect clients to the built-in WSS address, publish secret-free room entries `{roomId, name, humans, capacity, mode, state}`, and push changes within 1 second.
- [ ] Room name equals the worker-reported current leader name, capped at 16 characters. Update the listing when leadership passes.
- [ ] Permit Join only when the worker reports `waiting` and an available slot. Full/playing/recovering/stale rooms are visible with Join disabled, not silently joinable by ID.
- [ ] When all UDP ports are owned or reserved, refuse a fourth Create with exactly `Server full`, then force/return a refreshed room list.

### Fixed worker roster and recovery

- [ ] Model exactly three port slots: UDP 7778, 7779, and 7780. Each is `recovering`, `free`, `starting`, or `owned(roomId, generation, pid)`.
- [ ] At manager startup, mark all three `recovering`, listen for signed worker re-registration, and disable Create until the roster is reconciled. Never assume empty in-memory state means a port is free.
- [ ] Workers send signed heartbeats every 1 second. Authenticate re-registration with a host recovery credential plus manager challenge, room ID, generation, fixed port, process identity, heartbeat sequence, and fresh nonces; rotate to a fresh control session key after success.
- [ ] Accept only one live identity per port/generation. Reject stale generations, replayed heartbeats, duplicate room IDs, mismatched process/port claims, and unsigned state.
- [ ] Before declaring an unregistered port free, observe the recovery window and verify no worker owns/binds it. A late claimant must not collide with a newly spawned worker.
- [ ] On manager crash/restart, preserve worker processes and ENet matches. Rebuild listings from authenticated reports, including waiting rooms; active matches remain non-joinable.
- [ ] On worker heartbeat/control loss, mark the room unavailable immediately, retain its port lease while ownership is uncertain, and reconcile rather than spawning over it.
- [ ] On confirmed worker exit, remove its listing and release its fixed port. The worker itself owns the 60-second continuous-empty close timer.

### Create, reserve, and admission

- [ ] Create chooses only a reconciled-free fixed port, derives room name from the requesting leader name (max 16), generates a fresh room ID/generation/control session, and spawns one non-duplicate Godot worker.
- [ ] Keep the new room hidden while starting. Wait for authenticated readiness, reserve the creator first, and send the creator ticket only to that creator's WSS connection.
- [ ] Do not list the room or accept later reservations until a signed `creator_admitted` event arrives. On spawn/readiness/reservation failure, terminate only that incomplete worker and release its port after exit.
- [ ] Join validates the latest worker generation and `waiting` state, asks that worker to reserve atomically, and issues no ticket until the worker accepts.
- [ ] Send `{roomId, generation, udpEndpoint, ticketId, secret, expiresAt}` only as a direct WSS response to the requesting client. Never place it in synchronized state, listings, URLs, query strings, logs, metrics, or errors.
- [ ] Treat worker rejection, timeout, stale generation, and state change as normal explicit errors followed by list refresh. Never optimistically decrement capacity or issue a local ticket.
- [ ] No rejoin is offered during an active match. The next Join is permitted only after the same worker reports `waiting` after match completion.

### Independent lifetimes

- [ ] A client WSS drop/reconnect affects only directory UX and undelivered pending reservations. It never removes an admitted ENet player, triggers the technical timeout, or stops a worker.
- [ ] A manager crash does not send a parent-death signal or service-group kill to workers. Workers continue and re-register with the restarted manager.
- [ ] Configure service supervision so `Restart=on-failure` restarts the Node manager while preserving spawned workers on manager failure. A normal operator stop must first issue authenticated graceful shutdown to workers and wait/escalate deliberately.
- [ ] Keep lifecycle semantics testable: manager crash preserves workers; explicit whole-service shutdown stops them; machine shutdown may terminate all processes.

## Related Code Files

| Action | File | Responsibility |
|---|---|---|
| Create | `/Users/nangsontay/Hold-til-Dawn/multiplayer-server/package.json` | Pinned scripts and runtime/dev dependencies. |
| Create | `/Users/nangsontay/Hold-til-Dawn/multiplayer-server/tsconfig.json` | Strict Node TypeScript build configuration. |
| Create | `/Users/nangsontay/Hold-til-Dawn/multiplayer-server/src/server.ts` | HTTPS/WSS bootstrap, lobby registration, readiness, graceful whole-service shutdown. |
| Create | `/Users/nangsontay/Hold-til-Dawn/multiplayer-server/src/lobby-room.ts` | Colyseus list/create/join messages and direct secret-free/secret-bearing response separation. |
| Create | `/Users/nangsontay/Hold-til-Dawn/multiplayer-server/src/worker-manager.ts` | Fixed roster, spawn, recovery, authenticated control, heartbeat reconciliation, and port release. |
| Create | `/Users/nangsontay/Hold-til-Dawn/multiplayer-server/src/config.ts` | Validated WSS/proxy, fixed ports, Godot path, timeouts, and redacted secret config. |
| Create | `/Users/nangsontay/Hold-til-Dawn/multiplayer-server/test/manager.integration.test.ts` | Real manager, real worker, Colyseus client, crash recovery, and admission tests. |
| Create | `/Users/nangsontay/Hold-til-Dawn/deploy/hold-til-dawn-multiplayer.service` | Manager restart policy that preserves workers on manager failure and supports explicit whole-service stop. |
| Modify | `/Users/nangsontay/Hold-til-Dawn/.gitignore` | Ignore Node output and local runtime/secret files, not lockfiles. |

Phase 3 owns only these files. It consumes but does not edit Phase 2 files.

- [ ] Run the Node directory and the three allowed worker instances as separate service units on the Linux host. Key each worker unit by fixed port; its service identity exclusively owns the slot even while starting or paused before UDP bind. The manager can start only those units. A directory restart leaves worker units running; a deliberate whole-service stop drains rooms and explicitly stops each owned unit. A LAN test launcher may supervise the same executables locally.
- [ ] Test a delayed worker before bind across a manager restart. An existing or activating unit must never be replaced until its exit is confirmed. Also test creator WSS loss just before/after ticket delivery: hold only through TTL, then close an unpublished worker that never admitted its creator. Release the port after confirmed exit.
- [ ] Scope idempotent Create/Join request IDs to the caller and operation. Duplicate clicks or retries return the prior live reservation or its terminal result. Send actual free-seat availability and room-creation availability even while a provisional room is hidden. Only the intended session receives its ticket. On worker death invalidate pending admissions before a new generation can occupy that slot.

## Test Matrix

| Level | Cases | Mechanical pass condition |
|---|---|---|
| Unit | Config/redaction, fixed roster states, generation/sequence rejection, listing projection, direct-vs-public payload separation | TypeScript test command exits 0 and output contains no secret values |
| Create/join | Create real room; creator first; join waiting room; fill five slots; attempt active join | Tickets are worker-approved; creator leads; full/active requests fail and refresh list |
| Three-room limit | Create workers on 7778, 7779, 7780; request fourth | Fourth response is `Server full`; list refresh shows three rooms; no fourth process/port exists |
| WSS lifecycle | Drop before ticket use, reconnect directory, drop after ENet admission, zero WSS clients during play | Pending follows worker TTL; admitted player/match stays alive; no technical timeout originates from WSS |
| Recovery | Crash only Node manager with waiting and active workers, restart, authenticate heartbeats | Old worker PIDs and simulations survive; roster/list rebuilds; active room rejects Join; waiting room accepts Join |
| Collision safety | Delay re-registration, replay old registration, claim wrong port/generation, heartbeat gap | No port is reallocated while ownership is uncertain; invalid claims fail closed |
| Empty lifecycle | Last waiting human leaves, returns before 60 seconds; repeat and stay empty | First timer cancels; second worker exits at 60 seconds; listing/port clear after exit |
| Exposure | Probe WSS, UDP range, control endpoints, logs, listing/message payloads | Only intended endpoints respond; no secret/proof/control credential is exposed |

## Rollback

Disable new Create/Join, allow or deliberately stop live workers, then stop the WSS directory and remove its proxy/firewall route. Restore the last matched hosted release after drain. Keep the existing direct Godot path available only for an isolated developmental comparison. Drain surviving workers before switching to an incompatible control protocol. No database rollback exists.

## Next Steps

Phase 4 may migrate the player client only after real-process tests prove creator-first ordering, waiting-only admission, the three-room limit, WSS/ENet lifetime separation, and manager-crash recovery. Deployment docs must record WSS endpoint, UDP 7778-7780, recovery health, logs, and graceful whole-service stop without recording secrets.
