extends Node2D
## NOX boss prototype arena: the seven specimens of the GDD boss table (tab:boss-specs, AGT-05..AGT-11)
## with the shared slam (BAL-38), door damage (BAL-37), archetype limits (BAL-43) and health (FML-02).
## Standalone: game.gd, main.gd and bots.gd are untouched; this only reads the character art.
## Run:   godot --path . res://prototypes/boss_arena.tscn   (or open the scene in the editor and press F6)
## Check: godot --headless --path . -s prototypes/boss_check.gd
## Keys:  1-7 boss, N night 2/4, -/= team size (boss HP), R restart, WASD move, mouse aim, LMB attack, E (hold) repair door

const T := 32.0
const ARENA := Vector2(36, 24) * T  # centred on (0, 0); the north wall holds the door
const WALL_Y := -ARENA.y / 2 + T
const DOOR := Vector2(0, WALL_Y)
const DOOR_HP := 500.0  # BAL-21
const P_HP := 110.0  # Engineer (BAL-06)
const P_SPEED := 5.0 * T  # BAL-07
const P_DMG := 26.0  # metal blade, 1.5 hits/s = 39 DPS (BAL-09)
const SIGHT := 8.0 * T  # BAL-35
const TURN := PI * 2.0 / 3.0  # boss turn rate (rad/s): slow enough that one player can get behind the Ironhide
const CHARS := "res://art/chars/"
const NEON_RED := Color(2.4, 0.4, 0.3)
const NEON_CYAN := Color(0.6, 1.8, 2.2)
const NEON_PINK := Color(2.2, 0.5, 1.9)
const GOLD := Color(2.2, 1.8, 0.3)

enum { JUGGERNAUT, BROOD, CANNON, BURROWER, GATEBREAKER, PHANTOM, IRONHIDE }
const BOSSES := [  # code, name, u_b, radius in tiles, tint, weak window (GDD tab:boss-specs)
	["NOX-01", "Juggernaut", 0.5, 0.9, Color(1.35, 0.7, 0.55), "Step off the charge line; hit it in the 2 s recovery after each slam"],
	["NOX-02", "Broodmother", 0.6, 1.0, Color(1.2, 1.25, 0.55), "Kill the Grunts it summons every 12 s (max 4); it is slow, focus it"],
	["NOX-03", "Bio-Cannon", 0.45, 0.85, Color(0.55, 1.4, 0.8), "Stand in a gap of the 8-shot ring; attack in the 2 s after a volley"],
	["NOX-04", "Burrower", 0.4, 0.8, Color(1.15, 0.9, 0.55), "Move off the dirt marker; hit it in the 1.5 s climb-out"],
	["NOX-05", "Gatebreaker", 0.55, 0.95, Color(0.7, 0.85, 1.45), "Repair the door (hold E) between its 120-damage blows every 8 s"],
	["NOX-06", "Phantom", 0.4, 0.8, Color(1.1, 0.65, 1.5), "Watch for the arc telegraph; it stays visible 3 s after an ambush"],
	["NOX-07", "Ironhide", 0.35, 0.95, Color(0.95, 0.95, 1.1), "Front hits deal 30%: hit its back, or anywhere in the 3 s stagger after a slam"],
]
const STATE_TEXT := {
	"aim": "CHARGE wind-up", "dash": "CHARGING", "slam": "SLAM wind-up", "recover": "RECOVERING - weak",
	"under": "UNDERGROUND - immune", "surface": "SURFACING - immune", "climb": "CLIMB-OUT - weak",
	"blow": "DOOR BLOW wind-up", "ambush": "AMBUSH", "stagger": "STAGGERED - full damage",
}

var night := 2
var team := 1
var kind := JUGGERNAUT
var p := {}
var boss = null
var grunts: Array = []
var proj: Array = []
var tele: Array = []
var door_hp := DOOR_HP
var door_pause := 0.0
var events: Array = []
var popups: Array = []
var clock := 0.0
# input: read from the keyboard in _process, or set directly by the check
var move := Vector2.ZERO
var aim := 0.0
var attack := false
var repair := false

var sheets := {}
var info := {}
var font: Font
var hud: Control
var cam: Camera2D


func _ready() -> void:
	texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	info = JSON.parse_string(FileAccess.get_file_as_string(CHARS + "chars.json"))
	for skin in ["engineer", "grunt", "wrecker"]:
		sheets[skin] = load(CHARS + skin + ".png")
	font = load("res://fonts/PixelifySans.ttf")
	cam = Camera2D.new()
	cam.zoom = Vector2(4, 4) / 3.0  # 2 screen px per art px at the 1440 x 810 window, ~22 x 13 tiles in view
	add_child(cam)
	var layer := CanvasLayer.new()
	add_child(layer)
	hud = Control.new()
	hud.set_anchors_preset(Control.PRESET_FULL_RECT)
	hud.mouse_filter = Control.MOUSE_FILTER_IGNORE
	hud.draw.connect(_draw_hud)
	layer.add_child(hud)
	setup(JUGGERNAUT)


func setup(k: int) -> void:
	kind = k
	p = {"pos": Vector2(0, -2 * T), "hp": P_HP, "aim": 0.0, "cd": 0.0, "invuln": 0.0, "swing": 0.0, "moving": false}
	door_hp = DOOR_HP
	door_pause = 0.0
	grunts.clear()
	proj.clear()
	tele.clear()
	popups.clear()
	events.clear()
	var s: Array = BOSSES[k]
	var n4 := night == 4
	var m := 1.21 if n4 else 1.0  # night-4 version: damage x1.21, cooldowns x0.9, telegraphs unchanged
	var hp: float = 3900.0 * (1 + 0.8 * (team - 1)) / 3.4 * s[2] / 0.5 * (1.5625 if n4 else 1.0)  # FML-02 (Medium)
	boss = {
		"kind": k, "pos": Vector2(0, ARENA.y / 2 - 3 * T), "face": Vector2.UP, "hp": hp, "maxhp": hp, "r": s[3] * T,
		"spd": 2.0 * pow(1.08, night - 1) * T * (0.75 if k == BROOD else 1.0),  # Grunt speed of the night (BAL-39, DEC-12)
		"door": 60.0 * m, "slam": 40.0 * m, "sig": 35.0 * m, "m": m, "cdm": 0.9 if n4 else 1.0,
		"slam_cd": 0.0, "sig_cd": 2.0, "amb_cd": 0.0, "door_cd": 0.0, "act": "", "act_t": 0.0,
		"cloak": 0.0, "reveal": 0.0, "flash": 0.0, "t": 0.0, "moving": false,
		"dash_to": Vector2.ZERO, "hit": false, "surf": Vector2.ZERO,
	}
	_event("%s %s - night %d, team of %d: %d HP" % [s[0], s[1], night, team, ceili(hp)])


func _process(dt: float) -> void:
	clock += dt
	move = Vector2(float(Input.is_key_pressed(KEY_D)) - float(Input.is_key_pressed(KEY_A)),
		float(Input.is_key_pressed(KEY_S)) - float(Input.is_key_pressed(KEY_W)))
	aim = (get_global_mouse_position() - p.pos).angle()
	attack = Input.is_mouse_button_pressed(MOUSE_BUTTON_LEFT)
	repair = Input.is_key_pressed(KEY_E)
	step(dt)
	cam.position = p.pos.round()
	queue_redraw()
	hud.queue_redraw()


func _unhandled_input(ev: InputEvent) -> void:
	if not (ev is InputEventKey and ev.pressed and not ev.echo):
		return
	var key: int = ev.keycode
	if key >= KEY_1 and key <= KEY_7:
		setup(key - KEY_1)
	elif key == KEY_N:
		night = 6 - night
		setup(kind)
	elif key == KEY_EQUAL:
		team = mini(5, team + 1)
		setup(kind)
	elif key == KEY_MINUS:
		team = maxi(1, team - 1)
		setup(kind)
	elif key == KEY_R:
		setup(kind)


# --- rules ---------------------------------------------------------------

func step(dt: float) -> void:
	door_pause -= dt
	_step_player(dt)
	if boss != null:
		_step_boss(boss, dt)
	_step_grunts(dt)
	_step_proj(dt)
	_step_tele(dt)
	for pp in popups:
		pp.t -= dt
		pp.pos.y -= 20.0 * dt
	popups = popups.filter(func(pp): return pp.t > 0)


func _foes() -> Array:
	## Everything the player can hit or bump into; a burrowed boss is neither.
	var out := grunts.duplicate()
	if boss != null and not boss.act in ["under", "surface"]:
		out.append(boss)
	return out


func _step_player(dt: float) -> void:
	p.invuln -= dt
	p.cd -= dt
	p.swing -= dt
	p.moving = false
	if p.hp <= 0:
		return
	if move != Vector2.ZERO:
		p.pos += move.normalized() * P_SPEED * dt
		p.moving = true
	p.aim = aim
	for e in _foes():  # bodies push the player aside
		var d: Vector2 = p.pos - e.pos
		var need: float = e.r + 0.3 * T
		if d.length() < need:
			p.pos = e.pos + (d.normalized() if d.length() > 0.01 else Vector2.DOWN) * need
	p.pos = _clamp(p.pos, 0.3 * T)
	if attack and p.cd <= 0:  # 90 degree melee arc of 1.5 tiles (BAL-08)
		p.cd = 1.0 / 1.5
		p.swing = 0.25
		var dir := Vector2.from_angle(p.aim)
		for e in _foes():
			var d: Vector2 = e.pos - p.pos
			if d.length() <= 1.5 * T + e.r and (d.length() < 0.5 * T or dir.dot(d.normalized()) >= cos(PI / 4)):
				_hit(e, P_DMG)
	if repair and door_hp < DOOR_HP and door_pause <= 0 and p.pos.distance_to(DOOR) <= 1.5 * T:
		door_hp = minf(DOOR_HP, door_hp + 15.0 * dt)  # BAL-23; a hit blocks repair for 2 s (FML-08)


func _hit(e, dmg: float) -> void:
	if e.has("kind"):
		dmg *= armour(e, p.pos)
		if e.cloak > 0:
			e.cloak = 0.0
			_event("Phantom revealed by a hit")
	e.hp -= dmg
	e.flash = 0.1
	_popup(e.pos - Vector2(0, e.r * 2.2), str(roundi(dmg)), NEON_CYAN if dmg >= P_DMG else Color(0.7, 0.7, 0.8))
	if e.hp > 0:
		return
	if e.has("kind"):
		_event("%s killed in %.1f s" % [BOSSES[e.kind][1], e.t])
		boss = null
		grunts.clear()  # summons vanish with the boss (BAL-43)
		tele.clear()
	else:
		grunts = grunts.filter(func(g): return g.hp > 0)


static func armour(b, from: Vector2) -> float:
	if b.act in ["under", "surface"]:
		return 0.0
	if b.kind == IRONHIDE and b.act != "stagger" and b.face.dot((from - b.pos).normalized()) >= cos(PI / 3):
		return 0.3  # front 120 degrees
	return 1.0


func _hurt(dmg: float) -> void:
	if p.hp <= 0 or p.invuln > 0:
		return
	p.hp = maxf(0.0, p.hp - dmg)
	p.invuln = 1.0  # BAL-13
	_popup(p.pos - Vector2(0, 34), "-%d" % roundi(dmg), NEON_RED)
	if p.hp <= 0:
		_event("You are DOWN - press R to retry")


func _door_hit(d: float) -> void:
	if door_hp <= 0:
		return
	door_hp = maxf(0.0, door_hp - d)
	door_pause = 2.0
	_popup(DOOR + Vector2(0, -12), "-%d" % roundi(d), GOLD)
	if door_hp <= 0:
		_event("Door broken - BREACH")


# --- boss: shared state machine + one signature move each -----------------

func _step_boss(b, dt: float) -> void:
	b.t += dt
	for key in ["slam_cd", "sig_cd", "amb_cd", "door_cd", "act_t", "cloak", "reveal", "flash"]:
		b[key] -= dt
	var old: Vector2 = b.pos
	if b.act != "":
		_act(b, dt)
	else:
		_think(b, dt)
	b.pos = _clamp(b.pos, b.r)
	b.moving = b.pos.distance_to(old) > 0.1


func _think(b, dt: float) -> void:
	var tgt = p if p.hp > 0 and p.pos.distance_to(b.pos) <= SIGHT else null
	if tgt != null:
		b.face = Vector2.from_angle(rotate_toward(b.face.angle(), (tgt.pos - b.pos).angle(), TURN * dt))
		if b.slam_cd <= 0 and tgt.pos.distance_to(b.pos) < 3.0 * T:  # BAL-38: every boss has the slam
			_slam(b)
			return
	if _signature(b, tgt):
		return
	var goal: Vector2 = DOOR + Vector2(0, b.r + 0.2 * T)
	var stop := 2.0
	if tgt != null:
		goal = tgt.pos
		stop = b.r + 0.4 * T
		if b.kind == CANNON:  # keeps about 5 tiles from its target
			goal = tgt.pos + (b.pos - tgt.pos).normalized() * 5.0 * T
			stop = 0.5 * T
	var d: float = b.pos.distance_to(goal)
	if d > stop:
		var dir: Vector2 = (goal - b.pos) / d
		b.pos += dir * minf(b.spd * dt, d - stop)
		if tgt == null:
			b.face = Vector2.from_angle(rotate_toward(b.face.angle(), dir.angle(), TURN * dt))
	elif tgt == null and b.door_cd <= 0 and door_hp > 0:
		_door_attack(b)


func _signature(b, tgt) -> bool:
	## Starts the archetype's move; true while it owns the boss this frame.
	var d: float = INF if tgt == null else b.pos.distance_to(tgt.pos)
	match b.kind:
		JUGGERNAUT:  # Charge: every 8 s at a target 3-10 tiles away, 1.0 s line telegraph 1.5 tiles wide
			if b.sig_cd > 0 or d < 3.0 * T or d > 10.0 * T:
				return false
			var dir: Vector2 = (tgt.pos - b.pos) / d
			b.face = dir
			b.dash_to = _clamp(b.pos + dir * minf(10.0 * T, d + 1.5 * T), b.r)
			b.hit = false
			_tele("line", b.pos, 0.75 * T, 0.0, {"to": b.dash_to})  # the dash itself deals the damage
			b.sig_cd = 8.0 * b.cdm
			_start(b, "aim", 1.0)
			_event("Juggernaut lines up a charge")
		BROOD:  # Summon: every 12 s, 1.0 s body glow
			if b.sig_cd > 0:
				return false
			b.sig_cd = 12.0 * b.cdm
			_start(b, "glow", 1.0)
		CANNON:  # Volley: every 5 s, 1.0 s body glow
			if b.sig_cd > 0 or tgt == null:
				return false
			b.sig_cd = 5.0 * b.cdm
			_start(b, "glow", 1.0)
		BURROWER:  # Burrow: every 10 s, up to 3 s underground (2 s travel + 1 s dirt telegraph)
			if b.sig_cd > 0 or tgt == null:
				return false
			b.sig_cd = 10.0 * b.cdm
			_start(b, "under", 2.0)
			_event("Burrower dug in")
		PHANTOM:
			if tgt != null and b.amb_cd <= 0 and d <= b.r + 2.0 * T:  # Ambush: 1.0 s arc telegraph
				b.amb_cd = 6.0 * b.cdm
				b.cloak = 0.0  # a telegraph always reveals a hidden boss (BAL-43)
				b.face = (tgt.pos - b.pos) / d
				_tele("arc", b.pos, b.r + 1.5 * T, b.sig, {"dir": b.face.angle(), "half": PI / 3})
				_start(b, "ambush", 1.0)
				return true
			if b.sig_cd <= 0 and b.cloak <= 0 and b.reveal <= 0:  # Cloak: every 12 s, up to 4 s, still hittable
				b.sig_cd = 12.0 * b.cdm
				b.cloak = 4.0
				_event("Phantom vanished")
			return false
		_:
			return false  # Gatebreaker's move is its door blow; Ironhide's armour is passive
	return true


func _act(b, dt: float) -> void:
	var done: bool = b.act_t <= 0
	match b.act:
		"aim":
			if done:
				_start(b, "dash", 10.0 / 9.0 + 0.1)
		"dash":  # 9 tiles/s along the telegraphed line; never follows a player who steps aside
			b.pos = b.pos.move_toward(b.dash_to, 9.0 * T * dt)
			if not b.hit and p.hp > 0 and p.pos.distance_to(b.pos) < b.r + 0.75 * T:
				b.hit = true
				var side := Vector2(-b.face.y, b.face.x)
				_hurt(b.sig)
				p.pos = _clamp(p.pos + side * (1.0 if side.dot(p.pos - b.pos) >= 0 else -1.0) * 1.5 * T, 0.3 * T)  # knockback
			if done or b.pos.distance_to(b.dash_to) < 1.0:
				_slam(b)  # ends with the standard slam where it stops
		"slam":
			if done:
				if b.kind == JUGGERNAUT:
					_start(b, "recover", 2.0)
				elif b.kind == IRONHIDE:
					_start(b, "stagger", 3.0)
					_event("Ironhide staggered - full damage for 3 s")
				else:
					b.act = ""
		"glow":
			if done:
				if b.kind == BROOD:
					_summon(b)
				else:
					_volley(b)
				b.act = ""
		"under":  # cannot be hit and does not hit doors; the mound creeps toward its target
			if p.hp > 0:
				b.pos = b.pos.move_toward(p.pos, b.spd * 2.0 * dt)
			if done:
				var from: Vector2 = p.pos if p.hp > 0 else b.pos
				var off: Vector2 = b.pos - from
				var dir: Vector2 = off.normalized() if off.length() > 1.0 else Vector2.DOWN
				b.surf = _clamp(from + dir * 1.5 * T, b.r)  # surfaces at least 1.5 tiles from its target
				if b.surf.distance_to(from) < 1.5 * T - 1.0:  # an arena edge got in the way: other side
					b.surf = _clamp(from - dir * 1.5 * T, b.r)
				_tele("circle", b.surf, 2.0 * T, b.sig)
				_start(b, "surface", 1.0)
		"surface":
			if done:
				b.pos = b.surf
				_start(b, "climb", 1.5)
				_event("Burrower surfaced - 1.5 s climb-out")
		"blow":
			if done:
				_door_hit(2.0 * b.door)  # 120 in one blow every 8 s = the same 15 per s (BAL-37)
				b.act = ""
		"ambush":
			if done:
				b.reveal = 3.0
				b.act = ""
		_:  # recover, climb, stagger: weak windows, it stands still
			if done:
				b.act = ""


func _start(b, act: String, seconds: float) -> void:
	b.act = act
	b.act_t = seconds


func _slam(b) -> void:  # BAL-38: 40 damage, radius 3 tiles, every 6 s, 1.0 s telegraph
	b.slam_cd = 6.0 * b.cdm
	_tele("circle", b.pos, 3.0 * T, b.slam)
	_start(b, "slam", 1.0)


func _door_attack(b) -> void:
	if b.kind == GATEBREAKER:  # the door damage as one heavy blow every 8 s, after a 1.0 s wind-up
		b.door_cd = 8.0
		_start(b, "blow", 1.0)
	elif b.cloak <= 0:  # the Phantom does not hit doors while cloaked
		b.door_cd = 4.0
		_door_hit(b.door)


func _summon(b) -> void:  # 2 Grunts of the night, at most 4 alive, no drops (BAL-43)
	var n := mini(2, 4 - grunts.size())
	var hp := 60.0 * pow(1.1, night - 1)
	for i in n:
		var at: Vector2 = b.pos + Vector2.from_angle(randf() * TAU) * (b.r + 0.7 * T)
		grunts.append({"pos": _clamp(at, 0.5 * T), "hp": hp, "maxhp": hp, "r": 0.5 * T, "cd": 1.0, "flash": 0.0, "face": Vector2.DOWN, "moving": false})
	_event("Broodmother summoned %d Grunts (%d alive)" % [n, grunts.size()])


func _volley(b) -> void:  # ring of 8 (45 degree gaps), 12 damage, 4 tiles/s, range 8 tiles
	for i in 8:
		var dir := Vector2.from_angle(b.face.angle() + i * TAU / 8)
		proj.append({"pos": b.pos, "vel": dir * 4.0 * T, "ttl": 2.0, "dmg": 12.0 * b.m})
	_event("Bio-Cannon volley")


func _step_grunts(dt: float) -> void:
	var spd := 2.0 * pow(1.08, night - 1) * T
	for g in grunts:
		g.cd -= dt
		g.flash -= dt
		var old: Vector2 = g.pos
		var sees: bool = p.hp > 0 and p.pos.distance_to(g.pos) <= SIGHT
		var goal: Vector2 = p.pos if sees else DOOR + Vector2(0, g.r + 0.2 * T)
		var reach: float = g.r + 0.4 * T if sees else 2.0
		var d: float = g.pos.distance_to(goal)
		if d > reach:
			g.pos += (goal - g.pos) / d * minf(spd * dt, d - reach)
		elif g.cd <= 0:
			g.cd = 2.0
			if sees:
				_hurt(10.0 * pow(1.1, night - 1))
			else:
				_door_hit(10.0 * pow(1.1, night - 1))
		g.pos = _clamp(g.pos, g.r)
		g.moving = g.pos.distance_to(old) > 0.1
		if g.moving:
			g.face = (g.pos - old).normalized()


func _step_proj(dt: float) -> void:
	for b in proj:
		b.pos += b.vel * dt
		b.ttl -= dt
		if p.hp > 0 and b.pos.distance_to(p.pos) < 0.5 * T:
			_hurt(b.dmg)
			b.ttl = 0.0
	proj = proj.filter(func(b): return b.ttl > 0)


func _tele(shape: String, pos: Vector2, r: float, dmg: float, extra := {}) -> void:
	var tl := {"shape": shape, "pos": pos, "r": r, "t": 1.0, "dmg": dmg}  # every damaging move: 1.0 s (BAL-43)
	tl.merge(extra)
	tele.append(tl)


func _step_tele(dt: float) -> void:
	for tl in tele:
		tl.t -= dt
		if tl.t <= 0 and tl.dmg > 0 and p.hp > 0 and in_tele(tl, p.pos, 0.3 * T):
			_hurt(tl.dmg)
	tele = tele.filter(func(tl): return tl.t > 0)


static func in_tele(tl, pos: Vector2, pad: float) -> bool:
	match tl.shape:
		"line":
			return Geometry2D.get_closest_point_to_segment(pos, tl.pos, tl.to).distance_to(pos) <= tl.r + pad
		"arc":
			var d: Vector2 = pos - tl.pos
			return d.length() <= tl.r + pad and absf(angle_difference(tl.dir, d.angle())) <= tl.half
	return pos.distance_to(tl.pos) <= tl.r + pad


func _clamp(pos: Vector2, r: float) -> Vector2:
	return pos.clamp(Vector2(-ARENA.x / 2 + r, WALL_Y + r), ARENA / 2 - Vector2(r, r))


func _event(s: String) -> void:
	events.append(s)
	if events.size() > 6:
		events.pop_front()


func _popup(pos: Vector2, text: String, col: Color) -> void:
	popups.append({"pos": pos, "text": text, "col": col, "t": 0.8})


# --- drawing -------------------------------------------------------------

func _draw() -> void:
	var h := ARENA / 2
	draw_rect(Rect2(-h, ARENA), Color("2E4A35"))
	for x in range(int(-h.x), int(h.x) + 1, int(T)):
		draw_line(Vector2(x, -h.y), Vector2(x, h.y), Color(0, 0, 0, 0.12))
	for y in range(int(-h.y), int(h.y) + 1, int(T)):
		draw_line(Vector2(-h.x, y), Vector2(h.x, y), Color(0, 0, 0, 0.12))
	draw_rect(Rect2(-h.x, -h.y, ARENA.x, T), Color("5B5F6B"))  # north wall
	var door_col := Color("8FA3B8") if door_hp > 250 else (Color("F2B544") if door_hp > 0 else Color("2A2A30"))
	draw_rect(Rect2(DOOR.x - 1.2 * T, -h.y, 2.4 * T, T), door_col)
	draw_string(font, DOOR + Vector2(-60, -10), "DOOR %d" % roundi(door_hp), HORIZONTAL_ALIGNMENT_CENTER, 120, 10)
	for tl in tele:
		_draw_tele(tl)
	var items: Array = grunts.duplicate()
	if boss != null:
		items.append(boss)
	items.append(p)
	items.sort_custom(func(a, b): return a.pos.y < b.pos.y)
	for it in items:
		if it.has("kind"):
			_draw_boss(it)
		elif it.has("aim"):
			_draw_player()
		else:
			draw_circle(it.pos, it.r * 0.8, Color(0, 0, 0, 0.3))
			_char("grunt", it.pos, it.face.angle(), it.moving, 1.0, Color(2, 2, 2) if it.flash > 0 else Color(1.15, 0.95, 0.75))
			_bar(it.pos - Vector2(0, 28), 24, it.hp / it.maxhp, NEON_RED)
	for b in proj:
		draw_circle(b.pos, 5, Color(0.5, 2.0, 0.7, 0.6))
		draw_circle(b.pos, 2.5, Color.WHITE)
	for pp in popups:
		draw_string(font, pp.pos - Vector2(30, 0), pp.text, HORIZONTAL_ALIGNMENT_CENTER, 60, 12, pp.col)


func _draw_tele(tl) -> void:
	## Red ground marker that fills over its 1.0 s.
	var fill: float = clampf(1.0 - tl.t, 0.0, 1.0)
	var faint := Color(1.0, 0.1, 0.3, 0.16)
	var strong := Color(1.0, 0.15, 0.3, 0.35)
	match tl.shape:
		"line":
			var dir: Vector2 = (tl.to - tl.pos).normalized()
			var side: Vector2 = Vector2(-dir.y, dir.x) * tl.r
			var mid: Vector2 = tl.pos.lerp(tl.to, fill)
			var box := PackedVector2Array([tl.pos + side, tl.to + side, tl.to - side, tl.pos - side])
			draw_colored_polygon(box, faint)
			if fill > 0.02:
				draw_colored_polygon(PackedVector2Array([tl.pos + side, mid + side, mid - side, tl.pos - side]), strong)
			box.append(box[0])
			draw_polyline(box, NEON_RED, 2)
		"arc":
			draw_colored_polygon(_fan(tl, tl.r), faint)
			if fill > 0.02:
				draw_colored_polygon(_fan(tl, tl.r * fill), strong)
			var edge := _fan(tl, tl.r)
			edge.append(tl.pos)
			draw_polyline(edge, NEON_RED, 2)
		_:
			draw_circle(tl.pos, tl.r, faint)
			draw_circle(tl.pos, tl.r * fill, strong)
			draw_arc(tl.pos, tl.r, 0, TAU, 48, NEON_RED, 2)


func _fan(tl, r: float) -> PackedVector2Array:
	var pts := PackedVector2Array([tl.pos])
	for i in 17:
		pts.append(tl.pos + Vector2.from_angle(tl.dir - tl.half + 2.0 * tl.half * i / 16.0) * r)
	return pts


func _draw_boss(b) -> void:
	var s: Array = BOSSES[b.kind]
	var foot: Vector2 = b.pos
	if b.act in ["under", "surface"]:  # a dirt mound moves underground
		draw_circle(foot, b.r * 0.8, Color("6B4A2B"))
		draw_circle(foot + Vector2(-5, -4), b.r * 0.45, Color("8A6238"))
		return
	draw_circle(foot, b.r * 0.9, Color(0, 0, 0, 0.3))
	var weak: bool = b.act in ["recover", "climb", "stagger"]
	if weak:
		draw_arc(foot, b.r + 4, 0, TAU, 32, GOLD, 2)
	if b.kind == IRONHIDE and b.act != "stagger":  # armoured front 120 degrees
		draw_arc(foot, b.r + 6, b.face.angle() - PI / 3, b.face.angle() + PI / 3, 16, NEON_CYAN, 3)
	var mod: Color = s[4]
	if b.act in ["aim", "glow", "blow", "ambush", "slam"]:  # wind-up glow
		mod = mod.lerp(Color(2.4, 0.7, 0.6), 0.5 + 0.5 * sin(clock * 20.0))
	if b.flash > 0:
		mod = Color(2, 2, 2)
	if b.cloak > 0:
		mod.a = 0.12  # ponytail: faint shimmer so testers can follow it; the GDD wants it fully invisible
	_char("wrecker", foot, b.face.angle(), b.moving, b.r / (0.625 * T), mod)  # bosses are big tinted Wreckers until ART-03
	if weak:
		draw_string(font, foot - Vector2(40, b.r * 2.8), "WEAK", HORIZONTAL_ALIGNMENT_CENTER, 80, 10, GOLD)


func _draw_player() -> void:
	var foot: Vector2 = p.pos
	draw_circle(foot, 9, Color(0, 0, 0, 0.3))
	var mod := Color.WHITE
	if p.hp <= 0:
		mod = Color(0.4, 0.4, 0.4)
	elif p.invuln > 0 and fmod(clock, 0.2) < 0.1:
		mod.a = 0.4
	_char("engineer", foot, p.aim, p.moving, 1.0, mod)
	if p.swing > 0:
		draw_arc(foot - Vector2(0, 12), 1.35 * T, p.aim - PI / 4, p.aim + PI / 4, 16, NEON_CYAN, 4)
	_bar(foot - Vector2(0, 30), 28, p.hp / P_HP, Color("3DBE5A"))


func _char(skin: String, foot: Vector2, ang: float, moving: bool, s: float, mod: Color) -> void:
	var c: Dictionary = info.chars[skin]
	var anim := "run" if moving else "idle"
	var fr: Array = info.frames[anim]
	var f: int = int(fr[0]) + int(clock * info.fps[anim]) % int(fr[1])
	var dir := posmod(roundi(ang / (PI / 2.0)), 4)  # rows: right, down, left, up
	var size := Vector2(c.size, c.size)
	var anchor := Vector2(c.anchor[0], c.anchor[1])
	draw_texture_rect_region(sheets[skin], Rect2(foot - anchor * s, size * s), Rect2(Vector2(f, dir) * size, size), mod)


func _bar(center: Vector2, w: float, frac: float, col: Color) -> void:
	draw_rect(Rect2(center - Vector2(w / 2 + 1, 1), Vector2(w + 2, 4)), Color(0, 0, 0, 0.7))
	draw_rect(Rect2(center - Vector2(w / 2, 0), Vector2(w * clampf(frac, 0, 1), 2)), col)


func _draw_hud() -> void:
	var vs := hud.size
	var s: Array = BOSSES[kind]
	var br := Rect2(vs.x / 2 - 230, 34, 460, 10)
	hud.draw_string(font, Vector2(br.position.x, 26), "%s  %s   night %d   team %d" % [s[0], s[1].to_upper(), night, team],
		HORIZONTAL_ALIGNMENT_CENTER, br.size.x, 16, NEON_PINK)
	hud.draw_rect(br.grow(2), Color(0.05, 0.05, 0.08))
	if boss != null:
		hud.draw_rect(Rect2(br.position, Vector2(br.size.x * boss.hp / boss.maxhp, br.size.y)), NEON_PINK)
		var state: String = STATE_TEXT.get(boss.act, "CLOAKED" if boss.cloak > 0 else "")
		hud.draw_string(font, Vector2(br.position.x, 62), "%d / %d HP    %.1f s    %s" % [ceili(boss.hp), ceili(boss.maxhp), boss.t, state],
			HORIZONTAL_ALIGNMENT_CENTER, br.size.x, 12, GOLD if "weak" in state or "full" in state else Color.WHITE)
	else:
		hud.draw_string(font, Vector2(br.position.x, 62), "DEFEATED - R to fight again", HORIZONTAL_ALIGNMENT_CENTER, br.size.x, 12, GOLD)
	hud.draw_string(font, Vector2(br.position.x - 120, 82), s[5], HORIZONTAL_ALIGNMENT_CENTER, br.size.x + 240, 12, Color(0.8, 0.9, 1.0))
	for i in BOSSES.size():
		hud.draw_string(font, Vector2(12, 110 + i * 18), "%d  %s" % [i + 1, BOSSES[i][1]], HORIZONTAL_ALIGNMENT_LEFT, -1, 12,
			NEON_PINK if i == kind else Color(0.75, 0.75, 0.8))
	for i in events.size():
		hud.draw_string(font, Vector2(vs.x - 340, 110 + i * 16), events[i], HORIZONTAL_ALIGNMENT_LEFT, 330, 11, Color(0.85, 0.9, 1.0))
	hud.draw_string(font, Vector2(12, vs.y - 34), "HP %d / %d     Door %d / %d" % [ceili(p.hp), P_HP, roundi(door_hp), DOOR_HP],
		HORIZONTAL_ALIGNMENT_LEFT, -1, 14, Color.WHITE)
	hud.draw_string(font, Vector2(12, vs.y - 12), "1-7 boss   N night 2/4   -/= team size   R restart   WASD move   LMB attack   E repair door",
		HORIZONTAL_ALIGNMENT_LEFT, -1, 11, Color(0.7, 0.7, 0.75))
