class_name HarvestCheck
extends Node

func check(ok: bool, message) -> void:
	if not ok:
		Cs.fail(message)
		return
	print("ok   ", message)

func _ready() -> void:
	# try (GDScript has no exceptions; the body runs unguarded)
	run()
	get_tree().quit()
	# catch (Exception Error) dropped

func run() -> void:
	var s = GameSim.new()
	s.add_player(1, "Engineer", 2)
	s.start_match()
	var w = s.world
	var visible_resources = {}
	for n in s.nodes:
		Cs.set_add(visible_resources, [Vector2(n.get("pos")), Cs.s(n.get("sprite", ""))])
	check(w.harvest_sites.all(func(site): return visible_resources.has([site.pos, site.sprite])), "every natural scenery resource is an actual gatherable node")
	var lm_props = Cs.set_of(w.lm_props)
	check(w.deco.all(func(d): return (Harvestables.type_of(Cs.s(d[1])) < 0 or lm_props.has([Vector2(d[0]), Cs.s(d[1])])) and not Cs.s(d[1]).begins_with("dead_")), "no decorative trees, rocks, herbs or dead trees remain in world scenery, apart from the landmarks' own props")
	check(not w.harvest_sites.any(func(site): return lm_props.has([site.pos, site.sprite])), "a landmark's own props cannot be gathered")
	check(w.deco.filter(func(d): return lm_props.has([Vector2(d[0]), Cs.s(d[1])])).size() == w.lm_props.size(), "every landmark prop stands in the scenery")
	for site in w.harvest_sites:
		for stage in range(6):
			var name = site.sprite if stage == 0 else Harvestables.stage_sprite(site.sprite, stage)
			check_asset(name)
	print("ok   full, damaged and depleted sprites exist for all %s scenery resource sites" % [w.harvest_sites.size()])
	var cave_ore: int = w.harvest_sites.filter(func(site): return site.type == 3 and w.in_cave_floor(site.pos)).size()
	var cave_stone: int = w.harvest_sites.filter(func(site): return site.type == 1 and w.in_cave_region(site.pos) and not w.in_cave_floor(site.pos)).size()
	check(cave_ore >= 18, "the cave contains abundant gatherable titanium: %s nodes" % [cave_ore])
	check(cave_stone >= 20, "loose stone surrounds the cave chamber: %s nodes" % [cave_stone])
	var garden_herbs = Array(w.harvest_sites.filter(func(site): return site.type == 2 and w.garden_bounds.has_point(site.pos))).duplicate()
	check(garden_herbs.size() == 36 and w.garden_beds.all(func(b): return garden_herbs.filter(func(h): return b.has_point(h.pos)).size() == 6), "each of six garden beds retains six herbs after the 25 percent reduction")
	for pick in [0, 1, 2, 3, 4]:
		var site = garden_herbs[0] if pick == 4 else w.harvest_sites.filter(func(n): return n.type == pick and n.radius <= 20 and IslandMap.on_grass(n.pos, 64))[0]
		var type: int = site.type
		var n = site.create_node()
		s.nodes = [n]
		w.static_solids = []
		w.rebuild(s.nodes, s.towers)
		var revision: int = int(w.revision)
		var before = s.snapshot_nodes()[0]
		n["amount"] = 4
		var changed = s.snapshot_nodes()[0]
		check(changed[3] == site.sprite and float(changed[4]) == site.radius, "snapshot preserves the scenery sprite and footprint")
		check(not is_same(before, changed) and int(before[2]) == 5 and int(changed[2]) == 4, "resource snapshot changes without mutating its previous record")
		w.rebuild(s.nodes, s.towers)
		check(int(w.revision) == revision, "partial harvesting does not rebuild unchanged collision/navigation")
		n["amount"] = 5
		var player = s.players[1]
		player.pos = site.pos + Vector2.DOWN * (site.radius + 11)
		player.inter = true
		player.gather_acc = 0
		player.gather_pos = player.pos
		var had: int = s.count(player, GameSim.RES[type])
		for i in range(16 * 60):
			s.tick(1.0 / 60)
		player.inter = false
		check(int(n["amount"]) == 0 and s.count(player, GameSim.RES[type]) == had + 5, "holding E fully harvests a scenery " + Cs.s(GameSim.RES[type]) + " resource")
		w.rebuild(s.nodes, s.towers)
		if site.radius > 0:
			check(is_inf(float(w.first_hit(site.pos, site.pos, 1))), "exhausted scenery resource no longer blocks movement")
		s.dawn()
		w.rebuild(s.nodes, s.towers)
		check(int(n["amount"]) == 5, "scenery resource regenerates at dawn")
	print("HARVEST ALL OK")

static func check_asset(name) -> void:
	if not FileAccess.file_exists("res://art/nature/%s.png" % [name]) and not FileAccess.file_exists("res://art/landmarks/%s.png" % [name]):
		Cs.fail("Missing resource sprite: " + name)
		return
