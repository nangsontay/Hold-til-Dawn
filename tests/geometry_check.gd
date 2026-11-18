extends SceneTree
const G = preload("res://game.gd")
const Geometry = preload("res://world_geometry.gd")
var fails := 0


func check(ok: bool, message: String) -> void:
	print(("ok   " if ok else "FAIL ") + message)
	if not ok: fails += 1


func fixture():
	var s = G.new()
	s.add_player(1, "Engineer", 2)
	s.start_match()
	s.nodes.clear()
	s.towers = [null, null, null]
	s.world.static_solids = []
	s.world.rebuild([], [])
	return s


func _init() -> void:
	var first = Geometry.new()
	var second = Geometry.new()
	check(first.deco == second.deco and first.solids == second.solids, "server and renderer build identical scenery/colliders")
	check(first.deco.any(func(d): return d[0] == Vector2(960, 320)), "Godot map scene supplies hand-placed landmarks")
	check(first.solids.any(func(d): return d[0] == Vector2(960, 320) and d[1] == 24.0), "Inspector collider radius is used by simulation")
	check(absf(Geometry.circle_hit(Vector2.ZERO, Vector2(100, 0), Vector2(50, 0), 10) - 0.4) < 0.0001, "swept collision catches a target between frame endpoints")
	check(Geometry.circle_hit(Vector2.ZERO, Vector2(100, 0), Vector2(150, 0), 10) == INF, "segment does not hit objects beyond its endpoint")
	check(Geometry.circle_hit(Vector2.ZERO, Vector2.ZERO, Vector2.ZERO, 10) == 0, "zero-length segment inside a collider is safe")
	var s = fixture()
	s.world.static_solids = [[Vector2(500, 0), 24.0, true]]
	s.world.rebuild([], [])
	var moved: Vector2 = s.move_actor(Vector2(400, 0), Vector2(600, 0), 10)
	check(moved.x <= 466.01 and moved.distance_to(Vector2(500, 0)) >= 33.99, "large movement cannot tunnel through a rock")
	var pos := Vector2(400, 0)
	var clear := true
	for i in 160:
		var way: Vector2 = s.navigate(pos, Vector2(650, 0), 10, false)
		pos = s.move_actor(pos, pos + (way - pos).normalized() * minf(4, pos.distance_to(way)), 10)
		clear = clear and pos.distance_to(Vector2(500, 0)) >= 33.9
	check(clear and pos.distance_to(Vector2(650, 0)) < 8, "navigation reaches the goal around a rock without entering it")
	var p = s.players[1]
	p.pos = Vector2(400, 0)
	p.move_credit = 30.0
	s.input(1, Vector2(1500, 0), 0, false, false, false, p.tp)
	check(p.pos.distance_to(Vector2(400, 0)) <= 30.01, "server limits an arbitrary client position jump")
	var before: Vector2 = p.pos
	s.input(1, Vector2(NAN, 0), NAN, false, false, false, p.tp)
	check(p.pos == before and is_finite(p.aim), "invalid network coordinates/aim do not poison the simulation")
	s = fixture()
	p = s.players[1]
	p.pos = Vector2(650, 0)
	p.hp = 100
	p.invuln = 0
	s.world.static_solids = [[Vector2(500, 0), 24.0, true]]
	s.world.rebuild([], [])
	s.proj = [{"pos": Vector2(400, 0), "vel": Vector2(400, 0), "dmg": 10.0, "ttl": 1.0}]
	s._tick_proj(0.8)
	check(p.hp == 100 and s.proj.is_empty(), "rock absorbs a projectile before it reaches the player")
	s.world.static_solids = []
	s.world.rebuild([], [])
	s.proj = [{"pos": Vector2(400, 0), "vel": Vector2(400, 0), "dmg": 10.0, "ttl": 1.0}]
	s._tick_proj(0.8)
	check(p.hp == 90 and s.proj.is_empty(), "projectile hits a player even when both endpoints are outside the hitbox")
	s._place_tower(0, Vector2(-400, 0))
	var target = s._spawn(G.GRUNT, 0, 0, false)
	target.pos = Vector2(-550, 0)
	s.world.static_solids = [[Vector2(-475, 0), 24.0, true]]
	s.world.rebuild([], s.towers)
	s._tick_towers(0.5)
	check(target.hp == target.maxhp, "tower does not shoot through a rock")
	s.world.static_solids = []
	s.world.rebuild([], s.towers)
	s._tick_towers(0.5)
	check(absf(target.hp - target.maxhp + 4.0) < 0.01, "tower shoots normally when its line of sight clears")
	s.enemies.clear()
	s.towers = [null, null, null]
	s.world.rebuild([], [])
	s.phase = "night"
	p.pos = Vector2(150, 80)
	p.aim = 0
	var enemy = s._spawn(G.GRUNT, 2, 0, false)
	enemy.pos = Vector2(210, 80)
	s._attack(p)
	check(enemy.hp == enemy.maxhp, "melee cannot damage an enemy through a closed base wall")
	p.pos = Vector2(215, 80)
	s._attack(p)
	check(enemy.hp < enemy.maxhp, "unobstructed melee still deals damage")
	s = fixture()
	p = s.players[1]
	p.pos = Vector2(400, 0)
	p.inter = true
	s.nodes = [{"type": 0, "pos": Vector2(432, 0), "amount": 5}]
	s.tick(0.1)
	check(p.activity == "gather" and s.snapshot().ps[1][22] == "gather", "gather animation state is authoritative and included in snapshots")
	p.inter = false
	s.tick(0.1)
	check(p.activity == "idle", "releasing interaction stops the activity animation")
	s = fixture()
	s.world.static_solids = [[Vector2(500, 0), 24.0, true]]
	s.world.rebuild([], [])
	p = s.players[1]
	p.pos = Vector2(650, 0)
	enemy = s._spawn(G.GRUNT, 2, 0, false)
	enemy.pos = Vector2(400, 0)
	enemy.state = 1
	enemy.target = 1
	enemy.anchor = enemy.pos
	clear = true
	for i in 90:
		s._tick_enemies(0.05)
		clear = clear and enemy.pos.distance_to(Vector2(500, 0)) >= 39.9
	check(clear and enemy.pos.x > 560, "enemy follows a route around scenery")
	var bot = s.players[1]
	bot.pos = Vector2(400, 0)
	bot.nav_t = 0.0
	clear = true
	for i in 100:
		bot.nav_t -= 0.05
		G.Bots._move(s, bot, Vector2(650, 0), 0.05)
		clear = clear and bot.pos.distance_to(Vector2(500, 0)) >= 33.5
	check(clear and bot.pos.distance_to(Vector2(650, 0)) < 12, "bot walks around the same obstacle as the player and enemy")
	print("GEOMETRY ALL OK" if fails == 0 else "GEOMETRY FAILED %d" % fails)
	quit(fails)
