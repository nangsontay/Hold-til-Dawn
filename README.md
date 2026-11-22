# Hold 'til Dawn (prototype)

Godot 4.7.2. Rules from the GDD live in `game.gd` (server only); `main.gd` is network, input, drawing and HUD.

## Play

Open the desktop build, set your callsign and role, then choose **Play**. Create a room or join a waiting room with an open seat. The creator leads the crew. Every human marks Ready and at least one chooses Engineer before the leader can start; there is no minimum crew size (up to 5). When all three rooms are occupied, use **Refresh** to check for openings or join a waiting crew with space.

Esc leaves the room. During a match, a departure pauses play and transfers leadership to the next human if the leader left. The remaining crew votes to continue or end, then whether to assign replacements. After a result, the crew can play again in the same room.

### Development and LAN

Install Godot 4.7.2 and Node 26.13.0. From the repository root, import the project, install the locked dependencies and build the service.

```sh
python3 tools/fetch-colyseus-sdk.py
godot --headless --path . --import
npm ci --prefix multiplayer-server
npm run build --prefix multiplayer-server
```

Run the local service in its own terminal. It owns the temporary worker processes and closes them on Ctrl-C. Use a fresh directory for an independent rehearsal. Keep the same directory and key across directory-only restarts.

```sh
run_dir=$(mktemp -d)
(umask 077; openssl rand -hex 32 > "$run_dir/worker.key")
env WORKER_EXECUTABLE="$(command -v godot)" WORKER_PROJECT="$PWD"   ADMISSION_SECRET_FILE="$run_dir/worker.key" WORKER_STATE_DIR="$run_dir/workers"   npm start --prefix multiplayer-server
```

Launch a client with `godot --path . -- --lobby-url=ws://127.0.0.1:2567`. The argument takes precedence over `LOBBY_URL`. For a private LAN, set `WORKER_HOST` on the service to its LAN IP, use a LAN-only HTTP/WebSocket reverse proxy for loopback TCP 2567, and pass `--lobby-url=ws://<LAN-proxy-address>` to debug clients. Allow UDP 7778–7780 from the LAN. An exported release client requires WSS. The direct debug server uses `godot --headless --path . -- --server` on UDP 7777. Run a local debug client with `godot --path . -- --autojoin` to connect directly to that server.

Run `python3 tests/release-verification.py` for clean and impaired UDP load. It starts a real directory, three workers and fifteen headless Godot client peers, then releases the processes and ports. Use `--mode clean` or `--mode lossy` to run one network. `GODOT_BIN` overrides the Godot executable. Pair it with `npm test --prefix multiplayer-server`, `python3 tests/hosted-client-integration.py`, `python3 tests/match-worker-integration.py "$(command -v godot)"`, and the simulation and geometry checks below.

## Operate the hosted service

### Build a matched release

Use one source revision, the Godot 4.7.2 editor and matching export templates, and the pinned Node 26.13.0 Linux x64 distribution. In the command below, supply the paths to the extracted `linux_release.x86_64` export template and the extracted Linux Node distribution. The script imports assets in a staging project and generates the worker PCK. The headless executable loads the PCK beside it. The worker pack omits the client native SDK binary. Desktop exports include the SDK and a baked-in directory hostname. Keep the `RELEASE` manifest and previous matched artifacts for rollback. Verify upstream export-template and SDK binary notices and platform signing requirements before distributing desktop builds.

```sh
PACKAGE_DESKTOP=1 multiplayer-server/deploy/package-release.sh   /absolute/output/0.1.0 0.1.0 play.example.net   /absolute/templates/linux_release.x86_64 /absolute/node-v26.13.0-linux-x64
```

With three arguments the script builds the service and worker PCK for local inspection. Omit `PACKAGE_DESKTOP=1` if the desktop export templates are unavailable. For a local smoke test of the packaged service and worker PCK, run `python3 tests/hosted-client-integration.py --release /absolute/output/0.1.0`. Validate the Windows and macOS release exports on their target machines.

### Provision the Singapore VM

1. Use a Linux x86-64 VM with 4 vCPU and 4 GB RAM. Create the system account and group `htd`, then create `/var/lib/hold-til-dawn/workers` owned by `htd:htd` with mode 0700. Copy the release into `/opt/hold-til-dawn-releases/<release-id>` and initially point `/opt/hold-til-dawn` at it with an absolute symlink. Keep the releases owned by root.
2. Install the directory, worker, DDNS service and DDNS timer from the release's [deployment templates](multiplayer-server/deploy/) in `/etc/systemd/system/`. Install `50-hold-til-dawn.rules` in `/etc/polkit-1/rules.d/`. Copy the release's `multiplayer.conf.example` to `/etc/hold-til-dawn/multiplayer.conf` and verify both public endpoints use the same hostname. Generate the worker key on the host with `openssl rand -hex 32` redirected to the configured file using a 077 umask. Set ownership to `htd:htd` and mode 0600. Keep it and the worker state directory across restarts and releases.
3. Delegate the public name to a DNS provider supporting RFC 2136 updates. Provision a TSIG key with permission to update only that hostname's A record. Install `nsupdate` (usually `bind9-dnsutils`), `curl` and Python 3. Copy the release's `ddns.conf.example` to `/etc/hold-til-dawn/ddns.conf`. Set the zone, authoritative update server, and TSIG key file. Keep that file root-owned with mode 0600. Use a VM public IPv4 that matches the HTTPS IP discovery response. Remove a stale AAAA record unless you provision IPv6 separately. Run `systemctl daemon-reload`, `systemctl enable --now hold-til-dawn-dns.timer` and `systemctl start hold-til-dawn-dns.service`. Verify the authoritative answer with `dig @<authoritative-server> <game-hostname> A +short`.
4. Allow inbound TCP 443 and UDP 7778–7780. For HTTP-01 certificate issuance and renewal allow TCP 80. Keep the service HTTP listener TCP 2567, the worker management listener TCP 2569 and the development UDP 7777 private. Before enabling the TLS vhost, obtain the first certificate with `certbot certonly --standalone -d <game-hostname>` while TCP 80 is free. Then install the release's `nginx.conf.example` in the nginx HTTP configuration and create `/var/www/letsencrypt`. Validate with `nginx -t`. The proxy must forward `/health`, `POST /matchmake/joinOrCreate/directory`, and WebSocket upgrades at `/<processId>/<roomId>`.
5. Enable nginx and `hold-til-dawn-multiplayer.service`. Check `curl -fsS http://127.0.0.1:2567/health` on the VM and `curl -fsS https://<game-hostname>/health` from another network. A healthy service returns HTTP 200 including when full or draining. HTTP 503 means the directory is reconciling worker ownership or stopping.

### DNS and certificate rehearsal

Power down the VM after draining, obtain a new public IPv4, and boot. Watch `journalctl -u hold-til-dawn-dns.service`. Compare the authoritative and a public resolver's A records before and after boot. Honor the old record's TTL. Use an already exported client on an external network to open Play and join a match over the same hostname. Check a valid certificate, then verify the client refuses an untrusted certificate and a trusted certificate for a different name on isolated test endpoints.

Keep TCP 80 and the nginx challenge path available for HTTP-01. Reconfigure the original standalone certificate for ongoing webroot renewal while nginx is running. Run `certbot reconfigure --cert-name <game-hostname> --webroot-path /var/www/letsencrypt --authenticator webroot`, then `certbot renew --dry-run`. Set the Certbot deploy hook to `/opt/hold-til-dawn/deploy/reload-certificate.sh` and confirm the Certbot timer is enabled. After a renewal, check the served chain and expiry from another network and confirm that active matches continue through the nginx reload.

### Recovery, drain and rollback

- Read directory logs with `journalctl -u hold-til-dawn-multiplayer.service` and worker logs with `journalctl -u hold-til-dawn-worker@7778.service` (or 7779/7780). Allocation and release log lines include room ID, worker generation and UDP port. Keep admission credentials and TSIG material private.
- For a compatible directory-only restart, run `systemctl kill --kill-whom=main --signal=HUP hold-til-dawn-multiplayer.service`. Recheck health and confirm the existing rooms return with the same worker PIDs and occupancy. ENet matches continue while the directory restarts.
- For an incompatible release, run `/opt/hold-til-dawn/deploy/drain.sh on` as an operator. Confirm health shows `"draining":true`. Existing crews can finish their match. Previously issued tickets can settle or expire. Ask waiting crews and crews at results to leave. `drain.sh wait` waits for all three leases to clear, including the 60-second empty-room timer. If you changed the ports or state path, pass the matching `LOBBY_PORT` and `WORKER_STATE_DIR` to the operator scripts.
- Rehearse by placing the new and preceding matched bundles under `/opt/hold-til-dawn-releases/`. With the drained flag still set, run `/opt/hold-til-dawn/deploy/activate-release.sh <release-id>`. The script verifies the pinned versions (Node `v26.13.0`, Godot `4.7.2.stable.official.ed1daf0bf`, and the package manifest checksums), switches the symlink with `flock`/`mv -Tf` and starts the directory. Rehearsal requires both a compatible and an incompatible loopback directory peer to be rejected correctly, and an authenticated worker launch and close-acknowledged exit on all three slots while drained. It restores the preceding target if health or rehearsal fails; if the restored release cannot complete the same rehearsal, keep admissions drained until it is verified manually. Confirm the service versions and manifest, then use an isolated staging instance with admissions open to test the matched client against the directory and worker, plus signed worker registration. Reopen production admissions with `drain.sh off` only after confirming the matched exported client over public WSS and ENet in staging. To roll back, repeat the drain and activate the previous release ID. Distribute the corresponding client builds together. Rehearse this drain-and-rollback, including a deliberately failed activation, on the target Linux host; it has not run there yet.
- Before a planned VM shutdown, run `drain.sh on` and then `drain.sh shutdown`. Confirm the three worker units are inactive and UDP 7778–7780 are free before powering down. The drain flag persists across cold boots. Run `drain.sh off` after DNS, health and the release have been checked. An ordinary service stop sends authenticated close to existing workers immediately and is appropriate when you intend to end those rooms.

### Accepted risks and decisions for the three-room test

- **Anonymous room and seat hoarding is an accepted risk (user decision).** The lobby has no per-IP limits — neither nginx (`limit_conn`/`limit_req`) nor the directory applies them; only per-socket caps exist (10 messages/second, 256 clients). A few scripted sessions can occupy all three rooms or hold seats indefinitely. No code change is planned for the three-room test; add per-IP limits only if abuse occurs.
- **Departure policy (user decision).** Lost upstream traffic freezes the affected match within 1 s (after 900 ms of input silence), but the departure is final only once ENet confirms the loss through its own disconnect/timeout. If the player's traffic returns first, the crew resumes with the same seats and no vote. A player whose connection stays up but who sends no input for 12 s is removed as a departure, so the crew gets the same vote. The client retains its 4,800 ms liveness watchdog inside the 5-second budget.
- **Native-SDK WSS has only been proven locally**, through a temporary CA configured in the project's `network/tls/certificate_bundle_override` (repeatable check: `python3 tests/match-worker-wss.py /path/to/Godot`, run after importing the project and building the service; the CA must be set before the GDExtension loads). It has not run against public certificates or exported clients.
- The vendored `addons/colyseus/plugin.cfg` reports version `0.18.0` while `addons/colyseus/version.json` pins the SDK at `0.18.10`; both are upstream layout, preserved as-is. The five SDK binaries are fetched by `tools/fetch-colyseus-sdk.py`, not committed; their SHA-256 digests are recorded in `addons/colyseus/SHA256SUMS` and verified by the script and during packaging.

## Controls
WASD move, mouse aim, LMB attack, E (hold) gather / repair door (Engineer) / revive, Q bandage, C workbench (inside the base by day, then number keys), RMB healing flask (Healer), F (hold 2 s, Engineer at night) enter or leave the base through a closed door, V skip-day vote, T (Engineer, 3-8 tiles in front of a door) build a tower, mouse wheel zoom (2, 3 or 4 screen pixels per art pixel), Esc leave.

For playtests, the day-skip vote is available immediately: press V at any time during the day. Once at least 60% of human players vote (one vote when playing solo or with bots), the 5-second dusk countdown begins, followed by night.

## What is in
- 5 day/night cycles on an 11 x 7 tile base (GDD: 15 x 10) with doors A (west), B (north), C (east), no south door; players enter only through doors, and at night only the Engineer (hold F) or anyone through a broken door.
- Players, enemies and bots share scenery/resource/building colliders from `world_geometry.gd`. Movement is swept in small steps; the server validates player movement and acknowledges predicted inputs. Grass, pebbles, bushes and herbs stay walk-through. Enemies and bots navigate around obstacles with cached A* paths.
- Bot teammates (`bots.gd`, not in the GDD, for demos and solo tests): in development builds the lobby leader adds or removes them (max 5 players), while in release builds they appear only as replacements after a disconnect vote (the "bot" timeout vote); a bot takes the missing role (Engineer, then Healer, Scout). They run on the server with the same rules as people: dodge boss slams, revive downed teammates, Healer flasks, fight, Engineer door repair, gather by day and craft near dusk, hold the attacked door at night. Only people count for the day-skip vote.
- Night waves spawn all around the island, from 8 tiles outside the base to near the coast, including the south, and head for the nearest attacked door. First wave after 5 s, then every 30 s (15 s on night 5); all enemies, including daytime roamers and bosses, move at half their original speed. FML-04 budget x `SPAWN_MUL` = 3 remains enabled for playtests; boss on nights 2 and 4, night-5 helicopter extraction.
- Daytime roamers beyond 20 tiles from the base (chance grows with distance, x `ROAM_MUL` = 2 for playtests, pairs, max 8); at dusk they walk to an attacked door.
- Enemy drops (GDD: Grunt 10%, Spitter 15%, Wrecker 20%, boss always; x `DROP_MUL` = 3 for playtests; resource nodes x `NODE_MUL` = 2): walk over to pick up, gone after 30 s, not from inside the base.
- Defence towers (AGT-12, MEC-11, MEC-28, BAL-28): 300 HP, shoot the nearest enemy within 6 tiles at 8 DPS, light 6 tiles at night for everyone. One free tower (placed with T by an Engineer on day 1, or it appears in front of night 1's door at dusk); more cost 10 wood + 5 metal after the first boss; one per door. At night enemies walking into a tower's light attack it first (Wreckers x2, Spitters never); the Engineer repairs it like a door, a destroyed one only to 30% and only with no enemy in its light.

## 2.5D look (branch `proto-2.5d`)
- The rules are still flat 2D (`game.gd` unchanged); only `main.gd` draws differently: everything with height (trees, rocks, walls, enemies, players) is sorted by the y of its foot, walls have a top and a front face, and bodies stand above their shadows. Scenery and resource nodes stay opaque even when a character stands behind them; depleted nodes are smaller until dawn.
- Pixel-art world (GDD style guide): everything is drawn without smoothing, and the camera zoom keeps one art pixel on a whole number of screen pixels.
- Fonts in `fonts/` (SIL OFL 1.1, licence files next to them): **Pixelify Sans** (Stefie Justprince, weight 560) for all text, **Jersey 10** (Sarah Cadigan-Fried) for titles, the timer, big numbers and banners.
- Base props in `art/props/` (lighthouse = beacon, workbench, tower, wrecked tower, landing pad) are pixel art drawn by `tools/pixel_props.gd`; doors have lamp pillars that light up at dusk.
- Nature sprites in `art/nature/` are pixel art (32 px per tile, banded colours, 1 px outline) rendered from Quaternius 3D models (CC0) by `tools/bake_nature.gd`; see `art/nature/CREDITS.txt`.
- Players and enemies in `art/chars/` are 32x32 pixel art (Wrecker 40x40) of the GDD characters (CHR-01..03 Scout, Healer, Engineer; CHR-05..07 Grunt, Spitter, Wrecker), drawn by `tools/pixel_chars.gd` (pixel maps, GDD palette, outline, 4 directions, idle + walk); see `art/chars/CREDITS.txt`. The PNGs can be repainted by hand. A boss is a big tinted Wrecker for now; a coloured ring at a player's feet shows the role.

## Dev
- Playtest settings: open `config/playtest.tres` in Godot's Inspector to tune dusk, wave timing, enemy speed, multipliers and camera follow. Restart the game after changing these settings. Existing defaults retain immediate day skipping, 5-second dusk and half-speed enemies.
- Editable landmarks: open `maps/island.tscn` in Godot's 2D editor. Duplicate `ExampleRock`, move it, choose a nature PNG in Texture, set a uniform Scale, and adjust `Solid Radius` / `Blocks Attacks` in the Inspector. Position is the ground anchor; the script aligns nature textures automatically. Keep these Sprite2D objects as direct children with zero rotation. The procedural forest remains, and every peer reads the same landmark scene. Restart after map edits.
- Combat: solid scenery and intact base walls block melee, enemy sight, tower fire and Spitter projectiles. Projectiles test their complete movement segment and stop at the nearest obstacle/player. Destroyed towers remain physical wrecks. Harvested resource colliders disappear until dawn.
- Presentation: activity states are sent in snapshots; gathering/repair show a swinging tool, healing/reviving show a pulsing cross, and animation timing restarts when the state changes. Existing character PNGs are preserved. Camera follow uses a dead zone and exponential smoothing with whole-pixel positions.
- F3 toggles collision outlines for playtesting. Enemy separation and scenery queries use spatial buckets; the renderer culls against the actual camera view at every zoom level.
- Geometry checks: `godot --headless --path . -s tests/geometry_check.gd` (shared scenery, navigation, swept hits, blocked attacks and movement validation).
- `-- --autolobby` opens the crew lobby with 2 bots for UI previews (`--bots=N` to change). The menu and lobby also support `--shot` screenshots.
- `-- --autohost` hosts and starts at once with 2 bot teammates (`--bots=N` to change, 0 for none); add `--night` to jump to night 1. `-- --autojoin` joins 127.0.0.1 and starts.
- `--shot=<file.png>` saves a screenshot after `--shot-at=<seconds>` (default 4) and quits; `--at=<x>,<y>` puts you at that tile first.
- F9 (host, debug build) ends the current phase.
- Rules check: `godot --headless --path . -s tests/sim_check.gd`
