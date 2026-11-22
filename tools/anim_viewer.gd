class_name AnimViewer
extends Node2D
# Owner: Viet Anh. Looks at every sprite animation in art/chars without playing a match:
#   godot --path . tools/AnimViewer.tscn
# The top shows the chosen animation playing in all 8 directions, the bottom every frame of it in one direction.
# The red cross is the anchor (where the game puts the character's feet).
#   Left / Right  character        Up / Down  animation        Z / X  direction of the frame strip
#   Space  pause    , / .  one frame back / forward (paused)    - / +  speed    Esc  quit
# Start somewhere else with  -- --char=wrecker --anim=down  (--shot=file.png saves a screenshot and quits).
static var DIRS = ["right", "down", "left", "up", "down_right", "down_left", "up_left", "up_right"]
static var SPEEDS = [0.125, 0.25, 0.5, 1.0, 2.0]
# a one-shot animation holds its last frame this long before playing again
const HOLD: float = 0.8
var sheets = []
var who: int = 0
var which: int = 0
var strip_dir: int = 1
var speed: int = 3
var paused: bool = false
var t: float = 0.0
var up: float = 0.0
var shot = ""

func _ready() -> void:
	texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	var frames = null
	var fps = null
	for file in ["chars.json", "bosses.json"]:
		var info = JSON.parse_string(FileAccess.get_file_as_string("res://art/chars/" + file))
		# bosses.json has no idle / run of its own: the bosses use the same columns as chars.json
		if info.has("frames"):
			frames = info["frames"]
			fps = info["fps"]
		var chars = info["chars"]
		for key in chars:
			var value = chars[key]
			var c = value
			var a = c["anchor"]
			var s = Cs.init(Sheet.new(), {"name": str(key), "tex": load("res://art/chars/" + str(key) + ".png"), "size": int(c["size"]), "anchor": Vector2(float(a[0]), float(a[1]))})
			# the looping ones (idle, run) are listed at the top of the json, the one-shots under each character
			for n in frames:
				var f = frames[n]
				var r = f
				s.anims.append(Cs.init(Anim.new(), {"name": str(n), "first": int(r[0]), "count": int(r[1]), "fps": float(fps[n]), "loop": true}))
			if c.has("anims"):
				var names = []
				for n in c["anims"]:
					var _unused = c["anims"][n]
					names.append(str(n))
				# attacks first (in the order of the sheet), then down
				var one = c["anims"]
				Cs.sort_cmp(names, func(x, y): return Cs.compare(int(one[x][0]), int(one[y][0])))
				for n in names:
					var r = one[n]
					s.anims.append(Cs.init(Anim.new(), {"name": n, "first": int(r[0]), "count": int(r[1]), "fps": float(r[2]), "loop": n.begins_with("gather_")}))
			sheets.append(s)
	# start on the first attack of the first character, unless told otherwise
	which = mini(2, sheets[0].anims.size() - 1)
	for arg in OS.get_cmdline_user_args():
		if arg.begins_with("--char="):
			who = maxi(0, Cs.find_index(sheets, func(s): return s.name == arg.substr(7)))
		elif arg.begins_with("--anim="):
			which = maxi(0, Cs.find_index(sheets[who].anims, func(a): return a.name == arg.substr(7)))
		elif arg.begins_with("--shot="):
			shot = arg.substr(7)

var current:
	get:
		return sheets[who].anims[which]

# the column shown at time T
func frame(a, at: float) -> int:
	var f: int = int(floorf(at * a.fps))
	if a.loop:
		return a.first + (f % a.count + a.count) % a.count
	var cycle: float = a.count / a.fps + HOLD
	f = int(floorf(fposmod(at, cycle) * a.fps))
	return a.first + mini(f, a.count - 1)

func _process(delta: float) -> void:
	if not paused:
		t += delta * SPEEDS[speed]
	queue_redraw()
	up += delta
	if shot != "" and up > 0.5:
		get_viewport().get_texture().get_image().save_png(shot)
		get_tree().quit()

func _unhandled_input(e) -> void:
	var k = e
	if not e is InputEventKey or not k.pressed:
		return
	var s = sheets[who]
	var _sw1 = k.keycode
	if _sw1 == KEY_RIGHT:
		pick(1)
	elif _sw1 == KEY_LEFT:
		pick(-1)
	elif _sw1 == KEY_DOWN:
		which = (which + 1) % s.anims.size()
		t = 0
	elif _sw1 == KEY_UP:
		which = (which - 1 + s.anims.size()) % s.anims.size()
		t = 0
	elif _sw1 == KEY_Z:
		strip_dir = (strip_dir - 1 + 8) % 8
	elif _sw1 == KEY_X:
		strip_dir = (strip_dir + 1) % 8
	elif _sw1 == KEY_SPACE:
		paused = not paused
	elif _sw1 == KEY_PERIOD:
		paused = true
		t += 1.0 / current.fps
	elif _sw1 == KEY_COMMA:
		paused = true
		t = maxf(0, t - 1.0 / current.fps)
	elif _sw1 == KEY_MINUS or _sw1 == KEY_KP_SUBTRACT:
		speed = maxi(0, speed - 1)
	elif _sw1 == KEY_EQUAL or _sw1 == KEY_PLUS or _sw1 == KEY_KP_ADD:
		speed = mini(SPEEDS.size() - 1, speed + 1)
	elif _sw1 == KEY_ESCAPE:
		get_tree().quit()

# another character, keeping the same animation when it has one of that name
func pick(step: int) -> void:
	var was = current.name
	who = (who + step + sheets.size()) % sheets.size()
	var i: int = Cs.find_index(sheets[who].anims, func(a): return a.name == was)
	which = i if i >= 0 else mini(which, sheets[who].anims.size() - 1)
	t = 0

func _draw() -> void:
	var view = get_viewport_rect().size
	var font = ThemeDB.fallback_font
	var s = sheets[who]
	var a = current
	draw_rect(Rect2(Vector2.ZERO, view), Color(0.13, 0.14, 0.17))
	var pad: float = 12
	var gap: float = 10
	var head: float = 40
	var foot: float = 26
	# the bottom strip is a third of the height, the 8 directions get the rest
	var strip_h: float = (view.y - head - foot) * 0.32
	var grid_h: float = view.y - head - foot - strip_h - gap
	var big: float = maxf(1, floorf(minf((view.x - 2 * pad - 3 * gap) / (4 * s.size), (grid_h - gap - 32) / (2 * s.size))))
	var small: float = maxf(1, floorf(minf((view.x - 2 * pad) / (a.count * (s.size + 4)), (strip_h - 18) / s.size)))
	var title = "%s  (%s/%s)   %s  [%s..%s]  %s fps  x%s%s" % [s.name, who + 1, sheets.size(), a.name, a.first, a.first + a.count - 1, Cs.s(a.fps), Cs.s(SPEEDS[speed]), "  PAUSED" if paused else ""]
	draw_string(font, Vector2(pad, 26), title, HORIZONTAL_ALIGNMENT_LEFT, -1, 18, Color.WHITE)
	var col: int = frame(a, t)
	var cell_w: float = s.size * big
	var x0: float = floorf((view.x - (4 * cell_w + 3 * gap)) / 2)
	for d in range(8):
		var at = Vector2(x0 + d % 4 * (cell_w + gap), head + d / 4 * (cell_w + 16 + gap))
		cell(s, col, d, at, big, d == strip_dir)
		draw_string(font, at + Vector2(0, cell_w + 13), DIRS[d], HORIZONTAL_ALIGNMENT_LEFT, -1, 12, Color(0.75, 0.78, 0.85))
	# every frame of the animation in one direction, the one playing outlined
	var sy: float = view.y - foot - strip_h
	draw_string(font, Vector2(pad, sy + 12), "frames, %s (Z / X)" % [DIRS[strip_dir]], HORIZONTAL_ALIGNMENT_LEFT, -1, 12, Color(0.75, 0.78, 0.85))
	var sw: float = s.size * small
	var sx: float = floorf((view.x - (a.count * sw + (a.count - 1) * 4)) / 2)
	for f in range(a.count):
		var at = Vector2(sx + f * (sw + 4), sy + 18)
		cell(s, a.first + f, strip_dir, at, small, a.first + f == col)
	draw_string(font, Vector2(pad, view.y - 8), "Left/Right character   Up/Down animation   Z/X strip direction   Space pause   , . step   - + speed   Esc quit", HORIZONTAL_ALIGNMENT_LEFT, -1, 12, Color(0.55, 0.58, 0.65))

# one sheet cell scaled up, on a checkerboard, with the anchor marked
func cell(s, col: int, row: int, at: Vector2, zoom: float, lit: bool) -> void:
	var w: float = s.size * zoom
	var q: float = maxf(4, 4 * zoom)
	var y: float = 0
	while y < w:
		var x: float = 0
		while x < w:
			var odd: bool = (int(x / q) + int(y / q)) % 2 == 1
			draw_rect(Rect2(at + Vector2(x, y), Vector2(minf(q, w - x), minf(q, w - y))), Color(0.24, 0.26, 0.3) if odd else Color(0.2, 0.22, 0.26))
			x += q
		y += q
	draw_texture_rect_region(s.tex, Rect2(at, Vector2(w, w)), Rect2(col * s.size, row * s.size, s.size, s.size))
	var p = at + s.anchor * zoom
	var red = Color(1, 0.3, 0.3, 0.8)
	draw_line(p - Vector2(zoom * 2, 0), p + Vector2(zoom * 2, 0), red, 1)
	draw_line(p - Vector2(0, zoom * 2), p + Vector2(0, zoom * 2), red, 1)
	draw_rect(Rect2(at, Vector2(w, w)), Color(1, 0.85, 0.3) if lit else Color(0.35, 0.38, 0.45), false, 2 if lit else 1)

class Anim:
	var name
	var first: int = 0
	var count: int = 0
	var fps: float = 0.0
	var loop: bool = false

class Sheet:
	var name
	var tex
	var size: int = 0
	var anchor: Vector2 = Vector2.ZERO
	var anims = []
