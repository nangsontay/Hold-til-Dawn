#!/usr/bin/env python3
"""Run the production Godot client flow against local Colyseus and ENet workers."""

import argparse
import contextlib
import os
from pathlib import Path
import secrets
import shutil
import signal
import socket
import subprocess
import tempfile
import time
from urllib.request import urlopen

ROOT = Path(__file__).resolve().parents[1]
GODOT = os.environ.get("GODOT_BIN") or shutil.which("godot") or "/Applications/Godot.app/Contents/MacOS/Godot"
PORTS = (7778, 7779, 7780)


def verify_ports():
    with contextlib.ExitStack() as stack:
        for port in PORTS:
            endpoint = stack.enter_context(socket.socket(socket.AF_INET, socket.SOCK_DGRAM))
            endpoint.bind(("0.0.0.0", port))


def owned_workers(work):
    output = subprocess.check_output(["/bin/ps", "-axww", "-o", "pid=,stat=,command="], text=True)
    for row in output.splitlines():
        if f"--log-file {work}/worker-" not in row and f" {work}/godot-worker " not in row:
            continue
        pid, status, command = row.strip().split(None, 2)
        if not status.startswith("Z") and command.rsplit(None, 1)[-1].startswith("--htd-slot="):
            yield int(pid)


def clean_workers(work):
    for sig in (signal.SIGTERM, signal.SIGKILL):
        pids = list(owned_workers(work))
        if not pids:
            break
        for pid in pids:
            with contextlib.suppress(ProcessLookupError):
                os.killpg(pid, sig)
        deadline = time.monotonic() + 3
        while time.monotonic() < deadline and any(owned_workers(work)):
            time.sleep(0.05)
    if any(owned_workers(work)):
        raise RuntimeError("A hosted test worker did not exit")
    verify_ports()


def interrupt(_signal, _frame):
    raise KeyboardInterrupt


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--release", type=Path, help="use the packaged service and worker PCK with the local Godot binary")
    args = parser.parse_args()
    release = args.release.resolve() if args.release else None
    if not release:
        subprocess.run(["npm", "run", "build", "--prefix", str(ROOT / "multiplayer-server")], check=True)
    signal.signal(signal.SIGTERM, interrupt)
    verify_ports()
    with tempfile.TemporaryDirectory(prefix="htd-client-") as directory:
        work = Path(directory)
        key = work / "key"
        key.touch(mode=0o600)
        key.write_text(secrets.token_hex(32))
        with socket.socket() as lobby, socket.socket() as control:
            lobby.bind(("127.0.0.1", 0))
            control.bind(("127.0.0.1", 0))
            lobby_port = lobby.getsockname()[1]
            control_port = control.getsockname()[1]
        env = os.environ.copy()
        env.pop("LOBBY_URL", None)
        worker = work / "godot-worker"
        worker.write_text('#!/bin/sh\nif [ -n "${HTD_WORKER_PACK:-}" ]; then set -- --main-pack "$HTD_WORKER_PACK" "$@"; fi\n'
                          'exec "$GODOT_BIN" --log-file "$HTD_TEST_DIR/worker-$MATCH_UDP_PORT.log" "$@"\n')
        worker.chmod(0o700)
        env.pop("HTD_WORKER_PACK", None)
        env.update(WORKER_EXECUTABLE=str(worker), GODOT_BIN=GODOT, HTD_TEST_DIR=str(work),
                   WORKER_PROJECT=str(ROOT), WORKER_HOST="127.0.0.1",
                   WORKER_STATE_DIR=str(work / "workers"), WORKER_SUPERVISOR="local",
                   LOBBY_PORT=str(lobby_port), LOBBY_HOST="127.0.0.1",
                   CONTROL_ENDPOINT=f"127.0.0.1:{control_port}", ADMISSION_SECRET_FILE=str(key),
                   ADMISSION_TTL_MS="4200")
        server = (release or ROOT) / "multiplayer-server/dist/src/server.js"
        if release:
            pack = release / "game/Hold-til-Dawn-Server.pck"
            if not server.is_file() or not pack.is_file():
                parser.error("--release must contain a packaged service and worker PCK")
            env["HTD_WORKER_PACK"] = str(pack)
            env.pop("WORKER_PROJECT")
        with (work / "directory.log").open("w+") as log:
            manager = subprocess.Popen(["node", str(server)], cwd=work, env=env,
                                       stdout=log, stderr=subprocess.STDOUT, start_new_session=True)
            try:
                deadline = time.monotonic() + 12
                while time.monotonic() < deadline and manager.poll() is None:
                    try:
                        with urlopen(f"http://127.0.0.1:{lobby_port}/health", timeout=0.5) as response:
                            if response.status == 200:
                                break
                    except OSError:
                        time.sleep(0.1)
                else:
                    raise RuntimeError("Local directory failed to become healthy")
                subprocess.run([GODOT, "--headless", "--log-file", str(work / "client.log"), "--path", str(ROOT),
                                "-s", "res://tests/hosted-client-check.gd", "--", "--headless-client",
                                f"--lobby-url=ws://127.0.0.1:{lobby_port}"], cwd=ROOT, check=True, timeout=70)
                if "SCRIPT ERROR:" in (work / "client.log").read_text():
                    raise RuntimeError("Hosted client reported a script error")
            finally:
                try:
                    if manager.poll() is None:
                        manager.terminate()
                    try:
                        stopped = manager.wait(timeout=15)
                    except subprocess.TimeoutExpired:
                        with contextlib.suppress(ProcessLookupError):
                            os.killpg(manager.pid, signal.SIGKILL)
                        manager.wait()
                        raise
                    if stopped:
                        raise RuntimeError(f"Local directory exited with status {stopped}")
                finally:
                    clean_workers(work)


if __name__ == "__main__":
    main()
