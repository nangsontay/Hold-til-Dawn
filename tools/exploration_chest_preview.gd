class_name ExplorationChestPreview
extends Node

func _ready() -> void:
	# try (GDScript has no exceptions; the body runs unguarded)
	var out = "res://.tmp/expedition-chests"
	DirAccess.make_dir_recursive_absolute(out)
	var game = Main.new()
	add_child(game)
	game.set_physics_process(false)
	game.set_process(false)
	game.sim = GameSim.new()
	game.sim.add_player(1, "Scout", 0)
	game.sim.add_player(2, "Engineer", 2)
	game.sim.start_match()
	game.my_id = 1
	var p = game.sim.players[1]
	var chest = game.sim.exploration_chests.filter(func(c): return c.landmark == 5)[0]
	var camera = game.cam
	var names = ["sealed", "unlocking", "opened", "storage", "radio-build", "radio-online", "map"]
	var stage: int = 0
	while stage < names.size():
		var at: Vector2 = chest.pos if stage < 4 else GameSim.WORKBENCH_POS
		game.sim.teleport(p, at + Vector2(0, 32) if stage < 4 else at + Vector2(0, 24))
		if stage == 0:
			game.sim.set_count(p, "chest_key", 2)
		if stage == 1:
			p.inter = true
			game.sim.interact(p, 1.0)
		if stage == 2:
			p.inter = true
			game.sim.interact(p, 1.1)
		game.bag_open = stage == 3
		game.bag_exploration_chest = chest.id if stage == 3 else -1
		if stage == 4:
			for i in range(3):
				game.sim.rescue_components[i] = 1
			p.inter = true
			game.sim.try_build_radio(p, 1.5)
		if stage == 5:
			game.sim.try_build_radio(p, 1.6)
		game.local_pos = p.pos
		game.map_big = stage == 6
		game.sim.clock += 0.5
		game.on_snap(game.sim.snapshot())
		if stage == 2 and (not chest.opened or not game.bag_open or game.bag_exploration_chest != chest.id):
			Cs.fail("Unlocking must automatically open the correct storage panel")
			return
		game.bag_open = stage == 3
		game.bag_exploration_chest = chest.id if stage == 3 else -1
		if stage == 3 and game.bag_slots(Vector2(960, 540), game.sim.snapshot().get("ps").get(1)).size() != 16:
			Cs.fail("Chest UI must have eight bag and eight chest slots")
			return
		game.local_tp = p.tp
		game.pending_inputs.clear()
		for frame in range(20):
			game._process(0.016)
			game.tick_landmark_ground()
			camera.position = at
			camera.zoom = Vector2.ONE * (2 if stage < 4 else 1.5)
			camera.reset_smoothing()
			game.hud.visible = true
			game.banner.visible = false
			game.queue_redraw()
			game.hud.queue_redraw()
			await RenderingServer.frame_post_draw
		var shot = get_viewport().get_texture().get_image()
		shot.save_png("%s/%s.png" % [out, names[stage]])
		print("CHEST PREVIEW OK: " + names[stage])
		stage += 1
	get_tree().quit()
	# catch (Exception E) dropped
