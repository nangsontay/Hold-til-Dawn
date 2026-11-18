# Directory service, deployment, and ops fixes

The directory distinguishes unknown supervision from a known process owner. Ownership probes run asynchronously, share one result per slot, and run about once per second for stable slots and every 200 ms during launch or stop. Probe failure returns HTTP 503 and directory code `degraded`. An authenticated, heartbeating room stays online while ownership is unknown. Heartbeat and control socket deadlines are 2,800 ms against 1,000 ms worker reports. Spawn failures act only on the lease they started.

Create and Join require the matching protocol and client build before a seat or port can be reserved. WSS admission carries bounded `ttlMs`. Cancel forwards the original request ID and releases late reservations on the original generation. Start sends `room_started` to the original directory session and the invalidated ENet proof receives the same outcome.

Packaging checks the pinned Godot 4.7.2 build and the client and worker compatibility values. It records the verified values and includes checksums for the five Godot SDK binaries. The production package advertises only its start command. The worker export preset excludes the test and development fixture paths. The vendored `plugin.cfg` reports 0.18.0 and `version.json` identifies the pinned SDK as 0.18.10. The upstream plugin metadata is preserved.

Activation runs the pinned Node and Godot version checks as `htd`, then rehearses matched and mismatched directory peers while drained. An operator signal starts and reserves on all three workers through authenticated control. Activation requires an authenticated close acknowledgement and confirmed process exit and UDP release for each slot. A failed activation restores the preceding release with admissions drained and attempts the same rehearsal there if the restored release supports it.

## Verification

- `npm test --prefix multiplayer-server` passed all three tests in 123 seconds. The real Colyseus/ENet suite covered compatibility, cancellation before and after assignment, Start between challenge and proof, and the drained compatibility and authenticated worker rehearsal. A 1,600 ms control-only gap kept the established room online. A longer gap expired the control session and the worker re-registered while its ENet players stayed connected. The injected 550 ms probe error test observed the event loop while the probe was pending and kept the heartbeating session online through repeated errors.
- The hosted client suite passed against the source service and against a build-only package with the exported worker PCK on macOS arm64. The SDK emitted its known ObjectDB exit warning.
- The six-second clean release smoke passed with 15 ENet peers, three workers and 60 enemies per worker. Workers kept their PIDs across the directory restart. Worker death returned its five clients in 2,834 ms. A one-way UDP outage froze its crew in 921 ms and returned the isolated client in 2,192 ms.
- A deliberately failing hosted run stopped the test-owned manager and a bound detached worker after creating a real room. It forced the manager to hit its 15-second shutdown deadline. Both processes exited and all three UDP ports rebound.
- The offline build-only package passed checksum verification. The package command rejected an x86-64 ELF presented as a Godot template with mismatching build metadata. Shell syntax and the five SDK binary checksums passed.

## Docs changes required

- Update Phase 3 line 63 and its report to replace public `expiresAt` with `ttlMs`. Record compatibility and cancellation request fields, WSS `room_started`, and HTTP 503 / WSS `degraded`. In the worker-control schema, document `cancel {requestId}`, `reservation_cancelled {requestId, reason: "room_started"}`, the optional request ID on cancellation acknowledgements, and bounded `ttlMs` on successful reservation replies. The signed internal `reserve` command retains `expiresAt`.
- Mark Phase 3 lines 39, 70 and 71 as template or local evidence and keep their production checks open. Separate line 41's verified local directory publication and native SDK TLS fixture from the unverified built-in public endpoint. Leave the specific Linux rehearsal item at line 90 and both Phase 5 host checks open.
- Update the README activation procedure to require the pinned binary versions, compatible and incompatible loopback directory peers, and authenticated launches and exits on all three slots while drained. Rehearse two matched bundles and a failed activation on the target Linux host. Keep admissions drained if a restored release cannot perform these checks. Confirm the matched exported client against public WSS and ENet in staging before reopening with `drain.sh off`.
- Record anonymous room and seat hoarding as an accepted risk for the three-room test. Record the SDK metadata discrepancy and the five binary digests in `addons/colyseus/SHA256SUMS`.

## Unverified gates

Linux x86-64 Godot template execution and systemd activation remain open. This includes `Job` and `MainPID` ownership, `execve` marker visibility, polkit start and stop authorization, graceful and escalated stop, port release, and atomic switch and rollback with `flock` / `mv -Tf`. Public TLS and DNS, exported Windows and macOS clients, and the Singapore VM remain unverified.

Status: DONE_WITH_CONCERNS  
Summary: Side B fixes and all requested local verification passed.  
Concerns/Blockers: Linux supervision and rollback, public infrastructure, exported client runtimes, and the target host still require rehearsal.
