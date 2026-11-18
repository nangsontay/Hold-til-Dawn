# Phase 4 client migration

The Play flow is implemented and verified against a local Colyseus directory and managed Godot workers. Export and hosted verification remain open.

## Client behavior

Play opens the room directory. Create and Join send the protocol and client build and reserve a seat, then the assigned ENet worker checks the client build and room generation before admission. Server peer 1 stays the RPC authority at `/root/Main`. The first admitted human leads the crew. Cancelling or leaving sends WSS `cancel {requestId}`, releases the server-side reservation promptly, and clears client-side state; the same player can immediately retry the same room, and a cancelled Create frees its port for reuse. If a match starts while a ticket is outstanding, the client receives `room_started` and returns to the directory with "Match already running".

The crew shows each player’s role and Ready status and explains the start gate. The match shows both timeout vote stages, replacement bots, the resume countdown, and results. During a timeout the client sends neutral input for liveness. The worker acknowledges it independently of movement reconciliation. A new teleport serial on resume invalidates pre-pause input. The admission deadline is a monotonic countdown from the ticket's `ttlMs`, immune to a skewed client clock. Snapshot traffic and advancing input acknowledgements each have a five-second monotonic deadline.

The default directory address uses the deployment example `wss://game.example.com`. A launch argument `--lobby-url=<url>` overrides `LOBBY_URL`. A debug LAN launch can use `-- --lobby-url=ws://127.0.0.1:2567`. Release clients require WSS. Debug builds retain the direct server and solo/demo shortcuts. Scripted headless clients opt in with `--headless-client`. The Phase 4 client and worker use admission build `island-1-client-2` and protocol 1.

## Verification

- `python3 tests/hosted-client-integration.py` passed on macOS arm64 with Godot 4.7.2 and Node 26.13.0. The suite drove the actual Play and crew controls over local WS and ENet. It covered duplicate clicks, cancellation before and after ticket issuance with immediate retry from the same session, released seat capacity after a retry, a cancelled Create whose port was reused, Start between assignment and proof (terminal `room_started` notice over the directory), stale selections, full rooms, three-room capacity, offline Retry, an unreachable UDP port, invalid credentials, and denied mid-match joins. It also covered Ready resets after role and mode changes, movement correction and teleport reset, leader succession, both vote stages, changing a vote, bot replacement, an end-by-vote result, and return to the same crew. It verified that closing the directory socket leaves ENet running and that the client detects both a one-way input outage and missing snapshots. A stale absolute expiry injected into the ticket was ignored by the monotonic `ttlMs` deadline.
- `tests/match-worker-check.gd`, `tests/match-worker-integration.py`, and `tests/geometry_check.gd` passed. Worker coverage includes build mismatch, stale generation, the 20 Hz snapshot stream, the freeze deadline, and rejecting pre-pause input after resume. The integration fixture now waits for Bob’s admission before launching Dana so the succession assertion tests actual join order.
- The real ENet suite verified the pause-and-departure policy end to end: upstream loss paused the match at 895 ms, a short recoverable burst resumed the match with the player's seat and no vote, and sustained loss was finalized by ENet at 2,801 ms — inside the client's 4,800 ms watchdog within the 5-second liveness budget. The client retains its 4,800 ms liveness deadline for missing worker traffic, including during freezes.
- The hosted client test emitted an ObjectDB leak warning at exit. A separate three-connection reproduction using the pinned Colyseus SDK directly also emitted the warning. Phase 1 observed the same warning with the transport fixture.

## Open verification

- Windows and macOS exported clients, hosted WSS with certificate trust, and graphical cross-network play remain unverified. The local test exercised WS and the production ENet contract. The development kits lack matching desktop export templates, so no export of any kind has run.
- The production DNS name still needs provisioning. The client’s bundled default is the example address.
- The Phase 1 exported-handshake and fixture-removal gates remain open. The dev-handshake fixture is kept in source for those gates and is excluded from shipped client data by the export presets (`tests/*`, `plans/*`, `multiplayer-server/*`), which cover the fixture scene under `multiplayer-server/`.
