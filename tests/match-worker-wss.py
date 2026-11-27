#!/usr/bin/env python3
"""Exercise production Godot directory transport over loopback HTTPS and WSS."""

import contextlib
import json
import os
from pathlib import Path
import secrets
import select
import shutil
import socket
import subprocess
import sys
import tempfile

ROOT = Path(__file__).resolve().parents[1]
LOBBY = """
import { readFileSync } from 'node:fs';
import { createServer } from 'node:https';
import { defineRoom, defineServer } from '@colyseus/core';
import { WebSocketTransport } from '@colyseus/ws-transport';
import { readConfig } from './dist/src/config.js';
import { lobbyRoom } from './dist/src/lobby-room.js';
import { WorkerManager } from './dist/src/worker-manager.js';
const config = readConfig();
const manager = new WorkerManager(config);
await manager.start();
const transport = new WebSocketTransport({ server: createServer({
  key: readFileSync(process.argv[1] + '/leaf.key'), cert: readFileSync(process.argv[1] + '/leaf.pem'),
}) });
const app = defineServer({ transport, rooms: { directory: defineRoom(lobbyRoom(manager)) }, greet: false,
  publicAddress: config.publicAddress, gracefullyShutdown: false });
await app.listen(config.lobbyPort, config.lobbyHost);
console.log('listening');
"""


def certs(directory):
    (directory / "leaf.ext").write_text("subjectAltName=DNS:localhost\nextendedKeyUsage=serverAuth\n")
    commands = [
        ["req", "-new", "-newkey", "rsa:2048", "-nodes", "-x509", "-days", "1",
         "-subj", "/CN=Hold til Dawn Local Test CA", "-addext", "basicConstraints=critical,CA:TRUE",
         "-keyout", "ca.key", "-out", "ca.pem"],
        ["req", "-new", "-newkey", "rsa:2048", "-nodes", "-subj", "/CN=localhost",
         "-keyout", "leaf.key", "-out", "leaf.csr"],
        ["x509", "-req", "-in", "leaf.csr", "-CA", "ca.pem", "-CAkey", "ca.key",
         "-CAcreateserial", "-days", "1", "-extfile", "leaf.ext", "-out", "leaf.pem"],
    ]
    for command in commands:
        subprocess.run(["openssl", *command], cwd=directory, check=True, capture_output=True)


def main(godot):
    godot = shutil.which(godot)
    if not godot:
        raise RuntimeError("Godot executable not found")
    service = ROOT / "multiplayer-server"
    subprocess.run(["npm", "run", "build", "--prefix", str(service)], check=True)
    with tempfile.TemporaryDirectory(prefix="htd-wss-") as temp:
        directory = Path(temp)
        certs(directory)
        project = directory / "project"
        project.mkdir()
        for name in (".godot", "addons", "art", "config", "fonts", "maps", "multiplayer", "src", "tests",
                     "match-worker.gd", "worker-control.gd", "game_sim.gd"):
            (project / name).symlink_to(ROOT / name)
        key = directory / "control.key"
        key.touch(mode=0o600)
        key.write_text(secrets.token_hex(32))
        with socket.socket() as lobby, socket.socket() as control:
            lobby.bind(("127.0.0.1", 0))
            control.bind(("127.0.0.1", 0))
            port = lobby.getsockname()[1]
            control_port = control.getsockname()[1]
        env = dict(os.environ, LOBBY_PORT=str(port), LOBBY_HOST="127.0.0.1", LOBBY_URL=f"wss://localhost:{port}",
                   CONTROL_ENDPOINT=f"127.0.0.1:{control_port}", ADMISSION_SECRET_FILE=str(key),
                   WORKER_EXECUTABLE=godot, WORKER_PROJECT=str(ROOT), WORKER_SUPERVISOR="local",
                   WORKER_STATE_DIR=str(directory / "leases"))
        with subprocess.Popen(["node", "--input-type=module", "-e", LOBBY, temp],
                              cwd=service, env=env, stdout=subprocess.PIPE, text=True) as server:
            try:
                if not select.select([server.stdout], [], [], 10)[0] or server.stdout.readline().strip() != "listening":
                    raise RuntimeError("Local WSS directory failed to listen")
                for case, host, bundle, expected in [
                    ("trusted", "localhost", str(directory / "ca.pem"), 0),
                    ("untrusted", "localhost", "", 1),
                    ("hostname mismatch", "127.0.0.1", str(directory / "ca.pem"), 1),
                ]:
                    (project / "project.godot").write_text(
                        "config_version=5\n[network]\ntls/certificate_bundle_override=" + json.dumps(bundle) + "\n")
                    outcome = subprocess.run(
                        [godot, "--headless", "--log-file", str(directory / "godot.log"), "--path", str(project),
                         "-s", "res://tests/lobby-wss-check.gd"],
                        env=dict(os.environ, WSS_TEST_URL=f"wss://{host}:{port}"),
                        capture_output=True, text=True, timeout=10,
                    )
                    state = "OFFLINE" if expected else "ONLINE"
                    assert outcome.returncode == expected and f"WSS directory state {state}" in outcome.stdout, (
                        case, outcome.stdout, outcome.stderr)
                    if expected:
                        assert "TlsInitializationFailed" in outcome.stdout, (case, outcome.stdout, outcome.stderr)
                    print(f"{case}: {state}", flush=True)
            finally:
                with contextlib.suppress(ProcessLookupError):
                    server.terminate()
                try:
                    server.wait(timeout=5)
                except subprocess.TimeoutExpired:
                    server.kill()
                    server.wait()


if __name__ == "__main__":
    main(sys.argv[1])
