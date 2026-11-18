extends SceneTree
## Headless check of the boss prototype: godot --headless --path . -s prototypes/boss_check.gd

const Arena := preload("res://prototypes/boss_arena.gd")
const T := 32.0
const FAR := Vector2(-17, 11) * T  # bottom-left corner: out of the boss's 8-tile sight
var fails := 0


func check(ok: bool, what: String) -> void:
	print(("ok   " if ok else "FAIL ") + what)
	if not ok:
		fails += 1


func arena(kind: int, player: Vector2, team := 1):
	var a = Arena.new()
	a.team = team
	a.setup(kind)
	a.p.pos = player
	return a


func run(a, seconds: float, until := Callable()) -> void:
	for i in int(seconds * 60):
		a.step(1.0 / 60)
		if until.is_valid() and until.call():
			return


func _init() -> void:
	var a = arena(Arena.IRONHIDE, FAR, 4)
	check(absf(a.boss.maxhp - 2730) < 1, "FML-02: Ironhide, night 2, team of 4 = 2730 HP")
	a.night = 4
	a.setup(Arena.JUGGERNAUT)
	check(absf(a.boss.maxhp - 6094) < 1, "FML-02: Juggernaut, night 4, team of 4 = 6094 HP")
	a.free()

	# Juggernaut: the charge hits a player on the line and misses one who steps aside.
	a = arena(Arena.JUGGERNAUT, Vector2(0, -1) * T)
	run(a, 4.0, func(): return a.boss.act == "aim")
	check(a.boss.act == "aim" and a.tele.any(func(tl): return tl.shape == "line"), "Juggernaut telegraphs its charge with a line")
	run(a, 1.5)
	check(a.p.hp <= Arena.P_HP - 35, "charge hits a player standing on the line")
	a.free()
	a = arena(Arena.JUGGERNAUT, Vector2(0, -1) * T)
	run(a, 4.0, func(): return a.boss.act == "aim")
	a.p.pos.x += 4 * T
	run(a, 1.5)
	check(a.p.hp == Arena.P_HP, "charge misses a player who stepped aside")
	run(a, 2.0, func(): return a.boss.act == "recover")
	check(a.boss.act == "recover", "Juggernaut recovers 2 s after the slam that ends its charge")
	a.free()

	# Broodmother: at most 4 summons alive, gone with the boss.
	a = arena(Arena.BROOD, FAR)
	run(a, 40.0)
	check(a.grunts.size() == 4, "Broodmother keeps at most 4 Grunts alive (has %d)" % a.grunts.size())
	a.boss.hp = 1.0
	a._hit(a.boss, 26.0)
	check(a.boss == null and a.grunts.is_empty(), "summons vanish when the Broodmother dies")
	a.free()

	# Bio-Cannon: ring of 8 after a 1 s glow, keeps its distance.
	a = arena(Arena.CANNON, Vector2(0, 4) * T)
	run(a, 4.0, func(): return a.proj.size() > 0)
	check(a.proj.size() == 8, "Bio-Cannon fires a ring of 8 projectiles")
	run(a, 2.0)
	check(a.p.hp < Arena.P_HP, "a volley shot hits a player who does not move into a gap")
	check(a.boss.pos.distance_to(a.p.pos) > 4 * T, "Bio-Cannon keeps about 5 tiles away")
	a.free()

	# Burrower: immune underground, surfaces at least 1.5 tiles from its target.
	a = arena(Arena.BURROWER, Vector2(0, 4) * T)
	run(a, 4.0, func(): return a.boss.act == "under")
	var hp: float = a.boss.hp
	a._hit(a.boss, 26.0)
	check(a.boss.act == "under" and a.boss.hp == hp, "Burrower cannot be hit underground")
	run(a, 4.0, func(): return a.boss.act == "climb")
	check(a.boss.act == "climb" and a.boss.pos.distance_to(a.p.pos) >= 1.5 * T - 1.0, "Burrower surfaces at least 1.5 tiles from its target")
	check(a.p.hp <= Arena.P_HP - 35, "the surfacing hits a player who stayed on the dirt marker")
	a.free()

	# Gatebreaker: one 120 blow every 8 s.
	a = arena(Arena.GATEBREAKER, FAR)
	run(a, 30.0, func(): return a.door_hp < Arena.DOOR_HP)
	check(absf(a.door_hp - 380) < 0.01, "Gatebreaker hits the door for 120 in one blow")
	run(a, 7.5)
	check(absf(a.door_hp - 380) < 0.01, "... and not again for 8 s")
	run(a, 1.0)
	check(absf(a.door_hp - 260) < 0.01, "... then 120 again")
	a.free()

	# Phantom: cloaks, a hit reveals it; the ambush telegraph reveals it too.
	a = arena(Arena.PHANTOM, FAR)
	run(a, 3.0)
	check(a.boss.cloak > 0, "Phantom cloaks")
	a._hit(a.boss, 26.0)
	check(a.boss.cloak <= 0, "a hit reveals the Phantom")
	a.free()
	a = arena(Arena.PHANTOM, Vector2(0, 7.5) * T)
	var has_arc := func(): return a.tele.any(func(tl): return tl.shape == "arc")
	run(a, 3.0, has_arc)
	check(has_arc.call() and a.boss.cloak <= 0, "Phantom ambushes with an arc telegraph and is visible")
	a.free()

	# Ironhide: 30% from the front, full from behind or while staggered.
	a = arena(Arena.IRONHIDE, FAR)
	var b = a.boss
	b.face = Vector2.UP
	a.p.pos = b.pos + Vector2(0, -2) * T
	a._hit(b, 26.0)
	check(absf(b.maxhp - b.hp - 7.8) < 0.01, "Ironhide takes 30% from the front")
	a.p.pos = b.pos + Vector2(0, 2) * T
	hp = b.hp
	a._hit(b, 26.0)
	check(absf(hp - b.hp - 26.0) < 0.01, "Ironhide takes full damage from behind")
	a.p.pos = b.pos + Vector2(0, -2) * T
	run(a, 1.2)
	check(b.act == "stagger", "Ironhide is staggered after its slam")
	hp = b.hp
	a._hit(b, 26.0)
	check(absf(hp - b.hp - 26.0) < 0.01, "a staggered Ironhide takes full damage from the front")
	a.free()

	# Soak: every boss fights an attacking player for 60 s without errors.
	for kind in 7:
		a = arena(kind, Vector2(0, 5) * T)
		a.attack = true
		for i in 3600:
			if a.boss != null:
				a.aim = (a.boss.pos - a.p.pos).angle()
			a.step(1.0 / 60)
		check(a.p.hp < Arena.P_HP or a.boss == null or a.door_hp < Arena.DOOR_HP, "%s fight runs 60 s" % Arena.BOSSES[kind][1])
		a.free()

	print("%d failed" % fails if fails else "all boss checks passed")
	quit(1 if fails else 0)
