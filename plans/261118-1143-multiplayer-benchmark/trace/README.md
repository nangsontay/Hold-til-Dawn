# Game simulation fixture

Godot runs the repository's `game.gd` with its production playtest tuning and collision map. Each room uses a different deterministic seed (3101–3103). This forced night-4 stress scenario has 5 human player slots, 60 ordinary enemies (20 per archetype), 8 wandering grunts and 1 boss. The production wave cap counts all enemies together and surviving daytime roamers normally lose their wander state at dusk. The fixture deliberately injects the combined population after entering the night. It disables scheduled spawns, fills killed slots with the same category, and revives downed players so load continues. Players follow circular targets through authoritative `input()` every three ticks, aim at the nearest enemy and attack. Real navigation, collisions, damage, projectiles and AI advance at fixed dt = 1/60 s.

## Generate or stream

Run from the repository root. Set `PROJECT` to an imported checkout with matching source/resources and `GODOT` to its Godot 4.7.2 executable. On macOS the latter is usually `/Applications/Godot.app/Contents/MacOS/Godot`. In a new checkout import once with `"$GODOT" --headless --editor --path "$PROJECT" --import`.

```sh
SCRIPT="$PWD/plans/261118-1143-multiplayer-benchmark/trace/generate-trace.gd"
for room in 1 2 3; do
  "$GODOT" --headless --path "$PROJECT" --log-file "/tmp/htd-trace$room.log" \
    -s "$SCRIPT" -- "$room" "/tmp/htd-bench-trace-$room.jsonl" || exit 1
done
```

Each file has 400 JSONL frames over 20 s of game time. Ticks are 3, 6, …, 1200. Every frame holds 74 nearest-integer `[x_px,y_px,hp]` triples. Slots 0–4 hold players, 5–64 regular enemies, 65–72 roamers and 73 the boss. A respawn reuses its slot. The trace omits other fields of the production snapshot.

To run one real simulation for N × 20 s of wall time, replace the output argument with `pace N`. Replace it with `live N` to publish `{"entities":[...],"tick":3}` at 20 Hz. Launch one writer per room. Both writer and receiver must see the same writable directory (default `/dev/shm`, override with `HTD_LIVE_DIR`). Each writer closes a temporary file and renames it over `htd-live-ROOM.json`; readers open that file fresh for each read. Starting a fresh run requires a fresh directory or deletion of that run's stale frame before starting readers. Latest-value handoff may skip frames if the consumer falls behind. The tick continues upward as entity state restarts every 1200 ticks.

```sh
"$GODOT" --headless --path "$PROJECT" --log-file /tmp/htd-live-room1.log \
  -s "$SCRIPT" -- 1 live 6
```

## Verification

Two independent generations on macOS arm64, Godot 4.7.2.stable.official.ed1daf0bf, commit `89d72b452d91fc25dd6e464cffd260e18860bfa3`, produced byte-identical files. Room SHA-256, movement and HP changes compare adjacent sampled frames.

| Room | SHA-256 | Mean slots moving per frame | HP changes | Enemy replacements | Player revives |
| --- | --- | ---: | ---: | ---: | ---: |
| 1 | `829aa93aabd1a35cf7f5e7797950461f4c50c978f8bb5cd24e25a7d9d8378fc5` | 48.60 | 142 | 9 | 3 |
| 2 | `f09b6996e64abc2cc8bd5e48c294cddb87ed3482414f31a84c9ec2e168746de2` | 49.22 | 163 | 11 | 3 |
| 3 | `ca546cb012968ee79051a11a49172528a16a8d66fb9b067674869fd8fd28b597` | 45.06 | 141 | 7 | 2 |

A 40 s live run produced 2,400 ticks. A concurrent reader sampled 61 consecutive published frames, each matching its numbered frame from the offline trace, and the final frame matched tick 1,200 of the second cycle.

## Simulation-only macOS baseline

```sh
python3 plans/261118-1143-multiplayer-benchmark/trace/measure-simulation.py "$GODOT" "$PROJECT"
```

The sampler starts three uncapped host processes concurrently in `pace` mode. Each runs 1,200 ticks against absolute wall-time deadlines. It samples OS user + system CPU time and summed resident memory using macOS `proc_pid_rusage` from 2–18 s after launching the processes, with RSS sampled every ~100 ms. Per-room tick latency includes scripted input selection and validation, the authoritative simulation tick, and population maintenance. Percentiles use nearest-rank across all 1,200 ticks, including cold caches. A late frame completed after its absolute tick deadline, so one stall can leave subsequent ticks late. Snapshot serialization, transport and rendering are excluded. RSS sums may count shared pages more than once. A Docker image build was in progress during this host run.

[Raw baseline measurements](baseline-macos.json) record 67.96% total CPU (one busy core = 100%) and 236.60 MiB mean / 363.16 MiB peak sampled aggregate RSS. Room p95 ticks were 9.011 / 8.239 / 9.880 ms, p99 17.530 / 16.988 / 20.716 ms. These host-only samples characterize shared game code. Use simultaneous measurements inside the target server budget for capacity comparisons.
