class_name PixelBosses
extends Node
# Pixel-art sprite sheets of the seven NOX bosses (AGT-05..11, table 4.2), in the same sheet layout as
# tools/PixelChars.cs so the game animates them the same way: rows = right, down, left (mirrored), up;
# columns = idle 0-1, walk 2-5, attack 6-9 (wind-up, slam, impact, recover), knocked down 10-13 (staggers,
# knees give, falling, lying with the eyes shut). 48 x 48 px, displayed at 3x in game. Each boss has its own
# palette and silhouette, with the parts that show its move: Juggernaut horns (charge), Broodmother egg
# sacs (summons), Bio-Cannon spore pods (volley), Burrower claws (burrow), Gatebreaker ram fist (door blow),
# Phantom hood and cloak (cloak), Ironhide front plates (front armour; its back is bare, where it is weak).
# Not part of the game. Run: godot --headless --path . tools/PixelBosses.tscn  (-- --out=<dir> to preview)
# Writes art/chars/boss_<name>.png and art/chars/bosses.json. The PNGs can be repainted by hand.
const S: int = 48
static var ink: Color = Color("141019")
static var shade: Color = Color(0, 0, 0, 0.32)
static var clear: Color = Color(0, 0, 0, 0)

# [light, mid, dark]
static func tones(l, m, d, a: float = 1.0) -> Variant:
	return [Color(Color(l), a), Color(Color(m), a), Color(Color(d), a)]
static var bosses = [Cs.init(Boss.new(), {"name": "juggernaut", "body": tones("E77949", "AA402D", "60251F"), "part": tones("FFF0CC", "D9BD8A", "98724F"), "eye": Color("FFF080"), "shoulder_r": 7, "head_r": 6.5}), Cs.init(Boss.new(), {"name": "broodmother", "body": tones("D67385", "973B55", "53273F"), "part": tones("FFF1CC", "EABD72", "A97044"), "eye": Color("FFEC9A"), "torso_rx": 14, "torso_ry": 12, "head_r": 4.5, "shoulder_r": 4}), Cs.init(Boss.new(), {"name": "bio_cannon", "body": tones("7C9A50", "425D34", "25382B"), "part": tones("F1FFAE", "BCDF48", "6D9429"), "eye": Color("F1FFAE"), "torso_rx": 9, "head_r": 5}), Cs.init(Boss.new(), {"name": "burrower", "body": tones("D4A553", "9C6F33", "624322"), "part": tones("FFF2D6", "DCCA9F", "A48A65"), "eye": Color("E94732"), "torso_rx": 12, "torso_ry": 9, "head_r": 5.5, "shoulder_r": 4}), Cs.init(Boss.new(), {"name": "gatebreaker", "body": tones("C3854A", "84552E", "503522"), "part": tones("B9DFD6", "6C9C9B", "3C626C"), "eye": Color("FFB85E"), "shoulder_r": 6}), Cs.init(Boss.new(), {"name": "phantom", "body": tones("D9FAFF", "83CFD9", "3E7D9F", 0.86), "part": tones("A8E3EE", "5293B2", "305477", 0.94), "eye": Color("E6FFFF"), "legs": false, "torso_rx": 8, "shoulder_r": 3, "head_r": 5}), Cs.init(Boss.new(), {"name": "ironhide", "body": tones("68829F", "3C536F", "25354C"), "part": tones("DFE8EB", "99B1C4", "526D8C"), "eye": Color("FFD16B"), "torso_rx": 12, "shoulder_r": 7})]
# columns: idle 2, walk 4, attack 4, knocked down 4
const COLS: int = 14
# the top of the legs (the knees give above it)
const LEGS: int = 36
# the body rises for the wind-up and sinks into the slam
static var ATTACK_BOB = [-1, 1, 2, 1]

func _ready() -> void:
	var out = "res://art/chars"
	for a in OS.get_cmdline_user_args():
		if a.begins_with("--out="):
			out = a.substr(6)
	DirAccess.make_dir_recursive_absolute(out)
	var info = {}
	var chars = {}
	# rows: right, down, left, up, then the 3/4 diagonals down-right, down-left, up-left, up-right
	var views = ["side", "down", "side", "up", "down", "down", "up", "up"]
	var mirrored = [2, 5, 6]
	var turn = [3, 7, 11]
	# idle: (bob, step) = (0,0), (1,0); walk: the body dips on each stride: (1,+1), (0,0), (1,-1), (0,0)
	var frames = [[0, 0], [1, 0], [1, 1], [0, 0], [1, -1], [0, 0]]
	for b in bosses:
		var sheet = Image.create_empty(S * COLS, S * 8, false, Image.FORMAT_RGBA8)
		var shut = b.eyes_shut()
		for row in range(8):
			var view = views[row]
			var cells = []
			# idle 0-1, walk 2-5
			for f in frames:
				cells.append(finish(_ready_turned(draw(b, view, f[0], f[1]), b.eye, row, turn)))
			# attack 6-9: wind-up (arms up), slam, impact (dust), recover
			for p in range(1, 5):
				var im = finish(_ready_turned(draw(b, view, ATTACK_BOB[p - 1], 0, p), b.eye, row, turn))
				if p >= 3:
					dust(im, view, p == 3)
				cells.append(im)
			# knocked down 10-13: staggers back, knees give, falling, lying still with the eyes shut
			var stand = _ready_turned(draw(b, view, 0, 0), b.eye, row, turn)
			var back_ = Vector2i(-1 if view == "side" else 0, 0)
			cells.append(finish(PixelPoses.kneel(PixelPoses.lean(stand, LEGS, back_), LEGS, 1)))
			cells.append(finish(PixelPoses.kneel(PixelPoses.lean(stand, LEGS, back_), LEGS, 4)))
			cells.append(finish(PixelPoses.lie(stand, S, 41), 16))
			cells.append(finish(PixelPoses.lie(_ready_turned(draw(shut, view, 0, 0), null, row, turn), S, 45), 20))
			for col in range(COLS):
				var im = cells[col]
				if mirrored.find(row) >= 0:
					im.flip_x()
				sheet.blit_rect(im, Rect2i(0, 0, S, S), Vector2i(col * S, row * S))
		var key = "boss_" + b.name
		sheet.save_png(out.path_join(key + ".png"))
		# one-shot animations, as in art/chars/chars.json: [first column, frames, frames per second]
		var anims = {"attack": [6, 4, 10.0], "down": [10, 4, 8.0]}
		chars[key] = {"size": S, "body": S, "anchor": [S / 2.0, 46], "anims": anims}
		print("drew ", key)
	info["chars"] = chars
	var fa = FileAccess.open(out.path_join("bosses.json"), FileAccess.WRITE)
	fa.store_string(JSON.stringify(info, "\t"))
	get_tree().quit()

# local function Turned of the method above
func _ready_turned(raw, eye, row, turn) -> Variant:
	return PixelTurn.turn_right(raw, S / 2, turn, 0, eye) if row >= 4 else raw

# --- one frame -----------------------------------------------------------------
# Pose: 0 standing / walking; attack 1 wind-up (arms up), 2 slam, 3 impact, 4 recover
static func draw(b, view, bob: int, step: int, pose: int = 0) -> Variant:
	var im = Image.create_empty(S, S, false, Image.FORMAT_RGBA8)
	var side: bool = view == "side"
	var front: bool = view == "down"
	var y: int = bob  # the upper body sinks a pixel on the idle breath
	# legs (or the Phantom's cloak hem)
	if b.legs:
		if side:
			leg(im, 19 - 2 * step, b.body, 0)
			leg(im, 25 + 2 * step, b.body, 0)
		else:
			leg(im, 16, b.body, 2 if step > 0 else 0)
			leg(im, 27, b.body, 2 if step < 0 else 0)
	# back arm on the side view
	if side:
		arm(im, 17, 22 + y, b, front, true, pose, view)
	# torso
	var tx: float = 22 if side else 24
	if b.legs:
		ell(im, tx, 28 + y, b.torso_rx - 2 if side else b.torso_rx, b.torso_ry, b.body)
	else:
		cloak(im, tx, y, step, b, side)
	# the parts that sit on the back are drawn under the head and arms
	back(im, b, view, y)
	# shoulders and arms
	if side:
		ell(im, 26, 21 + y, b.shoulder_r, b.shoulder_r - 1, b.body)
		arm(im, 27, 22 + y, b, front, false, pose, view)
	else:
		ell(im, 13, 21 + y, b.shoulder_r, b.shoulder_r - 1, b.body)
		ell(im, 35, 21 + y, b.shoulder_r, b.shoulder_r - 1, b.body)
		arm(im, 10, 22 + y, b, front, false, pose, view)
		arm(im, 38, 22 + y, b, front, true, pose, view)
	# head, sunk between the shoulders
	# the Phantom's hood sits on its cloak; the brutes lean their head forward
	var hx: float = (31 if b.legs else 26) if side else 24
	var hy: float = 14 + y
	ell(im, hx, hy, b.head_r, b.head_r - 1, b.body)
	if front:
		px(im, int(hx - 3), int(hy), b.eye, 2)
		px(im, int(hx + 2), int(hy), b.eye, 2)
		fill(im, int(hx - 2), int(hy + 3), 5, 1, Color("120A16"))
		px(im, int(hx - 1), int(hy + 3), Color("E8E3C8"))
		px(im, int(hx + 1), int(hy + 3), Color("E8E3C8"))
	elif side:
		px(im, int(hx + 2), int(hy), b.eye, 2)
		fill(im, int(hx + 1), int(hy + 3), 4, 1, Color("120A16"))
	front_(im, b, view, y, hx, hy)
	return im

static func leg(im, x: int, t, lift: int) -> void:
	var top: int = 36
	var bottom: int = 45 - lift
	fill(im, x, top, 5, bottom - top, t[1])
	fill(im, x, top, 1, bottom - top, t[0])
	fill(im, x + 4, top, 1, bottom - top, t[2])
	# foot
	fill(im, x - 1, bottom - 1, 7, 2, t[2])

static func arm(im, x: int, y: int, b, front: bool, right: bool, pose: int, view) -> void:
	var t = b.part if b.name == "phantom" else b.body
	var w: int = 3 if b.name == "phantom" else 4
	if pose == 0:
		fill(im, x, y, w, 13, t[1])
		fill(im, x, y, 1, 13, t[0])
		fill(im, x + w - 1, y, 1, 13, t[2])
		fist(im, x + w / 2.0, y + 14, b, right)
		return
	# attacking: the arm goes from the shoulder to where the fist is in this pose
	var sh = Vector2(x + w / 2.0, y)
	var in_: float = 1 if x < S / 2 else -1
	var to: Vector2 = Vector2.ZERO
	if view == "side":
		to = Vector2(-3, -13) if pose == 1 else Vector2(12, 6) if pose == 2 else Vector2(13, 18) if pose == 3 else Vector2(9, 13)
	elif view == "down":
		to = Vector2(-in_, -13) if pose == 1 else Vector2(in_ * 4, 12) if pose == 2 else Vector2(in_ * 5, 18) if pose == 3 else Vector2(in_ * 3, 15)
	else:
		# seen from the back, reaching forward is reaching away from the camera (up the screen)
		to = Vector2(-in_, -13) if pose == 1 else Vector2(in_ * 3, -6) if pose == 2 else Vector2(in_ * 3, -2) if pose == 3 else Vector2(in_ * 2, 4)
	var h = sh + to
	var pp = (h - sh).normalized().orthogonal()
	# the lit edge faces the upper left
	if pp.dot(Vector2(-0.6, -0.8)) < 0:
		pp = -pp
	var n: int = ceili(sh.distance_to(h) * 2.0)
	for i in range(n + 1):
		var p = sh.lerp(h, i / float(n))
		fill(im, int(p.x - w / 2.0), int(p.y - w / 2.0), w, w, t[1])
	for i in range(n + 1):
		var p = sh.lerp(h, i / float(n))
		px(im, int(p.x + pp.x * (w / 2.0 - 0.5)), int(p.y + pp.y * (w / 2.0 - 0.5)), t[0])
		px(im, int(p.x - pp.x * (w / 2.0 - 0.5)), int(p.y - pp.y * (w / 2.0 - 0.5)), t[2])
	fist(im, h.x, h.y, b, right)

# the end of an arm: fist, claws, ram or ghost fingers
static func fist(im, fx: float, fy: float, b, right: bool) -> void:
	var t = b.part if b.name == "phantom" else b.body
	var _sw1 = b.name
	# digging claws: three bone hooks
	if _sw1 == "burrower":
		ell(im, fx, fy, 3.5, 2.5, b.body)
		for k in range(-1, 2):
			fill(im, int(fx + k * 2), int(fy + 2), 1, 3, b.part[1])
			px(im, int(fx + k * 2), int(fy + 5), b.part[2])
	# the ram: a steel block on the right fist
	elif _sw1 == "gatebreaker" and right:
		fill(im, int(fx) - 5, int(fy) - 4, 11, 9, b.part[2])
		fill(im, int(fx) - 5, int(fy) - 4, 9, 6, b.part[1])
		fill(im, int(fx) - 5, int(fy) - 4, 10, 2, b.part[0])
		fill(im, int(fx - 4), int(fy - 1), 9, 1, b.part[2])
		px(im, int(fx - 2), int(fy - 3), Color("D9534A"), 2)
	# long ghostly fingers
	elif _sw1 == "phantom":
		ell(im, fx, fy, 2, 2, b.part)
		for k in range(-1, 2):
			fill(im, int(fx + k), int(fy + 1), 1, 3, b.part[1])
	else:
		ell(im, fx, fy, 3, 3, t)

# The Phantom has no legs: a hooded cloak that narrows to a ragged hem which sways when it moves.
static func cloak(im, x: float, y: int, step: int, b, side: bool) -> void:
	for row in range(18 + y, 44):
		var k: float = (row - (18 + y)) / 25.0
		var half: float = (8.0 if side else 11.0) - k * 3.0
		var sway: float = step * k * 2.0
		for col in range(int(x - half + sway), int(x + half + sway) + 1):
			var rag: bool = row > 39 and (col + row) % 3 == 0
			if rag:
				continue
			var u: float = (col - (x + sway)) / half
			var c: Color = b.body[0] if u < -0.5 else b.body[2] if u > 0.45 else b.body[1]
			px(im, col, row, c)

# Parts on the back (drawn before the arms and head).
static func back(im, b, view, y: int) -> void:
	silhouette(im, b, view, y)
	var _sw1 = b.name
	# egg sacs on the abdomen (more of them on the back)
	if _sw1 == "broodmother":
		if view == "up":
			for _it2 in [[17, 26], [24, 24], [31, 26], [20, 32], [28, 32]]:
				var x = _it2[0]
				var ey = _it2[1]
				ell(im, x, ey + y, 3, 3, b.part)
		elif view == "down":
			for _it3 in [[18, 32], [24, 34], [30, 32]]:
				var x = _it3[0]
				var ey = _it3[1]
				ell(im, x, ey + y, 3, 2.5, b.part)
		else:
			ell(im, 16, 30 + y, 4, 4, b.part)
			ell(im, 20, 34 + y, 3, 3, b.part)
	# spore pods on the shoulders and a row of tubes down the back
	elif _sw1 == "bio_cannon":
		if view == "side":
			ell(im, 18, 17 + y, 4, 4, b.part)
			ell(im, 15, 24 + y, 3, 3, b.part)
		else:
			ell(im, 13, 15 + y, 4, 4, b.part)
			ell(im, 35, 15 + y, 4, 4, b.part)
			if view == "up":
				for k in range(3):
					ell(im, 24, 22 + k * 5 + y, 2.5, 2, b.part)
	# bone plates on the shoulders and spine
	elif _sw1 == "juggernaut":
		if view != "side":
			ell(im, 13, 18 + y, 6, 3, b.part)
			ell(im, 35, 18 + y, 6, 3, b.part)
		if view == "up":
			for k in range(4):
				fill(im, 23, 21 + k * 4 + y, 3, 2, b.part[1])
		elif view == "side":
			ell(im, 22, 18 + y, 6, 3, b.part)

# Parts in front (drawn last).
static func front_(im, b, view, y: int, hx: float, hy: float) -> void:
	var _sw1 = b.name
	# horns for the charge
	if _sw1 == "juggernaut":
		if view == "side":
			fill(im, int(hx + 3), int(hy - 4), 6, 2, b.part[1])
			fill(im, int(hx + 7), int(hy - 8), 2, 5, b.part[0])
		else:
			for dir in [-1, 1]:
				fill(im, int(hx + dir * 5), int(hy - 3), 1, 2, b.part[1])
				fill(im, int(hx + dir * 6), int(hy - 8), 2, 6, b.part[0])
				px(im, int(hx + dir * 7), int(hy - 9), b.part[0])
	# the cannon maw in the chest
	elif _sw1 == "bio_cannon":
		if view == "down":
			ell(im, 24, 27 + y, 4, 4, tones("2E4A2C", "1A2A18", "0E160C"))
			ell(im, 24, 27 + y, 2, 2, b.part)
		elif view == "side":
			fill(im, 28, 25 + y, 7, 5, b.body[2])
			fill(im, 34, 26 + y, 2, 3, b.part[1])
	# a pink snout for digging
	elif _sw1 == "burrower":
		if view == "down":
			ell(im, hx, hy + 2, 3, 2, tones("F0B8A0", "D98F7A", "A86452"))
		elif view == "side":
			ell(im, hx + 5, hy + 1, 3, 2, tones("F0B8A0", "D98F7A", "A86452"))
	# a steel helm with a red crest (every view)
	elif _sw1 == "gatebreaker":
		for col in range(int(hx - 6), int(hx + 6) + 1):
			for row in range(int(hy - 6), int(hy - 1) + 1):
				var dx: float = (col - hx) / 6.5
				var dy: float = (row - hy) / 6.0
				if dx * dx + dy * dy <= 1.0:
					px(im, col, row, b.part[0] if dx < -0.4 else b.part[2] if dx > 0.4 else b.part[1])
		fill(im, int(hx - 1), int(hy - 8), 2, 3, Color("D9534A"))
	# a deep hood; only the eyes glow out of it
	elif _sw1 == "phantom":
		for col in range(int(hx - 7), int(hx + 7) + 1):
			for row in range(int(hy - 7), int(hy + 4) + 1):
				var dx: float = (col - hx) / 7.0
				var dy: float = (row - (hy - 1)) / 6.5
				var d: float = dx * dx + dy * dy
				if d <= 1.0 and (d > 0.42 or view == "up"):
					px(im, col, row, b.part[0] if dx < -0.3 else b.part[2] if dx > 0.4 else b.part[1])
				elif d <= 0.42:
					px(im, col, row, Color("1A1030"))
		if view == "down":
			px(im, int(hx - 2), int(hy), b.eye, 2)
			px(im, int(hx + 1), int(hy), b.eye, 2)
		elif view == "side":
			px(im, int(hx + 2), int(hy), b.eye, 2)
	# thick front plates and pauldrons; the back stays bare (its weak side)
	elif _sw1 == "ironhide":
		if view == "down":
			ell(im, 24, 28 + y, 9, 8, b.part)
			fill(im, 16, 28 + y, 17, 1, b.part[2])
			for x in [18, 24, 30]:
				px(im, x, 24 + y, Color("F2B544"))
				px(im, x, 32 + y, Color("F2B544"))
		elif view == "side":
			fill(im, 27, 19 + y, 5, 18, b.part[1])
			fill(im, 27, 19 + y, 1, 18, b.part[0])
			fill(im, 31, 19 + y, 1, 18, b.part[2])
		if view != "up":
			ell(im, 26 if view == "side" else 13, 19 + y, 6, 3.5, b.part)
			if view != "side":
				ell(im, 35, 19 + y, 6, 3.5, b.part)

# Large silhouette features remain readable in shadow and in all animation frames.
static func silhouette(im, b, view, y: int) -> void:
	var side: bool = view == "side"
	var _sw1 = b.name
	# Three pairs of jointed insect legs frame a swollen abdomen.
	if _sw1 == "broodmother":
		for k in range(3):
			for d in [-1, 1]:
				var x: int = 19 if side else 24
				var knee: int = x + d * (18 - k)
				stroke(im, Vector2(x + d * 9, 26 + k * 3 + y), Vector2(knee, 23 + k * 5 + y), b.body[0], 2)
				stroke(im, Vector2(knee, 23 + k * 5 + y), Vector2(x + d * 20, 32 + k * 5), b.body[2], 2)
	# Two tall, open spore chimneys instead of a round shoulder profile.
	elif _sw1 == "bio_cannon":
		for x in [13, 19] if side else [10, 33]:
			fill(im, x, 7 + y, 5, 14, b.body[2])
			fill(im, x, 7 + y, 2, 12, b.part[1])
			ell(im, x + 2, 7 + y, 4, 2, b.part)
			fill(im, x, 6 + y, 4, 2, Color("25382B"))
	# A low ridged carapace, with ivory digging blades along its back.
	elif _sw1 == "burrower":
		for k in range(4):
			var x: int = 12 + k * 3 if side else 15 + k * 6
			stroke(im, Vector2(x, 21 + y), Vector2(x - 3, 12 + y + k % 2), b.part[1], 2)
	# A squared, riveted siege harness across the shoulders.
	elif _sw1 == "gatebreaker":
		fill(im, 17 if side else 7, 18 + y, 14 if side else 34, 6, b.part[2])
		fill(im, 17 if side else 7, 18 + y, 14 if side else 34, 2, b.part[0])
		for x in range(18 if side else 8, 31 if side else 41, 5):
			px(im, x, 21 + y, b.eye)
	# A pointed hood and long drifting ribbons with no solid feet.
	elif _sw1 == "phantom":
		stroke(im, Vector2(24, 12 + y), Vector2(22, 2 + y), b.part[0], 3)
		for d in [-1, 1]:
			stroke(im, Vector2(24 + d * 7, 28 + y), Vector2(24 + d * 13, 38), b.body[1], 2)
			stroke(im, Vector2(24 + d * 13, 38), Vector2(24 + d * 9, 44), b.body[0], 1)
	# Tall angular steel fins distinguish its armour from the horned Juggernaut.
	elif _sw1 == "ironhide":
		for x in [16, 22] if side else [10, 35]:
			fill(im, x, 12 + y, 4, 10, b.part[2])
			fill(im, x, 10 + y, 2, 10, b.part[0])
			fill(im, x + 1, 8 + y, 1, 3, b.part[0])

static func stroke(im, a: Vector2, b: Vector2, c: Color, width: int) -> void:
	var steps: int = ceili(a.distance_to(b) * 2)
	for i in range(steps + 1):
		var p: Vector2 = a.lerp(b, i / float(maxi(1, steps)))
		fill(im, roundi(p.x), roundi(p.y), width, width, c)

# --- drawing helpers ----------------------------------------------------------
# A shaded ellipse: light on the upper left, dark on the lower right.
static func ell(im, cx: float, cy: float, rx: float, ry: float, t) -> void:
	for y in range(int(floorf(cy - ry)), int(ceilf(cy + ry)) + 1):
		for x in range(int(floorf(cx - rx)), int(ceilf(cx + rx)) + 1):
			var dx: float = (x + 0.5 - cx) / rx
			var dy: float = (y + 0.5 - cy) / ry
			if dx * dx + dy * dy > 1.0:
				continue
			var lit: float = -0.6 * dx - 0.8 * dy
			px(im, x, y, t[0] if lit > 0.45 else t[2] if lit < -0.35 else t[1])

static func fill(im, x: int, y: int, w: int, h: int, c: Color) -> void:
	for row in range(y, y + h):
		for col in range(x, x + w):
			px(im, col, row, c)

static func px(im, x: int, y: int, c: Color, w: int = 1) -> void:
	for k in range(w):
		if x + k >= 1 and y >= 1 and x + k < S - 1 and y < S - 1:
			im.set_pixel(x + k, y, c)

# Dark outline around the silhouette, then a soft ground shadow under the feet (as tools/PixelChars.cs).
# ground dust in front of a slam (none seen from the back: the body hides it)
static func dust(im, view, fresh: bool) -> void:
	if view == "up":
		return
	var c = Vector2(41, 44) if view == "side" else Vector2(24, 46)
	var rx: float = 9.0 if fresh else 13.0
	var col = Color(0.80, 0.74, 0.62, 0.85 if fresh else 0.45)
	for i in range(18):
		var a: float = i / 18.0 * TAU
		var x: int = int(c.x + cos(a) * rx)
		var y: int = int(c.y + sin(a) * rx * 0.3)
		if x >= 0 and y >= 0 and x < S and y < S and im.get_pixel(x, y).a < 1:
			im.set_pixel(x, y, col)
	if fresh:
		for d in [Vector2(1, 0.2), Vector2(-1, 0.25), Vector2(0.4, 0.4)]:
			for k in range(5):
				var x: int = int(c.x + d.x * k)
				var y: int = int(c.y + d.y * k)
				if x >= 0 and y >= 0 and x < S and y < S and im.get_pixel(x, y).a < 1:
					im.set_pixel(x, y, Color(0.08, 0.06, 0.1, 0.75))

static func finish(src, rx: float = 13.0) -> Variant:
	var im = src.duplicate()
	for y in range(S):
		for x in range(S):
			if src.get_pixel(x, y).a > 0:
				continue
			for d in [Vector2i(1, 0), Vector2i(-1, 0), Vector2i(0, 1), Vector2i(0, -1)]:
				var q = Vector2i(x, y) + d
				if q.x >= 0 and q.y >= 0 and q.x < S and q.y < S and src.get_pixel(q.x, q.y).a > 0:
					im.set_pixel(x, y, ink)
					break
	for y in range(43, S):
		for x in range(S):
			var dx: float = (x + 0.5 - S / 2.0) / rx
			var dy: float = (y + 0.5 - 46.0) / 2.4
			if dx * dx + dy * dy <= 1.0 and im.get_pixel(x, y).a == 0:
				im.set_pixel(x, y, shade)
	return im

class Boss:
	var name
	var body
	var part
	var eye: Color = Color(0, 0, 0, 0)
	var torso_rx: float = 11
	var torso_ry: float = 10
	var shoulder_r: float = 5
	var head_r: float = 6
	var legs: bool = true

	# the same boss with its eyes closed (lying knocked down)
	func eyes_shut() -> Variant:
		var c = Cs.clone(self)
		c.eye = body[2]
		return c
