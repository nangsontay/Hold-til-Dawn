extends SceneTree

const Game = preload("res://game.gd")
const TICKS := 1200
const DT := 1.0 / 60.0
var game = Game.new()
var slots: Array = []
var homes: Array[Vector2] = []
var replacements := 0
var revives := 0


func spawn_slot(i: int):
	if i == 68:
		game._spawn_boss()
		var boss = game.enemies.back()
		boss.pos = game.DOOR_POS[boss.door] + game.DOOR_DIR[boss.door] * 12 * game.T
		return boss
	var roamer := i >= 60
	var kind := Game.GRUNT if roamer else i % 3
	var e = game._spawn(kind, i % 3, game.rng.randf_range(5, 35))
	e.door = game._nearest_active_door(e.pos)
	if roamer:
		e.home = e.pos
		e.wander = e.pos
		e.state = 4
	return e


func setup(room: int) -> void:
	seed(3100 + room)
	game.rng.seed = 3100 + room
	game.k = 4
	game.n0 = 5
	for i in 5:
		game.add_player(i + 1, "Player%d" % i, i % 3)
	game._gen_nodes()
	game.active = [0, 1, 2]
	game._night()
	game.next_wave = INF
	game.boss_spawned = true
	for i in 5:
		var door := i % 3
		var side: Vector2 = game.DOOR_DIR[door].orthogonal()
		var home: Vector2 = game.DOOR_POS[door] + game.DOOR_DIR[door] * 7 * game.T + side * (80 if i >= 3 else -35)
		homes.append(home)
		game._teleport(game.players[i + 1], home)
	for i in 69:
		slots.append(spawn_slot(i))


func advance(tick: int) -> void:
	if tick % 3 == 1:
		for i in 5:
			var p = game.players[i + 1]
			var target: Vector2 = homes[i] + Vector2.from_angle(tick * DT * 0.9 + i) * 72
			var closest = slots[0]
			for e in slots:
				if p.pos.distance_squared_to(e.pos) < p.pos.distance_squared_to(closest.pos):
					closest = e
			game.input(i + 1, target, p.pos.angle_to_point(closest.pos), true, false, false, p.tp, tick)
	game.tick(DT)
	for i in slots.size():
		if slots[i].hp <= 0:
			slots[i] = spawn_slot(i)
			replacements += 1
	for p in game.players.values():
		if p.state != 0:
			p.state = 0
			p.hp = p.maxhp
			revives += 1


func entities() -> Array:
	var result := []
	for actor in game.players.values() + slots:
		result.append([roundi(actor.pos.x), roundi(actor.pos.y), roundi(actor.hp)])
	return result


func percentile(samples: Array[int], p: float) -> int:
	return samples[ceili(samples.size() * p) - 1]


func _init() -> void:
	var args := OS.get_cmdline_user_args()
	if args.size() < 2 or args.size() > 3:
		printerr("Expected room (1..3), then output path, 'pace' or 'live', then optional number of 20 s repeats")
		quit(1)
		return
	var room := int(args[0])
	var live := args[1] == "live"
	var paced := live or args[1] == "pace"
	var directory := OS.get_environment("HTD_LIVE_DIR") if OS.has_environment("HTD_LIVE_DIR") else "/dev/shm"
	var live_path := directory.path_join("htd-live-%d.json" % room)
	var temp_path := live_path + ".tmp"
	var repeats := int(args[2]) if args.size() == 3 else 1
	var file: FileAccess
	if not paced:
		file = FileAccess.open(args[1], FileAccess.WRITE)
	if room < 1 or room > 3 or repeats < 1 or (not paced and (file == null or repeats != 1)):
		quit(1)
		return
	setup(room)
	var samples: Array[int] = []
	var late_frames := 0
	var start := Time.get_ticks_usec()
	for step in range(1, TICKS * repeats + 1):
		var tick := (step - 1) % TICKS + 1
		if step > 1 and tick == 1:
			game = Game.new()
			slots.clear()
			homes.clear()
			setup(room)
		var before := Time.get_ticks_usec()
		advance(tick)
		samples.append(Time.get_ticks_usec() - before)
		if game.phase != "night" or game.enemies.size() != 69 or game.players.size() != 5:
			printerr("Population or phase changed at tick ", tick, " phase ", game.phase)
			quit(2)
			return
		if (file or live) and step % 3 == 0:
			var frame := JSON.stringify({"tick": step, "entities": entities()})
			if live:
				var pending := FileAccess.open(temp_path, FileAccess.WRITE)
				if not pending:
					printerr("Cannot create live frame: ", temp_path)
					quit(3)
					return
				pending.store_string(frame)
				pending.close()
				if DirAccess.rename_absolute(temp_path, live_path) != OK:
					printerr("Cannot publish live frame: ", live_path)
					quit(3)
					return
			else:
				file.store_line(frame)
		if paced:
			var remain := start + roundi(step * DT * 1000000) - Time.get_ticks_usec()
			if remain > 0:
				OS.delay_usec(remain)
			else:
				late_frames += 1
	if file:
		file.close()
	samples.sort()
	print(JSON.stringify({"room": room, "ticks": samples.size(), "mean_us": float(samples.reduce(func(a,b): return a+b)) / samples.size(),
		"p50_us": percentile(samples,.5), "p95_us": percentile(samples,.95), "p99_us": percentile(samples,.99),
		"max_us": samples.back(), "late_frames": late_frames, "replacements": replacements, "revives": revives,
		"cpu_test_wall_s": (Time.get_ticks_usec() - start) / 1000000.0}))
	quit()
