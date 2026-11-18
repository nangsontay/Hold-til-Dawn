# Godot client and worker fixes

The directory client now sends the protocol and client build, cancels by request ID through admission, and uses the ticket's bounded monotonic TTL. A worker releases cancelled seats and exits after a cancelled unpublished Create. Start reports invalidated tickets over both WSS and ENet. Degraded readiness shows Server offline with Retry.

The worker pauses after 900 ms of missing input and starts a departure vote only on ENet peer disconnect. Traffic recovery resumes the crew with its seats and no vote. ENet uses a 200 ms ping interval, a retransmission factor of 8, and minimum and maximum timeout thresholds of 1,500 and 3,000 ms. The client retains its 4,800 ms watchdog to leave polling time within the 5-second budget. Gather, Revive and Enter retain progress until fresh input arrives after resume. A replacement bot reselects a node when the carried resource fills. An outstanding reservation pauses the 60-second empty timer.

The worker still emits signed heartbeats every 1,000 ms and on report changes. Each heartbeat includes `maxFrameMs`, the longest interval between worker control updates since the previous heartbeat. The manager's heartbeat and socket timeout is 2,800 ms. The metric is reported when the worker next runs.

## Verification

- Godot 4.7.2 on macOS arm64 passed `tests/geometry_check.gd` and `tests/match-worker-check.gd`. The worker check reported zero failures.
- The hosted client runner passed immediate cancellation retries, cancelled Create port reuse, Start before proof, stale absolute expiry, and both client liveness cases. The pinned SDK still warns about leaked ObjectDB instances at exit.
- The real ENet suite passed, including the 60-second empty shutdown. It measured a pause at 895 ms and departure confirmation at 2,801 ms after sustained upstream loss. A shorter recoverable burst resumed without a vote. The fixture also checked raw pre-admission snapshot counts, unchanged authoritative room state, expired peer removal from SceneMultiplayer, and reporting during a controlled 700 ms worker stall.
- Both macOS and Windows data ZIP export presets excluded `tests/`, `plans/` and `multiplayer-server/`, including the dev handshake fixture. Both contained the game entry point and SDK extension.
- The native SDK reached the production directory room through a loopback TLS listener when the temporary CA was set in Godot's `network/tls/certificate_bundle_override` before the extension loaded. The same endpoint rejected an untrusted CA and a hostname mismatch with `TlsInitializationFailed`. A runtime setting change came too late for the SDK. The test kept the system trust store intact.

## Requests to the other side

- Apply `exclude_filter="tests/*,plans/*,multiplayer-server/*"` in `multiplayer-server/deploy/worker-export.cfg`. That preset is copied over the root presets during packaging. The current worker staging copy already omits all three directories.
- Keep the 2,800 ms manager heartbeat and socket deadline against the worker's 1,000 ms reporting interval. The new `maxFrameMs` field is available for future target-host measurements.

## Docs changes required

- Update the Phase 2 report and control reference with WSS `cancel {requestId}`, control cancellation by request ID, bounded monotonic `ttlMs`, `reservation_cancelled {requestId, reason: "room_started"}` and the ENet `room_started` terminal result. The worker's signed `reserve` command still uses `expiresAt` between the directory and worker. Record `maxFrameMs`, the one-second heartbeat, the ENet timeouts, temporary connection pause, and transport-confirmed departure. State that the integration test counts raw snapshot arrivals and checks authoritative room state.
- Update Phase 3's direct WSS assignment checkbox and report to use `ttlMs` in place of `expiresAt`. Split the checked built-in WSS address requirement from the locally verified directory subscription and leave the public built-in endpoint open until real public verification. Record the repeatable local test command `python3 tests/match-worker-wss.py /path/to/Godot` after importing the project and installing the service dependencies. The test uses a temporary CA configured in the project before GDExtension initialization.
- Update Phase 4's cancellation, timeout and liveness evidence with immediate retry, released Create capacity, and the recoverable pause. Keep the Phase 1 exported handshake and fixture-removal gate open. The fixture remains available in source and is excluded from shipped client data.
- Keep Phase 5's clean Linux, systemd, public TLS/DNS, Singapore VM capacity and exported Windows/macOS runtime gates open. The development kits lack matching desktop export templates. The target VM, App Nap and cross-network clients were not exercised.

Status: DONE_WITH_CONCERNS  
Summary: Side A implemented the shared admission contract and passed the local Godot, hosted client, ENet and WSS checks.  
Concerns/Blockers: The worker preset is owned by Side B. Platform and public release gates remain open.
