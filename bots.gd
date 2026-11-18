extends RefCounted
## Demo bots and managed-match replacements. They run on the server inside GameSim and
## follow the same rules as people: a bot only sets what a player's input sets (where it stands, aim,
## attack, hold E) or calls the same actions (bandage, flask, craft). No planning, just priorities:
##   dodge a boss slam > revive a downed teammate > heal (Healer) > fight > repair a door (Engineer)
##   > by day gather, then craft at the workbench near dusk; at dusk and night hold the attacked door.
## Bots share obstacle collision and navigation with the authoritative simulation.

const T := GameSim.T
const GATHER := [0, 2, 1]  # favourite resource by role: Scout wood, Healer herb, Engineer stone
const HOME_AT := 40.0  # seconds of daylight left when bots walk back to the workbench
const SIGHT_DAY := 6.0 * T  # by day a bot fights what comes this close
const LEASH := 8.0 * T  # at dusk and night a bot fights within this distance of its post
const FLASK_AT := 0.55  # the Healer throws a flask at a teammate below this share of HP
const BANDAGE_AT := 0.4
const STACK_LIMIT := 20
const CARRY_SLOTS := 12
const STORAGE_SLOTS := 40
const WORKBENCH := Vector2(0, 1.6 * T)
const BODY := 0.4 * T  # same circle the client uses against walls
const BENCH_SPOTS := [Vector2(-2.5, 1.6), Vector2(2.5, 1.6), Vector2(-3.5, 0.6), Vector2(3.5, 0.6)]  # tiles


static func add(sim) -> void:
	## A bot takes the role the team lacks most: an Engineer first if there is none, then Healer, Scout.
	if sim.phase != "lobby" or sim.players.size() >= 5:
		return
	var count := [0, 0, 0]
	for p in sim.players.values():
		count[p.role] += 1
	var role := 2
	if count[2] > 0:
		role = 1 if count[1] <= count[0] else 0
	var id := -1
	while sim.players.has(id):
		id -= 1
	var pname: String = "Bot " + GameSim.ROLES[role]
	for p in sim.players.values():
		if p.name == pname:
			pname += " %d" % -id
			break
	sim.add_player(id, pname, role)
	if sim.players.has(id):
		sim.players[id].bot = true


static func remove(sim) -> void:
	if sim.phase != "lobby":
		return
	for i in range(sim.order.size() - 1, -1, -1):
		var id: int = sim.order[i]
		if sim.players[id].bot:
			sim.remove_player(id)
			return


static func replace(p: Dictionary) -> void:
	p.bot = true
	p.replacement = true
	p.nav_t = 0.0
	p.bot_node = -1


static func free_stack_space(inventory: Dictionary, item: String, slots: int) -> int:
	for amount: int in inventory.values():
		slots -= ceili(float(amount) / STACK_LIMIT)
	var remainder: int = int(inventory.get(item, 0)) % STACK_LIMIT
	return maxi(0, slots) * STACK_LIMIT + (STACK_LIMIT - remainder if remainder > 0 else 0)


static func tick(sim, dt: float) -> void:
	var playing: bool = sim.phase in ["day", "dusk", "night"]
	var slot := 0
	for p in sim.players.values():
		if not p.bot:
			continue
		p.atk = false
		p.inter = false
		p.ent = false
		p.nav_t = p.get("nav_t", 0.0) - dt
		p.bot_cd -= dt
		if playing and p.state == 0:
			if p.get("replacement", false):
				_think_replacement(sim, p, dt)
			else:
				_think(sim, p, slot, dt)
		slot += 1


static func _think_replacement(sim, p, dt: float) -> void:
	var day: bool = sim.phase == "day"
	if sim.phase == "night" and GameSim.inside_base(p.pos, BODY):
		var door: int = sim._nearest_door(p.pos)
		sim._teleport(p, GameSim.DOOR_POS[door] + GameSim.DOOR_DIR[door] * T)
		p.inside = false
		return
	if GameSim.wall_dist(p.pos) > GameSim.SAFE_D:
		var edge: Vector2 = p.pos.clamp(-GameSim.HALF, GameSim.HALF)
		_move(sim, p, edge + (p.pos - edge).limit_length(GameSim.SAFE_D - T), dt)
		return
	if p.role == 1:
		var down = _nearest(sim, p, func(q):
			return q.state == 1 and GameSim.wall_dist(q.pos) <= GameSim.SAFE_D and (sim.phase != "night" or not GameSim.inside_base(q.pos, BODY)), 8.0 * T)
		if down != null:
			p.inter = p.pos.distance_to(down.pos) <= T and sim.line_clear(p.pos, down.pos)
			if not p.inter:
				_move(sim, p, down.pos, dt)
			return
	if p.hp < BANDAGE_AT * p.maxhp and p.inv.bandage > 0 and p.channel <= 0:
		sim.action(p.id, "bandage", 0)
	if p.channel > 0:
		return
	if p.role == 2 and p.inv.wood > 0 and _repair(sim, p, dt):
		return
	if day:
		if not ["wood", "stone", "herb"].any(func(item): return free_stack_space(p.inv, item, CARRY_SLOTS) > 0):
			if p.pos.distance_to(WORKBENCH) > T:
				_move(sim, p, WORKBENCH, dt)
			else:
				for item in GameSim.RES:
					var amount: int = mini(p.inv[item], free_stack_space(sim.storage, item, STORAGE_SLOTS))
					sim.storage[item] = sim.storage.get(item, 0) + amount
					p.inv[item] -= amount
		else:
			_gather(sim, p, dt)
		return
	var door: int = sim.active[0]
	var fewest := 6
	for i: int in sim.active:
		var count := 0
		for q in sim.players.values():
			if q.id != p.id and q.state == 0 and q.pos.distance_to(GameSim.DOOR_POS[i]) <= 6.0 * T:
				count += 1
		if count < fewest:
			door = i
			fewest = count
	var door_pos: Vector2 = GameSim.DOOR_POS[door]
	var foe = _foe(sim, p, door_pos)
	if foe != null:
		p.atk = p.pos.distance_to(foe.pos) <= 1.5 * T + foe.r
		if not p.atk:
			_move(sim, p, foe.pos, dt)
		p.aim = (foe.pos - p.pos).angle()
	else:
		_move(sim, p, door_pos + GameSim.DOOR_DIR[door] * 2.0 * T, dt)


static func _think(sim, p, slot: int, dt: float) -> void:
	var post := _post(sim, p, slot)
	for tl in sim.tele:  # 1) step out of a telegraphed boss slam
		if p.pos.distance_to(tl.pos) < tl.r + 0.5 * T:
			_move(sim, p, tl.pos + (p.pos - tl.pos).normalized() * (tl.r + T), dt)
			return
	var foe = _foe(sim, p, post)
	var foe_d: float = INF if foe == null else p.pos.distance_to(foe.pos)
	var down = _nearest(sim, p, func(q): return q.state == 1, 25.0 * T)
	if down != null and foe_d > 2.5 * T:  # 2) revive (hold E next to them)
		if p.pos.distance_to(down.pos) > 1.0 * T:
			_move(sim, p, down.pos, dt)
		else:
			p.inter = true
		return
	if p.role == 1 and p.flasks > 0 and p.bot_cd <= 0:  # 3) Healer: flask on a hurt teammate in reach, or itself
		var hurt = _nearest(sim, p, func(q): return q.state == 0 and q.hp < FLASK_AT * q.maxhp, 3.0 * T)
		if hurt != null or p.hp < FLASK_AT * p.maxhp:
			sim._flask(p, hurt.pos if hurt != null else p.pos)
			p.bot_cd = 1.0
	if p.hp < BANDAGE_AT * p.maxhp and p.inv.bandage > 0 and p.channel <= 0 and foe_d > 3.0 * T:
		sim.action(p.id, "bandage", 0)
	if p.channel > 0 and foe_d > 1.5 * T:  # bandaging: stand still (attacking would cancel it)
		return
	if foe != null:  # 4) fight
		var reach: float = 1.25 * T + foe.r
		if foe_d > reach:
			_move(sim, p, foe.pos, dt)
		p.aim = (foe.pos - p.pos).angle()
		p.atk = p.pos.distance_to(foe.pos) <= 1.5 * T + foe.r
		return
	if p.role == 1 and sim.phase != "day":  # Healer keeps near a hurt teammate at night
		var far = _nearest(sim, p, func(q): return q.state == 0 and q.hp < FLASK_AT * q.maxhp, 10.0 * T)
		if far != null and p.flasks > 0:
			_move(sim, p, far.pos, dt)
			return
	if p.role == 2 and p.inv.wood > 0 and _repair(sim, p, dt):  # 5) Engineer repairs a damaged door
		return
	if sim.phase == "day" and sim.t > HOME_AT:  # 6) gather by day ...
		_gather(sim, p, dt)
	elif sim.phase == "day":  # ... then craft at the workbench before dusk
		var spot: Vector2 = BENCH_SPOTS[slot % BENCH_SPOTS.size()] * T
		_move(sim, p, spot, dt)
		if GameSim.inside_base(p.pos):
			_craft(sim, p)
	else:  # hold the post in front of the attacked door, facing out
		if p.pos.distance_to(post) > 0.3 * T:
			_move(sim, p, post, dt)
		else:
			p.aim = (post - Vector2.ZERO).angle()


static func _post(sim, p, slot: int) -> Vector2:
	## Bots share out the attacked doors and stand side by side in front of them; the Engineer stays closer.
	var act: Array = sim.active if not sim.active.is_empty() else [0]
	var d: int = act[slot % act.size()]
	var dir: Vector2 = GameSim.DOOR_DIR[d]
	var out := (2.0 if p.role == 2 else 3.0) * T
	if sim.k == 5:
		out = 4.0 * T  # night 5: between the door and its landing pad
	var side := float((slot / act.size()) % 3 - 1) * 1.5 * T
	return GameSim.DOOR_POS[d] + dir * out + dir.orthogonal() * side


static func _foe(sim, p, post: Vector2):
	var best = null
	var best_d := INF
	var day: bool = sim.phase == "day"
	for e in sim.enemies:
		var d: float = p.pos.distance_to(e.pos)
		var ok: bool
		if day:
			ok = d <= SIGHT_DAY or e.get("target", 0) == p.id
		else:
			ok = d <= 2.5 * T or e.pos.distance_to(post) <= LEASH or GameSim.inside_base(e.pos)
		if p.get("replacement", false):
			ok = e.pos.distance_to(post) <= 6.0 * T and not GameSim.inside_base(e.pos, BODY)
		if ok and d < best_d and sim.line_clear(p.pos, e.pos):
			best_d = d
			best = e
	return best


static func _nearest(sim, p, pred: Callable, radius: float):
	var best = null
	var best_d := radius
	for q in sim.players.values():
		if q.id != p.id and pred.call(q):
			var d: float = p.pos.distance_to(q.pos)
			if d <= best_d:
				best_d = d
				best = q
	return best


static func _repair(sim, p, dt: float) -> bool:
	## The nearest door below full (30% cap once broken) that can be repaired now; stands just outside it.
	var best := -1
	for i in 3:
		var cap: float = GameSim.DOOR_HP * (0.3 if sim.broken[i] else 1.0)
		if sim.doors[i] >= cap - 1.0 or sim.door_pause[i] > 0 or (sim.broken[i] and sim._enemy_inside()):
			continue
		if best < 0 or (sim.doors[i] < sim.doors[best] if p.get("replacement", false) else p.pos.distance_to(GameSim.DOOR_POS[i]) < p.pos.distance_to(GameSim.DOOR_POS[best])):
			best = i
	if best < 0:
		return false
	var spot: Vector2 = GameSim.DOOR_POS[best] + GameSim.DOOR_DIR[best] * 1.0 * T
	if p.pos.distance_to(spot) > 0.3 * T:
		_move(sim, p, spot, dt)
	else:
		p.inter = true
	return true


static func _gather(sim, p, dt: float) -> void:
	var i: int = p.bot_node
	if i < 0 or i >= sim.nodes.size() or not _can_gather_node(p, sim.nodes[i]):
		i = _pick_node(sim, p)
		p.bot_node = i
	if i < 0:
		return
	var nd = sim.nodes[i]
	if p.pos.distance_to(nd.pos) > 1.0 * T:
		_move(sim, p, nd.pos, dt)
	else:
		p.aim = (nd.pos - p.pos).angle()
		p.inter = true  # hold E standing still (MEC-08)


static func _can_gather_node(p, nd) -> bool:
	return nd.type != 3 and nd.amount > 0 and (not p.get("replacement", false) or (
		GameSim.wall_dist(nd.pos) <= GameSim.SAFE_D and free_stack_space(p.inv, GameSim.RES[nd.type], CARRY_SLOTS) > 0))


static func _pick_node(sim, p) -> int:
	## Nearest safe-zone node (no metal), its favourite resource counted as 12 tiles closer; one bot per node.
	var taken := []
	for q in sim.players.values():
		if q.bot and q.id != p.id:
			taken.append(q.bot_node)
	var best := -1
	var best_s := INF
	for i in sim.nodes.size():
		var nd = sim.nodes[i]
		if i in taken or not _can_gather_node(p, nd):
			continue
		var s: float = p.pos.distance_to(nd.pos) - (12.0 * T if nd.type == GATHER[p.role] else 0.0)
		if s < best_s:
			best_s = s
			best = i
	return best


static func _craft(sim, p) -> void:
	## One useful recipe it can afford every half second: best weapon and armour first, then kits.
	if p.bot_cd > 0:
		return
	p.bot_cd = 0.5
	var dead: bool = sim.players.values().any(func(q): return q.state == 2)
	for idx in [6, 7, 4, 5, 0, 2, 1, 3]:
		var r: Array = GameSim.RECIPES[idx]
		if r[2] > sim.unlock or (r[5] and p.role != 2):
			continue
		var useful: bool = {"weapon": r[4] > p.weapon, "armour": r[4] > p.armour, "bandage": p.inv.bandage < 2,
			"revive": dead, "base": sim.base_hp <= GameSim.BASE_HP - r[4]}[r[3]]
		if not useful or r[1].keys().any(func(res): return p.inv[res] < r[1][res]):
			continue
		sim.craft(p, idx)
		return


static func _move(sim, p, goal: Vector2, dt: float) -> void:
	## Walks like a player: same speed, same walls, and out of the base at night unless a door is broken.
	var base_way := _waypoint(sim, p.pos, goal)
	if p.get("nav_t", 0.0) <= 0 or not p.has("nav_way") or p.pos.distance_to(p.nav_way) < 4 or p.get("nav_goal", Vector2.INF).distance_to(base_way) > T or p.get("nav_revision", -1) != sim.world.revision:
		p.nav_way = sim.navigate(p.pos, base_way, 0.3 * T, false)
		p.nav_goal = base_way
		p.nav_revision = sim.world.revision
		p.nav_t = 0.4
	var way: Vector2 = p.nav_way
	var step: Vector2 = way - p.pos
	if step.length() < 1.0:
		return
	var spd: float = GameSim.SPEED * (1.2 if p.role == 0 else 1.0)
	var started_in_zone := GameSim.wall_dist(p.pos) <= GameSim.SAFE_D
	p.pos = sim.move_actor(p.pos, p.pos + step.normalized() * minf(spd * dt, step.length()), 0.3 * T)
	p.aim = step.angle()
	p.pos = p.pos.limit_length(GameSim.ISLAND)
	var night: bool = sim.phase == "night"
	p.pos = GameSim.collide(p.pos, BODY, sim.doors.map(func(d): return not night or d <= 0))
	if p.get("replacement", false):
		if started_in_zone:
			var edge: Vector2 = p.pos.clamp(-GameSim.HALF, GameSim.HALF)
			p.pos = edge + (p.pos - edge).limit_length(GameSim.SAFE_D)
		if night:
			p.pos = GameSim.push_out(p.pos, BODY)
	elif night and not p.inside and not sim.doors.any(func(d): return d <= 0):
		p.pos = GameSim.push_out(p.pos, BODY)


static func _waypoint(sim, from: Vector2, to: Vector2) -> Vector2:
	## Around the base outside, through a door to get in or out.
	var a_in := GameSim.inside_base(from)
	var b_in := GameSim.inside_base(to)
	if a_in and b_in:
		return to
	var night: bool = sim.phase == "night"
	var best := -1
	var best_d := INF
	for i in 3:
		if night and sim.doors[i] > 0:
			continue
		var d: float = from.distance_to(GameSim.DOOR_POS[i]) + GameSim.DOOR_POS[i].distance_to(to)
		if d < best_d:
			best_d = d
			best = i
	var gap := best >= 0 and from.distance_to(GameSim.DOOR_POS[best]) < 1.0 * T
	if not a_in:
		if b_in and best >= 0:
			var outer: Vector2 = GameSim.DOOR_POS[best] + GameSim.DOOR_DIR[best] * T
			if gap or from.distance_to(outer) < 0.3 * T:
				return GameSim.DOOR_POS[best] - GameSim.DOOR_DIR[best] * T
			if not GameSim.inside_base(from, BODY + 2):
				return GameSim._route(from, outer, BODY)
		if GameSim.inside_base(from, BODY + 2):  # hugging the wall outside: step off it first
			return GameSim.push_out(from, BODY + 4)
		return GameSim._route(from, to, BODY)
	if best < 0:
		return from
	var inner: Vector2 = GameSim.DOOR_POS[best] - GameSim.DOOR_DIR[best] * T
	if gap or from.distance_to(inner) < 0.3 * T:
		return GameSim.DOOR_POS[best] + GameSim.DOOR_DIR[best] * T
	return inner
