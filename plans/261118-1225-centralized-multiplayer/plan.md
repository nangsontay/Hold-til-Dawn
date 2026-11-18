---
title: "Move multiplayer to a developer-hosted Colyseus lobby and Godot match servers"
description: "Players create or join a room from the game. One headless Godot worker runs each match over ENet on the developer’s server."
status: implemented
priority: P1
branch: main
tags: [multiplayer, backend, infra]
created: 2026-11-18
blockedBy: []
blocks: []
---
# Centralized multiplayer migration

Players open the game, choose **Create Room** or **Join Room**, and enter the existing crew screen. The creator controls the crew as its first human leader. The developer operates both the directory and all matches. Clients predict their own movement; the worker validates it and sends corrections. An active match pauses when someone leaves and the remaining humans vote on its outcome.

| Component | Owns |
|---|---|
| Godot desktop client | WSS connection to Colyseus for discovery and admission, then direct ENet connection to the assigned worker for crew and gameplay. |
| Colyseus service | Room directory, create/join requests, bounded worker allocation and delivery of short-lived admission credentials. The directory lists only developer-managed workers. |
| Headless Godot worker | One room generation, seat admission, human and bot roster, rules, validation of client-predicted movement, actions, match phase and snapshots. Its public endpoint belongs to the developer. |

## Implementation order

| Order | Phase | Exit evidence |
|---|---|---|
| 1 | [Prove the connection and freeze baseline behavior](phase-01-start.md) | Exported desktop clients can use the chosen lobby SDK and ENet in the same application. |
| 2 | [Prepare a supervised match worker](phase-02-match-worker.md) | Two workers run independently. A peer can play only after admission. |
| 3 | [Build the Colyseus directory and admission flow](phase-03-lobby-and-admission.md) | Real create, join, expiry, and concurrent final-seat requests give consistent results. |
| 4 | [Move the game menus to the hosted flow](phase-04-client-migration.md) | Two clients reach the existing crew and play through a creator departure. |
| 5 | [Verify and release the complete stack](phase-05-release-verification.md) | Exported clients complete cross-network playtests; capacity, cleanup and rollback pass. |

Each phase depends on the preceding one. All five phases are implemented and locally verified; recheck the owning files before each release gate. The exported-client, Linux/systemd, public TLS/DNS and Singapore VM gates are still open. The service and worker can run together locally using the same room and admission contract as the hosted build. Only endpoint, certificate trust, executable path and port configuration should differ.

## Release contract

- Preserve movement reconciliation, action delivery and 20 Hz snapshots. Implement the GDD crew rules missing from the prototype: 3–5 ready humans, an Engineer at start, human leadership succession, timeout votes and replacement bots. Keep solo and manually added demo bots in development routes.
- Close admission throughout the active match, including technical timeouts. Disconnected players cannot reclaim their character. The crew may play another match in the same room.
- The initial test target and GDD limit are three rooms. At capacity show **Server full** with Refresh; players may still join waiting rooms with seats. A capacity shortfall must be reported.
- Keep private debug playtest routes separate from the public client. Accounts, a rewrite of gameplay into TypeScript, and changes to snapshot encoding are outside this migration.

## Acceptance scenario

Alice creates a room. Bob and Dana join. All three mark Ready with an Engineer and start. Carol creates an independent room. Alice quits, Bob becomes leader, and the worker freezes their match within 1 s. Bob and Dana vote Continue and No bot; they resume after 3 s. Carol’s room is unaffected. An empty room closes after 60 s. Its port can be reused without accepting old credentials.

## Design authority

The [GDD](../../GDD_Assignment1.pdf) defines these flows in MEC-24/27/29 (PDF pp. 17–19), BAL-46/47/48 (p. 32), and TECH-NET (§7.3, pp. 66, 71–72). The chosen Colyseus/WSS lobby replaces the original ENet lobby on UDP 7777. Keep UDP 7778–7780 for Godot rooms. Retaining current snapshot RPCs is a migration decision; verify the GDD synchronization targets before considering another replication mechanism.

## Evidence

The current [network entry point](../../main.gd) combines server startup, client input and drawing. The [simulation](../../game.gd) and [bot rules](../../bots.gd) already work on a dedicated server. The [benchmark fixture](../261118-1143-multiplayer-benchmark/trace/README.md) is a stress baseline; it omits production state and client rendering. The [official integration guide](https://docs.colyseus.io/getting-started/godot) marks the GDScript extension beta. These are the reasons to verify exported builds before moving the menu.

## Plan validation

The user confirmed the three-room test target and Server full with Refresh. The local GDD settled the reconnect policy. Review covered admission, worker supervision, recovery-time port ownership, pre-admission data exposure, technical timeout timing and desktop export packaging. Protocol parity, VM capacity and network deadlines are implementation acceptance gates. The clean-import Godot baseline passed both existing test scripts.
