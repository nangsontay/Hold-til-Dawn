class_name GameSim
extends RefCounted

# ---- GameSim.cs
# Owner: Pham Minh Hieu. Server-only match rules (GDD chapters 2-3): the public API used by the network
# (AddPlayer, RemovePlayer, Input, Action, Tick, Snapshot), core constants, match state and crafting.
# GameSim is split into partial files by owner; each file starts with an Owner comment. Clients never run this; they get Snapshot().
static var DAY_MIN: float = GameplaySettings.get_current().day_min
const DAY_MAX = 360.0
static var DUSK: float = GameplaySettings.get_current().dusk_seconds
static var NIGHT_LEN = [420.0, 480.0, 420.0, 480.0, 480.0]
const BASE_HP = 1000.0
const DOOR_HP = 500.0
static var ROLES = ["Scout", "Healer", "Engineer"]
static var ROLE_HP = [90.0, 100.0, 110.0]
const SPEED = 5.0 * T
static var ARMOUR = [0.0, 0.15, 0.3, 0.45]
static var MODES = ["Easy", "Medium", "Hard"]
static var MODE_MUL = [[0.75, 0.85, 0.8], [1.0, 1.0, 1.0], [1.25, 1.15, 1.2]]
static var RES = ["wood", "stone", "herb", "titanium"]
# Permanent equipment, healing and maintenance are craftable. Tactical buffs/traps are chest loot only.
static var RECIPES = [["5 arrows", {"wood": 1}, 0, "arrow", 5, false], ["Stone spear", {"wood": 8, "stone": 12}, 1, "stone_spear", 1, false], ["Bow", {"wood": 6, "titanium": 2}, 1, "bow", 1, false], ["Long spear", {"wood": 10, "stone": 8}, 1, "long_spear", 1, false], ["Titanium blade", {"wood": 10, "titanium": 20}, 2, "titanium_blade", 1, false], ["Stun maul", {"wood": 8, "titanium": 6}, 2, "stun_maul", 1, false], ["3 explosive arrows", {"wood": 3, "stone": 4, "titanium": 2}, 2, "explosive_arrow", 3, false], ["Light armour", {"wood": 6}, 0, "light_armour", 1, false], ["Bandage", {"herb": 2}, 0, "bandage", 1, false], ["Revival kit", {"herb": 4, "titanium": 4}, 0, "revive", 1, false], ["Base repair +100", {"stone": 5}, 0, "base", 100, true], ["Stone armour", {"wood": 8, "stone": 16}, 1, "stone_armour", 1, false], ["Titanium armour", {"titanium": 24}, 2, "titanium_armour", 1, false], ["Healing flask", {"herb": 6, "titanium": 2}, 1, "healing_flask", 1, false], ["Field repair kit", {"wood": 4, "stone": 3, "titanium": 1}, 1, "repair_kit", 1, true], ["Gathering kit", {"wood": 6, "stone": 4}, 0, "gathering_kit", 1, false], ["Crafting kit", {"wood": 6, "stone": 4}, 0, "crafting_kit", 1, false], ["Chest key", {"wood": 2, "stone": 2}, 0, "chest_key", 1, false]]
var world = WorldGeometry.new()
var phase = "lobby"
var t = 0.0
var elapsed = 0.0
var k = 1
var mode = 1
var base_hp = BASE_HP
var doors = [DOOR_HP, DOOR_HP, DOOR_HP]
var broken = [false, false, false]
var door_pause = [0.0, 0.0, 0.0]
var active = []
var players = {}
var order = []
var enemies = []
var proj = []
# players' arrows in flight (MEC-03)
var arrows = []
# seconds since the match started; FML-09 threat windows are measured with it
var clock = 0.0
var tele = []
var nodes = []
var drops = []
var votes = {}
var unlock = 0
var shown_unlock = 0
var bosses_killed = 0
var boss_dead = false
var wave = 0
var next_wave = 0.0
var carry = 0.0
var boss_spawned = false
var towers = [null, null, null]
var free_tower = true
var ext = -1.0
var pad_clear = [0.0, 0.0, 0.0]
var msg = ""
# door where the generator was last hit, for the loss cause on the result screen
var last_door = -1
# [win, cause, night reached] while the result screen shows
var result
var msg_t = 0.0
var next_id = 0
var n0 = 1
var rng = RandomNumberGenerator.new()

func leader() -> Variant:
	return order[0] if order.size() > 0 else 0

func add_player(id, pname, role) -> void:
	# ponytail: no joining mid-match (GDD: rejoin is out of scope).
	if phase != "lobby" or players.size() >= 5 or players.has(id):
		return
	players[id] = {"id": id, "name": pname.substr(0, 16), "role": clampi(role, 0, 2), "pos": Vector2.ZERO, "tp": 0, "bot": false}
	order.append(id)
	self.reset_player(players[id])

func remove_player(id) -> void:
	var leaving = players.get(id, null)
	if leaving != null:
		cancel_chest_unlock(leaving)
	players.erase(id)
	order.erase(id)
	votes.erase(id)
	if self.humans() == 0:
		for b in players.keys():
			players.erase(b)
			order.erase(b)
		if phase != "lobby":
			self.to_lobby()

func humans() -> Variant:
	return players.values().filter(func(p): return not p.bot).size()

func input(id, pos, aim, atk, inter, ent, tp, sequence = null) -> void:
	if sequence == null: sequence = 0
	var p = players.get(id)
	if p == null or not pos.is_finite() or not is_finite(aim):
		return
	if sequence > 0 and sequence <= p.input_sequence:
		return
	p.input_sequence = sequence
	p.aim = aim
	p.atk = atk
	p.inter = inter
	p.ent = ent
	if tp == p.tp and p.state == 0:
		var travel: Vector2 = Vector2((pos - p.pos).limit_length(p.move_credit))
		p.move_credit = maxf(0.0, p.move_credit - travel.length())
		p.pos = self.move_actor(p.pos, p.pos + travel, 0.3 * T)
		for e in enemies:
			var delta: Vector2 = Vector2(p.pos - e.pos)
			var need: float = float(0.3 * T + e.r * 0.6)
			if delta.length_squared() < need * need:
				p.pos = e.pos + (delta.normalized() if delta.length_squared() > 0.001 else Vector2.DOWN) * need
		p.pos = self.move_actor(p.pos, p.pos, 0.3 * T)
		# at night only the Engineer (MEC-15) or a breach (MEC-16) lets anyone in; players caught inside
		# when a breach ends are moved out by TickBreachEnd after a warning (MEC-17)
		if phase == "night" and not p.inside and not self.any_breach() and not inside_base(p.pos):
			p.pos = push_out(p.pos, 0.4 * T)

func action(id, kind, arg) -> void:
	var p = players.get(id)
	if p == null:
		return
	if kind == "role":
		if phase == "lobby":
			p.role = clampi(int(arg), 0, 2)
			self.reset_player(p)
	elif kind == "mode":
		if phase == "lobby" and id == self.leader():
			mode = clampi(int(arg), 0, 2)
	elif kind == "start":
		if phase == "lobby" and id == self.leader():
			var err = self.start_match()
			if err != "":
				self.say(err)
	elif kind == "add_bot":
		if id == self.leader():
			Bots.add(self)
	elif kind == "remove_bot":
		if id == self.leader():
			Bots.remove(self)
	elif kind == "craft":
		self.craft(p, int(arg))
	elif kind == "tower":
		self.build_tower(p)
	elif kind == "build":
		self.build(p, arg)
	elif kind == "lobby":
		# result screen: Back to lobby (section 2.5)
		if phase == "result":
			self.to_lobby()
	elif kind == "bandage":
		if p.state == 0 and self.count(p, "bandage") > 0 and p.hp < p.maxhp and p.channel <= 0:
			p.channel = 2.0
	elif kind == "select":
		p.sel = clampi(int(arg), 0, HOTBAR - 1)
	elif kind == "move_item":
		self.move_slot(p, arg)
	elif kind == "open_chest":
		open_chest_panel(p, int(arg))
	elif kind == "give":
		self.give_or_drop(p)
	elif kind == "cancel_craft":
		p.craft_idx = -1
	elif kind == "use_item":
		if arg is Vector2:
			use_selected_item(p, Vector2(arg))
	elif kind == "flask":
		if arg is Vector2:
			self.flask(p, arg)
	elif kind == "vote":
		if phase == "day":
			if votes.has(id):
				votes.erase(id)
			else:
				votes[id] = true
	elif kind == "skip":
		if id == 1 and OS.is_debug_build() and phase in ["day", "dusk", "night"]:
			enemies.clear()
			t = 0.0

func start_match() -> Variant:
	var eng = false
	for p in players.values():
		if p.role == 2:
			eng = true
	if not eng:
		return "Need at least one Engineer"
	# ponytail: GDD wants 3-5 players, all Ready (BAL-01, card H08); 1+ allowed until the Ready lobby exists.
	rng.randomize()
	n0 = players.size()
	k = 1
	base_hp = BASE_HP
	doors = [DOOR_HP, DOOR_HP, DOOR_HP]
	unlock = 0
	towers = [null, null, null]
	free_tower = true
	cancel_all_construction()
	shown_unlock = 0
	bosses_killed = 0
	enemies.clear()
	proj.clear()
	arrows.clear()
	reset_tactical_items()
	tele.clear()
	drops.clear()
	storage = empty_slots(STORAGE_SLOTS)
	built_walls = []
	clock = 0.0
	last_door = -1
	self.draw_bosses()
	self.gen_nodes()
	for p in players.values():
		self.reset_player(p)
	self.dawn()
	world.rebuild(nodes, towers, built_walls)
	self.place_supplies()
	return ""
var collider_check_left: float = 0.0

func tick(dt) -> void:
	if phase == "lobby":
		return
	t -= dt
	msg_t -= dt
	clock += dt
	if phase == "result":
		return
	for i in 3:
		door_pause[i] -= dt
	collider_check_left -= float(dt)
	if collider_check_left <= 0:
		world.rebuild(nodes, towers, built_walls)
		collider_check_left = 0.1
	tick_bots(float(dt))
	self.tick_players(dt)
	if phase == "night":
		elapsed += dt
		self.tick_waves()
	elif phase == "day":
		self.tick_roamers(dt)
	self.tick_tactical_items()
	self.tick_enemy_simulation(float(dt))
	self.tick_towers(dt)
	self.tick_proj(dt)
	self.tick_arrows(dt)
	self.tick_tele(dt)
	self.tick_drops(dt)
	if phase == "night":
		self.tick_breach_end(dt)
		self.check_end(dt)
		return
	if self.team_wiped():
		self.end(false, "Team wiped")
	elif phase == "day":
		if t <= 0 or DAY_MAX - t >= DAY_MIN and votes.size() >= ceili(0.6 * self.humans()):
			self.dusk()
	elif phase == "dusk" and t <= 0:
		self.night()

# Player array layout: 0 name, 1 role, 2 pos, 3 hp, 4 maxhp, 5 state, 6 bleed, 7 inventory slots, 8 weapon id,
# 9 armour tier, 10 flasks, 11 swinging, 12 aim, 13 bandage channel, 14 revive progress, 15 teleport count,
# 16 note, 17 kills, 18 damage dealt, 19 inside, 20 entry, 21 gather, 22 activity, 23 activity target,
# 24 input ack, 25 selected hotbar slot, 26 craft recipe (-1 none), 27 craft progress 0..1, 28 revives, 29 damage taken,
# 30 haste active, 31 field repair seconds remaining. Enemy fields 10/11: rooted or stunned / slowed.
func snapshot() -> Variant:
	var ps = {}
	for id in players:
		var p = players[id]
		ps[id] = [p.name, p.role, p.pos, p.hp, p.maxhp, p.state, p.bleed, p.inv, p.weapon, p.armour, p.flasks, p.swing > 0, p.aim, p.channel, p.revive_p, p.tp, p.note if p.note_t > 0 else "", p.kills, p.dmg, p.inside, p.entry, p.gather_acc, p.activity, p.activity_target, p.input_sequence, p.sel, p.craft_idx, p.craft_t / player_craft_duration(p, int(p.craft_idx)) if p.craft_idx >= 0 else 0.0, p.revives, p.taken, p.get("haste_until", 0.0) > clock, p.get("repair_kit_t", 0.0), p.open_chest, p.unlock_chest, float(p.unlock_t) / CHEST_UNLOCK_TIME, p.chest_use_seq]
	return {"ph": phase, "t": t, "k": k, "base": base_hp, "doors": doors, "active": active, "ps": ps, "en": enemies.map(func(e): return [e.id, e.type, e.pos, e.hp, e.maxhp, e.r, e.get("kind", -1), self.boss_look(e), e.get("face", 0.0), e.get("swing", 0.0) > 0, e.get("stun_until", 0.0) > clock, e.get("slow_until", 0.0) > clock]), "proj": proj.map(func(b): return [b.pos, b.get("kind", "spit")]), "tele": tele.map(func(tl): return [tl.pos, tl.r, tl.t / tl.dur, tl.kind, tl.end, tl.ang, tl.spread]), "nodes": snapshot_nodes(), "drops": drops.map(func(d): return [d.pos, d.item, d.n, d.ttl]), "msg": msg if msg_t > 0 or phase == "result" else "", "unlock": unlock, "votes": votes.size(), "need": ceili(0.6 * self.humans()), "leader": self.leader(), "mode": mode, "ext": ext, "pads": pad_clear, "broken": broken, "dpause": door_pause.map(func(v): return v > 0), "inside_en": self.enemy_inside(), "tw": towers.filter(func(tw): return tw != null).map(func(tw): return [tw.slot, tw.pos, tw.hp, tw.wrecked, tw.target != null, tw.target if tw.target != null else Vector2.ZERO]), "free_tower": free_tower, "nq": [night_killed(), night_quota], "sup": supply_snapshot(), "chests": chest_contents_snapshot(), "rescue": rescue_snapshot(), "construction": construction_snapshot(), "boss_next": self.boss_kind_for(k) if k == 2 or k == 4 else -1, "sto": storage, "walls": built_walls.map(func(w): return [w.pos, w.vert, w.hp, w.maxhp, w.kind]), "result": result, "tactical": tactical_snapshot(), "arrows": arrows.map(func(a): return [a.pos, a.vel.angle(), a.get("explosive", false)])}
const WALL_IN = 12.0
const WALL_OUT = 6.0
const DOOR_GAP = 1.2 * T

# ---- src/ai/Bots.Rescue.cs
var bot_step_accumulator: float = 0.0

func tick_bots(dt: float) -> void:
	bot_step_accumulator += dt
	if bot_step_accumulator < 0.05 - 0.000001:
		return
	Bots.tick(self, bot_step_accumulator)
	bot_step_accumulator = 0

# ---- src/ai/GameSim.Bosses.cs
# Owner: Vu Duc Viet Anh. The seven NOX boss archetypes (AGT-05..11, table 4.2, BAL-36..39, BAL-43, FML-05).
# Every boss walks to its door like a Grunt, slams players near it (BAL-38) and adds one signature move.
# A boss is "busy" while it winds up, dashes, burrows or recovers: then TickBoss moves it instead of TickEnemies.
const JUGGERNAUT: int = 0
const BROODMOTHER: int = 1
const BIO_CANNON: int = 2
const BURROWER: int = 3
const GATEBREAKER: int = 4
const PHANTOM: int = 5
const IRONHIDE: int = 6
# [code, name, HP at night 2 for 4 players on Medium, speed x Grunt] (table 4.2, BAL-39)
static var BOSS_KINDS = [["NOX-01", "Juggernaut", 3900.0, 1.0], ["NOX-02", "Broodmother", 4680.0, 0.75], ["NOX-03", "Bio-Cannon", 3510.0, 1.0], ["NOX-04", "Burrower", 3120.0, 1.0], ["NOX-05", "Gatebreaker", 4290.0, 1.0], ["NOX-06", "Phantom", 3120.0, 1.0], ["NOX-07", "Ironhide", 2730.0, 1.0]]
const TELEGRAPH: float = 1.0  # every damaging boss move is shown 1.0 s before it hits (BAL-43)
const SIGNATURE_HIT: float = 35.0
const MAX_SUMMONS: int = 4
# The bosses of nights 2 and 4, drawn when the match starts and announced at dawn of that day (FML-05).
var boss_plan = [0, 1]

func draw_bosses() -> void:
	var first: int = int(rng.randi() % 7)
	var second: int = int(rng.randi() % 6)
	if second >= first:
		second += 1
	boss_plan = [first, second]

func boss_kind_for(night_) -> int:
	return int(boss_plan[1 if night_ >= 4 else 0])

static func boss_title(kind) -> Variant:
	var b = BOSS_KINDS[int(kind)]
	return "%s %s" % [b[0], b[1]]

# Night 4: damage x1.21 and cooldowns x0.9, telegraphs unchanged (section 4.1).
func boss_damage(base: float) -> float:
	return base * (1.21 if k >= 4 else 1.0) * float(MODE_MUL[mode][2])

func boss_cooldown(base: float) -> float:
	return base * (0.9 if k >= 4 else 1.0)

func setup_boss(e, kind: int) -> void:
	var b = BOSS_KINDS[kind]
	e.type = BOSS
	e.kind = kind
	# FML-02: HP scales with the team, x1.5625 on night 4, and with the mode
	e.hp = b[2] * (1 + 0.8 * (players.size() - 1)) / 3.4 * (1.5625 if k == 4 else 1.0) * MODE_MUL[mode][1]
	e.maxhp = e.hp
	e.spd = e.spd * b[3]
	# BAL-37: 60 door damage per 4 s (the Gatebreaker deals it as one 120-point blow every 8 s)
	e.dmg = self.boss_damage(60.0)
	e.slam = self.boss_damage(40.0)
	e.slam_cd = 3.0
	e.sig_cd = 4.0
	e.act = ""
	e.act_t = 0.0
	e.face = (DOOR_POS[e.door] - e.pos).angle()
	# body radius: big enough to match the drawn boss (2x sprite, Main.EnemyView.cs), small enough to get
	# through a door gap (2.4 tiles) once the door is broken
	e.r = 1.1 * T
	e.dash_hit = []

# --- damage rules -------------------------------------------------------
func hittable(e) -> bool:
	# a burrowed boss cannot be hit (Burrower, table 4.2)
	return not (e.type == BOSS and e.get("act", "") in ["burrow", "surface"])

func boss_damage_taken(e, dmg, from) -> Variant:
	if e.type != BOSS or int(e.kind) != IRONHIDE or e.get("act", "") == "stagger":
		return dmg
	# Ironhide: hits from its front 120 degrees deal 30% damage
	var off: float = absf(angle_difference(float(e.face), (from - e.pos).angle()))
	return dmg * 0.3 if off <= deg_to_rad(60.0) else dmg

func boss_on_hit(e) -> void:
	# a hit reveals the Phantom
	if e.type == BOSS and int(e.kind) == PHANTOM and e.get("act", "") == "cloak":
		self.boss_act(e, "", 0.0)
		e.shown = 3.0

func boss_act(e, act, time: float) -> void:
	e.act = act
	e.act_t = time

# Whom the boss's signature move aims at: the player it retaliates against, else the nearest one it sees.
func boss_target(e) -> Variant:
	var p = players.get(e.target) if e.state == 1 else null
	if p != null and p.state == 0:
		return p
	return self.nearest_player(e.pos, 8.0 * T)

func add_telegraph(kind, pos: Vector2, r: float, dmg: float, extra = null) -> void:
	var tl = {"kind": kind, "pos": pos, "r": r, "t": TELEGRAPH, "dur": TELEGRAPH, "dmg": dmg, "end": pos, "ang": 0.0, "spread": TAU}
	if extra != null:
		for key in extra:
			tl[key] = extra[key]
	tele.append(tl)

# BAL-38: a 40-damage slam of radius 3 every 6 s when a player is close; the boss stands still while it lands.
func boss_slam(e, dt) -> void:
	e.slam_cd -= dt
	if e.slam_cd > 0 or e.act != "":
		return
	for p in players.values():
		if p.state == 0 and p.pos.distance_to(e.pos) < 3 * T:
			self.start_slam(e)
			return

func start_slam(e) -> void:
	self.add_telegraph("circle", e.pos, 3 * T, float(e.slam))
	e.slam_cd = self.boss_cooldown(6.0)
	self.boss_act(e, "slam", TELEGRAPH)

# Runs the boss's current move; returns true while the boss is busy (it then skips walking and door hits).
func tick_boss(e, dt) -> bool:
	e.sig_cd -= dt
	e.shown = e.get("shown", 0.0) - dt
	var kind: int = int(e.kind)
	var act = e.act
	if act == "cloak":
		return self.tick_cloak(e, dt)
	if act != "":
		e.act_t -= dt
		if act == "charge":
			return self.tick_charge(e, dt)
		if act == "burrow":
			return self.tick_burrow(e, dt)
		if e.act_t > 0:
			return true
		self.finish_act(e, act)
		return e.act != ""
	if e.sig_cd > 0:
		return false
	var p = self.boss_target(e)
	var d: float = p.pos.distance_to(e.pos) if p != null else INF
	if kind == JUGGERNAUT and d >= 3 * T and d <= 10 * T:
		# charge: 1.0 s line telegraph 1.5 tiles wide, then a dash of up to 10 tiles at 9 tiles/s
		var dir: Vector2 = (p.pos - e.pos).normalized()
		e.dash = dir
		e.dash_left = 10.0 * T
		e.face = dir.angle()
		e.dash_hit = []
		self.add_telegraph("line", e.pos, 0.75 * T, 0.0, {"end": e.pos + dir * (10.0 * T)})
		self.boss_act(e, "charge_wind", TELEGRAPH)
		e.sig_cd = self.boss_cooldown(8.0)
		return true
	if kind == BROODMOTHER:
		self.boss_act(e, "glow", TELEGRAPH)
		e.sig_cd = self.boss_cooldown(12.0)
		return true
	if kind == BIO_CANNON and p != null:
		self.boss_act(e, "glow", TELEGRAPH)
		e.sig_cd = self.boss_cooldown(5.0)
		return true
	if kind == BURROWER and p != null:
		e.burrow = p.id
		self.boss_act(e, "burrow", 3.0)
		e.sig_cd = self.boss_cooldown(10.0)
		return true
	if kind == PHANTOM:
		e.burrow = p.id if p != null else 0
		self.boss_act(e, "cloak", 4.0)
		e.sig_cd = self.boss_cooldown(12.0)
		return false
	return false

func finish_act(e, act) -> void:
	var kind: int = int(e.kind)
	self.boss_act(e, "", 0.0)
	if act == "slam":
		# Juggernaut: 2 s recovery after the slam; Ironhide: 3 s stagger with full damage from every side
		if kind == JUGGERNAUT and e.get("dashed", false):
			e.dashed = false
			self.boss_act(e, "recover", 2.0)
		elif kind == IRONHIDE:
			self.boss_act(e, "stagger", 3.0)
	elif act == "charge_wind":
		self.boss_act(e, "charge", 10.0)
	elif act == "glow" and kind == BROODMOTHER:
		self.summon(e)
	elif act == "glow" and kind == BIO_CANNON:
		# a ring of 8 projectiles with 45-degree gaps: 12 damage, 4 tiles/s, range 8 tiles
		for i in range(8):
			var dir: Vector2 = Vector2.from_angle(i * TAU / 8.0)
			proj.append({"pos": e.pos + dir * e.r, "vel": dir * 4.0 * T, "dmg": self.boss_damage(12.0), "ttl": 2.0, "kind": "orb"})
	elif act == "surface":
		e.pos = e.surface
		self.boss_act(e, "climb", 1.5)
	elif act == "ambush":
		e.shown = 3.0
	elif act == "blow":
		var tw = towers[e.tw] if e.state == 5 else null
		if tw != null and tw.hp > 0:
			self.damage_tower(e, tw, self.boss_damage(120.0))
		elif e.state != 5:
			self.damage_door(e, self.boss_damage(120.0))

func tick_charge(e, dt) -> bool:
	var old: Vector2 = Vector2(e.pos)
	var step: float = minf(9.0 * T * dt, e.dash_left)
	var want: Vector2 = old + e.dash * step
	e.pos = push_out(self.move_actor(old, want, e.r), e.r)
	e.dash_left -= step
	# 35 damage and knockback to anyone on the line, once per dash
	for p in players.values():
		if p.state == 0 and not p.id in e.dash_hit and p.pos.distance_to(e.pos) <= e.r + 0.5 * T:
			e.dash_hit.append(p.id)
			self.hurt(p, self.boss_damage(SIGNATURE_HIT))
			self.knockback(p, e.pos, 2.0 * T)
	var blocked_: bool = e.pos.distance_to(want) > 2.0
	if e.dash_left <= 0 or blocked_:
		e.dashed = true
		self.start_slam(e)
	return true

func tick_burrow(e, dt) -> bool:
	var p = players.get(e.burrow)
	var lost: bool = p == null or p.state != 0
	if not lost:
		# underground it closes in fast and cannot be hit or hit doors
		var old: Vector2 = Vector2(e.pos)
		e.pos = push_out(self.move_actor(old, old + (p.pos - old).limit_length(e.spd * 2.0 * dt), e.r), e.r)
	if e.act_t > 0 and not lost and e.pos.distance_to(p.pos) > 2.5 * T:
		return true
	# surfaces at least 1.5 tiles from its target behind a 1.0 s dirt telegraph of radius 2 tiles
	var spot: Vector2 = Vector2(e.pos)
	if not lost:
		var away: Vector2 = (e.pos - p.pos).normalized() if e.pos.distance_to(p.pos) > 1.0 else Vector2.RIGHT
		spot = push_out(world.resolve(p.pos + away * (1.5 * T), e.r), e.r)
	e.surface = spot
	self.add_telegraph("circle", spot, 2.0 * T, self.boss_damage(SIGNATURE_HIT))
	self.boss_act(e, "surface", TELEGRAPH)
	return true

func tick_cloak(e, dt) -> bool:
	e.act_t -= dt
	var p = players.get(e.burrow)
	if p == null or p.state != 0:
		p = self.boss_target(e)
	if p == null:
		if e.act_t <= 0:
			self.boss_act(e, "", 0.0)
		# invisible but still walking to its door; it does not hit doors while cloaked
		return false
	var old: Vector2 = Vector2(e.pos)
	if old.distance_to(p.pos) > 1.5 * T:
		e.pos = self.move_actor(old, old + (self.enemy_waypoint(e, p.pos) - old).normalized() * e.spd * dt, e.r)
		e.pos = push_out(e.pos, e.r)
	if old.distance_to(p.pos) <= 2.0 * T or e.act_t <= 0:
		# the ambush telegraph reveals it: a 1.0 s arc, then 35 damage in a 1.5-tile arc
		e.face = (p.pos - e.pos).angle()
		self.add_telegraph("arc", e.pos, e.r + 1.5 * T, self.boss_damage(SIGNATURE_HIT), {"ang": e.face, "spread": deg_to_rad(120.0)})
		self.boss_act(e, "ambush", TELEGRAPH)
	return true

# Broodmother: 2 Grunts of the current night appear beside it (at most 4 alive, no drops, vanish with it).
func summon(e) -> void:
	var mine = enemies.filter(func(x): return x.get("owner", -1) == e.id)
	var room_: int = min(MAX_SUMMONS - mine.size(), MAX_ENEMIES - enemies.size())
	for i in range(mini(2, room_)):
		var g = self.spawn(GRUNT, e.door, 0.0, false)
		var side: Vector2 = Vector2.from_angle(e.face + (PI / 2 if i == 0 else -PI / 2))
		g.pos = push_out(world.resolve(e.pos + side * (e.r + 0.7 * T), g.r), g.r)
		g.owner = e.id
		g.summon = true

func knockback(p, from: Vector2, dist: float) -> void:
	var dir: Vector2 = (p.pos - from).normalized() if p.pos.distance_to(from) > 1.0 else Vector2.RIGHT
	var to: Vector2 = self.move_actor(p.pos, p.pos + dir * dist, 0.3 * T)
	if not p.inside:
		to = push_out(to, 0.4 * T)
	self.teleport(p, to)

# Whether this boss hits doors and towers right now (not while burrowed or cloaked).
func boss_hits_doors(e) -> bool:
	return not e.get("act", "") in ["burrow", "surface", "cloak"]

# The Gatebreaker's door blow: a 1.0 s wind-up, then 120 damage in one hit, every 8 s.
func start_blow(e) -> bool:
	if e.type != BOSS or int(e.kind) != GATEBREAKER:
		return false
	self.add_telegraph("circle", e.pos + Vector2.from_angle(float(e.face)) * e.r, 1.0 * T, 0.0)
	self.boss_act(e, "blow", TELEGRAPH)
	e.cd = self.boss_cooldown(8.0)
	return true

# Boss state for the client: "" or the current move (cloak, burrow, glow, stagger, ...).
func boss_look(e) -> Variant:
	if e.type != BOSS:
		return ""
	var act = e.act
	if act == "" and e.get("shown", 0.0) > 0:
		return "shown"
	return act

# ---- src/ai/GameSim.Drops.cs
# Owner: Vu Duc Viet Anh. Enemy drops (MEC-23, BAL-41, FML-15).
static var DROP_MUL: float = AiSettings.get_current().drop_multiplier
const DROP_TTL = 30.0
static var DROPS = [[0.1, ["wood", "stone", "herb"], 1, 2], [0.15, ["bandage"], 1, 1], [0.2, ["titanium"], 2, 2]]

func drop(e) -> void:
	# summoned Grunts drop nothing (BAL-43)
	if e.get("summon", false):
		return
	if e.type == BOSS:
		self.add_drop(e.pos, "titanium", 10)
		self.add_drop(e.pos + Vector2(0.6 * T, 0), "bandage", 3)
		return
	var d = DROPS[e.type]
	if rng.randf() < minf(1.0, d[0] * DROP_MUL):
		self.add_drop(e.pos, d[1][rng.randi() % d[1].size()], rng.randi_range(d[2], d[3]))

func add_drop(pos, item, n) -> void:
	drops.append({"pos": pos, "item": item, "n": n, "ttl": DROP_TTL})

func tick_drops(dt) -> void:
	for d in drops:
		# items dropped by players never vanish and are picked up with E (GameSim.Inventory.cs)
		if d.get("player", false):
			continue
		d.ttl -= dt
		for p in players.values():
			# MEC-23: walk over it with free inventory space, never from inside the base
			if d.ttl > 0 and p.state == 0 and not inside_base(p.pos) and p.pos.distance_to(d.pos) <= 0.8 * T and self.room(p, d.item) > 0:
				var left: int = add_to(p.inv, d.item, int(d.n))
				self.note(p, "+%d %s" % [d.n - left, item_name(d.item)])
				d.n = left
				if left == 0:
					d.ttl = 0.0
	drops = drops.filter(func(d): return d.get("player", false) or d.ttl > 0)

# ---- src/ai/GameSim.Enemies.cs
# Owner: Vu Duc Viet Anh. Wave director, enemy state machine, boss, projectiles (MEC-19/20, FML-01..05, BAL-29..39).
# AI decides targets and movement; damage to players goes through Hurt() in src/gameplay.
const MAX_ENEMIES = 70
static var SPAWN_MUL: float = AiSettings.get_current().spawn_multiplier
const SPAWN_ALL_SIDES = false  # GDD 4.3 / BAL-34: waves spawn 25-35 tiles out in a band in front of each active door
static var ENEMY_SPEED_MUL: float = AiSettings.get_current().enemy_speed
static var FIRST_WAVE_DELAY: float = AiSettings.get_current().first_wave
static var WAVE_INTERVAL: float = AiSettings.get_current().wave_interval
static var FINAL_WAVE_INTERVAL: float = AiSettings.get_current().final_wave_interval
const WAVE_MIN_DISTANCE = 8.0
const GRUNT: int = 0
const SPITTER: int = 1
const WRECKER: int = 2
const BOSS: int = 3
static var ENEMY = [[40.0, 7.0, 2.0, 8, 1], [28.0, 6.0, 2.0, 12, 3], [70.0, 8.0, 1.6, 16, 4]]  # hp, dmg, speed tiles/s, cost, first night (BAL-29..31; weaker and cheaper so more of them come)

# --- enemies ------------------------------------------------------------
func tick_waves() -> void:
	if phase != "night" or float(t) <= 0:
		reset_night_spawns()
		return
	if float(elapsed) >= float(next_wave):
		wave = int(wave) + 1
		# Schedule from now; a long frame must never replay missed waves in a burst.
		next_wave = float(elapsed) + night_wave_interval
		self.spawn_wave()
	if boss_night and not boss_spawned and float(elapsed) >= boss_at:
		boss_spawned = true
		self.spawn_boss()
		return  # keep a regular birth off the boss's creation tick
	tick_pending_night_spawn()
	pull_night_forward()

func spawn_wave() -> void:
	plan_night_wave()

func spawn(ty, door, dist_tiles, any_side = null) -> Variant:
	return spawn_enemy(int(ty), int(door), float(dist_tiles), SPAWN_ALL_SIDES if any_side == null else any_side)

func spawn_enemy(type: int, door: int, distance: float, any_side: bool) -> Variant:
	var stats = ENEMY[type]
	var mode_stats = MODE_MUL[int(mode)]
	var night_: int = int(k)
	var id: int = int(next_id)
	var growth: float = pow(1.1, night_ - 1)
	var radius: float = (0.625 if type == WRECKER else 0.5) * T
	var hp: float = float(stats[0]) * growth * float(mode_stats[1])
	var pos: Vector2 = spawn_position(door, distance, any_side)
	pos = world.resolve_fast(pos, radius)
	var enemy = {"id": id, "type": type, "pos": pos, "hp": hp, "maxhp": hp, "dmg": float(stats[1]) * growth * float(mode_stats[2]), "spd": minf(3.0, float(stats[2]) * pow(1.08, night_ - 1)) * T * ENEMY_SPEED_MUL, "door": door, "state": 0, "cd": 0.0, "hits": [], "target": 0, "anchor": Vector2.ZERO, "nodmg": 0.0, "r": radius}
	next_id = id + 1
	enemies.append(enemy)
	return enemy

func spawn_boss() -> void:
	var door: int = int(active[rng.randi() % active.size()])
	var e = self.spawn(GRUNT, door, 60.0, false)
	# BAL-34: 60 tiles in front of its door; BAL-39: a Grunt's speed of this night
	e.pos = IslandMap.pull_inland(Vector2(DOOR_POS[door] + DOOR_DIR[door] * 60.0 * T), 2 * T)
	self.setup_boss(e, self.boss_kind_for(k))
	# the boss is the last enemy of the night's quota
	e.quota = true
	night_spawned += 1
	self.say("%s is heading for door %s!" % [boss_title(e.kind).to_upper(), DOOR_NAMES[door]])

func boss() -> Variant:
	for e in enemies:
		if e.type == BOSS:
			return e
	return null

# Fixed-rate entry for real-time simulation; the explicit TickEnemies(dt) helper retains exact-step semantics.
func tick_enemy_simulation(dt: float) -> void:
	enemy_navigation_updates_this_tick = 0
	enemy_step_accumulator += dt
	if enemy_step_accumulator < enemy_body_interval - 0.000001:
		return
	var step: float = enemy_step_accumulator
	enemy_step_accumulator = 0
	enemy_body_steps += 1
	tick_enemies(step)

func tick_enemies(dt) -> void:
	enemy_navigation_updates_this_tick = 0
	limit_enemy_navigation = true
	# try (GDScript has no exceptions; the body runs unguarded)
	tick_enemy_bodies(dt)
	limit_enemy_navigation = false
var enemy_batch = []

func tick_enemy_bodies(dt) -> void:
	# a copy: a Broodmother's summons join the list during the loop
	var frame_dt: float = float(dt)
	var batch = enemy_batch
	batch.clear()
	for enemy in enemies:
		batch.append(enemy)
	var start: int = 0 if batch.size() == 0 else enemy_think_cursor % batch.size()
	enemy_think_cursor = start + 1
	var index: int = 0
	while index < batch.size():
		dt = frame_dt
		var e = batch[(start + index) % batch.size()]
		var old_pos: Vector2 = Vector2(e.pos)
		e.nav_t = e.get("nav_t", 0.0) - dt
		e.cd -= dt
		e.nodmg += dt
		e.swing = e.get("swing", 0.0) - dt
		if e.has("home") and phase == "day":
			var near: bool = false
			for p in players.values():
				if int(p.get("state")) == 0 and Vector2(p.get("pos")).distance_squared_to(Vector2(e.pos)) < 45 * T * 45 * T:
					near = true
					break
			e.unseen = 0.0 if near else e.get("unseen", 0.0) + float(dt)
			if e.unseen > 25.0:
				e.hp = 0.0
				index += 1
				continue
		if e.hp <= 0 or e.get("stun_until", 0.0) > clock:
			index += 1
			continue
		if e.get("slow_until", 0.0) > clock:
			dt = frame_dt * 0.75
		if e.type == BOSS and self.tick_boss(e, dt):
			index += 1
			continue
		# a Broodmother's summons vanish with it (BAL-43)
		if e.get("summon", false) and not enemies.any(func(x): return x.id == e.owner and x.hp > 0):
			e.hp = 0.0
			index += 1
			continue
		if e.type == SPITTER:
			self.tick_spitter(e, dt)
			e.pos = self.move_actor(old_pos, e.pos, e.r)
			index += 1
			continue
		if e.state == 4:
			var seen = self.nearest_player(e.pos, ROAM_SIGHT)
			if seen == null:
				self.wander(e, dt)
				e.pos = self.move_actor(old_pos, e.pos, e.r)
				index += 1
				continue
			e.state = 1
			e.target = seen.id
			e.anchor = e.pos
			e.nodmg = 0.0
		var target_p = null
		e.calm = e.get("calm", 0.0) - dt
		if e.state == 1:
			# FML-09: re-evaluate the top threat every 0.25 s
			e.retarget_t = e.get("retarget_t", 0.0) - dt
			if e.retarget_t <= 0:
				e.retarget_t = 0.25
				self.retarget(e)
			target_p = players.get(e.target)
			if target_p == null or target_p.state != 0 or target_p.inside and not self.any_breach() or e.nodmg > 5.0 or e.pos.distance_to(e.anchor) > 12 * T:
				# roamers go back to wandering by day; a boss ignores new threat for 10 s (BAL-35)
				e.state = 4 if e.has("home") else 0
				e.hits.clear()
				if e.type == BOSS:
					e.calm = 10.0
				target_p = null
				if e.state == 4:
					index += 1
					continue
		if e.state == 0 and phase == "night":
			var lit = self.tower_in_light(e.pos)
			if lit != null:
				e.state = 5
				e.tw = lit.slot
		if e.state == 5:
			var tw = towers[e.tw]
			if tw == null or tw.hp <= 0:
				e.state = 0
			else:
				if e.pos.distance_to(tw.pos) > e.r + 0.7 * T + 2.0:
					e.pos += (self.enemy_waypoint(e, tw.pos) - e.pos).normalized() * minf(e.spd * dt, e.pos.distance_to(tw.pos))
				elif e.cd <= 0:
					self.tower_attack(e, tw)
				if e.type == BOSS:
					self.boss_slam(e, dt)
				e.pos = push_out(self.move_actor(old_pos, e.pos, e.r), e.r)
				index += 1
				continue
		if (e.state == 0 or e.state == 2) and doors[e.door] <= 0:
			e.state = 3
		var door_pos: Vector2 = Vector2(DOOR_POS[e.door])
		# anywhere along the door gap
		var near_door: bool = e.pos.distance_to(door_pos) <= e.r + T
		var goal: Vector2 = door_pos + DOOR_DIR[e.door] * (e.r + 0.2 * T)
		var arrived = near_door
		var outside = true
		if e.state == 1:
			goal = target_p.pos
			arrived = e.pos.distance_to(goal) <= e.r + 0.4 * T
		elif e.state == 3 and (inside_base(e.pos, e.r + 2) or near_door):
			# through the broken door to the generator
			goal = Vector2.ZERO
			arrived = e.pos.length() <= 2.5 * T
			outside = false
		if not arrived:
			var way = self.enemy_waypoint(e, goal, outside)
			e.pos += (way - e.pos).normalized() * minf(e.spd * dt, e.pos.distance_to(goal))
		elif e.cd <= 0:
			self.enemy_attack(e, target_p)
		if e.type == BOSS:
			self.boss_slam(e, dt)
			if e.pos.distance_to(old_pos) > 0.5:
				e.face = (e.pos - old_pos).angle()
		e.pos = self.move_actor(old_pos, e.pos, e.r)
		if outside:
			e.pos = push_out(e.pos, e.r)
		self.tick_blocked(e, old_pos, arrived, dt)
		index += 1
	enemies = enemies.filter(func(e): return e.hp > 0)
	self.separate()

func enemy_attack(e, p) -> void:
	var boss_: bool = e.type == BOSS
	if e.state == 1:
		# a boss hurts players only with its slam and signature move
		if not boss_:
			if self.line_clear(e.pos, p.pos):
				self.hurt(p, e.dmg)
				self.swung(e)
		e.cd = 2.0
		return
	if boss_ and (not self.boss_hits_doors(e) or self.start_blow(e)):
		return
	self.damage_door(e, e.dmg)
	e.cd = 4.0 if boss_ else 2.0

# An enemy just struck (or spat): the snapshot flags it for 0.4 s so clients play its attack frames.
func swung(e) -> void:
	e.swing = 0.4

# FML-07: a hit on an intact door also hurts the generator, more as the door degrades; through a broken
# door the damage goes to the generator in full. Wreckers deal double damage to doors and towers.
func damage_door(e, dmg) -> void:
	self.swung(e)
	var d: float = float(dmg * (2.0 if e.type == WRECKER else 1.0))
	if e.state == 3:
		base_hp -= d
	else:
		var x: float = float(1.0 - doors[e.door] / DOOR_HP)
		doors[e.door] = maxf(0.0, doors[e.door] - d)
		base_hp -= 0.3 * d * (1.0 + x)
		door_pause[e.door] = 2.0
		e.state = 2
		if doors[e.door] <= 0 and not broken[e.door]:
			broken[e.door] = true
			self.say("Door %s broken!" % [DOOR_NAMES[e.door]])
	# the result screen names the door where the generator took its last hit
	last_door = e.door
	base_hp = maxf(0.0, base_hp)

# MEC-11: an enemy that cannot get past a built wall for a second attacks it (as it would a door).
func tick_blocked(e, old_pos: Vector2, arrived, dt) -> void:
	var stuck: bool = not arrived and e.pos.distance_to(old_pos) < maxf(e.spd * dt * 0.3, 0.2)
	e.stuck_t = e.get("stuck_t", 0.0) + dt if stuck else 0.0
	if e.stuck_t < 1.0 or e.cd > 0:
		return
	var w = self.wall_touching(e.pos, e.r)
	if w != null:
		self.swung(e)
		self.damage_wall(e, w, e.dmg)
		e.cd = 4.0 if e.type == BOSS else 2.0

func tower_attack(e, tw) -> void:
	if e.type == BOSS and (not self.boss_hits_doors(e) or self.start_blow(e)):
		return
	self.damage_tower(e, tw, e.dmg)
	e.cd = 4.0 if e.type == BOSS else 2.0

func damage_tower(e, tw, dmg) -> void:
	self.swung(e)
	tw.hp = maxf(0.0, tw.hp - dmg * (2.0 if e.type == WRECKER else 1.0))
	# FML-08: a hit blocks repair for 2 s
	tw.pause = 2.0
	if tw.hp <= 0 and not tw.wrecked:
		tw.wrecked = true
		self.say("Tower destroyed")

func enemy_near(pos, radius) -> Variant:
	for e in enemies:
		if e.pos.distance_to(pos) <= radius:
			return true
	return false

func tick_spitter(e, dt) -> void:
	var best = self.nearest_player(e.pos, ROAM_SIGHT if e.state == 4 else 8.0 * T)
	var bd: float = best.pos.distance_to(e.pos) if best != null else INF
	if best == null and e.state == 4:
		self.wander(e, dt)
	elif best == null:
		var front: Vector2 = Vector2(DOOR_POS[e.door] + DOOR_DIR[e.door] * 4 * T)
		if e.pos.distance_to(front) > 0.5 * T:
			e.pos += (self.enemy_waypoint(e, front) - e.pos).normalized() * e.spd * dt
			e.pos = push_out(e.pos, e.r)
	elif bd > 5.5 * T:
		e.pos += (self.enemy_waypoint(e, best.pos) - e.pos).normalized() * e.spd * dt
		e.pos = push_out(e.pos, e.r)
	elif e.cd <= 0 and self.line_clear(e.pos, best.pos):
		e.cd = 2.0
		self.swung(e)
		proj.append({"pos": e.pos, "vel": (best.pos - e.pos).normalized() * 8 * T, "dmg": e.dmg, "ttl": 1.0})

func nearest_player(pos, radius) -> Variant:
	var best = null
	var bd = radius
	for p in players.values():
		# an Engineer inside the base can only be reached through a broken door
		if p.state == 0 and (not p.inside or self.any_breach()):
			var d: float = float(p.pos.distance_to(pos))
			if d <= bd and self.line_clear(pos, p.pos):
				bd = d
				best = p
	return best

func nearest_active_door(pos) -> Variant:
	var best: int = int(active[0])
	for i in active:
		if pos.distance_to(DOOR_POS[i]) < pos.distance_to(DOOR_POS[best]):
			best = i
	return best

func tick_proj(dt) -> void:
	for b in proj:
		var start: Vector2 = Vector2(b.pos)
		var end_: Vector2 = start + b.vel * minf(dt, maxf(0.0, b.ttl))
		var first = self.obstacle_hit(start, end_, 3.5)
		var victim = null
		for p in players.values():
			if p.state != 0:
				continue
			var hit_ = WorldGeometry.circle_hit(start, end_, p.pos, 0.5 * T + 3.5)
			if hit_ < first:
				first = hit_
				victim = p
		b.pos = start.lerp(end_, first) if first < INF else end_
		b.ttl -= dt
		if first < INF:
			if victim != null:
				self.hurt(victim, b.dmg)
			b.ttl = 0.0
	proj = proj.filter(func(b): return b.ttl > 0)

func tick_tele(dt) -> void:
	for tl in tele:
		tl.t -= dt
		if tl.t <= 0 and tl.dmg > 0:
			for p in players.values():
				var d: Vector2 = Vector2(p.pos - tl.pos)
				var in_: bool = d.length() <= tl.r
				if in_ and tl.kind == "arc":
					in_ = absf(angle_difference(float(tl.ang), d.angle())) <= tl.spread / 2
				if in_:
					self.hurt(p, tl.dmg)
	tele = tele.filter(func(tl): return tl.t > 0)
# reused every tick by Separate()
var sep_cells = {}
var sep_pairs = []
var sep_seen = {}
var sep_pos = Cs.arr(0, Vector2.ZERO)
var sep_r = Cs.arr(0, 0.0)

func separate() -> void:
	# Typed: with dozens of enemies at night this ran every tick on dynamic dictionaries. Same cells, same
	# pairs in the same order and the same pushes as before.
	var n: int = enemies.size()
	if sep_pos.size() < n:
		sep_pos = Cs.arr(n, Vector2.ZERO)
		sep_r = Cs.arr(n, 0.0)
	for i in range(n):
		var e = enemies[i]
		sep_pos[i] = Vector2(e.pos)
		sep_r[i] = float(e.r)
	# Broad phase: circles cover every overlapping cell, including negative coordinates.
	for list in sep_cells.values():
		list.clear()
	sep_pairs.clear()
	sep_seen.clear()
	for i in range(n):
		var lo = Vector2i(((sep_pos[i] - Vector2.ONE * sep_r[i]) / 64.0).floor())
		var hi = Vector2i(((sep_pos[i] + Vector2.ONE * sep_r[i]) / 64.0).floor())
		for y in range(lo.y, hi.y + 1):
			for x in range(lo.x, hi.x + 1):
				var cell = Vector2i(x, y)
				var in_ = sep_cells.get(cell, null)
				if not sep_cells.has(cell):
					in_ = []
					sep_cells[cell] = in_
				for j in in_:
					if Cs.set_add(sep_seen, [j, i]):
						sep_pairs.append([j, i])
				in_.append(i)
	# cells nobody stood in this tick are dropped, so the table does not grow all night
	if sep_cells.size() > 4 * n + 64:
		var empty = []
		for cell in sep_cells:
			var list = sep_cells[cell]
			if list.size() == 0:
				empty.append(cell)
		for cell in empty:
			sep_cells.erase(cell)
	for _it1 in sep_pairs:
		var a = _it1[0]
		var b = _it1[1]
		var d: Vector2 = sep_pos[b] - sep_pos[a]
		var need: float = sep_r[a] + sep_r[b]
		if d.length_squared() >= need * need:
			continue
		var length: float = d.length()
		var normal: Vector2 = d / length if length > 0.001 else Vector2.RIGHT
		var push: Vector2 = normal * (need - length) * 0.5
		sep_pos[a] -= push
		sep_pos[b] += push
	# Resolve scenery once per body after all pair corrections, not once per pair.
	for i in range(n):
		var e = enemies[i]
		e.pos = self.move_actor(sep_pos[i], sep_pos[i], sep_r[i])

func spawn_pos(door, dist_tiles, any_side) -> Variant:
	return spawn_position(int(door), float(dist_tiles), any_side)

func spawn_position(door: int, distance: float, any_side: bool) -> Vector2:
	var random = rng
	if not any_side:
		var direction: Vector2 = Vector2(DOOR_DIR[door])
		var at: Vector2 = Vector2(DOOR_POS[door]) + direction * distance * T + Vector2(-direction.y, direction.x) * random.randf_range(-5.0, 5.0) * T
		return IslandMap.pull_inland(at, 2 * T)
	var dir: Vector2 = Vector2.from_angle(random.randf() * TAU)
	var pos: Vector2 = dir * (distance * T + Vector2(HALF).length())
	while float(wall_dist(pos)) < distance * T:
		pos += dir * T
	return pos

func enemy_waypoint(e, goal, outside = null) -> Variant:
	if outside == null: outside = true
	if e.get("nav_t", 0.0) <= 0 or not e.has("nav_way") or e.pos.distance_to(e.nav_way) < 4.0 or e.get("nav_revision", -1) != world.revision or e.get("nav_goal", Vector2.INF).distance_to(goal) > T:
		if limit_enemy_navigation and enemy_navigation_updates_this_tick >= ENEMY_NAVIGATION_LIMIT:
			# Never follow an obsolete route through newly built walls; wait for a fair turn instead.
			return e.nav_way if e.has("nav_way") and e.get("nav_revision", -1) == world.revision and e.get("nav_outside", true) == outside and e.pos.distance_to(e.nav_way) >= 4.0 else e.pos
		enemy_navigation_updates_this_tick += 1
		e.nav_way = self.navigate(e.pos, goal, e.r, outside)
		e.nav_goal = goal
		e.nav_revision = world.revision
		e.nav_outside = outside
		e.nav_t = 0.4 + int(e.id) % 7 * 0.03
	return e.nav_way

# ---- src/ai/GameSim.EnemyNavigation.cs
const ENEMY_NAVIGATION_LIMIT: int = 2
var limit_enemy_navigation: bool = false
var enemy_think_cursor: int = 0
var enemy_step_accumulator: float = 0.0
var enemy_body_interval: float = 0.05

var enemy_body_steps: int = 0

var enemy_navigation_updates_this_tick: int = 0

# ---- src/ai/GameSim.NightSpawns.cs
# A wave is a bounded budget, not a queue of allocated enemies. Only one body is made when due.
var pending_wave_budget: float = 0.0
# A wave is a bounded budget, not a queue of allocated enemies. Only one body is made when due.
var next_enemy_spawn_at: float = 0.0
# A wave is a bounded budget, not a queue of allocated enemies. Only one body is made when due.
var wave_spawn_ends_at: float = 0.0
var wave_spawn_ordinal: int = 0
static var NIGHT_BUDGET = [18, 30, 45, 60, 80]
static var SPAWN_GAP = [6, 4, 3, 2, 1.5]
static var NIGHT_ALIVE_CAP = [12, 16, 20, 24, 28]

var night_index: int:
	get:
		return clampi(int(k) - 1, 0, 4)

var night_first_wave_delay: float:
	get:
		return maxf(0, AiSettings.get_current().early_night_first_wave if night_index == 0 else (AiSettings.get_current().early_night_first_wave + FIRST_WAVE_DELAY) * 0.5 if night_index == 1 else FIRST_WAVE_DELAY)

var night_wave_interval: float:
	get:
		return maxf(10, AiSettings.get_current().early_night_wave_interval if night_index == 0 else (AiSettings.get_current().early_night_wave_interval + WAVE_INTERVAL) * 0.5 if night_index == 1 else WAVE_INTERVAL if night_index == 2 else WAVE_INTERVAL * (5.0 / 6.0) if night_index == 3 else FINAL_WAVE_INTERVAL)

var night_spawn_spacing: float:
	get:
		return maxf(0.25, SPAWN_GAP[night_index] * AiSettings.get_current().spawn_spacing / 6.0)

var night_enemy_limit: int:
	get:
		return clampi(mini(NIGHT_ALIVE_CAP[night_index], AiSettings.get_current().max_night_enemies), 1, int(MAX_ENEMIES))
# Each night has a fixed number of enemies (its quota), worked out at nightfall from the budgets of the waves the
# night has time for, plus the boss on nights 2 and 4. The HUD bar fills as they die; once all of them are dead
# the night is over and dawn comes early (CheckEnd). While the field is empty the next wave does not keep the
# players waiting, so a strong team clears the night sooner. The night's clock stays the upper limit.
var night_quota: int = 0
# Each night has a fixed number of enemies (its quota), worked out at nightfall from the budgets of the waves the
# night has time for, plus the boss on nights 2 and 4. The HUD bar fills as they die; once all of them are dead
# the night is over and dawn comes early (CheckEnd). While the field is empty the next wave does not keep the
# players waiting, so a strong team clears the night sooner. The night's clock stays the upper limit.
var night_spawned: int = 0
var regular_quota: int = 0
# seconds after the field is cleared until the next wave comes
const CLEARED_WAVE_DELAY: float = 5.0
# when the boss of nights 2 and 4 comes (seconds into the night), unless the field is cleared before
const BOSS_AT: float = 150.0
var boss_at: float = BOSS_AT
# the boss of nights 2 and 4 has been killed tonight (the night still ends only with its quota and supplies)
var boss_down_tonight: bool = false

var boss_night: bool:
	get:
		return int(k) == 2 or int(k) == 4

func plan_night_quota() -> void:
	var crew: float = 1.0 + 0.35 * maxi(0, players.size() - 1)
	var mode_stats = MODE_MUL[int(mode)]
	var mode_scale: float = float(mode_stats[0])
	var len_: float = float(NIGHT_LEN[night_index])
	var budget: float = 0
	var w: int = 1
	# the waves that come in the first part of the night; the last minute is slack for the supplies
	var at: float = night_first_wave_delay
	while at < len_ - 60.0:
		var growth: float = 1 if night_index == 0 else 1 + 0.08 * clampi(w - 1, 0, 3)
		# a boss night counts its waves at full strength: while the boss lives the waves are thinner, but
		# killing the boss ends the night anyway (CheckEnd)
		budget += clampf(NIGHT_BUDGET[night_index] * crew * growth * mode_scale * SPAWN_MUL, 0, night_enemy_limit * 20.0)
		at += night_wave_interval
		w += 1
	# the average cost of the kinds that come this night
	var cost: float = 0
	var kinds: int = 0
	for type in range(3):
		var stats = ENEMY[type]
		if int(stats[4]) <= int(k):
			cost += float(stats[3])
			kinds += 1
	regular_quota = maxi(1, int(budget / maxf(1.0, cost / maxi(1, kinds))))
	night_quota = regular_quota + (1 if boss_night else 0)
	night_spawned = 0

# Quota enemies (the night's own, not daytime roamers or a boss's brood) still alive.
func night_alive() -> int:
	var n: int = 0
	for enemy in enemies:
		if enemy.get("quota", false) and float(enemy.get("hp")) > 0:
			n += 1
	return n

func night_killed() -> int:
	return maxi(0, night_spawned - night_alive())

func night_all_down() -> bool:
	return night_quota > 0 and night_spawned >= night_quota and night_alive() == 0

# Called from TickWaves: an empty field brings the next wave (or the boss) forward.
func pull_night_forward() -> void:
	if night_spawned == 0 or pending_wave_budget >= 10 or night_alive() > 0:
		return
	var now: float = float(elapsed)
	if night_spawned < regular_quota:
		if float(next_wave) > now + CLEARED_WAVE_DELAY:
			next_wave = now + CLEARED_WAVE_DELAY
	elif boss_night and not boss_spawned:
		if boss_at > now + CLEARED_WAVE_DELAY:
			boss_at = now + CLEARED_WAVE_DELAY

func reset_night_spawns() -> void:
	pending_wave_budget = 0
	next_enemy_spawn_at = 0
	wave_spawn_ends_at = 0
	wave_spawn_ordinal = 0
	carry = 0.0

func plan_night_wave() -> void:
	# Old unfinished waves are discarded. Neither a full field nor a hitch accumulates future bursts.
	var crew: float = 1.0 + 0.35 * maxi(0, players.size() - 1)
	var growth: float = 1 if night_index == 0 else 1 + 0.08 * clampi(int(wave) - 1, 0, 3)
	var boss_scale: float = 1
	for enemy in enemies:
		if int(enemy.get("type")) == BOSS:
			boss_scale = 1.0 / 3.0
			break
	var mode_stats = MODE_MUL[int(mode)]
	var mode_scale: float = float(mode_stats[0])
	pending_wave_budget = clampf(NIGHT_BUDGET[night_index] * crew * growth * boss_scale * mode_scale * SPAWN_MUL + float(carry), 0, night_enemy_limit * 20.0)
	carry = 0.0
	next_enemy_spawn_at = float(elapsed)
	wave_spawn_ends_at = next_enemy_spawn_at + night_wave_interval * 0.7
	wave_spawn_ordinal = 0

func tick_pending_night_spawn() -> void:
	var now: float = float(elapsed)
	if pending_wave_budget < 10:
		return
	if night_spawned >= regular_quota:
		pending_wave_budget = 0
		return
	# an unfinished wave hands what is left to the next one, so the night's quota still comes
	if now >= wave_spawn_ends_at:
		carry = float(carry) + pending_wave_budget
		pending_wave_budget = 0
		return
	if now < next_enemy_spawn_at:
		return
	# Delay instead of a catch-up loop, including when the alive cap has been reached.
	next_enemy_spawn_at = now + night_spawn_spacing
	if enemies.size() >= night_enemy_limit or active.size() == 0:
		return
	var eligible = Cs.arr(3, 0)
	var count_: int = 0
	for type in range(3):
		var stats = ENEMY[type]
		if int(stats[4]) <= int(k) and float(stats[3]) <= pending_wave_budget:
			var _old1 = count_
			count_ += 1
			eligible[_old1] = type
	if count_ == 0:
		pending_wave_budget = 0
		return
	var random = rng
	var kind: int = eligible[int(random.randi() % count_)]
	var _old2 = wave_spawn_ordinal
	wave_spawn_ordinal += 1
	var door: int = int(active[_old2 % active.size()])
	var distance: float = random.randf_range(float(WAVE_MIN_DISTANCE), (float(ISLAND) - Vector2(HALF).length()) / T - 2) if SPAWN_ALL_SIDES else random.randf_range(25.0, 35.0)
	var enemy = spawn_enemy(kind, door, distance, SPAWN_ALL_SIDES)
	if SPAWN_ALL_SIDES:
		enemy["door"] = nearest_active_door(enemy.get("pos"))
	enemy["quota"] = true
	night_spawned += 1
	var chosen_stats = ENEMY[kind]
	pending_wave_budget -= float(chosen_stats[3])
	if pending_wave_budget < 10:
		carry = pending_wave_budget
		pending_wave_budget = 0

# ---- src/ai/GameSim.Roamers.cs
# Owner: Vu Duc Viet Anh. Daytime roamers (MEC-26, FML-16, BAL-44).
static var ROAM_MUL: float = AiSettings.get_current().roamer_multiplier
const ROAM_CHECK = 8.0
const ROAM_D0 = 10.0
const ROAM_D1 = 45.0
const ROAM_PMAX = 0.65
const ROAM_MAX = 14
const ROAM_SIGHT = 4.0 * T
const VIEW_HALF = Vector2(15.0, 8.5) * T

func tick_roamers(dt) -> void:
	for p in players.values():
		p.roam_t -= dt
		if p.roam_t > 0:
			continue
		p.roam_t = ROAM_CHECK
		if p.state != 0 or inside_base(p.pos) or self.roamer_count() >= ROAM_MAX or enemies.size() > MAX_ENEMIES - 2:
			continue
		if rng.randf() < roam_chance(p.pos) * ROAM_MUL * (1.4 if world.swamp_centre.distance_to(Vector2(p.pos)) < 14 * T else 1.0):
			self.spawn_roamers(p)

static func roam_chance(pos) -> Variant:
	# tiles from the nearest base wall (FML-16)
	var d = wall_dist(pos) / T
	return ROAM_PMAX * clampf((d - ROAM_D0) / (ROAM_D1 - ROAM_D0), 0.0, 1.0)

func spawn_roamers(p) -> void:
	# Just outside this player's camera view, on the side away from the base.
	var away: Vector2 = Vector2(p.pos.normalized())
	var side = Vector2(-away.y, away.x)
	var at: Vector2 = p.pos + away * (VIEW_HALF.x + 2 * T) + side * rng.randf_range(-4.0, 4.0) * T
	if not IslandMap.on_land(at, 2 * T):
		at = p.pos + side * (VIEW_HALF.x + 2 * T) * (1.0 if rng.randf() < 0.5 else -1.0)
	at = IslandMap.pull_inland(at, 2 * T)
	for q in players.values():
		if absf(q.pos.x - at.x) < VIEW_HALF.x + T and absf(q.pos.y - at.y) < VIEW_HALF.y + T:
			return
	for i in mini(2, ROAM_MAX - roamer_count()):
		var ty: int = SPITTER if k >= 4 and i == 1 and rng.randf() < 0.5 else GRUNT
		# stats of night k on day k
		var e = self.spawn(ty, self.nearest_active_door(at), 0.0)
		e.pos = world.resolve_fast(at + Vector2(int(i) * 1.3 * T, 0), float(e.r))
		e.home = at
		e.wander = e.pos
		e.state = 4

func roamer_count() -> Variant:
	return enemies.filter(func(e): return e.has("home")).size()

func wander(e, dt) -> void:
	if e.pos.distance_to(e.wander) < 4.0:
		e.wander = e.home + Vector2.from_angle(rng.randf() * TAU) * rng.randf_range(0.0, 3.0) * T
	e.pos += (self.enemy_waypoint(e, e.wander) - e.pos).normalized() * minf(e.spd * 0.4 * dt, e.pos.distance_to(e.wander))
	e.pos = push_out(e.pos, e.r)

# ---- src/gameplay/GameSim.Construction.cs
const TOWER_BUILD_TIME: float = 6.0
var construction = []

func cancel_all_construction() -> void:
	construction.clear()
	for p in players.values():
		p["tower_build_t"] = 0.0

func construction_slot_taken(slot: int) -> bool:
	for job in construction:
		if int(job.get("slot")) == slot:
			return true
	return false

func reserve_tower(p, slot: int, at: Vector2) -> bool:
	for job in construction:
		if int(job.get("slot")) == slot or int(job.get("owner")) == int(p.id) or Vector2(job.get("pos")).distance_to(at) < 2 * T:
			return false
	construction.append({"slot": slot, "owner": p.id, "pos": at, "origin": p.pos, "taken": p.taken, "left": TOWER_BUILD_TIME})
	p.tower_build_t = TOWER_BUILD_TIME
	note(p, "Preparing dirt foundation and tower: 6 seconds. Stand still.")
	return true

func tick_tower_construction(p, dt: float) -> bool:
	for i in range(construction.size() - 1, -1, -1):
		var job = construction[i]
		if int(job.get("owner")) != int(p.id):
			continue
		var at: Vector2 = Vector2(job.get("pos"))
		if p.state != 0 or p.role != 2 or count(p, "hammer") <= 0 or p.inside or p.craft_idx >= 0 or p.atk or p.taken > job.get("taken") or Vector2(p.pos).distance_to(Vector2(job.get("origin"))) > 4:
			construction.remove_at(i)
			p.tower_build_t = 0.0
			note(p, "Construction interrupted; materials kept")
			return false
		p.activity = "repair"
		p.activity_target = at
		var left: float = maxf(0, float(job.get("left")) - dt)
		job["left"] = left
		p.tower_build_t = left
		if left > 0:
			return true
		construction.remove_at(i)
		# People may have walked across the foundation, or a wall may have been placed during the channel.
		var why = tower_spot_problem(at, world)
		for q in players.values():
			if q.id != p.id and Vector2(q.pos).distance_to(at) < 1.1 * T:
				why = "Someone is standing there"
		if towers[job.get("slot")] != null:
			why = "Tower slot is occupied"
		if not free_tower:
			if unlock < 2:
				why = "Unlocks after the first boss"
			for id in TOWER_COST:
				if count(p, id) < TOWER_COST[id]:
					why = "Need tower materials"
		if why != "":
			note(p, why + "; materials kept")
			return true
		if free_tower:
			free_tower = false
		else:
			for id in TOWER_COST:
				take(p, id, TOWER_COST[id])
		place_tower(job.get("slot"), at)
		world.rebuild(nodes, towers, built_walls)
		note(p, "Tower ready on dirt foundation")
		return true
	p.tower_build_t = 0.0
	return false

func construction_snapshot() -> Variant:
	var result_ = []
	for job in construction:
		result_.append([job.get("pos"), 1.0 - float(job.get("left")) / TOWER_BUILD_TIME, job.get("owner")])
	return result_

# ---- src/gameplay/GameSim.Inventory.cs
# Owner: Pham Minh Hieu. Items, the 8-slot inventory with its 4-slot hotbar, shared base storage,
# giving, dropping and picking up (MEC-09, BAL-18, ITM-01..13).
const INV_SLOTS: int = 8
const HOTBAR: int = 4
const STORAGE_SLOTS: int = 40
const STACK: int = 20
# A crafted bow carries its own arrows: a bow slot is [id, 1, arrows loaded], up to QUIVER, so the arrows take
# no slot of their own. Arrows a player gets go into a bow first; "arrow" counts and takes cover both.
const QUIVER: int = 30
# id -> [name, stack size, kind]; kind is res, use, ammo, weapon, tool or armour.
static var ITEMS = {"wood": ["Wood", STACK, "res"], "stone": ["Stone", STACK, "res"], "herb": ["Herb", STACK, "res"], "titanium": ["Titanium", STACK, "res"], "bandage": ["Bandage", STACK, "use"], "arrow": ["Arrow", STACK, "ammo"], "revive": ["Revival kit", 3, "use"], "long_spear": ["Long spear", 1, "weapon"], "stun_maul": ["Stun maul", 1, "weapon"], "vine_trap": ["Vine trap", 5, "use"], "poison_flask": ["Poison flask", 5, "use"], "healing_flask": ["Healing flask", 5, "use"], "haste_tonic": ["Haste tonic", 5, "use"], "gathering_kit": ["Gathering kit", 1, "tool"], "crafting_kit": ["Crafting kit", 1, "tool"], "chest_key": ["Chest key", 10, "tool"], "explosive_arrow": ["Explosive arrow", 12, "ammo"], "repair_kit": ["Field repair kit", 3, "use"], "stone_spear": ["Stone spear", 1, "weapon"], "titanium_blade": ["Titanium blade", 1, "weapon"], "starter_bow": ["Starter bow", 1, "weapon"], "bow": ["Bow", 1, "weapon"], "hammer": ["Hammer", 1, "weapon"], "light_armour": ["Light armour", 1, "armour"], "stone_armour": ["Stone armour", 1, "armour"], "titanium_armour": ["Titanium armour", 1, "armour"]}
# id -> [damage, attacks per second, reach or range in tiles, ranged, uses arrows] (BAL-08..10, BAL-42)
static var WEAPON_STATS = {"long_spear": [22.0, 1.0, 2.6, false, false], "stun_maul": [30.0, 0.65, 1.5, false, false], "fists": [15.0, 1.5, 1.5, false, false], "stone_spear": [20.0, 1.5, 1.5, false, false], "titanium_blade": [26.0, 1.5, 1.5, false, false], "hammer": [12.0, 1.2, 1.5, false, false], "starter_bow": [12.0, 1.0, 8.0, true, false], "bow": [12.0, 1.0, 8.0, true, true]}
# armour items by tier; ARMOUR holds the matching values (BAL-11)
static var ARMOUR_ITEMS = ["", "light_armour", "stone_armour", "titanium_armour"]
# Team storage inside the base (BAL-18).
var storage = empty_slots(STORAGE_SLOTS)

static func empty_slots(n: int) -> Variant:
	var slots = []
	for i in range(n):
		slots.append(null)
	return slots

static func item_name(id) -> Variant:
	var component: int = COMPONENT_IDS.find(Cs.s(id))
	if component >= 0:
		return SUPPLY_NAMES[component]
	var rescue: int = rescue_kind(Cs.s(id))
	if rescue >= 0:
		return "Locator radio"
	return ITEMS[id][0] if ITEMS.has(id) else Cs.s(id)

static func stack_of(id) -> int:
	return int(ITEMS[id][1]) if ITEMS.has(id) else 1

static func kind_of(id) -> Variant:
	return ITEMS[id][2] if ITEMS.has(id) else ""

# --- a bow's arrows ---------------------------------------------------------
static func is_bow(s) -> bool:
	return s != null and s[0] == "bow"

# Arrows loaded in the bow of slot S (0 for any other slot).
static func loaded(s) -> int:
	return int(s[2]) if is_bow(s) and s.size() > 2 else 0

static func bow_slot(arrows_: int) -> Variant:
	return ["bow", 1, arrows_]

# Loads up to N arrows into the bows of a slot array; returns what did not fit.
static func load_arrows(slots, n: int) -> int:
	var i: int = 0
	while i < slots.size() and n > 0:
		var s = slots[i]
		if is_bow(s) and loaded(s) < QUIVER:
			var put: int = mini(n, QUIVER - loaded(s))
			slots[i] = bow_slot(loaded(s) + put)
			n -= put
		i += 1
	return n

# Moves loose arrows of a slot array into its bows, emptying the arrow slots first from the back.
static func load_bows(slots) -> void:
	var room_: int = 0
	var loose: int = 0
	for s in slots:
		if is_bow(s):
			room_ += QUIVER - loaded(s)
		elif s != null and s[0] == "arrow":
			loose += int(s[1])
	var n: int = mini(room_, loose)
	if n > 0:
		take_loose(slots, "arrow", n)
		load_arrows(slots, n)

# --- slot arrays (a player's inventory or the storage) -------------------
static func count_in(slots, id) -> int:
	var n: int = 0
	var arrows_: bool = id == "arrow"
	for s in slots:
		if s != null and s[0] == id:
			n += int(s[1])
		elif arrows_:
			n += loaded(s)
	return n

static func room_in(slots, id) -> int:
	var room_: int = 0
	var stack: int = stack_of(id)
	var arrows_: bool = id == "arrow"
	for s in slots:
		if s == null:
			room_ += stack
		elif s[0] == id:
			room_ += stack - int(s[1])
		elif arrows_ and is_bow(s):
			room_ += QUIVER - loaded(s)
	return room_

# Adds up to N; returns what did not fit. Arrows go into a bow first; then it tops up existing stacks, then
# fills empty slots.
static func add_to(slots, id, n: int) -> int:
	var stack: int = stack_of(id)
	if id == "arrow":
		n = load_arrows(slots, n)
	var i: int = 0
	while i < slots.size() and n > 0:
		var s = slots[i]
		if s != null and s[0] == id and s[1] < stack:
			var put: int = mini(n, stack - int(s[1]))
			slots[i] = [id, int(s[1]) + put]
			n -= put
		i += 1
	i = 0
	while i < slots.size() and n > 0:
		if slots[i] == null:
			var put: int = mini(n, stack)
			slots[i] = [id, put]
			n -= put
		i += 1
	return n

# Removes N if there are that many (from the last slots first); otherwise removes nothing. Arrows come out of
# the loose stacks first, so a slot frees up as soon as it can, then out of the bows.
static func take_from(slots, id, n: int) -> bool:
	if count_in(slots, id) < n:
		return false
	n -= take_loose(slots, id, n)
	var i: int = slots.size() - 1
	while i >= 0 and n > 0:
		var s = slots[i]
		if id == "arrow" and loaded(s) > 0:
			var got: int = mini(n, loaded(s))
			n -= got
			slots[i] = bow_slot(loaded(s) - got)
		i -= 1
	return true

# Removes up to N from the stacks of Id (from the last slots first); returns how many it took.
static func take_loose(slots, id, n: int) -> int:
	var want: int = n
	var i: int = slots.size() - 1
	while i >= 0 and n > 0:
		var s = slots[i]
		if s != null and s[0] == id:
			var got: int = mini(n, int(s[1]))
			n -= got
			slots[i] = [id, int(s[1]) - got] if int(s[1]) - got > 0 else null
		i -= 1
	return want - n

# --- a player's inventory --------------------------------------------------
func count(p, id) -> int:
	return count_in(p.inv, id)

func room(p, id) -> int:
	return room_in(p.inv, id)

func take(p, id, n) -> bool:
	return take_from(p.inv, id, int(n))

# Puts items in the inventory; whatever does not fit is dropped at the player's feet.
func add_item(p, id, n) -> void:
	# armour is worn at once and takes no bag slot
	if kind_of(id) == "armour":
		p.armour = maxi(int(p.armour), ARMOUR_ITEMS.find(id))
		return
	var left: int = add_to(p.inv, id, int(n))
	load_bows(p.inv)
	if left > 0:
		self.player_drop(p.pos, id, left)
		self.note(p, "Bag full - dropped")

# Sets how many of an item a player carries (tests and debug).
func set_count(p, id, n) -> void:
	take_from(p.inv, id, self.count(p, id))
	add_to(p.inv, id, int(n))

func give_starting_kit(p) -> void:
	# Team change to BAL-42: no basic weapon item; everyone fights bare-handed when the selected slot holds
	# no weapon. Scout: starter bow; Engineer: hammer; Healer: empty hands (the flasks are not an item).
	p.inv = empty_slots(INV_SLOTS)
	if p.role == 0:
		p.inv[0] = ["starter_bow", 1]
	elif p.role == 2:
		p.inv[0] = ["hammer", 1]
	p.sel = 0

# The weapon in hand: the item in the selected hotbar slot if it is a weapon, otherwise bare hands.
func weapon_of(p) -> Variant:
	var s = p.inv[int(p.sel)]
	if s != null and kind_of(s[0]) == "weapon":
		return s[0]
	return "fists"

static func weapon_name(id) -> Variant:
	return "Bare hands" if id == "fists" else item_name(id)

# Armour is worn from the inventory: the best piece carried counts (BAL-11).
# The armour worn (0 none, 1 light, 2 stone, 3 titanium). Armour is not kept in the bag: it is worn as soon as
# it is crafted or picked up, the best piece kept.
func armour_tier(p) -> int:
	return int(p.armour)

# Any armour that reached the bag (taken from storage, picked up) is put on at once and leaves the bag.
func wear_armour(p) -> void:
	for tier in range(1, ARMOUR_ITEMS.size()):
		var id = ARMOUR_ITEMS[tier]
		var have: int = self.count(p, id)
		if have > 0:
			take_from(p.inv, id, have)
			p.armour = maxi(int(p.armour), tier)

# Storage is the chest by the north wall (GameSim.STORAGE_POS): reachable standing at it, inside the base by
# day, or as the Engineer inside at night.
func at_storage(p) -> bool:
	return self.in_base_to_work(p) and near_storage(Vector2(p.pos))

func inventory_box(p, box) -> Variant:
	if box == "inv":
		return p.inv
	if box == "sto":
		return storage if at_storage(p) else null
	var id = box.substr(6).to_int()
	if not box.begins_with("chest:") or not box.substr(6).is_valid_int():
		return null
	var c = chest_by_id(id)
	return c.slots if can_access_chest(p, c) else null

# Drag and drop between the inventory ("inv") and storage ("sto"): swaps, or merges equal stacks.
func move_slot(p, arg) -> void:
	if p.state != 0 or not arg is Array or arg.size() < 4:
		return
	var from_box = Cs.s(arg[0])
	var to_box = Cs.s(arg[2])
	var from = inventory_box(p, from_box)
	var to = inventory_box(p, to_box)
	if from == null or to == null:
		self.note(p, "Unlock the chest and stand close to it")
		return
	var a: int = int(arg[1])
	var b: int = int(arg[3])
	if a < 0 or a >= from.size() or b < 0 or b >= to.size() or from_box == to_box and a == b:
		return
	var sa = from[a]
	var sb = to[b]
	if sa == null:
		return
	var component: int = COMPONENT_IDS.find(Cs.s(sa[0]))
	if component >= 0 and from_box.begins_with("chest:") and to_box == "inv":
		var chest = chest_by_id(from_box.substr(6).to_int())
		from[a] = null
		chest.component = -1
		rescue_components[component] = 1
		say(SUPPLY_NAMES[component] + " secured (" + str(supplies_got()) + "/3). Assemble the locator radio before night 5.")
		return  # team part is selected explicitly but never displaces a bag item, even when full
	# A radio part cannot be swapped into a backpack or another container by the reverse direction.
	if component >= 0 or sb != null and COMPONENT_IDS.find(Cs.s(sb[0])) >= 0:
		return
	# arrows dropped on a bow (or a bow on arrows) are loaded into it
	var bow = sb if is_bow(sb) else sa if is_bow(sa) else null
	var ammo = sa if is_bow(sb) else sb
	if bow != null and ammo != null and ammo[0] == "arrow" and loaded(bow) < QUIVER:
		var put: int = mini(int(ammo[1]), QUIVER - loaded(bow))
		var rest_ = ["arrow", int(ammo[1]) - put] if int(ammo[1]) - put > 0 else null
		var full = bow_slot(loaded(bow) + put)
		# the bow stays in its slot, what is left of the arrows stays in theirs
		from[a] = full if is_bow(sa) else rest_
		to[b] = full if is_bow(sb) else rest_
		self.note(p, "+%d arrows" % [put])
		return
	if sb != null and sa[0] == sb[0] and stack_of(sa[0]) > 1:
		var stack: int = stack_of(sa[0])
		var put: int = mini(int(sa[1]), stack - int(sb[1]))
		to[b] = [sb[0], int(sb[1]) + put]
		from[a] = [sa[0], int(sa[1]) - put] if int(sa[1]) - put > 0 else null
		return
	from[a] = sb
	to[b] = sa
	load_bows(p.inv)

# G: hand the selected stack to the nearest teammate in reach (1.5 tiles), otherwise drop it (MEC-09).
func give_or_drop(p) -> void:
	var sel: int = int(p.sel)
	var s = p.inv[sel]
	if s == null or p.state != 0:
		return
	var best = null
	var best_d: float = 1.5 * T
	for q in players.values():
		if q.id != p.id and q.state == 0 and q.pos.distance_to(p.pos) <= best_d and room_in(q.inv, s[0]) > 0:
			best_d = q.pos.distance_to(p.pos)
			best = q
	p.inv[sel] = null
	var arrows_: int = loaded(s)
	if best != null:
		var left: int = add_to(best.inv, s[0], int(s[1]))
		if left > 0:
			p.inv[sel] = s if is_bow(s) else [s[0], left]
		elif arrows_ > 0:
			# the bow's arrows go with it: into its new owner's bows or bag, the rest at their feet
			var over: int = add_to(best.inv, "arrow", arrows_)
			if over > 0:
				self.player_drop(best.pos, "arrow", over)
		self.note(p, "Gave %s to %s" % [item_name(s[0]), best.name])
		self.note(best, "%s gave you %s" % [p.name, item_name(s[0])])
		return
	self.player_drop(p.pos, s[0], s[1])
	if arrows_ > 0:
		self.player_drop(p.pos, "arrow", arrows_)

# Items dropped by players stay until someone picks them up with E (MEC-09).
func player_drop(pos, id, n) -> void:
	drops.append({"pos": pos, "item": id, "n": int(n), "ttl": -1.0, "player": true})

# E next to a dropped item: picks up as much as fits. Returns true when there was something to pick up.
func pick_up(p) -> bool:
	for d in drops:
		if d.get("player", false) and d.pos.distance_to(p.pos) <= 1.2 * T:
			var left: int = add_to(p.inv, d.item, int(d.n))
			if left == int(d.n):
				self.note(p, "Inventory full")
				return true
			d.n = left
			drops = drops.filter(func(x): return x.n > 0)
			load_bows(p.inv)
			return true
	return false

# ---- src/gameplay/GameSim.Players.cs
# Owner: Pham Minh Hieu. Player actions: movement credit, combat, gather/repair/revive, healing (MEC-01..13).
# Crafting takes CRAFT_TIME at the workbench (BAL-19); base repair runs at 20 HP/s (BAL-20).
const CRAFT_TIME: float = 3.0
# seconds of holding E over a dead teammate to use a Revival kit on them
const REVIVE_KIT_TIME: float = 3.0

# In the base (by day, or as the Engineer who went in at night) and standing at the furniture.
func in_base_to_work(p) -> bool:
	return phase in ["day", "dusk"] and inside_base(p.pos) or phase == "night" and p.inside

func at_bench(p) -> bool:
	return self.in_base_to_work(p) and near_bench(Vector2(p.pos))

# Checks every rule of MEC-10; Note() tells the player why not.
func can_craft(p, idx: int, tell: bool = true) -> bool:
	if idx < 0 or idx >= RECIPES.size():
		return false
	var r = RECIPES[idx]
	var why = ""
	if p.state != 0 or not self.at_bench(p):
		why = "Go to the workbench"
	elif radio_builder == int(p.id):
		why = "Release E to stop assembling the radio first"
	elif r[2] > unlock:
		why = "%s is locked" % [r[0]]
	elif r[5] and p.role != 2:
		why = "Engineer only"
	elif r[3] == "base" and phase == "night":
		why = "By day only"
	elif r[3] == "base" and base_hp >= BASE_HP:
		why = "Generator full"
	elif kind_of(r[3]) == "armour" and ARMOUR_ITEMS.find(r[3]) <= int(p.armour):
		why = "Already worn"
	else:
		for res in r[1]:
			if self.craft_have(p, Cs.s(res)) < r[1][res]:
				why = "Need more %s" % [item_name(res)]
				break
	if why != "" and tell:
		self.note(p, why)
	return why == ""

func craft_duration(idx: int) -> Variant:
	var r = RECIPES[idx]
	return r[4] / 20.0 if r[3] == "base" else CRAFT_TIME

func player_craft_duration(p, idx: int) -> float:
	return float(craft_duration(idx)) * (0.8 if count(p, "crafting_kit") > 0 else 1.0)

# Starts the craft channel; the cost is paid when it completes (TickCraft).
func craft(p, idx) -> void:
	if p.craft_idx >= 0 or not self.can_craft(p, int(idx)):
		return
	p.craft_idx = int(idx)
	p.craft_t = 0.0

func tick_craft(p, dt) -> void:
	if p.craft_idx < 0:
		return
	if p.state != 0 or not self.at_bench(p):
		p.craft_idx = -1
		return
	p.activity = "craft"
	p.craft_t += dt
	if p.craft_t < player_craft_duration(p, int(p.craft_idx)):
		return
	var idx: int = int(p.craft_idx)
	p.craft_idx = -1
	p.craft_t = 0.0
	if self.can_craft(p, idx):
		self.finish_craft(p, idx)

func finish_craft(p, idx: int) -> void:
	var r = RECIPES[idx]
	for res in r[1]:
		self.take(p, res, r[1][res])
	if r[3] == "base":
		base_hp = minf(float(BASE_HP), float(base_hp) + int(r[4]))
	else:
		self.add_item(p, r[3], r[4])
	self.note(p, "Crafted " + Cs.s(r[0]))

# --- players ------------------------------------------------------------
func reset_player(p) -> void:
	cancel_chest_unlock(p)
	p.open_chest = -1
	p.chest_use_seq = 0
	p.hp = ROLE_HP[p.role]
	p.maxhp = p.hp
	p.haste_until = 0.0
	p.item_cd = 0.0
	p.repair_kit_t = 0.0
	p.area_heal_at = 0.0
	p.tactical_think_at = 0.0
	p.next_trap_at = 0.0
	# 0 standing, 1 downed, 2 dead
	p.state = 0
	p.bleed = 0.0
	self.give_starting_kit(p)
	p.weapon = self.weapon_of(p)
	p.armour = 0
	p.craft_idx = -1
	p.craft_t = 0.0
	p.flasks = 3 if p.role == 1 else 0
	p.invuln = 0.0
	p.atk_cd = 0.0
	p.swing = 0.0
	p.channel = 0.0
	p.repair_kit_t = 0.0
	p.tower_build_t = 0.0
	p.revive_p = 0.0
	p.gather_acc = 0.0
	p.repair_acc = 0.0
	p.aim = 0.0
	p.atk = false
	p.inter = false
	# result screen stats (section 2.5): kills, damage dealt, revives, damage taken
	p.kills = 0
	p.dmg = 0.0
	p.revives = 0
	p.taken = 0.0
	# Engineer inside the base at night (MEC-15)
	p.inside = false
	p.entry = 0.0
	p.entry_pos = Vector2.ZERO
	p.ent = false
	p.gather_pos = Vector2.ZERO
	p.roam_t = ROAM_CHECK
	p.note = ""
	p.note_t = 0.0
	p.bot_cd = 0.0
	p.activity = "idle"
	p.activity_target = Vector2.ZERO
	p.move_credit = SPEED * 0.1
	p.input_sequence = 0
	# index of the resource node a bot is gathering
	p.bot_node = -1
	# spawn beside the lighthouse, left or right (behind it the sprite hides you), in front of the workbench
	# and the storage chest by the north wall
	var side: float = -1.0 if rng.randf() < 0.5 else 1.0
	self.teleport(p, world.resolve(Vector2(side * rng.randf_range(2.5, 4.0), rng.randf_range(-0.6, 1.5)) * T, 0.3 * T))

func tick_players(dt) -> void:
	tick_chest_access()
	if radio_builder != null:
		var builder = players.get(radio_builder, null)
		if builder == null or builder.state != 0 or not builder.inter or not at_bench(builder) or phase != "day" or Vector2(builder.pos).distance_squared_to(radio_build_pos) > 16 or float(builder.taken) != radio_build_damage:
			cancel_radio_build()
			if builder != null:
				builder.inter = false
	for p in players.values():
		p.activity = "idle"
		p.move_credit = minf(player_speed(p) * 0.2, p.move_credit + player_speed(p) * dt)
		p.invuln -= dt
		p.atk_cd -= dt
		p.swing -= dt
		p.note_t -= dt
		p.weapon = self.weapon_of(p)
		self.wear_armour(p)
		if p.state == 1:
			p.bleed -= dt
			if p.bleed <= 0:
				p.state = 2
				self.say("%s died" % [p.name])
		if p.state != 0:
			tick_tower_construction(p, 0)
			continue
		if p.channel > 0:
			p.activity = "heal"
			if p.atk:
				# attacking cancels the bandage and keeps it
				p.channel = 0.0
			else:
				p.channel -= dt
				if p.channel <= 0 and self.take(p, "bandage", 1):
					p.hp = minf(p.maxhp, p.hp + 30.0 * (1.5 if p.role == 1 else 1.0))
		if tick_tower_construction(p, float(dt)):
			continue
		if tick_field_repair(p, float(dt)):
			continue
		self.tick_entry(p, dt)
		self.tick_craft(p, dt)
		if p.inside:
			p.revive_p = 0.0
			continue
		if p.atk and p.atk_cd <= 0:
			self.attack(p)
		if p.inter:
			self.interact(p, dt)
		else:
			p.revive_p = 0.0
			p.gather_acc = 0.0

func tick_entry(p, dt) -> void:
	var door = -1
	if p.ent and p.role == 2 and phase == "night":
		for i in 3:
			if doors[i] > 0 and p.pos.distance_to(DOOR_POS[i]) <= 2.0 * T:
				door = i
	if door < 0 or p.entry > 0 and p.pos.distance_to(p.entry_pos) > 4.0:
		p.entry = 0.0
		return
	if p.entry == 0.0:
		p.entry_pos = p.pos
	p.entry += dt
	if p.entry < 2.0:
		return
	p.entry = 0.0
	p.inside = not p.inside
	if p.inside:
		self.teleport(p, DOOR_POS[door] - DOOR_DIR[door] * 1.2 * T)
		self.note(p, "Inside the base")
	else:
		self.teleport(p, DOOR_POS[door] + DOOR_DIR[door] * T)
		p.invuln = 0.5
const ARROW_SPEED: float = 16.0  # tiles/s

# MEC-02 / MEC-03: a melee swing hits every enemy in the 90-degree arc once; a bow fires one arrow.
func attack(p) -> void:
	var selected = selected_item(p)
	if is_tactical_use(selected):
		return
	var w = p.weapon
	if w == "long_spear":
		attack_long_spear(p)
		return
	var st = WEAPON_STATS[w]
	if st[4] and not self.take(p, "arrow", 1):
		self.note(p, "No arrows")
		p.atk_cd = 0.5
		return
	# BAL-07: the Scout attacks 1.25x faster
	p.atk_cd = 1.0 / (st[1] * (1.25 if p.role == 0 else 1.0))
	p.swing = 0.25
	var dir = Vector2.from_angle(p.aim)
	if st[3]:
		arrows.append({"pos": p.pos + dir * (0.4 * T), "vel": dir * ARROW_SPEED * T, "dmg": st[0], "left": st[2] * T - 0.4 * T, "owner": p.id})
		return
	var reach: float = float(st[2] * T)
	for e in enemies:
		var d: Vector2 = Vector2(e.pos - p.pos)
		if d.length() <= reach + e.r and (d.length() < 0.5 * T or dir.dot(d.normalized()) >= cos(PI / 4)) and self.line_clear(p.pos, e.pos):
			self.hit(e, st[0], p)
			if w == "stun_maul":
				if e.type == BOSS:
					e.slow_until = float(clock) + 1.0
				else:
					e.stun_until = float(clock) + 0.8
	enemies = enemies.filter(func(e): return e.hp > 0)

# Arrows fly toward the cursor, hit the first enemy on their way and stop at walls or at their range.
func tick_arrows(dt) -> void:
	for a in arrows:
		var start: Vector2 = Vector2(a.pos)
		var step: float = minf(a.vel.length() * dt, a.left)
		var end_: Vector2 = start + a.vel.normalized() * step
		var first = self.obstacle_hit(start, end_, 2.0)
		var victim = null
		for e in enemies:
			if not self.hittable(e):
				continue
			var h = WorldGeometry.circle_hit(start, end_, e.pos, e.r + 2.0)
			if h < first:
				first = h
				victim = e
		a.left -= step
		a.pos = start.lerp(end_, first) if first < INF else end_
		if first == INF and a.left <= 0 and a.get("explosive", false):
			explode_arrow(a)
		if first < INF:
			a.left = 0.0
			var owner = players.get(a.owner)
			if a.get("explosive", false):
				explode_arrow(a)
			elif victim != null and owner != null:
				self.hit(victim, a.dmg, owner)
	arrows = arrows.filter(func(a): return a.left > 0)
	enemies = enemies.filter(func(e): return e.hp > 0)

# FML-09: threat is the damage a player dealt to this enemy in the last 5 s.
func threat(e, pid) -> float:
	var sum: float = 0.0
	for h in e.hits:
		if h[1] == pid and clock - h[0] <= 5.0:
			sum += float(h[2])
	return sum

# Chase the top threat; switch only when a newcomer's threat beats the current target's by 20%.
func retarget(e) -> void:
	e.hits = e.hits.filter(func(h): return clock - h[0] <= 5.0)
	var cur: float = self.threat(e, e.target)
	for h in e.hits:
		var pid = h[1]
		var q = players.get(pid)
		if q != null and q.state == 0 and self.threat(e, pid) > 1.2 * cur:
			e.target = pid
			cur = self.threat(e, pid)

func hit(e, dmg, p) -> void:
	if e.hp <= 0 or not self.hittable(e):
		return
	dmg = self.boss_damage_taken(e, dmg, p.pos)
	e.hp -= dmg
	e.nodmg = 0.0
	p.dmg += dmg
	self.boss_on_hit(e)
	# Grunts and bosses retaliate unless locked on a door or tower; Wreckers never do (AGT-01..03).
	if (e.type == GRUNT or e.type == BOSS) and (e.state <= 1 or e.state == 4):
		e.hits.append([clock, p.id, dmg])
		if e.state == 0 or e.state == 4:
			# BAL-35: a boss ignores new threat for 10 s after its leash broke
			if e.get("calm", 0.0) <= 0:
				e.state = 1
				e.anchor = e.pos
				e.target = p.id
		else:
			self.retarget(e)
	if e.hp <= 0:
		self.drop(e)
		p.kills += 1
		if e.type == BOSS:
			boss_dead = true

func interact(p, dt) -> void:
	for q in players.values():
		if q.state == 1 and q.id != p.id and q.pos.distance_to(p.pos) <= 1.5 * T and self.line_clear(p.pos, q.pos):
			p.activity = "revive"
			p.activity_target = q.pos
			p.revive_p += dt / (10.0 if p.role == 1 else 20.0)
			if p.revive_p >= 1.0:
				p.revive_p = 0.0
				q.state = 0
				q.hp = 0.3 * q.maxhp
				q.invuln = 1.0
				p.revives += 1
				self.say("%s revived %s" % [p.name, q.name])
			return
	# a Revival kit is carried to a dead teammate's body and used there: hold E for REVIVE_KIT_TIME
	if self.count(p, "revive") > 0:
		for q in players.values():
			if q.state == 2 and q.id != p.id and q.pos.distance_to(p.pos) <= 1.5 * T and self.line_clear(p.pos, q.pos):
				p.activity = "revive"
				p.activity_target = q.pos
				p.revive_p += dt / REVIVE_KIT_TIME
				if p.revive_p >= 1.0 and self.take(p, "revive", 1):
					# MEC-07: back where they fell, with 50% HP
					p.revive_p = 0.0
					q.state = 0
					q.hp = q.maxhp * 0.5
					q.invuln = 1.0
					p.revives += 1
					self.say("%s is back" % [q.name])
				return
	p.revive_p = 0.0
	if self.pick_up(p):
		p.inter = false
		return
	if try_loot_chest(p, float(dt)) or try_build_radio(p, float(dt)):
		return
	# repairs need the Engineer's hammer (ITM-04) and wood (BAL-23)
	if p.role == 2 and not inside_base(p.pos) and self.count(p, "hammer") > 0:
		for i in 3:
			var dp: Vector2 = Vector2(DOOR_POS[i])
			if p.pos.distance_to(dp) <= 1.5 * T:
				var cap: float = float(DOOR_HP * 0.3 if broken[i] else DOOR_HP)
				if broken[i] and self.enemy_inside() or door_pause[i] > 0 or doors[i] >= cap or self.count(p, "wood") <= 0:
					return
				p.activity = "repair"
				p.activity_target = dp
				var amt = 15.0 * dt
				doors[i] = minf(cap, doors[i] + amt)
				p.repair_acc += amt
				if p.repair_acc >= 10.0:
					p.repair_acc -= 10.0
					self.take(p, "wood", 1)
				return
		for tw in towers:
			if tw != null and p.pos.distance_to(tw.pos) <= 1.5 * T:
				var tcap: float = float(TOWER_HP * 0.3 if tw.wrecked else TOWER_HP)
				if tw.wrecked and self.enemy_near(tw.pos, TOWER_LIGHT) or tw.pause > 0 or tw.hp >= tcap or self.count(p, "wood") <= 0:
					return
				p.activity = "repair"
				p.activity_target = tw.pos
				var tamt = 15.0 * dt
				tw.hp = minf(tcap, tw.hp + tamt)
				p.repair_acc += tamt
				if p.repair_acc >= 10.0:
					p.repair_acc -= 10.0
					self.take(p, "wood", 1)
				return
	for nd in nodes:
		if nd.amount > 0 and nd.pos.distance_to(p.pos) <= 1.5 * T and self.line_clear(p.pos, nd.pos, nd.pos):
			if self.room(p, RES[nd.type]) <= 0:
				self.note(p, "Bag full")
				p.gather_acc = 0.0
				return
			p.activity = "gather"
			p.activity_target = nd.pos
			if p.gather_acc > 0 and p.pos.distance_to(p.gather_pos) > 4.0:
				p.gather_acc = 0.0
			if p.gather_acc == 0.0:
				p.gather_pos = p.pos
			p.gather_acc += 0.35 * dt * (1.25 if count(p, "gathering_kit") > 0 else 1.0)
			if p.gather_acc >= 1.0:
				p.gather_acc = 0.0
				nd.amount -= 1
				add_to(p.inv, RES[nd.type], 1)
			return
	# nothing in reach
	p.gather_acc = 0.0

func hurt(p, dmg) -> void:
	# An Engineer inside the base can be hit too, but only enemies that broke in can reach them.
	if p.state != 0 or p.invuln > 0:
		return
	p.entry = 0.0
	# FML-06: armour reduces the hit, never below 1 damage; BAL-13: 1 s invulnerability
	var taken: float = maxf(1.0, roundf(dmg * (1.0 - minf(0.6, ARMOUR[p.armour]))))
	p.hp -= taken
	p.taken += taken
	p.invuln = 1.0
	p.channel = 0.0
	p.repair_kit_t = 0.0
	p.tower_build_t = 0.0
	p.revive_p = 0.0
	p.gather_acc = 0.0
	p.craft_idx = -1
	if radio_builder == int(p.id):
		cancel_radio_build()
	cancel_chest_unlock(p)
	if p.hp <= 0:
		p.hp = 0.0
		p.state = 1
		p.bleed = 30.0
		self.say("%s is down" % [p.name])

func flask(p, aim_point) -> void:
	if p.role != 1 or p.flasks <= 0 or p.state != 0:
		return
	var target = p
	for q in players.values():
		if q.state == 0 and q.id != p.id and q.pos.distance_to(p.pos) <= 3 * T and q.pos.distance_to(aim_point) < T:
			target = q
	target.hp = minf(target.maxhp, target.hp + 40.0)
	p.flasks -= 1

func say(s) -> void:
	msg = s
	msg_t = 6.0

func note(p, s) -> void:
	p.note = s
	p.note_t = 3.0

# ---- src/gameplay/GameSim.TacticalItems.cs
# Small bounded lists, checked at 4 Hz (traps) / 2 Hz (areas), without physics nodes or navigation changes.
var tactical_areas = []
var vine_traps = []
var next_trap_check: float = 0.0
var poisoned = {}
var healed = {}
const MAX_TACTICAL_AREAS: int = 16
const MAX_VINE_TRAPS: int = 12

static func is_tactical_use(id) -> bool:
	return id == "vine_trap" or id == "poison_flask" or id == "healing_flask" or id == "haste_tonic" or id == "repair_kit" or id == "explosive_arrow"

static func tactical_description(id) -> Variant:
	return ["22 damage, reach 2.6, hits two"] if id == "long_spear" else ["30 damage, stuns 0.8 s"] if id == "stun_maul" else ["Roots a foe for 2.5 s"] if id == "vine_trap" else ["8 damage/s for 5 s"] if id == "poison_flask" else ["Heals 8 HP/s for 5 s"] if id == "healing_flask" else ["+20% speed for 8 s"] if id == "haste_tonic" else ["Gather 20% faster"] if id == "gathering_kit" else ["Craft 20% faster while carried", "Passive; one kit is enough"] if id == "crafting_kit" else ["Unlocks one expedition chest", "Hold E for 2 s; consumed on completion", "Unlocked chests reopen for free"] if id == "chest_key" else ["32 blast damage"] if id == "explosive_arrow" else ["+100 HP to a door, wall or tower"] if id == "repair_kit" else []

func player_speed(p) -> float:
	return float(SPEED) * (1.2 if int(p.role) == 0 else 1.0) * (1.2 if p.get("haste_until", 0.0) > float(clock) else 1.0)

func reset_tactical_items() -> void:
	tactical_areas.clear()
	vine_traps.clear()
	next_trap_check = 0
	for p in players.values():
		p.repair_kit_t = 0.0
		p.haste_until = 0.0

func selected_item(p) -> Variant:
	var s = p.inv[int(p.sel)]
	return "" if s == null else Cs.s(s[0])

func use_selected_item(p, aim_point: Vector2) -> void:
	use_tactical_item(p, selected_item(p), aim_point)

func use_tactical_item(p, id, aim_point: Vector2) -> bool:
	if not is_tactical_use(id) or not aim_point.is_finite() or not phase in ["day", "dusk", "night"] or int(p.state) != 0 or p.inside or int(p.craft_idx) >= 0 or float(p.channel) > 0 or count(p, id) <= 0 or p.get("item_cd", 0.0) > float(clock) or p.get("repair_kit_t", 0.0) > 0:
		return false
	var pos: Vector2 = Vector2(p.pos)
	var at: Vector2 = pos + (aim_point - pos).limit_length((2.0 if id == "vine_trap" else 5.0) * T)
	var now: float = float(clock)
	if id == "haste_tonic":
		if p.get("haste_until", 0.0) > now:
			note(p, "Already active")
			return false
		p.haste_until = now + 8.0
	elif id == "repair_kit":
		return begin_field_repair(p, aim_point)
	elif id == "explosive_arrow":
		if count(p, "bow") + count(p, "starter_bow") == 0:
			note(p, "Needs a bow")
			return false
		if float(p.atk_cd) > 0:
			return false
		var dir: Vector2 = (aim_point - pos).normalized()
		if dir == Vector2.ZERO:
			dir = Vector2.from_angle(float(p.aim))
		arrows.append({"pos": pos + dir * 0.4 * T, "vel": dir * ARROW_SPEED * T, "dmg": 32.0, "left": 7.6 * T, "owner": p.id, "explosive": true})
		p.aim = dir.angle()
		p.swing = 0.25
		p.atk_cd = 1.0
	else:
		if not line_clear(pos, at) or not IslandMap.on_land(at, 8.0) or id == "vine_trap" and Vector2(world.resolve_fast(at, 8.0)).distance_to(at) > 1.0:
			note(p, "Blocked")
			return false
		if id == "vine_trap":
			var own: int = 0
			for trap in vine_traps:
				if trap.owner == p.id:
					own += 1
			if own >= 3 or vine_traps.size() >= MAX_VINE_TRAPS:
				note(p, "Trap limit")
				return false
			vine_traps.append({"pos": at, "owner": p.id, "until": now + 120.0})
		else:
			if tactical_areas.size() >= MAX_TACTICAL_AREAS:
				note(p, "Wait a moment")
				return false
			tactical_areas.append({"pos": at, "owner": p.id, "kind": "poison" if id == "poison_flask" else "heal", "until": now + 5.0, "next": now + 0.5})
	take(p, id, 1)
	p.item_cd = now + 0.6
	# the throw / drink plays as a short swing of the character
	if id != "explosive_arrow":
		p.swing = 0.25
	note(p, "Used " + item_name(id))
	return true

func tick_tactical_items() -> void:
	var now: float = float(clock)
	if now >= next_trap_check:
		next_trap_check = now + 0.25
		for i in range(vine_traps.size() - 1, -1, -1):
			var trap = vine_traps[i]
			if float(trap.until) <= now:
				vine_traps.remove_at(i)
				continue
			for e in enemies:
				if e.hp <= 0 or int(e.type) == BOSS or not hittable(e) or Vector2(e.pos).distance_to(Vector2(trap.pos)) > float(e.r) + 16.0:
					continue
				e.stun_until = maxf(e.get("stun_until", 0.0), now + 2.5)
				vine_traps.remove_at(i)
				break
	# Each actor receives a given area effect once per half-second, even where areas overlap.
	if tactical_areas.size() == 0:
		return
	poisoned.clear()
	healed.clear()
	for i in range(tactical_areas.size() - 1, -1, -1):
		var area = tactical_areas[i]
		var until: float = float(area.until)
		var next: float = float(area.next)
		if now >= next and next <= until and now <= until + 0.01:
			area.next = now + 0.5
			var owner = players.get(area.owner)
			if Cs.s(area.kind) == "poison" and owner != null:
				for e in enemies:
					if e.hp > 0 and hittable(e) and e.get("area_poison_at", 0.0) <= now and Vector2(e.pos).distance_to(Vector2(area.pos)) <= 1.8 * T + float(e.r) and line_clear(area.pos, e.pos) and Cs.set_add(poisoned, int(e.id)):
						e.area_poison_at = now + 0.5
						hit(e, 4.0, owner)
			elif Cs.s(area.kind) == "heal":
				for q in players.values():
					if int(q.state) == 0 and q.get("area_heal_at", 0.0) <= now and Vector2(q.pos).distance_to(Vector2(area.pos)) <= 1.8 * T and line_clear(area.pos, q.pos) and Cs.set_add(healed, int(q.id)):
						q.area_heal_at = now + 0.5
						q.hp = minf(float(q.maxhp), float(q.hp) + 4.0)
		if now >= until:
			tactical_areas.remove_at(i)
	# Hit() owns drops, boss progression, threat and player statistics.
	enemies = enemies.filter(func(e): return e.hp > 0)

func explode_arrow(arrow) -> void:
	var at: Vector2 = Vector2(arrow.pos)
	var owner = players.get(arrow.owner)
	if owner != null:
		for e in enemies:
			if e.hp > 0 and hittable(e) and Vector2(e.pos).distance_to(at) <= 1.5 * T + float(e.r) and line_clear(at, e.pos):
				hit(e, 32.0, owner)
	if tactical_areas.size() < MAX_TACTICAL_AREAS:
		tactical_areas.append({"pos": at, "owner": arrow.owner, "kind": "blast", "until": float(clock) + 0.3, "next": INF})

func tactical_snapshot() -> Variant:
	var out = []
	# [kind, pos, seconds left, owner]
	for a in tactical_areas:
		out.append([a.kind, a.pos, maxf(0.0, float(a.until) - float(clock)), a.owner])
	for a in vine_traps:
		out.append(["trap", a.pos, maxf(0.0, float(a.until) - float(clock)), a.owner])
	return out

func attack_long_spear(p) -> void:
	var pos: Vector2 = Vector2(p.pos)
	var dir: Vector2 = Vector2.from_angle(float(p.aim))
	var hits = []
	for e in enemies:
		var delta: Vector2 = Vector2(e.pos) - pos
		var along: float = delta.dot(dir)
		var side: float = absf(delta.cross(dir))
		if hittable(e) and along >= 0 and along <= 2.6 * T + float(e.r) and side <= 10.0 + float(e.r) and line_clear(pos, e.pos):
			hits.append([along, e])
	Cs.sort_cmp(hits, func(a, b): return Cs.compare(a[0], b[0]))
	for i in range(mini(2, hits.size())):
		hit(hits[i][1], 22.0, p)
	p.atk_cd = 0.8 if int(p.role) == 0 else 1.0
	p.swing = 0.25
	enemies = enemies.filter(func(e): return e.hp > 0)

func begin_field_repair(p, aim: Vector2) -> bool:
	if int(p.role) != 2 or inside_base(p.pos):
		note(p, "Use outside the base")
		return false
	var target = null
	var kind = ""
	var door: int = -1
	var best: float = T
	for i in range(3):
		var at: Vector2 = Vector2(DOOR_POS[i])
		var d: float = at.distance_to(aim)
		if d < best and field_door_cap(i) > float(doors[i]):
			best = d
			door = i
			kind = "door"
	for w in built_walls:
		if Vector2(w.pos).distance_to(aim) < best and float(w.hp) < float(w.maxhp):
			target = w
			best = Vector2(w.pos).distance_to(aim)
			kind = "wall"
	for w in towers:
		if w != null and Vector2(w.pos).distance_to(aim) < best and field_tower_cap(w) > float(w.hp):
			target = w
			best = Vector2(w.pos).distance_to(aim)
			kind = "tower"
	var spot: Vector2 = Vector2(DOOR_POS[door]) if kind == "door" else Vector2.INF if target == null else Vector2(target.pos)
	var approach: Vector2 = spot.move_toward(Vector2(p.pos), T) if spot.is_finite() else spot
	if kind == "" or spot.distance_to(Vector2(p.pos)) > 3.0 * T or not line_clear(p.pos, approach):
		note(p, "Nothing to repair here")
		return false
	p.repair_kit_kind = kind
	p.repair_kit_door = door
	p.repair_kit_target = target
	p.repair_kit_t = 3.0
	p.repair_kit_pos = p.pos
	p.repair_kit_spot = spot
	p.atk = false
	note(p, "Repairing...")
	return true

func field_door_cap(i: int) -> float:
	return 0.0 if float(door_pause[i]) > 0 or broken[i] and enemy_inside() else float(DOOR_HP) * (0.3 if broken[i] else 1.0)

func field_tower_cap(w) -> float:
	return 0.0 if float(w.pause) > 0 or w.wrecked and enemy_near(w.pos, TOWER_LIGHT) else float(TOWER_HP) * (0.3 if w.wrecked else 1.0)

func tick_field_repair(p, dt: float) -> bool:
	var left: float = p.get("repair_kit_t", 0.0)
	if left <= 0:
		return false
	if int(p.state) != 0 or p.atk or p.inside or Vector2(p.pos).distance_to(Vector2(p.repair_kit_pos)) > 4.0 or count(p, "repair_kit") == 0:
		p.repair_kit_t = 0.0
		note(p, "Repair cancelled")
		return false
	p.activity = "repair"
	p.activity_target = p.repair_kit_spot
	p.repair_kit_t = maxf(0.0, left - dt)
	if float(p.repair_kit_t) > 0:
		return true
	var kind = Cs.s(p.repair_kit_kind)
	var target = p.repair_kit_target
	var before: float = 0.0
	var after: float = 0.0
	if kind == "door":
		var i: int = int(p.repair_kit_door)
		before = float(doors[i])
		after = minf(field_door_cap(i), before + 100.0)
		if after > before:
			doors[i] = after
	elif target != null and (kind == "wall" and target in built_walls or kind == "tower" and target in towers):
		before = float(target.hp)
		after = minf(float(target.maxhp) if kind == "wall" else field_tower_cap(target), before + 100.0)
		if after > before:
			target.hp = after
	if after > before:
		take(p, "repair_kit", 1)
		note(p, "+" + Cs.s(Cs.round_even(after - before)) + " HP")
	else:
		note(p, "Nothing to repair")
	return true

# ---- src/gameplay/GameSim.Towers.cs
# Owner: Pham Minh Hieu. Defence towers: placing, shooting, light (AGT-12, MEC-11, MEC-28, BAL-28).
# Team change: a tower goes anywhere outside the base except on a landing pad (was 3-8 tiles in front of a door).
const TOWER_HP = 300.0
const TOWER_DPS = 8.0
const TOWER_RANGE = 6.0 * T
const TOWER_LIGHT = 6.0 * T
static var TOWER_COST = {"wood": 10, "titanium": 5}
# the team has this many tower slots (a wrecked tower keeps its slot and can be repaired)
const MAX_TOWERS: int = 3
# the tower's footprint (its collider, WorldGeometry.DynamicColliders)
const TOWER_R: float = 0.7

# --- towers (AGT-12, MEC-11, MEC-28) -------------------------------------
# A tower goes anywhere outside the base on open land, except on a helicopter landing pad (FML-13) and
# right in front of a door. Returns "" when the spot is fine, else what is wrong.
static func tower_spot_problem(pos: Vector2, world_) -> Variant:
	if inside_base(pos, TOWER_R * T):
		return "Towers go outside the base"
	for i in 3:
		if pos.distance_to(Vector2(pad_pos(i))) < float(PAD_R) + TOWER_R * T:
			return "Keep the landing pads clear"
		if pos.distance_to(Vector2(DOOR_POS[i])) < WALL_DOOR_GAP * T:
			return "Too close to a door (3 tiles)"
	if not IslandMap.on_land(pos, TOWER_R * T):
		return "Too close to the water"
	var ground = world_
	if world_ is WorldGeometry and ground.terrain_at(pos, TOWER_R * T) == 1:
		return "Needs dry land for the dirt foundation"
	if world_ != null and Vector2(world_.resolve(pos, TOWER_R * T)) != pos:
		return "Something is in the way"
	return ""

# The first free tower slot, or -1 when all MAX_TOWERS are taken.
func free_tower_slot() -> int:
	for i in range(MAX_TOWERS):
		if towers[i] == null and not construction_slot_taken(i):
			return i
	return -1

# Where the unplaced free tower appears at dusk: in front of the door, beside its landing pad.
func free_tower_spot(door: int) -> Vector2:
	var dp: Vector2 = Vector2(DOOR_POS[door])
	var dd: Vector2 = Vector2(DOOR_DIR[door])
	var side: Vector2 = Vector2(-dd.y, dd.x)
	var out: float = float(PAD_DIST)
	var off: float = float(PAD_R) + (TOWER_R + 1.0) * T
	for grow in [0.0, 1.0, 2.0, 3.0, 4.0]:
		for sgn in [1.0, -1.0]:
			for along in [0.0, -1.5, 1.5]:
				var p: Vector2 = dp + dd * (out + along * T) + side * (sgn * (off + grow * T))
				if tower_spot_problem(p, self.world) == "":
					return p
	# nothing free beside the pad: just past its far edge
	return dp + dd * (out + float(PAD_R) + (TOWER_R + 1.0) * T)

func build_tower(p, at = null) -> void:
	var spot: Vector2 = Vector2(at if at != null else p.pos)
	if not spot.is_finite() or not phase in ["day", "dusk", "night"]:
		return
	if p.craft_idx >= 0 or p.channel > 0 or p.get("repair_kit_t", 0.0) > 0:
		note(p, "Finish your current action first")
		return
	if p.role != 2 or p.state != 0 or p.inside:
		self.note(p, "Engineer only")
		return
	if self.count(p, "hammer") <= 0:
		self.note(p, "Needs the hammer")
		return
	if spot.distance_to(p.pos) > BUILD_REACH * T:
		self.note(p, "Too far away to build there")
		return
	# T places it at the Engineer's feet (they stand there already); from the build menu the spot must be free of people too
	var why = tower_spot_problem(spot, self.world if at != null else null)
	if why == "" and at != null and players.values().any(func(q): return q.pos.distance_to(spot) < 1.1 * T):
		why = "Someone is standing there"
	if why == "" and towers.any(func(tw): return tw != null and Vector2(tw.pos).distance_to(spot) < 2.0 * TOWER_R * T):
		why = "Something is in the way"
	if why != "":
		self.note(p, why)
		return
	var i: int = self.free_tower_slot()
	if i < 0:
		self.note(p, "The team already has %d towers" % [MAX_TOWERS])
		return
	if not free_tower:
		if unlock < 2:
			note(p, "Unlocks after the first boss")
			return
		for res in TOWER_COST:
			if count(p, res) < TOWER_COST[res]:
				note(p, "A tower costs 10 wood + 5 titanium")
				return
	if not reserve_tower(p, i, spot):
		note(p, "Tower foundation is already reserved")

func place_tower(slot, pos) -> void:
	towers[slot] = {"slot": slot, "pos": pos, "hp": TOWER_HP, "wrecked": false, "pause": 0.0, "target": null}

func tick_towers(dt) -> void:
	# A standing tower shoots the nearest enemy within 6 tiles at 8 DPS, day or night.
	for tw in towers:
		if tw == null:
			continue
		tw.pause -= dt
		tw.target = null
		if tw.hp <= 0:
			continue
		var best = null
		var bd = TOWER_RANGE
		for e in enemies:
			var d: float = float(e.pos.distance_to(tw.pos))
			if e.hp > 0 and d <= bd and self.line_clear(tw.pos, e.pos, tw.pos):
				bd = d
				best = e
		if best != null:
			tw.target = best.pos
			best.hp -= TOWER_DPS * dt
			best.nodmg = 0.0
			if best.hp <= 0:
				self.drop(best)
				if best.type == BOSS:
					boss_dead = true
	enemies = enemies.filter(func(e): return e.hp > 0)

func tower_in_light(pos) -> Variant:
	for tw in towers:
		if tw != null and tw.hp > 0 and pos.distance_to(tw.pos) <= TOWER_LIGHT:
			return tw
	return null

# ---- src/gameplay/GameSim.Walls.cs
# Owner: Pham Minh Hieu. Engineer construction from the build menu (B): wood and stone walls (ITM-07, ITM-11,
# MEC-11, BAL-27). A wall is 3 tiles long and 1 tile thick; enemies whose way it blocks attack it.
# kind -> [name, cost, unlock tier, HP]
static var WALL_KINDS = {"wood": ["Wood wall", {"wood": 5}, 0, 200.0], "stone": ["Stone wall", {"stone": 5}, 1, 400.0]}
const BUILD_REACH: float = 5.0  # tiles from the Engineer
const WALL_DOOR_GAP: float = 3.0  # BAL-27: no wall within 3 tiles of a door
const MAX_WALLS: int = 30
var built_walls = []

# Why a structure cannot go there, or "" when it can (shared by the server and the client preview).
static func wall_spot_problem(pos, vert: bool, world_, walls_) -> Variant:
	for piece in WorldGeometry.wall_pieces(pos, vert):
		if inside_base(piece, 0.6 * T):
			return "Walls go outside the base"
		for i in 3:
			if piece.distance_to(DOOR_POS[i]) < WALL_DOOR_GAP * T:
				return "Too close to a door (3 tiles)"
		if not IslandMap.on_grass(Vector2(piece), T):
			return "Too close to the shore"
		if world_.resolve(piece, 0.5 * T) != piece:
			return "Something is in the way"
	return ""

static func snap_to_tile(pos: Vector2) -> Vector2:
	return (pos / T).round() * T

# Action "build": [kind, position, vertical]; kind is "wood", "stone" or "tower".
func build(p, arg) -> void:
	if not arg is Array or arg.size() < 3 or not arg[1] is Vector2:
		return
	var kind = Cs.s(arg[0])
	var pos: Vector2 = Vector2(arg[1])
	if kind == "tower":
		self.build_tower(p, pos)
		return
	if not WALL_KINDS.has(kind):
		return
	var w = WALL_KINDS[kind]
	if p.role != 2 or p.state != 0 or p.inside:
		self.note(p, "Engineer only, outside the base")
		return
	if self.count(p, "hammer") <= 0:
		self.note(p, "Needs the hammer")
		return
	if w[2] > unlock:
		self.note(p, "%s unlocks after night 1" % [w[0]])
		return
	pos = snap_to_tile(pos)
	if pos.distance_to(p.pos) > BUILD_REACH * T:
		self.note(p, "Too far away to build there")
		return
	if built_walls.size() >= MAX_WALLS:
		self.note(p, "At most %d walls" % [MAX_WALLS])
		return
	var vert: bool = arg[2]
	var why = wall_spot_problem(pos, vert, world, built_walls)
	if why == "" and self.player_on_wall(pos, vert):
		why = "Someone is standing there"
	if why != "":
		self.note(p, why)
		return
	for res in w[1]:
		if self.count(p, res) < w[1][res]:
			self.note(p, "Need more %s" % [res])
			return
	for res in w[1]:
		self.take(p, res, w[1][res])
	# MEC-11: the structure appears at once with full HP
	built_walls.append({"id": next_id, "kind": kind, "pos": pos, "vert": vert, "hp": w[3], "maxhp": w[3]})
	next_id += 1
	world.rebuild(nodes, towers, built_walls)
	self.note(p, "Built a " + Cs.s(w[0]))

func player_on_wall(pos: Vector2, vert: bool) -> bool:
	for piece in WorldGeometry.wall_pieces(pos, vert):
		for q in players.values():
			if q.pos.distance_to(piece) < 0.9 * T:
				return true
	return false

# The wall an enemy is pressed against, if any.
func wall_touching(pos, r) -> Variant:
	for w in built_walls:
		for piece in WorldGeometry.wall_pieces(w.pos, w.vert):
			if pos.distance_to(piece) <= r + WorldGeometry.WALL_PIECE * T + 4.0:
				return w
	return null

func damage_wall(e, w, dmg) -> void:
	w.hp -= dmg * (2.0 if e.type == WRECKER else 1.0)
	if w.hp <= 0:
		built_walls = built_walls.filter(func(x): return x.id != w.id)
		self.say("A %s was destroyed" % [WALL_KINDS[w.kind][0].to_lower()])

# ---- src/match/GameSim.Phases.cs
# Owner: Pham Minh Hieu. Day / dusk / night cycle, win and loss, extraction (MEC-14..22, FML-13).

# --- phases -------------------------------------------------------------
func dawn() -> void:
	cancel_radio_build()
	reset_night_spawns()
	reset_tactical_items()
	cancel_all_construction()
	phase = "day"
	t = DAY_MAX
	votes.clear()
	broken = [false, false, false]
	for tw in towers:
		if tw != null:
			tw.wrecked = false
	for nd in nodes:
		nd.amount = 5
	for p in players.values():
		p.inside = false
		p.entry = 0.0
		if p.role == 1:
			p.flasks = 3
	var idx = [0, 1, 2]
	idx.shuffle()
	active = idx.slice(0, mini(3, k))
	active.sort()
	var names = PackedStringArray(active.map(func(i): return DOOR_NAMES[i]))
	var s = "Day %d/5 - %s %s tonight" % [k, "door" if names.size() == 1 else "doors", Cs.join(", ", names)]
	if k == 2 or k == 4:
		# FML-05: tonight's boss is announced at dawn so the team can prepare
		s += " - boss %s" % [boss_title(self.boss_kind_for(k)).to_upper()]
	if k == 5:
		s = "Day 5/5 - radio online: keep a landing pad clear tonight" if radio_built else "Day 5/5 - LAST CHANCE: assemble the locator radio before tonight!"
	if unlock > shown_unlock:
		shown_unlock = unlock
		s += "  |  new recipes"
	self.say(s)

func dusk() -> void:
	cancel_radio_build()
	phase = "dusk"
	t = DUSK
	for e in enemies:
		if e.has("home"):
			e.erase("home")
			e.state = 0
			e.hits.clear()
			e.door = self.nearest_active_door(e.pos)
	if free_tower and construction.size() == 0 and k == 1:
		free_tower = false
		var d: int = int(active[0])
		var slot: int = self.free_tower_slot()
		if slot >= 0:
			# beside the door's landing pad, never on it
			self.place_tower(slot, self.free_tower_spot(d))
	self.say("Dusk - night in %d s" % [DUSK])

func night() -> void:
	cancel_radio_build()
	if int(k) == 5 and not radio_built:
		end(false, "Locator radio incomplete: find all 3 parts in landmark chests and assemble it before night 5")
		return
	phase = "night"
	t = NIGHT_LEN[k - 1]
	elapsed = 0.0
	wave = 0
	reset_night_spawns()
	next_wave = night_first_wave_delay
	boss_spawned = false
	boss_down_tonight = false
	boss_at = BOSS_AT
	self.plan_night_quota()
	cancel_all_construction()
	ext = -1.0
	pad_clear = [0.0, 0.0, 0.0]
	for p in players.values():
		p.inside = false
		if inside_base(p.pos, 0.5 * T):
			var d = self.nearest_door(p.pos)
			self.teleport(p, DOOR_POS[d] + DOOR_DIR[d] * T)
	self.say("Night %d" % [k])

func night_cleared() -> void:
	if k == 1:
		unlock = maxi(unlock, 1)
	enemies.clear()
	proj.clear()
	tele.clear()
	k += 1
	self.dawn()

func end(win, why) -> void:
	cancel_all_construction()
	reset_tactical_items()
	reset_night_spawns()
	clear_supplies()
	# section 2.5: the result screen stays until the players go back to the lobby
	phase = "result"
	t = 0.0
	result = [win, why, k]
	msg = ("MISSION COMPLETE - " if win else "BEACON LOST - ") + Cs.s(why) + " (night %d)" % [k]
	enemies.clear()
	proj.clear()
	tele.clear()

func to_lobby() -> void:
	cancel_all_construction()
	reset_tactical_items()
	reset_night_spawns()
	clear_supplies()
	phase = "lobby"
	msg = ""
	result = null
	drops.clear()
	enemies.clear()
	proj.clear()
	tele.clear()
	for p in players.values():
		self.reset_player(p)

func check_end(dt) -> void:
	if base_hp <= 0:
		self.end(false, "Generator destroyed at door %s" % [DOOR_NAMES[last_door]] if last_door >= 0 else "Generator destroyed")
		return
	if self.team_wiped():
		self.end(false, "Team wiped")
		return
	# the boss counts toward the night's quota like any other enemy
	if boss_dead:
		boss_dead = false
		boss_down_tonight = true
		bosses_killed += 1
		unlock = maxi(unlock, 2)
	# Clearing the quota advances dawn. Final-night extraction also requires the crafted rescue equipment.
	if t > 0 and self.night_all_down():
		if k < 5:
			self.night_cleared()
			self.say("Dawn comes early")
			return
		t = 0.0
		self.say("The helicopter is coming")
	if t > 0:
		return
	if (k == 2 or k == 4) and not boss_down_tonight:
		self.end(false, "Specimen still alive at dawn")
		return
	if k < 5:
		self.night_cleared()
		return
	if not supplies_all_in():
		end(false, "Locator radio incomplete: find all 3 parts in landmark chests and assemble it before night 5")
		return
	# night 5 extraction (FML-13)
	var clear = []
	for i in 3:
		clear.append(self.pad_is_clear(i))
	if ext < 0:
		if true in clear:
			self.end(true, "Helicopter landed at pad %s" % [DOOR_NAMES[clear.find(true)]])
		elif base_hp > 500:
			ext = 60.0
			self.say("Keep a landing pad clear!")
		else:
			self.end(false, "No landing pad was clear")
		return
	ext -= dt
	for i in 3:
		pad_clear[i] = pad_clear[i] + dt if clear[i] else 0.0
		if pad_clear[i] >= 10.0:
			self.end(true, "Helicopter landed at pad %s" % [DOOR_NAMES[i]])
			return
	if ext <= 0:
		self.end(false, "Extension ended without a clear pad")

func team_wiped() -> Variant:
	return not players.is_empty() and players.values().filter(func(p): return p.state == 0).is_empty()

func near_pad(p) -> Variant:
	for i in 3:
		if p.distance_to(pad_pos(i)) < PAD_R + T:
			return true
	return false

func pad_is_clear(i) -> Variant:
	var pad = pad_pos(i)
	for e in enemies:
		if e.pos.distance_to(pad) < PAD_R:
			return false
	return true
# MEC-17 / BAL-25: once no door is broken, players inside (other than the Engineer who entered) get a
# 5 s warning and are then moved to the nearest door.
var breach_end_t = -1.0

func tick_breach_end(dt) -> void:
	var caught = players.values().filter(func(p): return p.state != 2 and not p.inside and inside_base(p.pos))
	if self.any_breach() or caught.is_empty():
		breach_end_t = -1.0
		return
	if breach_end_t < 0:
		breach_end_t = 5.0
		self.say("Everyone out in 5 s")
	breach_end_t -= dt
	if breach_end_t > 0:
		return
	breach_end_t = -1.0
	for p in caught:
		var d = self.nearest_door(p.pos)
		self.teleport(p, DOOR_POS[d] + DOOR_DIR[d] * T)

func any_breach() -> Variant:
	return doors.any(func(d): return d <= 0)

func enemy_inside() -> Variant:
	for e in enemies:
		if e.state == 3 and inside_base(e.pos):
			return true
	return false

func nearest_door(pos) -> Variant:
	var best = 0
	for i in 3:
		if pos.dot(DOOR_DIR[i]) > pos.dot(DOOR_DIR[best]):
			best = i
	return best

func teleport(p, pos) -> void:
	p.pos = world.resolve(pos, 0.3 * T)
	p.nav_t = 0.0
	p.tp += 1

# ---- src/world/GameSim.Supplies.cs
static var SUPPLY_NAMES = ["Radio battery", "Antenna assembly", "Locator circuit"]
static var COMPONENT_IDS = ["battery_cells", "antenna_assembly", "locator_circuit"]
static var RESCUE_IDS = ["locator_radio"]
const SUPPLY_COUNT: int = 3
const SUPPLY_PICK: float = 1.1
const RADIO_BUILD_TIME: float = 3.0
const EXPLORATION_CHEST_SLOTS: int = 8
const CHEST_UNLOCK_TIME: float = 2.0
const CHEST_REACH: float = 44.0
static var CHEST_SUPPORT = ["bandage", "revive", "light_armour", "stone_armour", "titanium_armour", "vine_trap", "poison_flask", "healing_flask", "haste_tonic", "gathering_kit", "repair_kit"]
var exploration_chests = []
var rescue_components = Cs.arr(3, 0)
var radio_built: bool = false
var radio_builder
var radio_build_t: float = 0.0
var radio_build_pos: Vector2 = Vector2.ZERO
var radio_build_damage: float = 0.0
var chest_scenery = []
var chest_resources = []
static var chest_prop_anchors = null

static func rescue_kind(id) -> int:
	return RESCUE_IDS.find(id)

func supplies_got() -> int:
	var count_: int = 0
	for n in rescue_components:
		if n > 0:
			count_ += 1
	return count_

func supplies_all_in() -> bool:
	return radio_built

func craft_have(p, id) -> int:
	var kind: int = COMPONENT_IDS.find(id)
	return rescue_components[kind] if kind >= 0 else count(p, id)

func cancel_radio_build() -> void:
	radio_builder = null
	radio_build_t = 0

func clear_supplies() -> void:
	exploration_chests.clear()
	Cs.array_clear(rescue_components)
	radio_built = false
	cancel_radio_build()
	for p in players.values():
		cancel_chest_unlock(p)
		p.open_chest = -1
		p.chest_use_seq = 0

func place_supplies() -> void:
	clear_supplies()
	cache_chest_scenery()
	var random = rng
	var places = Array(IslandMap.landmarks).duplicate()
	Cs.remove_all(places, func(l): return l.id <= 4)
	for i in range(places.size() - 1, 0, -1):
		var j: int = random.randi_range(0, i)
		var _t1 = places[j]
		var _t2 = places[i]
		places[i] = _t1
		places[j] = _t2
	var support = CHEST_SUPPORT.duplicate()
	for i in range(support.size() - 1, 0, -1):
		var j: int = random.randi_range(0, i)
		var _t3 = support[j]
		var _t4 = support[i]
		support[i] = _t3
		support[j] = _t4
	for i in range(20):
		var c = Cs.init(ExplorationChest.new(), {"id": i, "landmark": places[i].id if i < places.size() else 0, "pos": chest_spot(places[i], random.randf_range(0, TAU)) if i < places.size() else scattered_chest_spot(random)})
		add_to(c.slots, "wood", random.randi_range(4, 10))
		add_to(c.slots, "stone", random.randi_range(3, 8))
		add_to(c.slots, "herb", random.randi_range(2, 5))
		add_to(c.slots, "titanium", random.randi_range(1, 3))
		# Guaranteed variety, one support stack per chest at most. Total loot fits the eight real slots.
		if i < support.size() or random.randf() < 0.35:
			add_to(c.slots, support[i % support.size()], 1)
		if i < 3:
			c.component = i
			add_to(c.slots, COMPONENT_IDS[i], 1)
		exploration_chests.append(c)

func scattered_chest_spot(random) -> Vector2:
	var w = world
	for attempt in range(20000):
		var at: Vector2 = (IslandMap.box.position + Vector2(random.randf(), random.randf()) * IslandMap.box.size).round()
		var remote: bool = true
		for id in w.lm_at:
			var centre = w.lm_at[id]
			if at.distance_squared_to(centre) < 520 * 520:
				remote = false
				break
		if remote and chest_spot_free(at, chest_scenery):
			return at
	Cs.fail("No accessible scattered chest spot")
	return Vector2.ZERO

func chest_spot(l, angle: float) -> Vector2:
	var w = world
	var centre: Vector2 = w.lm_at.get(l.id, l.pos)
	var search = Rect2(centre - Vector2.ONE * 432, Vector2.ONE * 864)
	var scenery = chest_scenery.filter(func(b): return b.intersects(search))
	var ring: float = 2
	while ring <= 12:
		for i in range(24):
			var at: Vector2 = (centre + Vector2.from_angle(angle + i * TAU / 24) * ring * 32).round()
			if chest_spot_free(at, scenery):
				return at
		ring += 0.75
	Cs.fail("No accessible chest spot at landmark %s" % [l.id])
	return Vector2.ZERO

func chest_spot_free(at: Vector2, scenery) -> bool:
	var footprint = Rect2(at - Vector2(20, 33), Vector2(40, 40)).grow(8)
	for bounds in scenery:
		if bounds.intersects(footprint):
			return false
	for resource in chest_resources:
		if resource.distance_squared_to(at) < 56 * 56:
			return false
	for other in exploration_chests:
		if other.pos.distance_squared_to(at) < 160 * 160:
			return false
	var w = world
	if Cs.has_point(w.weather_room.grow(48), at) or not IslandMap.on_land(at, 32) or w.terrain_at(at) == 1 or not w.resolve_fast(at, 26).is_equal_approx(at) or inside_base(at) or IslandMap.in_bridge_approach(at, 32):
		return false
	return not w.in_cave_region(at) or w.in_cave_floor(at)

# Physical trunks/vehicle bodies are smaller than their pictures. Reserve the complete artwork too.
# Cache once at placement, with no texture loads, scene nodes or per-frame scenery scans.
func cache_chest_scenery() -> void:
	chest_scenery.clear()
	chest_resources.clear()
	if chest_prop_anchors == null:
		chest_prop_anchors = {}
		var index = JSON.parse_string(FileAccess.get_file_as_string("res://art/landmarks/index.json"))
		for key in index.keys():
			var meta = index[key]
			chest_prop_anchors[str(key)] = Vector2(float(meta[0]), float(meta[1]))
	var w = world
	for _it1 in w.lm_props:
		var at = _it1[0]
		var sprite = _it1[1]
		var anchor: Vector2 = chest_prop_anchors.get(sprite, Vector2(32, 48))
		chest_scenery.append(Rect2(at - anchor, Vector2(anchor.x * 2, anchor.y + 12)))
	for v in w.expedition_vehicles:
		chest_scenery.append(Rect2(v.pos - Vector2(112, 120), Vector2(224, 150)))
	for cliff in w.expedition_cliffs:
		add_bounds(cliff.cap)
	for b in w.compound_boundaries:
		add_bounds(b.face)
	for o in w.lookout_objects:
		chest_scenery.append(Rect2(o.pos - (Vector2(88, 110) if o.model == 0 else Vector2(38, 106)), Vector2(176, 140) if o.model == 0 else Vector2(76, 125)))
	for node in nodes:
		var type: int = int(node.get("type"))
		var at: Vector2 = Vector2(node.get("pos"))
		chest_resources.append(at)
		var width: float = 42 if type == 0 else 18 if type == 2 else 28
		var height: float = 110 if type == 0 else 26 if type == 2 else 60
		chest_scenery.append(Rect2(at - Vector2(width, height), Vector2(width * 2, height + 10)))

func add_bounds(points) -> void:
	if points.size() == 0:
		return
	var lo: Vector2 = points[0]
	var hi: Vector2 = points[0]
	for p in points:
		lo = lo.min(p)
		hi = hi.max(p)
	chest_scenery.append(Rect2(lo, hi - lo))

func chest_by_id(id: int) -> Variant:
	return exploration_chests[id] if id >= 0 and id < exploration_chests.size() else null

func in_chest_reach(p, c) -> bool:
	return c != null and p.state == 0 and (phase == "day" or phase == "dusk" or phase == "night") and not p.inside and Vector2(p.pos).distance_squared_to(c.pos) <= CHEST_REACH * CHEST_REACH and line_clear(p.pos, c.pos)

func can_access_chest(p, c) -> bool:
	return c != null and c.opened and in_chest_reach(p, c)

func open_chest_panel(p, id: int) -> void:
	var c = chest_by_id(id)
	if not can_access_chest(p, c):
		return
	cancel_chest_unlock(p)
	p.open_chest = id
	p.chest_use_seq = p.get("chest_use_seq", 0) + 1
	p.inter = false
	p.activity = "loot"
	p.activity_target = c.pos

func cancel_chest_unlock(p) -> void:
	var c = chest_by_id(p.get("unlock_chest", -1))
	if c != null and c.unlocker == int(p.id):
		c.unlocker = null
		c.unlock_progress = 0
	p.unlock_chest = -1
	p.unlock_t = 0.0

func unlock_interrupted(p, c) -> bool:
	return not in_chest_reach(p, c) or not p.inter or p.atk or p.craft_idx >= 0 or float(p.channel) > 0 or count(p, "chest_key") == 0 or Vector2(p.pos).distance_squared_to(Vector2(p.unlock_pos)) > 16 or float(p.taken) != float(p.unlock_damage)

func tick_chest_access() -> void:
	for p in players.values():
		var id: int = p.get("unlock_chest", -1)
		if id >= 0 and unlock_interrupted(p, chest_by_id(id)):
			cancel_chest_unlock(p)
			p.inter = false
		if p.get("open_chest", -1) >= 0 and not can_access_chest(p, chest_by_id(int(p.open_chest))):
			p.open_chest = -1

func try_loot_chest(p, dt: float = 0.05) -> bool:
	var current_phase = phase
	if p.state != 0 or not (current_phase == "day" or current_phase == "dusk" or current_phase == "night"):
		return false
	for c in exploration_chests:
		if not in_chest_reach(p, c):
			continue
		if c.opened:
			open_chest_panel(p, c.id)
			return true
		if count(p, "chest_key") == 0:
			note(p, "Locked - craft a chest key (2 wood + 2 stone)")
			p.inter = false
			return true
		var player_id: int = int(p.id)
		if c.unlocker != null and c.unlocker != player_id:
			note(p, "A teammate is unlocking this chest")
			return true
		if int(p.unlock_chest) != c.id:
			cancel_chest_unlock(p)
			p.unlock_chest = c.id
			p.unlock_pos = p.pos
			p.unlock_damage = p.taken
			c.unlocker = player_id
		if unlock_interrupted(p, c):
			cancel_chest_unlock(p)
			return true
		p.unlock_t += maxf(0, dt)
		c.unlock_progress = minf(1, float(p.unlock_t) / CHEST_UNLOCK_TIME)
		p.activity = "loot"
		p.activity_target = c.pos
		if float(p.unlock_t) + 0.00001 >= CHEST_UNLOCK_TIME and take(p, "chest_key", 1):
			c.opened = true
			c.opened_at = float(clock)
			cancel_chest_unlock(p)
			open_chest_panel(p, c.id)
			note(p, "Chest unlocked - drag only the items you want into your bag")
		return true
	cancel_chest_unlock(p)
	return false

# Only physically reachable, unlocked contents are published for the player's storage panel.
func chest_contents_snapshot() -> Variant:
	var views = {}
	for p in players.values():
		var c = chest_by_id(p.get("open_chest", -1))
		if can_access_chest(p, c):
			views[p.id] = [c.id, Array(c.slots.map(func(s):
				var a = s
				return Array(a).duplicate() if s is Array else s
			)).duplicate()]
	return views

func try_build_radio(p, dt: float) -> bool:
	if radio_built or p.state != 0 or not at_bench(p):
		return false
	if phase != "day":
		note(p, "Assemble the locator radio by day, before night 5")
		return true
	if supplies_got() < 3:
		note(p, "Find all 3 radio parts in landmark chests (" + str(supplies_got()) + "/3)")
		return true
	if p.craft_idx >= 0:
		note(p, "Finish crafting before assembling the radio")
		return true
	var id: int = int(p.id)
	if radio_builder != null and radio_builder != id:
		note(p, "A teammate is assembling the radio")
		return true
	if not radio_builder != null:
		radio_builder = id
		radio_build_pos = p.pos
		radio_build_damage = p.taken
	if Vector2(p.pos).distance_squared_to(radio_build_pos) > 16 or float(p.taken) != radio_build_damage:
		cancel_radio_build()
		return true
	radio_build_t += dt
	p.activity = "craft"
	p.activity_target = WORKBENCH_POS
	if radio_build_t >= RADIO_BUILD_TIME:
		radio_built = true
		cancel_radio_build()
		say("Locator radio online. Rescue is confirmed for night 5 - defend a landing pad!")
	return true

# Public chest state only. Contents and component assignments stay hidden.
func supply_snapshot() -> Variant:
	var rows = []
	for c in exploration_chests:
		rows.append([c.id, c.pos, c.empty, c.landmark, c.opened, c.unlock_progress, clampf((float(clock) - c.opened_at) / 0.35, 0, 1) if c.opened else 0])
	return rows

func rescue_snapshot() -> Variant:
	return [[rescue_components[0], rescue_components[1], rescue_components[2]], radio_built, radio_build_t / RADIO_BUILD_TIME]

# ---- src/world/GameSim.World.cs
# Owner: Vu Duc Viet Anh. Island and base geometry, collision, navigation and resource nodes (shared by every peer).
const T: float = 32.0  # pixels per tile; const so every partial file can use it in its static fields
const HALF = Vector2(5.5, 3.5) * T
const SAFE_D = 20.0 * T
# how far the island reaches from the outpost; its real outline (and the sea) is src/world/IslandMap.cs
const ISLAND = 126.0 * T
static var DOOR_DIR = [Vector2(-1, 0), Vector2(0, -1), Vector2(1, 0)]
static var DOOR_POS = [Vector2(-HALF.x, 0), Vector2(0, -HALF.y), Vector2(HALF.x, 0)]
static var DOOR_NAMES = ["A", "B", "C"]
const PAD_DIST = 5.5 * T
const PAD_R = 4.0 * T
static var NODE_MUL: int = WorldSettings.get_current().node_multiplier
# The base furniture, both against the north wall and out of the door lanes: the workbench (crafting, MEC-10)
# west of door B, the shared storage chest (BAL-18) east of it. The ground point (front edge, middle).
const WORKBENCH_POS: Vector2 = Vector2(-3.2, -2.0) * T
const STORAGE_POS: Vector2 = Vector2(3.3, -2.0) * T
# how close you have to stand to use them
const BENCH_REACH: float = 2.2 * T
const STORAGE_REACH: float = 1.8 * T

static func near_bench(pos: Vector2) -> bool:
	return pos.distance_to(WORKBENCH_POS) <= BENCH_REACH

static func near_storage(pos: Vector2) -> bool:
	return pos.distance_to(STORAGE_POS) <= STORAGE_REACH

# Colliders of the base furniture as [centre, radius]: a row of circles along the bench, two under the chest.
# They reach back to the wall so nobody slips behind.
static func furniture_solids() -> Variant:
	var out = []
	for x in [-19.0, -6.0, 6.0, 19.0]:
		out.append([WORKBENCH_POS + Vector2(x, -10.0), 12.0])
	for x in [-7.0, 7.0]:
		out.append([STORAGE_POS + Vector2(x, -7.0), 10.0])
	return out

# --- helpers ------------------------------------------------------------
static func wall_dist(pos) -> Variant:
	# Distance to the nearest base wall (0 inside); every zone distance in the GDD uses this.
	return Vector2(maxf(absf(pos.x) - HALF.x, 0.0), maxf(absf(pos.y) - HALF.y, 0.0)).length()

static func in_door_lane(pos, half_width = null) -> Variant:
	if half_width == null: half_width = 1.5 * T
	# The 3-tile corridor in front of each door that stays clear (section 4.3).
	for i in 3:
		var rel: Vector2 = Vector2(pos - DOOR_POS[i])
		var dd: Vector2 = Vector2(DOOR_DIR[i])
		if rel.dot(dd) > 0 and absf(rel.dot(Vector2(-dd.y, dd.x))) < half_width:
			return true
	return false

func rand_zone_point(lo, hi) -> Variant:
	# Random point lo..hi from the nearest base wall, on the island, off the door lanes and pads.
	for i in 400:
		var p = Vector2(rng.randf_range(-ISLAND, ISLAND), rng.randf_range(-ISLAND, ISLAND))
		var d = wall_dist(p)
		if d >= lo and d <= hi and IslandMap.on_grass(Vector2(p), 2 * T) and not in_door_lane(p) and not self.near_pad(p):
			return p
	return Vector2(0, HALF.y + lo)

static func inside_base(pos, margin = null) -> Variant:
	if margin == null: margin = 0.0
	return absf(pos.x) < HALF.x + margin and absf(pos.y) < HALF.y + margin
# The wall pieces for each of the 8 ways the three doors can be open or shut, built once: bodies are pushed
# out of them several times a tick. Callers only read the arrays.
static var wall_sets = Cs.arr(8, null)
static var wall_rect_sets = Cs.arr(8, null)

# bit I set: door I is open
static func open_mask(open) -> int:
	var m: int = 0
	for i in range(3):
		if open[i]:
			m |= 1 << i
	return m

# doors open to bodies moving now: all by day, the broken ones at night
func door_mask() -> int:
	var day: bool = phase != "night"
	var m: int = 0
	for i in range(3):
		if day or float(doors[i]) <= 0:
			m |= 1 << i
	return m

static func wall_rects(mask: int) -> Variant:
	if wall_rect_sets[mask] == null:
		var set = walls([mask & 1 != 0, mask & 2 != 0, mask & 4 != 0])
		var rects = Cs.arr(set.size(), Rect2())
		for i in range(rects.size()):
			rects[i] = Rect2(set[i])
		wall_rect_sets[mask] = rects
	return wall_rect_sets[mask]

static func walls(open) -> Variant:
	var mask: int = open_mask(open)
	if wall_sets[mask] == null: wall_sets[mask] = build_walls(open)
	return wall_sets[mask]

static func build_walls(open) -> Variant:
	# Solid wall pieces; an open door (day, or broken) leaves a gap.
	var h = HALF
	var th = WALL_IN + WALL_OUT
	var top = Rect2(-h.x - WALL_OUT, -h.y - WALL_OUT, 2 * h.x + th, th)
	var bottom = Rect2(-h.x - WALL_OUT, h.y - WALL_IN, 2 * h.x + th, th)
	var left = Rect2(-h.x - WALL_OUT, -h.y - WALL_OUT, th, 2 * h.y + th)
	var right = Rect2(h.x - WALL_IN, -h.y - WALL_OUT, th, 2 * h.y + th)
	return [bottom] + split(top, open[1], false) + split(left, open[0], true) + split(right, open[2], true)

static func split(r, open, vertical) -> Variant:
	if not open:
		return [r]
	if vertical:
		return [Rect2(r.position.x, r.position.y, r.size.x, -DOOR_GAP - r.position.y), Rect2(r.position.x, DOOR_GAP, r.size.x, r.end.y - DOOR_GAP)]
	return [Rect2(r.position.x, r.position.y, -DOOR_GAP - r.position.x, r.size.y), Rect2(DOOR_GAP, r.position.y, r.end.x - DOOR_GAP, r.size.y)]

static func collide(pos, radius, open) -> Variant:
	var at = pos
	if pos is Vector2:
		return collide_fast(at, float(radius), open_mask(open))
	# Pushes a circle out of every wall piece (players only enter through doors).
	for w in walls(open):
		var q = pos.clamp(w.position, w.end)
		var d = pos - q
		var l = d.length()
		if l >= radius:
			continue
		if l > 0.001:
			pos = q + d / l * radius
		else:
			var c = w.get_center()
			var e = w.size / 2 + Vector2(radius, radius)
			var o = pos - c
			if e.x - absf(o.x) < e.y - absf(o.y):
				pos.x = c.x + (1.0 if o.x >= 0 else -1.0) * e.x
			else:
				pos.y = c.y + (1.0 if o.y >= 0 else -1.0) * e.y
	return pos

# Collide(), typed
static func collide_fast(pos: Vector2, radius: float, mask: int) -> Vector2:
	for w in wall_rects(mask):
		var q: Vector2 = pos.clamp(w.position, w.end)
		var d: Vector2 = pos - q
		var l: float = d.length()
		if l >= radius:
			continue
		if l > 0.001:
			pos = q + d / l * radius
		else:
			var c: Vector2 = w.get_center()
			var e: Vector2 = w.size / 2 + Vector2(radius, radius)
			var o: Vector2 = pos - c
			if e.x - absf(o.x) < e.y - absf(o.y):
				pos.x = c.x + (1.0 if o.x >= 0 else -1.0) * e.x
			else:
				pos.y = c.y + (1.0 if o.y >= 0 else -1.0) * e.y
	return pos

static func route(from, to, r) -> Variant:
	# Next waypoint around the base: the goal if the way is clear, else the best visible corner.
	# ponytail: corner hopping around one rectangle; NavigationAgent2D once the map has obstacles.
	if not blocked(from, to, r):
		return to
	var c = HALF + Vector2(r + T, r + T)
	var corners = [Vector2(c.x, c.y), Vector2(-c.x, c.y), Vector2(c.x, -c.y), Vector2(-c.x, -c.y)]
	var best = to
	var best_d = INF
	for corner in corners:
		if from.distance_to(corner) < 4.0 or blocked(from, corner, r):
			continue
		var d = from.distance_to(corner) + rest(corner, to, r, corners)
		if d < best_d:
			best_d = d
			best = corner
	return best

static func rest(corner, to, r, corners) -> Variant:
	# Distance left from a corner to the goal, with at most one more corner (enough for a rectangle).
	if not blocked(corner, to, r):
		return corner.distance_to(to)
	var d = INF
	for c2 in corners:
		if c2 != corner and not blocked(corner, c2, r) and not blocked(c2, to, r):
			d = minf(d, corner.distance_to(c2) + c2.distance_to(to))
	return d

static func blocked(a, b, r) -> Variant:
	var h = HALF + Vector2(r, r)
	var pts = [Vector2(-h.x, -h.y), Vector2(h.x, -h.y), Vector2(h.x, h.y), Vector2(-h.x, h.y)]
	for i in 4:
		if Geometry2D.segment_intersects_segment(a, b, pts[i], pts[(i + 1) % 4]) != null:
			return true
	return false

static func push_out(pos, margin) -> Variant:
	# Moves a point inside the (grown) base rectangle to its nearest edge.
	if not inside_base(pos, margin):
		return pos
	var dx = HALF.x + margin - absf(pos.x)
	var dy = HALF.y + margin - absf(pos.y)
	if dx < dy:
		pos.x = (HALF.x + margin) * (1.0 if pos.x >= 0 else -1.0)
	else:
		pos.y = (HALF.y + margin) * (1.0 if pos.y >= 0 else -1.0)
	return pos

static func pad_pos(i) -> Variant:
	return DOOR_POS[i] + DOOR_DIR[i] * PAD_DIST
# Primary wood, stone and herbs share the actual compact home-farm sites and their soil footprints.
# Titanium remains spread over five salvage landmarks, with additional fixed veins in Southwest Cave.
static var TITANIUM_SITES = [5, 6, 7, 13, 14]

func gen_nodes() -> void:
	nodes.clear()
	var geometry = world
	var primary_count: int = int((4 + 3 * n0) * NODE_MUL)
	geometry.ensure_home_farm_resources(primary_count)
	var ambient = []
	var home_count = Cs.arr(3, 0)
	for site in geometry.harvest_sites:
		var node = site.create_node()
		if site.type < 3 and WorldGeometry.in_home_farm(site.pos, site.type) and home_count[site.type] < primary_count:
			node["ambient"] = false
			home_count[site.type] += 1
			nodes.append(node)
		else:
			ambient.append(node)
	var occupied = []
	occupied.append_array(nodes)
	occupied.append_array(ambient)
	world.rebuild(occupied, towers)
	for i in range(primary_count):
		var site: int = TITANIUM_SITES[i % TITANIUM_SITES.size()]
		var centre: Vector2 = geometry.lm_at.get(site, IslandMap.by_id(site).pos)
		var spread: float = 6 * T
		var spot = null
		for attempt in range(300):
			var p: Vector2 = centre + Vector2.from_angle(rng.randf() * TAU) * spread * sqrt(rng.randf())
			if geometry.in_expedition_aisle(p):
				continue
			if geometry.in_compound_passage(p):
				continue
			if geometry.in_lookout_passage(p):
				continue
			if IslandMap.in_bridge_approach(p, T):
				continue
			if (site == 5 or site == 7) and WorldGeometry.soil_coverage(Cs.first_or_default(WorldGeometry.landmark_soils, func(s): return s.id == site), p - centre) < 0.95:
				continue
			# The cave supplies fixed ore veins and exterior scree, with clear walkways between them.
			if world.in_cave_region(p, T) or Cs.has_point(world.garden_bounds.grow(T), p) or Cs.has_point(world.weather_room.grow(T), p):
				continue
			var wd: float = float(wall_dist(p))
			if not IslandMap.on_grass(p, 1.5 * T) or wd < 3 * T or in_door_lane(p) or self.near_pad(p):
				continue
			if wd < float(SAFE_D) + 3 * T:
				continue
			if world.terrain_at(p, 0.5 * T) == 1 or world.terrain_at(p, 0.5 * T) == 5 or Vector2(world.resolve(p, T)).distance_to(p) > 0.01 or nodes.any(func(n): return n.pos.distance_to(p) <= 2.5 * T):
				continue
			spot = p
			break
		# a crowded site: anywhere in the right zone
		if spot == null: spot = self.rand_zone_point(SAFE_D + 3 * T, ISLAND)
		nodes.append({"type": 3, "pos": spot, "amount": 5})
	# The visible forest, rocks and herbs use the same gathering rules as the original resource clusters.
	nodes.append_array(ambient)
	world.rebuild(nodes, towers)

func obstacle_hit(a, b, radius = null, ignore = null) -> Variant:
	if radius == null: radius = 0.0
	if ignore == null: ignore = Vector2.INF
	var hit_: float = float(world.first_hit(a, b, radius, true, ignore))
	for w in self.movement_walls():
		hit_ = minf(hit_, WorldGeometry.segment_box(a, b, w.grow(radius)))
	return hit_

func line_clear(a, b, ignore = null) -> Variant:
	if ignore == null: ignore = Vector2.INF
	return self.obstacle_hit(a, b, 0.0, ignore) == INF

func move_actor(from, to, radius) -> Variant:
	# typed all the way (src/world/WorldGeometry.FastMove.cs): every body moves through here every tick
	var mask: int = self.door_mask()
	var f: Vector2 = Vector2(from)
	var r: float = float(radius)
	# the sea stops everyone: slide along the coast (src/world/IslandMap.cs)
	var moved: Vector2 = IslandMap.keep(f, world.move_body_fast(f, IslandMap.keep(f, Vector2(to), r), r, wall_rects(mask)), r)
	return collide_fast(moved, r, mask)
static var outside_navigation_walls = [Rect2(-Vector2(HALF), Vector2(HALF) * 2)]

func navigate(from, to, radius, outside = null) -> Variant:
	if outside == null: outside = true
	var barriers = outside_navigation_walls if outside else wall_rects(door_mask())
	return world.waypoint_fast(Vector2(from), Vector2(to), float(radius), barriers)

func movement_walls() -> Variant:
	var mask: int = self.door_mask()
	var _v1 = wall_sets[mask]
	return _v1 if _v1 != null else walls([mask & 1 != 0, mask & 2 != 0, mask & 4 != 0])

# ---- src/world/Harvestables.cs
var harvest_snapshot_source
var harvest_snapshot = []
var harvest_snapshot_state = []

# Most resources are unchanged between snapshots; reuse their immutable records instead of allocating
# hundreds of dictionaries/arrays every 50 ms now that the whole forest can be gathered.
func snapshot_nodes() -> Variant:
	var current = nodes
	if not is_same(current, harvest_snapshot_source) or current.size() != harvest_snapshot_state.size():
		harvest_snapshot_source = current
		harvest_snapshot = []
		harvest_snapshot_state.clear()
	var i: int = 0
	while i < current.size():
		var n = current[i]
		var state = [int(n.get("type")), Vector2(n.get("pos")), int(n.get("amount")), Cs.s(n.get("sprite", "")), Harvestables.radius(n)]
		if i < harvest_snapshot_state.size() and harvest_snapshot_state[i] == state:
			i += 1
			continue
		var record = [state[0], state[1], state[2], state[3], state[4]]
		if i < harvest_snapshot_state.size():
			harvest_snapshot_state[i] = state
			harvest_snapshot[i] = record
		else:
			harvest_snapshot_state.append(state)
			harvest_snapshot.append(record)
		i += 1
	return harvest_snapshot

class ExplorationChest:
	var id: int = 0
	var landmark: int = 0
	var component: int = -1
	var pos: Vector2 = Vector2.ZERO
	var opened: bool = false
	var opened_at: float = 0.0
	var unlocker
	var unlock_progress: float = 0.0
	var slots = GameSim.empty_slots(GameSim.EXPLORATION_CHEST_SLOTS)

	var empty: bool:
		get:
			return slots.all(func(s): return s == null)
