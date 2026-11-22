class_name PixelPoses
extends RefCounted

# ---- tools/PixelPoses.cs
# Fight and knock-down frames for the character sheets of tools/PixelChars.cs (players and enemies).
# Not part of the game; used only by the sprite generator.
#
# A pose starts from the character's standing pixel map. The arm that acts is taken off the body and
# drawn again from its shoulder to where the hand is in this frame, together with the weapon it holds.
# Hands and weapons are placed in 3D around the body (f = forward, s = toward the weapon side,
# u = up) and projected to the view of the row (side, front, back, 3/4), so one table of poses gives
# all 8 directions. A weapon in front of the body hides it; seen from the back the body hides the
# weapon. Arm and weapon get their own dark outline, as in hand-drawn pixel art, and smears, dust
# and sparks are added on top without an outline.
# extra pixels around the body on every side of a sheet cell, so a weapon at full reach still fits
const M: int = 8
# ground depth is drawn shorter than height (top-down 3/4 camera)
const K: float = 0.6
static var ink: Color = Color("141019")
static var shade: Color = Color(0, 0, 0, 0.32)
static var clear: Color = Color(0, 0, 0, 0)
static var wood: Color = Color("7A5634")
static var wood_dark: Color = Color("4E3520")
static var wood_lit: Color = Color("A07850")
static var stone: Color = Color("8A8F98")
static var stone_lit: Color = Color("C9CED6")
static var steel: Color = Color("E4E8EC")
static var steel_dark: Color = Color("8A8F98")
static var iron: Color = Color("5B5F6B")
static var iron_lit: Color = Color("A3A9B3")
static var gold: Color = Color("F2B544")
static var bow_string: Color = Color("E8E3C8")
static var smear: Color = Color(0.91, 0.96, 1.0, 0.6)
static var dust: Color = Color(0.80, 0.74, 0.62, 0.85)
static var spark: Color = Color("FFF2A8")
static var spit: Color = Color("D8E03A")
static var spit_lit: Color = Color("F6F9A8")
static var spit_dark: Color = Color("8F9620")
static var crack: Color = Color(0.08, 0.06, 0.1, 0.75)

# The direction a view faces on screen (rows facing left are these, mirrored).
static func facing(view) -> float:
	return deg_to_rad(90.0 if view == "down" else -90.0 if view == "up" else 45.0 if view == "down3q" else -45.0 if view == "up3q" else 0.0)

# 3D (forward, weapon side, up) to screen pixels for a body facing angle A.
static func proj(a: float, v: Vector3) -> Vector2:
	var f = Vector2(cos(a), sin(a))
	var sd = Vector2(-f.y, f.x)
	var g = f * v.x + sd * v.y
	return Vector2(g.x, g.y * K - v.z)

# a unit direction from pitch (up from level) and yaw (toward the weapon side), in degrees
static func dir(pitch: float, yaw: float) -> Vector3:
	var p: float = deg_to_rad(pitch)
	var y: float = deg_to_rad(yaw)
	return Vector3(cos(p) * cos(y), cos(p) * sin(y), sin(p))

# --- pixel helpers ------------------------------------------------------------------------------
static func set_(im, p: Vector2, c: Color) -> void:
	var x: int = floori(p.x)
	var y: int = floori(p.y)
	if x >= 0 and y >= 0 and x < im.get_width() and y < im.get_height():
		im.set_pixel(x, y, c)

static func dot(im, p: Vector2, w: int, c: Color) -> void:
	var o: float = (w - 1) / 2.0
	for dy in range(w):
		for dx in range(w):
			set_(im, p + Vector2(dx - o, dy - o), c)

static func seg(im, a: Vector2, b: Vector2, w: int, c: Color) -> void:
	var n: int = maxi(1, ceili(a.distance_to(b) * 2.0))
	for i in range(n + 1):
		dot(im, a.lerp(b, i / float(n)), w, c)

static func across(wd: Vector2) -> Vector2:
	return Vector2.RIGHT if wd.length() < 0.25 else wd.normalized().orthogonal()

# a block across a weapon (axe or hammer head, sword guard, blade): from T0 to T1 along it, Half wide
static func block(im, grip: Vector2, wd: Vector2, t0: float, t1: float, half: float, c: Color, lit: Color) -> void:
	var pp = across(wd)
	var t: float = t0
	while t <= t1:
		var u: float = -half
		while u <= half:
			set_(im, grip + wd * t + pp * u, lit if u < -half + 0.7 else c)
			u += 0.4
		t += 0.4

static func blank(w: int, h: int) -> Variant:
	return Image.create_empty(w, h, false, Image.FORMAT_RGBA8)

# the layer on top, with a dark line around it drawn over whatever is under that line
static func over(dst, layer, outline: bool) -> void:
	var w: int = dst.get_width()
	var h: int = dst.get_height()
	if outline:
		for y in range(h):
			for x in range(w):
				if layer.get_pixel(x, y).a > 0:
					continue
				if x > 0 and layer.get_pixel(x - 1, y).a >= 1 or x < w - 1 and layer.get_pixel(x + 1, y).a >= 1 or y > 0 and layer.get_pixel(x, y - 1).a >= 1 or y < h - 1 and layer.get_pixel(x, y + 1).a >= 1:
					dst.set_pixel(x, y, ink)
	for y in range(h):
		for x in range(w):
			var c: Color = layer.get_pixel(x, y)
			if c.a > 0:
				dst.set_pixel(x, y, c if c.a >= 1 else dst.get_pixel(x, y).blend(c))

# the layer only where nothing solid is drawn yet
static func under(dst, layer) -> void:
	for y in range(dst.get_height()):
		for x in range(dst.get_width()):
			var c: Color = layer.get_pixel(x, y)
			var d: Color = dst.get_pixel(x, y)
			if c.a > 0 and d.a < 1:
				dst.set_pixel(x, y, c.blend(d))

# --- whole-body moves ---------------------------------------------------------------------------
# the body in the middle of a bigger cell
static func embed(body, cell: int) -> Variant:
	var im = blank(cell, cell)
	var o: int = (cell - body.get_width()) / 2
	im.blend_rect(body, Rect2i(0, 0, body.get_width(), body.get_height()), Vector2i(o, o))
	return im

# everything above the legs moves by D (the legs stay planted); a move up leaves no gap, so it is dropped
static func lean(body, legs: int, d: Vector2i) -> Variant:
	d.y = maxi(0, d.y)
	var w: int = body.get_width()
	var h: int = body.get_height()
	var im = blank(w, h)
	im.blend_rect(body, Rect2i(0, legs, w, h - legs), Vector2i(0, legs))
	im.blend_rect(body, Rect2i(0, 0, w, legs), d)
	return im

# the knees give: the upper body sinks Drop pixels and the legs get as much shorter
static func kneel(body, legs: int, drop: int) -> Variant:
	var w: int = body.get_width()
	var h: int = body.get_height()
	var im = blank(w, h)
	im.blend_rect(body, Rect2i(0, legs + drop, w, h - (legs + drop)), Vector2i(0, legs + drop))
	im.blend_rect(body, Rect2i(0, 0, w, legs), Vector2i(0, drop))
	return im

# Lying on its back: the standing body turned a quarter (head toward the left: it fell away from the
# way it faced), centred on the cell, its lowest pixel Bottom.
static func lie(body, cell: int, bottom: int) -> Variant:
	var w: int = body.get_width()
	var h: int = body.get_height()
	var turned = blank(h, w)
	var x0: int = 2147483647
	var x1: int = -1
	var y1: int = -1
	for y in range(h):
		for x in range(w):
			var c: Color = body.get_pixel(x, y)
			if c.a <= 0:
				continue
			# counter-clockwise: (x, y) -> (y, w-1-x)
			var nx: int = y
			var ny: int = w - 1 - x
			turned.set_pixel(nx, ny, c)
			x0 = mini(x0, nx)
			x1 = maxi(x1, nx)
			y1 = maxi(y1, ny)
	var im = blank(cell, cell)
	if x1 < 0:
		return im
	var ox: int = cell / 2 - (x0 + x1 + 1) / 2
	var oy: int = bottom - y1
	im.blend_rect(turned, Rect2i(0, 0, h, w), Vector2i(ox, oy))
	return im

# Dark outline around the silhouette, then a soft ground shadow (an ellipse Rx wide) under the feet.
static func finish(src, feet_y: float, rx: float) -> Variant:
	var w: int = src.get_width()
	var h: int = src.get_height()
	var im = src.duplicate()
	for y in range(h):
		for x in range(w):
			if src.get_pixel(x, y).a > 0:
				continue
			if x > 0 and src.get_pixel(x - 1, y).a > 0 or x < w - 1 and src.get_pixel(x + 1, y).a > 0 or y > 0 and src.get_pixel(x, y - 1).a > 0 or y < h - 1 and src.get_pixel(x, y + 1).a > 0:
				im.set_pixel(x, y, ink)
	for y in range(maxi(0, int(feet_y) - 3), mini(h, int(feet_y) + 3)):
		for x in range(w):
			var dx: float = (x + 0.5 - w / 2.0) / rx
			var dy: float = (y + 0.5 - feet_y) / 2.2
			if dx * dx + dy * dy <= 1.0 and im.get_pixel(x, y).a == 0:
				im.set_pixel(x, y, shade)
	return im

# --- knocked down ---------------------------------------------------------------------------------
# 4 frames: hit (staggers back), knees give, falling, lying still with the eyes shut (Shut: the same
# body with closed eyes). Returns the frames and the width of the shadow under each.
static func fall(body, shut, view, legs: int, feet: int, cell: int, rx: float) -> Variant:
	var a: float = facing(view)
	var back: int = -signf(Cs.round_even(cos(a) * 10.0))
	var ground: int = feet + M
	# lying, the shadow is as long as the body is tall
	var top: int = body.get_used_rect().position.y
	var long: float = (feet - top) / 2.0 + 1.0
	return [[embed(kneel(lean(body, legs, Vector2i(back, 0)), legs, 1), cell), rx], [embed(kneel(lean(body, legs, Vector2i(back, 0)), legs, 3), cell), rx], [lie(body, cell, ground - 4), rx + 2], [lie(shut, cell, ground), long]]
# How many frames each attack has, and how fast it plays (frames per second).
static var PLAYER_ATTACKS = ["fists", "spear", "blade", "hammer", "bow"]
static var PLAYER_ATTACK_FPS = [18.0, 16.0, 16.0, 12.0, 12.0]
const ATTACK_FRAMES: int = 4

# An attack (Kind: fists, spear, blade, hammer, bow; claw, spit, smash for the NOX), 4 finished cells.
static func attack(kind, body, view, r, legs: int, feet: int, cell: int, shadow_rx: float) -> Variant:
	var a: float = facing(view)
	var fwd = Vector2(cos(a), sin(a) * K)
	var behind: bool = view.begins_with("up")
	var out = Cs.arr(ATTACK_FRAMES, null)
	for i in range(ATTACK_FRAMES):
		var lean_by: int = [0, 1, 1, 0][i] if kind == "fists" else [-1, 0, 2, 1][i] if kind == "hammer" else [-1, 1, 2, 1][i] if kind == "smash" else 0 if kind == "bow" else [-1, 1, 1, 0][i]
		var uses_main: bool = kind != "spit"
		var uses_off: bool = kind == "spear" or kind == "hammer" or kind == "bow" or kind == "smash"
		# take the acting arms off the resting body (seen from the side, the resting hand is painted over)
		var b = body.duplicate()
		if uses_main:
			b.fill_rect(r.erase_main, clear)
			if r.patch.size != Vector2i.ZERO:
				b.fill_rect(r.patch, r.patch_col)
		if uses_off:
			b.fill_rect(r.erase_off, clear)
		var d = Vector2i(roundi(fwd.x * lean_by), roundi(fwd.y * lean_by))
		var im = embed(lean(b, legs, d), cell)
		var o: int = (cell - body.get_width()) / 2
		# the shoulders move with the upper body (Lean never moves it up)
		var shift = Vector2(o + d.x, o + maxi(0, d.y))
		var c = Cs.init(Ctx.new(), {"a": a, "r": r, "sm": r.main + shift, "so": r.off + shift, "l": blank(cell, cell), "fx": blank(cell, cell)})
		if kind == "fists":
			fists(c, i)
		elif kind == "spear":
			spear(c, i)
		elif kind == "blade":
			blade(c, i)
		elif kind == "hammer":
			hammer(c, i)
		elif kind == "bow":
			bow(c, i)
		elif kind == "claw":
			claw(c, i)
		elif kind == "spit":
			spit_at(c, i, r.mouth + shift, behind)
		elif kind == "smash":
			smash(c, i)
		var done
		if behind:
			# seen from the back: the body covers the arm and weapon
			done = blank(cell, cell)
			over(done, c.l, true)
			over(done, im, false)
		else:
			done = im
			over(done, c.l, true)
		done = finish(done, feet + o + 1, shadow_rx)
		if behind:
			# smears and dust are behind the body too
			under(done, c.fx)
		else:
			over(done, c.fx, false)
		out[i] = done
	return out

# jab: guard, the fist shoots out, impact, back to guard
static func fists(c, i: int) -> void:
	var hands = [Vector3(2, -1.5, -1.5), Vector3(7, -2, -0.5), Vector3(9, -2.5, -0.5), Vector3(4, -1.5, -1.5)]
	var h = c.hand(hands[i])
	c.arm_to(h, false, 3)
	if i == 2:
		c.star(h + c.w(dir(0, 0)) * 3.0, 3.0)
	if i == 1:
		seg(c.fx, h - c.w(dir(0, 0)) * 5.0, h - c.w(dir(0, 0)) * 2.5, 1, smear)

# stone spear, two hands: drawn back, thrust, full reach, recover
static func spear(c, i: int) -> void:
	var hands = [Vector3(-2.5, -1, -1.5), Vector3(3.5, -1, -1.5), Vector3(6, -1, -1.5), Vector3(1.5, -1, -2)]
	var dirs = [dir(12, 8), dir(2, 0), dir(0, 0), dir(8, 4)]
	var h = c.hand(hands[i])
	var wd = c.w(dirs[i])
	seg(c.l, h - wd * 6.0, h + wd * 9.0, 1, wood)
	block(c.l, h, wd, 9.0, 10.6, 1.0, stone, stone_lit)
	seg(c.l, h + wd * 10.6, h + wd * 12.2, 1, stone_lit)
	c.arm_to(h - wd * 4.0, true)
	c.arm_to(h, false)
	if i == 2:
		var pp = across(wd)
		seg(c.fx, h + wd * 2.0 + pp * 2.0, h + wd * 8.0 + pp * 2.0, 1, smear)
		seg(c.fx, h + wd * 3.0 - pp * 2.0, h + wd * 9.0 - pp * 2.0, 1, smear)
		set_(c.fx, h + wd * 13.5, spark)

# metal blade, one hand: raised, slash across with a crescent smear, follow-through, recover
static func blade(c, i: int) -> void:
	var hands = [Vector3(0, 1, 3.5), Vector3(4, 0.5, 0.5), Vector3(5, -2.5, -1.5), Vector3(2.5, -0.5, -3)]
	var dirs = [dir(75, 25), dir(15, 40), dir(-20, -45), dir(-55, -10)]
	var h = c.hand(hands[i])
	var wd = c.w(dirs[i])
	if i == 1 or i == 2:
		c.smear_arc(c.hand(hands[i - 1]), dirs[i - 1], h, dirs[i], 4.0, 9.0, smear)
	seg(c.l, h - wd * 2.0, h, 1, wood_dark)
	block(c.l, h, wd, 0.0, 0.4, 1.6, gold, gold)
	block(c.l, h, wd, 1.0, 8.0, 0.5, steel, steel_dark)
	seg(c.l, h + wd * 8.0, h + wd * 9.2, 1, steel)
	c.arm_to(h, false)

# hammer, two hands: lifted over the head, coming down, smashed into the ground (dust, cracks), recover
static func hammer(c, i: int) -> void:
	var hands = [Vector3(-0.5, -1.5, 5), Vector3(3, -1.5, 2.5), Vector3(5.5, -1.5, -2.5), Vector3(4.5, -1.5, -3)]
	var dirs = [dir(115, 0), dir(40, 0), dir(-50, 0), dir(-40, 0)]
	var h = c.hand(hands[i])
	var wd = c.w(dirs[i])
	if i == 1 or i == 2:
		c.smear_arc(c.hand(hands[i - 1]), dirs[i - 1], h, dirs[i], 6.0, 10.5, smear)
	seg(c.l, h - wd * 2.0, h + wd * 8.0, 1, wood)
	block(c.l, h, wd, 7.5, 10.5, 2.5, iron, iron_lit)
	c.arm_to(h - wd * 2.5, true)
	c.arm_to(h, false)
	if i >= 2:
		var hit = h + wd * 9.0 + Vector2(0, 2.5)
		c.dust_ring(hit, 6.0 if i == 2 else 8.5, 1.0 if i == 2 else 0.55, true)

# bow, held out in the other hand: nock, full draw, release (the string snaps, a streak), recover
static func bow(c, i: int) -> void:
	var draw = [Vector3(3, -4.5, -1), Vector3(-1, -4.5, -1), Vector3(-2.5, -3.5, 0), Vector3(0.5, -4, -2)]
	var bend = [1.1, 1.7, 1.0, 1.0]
	var grip = c.hand(Vector3(6, -4.5, -1), true)
	var h = c.hand(draw[i])
	# the bow: limbs from the grip up and down, curving back toward the archer. Upright seen from the side;
	# canted over seen from the front or back, so its curve still shows (edge-on it would be a stick).
	var cant: float = absf(sin(c.a)) * deg_to_rad(75.0)
	for k in range(-7, 7):
		seg(c.l, _bow_limb(k, bend, c, cant, grip, i), _bow_limb(k + 1, bend, c, cant, grip, i), 1, wood_dark if absi(k) > 4 else wood_lit)
	var top = _bow_limb(7, bend, c, cant, grip, i)
	var bottom = _bow_limb(-7, bend, c, cant, grip, i)
	var fwd = c.w(dir(0, 0))
	if i <= 1:
		# string to the drawing hand, and the arrow on it
		seg(c.l, top, h, 1, bow_string)
		seg(c.l, h, bottom, 1, bow_string)
		var tip = grip + fwd * 3.0
		seg(c.l, h, tip, 1, wood_lit)
		dot(c.l, tip, 2, stone_lit)
	else:
		seg(c.l, top, bottom, 1, bow_string)
	c.arm_to(grip, true)
	c.arm_to(h, false)
	if i == 2:
		seg(c.fx, grip + fwd * 3.0, grip + fwd * 11.0, 1, smear)
		c.star(grip + fwd * 2.0, 2.0)

# local function Limb of the method above
static func _bow_limb(u: float, bend, c, cant, grip, i) -> Vector2:
	return grip + c.w(Vector3(-(u * u) / 18.0 * bend[i], u * sin(cant) * 1.2, u * cos(cant)))

# NOX claw swipe: arm raised, the claws rake across (magenta smear), follow-through, recover
static func claw(c, i: int) -> void:
	var hands = [Vector3(0, 1.5, 4.5), Vector3(5, 0.5, 0.5), Vector3(5, -3.5, -2.5), Vector3(1.5, 0, -4)]
	var h = c.hand(hands[i])
	if i == 1 or i == 2:
		var h0 = c.hand(hands[i - 1])
		for k in range(11):
			var p = h0.lerp(h, k / 10.0)
			dot(c.fx, p + c.w(dir(0, 0)) * 2.0, 2, Color(c.r.claw, 0.5))
	c.arm_to(h, false)
	c.claws(h, false)

# Spitter: rears back with the mouth open, then a glob of acid flies out
static func spit_at(c, i: int, mouth: Vector2, behind: bool) -> void:
	var fwd = c.w(dir(5, 0))
	if i == 0 and not behind:
		dot(c.l, mouth, 2, Color("120A16"))
	if i == 1 or i == 2:
		var g = mouth + fwd * (3.0 if i == 1 else 8.0)
		dot(c.l, g, 3, spit)
		set_(c.l, g - Vector2(1, 1), spit_lit)
		set_(c.fx, g - fwd * 2.5, spit_dark)
		if i == 2:
			set_(c.fx, g - fwd * 4.5, spit_dark)
	if i == 3:
		set_(c.fx, mouth + fwd * 10.0, Color(spit, 0.7))
		set_(c.fx, mouth + fwd * 12.0 + Vector2(0, 1), Color(spit, 0.5))

# Wrecker: both fists up, down, into the ground (dust and cracks), recover
static func smash(c, i: int) -> void:
	var hands = [Vector3(-0.5, -0.5, 6.5), Vector3(4, -1, 1), Vector3(6.5, -1.5, -5.5), Vector3(5, -1, -4.5)]
	var hm = c.hand(hands[i])
	var ho = c.hand(hands[i], true)
	c.arm_to(ho, true, c.r.hand_w + 1)
	c.claws(ho, true)
	c.arm_to(hm, false, c.r.hand_w + 1)
	c.claws(hm, false)
	if i >= 2:
		var hit = hm.lerp(ho, 0.5) + Vector2(0, 3)
		c.dust_ring(hit, 8.0 if i == 2 else 11.0, 1.0 if i == 2 else 0.55, true)

# ---- tools/PixelGatherPoses.cs
# Dedicated harvesting cells, composed from each character's own body, clothes and skin palette.
static var GATHER_KINDS = ["wood", "stone", "herb"]
const GATHER_FRAMES: int = 6
const GATHER_FPS: float = 7.0

static func gather(kind, body, view, r, legs: int, feet: int, cell: int, shadow_rx: float) -> Variant:
	var a: float = facing(view)
	var behind: bool = view.begins_with("up")
	var out = Cs.arr(GATHER_FRAMES, null)
	for i in range(out.size()):
		var b = body.duplicate()
		b.fill_rect(r.erase_main, clear)
		b.fill_rect(r.erase_off, clear)
		if r.patch.size != Vector2i.ZERO:
			b.fill_rect(r.patch, r.patch_col)
		var drop: int = [1, 3, 5, 5, 3, 1][i] if kind == "herb" else 0
		var lean_by: int = [0, -1, 0, 2, 1, 0][i]
		var d = Vector2i(roundi(cos(a) * lean_by), maxi(0, roundi(sin(a) * lean_by * K)))
		var im = embed(lean(kneel(b, legs, drop), legs + drop, d), cell)
		var o: int = (cell - body.get_width()) / 2
		var shift = Vector2(o + d.x, o + d.y + drop)
		var c = Cs.init(Ctx.new(), {"a": a, "r": r, "sm": r.main + shift, "so": r.off + shift, "l": blank(cell, cell), "fx": blank(cell, cell)})
		if kind == "herb":
			gather_herb(c, i)
		else:
			gather_tool(c, i, kind == "wood")
		var done = blank(cell, cell)
		if behind:
			over(done, c.l, true)
			over(done, im, false)
		else:
			over(done, im, false)
			over(done, c.l, true)
		done = finish(done, feet + o + 1, shadow_rx)
		if behind:
			under(done, c.fx)
		else:
			over(done, c.fx, false)
		out[i] = done
	return out

static func gather_tool(c, i: int, axe: bool) -> void:
	# Raise, wind up, swing, contact, follow through, return to a relaxed grip.
	var hands = [Vector3(1, -1, -1), Vector3(-1, -1, 4), Vector3(1, -1, 5), Vector3(6, -1, -1), Vector3(5, -1, -2), Vector3(2, -1, -1)]
	var pitch = [35.0, 100, 80, -12, -30, 20] if axe else [30.0, 110, 90, -50, -60, 10]
	var h = c.hand(hands[i])
	var wd = c.w(dir(pitch[i], 0))
	var pp = across(wd)
	var head = h + wd * 8
	seg(c.l, h - wd * 3, head, 1, wood)
	seg(c.l, h - wd * 2, h + wd * 5, 1, wood_lit)
	if axe:
		# Wedge blade on one side of the wooden haft, with a bright cutting edge.
		block(c.l, head, pp, -1, 1, 1.2, iron, iron_lit)
		for j in range(1, 5):
			seg(c.l, head + pp * j - wd * (1 + j * 0.35), head + pp * j + wd * (1 + j * 0.35), 1, steel if j == 4 else steel_dark)
	else:
		# Slim curved pick: pointed ends read differently from the broad axe blade.
		seg(c.l, head - pp * 3, head + pp * 3, 2, iron_lit)
		seg(c.l, head - pp * 3, head - pp * 5 + wd * 2, 1, steel)
		seg(c.l, head + pp * 3, head + pp * 5 + wd * 2, 1, steel)
	c.arm_to(h - wd * 2.5, true)
	c.arm_to(h, false)
	if i == 3:
		var hit = head + pp * 4 if axe else head + pp * 5 + wd * 2
		if not axe:
			c.star(hit, 2)
		var chip: Color = wood_lit if axe else stone_lit
		set_(c.fx, hit + Vector2(3, -3), chip)
		set_(c.fx, hit + Vector2(-3, -1), chip)

static func gather_herb(c, i: int) -> void:
	var hands = [Vector3(2, -1, -1), Vector3(4, -1, -2), Vector3(6, -1, -3), Vector3(6, -1, -3), Vector3(4, -1, 0), Vector3(2, -1, 0)]
	var h = c.hand(hands[i])
	c.arm_to(c.hand(Vector3(3, -2.5, -2), true), true)
	c.arm_to(h, false)
	if i >= 3 and i <= 4:
		var leaf = Color("64A65B")
		seg(c.l, h, h + Vector2(1, -4), 1, Color("356746"))
		seg(c.l, h + Vector2(1, -2), h + Vector2(-2, -4), 2, leaf)
		seg(c.l, h + Vector2(1, -3), h + Vector2(3, -5), 2, leaf)

class Rig:
	var main: Vector2 = Vector2.ZERO  # shoulders: the weapon arm and the other one
	var off: Vector2 = Vector2.ZERO  # shoulders: the weapon arm and the other one
	var erase_main: Rect2i = Rect2i()  # the arms as drawn at rest (taken off when that arm acts)
	var erase_off: Rect2i = Rect2i()  # the arms as drawn at rest (taken off when that arm acts)
	var patch: Rect2i = Rect2i()  # pixels repainted with PatchCol (the resting hand in the side view)
	var patch_col: Color = Color(0, 0, 0, 0)
	var mouth: Vector2 = Vector2.ZERO  # the Spitter's mouth
	var arm_w: int = 2
	var hand_w: int = 2
	var reach: float = 1.0  # hand distances are scaled by this (bigger bodies reach further)
	var arm: Color = Color(0, 0, 0, 0)
	var hand: Color = Color(0, 0, 0, 0)
	var claw: Color = PixelPoses.clear

class Ctx:
	var a: float = 0.0
	var r
	var sm: Vector2 = Vector2.ZERO
	var so: Vector2 = Vector2.ZERO
	var l
	var fx

	# the hand (weapon arm, or the other arm with the side mirrored) at V from its shoulder
	func hand(v: Vector3, off: bool = false) -> Vector2:
		var vv = Vector3(v.x, -v.y, v.z) if off else v
		return (so if off else sm) + PixelPoses.proj(a, vv * r.reach)

	# one unit along a 3D direction, on screen
	func w(d: Vector3) -> Vector2:
		return PixelPoses.proj(a, d)

	func arm_to(hand_: Vector2, off: bool, hand_w: int = 0) -> void:
		PixelPoses.seg(l, so if off else sm, hand_, r.arm_w, r.arm)
		PixelPoses.dot(l, hand_, hand_w if hand_w > 0 else r.hand_w, r.hand)

	# three hooked claws out of a hand, pointing away from the shoulder
	func claws(hand_: Vector2, off: bool) -> void:
		var d = hand_ - (so if off else sm)
		d = Vector2.DOWN if d.length() < 0.5 else d.normalized()
		var pp = d.orthogonal()
		for k in range(-1, 2):
			var root = hand_ + pp * (k * 1.2)
			PixelPoses.seg(l, root, root + d * 2.5 + pp * (k * 0.8), 1, r.claw)

	# a pale crescent left by the tip of a swung weapon, between two hand positions and directions
	func smear_arc(h0: Vector2, d0: Vector3, h1: Vector2, d1: Vector3, r0: float, r1: float, c: Color) -> void:
		for i in range(15):
			var t: float = i / 14.0
			var d = d0.lerp(d1, t).normalized()
			var h = h0.lerp(h1, t)
			# thin at the start, full at the end
			var inner: float = lerpf(r1 - 0.5, r0, t)
			var rr: float = inner
			while rr <= r1:
				PixelPoses.set_(fx, h + w(d) * rr, c)
				rr += 0.5

	func dust_ring(c: Vector2, rx: float, alpha: float, cracks: bool) -> void:
		var col = Color(PixelPoses.dust, PixelPoses.dust.a * alpha)
		for i in range(14):
			var ang: float = i / 14.0 * TAU
			PixelPoses.set_(fx, c + Vector2(cos(ang) * rx, sin(ang) * rx * 0.45), col)
		# puffs at the sides, clear of what made the dust
		for p in [Vector2(-rx * 0.9, -1.5), Vector2(rx * 0.85, -2), Vector2(-rx * 0.6, -3), Vector2(rx * 0.55, -3.5)]:
			PixelPoses.dot(fx, c + p, 2, col)
		if cracks:
			for d in [Vector2(1, 0.35), Vector2(-1, 0.3), Vector2(0.2, 0.5)]:
				PixelPoses.seg(fx, c, c + d * 4.0, 1, PixelPoses.crack)

	func star(c: Vector2, r_: float) -> void:
		PixelPoses.set_(fx, c, PixelPoses.spark)
		for d in [Vector2(1, 0), Vector2(-1, 0), Vector2(0, 1), Vector2(0, -1)]:
			PixelPoses.seg(fx, c + d * 1.0, c + d * r_, 1, Color(PixelPoses.spark, 0.9))
