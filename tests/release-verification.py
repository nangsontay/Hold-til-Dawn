#!/usr/bin/env python3
"""Exercise three five-client rooms over clean and impaired loopback UDP."""

import argparse
from collections import defaultdict
from contextlib import ExitStack, suppress
import heapq
import itertools
import json
import math
import os
from pathlib import Path
import random
import secrets
import selectors
import shutil
import signal
import socket
import subprocess
import tempfile
import threading
import time
from urllib.request import urlopen

ROOT = Path(__file__).resolve().parents[1]
DEPLOY = ROOT / "multiplayer-server/deploy"
GODOT = os.environ.get("GODOT_BIN") or shutil.which("godot") or "/Applications/Godot.app/Contents/MacOS/Godot"
PORTS = (7778, 7779, 7780)


def verify_ports():
    with ExitStack() as stack:
        for port in PORTS:
            endpoint = stack.enter_context(socket.socket(socket.AF_INET, socket.SOCK_DGRAM))
            endpoint.bind(("0.0.0.0", port))


def lobby_ports():
    with socket.socket() as lobby, socket.socket() as control:
        for endpoint in (lobby, control):
            endpoint.bind(("127.0.0.1", 0))
        return lobby.getsockname()[1], control.getsockname()[1]


def workers(work):
    output = subprocess.check_output(["/bin/ps", "-axww", "-o", "pid=,stat=,command="], text=True)
    owners = {}
    for row in output.splitlines():
        if f"--log-file {work}/worker-" not in row:
            continue
        pid, stat, command = row.strip().split(None, 2)
        marker = command.rsplit(None, 1)[-1]
        if not stat.startswith("Z") and marker.startswith("--htd-slot="):
            owners[int(marker.split("=", 1)[1].split("-", 1)[0])] = int(pid)
    return owners


class Relay:
    def __init__(self, lossy, stack):
        self.lossy = lossy
        self.selector = stack.enter_context(selectors.DefaultSelector())
        self.stopped = threading.Event()
        self.measure = threading.Event()
        self.blackhole = None
        self.counts = defaultdict(lambda: [0, 0, 0])
        self.bases = []
        for first in range(32000, 42000, 3):
            with ExitStack() as candidate:
                endpoints = [candidate.enter_context(socket.socket(socket.AF_INET, socket.SOCK_DGRAM)) for _ in PORTS]
                try:
                    for offset, endpoint in enumerate(endpoints):
                        endpoint.bind(("127.0.0.1", first + offset))
                except OSError:
                    continue
                stack.enter_context(candidate.pop_all())
            for port, endpoint in zip(PORTS, endpoints):
                self.selector.register(endpoint, selectors.EVENT_READ, port)
            self.bases.append(first)
            if len(self.bases) == 16:
                break
        if len(self.bases) != 16:
            raise RuntimeError("Could not reserve UDP relay ports")
        self.error = None
        self.thread = threading.Thread(target=self.forward)
        self.thread.start()
        stack.callback(self.close)

    def forward(self):
        rng = random.Random(9137)
        pending, sources = [], {}
        order = itertools.count()
        try:
            while not self.stopped.is_set():
                now = time.monotonic()
                while pending and pending[0][0] <= now:
                    _, _, endpoint, data, destination = heapq.heappop(pending)
                    endpoint.sendto(data, destination)
                delay = min(0.01, max(0, pending[0][0] - now)) if pending else 0.01
                for key, _ in self.selector.select(delay):
                    endpoint, port = key.fileobj, key.data
                    data, address = endpoint.recvfrom(65535)
                    if address[1] == port:
                        destination, direction = sources.get(endpoint), "down"
                    else:
                        sources[endpoint] = address
                        destination, direction = ("127.0.0.1", port), "up"
                        if endpoint.getsockname()[1] == self.blackhole:
                            continue
                    if destination is None:
                        continue
                    dropped = self.lossy and rng.random() < 0.025
                    if self.measure.is_set():
                        tally = self.counts[port, direction]
                        tally[0] += len(data) + 28  # UDP plus IPv4 headers
                        tally[1] += 1
                        tally[2] += dropped
                    if not dropped and self.lossy:
                        at = time.monotonic() + rng.uniform(0.015, 0.045)
                        heapq.heappush(pending, (at, next(order), endpoint, data, destination))
                    elif not dropped:
                        endpoint.sendto(data, destination)
        except OSError as error:
            self.error = error

    def close(self):
        self.stopped.set()
        self.thread.join()
        if self.error:
            raise RuntimeError("UDP relay failed") from self.error


def stop(process, grace):
    if process.poll() is None:
        process.terminate()
        try:
            process.wait(timeout=grace)
        except subprocess.TimeoutExpired:
            os.killpg(process.pid, signal.SIGKILL)
            process.wait()


def clean_workers(work):
    for sig in (signal.SIGTERM, signal.SIGKILL):
        for pid in workers(work).values():
            with suppress(ProcessLookupError):
                os.killpg(pid, sig)
        deadline = time.monotonic() + 3
        while workers(work) and time.monotonic() < deadline:
            time.sleep(0.05)
    assert not workers(work), "A test worker did not exit"
    verify_ports()


def summary(values):
    data = sorted(values)
    if not data:
        return None
    return dict(n=len(data), p50=round(data[math.ceil(len(data) * 0.5) - 1], 2),
                p95=round(data[math.ceil(len(data) * 0.95) - 1], 2), max=round(data[-1], 2))


def resources(manager, owned):
    ids = [manager, *owned.values()]
    output = subprocess.check_output(["/bin/ps", "-o", "pid=,%cpu=,rss=", "-p", ",".join(map(str, ids))], text=True)
    samples = {int(pid): (float(cpu), int(rss) / 1024) for pid, cpu, rss in map(str.split, output.splitlines())}
    return {"directory": samples[manager], **{port: samples[pid] for port, pid in owned.items()}}


def consistency(clients):
    tables = [client["snapshots"] for client in clients]
    start = max(table[0][0] for table in tables)
    finish = min(table[-1][0] for table in tables)
    assert finish - start >= 1000
    max_tiles, max_disagree_ms, disagree_since = 0, 0, None
    indices = [0] * len(tables)
    for now in range(math.ceil(start), math.floor(finish), 10):
        latest = []
        for i, table in enumerate(tables):
            while indices[i] + 1 < len(table) and table[indices[i] + 1][0] <= now:
                indices[i] += 1
            sample = table[indices[i]]
            latest.append(((sample[2], *sample[3]), {enemy[0]: enemy[1:] for enemy in sample[4]}))
        if len({state for state, _ in latest}) > 1:
            if disagree_since is None:
                disagree_since = now
            max_disagree_ms = max(max_disagree_ms, now - disagree_since + 10)
        else:
            disagree_since = None
        for (_, a), (_, b) in itertools.combinations(latest, 2):
            max_tiles = max(max_tiles, max((math.dist(a[i], b[i]) / 32 for i in a.keys() & b.keys()), default=0))
    return dict(hp_door_disagreement_ms=max_disagree_ms, enemy_divergence_tiles=round(max_tiles, 3))


def report(work, cpu, counts, duration):
    clients = json.loads((work / "clients.json").read_text())
    result = {name: dict(cpu_pct=summary([sample[name][0] for sample in cpu]),
                         peak_rss_mib=round(max(sample[name][1] for sample in cpu), 2))
              for name in cpu[0]}
    for port in PORTS:
        rows = json.loads((work / f"worker-{port}.json").read_text())
        group = clients[str(port)]
        times = {tick: at for at, _, _, tick in rows if tick >= 0}
        bandwidth = {}
        for direction in ("up", "down"):
            size, packets, dropped = counts[port, direction]
            bandwidth[direction] = dict(mbit_s=round(size * 8 / (duration * 1e6), 3), datagrams=packets, dropped=dropped)
        ages = [at - times[tick] for c in group for at, tick, *_ in c["snapshots"] if tick in times]
        gaps = [b[0] - a[0] for c in group for a, b in zip(c["snapshots"], c["snapshots"][1:])]
        costs = [cost / 1000 for _, cost, *_ in rows]
        result[port].update(ticks_per_s=round((len(rows) - 1) * 1000 / (rows[-1][0] - rows[0][0]), 3),
                            snapshots_per_s=round(len(times) / duration, 2), worker_tick_ms=summary(costs),
                            cost_overruns=sum(cost > 1000 / 60 for cost in costs),
                            intervals_over_25ms=sum(b[0] - a[0] > 25 for a, b in zip(rows, rows[1:])),
                            enemies=[min(row[2] for row in rows), max(row[2] for row in rows)], bandwidth=bandwidth,
                            snapshot_age_ms=summary(ages), update_gap_ms=summary(gaps),
                            input_ack_ms=summary([ms for c in group for ms in c["input_ms"]]),
                            reliable_action_ms=summary([ms for c in group for ms in c["action_ms"]]), **consistency(group))
    result["stack_peak_rss_mib"] = round(max(sum(rss for _, rss in sample.values()) for sample in cpu), 2)
    return result


def run(work, seconds, lossy):
    key = work / "key"
    key.touch(mode=0o600)
    key.write_text(secrets.token_hex(32))
    wrapper = work / "godot-worker"
    wrapper.write_text('#!/bin/sh\nexec "$GODOT_BIN" --log-file "$HTD_TEST_DIR/worker-$MATCH_UDP_PORT.log" '
                       '-s res://tests/release-load-worker.gd "$@"\n')
    wrapper.chmod(0o700)
    state = work / "workers"
    lobby, control = lobby_ports()
    settings = dict(os.environ, GODOT_BIN=GODOT, HTD_TEST_DIR=str(work), HTD_LOAD_SECONDS=str(seconds),
                    HTD_LOAD_RECOVERY=str(int(not lossy)), WORKER_EXECUTABLE=str(wrapper), WORKER_PROJECT=str(ROOT),
                    WORKER_STATE_DIR=str(state), WORKER_HOST="127.0.0.1", LOBBY_HOST="127.0.0.1",
                    WORKER_SUPERVISOR="local", LOBBY_PORT=str(lobby), CONTROL_ENDPOINT=f"127.0.0.1:{control}",
                    ADMISSION_SECRET_FILE=str(key), XDG_DATA_HOME=str(work / "data"))
    for name in ("LOBBY_URL", "ADMISSION_TTL_MS", "DEBUG"):
        settings.pop(name, None)
    health = f"http://127.0.0.1:{lobby}/health"
    with ExitStack() as stack:
        stack.callback(clean_workers, work)
        relay = Relay(lossy, stack)
        (work / "proxy.json").write_text(json.dumps(relay.bases))

        def launch(command, name, grace):
            with (work / name).open("ab") as log:
                process = subprocess.Popen(command, stdin=subprocess.DEVNULL, stdout=log, stderr=subprocess.STDOUT,
                                           cwd=work, env=settings, start_new_session=True)
            stack.callback(stop, process, grace)
            return process

        def start():
            service = launch(["node", str(ROOT / "multiplayer-server/dist/src/server.js")], "directory.log", 12)
            end = time.monotonic() + 10
            while time.monotonic() < end and service.poll() is None:
                with suppress(OSError):
                    with urlopen(health, timeout=0.25) as response:
                        if json.load(response)["ready"]:
                            return service
                time.sleep(0.05)
            raise RuntimeError("Directory did not become healthy")

        manager = start()
        client = launch([GODOT, "--headless", "--path", str(ROOT), "--log-file", str(work / "godot.log"),
                         "-s", "res://tests/release-load-clients.gd", "--", "--headless-client",
                         f"--lobby-url=ws://127.0.0.1:{lobby}"], "client.log", 2)
        deadline = time.monotonic() + seconds + 95
        measured_at, next_sample, next_log_check, cpu, handled = None, 0.0, 0.0, [], set()
        while client.poll() is None and time.monotonic() < deadline:
            now = time.monotonic()
            if now >= next_log_check:
                if "SCRIPT ERROR:" in (work / "client.log").read_text():
                    break
                next_log_check = now + 0.5
            if (work / "measure").exists():
                if measured_at is None:
                    measured_at = now
                    relay.measure.set()
                if now - measured_at >= seconds:
                    relay.measure.clear()
                elif now >= next_sample:
                    owned = workers(work)
                    assert set(owned) == set(PORTS)
                    cpu.append(resources(manager.pid, owned))
                    next_sample = now + 1
            for name in ("restart", "drain", "kill-worker", "reopen", "blackhole"):
                if name in handled or not (work / name).exists():
                    continue
                if name == "restart":
                    before = workers(work)
                    manager.kill()
                    manager.wait(timeout=5)
                    manager = start()
                    assert workers(work) == before
                    (work / "restarted").touch()
                elif name == "drain":
                    subprocess.run([DEPLOY / "drain.sh", "on"], env=settings, check=True)
                    waiting = subprocess.run([DEPLOY / "drain.sh", "wait", "0"], env=settings, capture_output=True)
                    assert waiting.returncode == 1
                elif name == "kill-worker":
                    port = int((work / name).read_text())
                    pid = workers(work)[port]
                    killed_at = time.time() * 1000
                    os.kill(pid, signal.SIGKILL)
                    (work / "killed-at").write_text(str(killed_at))
                elif name == "reopen":
                    subprocess.run([DEPLOY / "drain.sh", "off"], env=settings, check=True)
                else:
                    relay.blackhole = int((work / name).read_text())
                    (work / "blackholed-at").write_text(str(time.time() * 1000))
                handled.add(name)
            time.sleep(0.015)
        log = (work / "client.log").read_text()
        for line in log.splitlines():
            if "SCRIPT ERROR" in line or "verification" in line or line.startswith(
                    ("Directory", "Lobby crash", "All 5", "Freeze_ms", "Voted End", "One-way")):
                print(line, flush=True)
        if client.poll() != 0 or "SCRIPT ERROR:" in log:
            traces = list(work.glob("worker-*.log"))
            for path in traces:
                for line in path.read_text().splitlines():
                    if "ERROR" in line:
                        print(path.name, line, flush=True)
            if cpu and all((work / name).exists() for name in ("clients.json", *(f"worker-{p}.json" for p in PORTS))):
                print(json.dumps(report(work, cpu, relay.counts, seconds), indent=2), flush=True)
            raise RuntimeError(f"Godot release clients did not pass (exit {client.poll()})")
        manager.terminate()
        assert manager.wait(timeout=12) == 0
        assert not workers(work)
        assert not list(state.glob("worker-*.json"))
        return report(work, cpu, relay.counts, seconds)


def interrupt(_sig, _frame):
    raise KeyboardInterrupt


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--mode", choices=("both", "clean", "lossy"), default="both")
    parser.add_argument("--seconds", type=int, default=20)
    args = parser.parse_args()
    if not 6 <= args.seconds <= 60:
        parser.error("--seconds must be between 6 and 60")
    signal.signal(signal.SIGTERM, interrupt)
    verify_ports()
    subprocess.run(["npm", "run", "build", "--prefix", str(ROOT / "multiplayer-server")], check=True)
    for mode in ("clean", "lossy") if args.mode == "both" else (args.mode,):
        with tempfile.TemporaryDirectory(prefix=f"htd-release-{mode}-") as temp:
            measurements = run(Path(temp), args.seconds, mode == "lossy")
            print(json.dumps(dict(mode=mode, seconds=args.seconds, measurements=measurements), indent=2), flush=True)
            for port in PORTS:
                room = measurements[port]
                assert 59.5 <= room["ticks_per_s"] <= 60.5, f"UDP {port} missed the 60 Hz tick target"
                assert 18 <= room["snapshots_per_s"] <= 22, f"UDP {port} missed the 20 Hz snapshot target"
                assert room["reliable_action_ms"] and room["reliable_action_ms"]["n"] == 3, f"UDP {port} lost an action acknowledgement"
                if mode == "clean":
                    assert room["hp_door_disagreement_ms"] <= 200, f"UDP {port} missed the HP and door sync target"
                    assert room["enemy_divergence_tiles"] < 0.5, f"UDP {port} missed the enemy position sync target"
    verify_ports()


if __name__ == "__main__":
    main()
