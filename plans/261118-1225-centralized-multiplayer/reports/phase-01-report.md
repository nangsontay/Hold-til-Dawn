# Phase 1 handoff

Status: In progress. The exported-client, WSS and local gameplay acceptance gates are not verified; do not treat the transport proof as a completed phase.

## Baseline

Revision `1e09ad55911cb31405e4599c1fdb3d8669714c47`, macOS arm64, Godot `4.7.2.stable.official.ed1daf0bf`. The first import exited 0 after importing 144 assets; the sandbox prevented Godot from saving user/editor settings and reading the macOS certificate store. A subsequent SDK import outside that restriction exited 0. `tests/geometry_check.gd` passed. `tests/sim_check.gd` initially failed `bots gather resources by day` and passed on a second run; investigate the intermittent failure. The dedicated host printed `Hold 'til Dawn dedicated server on UDP 7777` and was stopped. Two interactive clients, actual local-network play, and identical exported map/texture packaging remain unverified. The geometry check does verify two in-process geometry instances have identical scenery/colliders and use the imported map's landmark.

Source and README describe the first human as leader, roles including Engineer, an Engineer start requirement, manually added bots, and the movement and action controls. Existing simulation checks cover bots, movement validation, day-to-night transition and a result. Against MEC-24/27/29 the prototype immediately removes disconnected players, has no Ready state, and allows solo start; interactive leader succession, disconnect and result-to-crew behavior have not been exercised here.

## Pins and packaging

- Godot native SDK `godot-v0.18.10`, from https://github.com/colyseus/native-sdk/releases/tag/godot-v0.18.10. The downloaded archive SHA-256 is `d2841b3d3cd959edce60c6073ba4c321d79fb09f15afefe69f57d51204293fac`. The extracted subset contains macOS arm64 debug/release, Windows x86-64 debug/release and Linux x86-64 release (also mapped for debug, as in upstream). Intel macOS, Windows arm64 and Linux arm64 are not provided by this package. The SDK is beta.
- Node `26.13.0`; lockfile pins `@colyseus/core` 0.18.28, `@colyseus/schema` 5.0.49, `@colyseus/ws-transport` 0.18.5 and `express` 5.3.0, separate from the benchmark. `npm ci --prefix multiplayer-server` installs the locked package.
- `addons/colyseus/LICENSE` is the SDK's MIT license; include it in distributions. The npm lockfile lists MIT, Apache-2.0, ISC and BSD-3-Clause dependencies; collect and include applicable third-party notices from installed packages before redistribution. Native binary bundled dependencies and their notices still require upstream verification. The export presets `Development macOS arm64` and `Development Windows x86_64` are configured but untested because Godot 4.7.2 export templates are not installed. The Windows debug export was attempted and exited 1, reporting missing `4.7.2.stable/windows_debug_x86_64.exe` and `windows_release_x86_64.exe` templates. Signing and notarization are not configured.

## Local proof

With local loopback sockets allowed, `node multiplayer-server/server.js`, then `Godot --headless --path . res://multiplayer-server/dev-handshake.tscn -- --worker`, then `Godot --headless --path . res://multiplayer-server/dev-handshake.tscn` returned exit 0 on macOS arm64. Output confirmed a Colyseus `hello`/`hello_ack` protocol-1 reply over **WS**, an ENet action and snapshot, and a second action/snapshot after a clean lobby leave. Godot reported four ObjectDB instances leaked at exit. All started services were stopped. This is a development-only transport fixture, not an admission or game server, and must be removed before shipping.

A self-signed TLS endpoint at `wss://127.0.0.1:2568` produced `Lobby error 0: TlsInitializationFailed`; this does not establish certificate rejection or a successful WSS connection. WSS, certificate validation and WSS loss against an active exported match remain unverified.

## Next verification

1. Install matching Godot 4.7.2 export templates. Export debug builds with `godot --headless --path . --export-debug 'Development macOS arm64' /absolute/path/handshake.zip` and `godot --headless --path . --export-debug 'Development Windows x86_64' /absolute/path/handshake.exe`. Verify the SDK binaries and license are packaged and execute on their respective machines; correct presets if necessary. The regular entry point remains unchanged. Run the development scene explicitly using the exported executable's scene argument `res://multiplayer-server/dev-handshake.tscn`; confirm this invocation on each target, since it has not been tested in an export.
2. Run the fixture lobby behind a properly configured TLS reverse proxy that forwards HTTP matchmaking and WebSocket upgrades. Set `LOBBY_URL=wss://<trusted-host>` and `WORKER_HOST=<reachable-worker-host>` on each exported client; start the fixture worker with `--worker` and allow UDP 7778. Confirm both typed replies, both ENet snapshots, clean disconnect and continued ENet traffic after WSS leaves. Repeat with an untrusted certificate and a hostname mismatch; both must fail, while the trusted certificate succeeds. Test from separate networks and capture versions and results without credentials.
3. With the same imported build, run `godot --headless --path . -- --server` and two graphical clients against it. Exercise and record leader, roles, Engineer gate, bots, movement, actions, night transition, disconnect and result-to-crew; compare exported assets and geometry. Stop all test processes.
4. On a clean Linux x86-64 target install no SDK or editor, export a worker-only artifact without SDK code and run it; verify it loads the map and collision assets and accepts ENet. Whether a worker can omit the native extension remains unverified. Repeat for each Linux architecture actually selected for deployment.

## Configuration handoff

Implemented development names: `LOBBY_HOST` defaults to `127.0.0.1`, `LOBBY_PORT` to `2567`, `LOBBY_URL` to `ws://127.0.0.1:2567`, and `WORKER_HOST` to `127.0.0.1`. The fixture worker uses UDP 7778. Proposed production names, not yet implemented: `LOBBY_URL` for public DNS/WSS URL, `CONTROL_ENDPOINT` for a private loopback control endpoint, `MATCH_PORT_MIN=7778`, `MATCH_PORT_MAX=7780`, `WORKER_EXECUTABLE` for the exported Godot worker, and `ADMISSION_SECRET_FILE` for a protected credential source. No hosted DNS, control service, worker executable, credential source or production TLS configuration has been selected or verified.

## Unresolved questions

- Which desktop and server architectures will actually be shipped, and where will their third-party notices be distributed?
- What hosted DNS, certificate, TLS termination, worker executable and protected credential source will be used?

The import generated untracked `.gd.uid` files outside the permitted edit scope. Automatic approval review rejected their removal as outside that scope; leave them for the coordinator or user to inspect and clean up.
