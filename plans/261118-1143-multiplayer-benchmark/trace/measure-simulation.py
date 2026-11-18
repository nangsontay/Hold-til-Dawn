"""Measure three local paced Godot processes on macOS. Python standard library only."""
import ctypes
import json
import statistics
import subprocess
import sys
import tempfile
import time
from pathlib import Path

godot, project = sys.argv[1:3]
script = Path(__file__).resolve().with_name("generate-trace.gd")
proc = ctypes.CDLL("/usr/lib/libproc.dylib", use_errno=True)
kernel = ctypes.CDLL("/usr/lib/libSystem.B.dylib")
ratio = (ctypes.c_uint32 * 2)()
assert kernel.mach_timebase_info(ratio) == 0
to_sec = ratio[0] / ratio[1] / 1e9


def usage(pid):
    # macOS sys/resource.h rusage_info_v0: UUID, followed by ten uint64 fields.
    values = (ctypes.c_uint64 * 12)()
    if proc.proc_pid_rusage(pid, 0, ctypes.byref(values)):
        raise OSError(ctypes.get_errno(), "proc_pid_rusage")
    return (values[2] + values[3]) * to_sec, values[8] / (1024 ** 2)


with tempfile.TemporaryDirectory() as tmp:
    children = []
    outputs = []
    try:
        for room in range(1, 4):
            output = open(f"{tmp}/{room}.out", "w+")
            outputs.append(output)
            children.append(subprocess.Popen([
                godot, "--headless", "--log-file", f"{tmp}/{room}.log", "--path", project,
                "-s", str(script), "--", str(room), "pace"
            ], stdout=output))
        # Measure only the mutually active central window, after startup and before exit.
        time.sleep(2)
        start = time.monotonic()
        initial = [usage(c.pid) for c in children]
        rss = []
        while time.monotonic() - start < 16:
            rss.append(sum(usage(c.pid)[1] for c in children))
            time.sleep(.10)
        final = [usage(c.pid) for c in children]
        elapsed = time.monotonic() - start
        for child in children:
            if child.wait():
                raise RuntimeError("Godot simulation exited unsuccessfully")
        metrics = []
        for output in outputs:
            output.seek(0)
            metrics.append(next(json.loads(line) for line in output if line.startswith("{")))
        print(json.dumps({
            "window_s": elapsed, "cpu_pct_per_room": [round(100 * (b[0]-a[0])/elapsed, 2) for a,b in zip(initial,final)],
            "mean_aggregate_rss_mib": statistics.mean(rss), "peak_aggregate_rss_mib": max(rss),
            "rss_samples": len(rss), "tick_results": metrics
        }, indent=2))
    finally:
        for child in children:
            if child.poll() is None:
                child.terminate()
            child.wait()
        for output in outputs:
            output.close()
