"""Run with python3 tests/match-worker-integration.py /path/to/Godot."""
import base64
import contextlib
import hashlib
import hmac
import json
import os
from pathlib import Path
import queue
import secrets
import select
import signal
import socket
import struct
import subprocess
import sys
import tempfile
import threading
import time

ROOT = Path(__file__).resolve().parents[1]
DOMAIN = "hold-til-dawn/control"
SESSION = "hold-til-dawn/session"


def mac(key, fields):
    return hmac.digest(key, "\n".join(map(str, fields)).encode(), hashlib.sha256)


def read_exact(stream, size):
    data = bytearray()
    while len(data) < size:
        chunk = stream.recv(size - len(data))
        if not chunk:
            raise EOFError
        data.extend(chunk)
    return data


def recv_frame(stream):
    size, = struct.unpack("!I", read_exact(stream, 4))
    assert 0 < size <= 8192
    return json.loads(read_exact(stream, size))


def checked(record, context, counter, key):
    assert record[:6] == ["1", "worker", *context, str(counter)]
    assert hmac.compare_digest(record[-1], mac(key, [DOMAIN, *record[:-1]]).hex())
    return record[6], json.loads(base64.b64decode(record[7], validate=True))


def send_frame(stream, context, counter, key, kind, data, fault=None):
    body = base64.b64encode(json.dumps(data, separators=(",", ":")).encode()).decode()
    fields = ["1", "manager", *context, str(counter), kind, body]
    if fault == "generation":
        fields[3] = "obsolete"
    elif fault == "counter":
        fields[5] = str(counter - 1)
    signature = mac(key, [DOMAIN, *fields]).hex()
    if fault == "mac":
        signature = ("0" if signature[0] != "0" else "1") + signature[1:]
    frame = json.dumps([*fields, signature]).encode()
    stream.sendall(struct.pack("!I", len(frame)) + frame)


def fixture(port, key_file, fault=None):
    host_key = bytes.fromhex(Path(key_file).read_text().strip())
    with socket.socket() as listener:
        listener.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        listener.bind(("127.0.0.1", int(port)))
        listener.listen(4)
        print(json.dumps({"type": "listening", "port": listener.getsockname()[1]}), flush=True)
        sessions = {}
        while True:
            readable, _, _ = select.select([sys.stdin, listener, *sessions], [], [], 1)
            for stream in readable:
                if stream == listener:
                    connection, address = listener.accept()
                    connection.settimeout(1)
                    try:
                        hello = recv_frame(connection)
                        context = hello[2:5]
                        kind, data = checked(hello, context, 1, host_key)
                        assert address[0] == "127.0.0.1" and kind == "hello"
                        assert context[2] in ["7778", "7779", "7780"]
                        nonce = secrets.token_hex(32)
                        send_frame(connection, context, 1, host_key, "challenge",
                                   {"workerNonce": data["nonce"], "managerNonce": nonce}, fault)
                        if fault:
                            connection.close()
                            continue
                        session_key = mac(host_key, [SESSION, "1", *context, data["pid"],
                                                     data["reportSequence"], data["nonce"], nonce])
                        assert checked(recv_frame(connection), context, 2, session_key) == (
                            "register", {"pid": data["pid"], "reportSequence": data["reportSequence"]})
                        send_frame(connection, context, 2, session_key, "registered", {})
                        sessions[connection] = {"context": context, "key": session_key, "sent": 2,
                                                "received": 2, "sequence": data["reportSequence"]}
                    except (EOFError, OSError):
                        connection.close()
                elif stream == sys.stdin:
                    command = sys.stdin.readline()
                    if not command:
                        return
                    msg = json.loads(command)
                    destination = next(s for s, state in sessions.items() if state["context"][0] == msg["room"])
                    state = sessions[destination]
                    state["sent"] += 1
                    send_frame(destination, state["context"], state["sent"], state["key"], msg["type"],
                               msg.get("data", {}), msg.get("fault"))
                else:
                    state = sessions[stream]
                    try:
                        kind, data = checked(recv_frame(stream), state["context"], state["received"] + 1, state["key"])
                        state["received"] += 1
                        print(json.dumps({"type": kind, "room": state["context"][0], "data": data}), flush=True)
                        if kind == "heartbeat":
                            assert data["sequence"] > state["sequence"]
                            state["sequence"] = data["sequence"]
                            state["sent"] += 1
                            send_frame(stream, state["context"], state["sent"], state["key"], "ping", {})
                    except (EOFError, OSError):
                        del sessions[stream]
                        stream.close()


class Process:
    def __init__(self, stack, argv, env=None, capture=True):
        self.proc = subprocess.Popen(argv, cwd=ROOT, env=env, stdin=subprocess.PIPE,
                                     stdout=subprocess.PIPE if capture else subprocess.DEVNULL,
                                     stderr=subprocess.DEVNULL, text=True, bufsize=1)
        stack.callback(self.stop)
        self.lines = queue.Queue()
        if capture:
            threading.Thread(target=self.read, daemon=True).start()

    def read(self):
        for line in self.proc.stdout:
            self.lines.put((time.monotonic(), line.strip()))
        self.lines.put((time.monotonic(), None))

    def until(self, label, predicate, timeout=8):
        until = time.monotonic() + timeout
        while time.monotonic() < until:
            try:
                timestamp, line = self.lines.get(timeout=max(0, until - time.monotonic()))
            except queue.Empty:
                break
            if line is None:
                break
            result = predicate(line)
            if result is not None:
                return timestamp, result
        raise RuntimeError(f"Timed out waiting for {label}")

    def stop(self, abrupt=False):
        if self.proc.poll() is None:
            if abrupt:
                self.proc.kill()
            else:
                self.proc.terminate()
            try:
                self.proc.wait(3)
            except subprocess.TimeoutExpired:
                self.proc.kill()
                self.proc.wait(3)


def event(process, kind, room=None, where=lambda data: True, timeout=8):
    def match(line):
        data = json.loads(line)
        if data["type"] == kind and (room is None or data["room"] == room):
            payload = data.get("data", data)
            if where(payload):
                return payload
        return None
    return process.until(f"{kind} {room or ''}", match, timeout)


def command(manager, room, kind, data=None, fault=None):
    manager.proc.stdin.write(json.dumps({"type": kind, "room": room, "data": data or {}, "fault": fault}) + "\n")
    manager.proc.stdin.flush()


def client_line(client, expected, timeout=8):
    return client.until(expected, lambda line: line if line.startswith(expected) else None, timeout)


def sample_after(client, since):
    timestamp, line = client_line(client, "CLIENT sample")
    while timestamp < since:
        timestamp, line = client_line(client, "CLIENT sample")
    return timestamp, line


def reserve(manager, room, identity, creator=False):
    request = f"req-{secrets.token_hex(8)}"
    command(manager, room, "reserve", {"requestId": request, "identity": identity, "name": identity,
                                       "role": 2, "creator": creator, "expiresAt": int(time.time() * 1000) + 20000})
    return event(manager, "reservation", room, lambda d: d["requestId"] == request)[1]


@contextlib.contextmanager
def upstream_relay(port, worker_port):
    blocked = threading.Event()
    stopping = threading.Event()
    worker = ("127.0.0.1", worker_port)
    with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as relay:
        relay.bind(("127.0.0.1", port))
        relay.settimeout(0.1)

        def forward():
            client = None
            while not stopping.is_set():
                try:
                    data, source = relay.recvfrom(65535)
                except socket.timeout:
                    continue
                if source == worker:
                    if client is not None:
                        relay.sendto(data, client)
                else:
                    client = source
                    if not blocked.is_set():
                        relay.sendto(data, worker)

        thread = threading.Thread(target=forward)
        thread.start()
        try:
            yield blocked
        finally:
            stopping.set()
            thread.join()


def run(godot):
    for port in (7777, 7778, 7779, 7780):
        with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as probe:
            probe.bind(("0.0.0.0", port))
    with tempfile.TemporaryDirectory(prefix="htd-worker-check-") as temp, contextlib.ExitStack() as stack:
        key_file = Path(temp, "host-key")
        key_file.touch(mode=0o600)
        key_file.write_text(secrets.token_hex(32) + "\n")

        def manager_at(port, fault=None):
            fixture_args = ["--fixture", str(port), str(key_file), *([fault] if fault else [])]
            proc = Process(stack, [sys.executable, str(Path(__file__).resolve()), *fixture_args])
            return proc, event(proc, "listening")[1]["port"]

        def worker(room, udp, tcp, generation="current", observe_peers=False):
            env = dict(os.environ, MATCH_ROOM_ID=room, MATCH_ROOM_NAME=room, MATCH_GENERATION=generation,
                       MATCH_UDP_PORT=str(udp), CONTROL_ENDPOINT=f"127.0.0.1:{tcp}", ADMISSION_SECRET_FILE=str(key_file))
            script = ["--script", "tests/admission-client.gd"] if observe_peers else []
            return Process(stack, [godot, "--headless", "--log-file", f"{temp}/worker-{room}.log",
                                  "--path", ".", *script, "--", "--match-worker"], env, capture=observe_peers)

        def client(room, udp, kind, ticket=None, expected=None, **options):
            config = {"room": room, "port": udp, "kind": kind, "generation": "current", "expected": expected, **options}
            if ticket:
                config |= {"ticket": ticket["ticketId"], "secret": ticket["secret"]}
            if kind == "wrong_secret":
                config["secret"] = secrets.token_hex(32)
            env = dict(os.environ, ADMISSION_TEST_CONFIG=json.dumps(config))
            return Process(stack, [godot, "--headless", "--log-file", f"{temp}/client-{room}-{kind}.log",
                                  "--path", ".", "--script", "tests/admission-client.gd"], env)

        legacy = Process(stack, [godot, "--headless", "--log-file", f"{temp}/legacy.log", "--path", ".", "--",
                                 "--server"])
        legacy.until("direct UDP server", lambda line: line if "dedicated server on UDP 7777" in line else None)
        legacy_client = client("legacy", 7777, "legacy")
        assert legacy_client.proc.wait(8) == 0, "Direct host keeps demo bots and solo start"
        client_line(legacy_client, "CLIENT legacy_started")
        legacy.stop()

        for fault in ("mac", "counter", "generation"):
            bad_manager, endpoint = manager_at(0, fault)
            rejected_worker = worker(f"bad-{fault}", 7780, endpoint)
            assert rejected_worker.proc.wait(8) == 1, "Invalid/unauthenticated control must fail closed"
            bad_manager.stop()

        manager, port = manager_at(0)
        a = worker("north", 7778, port, observe_peers=True)
        a_ready = event(manager, "ready", "north")[1]
        b = worker("south", 7779, port)
        b_ready = event(manager, "ready", "south")[1]
        upstream = contextlib.ExitStack()
        stack.callback(upstream.close)
        blocked = upstream.enter_context(upstream_relay(7780, 7778))
        assert a_ready["pid"] != b_ready["pid"] and not a_ready["creatorAdmitted"] and a_ready["humans"] == 0
        collision = worker("collision", 7778, port)
        assert collision.proc.wait(4) == 1, "Existing UDP binding must fail closed"
        assert reserve(manager, "north", "NotCreator")["code"] == "creator_pending"

        alice_ticket = reserve(manager, "north", "Alice", creator=True)
        assert 19000 <= alice_ticket["ttlMs"] <= 20000
        wrong_room = client("south", 7779, "stale_ticket", alice_ticket, "invalid_ticket")
        assert wrong_room.proc.wait(8) == 0, "Another room rejects the unused creator ticket"
        alice = client("north", 7780, "play", alice_ticket)
        client_line(alice, "CLIENT result admitted")
        assert event(manager, "creator_admitted", "north")[1]["name"] == "Alice"
        for kind, rejection in (("wrong_secret", "invalid_proof"), ("wrong_content", "incompatible_content")):
            carol_ticket = reserve(manager, "south", "Carol", creator=True)
            bad_client = client("south", 7779, kind, carol_ticket, rejection)
            assert bad_client.proc.wait(8) == 0, f"ENet {kind} rejection"
            command(manager, "south", "cancel", {"requestId": carol_ticket["requestId"]})
            assert event(manager, "cancellation", "south")[1]["code"] == "cancelled"
            assert b.proc.wait(4) == 0, "Cancelled unpublished creator releases its worker port"
            b = worker("south", 7779, port)
            event(manager, "ready", "south")
        carol = client("south", 7779, "play", reserve(manager, "south", "Carol", creator=True))
        client_line(carol, "CLIENT result admitted")
        first_report, first_state = event(manager, "heartbeat", "south", lambda d: d["humans"] == 1)
        next_report, next_state = event(manager, "heartbeat", "south", lambda d: d["humans"] == 1)
        assert next_state["sequence"] == first_state["sequence"] + 1 and 0.85 <= next_report - first_report <= 1.2
        assert 0 < next_state["maxFrameMs"] < 250
        os.kill(b.proc.pid, signal.SIGSTOP)
        try:
            time.sleep(0.7)
        finally:
            os.kill(b.proc.pid, signal.SIGCONT)
        event(manager, "heartbeat", "south", lambda d: d["maxFrameMs"] >= 600)
        silent = client("north", 7778, "silent")
        _, silent_connected = client_line(silent, "CLIENT connected")
        time.sleep(0.3)
        command(manager, "north", "reserve", {"requestId": "inspect-authority"})
        event(manager, "reservation", "north", lambda d: d["requestId"] == "inspect-authority")
        _, untouched = event(manager, "heartbeat", "north")
        assert (untouched["state"], untouched["mode"], untouched["name"], untouched["humans"],
                untouched["available"], untouched["bots"], untouched["pending"]) == ("waiting", 1, "Alice", 1, 4, 0, 0)
        waiting_ticket = reserve(manager, "north", "Waiting")
        proof_file = Path(temp, "send-proof")
        waiting = client("north", 7778, "deferred", waiting_ticket, "room_started", proofFile=str(proof_file))
        client_line(waiting, "CLIENT proof_paused")
        bob = client("north", 7778, "play", reserve(manager, "north", "Bob"))
        client_line(bob, "CLIENT result admitted")
        dana = client("north", 7778, "play", reserve(manager, "north", "Dana"))
        client_line(bob, "CLIENT state playing/")
        event(manager, "reservation_cancelled", "north",
              lambda d: d == {"requestId": waiting_ticket["requestId"], "reason": "room_started"})
        proof_file.touch()
        assert waiting.proc.wait(4) == 0, "Start invalidates an assigned, already challenged ticket"
        client_line(bob, "CLIENT movement_validated")
        _, sample = client_line(bob, "CLIENT sample")
        _, _, seed, match_time, _ = sample.split()
        assert int(seed) != 0 and float(match_time) > 350
        assert silent.proc.wait(8) == 0, "Pre-admission peer got no snapshots and timed out"
        client_line(silent, "CLIENT preadmission_timeout")
        client_line(a, "WORKER peer_removed " + silent_connected.split()[-1])
        client_line(carol, "CLIENT state waiting/")

        _, old = event(manager, "heartbeat", "north", lambda d: d["state"] == "playing")
        manager.stop(abrupt=True)
        sampled_at, before_restart = sample_after(bob, time.monotonic())
        _, after = sample_after(bob, sampled_at + 2)
        assert before_restart.split()[2] == after.split()[2] == seed
        assert float(after.split()[3]) < float(before_restart.split()[3]), "Simulation progresses without manager"
        snapshot_count = int(after.split()[4]) - int(before_restart.split()[4])
        game_seconds = float(before_restart.split()[3]) - float(after.split()[3])
        rate = snapshot_count / game_seconds
        assert 18 <= rate <= 22, f"Expected about 20 snapshots/s, received {rate:.1f} snapshots/s over {game_seconds:.2f}s"
        manager, _ = manager_at(port)
        restored = event(manager, "heartbeat", "north")[1]
        assert restored["pid"] == old["pid"] and restored["sequence"] > old["sequence"] and restored["humans"] == 3
        command(manager, "north", "reserve", {"requestId": "stale", "identity": "Stale", "name": "Stale", "role": 2,
                                               "creator": False, "expiresAt": int(time.time() * 1000) + 20000},
                fault="generation")
        event(manager, "ready", "north", lambda d: d["humans"] == 3 and d["pending"] == 0)
        assert a.proc.poll() is None
        assert reserve(manager, "north", "Latecomer")["code"] == "room_started"

        # A short upstream burst leaves the match running, then a longer recoverable burst pauses it.
        blocked.set()
        time.sleep(0.35)
        blocked.clear()
        time.sleep(0.2)
        burst = time.monotonic()
        blocked.set()
        frozen, _ = client_line(bob, "CLIENT state timeout/connection")
        assert 0 <= frozen - burst < 1.0, "Upstream loss freezes within one second"
        _, retained = event(manager, "heartbeat", "north", lambda d: d["state"] == "timeout")
        assert retained["humans"] == 3 and retained["bots"] == 0 and retained["name"] == "Alice"
        blocked.clear()
        transition, line = client_line(bob, "CLIENT state ")
        assert line == "CLIENT state playing/" and transition - burst < 2, "Recovered traffic resumes without a vote"

        # Leave the upstream link blocked until ENet confirms the departure.
        # Let ENet acknowledge its ping retries before starting a new outage.
        time.sleep(1.0)
        gone = time.monotonic()
        blocked.set()
        frozen_at, _ = client_line(bob, "CLIENT state timeout/connection")
        assert 0 <= frozen_at - gone < 1.0, "Active player disappearance must freeze within one second"
        confirmed, _ = client_line(bob, "CLIENT state timeout/continue")
        assert 1.1 < confirmed - gone < 4.5, "Only ENet timeout finalizes the departure before the 5 s client deadline"
        print(f"ENet upstream loss: pause at {frozen_at - gone:.3f}s, departure confirmed at {confirmed - gone:.3f}s")
        _, succession = event(manager, "heartbeat", "north", lambda d: d["state"] == "timeout" and d["humans"] == 2)
        assert succession["name"] == "Bob" and succession["humans"] == 2
        client_line(bob, "CLIENT state timeout/bot")
        countdown, _ = client_line(bob, "CLIENT state timeout/countdown")
        resumed, _ = client_line(bob, "CLIENT state playing/")
        assert 2.85 <= resumed - countdown <= 3.3, "Resume countdown lasts three seconds"
        assert carol.proc.poll() is None and b.proc.poll() is None, "Second room is unaffected"
        alice.stop(abrupt=True)
        upstream.close()
        empty = worker("empty", 7780, port)
        empty_started, _ = event(manager, "ready", "empty")
        for room, proc in (("north", a), ("south", b)):
            command(manager, room, "close")
            assert event(manager, "closing", room)[1]["code"] == "closed"
            assert proc.proc.wait(5) == 0
        reused = worker("south", 7779, port, generation="next")
        event(manager, "ready", "south")
        stale_client = client("south", 7779, "stale_ticket", carol_ticket, "invalid_ticket")
        assert stale_client.proc.wait(8) == 0, "Reused port rejects a prior generation's ticket"
        command(manager, "south", "close")
        assert reused.proc.wait(5) == 0
        assert empty.proc.wait(65) == 0 and 59 <= time.monotonic() - empty_started <= 65, "Empty worker exits after sixty seconds"
        print("match-worker-integration: legacy host, control, ENet, two rooms, recovery, freeze and shutdown passed")


if __name__ == "__main__":
    if sys.argv[1] == "--fixture":
        fixture(*sys.argv[2:])
    else:
        run(sys.argv[1])
