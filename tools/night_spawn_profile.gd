class_name NightSpawnProfile
extends Node
# Reproducible CPU sample, independent of drawing/GPU load. Run with -- --night=1 or --night=5.

func _ready() -> void:
	# try (GDScript has no exceptions; the body runs unguarded)
	var night: int = 1
	var every_frame: bool = OS.get_cmdline_user_args().find("--every-frame") >= 0
	var hz: int = 60
	var steps: int = 1800
	for arg in OS.get_cmdline_user_args():
		if arg.begins_with("--night="):
			night = arg.substr(8).to_int()
	var sim = GameSim.new()
	for i in range(1, 4):
		sim.add_player(i, "Profile", i - 1)
	sim.rng.seed = 42
	sim.start_match()
	sim.rng.seed = 42
	sim.enemy_body_interval = 0.0 if every_frame else 0.05
	sim.k = night
	sim.radio_built = true
	sim.active = [0, 1, 2]
	sim.night()
	sim.boss_spawned = true
	for p in sim.players.values():
		p.pos = Vector2(0, 2000)
	var spawn_ms: float = 0
	var ai_ms: float = 0
	var total: float = 0
	var bytes: int = Performance.get_monitor(Performance.MEMORY_STATIC)
	var born: int = 0
	var max_births: int = 0
	# Warm the dynamic call sites without creating an enemy or navigation grid.
	sim.tick_enemy_simulation(1.0 / hz)
	sim.tick_waves()
	for i in range(steps):
		sim.elapsed = 60.0 + i / float(hz)
		var before_id: int = int(sim.next_id)
		var t: int = Time.get_ticks_usec()
		sim.tick_waves()
		var spawn: float = Cs.TimeSpan.new(Time.get_ticks_usec() - t).total_milliseconds
		t = Time.get_ticks_usec()
		sim.tick_enemy_simulation(1.0 / hz)
		var ai: float = Cs.TimeSpan.new(Time.get_ticks_usec() - t).total_milliseconds
		spawn_ms = maxf(spawn_ms, spawn)
		ai_ms = maxf(ai_ms, ai)
		total += spawn + ai
		var births: int = int(sim.next_id) - before_id
		born += births
		max_births = maxi(max_births, births)
	print("NIGHT PROFILE night=%s ai=%s body_steps=%s births=%s max_births/tick=%s worst_spawn_ms=%.3f worst_ai_ms=%.3f avg_spawn_ai_ms=%.3f managed_mb=%.2f" % [night, "60Hz" if every_frame else "20Hz", sim.enemy_body_steps, born, max_births, spawn_ms, ai_ms, total / steps, (Performance.get_monitor(Performance.MEMORY_STATIC) - bytes) / 1048576.0])
	get_tree().quit()
	# catch (Exception E) dropped
