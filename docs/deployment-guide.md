# Deployment guide

This guide is for a teammate setting up local development and testing, or a matched hosted release. It covers three ways to run the server: from source on one machine, hosted on a Linux VM with systemd, and hosted with Docker Compose. Run commands from the game repository root unless a step says otherwise. Replace every angle-bracket placeholder with its actual value before running a command; the brackets are not shell syntax to copy. See the [README](../README.md) for gameplay and the compact operations runbook.

## Components and network

```text
Desktop client -- HTTPS/WSS TCP 443 --> nginx -- HTTP/WebSocket TCP 2567 --> directory
Desktop client -------- ENet UDP 7778–7780 --------------------------------> match workers
Directory <-------------------- loopback TCP 2569 -------------------------> workers
Internet ---------------- HTTP TCP 80 --> nginx (HTTP-01 certificate challenge)
Debug client ------------ ENet UDP 7777 --> direct debug server (private only)
```

The directory allocates one headless Godot worker per room, up to three rooms on UDP 7778, 7779 and 7780. The directory and workers run on the same host; their control listener is loopback only. The directory HTTP listener is loopback only from source and under systemd, and binds its container interface under Docker. Public clients need both WSS for discovery/admission and UDP for gameplay. Keep TCP 2567, TCP 2569 and UDP 7777 private.

## Repository layout

The directory service, its tests and both hosted deployment paths live in the server repository [hold-til-dawn-server](https://github.com/nangsontay/hold-til-dawn-server), checked out at `multiplayer-server/` as a git submodule. The game repository pins the exact server commit. The match worker is the game itself running headless (`main.gd`, `match-worker.gd`, `worker-control.gd` and the shared simulation stay in the game repository); both hosted paths export it as `Hold-til-Dawn-Server.pck`.

Server tests and release scripts read game files through the parent directory, so run them from a game checkout with the submodule initialised, not from a standalone clone of the server repository. To change server code, commit inside `multiplayer-server/`, push the server repository, then commit the updated submodule pointer in the game repository.

## Prerequisites

Clone with `git clone --recurse-submodules <game-repository-url>`, or run `git submodule update --init` in an existing checkout.

- All paths: Python 3, OpenSSL, Godot **4.7.2 stable official** build `ed1daf0bf`, and Node **26.13.0** with npm. The Node pin is in [`.nvmrc`](https://github.com/nangsontay/hold-til-dawn-server/blob/main/.nvmrc) and [`package.json`](https://github.com/nangsontay/hold-til-dawn-server/blob/main/package.json); the exact Godot build is checked by [`package-release.sh`](https://github.com/nangsontay/hold-til-dawn-server/blob/main/deploy/package-release.sh) and [`build-docker.sh`](https://github.com/nangsontay/hold-til-dawn-server/blob/main/deploy/build-docker.sh).
- Fetch the Colyseus Godot SDK **0.18.10** native binaries before import or packaging: [`version.json`](../addons/colyseus/version.json) records the version, [`SHA256SUMS`](../addons/colyseus/SHA256SUMS) records each binary digest, and [`fetch-colyseus-sdk.py`](../tools/fetch-colyseus-sdk.py) verifies the download. The checked-in plugin metadata reports 0.18.0; the SDK pin is 0.18.10.
- Desktop clients for any hosted path: Godot desktop export templates for the [macOS arm64 and Windows x86-64 presets](../export_presets.cfg).
- systemd hosting: matching Godot export templates including the extracted Linux x86-64 `linux_release.x86_64`, and the extracted Node 26.13.0 Linux x64 distribution. The release script checks the Linux template's embedded build metadata and executable formats; activation verifies both bundled executable versions. The VM needs Linux x86-64, 4 vCPU and 4 GB RAM, systemd and polkit, nginx, Certbot, `nsupdate`, curl and Python 3.
- Docker: the build machine needs Docker with `linux/amd64` support (Apple Silicon uses emulation) in addition to Godot and the SDK above; no export templates are needed for the image. The host needs Linux x86-64, Docker Engine with Docker Compose v2.24 or newer, git and OpenSSL; Node, Godot, nginx and Certbot run in containers.
- Any public host: a public IPv4, control of `<game-hostname>`, and for a dynamic IP a DNS provider supporting RFC 2136 updates with a narrowly scoped TSIG key. The repo does not prescribe a Linux distribution or account-creation command.

## Local development and testing

### Run from source

1. Install the pinned tools and, in the checkout, initialise the submodule, fetch, import and build:

   ```sh
   git submodule update --init
   python3 tools/fetch-colyseus-sdk.py
   godot --headless --path . --import
   npm ci --prefix multiplayer-server
   npm run build --prefix multiplayer-server
   ```

2. In a service terminal, create a private run directory and key, then start the directory. `godot` must resolve to an absolute executable path via `command -v`; use that absolute path explicitly if it does not.

   ```sh
   run_dir=$(mktemp -d)
   (umask 077; openssl rand -hex 32 > "$run_dir/worker.key")
   env WORKER_EXECUTABLE="$(command -v godot)" WORKER_PROJECT="$PWD" \
     ADMISSION_SECRET_FILE="$run_dir/worker.key" WORKER_STATE_DIR="$run_dir/workers" \
     npm start --prefix multiplayer-server
   ```

   Leave this terminal running. In another terminal, check:

   ```sh
   curl -fsS http://127.0.0.1:2567/health
   ```

   Expect HTTP 200 with `"ready":true` and `"draining":false`. A 503 indicates ownership reconciliation or shutdown; full and draining can still be healthy.

3. In two or more separate terminals on this machine, start clients:

   ```sh
   godot --path . -- --lobby-url=ws://127.0.0.1:2567
   ```

   In each client choose a callsign and role and select **Play**. Create a room in the first, then join it from the others. Confirm the crew appears together; ready the players, choose an Engineer and start. The debug client accepts `ws://`; an exported release client requires `wss://`. Debug builds also let the room leader add and remove bots; hosted release workers do not.

4. Quit the clients and press Ctrl-C in the service terminal. An ordinary stop closes managed workers and ends their rooms. Keep the same key and state directory for a directory-only restart that should recover live workers; do not delete them while workers may be running. The state directory retains leases and the drain flag across restarts. A fresh temporary directory gives an independent rehearsal; remove it only after stopping the service and workers.

### Automated tests

Run these after the import and build in step 1. The server suite and the integration checks start real directories and Godot workers on TCP 2567 and UDP 7778–7780, so stop a running local directory or Docker stack first.

```sh
GODOT_BIN="$(command -v godot)" npm test --prefix multiplayer-server
python3 tests/hosted-client-integration.py
python3 tests/match-worker-integration.py "$(command -v godot)"
python3 tests/release-verification.py --mode clean
godot --headless --path . -s tests/sim_check.gd
godot --headless --path . -s tests/geometry_check.gd
```

- `npm test` builds the service and runs its unit and integration tests. `GODOT_BIN` defaults to `/Applications/Godot.app/Contents/MacOS/Godot`, so set it on other systems.
- [`hosted-client-integration.py`](../tests/hosted-client-integration.py) creates a separate local directory and tests the real client flow. `GODOT_BIN` can select the Godot executable.
- [`release-verification.py`](../tests/release-verification.py) runs three rooms with fifteen headless client peers; `--mode lossy` adds impaired UDP, and no `--mode` runs both.
- The last two are the rules and geometry checks from the README's [Dev](../README.md#dev) section.

### Run the server in Docker locally

This runs the same image as a hosted Docker deployment, with the directory on `ws://127.0.0.1:2567` and workers on local UDP 7778–7780. Build the image once per change, then start it:

```sh
GODOT_BIN="$(command -v godot)" multiplayer-server/deploy/build-docker.sh hold-til-dawn:local
multiplayer-server/deploy/docker-local.sh
```

The build exports the worker PCK from this checkout, downloads the official Godot 4.7.2 Linux release template and verifies its SHA-512, and tags the image. [`docker-local.sh`](https://github.com/nangsontay/hold-til-dawn-server/blob/main/deploy/docker-local.sh) creates `multiplayer-server/worker.key` if it is absent (the file is gitignored), starts the directory and workers as the separate Compose project `hold-til-dawn-local`, and returns once the directory is healthy. To run another tag, set `HTD_IMAGE=hold-til-dawn:<tag>` before the script.

Check it, connect debug clients as in [Run from source](#run-from-source), and confirm the worker is a release build:

```sh
curl -fsS http://127.0.0.1:2567/health
godot --path . -- --lobby-url=ws://127.0.0.1:2567
godot --headless --path . -s res://multiplayer-server/test/release-client-check.gd -- --headless-client --lobby-url=ws://127.0.0.1:2567
```

The last command creates a room through the container, is admitted by a worker over UDP, tries to add a bot and expects the worker to refuse; it prints `Release worker admitted the leader and rejected add_bot` and exits 0.

Rehearse the drain and release check against the local stack:

```sh
export COMPOSE_FILE=compose.yaml:compose.local.yaml COMPOSE_PROFILES=local
multiplayer-server/deploy/docker-ops.sh on
multiplayer-server/deploy/docker-ops.sh wait 120
multiplayer-server/deploy/docker-ops.sh check
multiplayer-server/deploy/docker-ops.sh off
unset COMPOSE_FILE COMPOSE_PROFILES
```

View the directory log with `(cd multiplayer-server && docker compose -p hold-til-dawn-local logs -f directory)`. Worker output is not captured; the directory logs each allocation and release. Stop the stack with:

```sh
(cd multiplayer-server && COMPOSE_PROJECT_NAME=hold-til-dawn-local HTD_IMAGE=hold-til-dawn:local GAME_HOST=127.0.0.1 \
  docker compose -f compose.yaml -f compose.local.yaml --profile local down)
```

Add `-v` to `down` to also delete the local state and key volumes for a clean next run. Restarting the container ends every match. The local Docker stack, a source directory and the automated tests use the same ports, so run one at a time.

### LAN and direct debug play

For LAN clients, set `WORKER_HOST` to the service machine's reachable LAN IP when starting the directory. Provide a LAN-only HTTP/WebSocket reverse proxy to its loopback TCP 2567 listener, allow UDP 7778–7780 from the LAN, and start debug clients with `--lobby-url=ws://<LAN-proxy-address>`. The repository does not supply a LAN proxy configuration; ensure it forwards the same paths and WebSocket upgrades as the [nginx example](https://github.com/nangsontay/hold-til-dawn-server/blob/main/deploy/nginx.conf.example). Do not expose the debug endpoint publicly.

For a separate direct debug route, start `godot --headless --path . -- --server` and, in another terminal, `godot --path . -- --autojoin`. This connects to 127.0.0.1 over UDP 7777 without the directory. Stop the client and server with their usual quit action or Ctrl-C.

## Hosted deployment with systemd

The following host commands require the appropriate operator privileges. The commands marked as examples illustrate the required filesystem layout; account creation and service installation differ by distribution.

### 1. Build the matched release

Fetch the SDK and import as above. Choose a new output path whose parent exists, a release ID and the public hostname. Supply the **extracted** Linux Godot release executable and Linux Node distribution directory:

```sh
PACKAGE_DESKTOP=1 multiplayer-server/deploy/package-release.sh \
  /absolute/output/<release-id> <release-id> <game-hostname> \
  /absolute/templates/linux_release.x86_64 /absolute/node-v26.13.0-linux-x64
```

The output must not already exist. The script builds a staged service and `game/Hold-til-Dawn-Server.pck`, adds the Linux worker executable and `node/`, copies `deploy/` and `notices/`, and places desktop exports under `clients/macOS/` and `clients/Windows/`. The root `RELEASE` manifest records the release, package kind, endpoint, versions, compatibility values and artifact checksums. Keep it and the complete preceding matched bundle for rollback. With only the first three arguments the output is `build-only`, suitable for local inspection, not Linux activation. `GODOT_BIN` can select the editor. Check upstream notices and platform signing requirements before distribution.

Optional local packaged smoke test, after building the service and importing the checkout:

```sh
python3 tests/hosted-client-integration.py --release /absolute/output/<release-id>
```

This uses the packaged service and worker PCK with the local Godot binary, not the bundled Linux executable.

### 2. Provision the VM

1. Create service user and group `htd`; the [directory](https://github.com/nangsontay/hold-til-dawn-server/blob/main/deploy/hold-til-dawn-multiplayer.service) and [worker](https://github.com/nangsontay/hold-til-dawn-server/blob/main/deploy/hold-til-dawn-worker@.service) units run as `htd:htd`. Create `/opt/hold-til-dawn-releases`, `/etc/hold-til-dawn` and `/var/lib/hold-til-dawn/workers`; make the state directory `htd:htd` mode 0700. Copy the complete release to `/opt/hold-til-dawn-releases/<release-id>` and keep releases root-owned. Before first activation, `/opt/hold-til-dawn` must be an **absolute symlink** to a complete release: the activation script requires an existing symlink even on first use.

   For example, after creating the account and copying the bundle:

   ```sh
   install -d -o htd -g htd -m 0700 /var/lib/hold-til-dawn/workers
   ln -s /opt/hold-til-dawn-releases/<release-id> /opt/hold-til-dawn
   ```

2. Install the four [systemd unit and timer files](https://github.com/nangsontay/hold-til-dawn-server/tree/main/deploy) into `/etc/systemd/system/`, and [`50-hold-til-dawn.rules`](https://github.com/nangsontay/hold-til-dawn-server/blob/main/deploy/50-hold-til-dawn.rules) into `/etc/polkit-1/rules.d/`. Copy [`multiplayer.conf.example`](https://github.com/nangsontay/hold-til-dawn-server/blob/main/deploy/multiplayer.conf.example) to `/etc/hold-til-dawn/multiplayer.conf`. Set `LOBBY_URL=wss://<game-hostname>` and `WORKER_HOST=<game-hostname>` to the same public name; retain the `systemd` supervisor and absolute paths. Do not set `WORKER_PROJECT` for the packaged worker.

3. Generate `/etc/hold-til-dawn/worker.key` on the host as a private file, owned by `htd:htd` and mode 0600. For example:

   ```sh
   (umask 077; openssl rand -hex 32 > /etc/hold-til-dawn/worker.key)
   chown htd:htd /etc/hold-til-dawn/worker.key
   chmod 0600 /etc/hold-til-dawn/worker.key
   ```

   Preserve this key and the state directory across restarts and releases. Never distribute the key with clients.

4. Permit inbound TCP 443 and UDP 7778–7780, and TCP 80 for HTTP-01 issuance and renewal. Keep TCP 2567 and 2569 loopback/private and UDP 7777 private. Apply the rules with the VM provider and its local firewall; the repo does not select a firewall tool.

### 3. Configure DNS and DDNS

Delegate the name to an RFC 2136 provider and provision a TSIG key limited to its A record. Copy [`ddns.conf.example`](https://github.com/nangsontay/hold-til-dawn-server/blob/main/deploy/ddns.conf.example) to `/etc/hold-til-dawn/ddns.conf` and set `DDNS_NAME`, `DDNS_ZONE`, `DDNS_SERVER`, `DDNS_KEY_FILE` and `DDNS_IP_URL`. Keep the TSIG key file root-owned, mode 0600. The configured HTTPS discovery response must match the VM's public IPv4; remove a stale AAAA record unless IPv6 is separately provisioned. [`update-dns.sh`](https://github.com/nangsontay/hold-til-dawn-server/blob/main/deploy/update-dns.sh) validates the response and updates the A record.

```sh
systemctl daemon-reload
systemctl enable --now hold-til-dawn-dns.timer
systemctl start hold-til-dawn-dns.service
dig @<authoritative-server> <game-hostname> A +short
```

The [timer](https://github.com/nangsontay/hold-til-dawn-server/blob/main/deploy/hold-til-dawn-dns.timer) runs at boot and every five minutes after the service becomes inactive. Check the authoritative answer and a public resolver, taking the previous record's TTL into account.

### 4. Obtain TLS and configure nginx

After DNS resolves to the VM and while TCP 80 is free, obtain the first certificate:

```sh
certbot certonly --standalone -d <game-hostname>
```

Install the release's [`nginx.conf.example`](https://github.com/nangsontay/hold-til-dawn-server/blob/main/deploy/nginx.conf.example) in nginx's HTTP configuration, with `<game-hostname>` substituted, and create `/var/www/letsencrypt` for webroot challenges. The release packaging substitutes the hostname in its copy. Validate before enabling nginx:

```sh
nginx -t
```

Enable nginx using the host's service setup. The configuration forwards `/health`, the directory matchmaking POST and WebSocket upgrades; the client connects directly to worker UDP ports. Reconfigure the standalone certificate for webroot renewal, test it, and configure the Certbot deploy hook to `/opt/hold-til-dawn/deploy/reload-certificate.sh`:

```sh
certbot reconfigure --cert-name <game-hostname> --webroot-path /var/www/letsencrypt --authenticator webroot
certbot renew --dry-run
```

Confirm the Certbot timer is enabled. The [hook](https://github.com/nangsontay/hold-til-dawn-server/blob/main/deploy/reload-certificate.sh) checks nginx configuration then reloads it after renewal. Hook installation and timer setup depend on the host's Certbot packaging.

### 5. Start drained, activate and verify

The activation script requires an existing running directory, drain flag and no worker leases. For a first installation, with the initial symlink and configuration in place, create the drain flag before starting the service, then enable it. The following uses the default state path:

```sh
/opt/hold-til-dawn/deploy/drain.sh on
systemctl enable --now hold-til-dawn-multiplayer.service
curl -fsS http://127.0.0.1:2567/health
/opt/hold-til-dawn/deploy/drain.sh wait
/opt/hold-til-dawn/deploy/activate-release.sh <release-id>
```

Activation validates the manifest and bundled versions, verifies a drained healthy service, tests directory compatibility and launches and closes each authenticated worker. It leaves admissions drained. If activation fails, it attempts to restore and rehearse the previous symlink target; inspect its output and do not reopen an unverified release. See [`activate-release.sh`](https://github.com/nangsontay/hold-til-dawn-server/blob/main/deploy/activate-release.sh) and [`drain.sh`](https://github.com/nangsontay/hold-til-dawn-server/blob/main/deploy/drain.sh). When using non-default state or lobby port, pass the matching `WORKER_STATE_DIR` and `LOBBY_PORT` to operator scripts.

From another network check `curl -fsS https://<game-hostname>/health`. On an isolated staging instance with admissions open, verify the matched exported client can create and join a room over public WSS and ENet, and verify signed worker registration. Check the manifest and versions. Only after this passes, open production admissions:

```sh
/opt/hold-til-dawn/deploy/drain.sh off
```

Confirm health, create and join a room from exported clients on an external network, then distribute the matching `clients/macOS/` and `clients/Windows/` artifacts and their notices together. Validate each export on its target OS; preserve the matching release bundle for rollback.

### 6. Operate and troubleshoot

- **Drain and upgrade:** Run `drain.sh on`, confirm health reports `"draining":true`, let active crews finish and ask waiting/results crews to leave. Run `drain.sh wait` (default timeout 3600 seconds; empty rooms can take 60 seconds). Copy the next complete bundle into the releases directory and run `activate-release.sh <release-id>`. Verify staging and matched exported clients before `drain.sh off`.
- **Rollback:** Drain and wait again, activate the preceding release ID using the same procedure, verify it and distribute its matching desktop clients. Rehearse a deliberately failed activation on the target host; if restoration cannot be verified, leave admissions drained.
- **Directory-only restart:** `systemctl kill --kill-whom=main --signal=HUP hold-til-dawn-multiplayer.service` restarts the directory under systemd. Check health and that the same worker PIDs and occupancy return; live ENet matches can continue. A normal service stop instead closes workers and ends rooms.
- **Planned shutdown:** Run `drain.sh on` then `drain.sh shutdown`; confirm the three worker units are inactive and UDP 7778–7780 are free before shutting down. The drain flag persists across boot; check DNS, health and release before `drain.sh off`.
- **Certificates and DNS:** Keep TCP 80 and the challenge location available. Monitor renewal and, after renewal, check the served certificate chain and expiry from another network and that matches survive nginx reload. For a changed VM IP, inspect `journalctl -u hold-til-dawn-dns.service`, authoritative/public A records and the old TTL.
- **Logs and recovery:** Check `journalctl -u hold-til-dawn-multiplayer.service`, `journalctl -u hold-til-dawn-worker@7778.service` (also 7779 and 7780), and `journalctl -u hold-til-dawn-dns.service`. For recovering rooms or HTTP 503, inspect worker ownership, the state directory, key permissions and directory/worker logs before changing state. For connection failures, check DNS, certificate name and trust, public `/health`, nginx forwarding, WSS client endpoint, advertised `WORKER_HOST`, UDP firewall and matching client/release versions. Do not remove live leases or expose secrets in logs.

## Hosted deployment with Docker Compose

One Compose stack runs the directory with its three workers in one container, nginx for TLS and WSS, Certbot for renewal and an optional DDNS updater. The release is an image tag. The stack files live in the server repository: [`compose.yaml`](https://github.com/nangsontay/hold-til-dawn-server/blob/main/compose.yaml), [`Dockerfile`](https://github.com/nangsontay/hold-til-dawn-server/blob/main/Dockerfile), [`docker/`](https://github.com/nangsontay/hold-til-dawn-server/tree/main/docker) and the scripts in [`deploy/`](https://github.com/nangsontay/hold-til-dawn-server/tree/main/deploy). Its [README](https://github.com/nangsontay/hold-til-dawn-server#docker) is the short reference; this section is the full sequence.

### 1. Build the image and matched clients

On the build machine, check out the game revision to release with its submodule, fetch the SDK, then build the image and the desktop clients from that same revision:

```sh
git submodule update --init
python3 tools/fetch-colyseus-sdk.py
GODOT_BIN="$(command -v godot)" multiplayer-server/deploy/build-docker.sh hold-til-dawn:<release-id>
PACKAGE_DESKTOP=1 multiplayer-server/deploy/package-release.sh /absolute/output/<release-id> <release-id> <game-hostname>
git -C multiplayer-server rev-parse HEAD
```

The image holds the built directory, its production dependencies, the worker PCK and the official Godot 4.7.2 Linux x86-64 release template, whose SHA-512 is checked against Godot's published checksums. It is built for `linux/amd64`. The three-argument `package-release.sh` run is a `build-only` bundle; for Docker only its `clients/macOS/` and `clients/Windows/` exports are used, with `<game-hostname>` baked in as their WSS endpoint. Record the server commit printed by the last command; the host uses the same commit.

Move the image to the host. The repo does not prescribe a registry; for example:

```sh
docker save hold-til-dawn:<release-id> | ssh <host> docker load
```

### 2. Prepare the host

Install Docker Engine and the Compose plugin with the distribution's method. Clone the server repository at the recorded commit; every later command runs in this checkout:

```sh
git clone https://github.com/nangsontay/hold-til-dawn-server.git
cd hold-til-dawn-server
git checkout <server-commit>
```

Create the gitignored `.env` with the public hostname and the release image, and the worker key:

```sh
printf 'GAME_HOST=%s\nHTD_IMAGE=%s\n' <game-hostname> hold-til-dawn:<release-id> > .env
(umask 077; openssl rand -hex 32 > worker.key)
```

Compose reads `.env` automatically and refuses to start if `GAME_HOST` or `HTD_IMAGE` is missing. Shell variables override `.env`, so unset any stale `GAME_HOST` or `HTD_IMAGE` in the operator shell. A one-shot root `init` container copies `worker.key` into a private volume owned by the unprivileged directory user with mode 0600 each time the stack starts; keep the host file private as well and never commit or distribute it.

Permit inbound TCP 80 and 443 and UDP 7778–7780. The production profile does not publish TCP 2567. Docker publishes ports through its own packet-filter rules, which can bypass host firewall front-ends such as ufw, so check reachability from another network.

### 3. DNS and the first certificate

With a static IP, point the A record for `<game-hostname>` at the host. With a dynamic IP, start the DDNS updater first. Copy [`ddns.conf.example`](https://github.com/nangsontay/hold-til-dawn-server/blob/main/deploy/ddns.conf.example) to `ddns.conf`, set `DDNS_NAME`, `DDNS_ZONE`, `DDNS_SERVER` and `DDNS_IP_URL`, and set `DDNS_KEY_FILE=/config/ddns.key`. Put the TSIG key in `ddns.key` with mode 0600. The `DDNS_CONFIG` and `DDNS_KEY` variables select other host paths. Then:

```sh
docker compose --profile ddns up -d --build ddns
docker compose --profile ddns logs ddns
dig @<authoritative-server> <game-hostname> A +short
```

The updater runs the existing [`update-dns.sh`](https://github.com/nangsontay/hold-til-dawn-server/blob/main/deploy/update-dns.sh) immediately and every five minutes, and keeps running independently of the production services.

TLS nginx cannot start before a certificate exists, so a bootstrap nginx serves only HTTP-01 challenges on TCP 80 for the first issuance. Once DNS resolves to the host:

```sh
docker compose --profile bootstrap up -d bootstrap
docker compose --profile production run --rm --no-deps --entrypoint certbot certbot certonly \
  --webroot -w /var/www/letsencrypt -d <game-hostname> --email <operator-email> --agree-tos --no-eff-email
docker compose --profile bootstrap stop bootstrap
```

The certificate and the challenge webroot are named volumes shared by Certbot and nginx.

### 4. Start drained, check and open

Create the drain flag in the state volume before the first start, then start the stack and run the release check:

```sh
docker compose --profile production run --rm --no-deps --user root --entrypoint sh init \
  -c 'mkdir -p /state; touch /state/drain; chown 1000:1000 /state /state/drain; chmod 700 /state; chmod 600 /state/drain'
docker compose --profile production up -d --wait
deploy/docker-ops.sh check
```

`--wait` returns once the directory reports `"ready":true`. [`docker-ops.sh check`](https://github.com/nangsontay/hold-til-dawn-server/blob/main/deploy/docker-ops.sh) requires a drained directory with no leases, runs the directory compatibility check, then launches and closes an authenticated worker on all three slots, and leaves admissions drained. If it fails, keep admissions drained and inspect the logs.

From another network check `curl -fsS https://<game-hostname>/health`; expect `"draining":true` at this point. Open admissions, then create and join a room from the matched exported clients on an external network before distributing them:

```sh
deploy/docker-ops.sh off
```

### 5. Operate

- **Drain:** `deploy/docker-ops.sh on` stops new rooms. `deploy/docker-ops.sh wait [seconds]` waits until no worker leases remain (default 3600 seconds), and `deploy/docker-ops.sh shutdown [seconds]` waits and then stops the directory. The drain flag persists in the state volume across restarts and reboots.
- **Upgrade:** build and move the new image and build its matched clients as in step 1, and check out the new server commit in the host checkout. Then drain and wait, edit `HTD_IMAGE` in `.env` to the new tag, and recreate the directory:

  ```sh
  deploy/docker-ops.sh on
  deploy/docker-ops.sh wait
  docker compose --profile production up -d --wait --no-deps --force-recreate directory
  docker compose --profile production exec nginx nginx -s reload
  deploy/docker-ops.sh check
  ```

  The nginx reload makes it resolve the new directory container. If the server commit changed `compose.yaml` or the files in `docker/`, run `docker compose --profile production up -d --wait --force-recreate` instead. Verify the new clients over WSS and UDP, then `deploy/docker-ops.sh off`.
- **Rollback:** repeat the upgrade with the previous tag in `.env` and the previous server commit, checking again while drained, and redistribute the previous clients. Keep the previous image on the host (`docker image ls hold-til-dawn`) until the new release is proven.
- **Restarts:** the workers are child processes of the directory container, so any restart of that container, including `docker compose restart directory`, a crash or a host reboot, ends every live match. There is no directory-only restart; drain first. Services restart automatically unless stopped.
- **Certificates:** the Certbot container runs `certbot renew` every 12 hours, and nginx checks its configuration and reloads every six hours, so renewed certificates are picked up without giving any container the Docker socket. Test renewal with `docker compose --profile production run --rm --no-deps --entrypoint certbot certbot renew --dry-run` and monitor the served chain and expiry from another network.
- **Logs:** `docker compose --profile production logs -f directory nginx certbot`, and `docker compose --profile ddns logs -f ddns` if DDNS is enabled. Worker output is not captured; the directory logs each allocation and release. For recovering rooms or failed connections, follow the checks in the systemd troubleshooting list above, using these logs.
- **Data:** the named volumes `state` (leases and drain flag), `private` (worker key copy), `certificates` and `acme` are prefixed with the Compose project name, which defaults to the checkout directory name. Never run `docker compose down -v` in production; it deletes the drain flag, key copy and certificates.

### Differences from the systemd path

| | systemd | Docker Compose |
|---|---|---|
| Release unit | `/opt/hold-til-dawn` symlink and `activate-release.sh` | image tag in `.env` and `docker-ops.sh check` |
| Worker runtime | Linux release template from the release bundle | the same official template, downloaded and verified during the image build |
| Worker supervision | `hold-til-dawn-worker@` units | child processes of the directory container |
| Directory-only restart | `HUP` keeps live matches | not available; a restart ends matches |
| TLS renewal | host Certbot with the `reload-certificate.sh` deploy hook | Certbot container, nginx reload every six hours |
| DDNS | systemd timer | optional `ddns` profile, every five minutes |
| Logs | `journalctl` for directory and each worker | `docker compose logs`; worker output is not kept |

## Configuration reference

The authoritative validation is in [`config.ts`](https://github.com/nangsontay/hold-til-dawn-server/blob/main/src/config.ts), [`worker-process.ts`](https://github.com/nangsontay/hold-til-dawn-server/blob/main/src/worker-process.ts), [`match-worker.gd`](../match-worker.gd), and [`lobby-client.gd`](../multiplayer/lobby-client.gd). “Both” means local and hosted; worker values are supplied by the directory, not manually configured for each room.

| Variable | Default | Allowed values / purpose | Scope |
|---|---|---|---|
| `LOBBY_PORT` | `2567` | Integer 1–65535; different from control port. | Both |
| `LOBBY_HOST` | `127.0.0.1` | `127.0.0.1` or explicitly `0.0.0.0` for a container interface. | Both |
| `CONTROL_ENDPOINT` | `127.0.0.1:2569` | Loopback IPv4 plus port 1–65535, distinct from lobby port; passed to workers. | Both |
| `WORKER_HOST` | `127.0.0.1` | IP address or valid hostname advertised for ENet. | Both; LAN/public must be reachable |
| `LOBBY_URL` (directory) | unset | WSS URL with authority only, no credentials, path other than `/`, query or fragment; required with systemd. | Production |
| `WORKER_EXECUTABLE` | unset; required | Absolute path to executable file. | Both |
| `WORKER_PROJECT` | unset | Optional absolute path containing `project.godot`. | Local source workers |
| `ADMISSION_SECRET_FILE` | unset; required | Absolute file path, 64 lowercase hex characters plus optional newline; directory rejects group write/execute and any permissions for other users. Worker reads same key. | Both |
| `WORKER_SUPERVISOR` | `local` | `local`, or `systemd` on Linux with public WSS configured. | Both |
| `WORKER_STATE_DIR` | service `.runtime` directory | Absolute directory, created if needed; no group/other permission bits. | Both |
| `ADMISSION_TTL_MS` | `15000` | Integer 3000–30000 milliseconds. | Both |
| `MATCH_ROOM_ID`, `MATCH_GENERATION` | unset; required by worker | Directory-generated tokens: 1–64 letters, digits, `_` or `-`. | Both, internal |
| `MATCH_ROOM_NAME` | unset; required by worker | Nonblank name, at most 64 characters, no control characters; worker truncates to 16. | Both, internal |
| `MATCH_UDP_PORT` | unset; required by worker | `7778`, `7779`, or `7780`. | Both, internal |

In Docker the image sets `WORKER_EXECUTABLE=/usr/local/bin/worker`, `WORKER_STATE_DIR=/state`, `ADMISSION_SECRET_FILE=/private/worker.key`, `WORKER_SUPERVISOR=local`, `CONTROL_ENDPOINT=127.0.0.1:2569` and `LOBBY_HOST=0.0.0.0`, and Compose sets `WORKER_HOST=<GAME_HOST>` and `LOBBY_URL=wss://<GAME_HOST>` (the local profile uses `127.0.0.1` and no `LOBBY_URL`). The Compose files read these host variables:

| Variable | Default | Purpose |
|---|---|---|
| `HTD_IMAGE` | required; `hold-til-dawn:local` in the local scripts | Image tag to run. |
| `GAME_HOST` | required; `127.0.0.1` in the local scripts | Public hostname for nginx, the certificate path, `WORKER_HOST` and `LOBBY_URL`. |
| `WORKER_KEY` | `./worker.key` | Host file holding the worker key. |
| `DDNS_CONFIG`, `DDNS_KEY` | `./ddns.conf`, `./ddns.key` | Host files for the DDNS updater. |

Client endpoint: `--lobby-url=<url>` overrides client `LOBBY_URL`, which overrides the baked-in `wss://game.example.com` default (replaced with the build hostname during desktop packaging). URLs must be WSS, except debug builds may use WS; they must have an authority and no path, credentials, query or fragment. Pass game flags after `--` to Godot.

Other debug flags from [Dev](../README.md#dev): `--server` starts the direct debug server; `--autojoin` connects to 127.0.0.1; `--autolobby` previews the crew screen with two bots; `--autohost` starts with two bots; `--bots=N` changes that number and `--night` starts at night 1. `--shot=<file.png>` saves and quits after `--shot-at=<seconds>` (default 4); `--at=<x>,<y>` sets the initial tile. `--headless-client` selects headless client mode for tests; `--match-worker` is the internal managed-worker route.

## Known gaps

The exported-client, Linux/systemd, public TLS/DNS and Singapore VM release gates remain open. Drain and rollback, including a deliberately failed activation, have not yet been rehearsed on the target Linux host (see the README's [recovery, drain and rollback](../README.md#recovery-drain-and-rollback) section). Public-certificate WSS and exported-client validation remain release checks. See the README's [accepted risks and decisions for the three-room test](../README.md#accepted-risks-and-decisions-for-the-three-room-test).

The Docker path has run only on macOS under `linux/amd64` emulation: the local profile with a real client, and the production profile with a self-signed certificate, the route checks and the drained release check. Public certificate issuance, DDNS updates, a Linux Docker host, and Docker upgrade and rollback have not been exercised.
