extends SceneTree
## The live worker with a seeded night workload and local-only measurements.

class LoadWorker extends "res://main.gd":
	var samples := []
	var started_at := 0.0
	var seeded := false
	var reported := false
	var output := OS.get_environment("HTD_TEST_DIR")
	var duration := OS.get_environment("HTD_LOAD_SECONDS").to_float()

	func _physics_process(dt: float) -> void:
		if sim and sim.phase == "night" and not seeded:
			seeded = true
			sim.rng.seed = 9137
			while sim.enemies.size() < 60:
				sim._spawn(sim.enemies.size() % 3, sim.active[0], 12.0 + sim.enemies.size() % 35, true)
		var before := Time.get_ticks_usec()
		super._physics_process(dt)
		var wall_ms := Time.get_unix_time_from_system() * 1000.0
		if reported or not seeded:
			return
		if started_at == 0.0 and FileAccess.file_exists(output + "/measure"):
			started_at = wall_ms
		if started_at == 0.0:
			return
		var snapshot_tick := frame % 3 == 0 and sim.phase == "night"
		samples.append([wall_ms, Time.get_ticks_usec() - before, sim.enemies.size(),
			roundi(sim.t * 1000.0) if snapshot_tick else -1])
		if wall_ms - started_at >= duration * 1000.0:
			reported = true
			var file := FileAccess.open(output + "/worker-" + OS.get_environment("MATCH_UDP_PORT") + ".json", FileAccess.WRITE)
			file.store_string(JSON.stringify(samples))


func _initialize() -> void:
	var main := LoadWorker.new()
	main.name = "Main"
	root.add_child.call_deferred(main)
