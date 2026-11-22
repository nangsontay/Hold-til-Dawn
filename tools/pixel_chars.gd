class_name PixelChars
extends Node

# ---- tools/PixelChars.cs
# Pixel-art sprites of the six GDD characters (tab:characters: CHR-01..03 players, CHR-05..07 enemies),
# following the style guide of GDD ch. 6 (tab:style-guide): 32 x 32 px at tile scale (Wrecker 1.25 tiles = 40 x 40),
# dark outline, limited shading, light from the upper left, GDD palette, a walk cycle in 8 directions
# (left is the mirrored right). The diagonals are 3/4 views: the same silhouette as the front / back view,
# with the face redrawn turned toward the right (far eye thinner, face shifted, hair or an ear showing). Not part of the game. Run: godot --headless --path . tools/PixelChars.tscn
# Writes art/chars/<character>.png (1x pixels; the game scales them up without smoothing) and art/chars/chars.json.
# Sheet layout: rows = right, down, left, up, down-right, down-left, up-left, up-right; columns = idle 0-1,
# walk 2-5, then the fight frames of tools/PixelPoses.cs, 4 per attack (players: bare hands, stone spear,
# metal blade, hammer, bow; enemies: their own attack), and last 4 frames knocked down (hit, knees give,
# falling, lying with the eyes shut). Players then have three 6-frame harvesting cycles (wood, stone,
# herbs) from PixelGatherPoses.cs. chars.json lists them under "anims". A cell is the body plus
# PixelPoses.M pixels on every side, so a weapon at full reach fits; "body" is the character's own size.
# The PNGs can be repainted by hand (Aseprite, Piskel, ...): the game reads them as they are.
# #
# Each body is a pixel map: one letter per pixel, looked up in the character's palette ('.' = empty).
# A character = a body (human / nox / wrecker) + its palette + overlays (gear) drawn on top.
const INK = Color("141019")
const SHADOW = Color(0, 0, 0, 0.32)
# --- bodies: [top row, rows]; rows are 16 px wide (20 for the Wrecker), centred on the canvas --------
static var HUMAN = {"size": 32, "feet": 28, "legs": 23, "shadow": 6, "split": {"down": [2, 8, 14], "up": [2, 8, 14], "side": [4, 8, 14]}, "down": [6, ["....HHHHHHHH....", "...HHHHHHHHHH...", "..HHHHHHHHHHHh..", "..HHHHHHHHHHHh..", "..HHSSSSSSSSHh..", "..HSSSSSSSSSSh..", "..HSSEESSEESSh..", "..hSSEESSEESSh..", "..hSSSSSSSSSSs..", "...sSSSSSSSSs...", "....ssSSSSss....", "..ccLCCCCCCCcc..", "..ccLCCCCCCCcc..", "..ccLCCCCCCCcc..", "..SSLCCCCCCCSS..", "..SSCCCCCCCCSS..", "...GGGGGGGGGG...", "...PPPPPPPPPp...", "...PPPp..PPPp...", "...PPPp..PPPp...", "...PPPp..PPPp...", "...BBBb..BBBb...", "..BBBBb..BBBBb.."]], "up": [6, ["....HHHHHHHH....", "...HHHHHHHHHH...", "..HHHHHHHHHHHh..", "..HHHHHHHHHHHh..", "..HHHHHHHHHHHh..", "..HHHHHHHHHHHh..", "..HHHHHHHHHHHh..", "..hHHHHHHHHHHh..", "..shHHHHHHHHhs..", "...shhhhhhhhs...", "....ssSSSSss....", "..ccLCCCCCCCcc..", "..ccLCCCCCCCcc..", "..ccLCCCCCCCcc..", "..SSLCCCCCCCSS..", "..SSCCCCCCCCSS..", "...GGGGGGGGGG...", "...PPPPPPPPPp...", "...PPPp..PPPp...", "...PPPp..PPPp...", "...PPPp..PPPp...", "...BBBb..BBBb...", "..BBBBb..BBBBb.."]], "side": [6, [".....HHHHHHH....", "....HHHHHHHHH...", "...HHHHHHHHHHH..", "...HHHHHHHHHHH..", "...HHHHHHSSSSS..", "...HHHHHSSSSSS..", "...hHHHHSSSSES..", "...hHHHSSSSSES..", "...hhHSSSSSSSS..", "....hhsSSSSSs...", "......sSSSs.....", ".....cLCCCCc....", ".....cLCCCCc....", ".....cLccCCc....", ".....cLccCCc....", ".....CCSSCCc....", ".....GGGGGGG....", ".....PPPPPPp....", ".....PPp.PPp....", ".....PPp.PPp....", ".....PPp.PPp....", ".....BBb.BBb....", ".....BBBB.BBBB.."]]}
static var NOX = {"size": 32, "feet": 28, "legs": 25, "shadow": 7, "split": {"down": [4, 8, 13], "up": [4, 8, 13], "side": [2, 7, 12]}, "down": [10, ["....nMMMMMMn....", "...MMNNNNNNNn...", "..MNNNNNNNNNNn..", ".MNNNNNNNNNNNNn.", ".MNYYNNNNNNYYNn.", ".NNYYNNNNNNYYNn.", ".NNNNNNNNNNNNNn.", ".nNNDTDTTDTDNNn.", "MNnNNDDDDDDNNnNn", "MNnnNNNNNNNNnnNn", "MNn.nNNNNNNNn.Nn", "MNn.nNNNNNNNn.Nn", "NNn.nNNNNNNNn.Nn", "Nn..nNNNNNNNn.nn", "AA...nNnnNNn..AA", ".....nNn.nNn....", ".....nNn.nNn....", "....nnNn.nNnn...", "....nnnn.nnnn..."]], "up": [10, ["....nMMMMMMn....", "...MMNNNNNNNn...", "..MNNNNNNNNNNn..", ".MNNNNNNNNNNNNn.", ".MNNNNNNNNNNNNn.", ".NNNNNNNNNNNNNn.", ".NNNNNNNNNNNNNn.", ".nNNNNNNNNNNNNn.", "MNnNNNNNNNNNNnNn", "MNnnNNNNNNNNnnNn", "MNn.nNNNNNNNn.Nn", "MNn.nNNNNNNNn.Nn", "NNn.nNNNNNNNn.Nn", "Nn..nNNNNNNNn.nn", "AA...nNnnNNn..AA", ".....nNn.nNn....", ".....nNn.nNn....", "....nnNn.nNnn...", "....nnnn.nnnn..."]], "side": [10, ["...nMMMMMMn.....", "..MNNNNNNNNMn...", ".MNNNNNNNNNNNn..", ".NNNNNNNNNNNNNn.", ".NNNNNNNNNNYYNn.", ".nNNNNNNNNNYYNNn", ".nNNNNNNNNNNNNNn", ".nNNNNNNNNNNDTDn", "..nNNNNNNNNNNDDn", "..nNNNNNNNNNNNn.", "..nnNNNNNNNNNNn.", "...nNNNNNNNnNNn.", "...nNNNNNNn.nNn.", "...nNNnnNNn.nNn.", "...nNn..nNn.nNn.", "...nNn..nNn.AAA.", "...nNn..nNn.....", "..nnNn.nnNn.....", "..nnnn.nnnn....."]]}
static var WRECKER_BODY = {"size": 40, "feet": 37, "legs": 32, "shadow": 10, "split": {"down": [4, 10, 17], "up": [4, 10, 17], "side": [3, 10, 17]}, "down": [12, ["......A......A......", "......AA....AA......", ".....RrRRRRRRrR.....", "....RRRRRRRRRRRr....", "...RRRRRRRRRRRRRr...", "...NNNNNNNNNNNNNn...", "..NNYYYNNNNNNYYYNn..", "..NNNNNNNNNNNNNNNn..", "..nNNDTDTDTDTDNNNn..", ".RRRnNDDDDDDDDNnRRR.", "RRRRRRNNNNNNNNRRRRRr", "ARRRRRRRRRRRRRRRRRrA", "NNn.RRRRRRRRRRRr.nNn", "NNn.RRRRAARRRRRr.nNn", "NNn.RRRRRRRRRRRr.nNn", "NNn.rrrrrrrrrrrr.nNn", "NNn.nNNNNNNNNNNn.nNn", "NNn.nNNNNNNNNNNn.nNn", "Nnn..nNNNNNNNNn..nNn", "AAA..nNNNnnNNNn..AAA", ".....nNNn..nNNn.....", ".....nNNn..nNNn.....", ".....RRRr..RRRr.....", ".....nNNn..nNNn.....", "....nnNNn..nNNnn....", "....nnnnn..nnnnn...."]], "up": [12, ["......A......A......", "......AA....AA......", ".....RrRRRRRRrR.....", "....RRRRRRRRRRRr....", "...RRRRRRRRRRRRRr...", "...NNNNNNNNNNNNNn...", "..NNNNNNNNNNNNNNNn..", "..NNNNNNNNNNNNNNNn..", "..nNNNNNNNNNNNNNNn..", ".RRRnNNNNNNNNNNnRRR.", "RRRRRRRRRRRRRRRRRRRr", "ARRRRRRRRRRRRRRRRRrA", "NNn.RRRRRAARRRRr.nNn", "NNn.RRRRRAARRRRr.nNn", "NNn.RRRRRAARRRRr.nNn", "NNn.rrrrrrrrrrrr.nNn", "NNn.nNNNNNNNNNNn.nNn", "NNn.nNNNNNNNNNNn.nNn", "Nnn..nNNNNNNNNn..nNn", "AAA..nNNNnnNNNn..AAA", ".....nNNn..nNNn.....", ".....nNNn..nNNn.....", ".....RRRr..RRRr.....", ".....nNNn..nNNn.....", "....nnNNn..nNNnn....", "....nnnnn..nnnnn...."]], "side": [12, [".........A....A.....", ".........AA...AA....", "......RRRRRRRRRr....", ".....RRRRRRRRRRRr...", "....RRRRRRRRRRRRRr..", "....NNNNNNNNNNNNNn..", "...NNNNNNNNNNNYYYNn.", "...NNNNNNNNNNNNNNNn.", "...nNNNNNNNNNNNDTDn.", "..RRRRnNNNNNNNNNDDn.", ".RRRRRRRRNNNNNNNNn..", ".ARRRRRRRRRRRRRRRr..", "..RRRRRRRRRRRRRRRr..", "..RRRRRRRRRRRnNNNn..", "..RRRRRRRRRRRnNNNn..", "..nrrrrrrrrrrnNNNn..", "...nNNNNNNNNNnNNNn..", "...nNNNNNNNNNnNNNn..", "....nNNNNNNNNnAAAA..", "....nNNNNnNNNn......", "....nNNNn.nNNNn.....", "....nNNNn.nNNNn.....", "....RRRr..RRRRr.....", "....nNNNn.nNNNn.....", "...nnNNNn.nNNNNn....", "...nnnnnn.nnnnnn...."]]}
# --- characters: body, palette (GDD tab:style-guide colours first), gear overlays -------------------
static var SKIN_L = {"S": "E9B48A", "s": "C98F66", "E": "1A1418"}
static var BOOTS = {"B": "2E2620", "b": "1C1612"}
static var NOX_PAL = {"N": "4A2C5A", "n": "2F1B3B", "M": "6B4380", "Y": "E8E3C8", "D": "120A16", "T": "E8E3C8", "A": "C63FA8", "a": "8A2A75"}
static var CHARACTERS = {"scout": {"body": "human", "pal": {"H": "6B4A2B", "h": "4E3520", "C": "2BB3B1", "L": "6FD9D5", "c": "1E7F7D", "G": "4A3626", "P": "5A6B34", "p": "3E4B23", "T": "36C9C6", "W": "7A5634", "w": "E8E3C8", "F": "36C9C6"}, "over": {"down": [9, ["..TTTTTTTTTTTT..", "................", "...............W", "..............WW", "..............W.", "..............W.", "..............W.", "..............W.", ".....W........W.", "......W.......W.", ".......W......W.", "........W.....W.", ".........W.....W"]], "up": [9, ["..TTTTTTTTTTTT..", ".......TT.......", ".......TT.......", "................", "..............W.", "..............W.", ".............W..", "...........WW...", "..........W.....", ".........W......", "........W.......", ".......W........", "......W.........", "....WW.........."]], "side": [9, ["...TTTTTTTTTTT..", "...TT...........", "..TT............", "...W............", "..Ww............", "..Ww............", "..Ww............", "..Ww............", "..Ww............", "..Ww............", "..Ww............", "..Ww............", "..Ww............", "...W............"]]}}, "healer": {"body": "human", "pal": {"H": "2B1E1A", "h": "1A120F", "S": "A86B45", "s": "82502F", "C": "F2F2F2", "L": "FFFFFF", "c": "C3CAD3", "G": "8A96A3", "P": "4A535E", "p": "353C45", "X": "3DBE5A", "K": "F2F2F2", "k": "8A96A3", "Z": "9BE3B0"}, "over": {"down": [14, ["..H..........H..", "..H..........H..", "..H..........H..", "..k.........k...", "...k.......k....", ".......X........", "......XXX.......", ".......X........", "................", "..............Z.", "..............Z."]], "up": [14, ["..hHHHHHHHHHHh..", "...HHHHHHHHHH...", "....HHHHHHHH....", "...kkkkkkkkkk...", "...kKKKKKKKKk...", "...kKKKXXKKKk...", "...kKKXXXXKKk...", "...kKKKXXKKKk...", "...kkkkkkkkkk..."]], "side": [14, ["...HH...........", "...HHH..........", "..kkkk..........", "..kKKk..........", "..kXKk..........", "..XXXk.XX.......", "..kXKk..........", "..kKKk..........", "..kkkk.........."]]}}, "engineer": {"body": "human", "pal": {"H": "3A2A20", "h": "261A13", "C": "FF7A1A", "L": "FFA65C", "c": "2B3A55", "G": "5A3E26", "P": "2B3A55", "p": "1E2A3F", "O": "FF7A1A", "o": "C2560A", "Q": "FFB070", "V": "D9DDE0", "M": "8A8F98", "m": "5B5F6B", "W": "7A5634", "D": "3A4250", "Y": "F2B544"}, "over": {"down": [4, [".....OOQOO......", "...OOOOQOOOO....", "..OOOOOQOOOOo...", "..OOOOOQOOOOo...", "..OOOOOOOOOOo...", ".oooooooooooooo.", "................", "................", "................", "................", "................", "................", "MMM.............", "MmM.............", ".W..............", ".W..VVVVVVVV....", ".W..............", ".W..............", ".W.....YY.......", ".W.............."]], "up": [4, ["......OOQOO.....", "....OOOOQOOOO...", "...OOOOOQOOOOo..", "...OOOOOQOOOOo..", "..OOOOOOOOOOOo..", ".oooooooooooooo.", "................", "................", "................", "................", "................", "................", "...DDDDDDDDDD.MM", "...DOOOOOOOOD.Mm", "...DDDDDDDDDD..W", "...DDDDDDDDDD..W", "...DDDDDDDDDD..W", "...............W", "...............W"]], "side": [4, [".....OOOQOO.....", "....OOOOQOOOO...", "...OOOOOQOOOOo..", "...OOOOOQOOOOo..", "...OOOOOOOOOOo..", "..ooooooooooooo.", "................", "................", "................", "................", "................", "................", "...DD...........", "..DDD...........", "..DOD.VVVV......", "..DDD...........", "..DDD...........", "...........MM...", ".........WWMm...", "................"]]}}, "grunt": {"body": "nox", "pal": {}, "over": {"down": [8, ["....A......A....", "....aA....Aa....", "................", "................", "................", "................", "................", "................", "................", "................", "................", "................", "......A...A.....", ".......A...A....", "........A...A..."]], "up": [8, ["....A......A....", "....aA....Aa....", "................", ".......AA.......", "......aAAa......", "................", ".......AA.......", "......aAAa......", "................", ".......AA.......", "......aAAa......"]], "side": [8, ["................", "...A..A.........", "..aAaaAa........", "................", "................", "................", "................", "................", "................", "................", "....A.A........."]]}}, "spitter": {"body": "nox", "pal": {"Q": "D8E03A", "q": "F6F9A8", "j": "8F9620"}, "over": {"down": [8, ["..........jQQj..", ".........jQqQQj.", ".........QQQQQj.", ".........jQQQj..", "................", "................", "................", "................", "................", ".......QQ.......", "........Q.......", "........Q......."]], "up": [11, ["................", ".....jQQQQj.....", "....jQqqQQQj....", "....QQqQQQQQ....", "....QQQQQQQQ....", "....jQQQQQQj....", ".....jQQQQj....."]], "side": [8, ["..jQQj..........", ".jQqQQj.........", ".QQQQQQ.........", ".jQQQQj.........", "..jQQj..........", "................", "................", "................", "..............Q.", "..............Q."]]}}, "wrecker": {"body": "wrecker", "pal": {"R": "6E5A7A", "r": "463650"}, "over": {}}}
static var BODIES = {"human": HUMAN, "nox": NOX, "wrecker": WRECKER_BODY}
static var VIEWS = ["side", "down", "side", "up", "down3q", "down3q", "up3q", "up3q"]
# rows drawn mirrored (facing left) and rows turned into a 3/4 view
static var MIRRORED = [2, 5, 6]
# the attack each enemy has (tools/PixelPoses.cs); players get one attack per weapon (PixelPoses.PLAYER_ATTACKS)
static var ENEMY_ATTACK = {"grunt": "claw", "spitter": "spit", "wrecker": "smash"}
const ENEMY_ATTACK_FPS: float = 10.0
const DOWN_FPS: float = 8.0

func _ready() -> void:
	var out = "res://art/chars"
	for output_argument in OS.get_cmdline_user_args():
		if output_argument.begins_with("--out="):
			out = output_argument.substr(6)
	DirAccess.make_dir_recursive_absolute(out)
	configure_enemy_designs()
	self.add_three_quarter_views()
	var info = {"frames": {"idle": [0, 2], "run": [2, 4]}, "fps": {"idle": 2.0, "run": 10.0}, "dirs": ["right", "down", "left", "up", "down_right", "down_left", "up_left", "up_right"], "chars": {}}
	for n in CHARACTERS:
		var name = n
		var body_name = CHARACTERS[n].body
		var body = BODIES[body_name]
		var size: int = int(body.size)
		var legs: int = int(body.legs)
		var feet: int = int(body.feet)
		var rx: float = float(body.shadow)
		# the body sits in the middle of a bigger cell, so a weapon at full reach still fits
		var cell: int = size + 2 * PixelPoses.M
		var ground: float = feet + PixelPoses.M + 1
		var player: bool = body_name == "human"
		var attacks = PixelPoses.PLAYER_ATTACKS if player else [ENEMY_ATTACK[name]]
		var down_first: int = 6 + PixelPoses.ATTACK_FRAMES * attacks.size()
		var gather_first: int = down_first + 4
		var cols: int = gather_first + (PixelPoses.GATHER_FRAMES * PixelPoses.GATHER_KINDS.size() if player else 0)
		var sheet = Image.create_empty(cell * cols, cell * 8, false, Image.FORMAT_RGBA8)
		var pal = self.palette(n)
		for row in range(8):
			var view = VIEWS[row]
			var base = self.compose(n, view)
			var frames = []
			# idle 0-1, walk 2-5: the body dips a pixel on each stride and rises as the legs pass (a light bounce)
			for f in [base, self.bob(base, body), self.bob(self.step(base, body, view, 1), body), base, self.bob(self.step(base, body, view, -1), body), base]:
				frames.append(PixelPoses.finish(PixelPoses.embed(raw(f), cell), ground, rx))
			# the attacks (4 frames each), then knocked down (4 frames, the last one lying with the eyes shut)
			var r = rig(body_name, view, pal)
			for kind in attacks:
				frames.append_array(PixelPoses.attack(kind, raw(base), view, r, legs, feet, cell, rx))
			var fallen = PixelPoses.fall(raw(base), raw(self.compose(n, view, true)), view, legs, feet, cell, rx)
			for _it1 in fallen:
				var im = _it1[0]
				var srx = _it1[1]
				frames.append(PixelPoses.finish(im, ground, srx))
			if player:
				for kind in PixelPoses.GATHER_KINDS:
					frames.append_array(PixelPoses.gather(kind, raw(self.compose(n, view, false, true)), view, r, legs, feet, cell, rx))
			for col in range(cols):
				var f = frames[col]
				if MIRRORED.find(row) >= 0:
					f.flip_x()
				sheet.blit_rect(f, Rect2i(0, 0, cell, cell), Vector2i(col * cell, row * cell))
		sheet.save_png(out.path_join(name + ".png"))
		# one-shot animations: [first column, frames, frames per second]
		var anims = {}
		for i in range(attacks.size()):
			var first: int = 6 + i * PixelPoses.ATTACK_FRAMES
			if player:
				anims["attack_" + attacks[i]] = [first, PixelPoses.ATTACK_FRAMES, PixelPoses.PLAYER_ATTACK_FPS[i]]
			else:
				anims["attack"] = [first, PixelPoses.ATTACK_FRAMES, ENEMY_ATTACK_FPS]
		anims["down"] = [down_first, 4, DOWN_FPS]
		if player:
			for i in range(PixelPoses.GATHER_KINDS.size()):
				anims["gather_" + PixelPoses.GATHER_KINDS[i]] = [gather_first + i * PixelPoses.GATHER_FRAMES, PixelPoses.GATHER_FRAMES, PixelPoses.GATHER_FPS]
		# size: the sheet cell; body: the drawn character (what the game scales to its size in tiles)
		info.chars[n] = {"size": cell, "body": size, "anchor": [cell / 2.0, ground], "anims": anims}
		print("drew ", n, " ", cell, "x", cell, ", ", cols, " columns")
	var fa = FileAccess.open(out.path_join("chars.json"), FileAccess.WRITE)
	fa.store_string(JSON.stringify(info, "\t"))
	fa.close()
	get_tree().quit()

static func raw(im) -> Variant:
	return im

# Where the arms are in each view (see PixelPoses.Rig), in body pixels. The weapon arm is the character's
# right: on the left of the screen seen from the front, in front of the body seen from the side.
static func rig(body_name, view, pal) -> Variant:
	var side: bool = view == "side"
	var front: bool = view.begins_with("down")
	var r
	var l: Vector2 = Vector2.ZERO
	var rr: Vector2 = Vector2.ZERO
	var el: Rect2i = Rect2i()
	var er: Rect2i = Rect2i()
	if body_name == "human":
		r = Cs.init(PixelPoses.Rig.new(), {"arm_w": 2, "hand_w": 2, "arm": _rig_c("c", pal), "hand": _rig_c("S", pal)})
		l = Vector2(11, 18)
		rr = Vector2(21, 18)
		el = Rect2i(10, 17, 2, 5)
		er = Rect2i(20, 17, 2, 5)
		if side:
			r.main = Vector2(15, 18)
			r.off = Vector2(16, 17.5)
			# the resting hand in front of the belly is painted over with the shirt
			r.patch = Rect2i(15, 21, 2, 1)
			r.patch_col = _rig_c("C", pal)
			return r
	elif body_name == "nox" or body_name == "spitter":
		r = Cs.init(PixelPoses.Rig.new(), {"arm_w": 3, "hand_w": 3, "reach": 1.1, "arm": _rig_c("N", pal), "hand": _rig_c("N", pal), "claw": _rig_c("A", pal)})
		l = Vector2(9.5, 19.5)
		rr = Vector2(23, 19.5)
		el = Rect2i(8, 19, 3, 6)
		er = Rect2i(22, 19, 2, 6)
		r.mouth = Vector2(21.5, 17.5) if side else Vector2(16, 17.5) if view == "down" else Vector2(18, 17.5) if view == "down3q" else Vector2(16, 13)
		if side:
			r.main = Vector2(21.5, 21.5)
			r.off = Vector2(19, 21)
			r.erase_main = Rect2i(20, 22, 3, 4)
			return r
	else:
		r = Cs.init(PixelPoses.Rig.new(), {"arm_w": 4, "hand_w": 4, "reach": 1.4, "arm": _rig_c("N", pal), "hand": _rig_c("N", pal), "claw": _rig_c("A", pal)})
		l = Vector2(11.5, 24)
		rr = Vector2(28.5, 24)
		el = Rect2i(10, 24, 3, 8)
		er = Rect2i(27, 24, 3, 8)
		if side:
			r.main = Vector2(25.5, 24)
			r.off = Vector2(22, 24)
			r.erase_main = Rect2i(24, 25, 4, 6)
			return r
	# from the front the weapon arm is on the screen's left; from the back, on its right
	r.main = l if front else rr
	r.erase_main = el if front else er
	r.off = rr if front else l
	r.erase_off = er if front else el
	return r

# local function C of the method above
static func _rig_c(k, pal) -> Color:
	return Color(pal[k])

func palette(n) -> Variant:
	var pal = {}
	for p in [SKIN_L, BOOTS, NOX_PAL, CHARACTERS[n].pal]:
		pal.merge(p, true)
	return pal

# Shut: the eyes closed (lying knocked down)
func compose(n, view, shut: bool = false, working: bool = false) -> Variant:
	var ch = CHARACTERS[n]
	var body = BODIES[ch.body]
	var size: int = int(body.size)
	var pal = self.palette(n)
	if shut:
		pal["E"] = pal["s"]
		pal["Y"] = pal["n"]
	var im = Image.create_empty(size, size, false, Image.FORMAT_RGBA8)
	var pad: int = int((size - body[view][1][0].length()) / 2)
	# gear is the same in a 3/4 view as in the front / back view
	var gear_view = view.replace("3q", "")
	for layer in [body[view], ch.over.get(gear_view, [0, []])]:
		var top: int = int(layer[0])
		for r in layer[1].size():
			var line = layer[1][r]
			for i in line.length():
				var k = line[i]
				if k == ".":
					continue
				# Stow the resting bow, hammer or vial while holding a harvesting tool.
				if working and (n == "scout" and (k == "W" or k == "w") or n == "engineer" and (k == "M" or k == "m" or k == "W") or n == "healer" and k == "Z"):
					continue
				if not pal.has(k):
					push_error("%s %s: no colour for '%s'" % [n, view, k])
					continue
				im.set_pixel(pad + i, top + r, Color(pal[k]))
	paint_enemy_details(n, view, raw(im), shut)
	return im

# 3/4 views facing right (the left ones are mirrored): the front / back pixel map with a few rows redrawn.
# The face slides one pixel toward the turn and the hair shows on the far side; both eyes keep the front
# view's size (2 x 2 for players, 2 or 3 wide for NOX), so they never look mismatched at this small size.
func add_three_quarter_views() -> void:
	turned(HUMAN, "down", {4: "..HHHHSSSSSSSh..", 5: "..HHHSSSSSSSSS..", 6: "..hHSSEESSEESS..", 7: "..hHSSEESSEESS..", 8: "..hhSSSSSSSSSs..", 9: "...hsSSSSSSSSs.."})
	turned(HUMAN, "up", {6: "..HHHHHHHHHHSs..", 7: "..hHHHHHHHHHSs..", 8: "..shHHHHHHHHSs.."})
	turned(NOX, "down", {4: ".MNNNYYNNNNYYNn.", 5: ".NNNNYYNNNNYYNn.", 7: ".nNNNNDTDTTDTDn."})
	turned(NOX, "up", {})
	turned(BODIES["spitter"], "down", {})
	turned(BODIES["spitter"], "up", {})
	turned(WRECKER_BODY, "down", {6: "..NNNNYYYNNNNYYYNn..", 8: "..nNNNNDTDTDTDTDNn.."})
	turned(WRECKER_BODY, "up", {})

static func turned(body, view, rows) -> void:
	var map = body[view]
	var lines = map[1].duplicate()
	for r in rows:
		lines[r] = rows[r]
	body[view + "3q"] = [map[0], lines]
	body.split[view + "3q"] = body.split[view]

func bob(src, body) -> Variant:
	# Idle breath: everything above the legs sinks one pixel.
	var im = src.duplicate()
	var size: int = int(body.size)
	var legs: int = int(body.legs)
	im.fill_rect(Rect2i(0, 0, size, legs), Color(0, 0, 0, 0))
	im.blit_rect(src, Rect2i(0, 0, size, legs), Vector2i(0, 1))
	return im

func step(src, body, view, side) -> Variant:
	# Walk pose. Front/back views: one foot lifts a pixel. Side view: the legs open into a stride (or cross).
	var im = src.duplicate()
	var size: int = int(body.size)
	var pad: int = (size - (16 if size == 32 else 20)) / 2
	var sp = body.split[view]
	var top: int = int(body.legs)
	var h: int = int(body.feet + 1 - top)
	# left / back leg
	var a = Rect2i(pad + sp[0], top, sp[1] - sp[0], h)
	# right / front leg
	var b = Rect2i(pad + sp[1], top, sp[2] - sp[1], h)
	im.fill_rect(a, Color(0, 0, 0, 0))
	im.fill_rect(b, Color(0, 0, 0, 0))
	if view == "side":
		im.blend_rect(src, a, a.position + Vector2i(-side, 0))
		im.blend_rect(src, b, b.position + Vector2i(side, 0))
	else:
		var lift = a if side > 0 else b
		var stay = b if side > 0 else a
		im.blend_rect(src, stay, stay.position)
		im.blend_rect(src, Rect2i(lift.position + Vector2i(0, 1), lift.size - Vector2i(0, 1)), lift.position)
	return im

# ---- tools/PixelEnemyDesigns.cs
# Shared by every idle, walking, attack and fallen frame through PixelChars.Compose.

static func configure_enemy_designs() -> void:
	CHARACTERS["grunt"].pal = {"N": "BA4E32", "n": "713025", "M": "ED8D48", "Y": "FFE6A0", "A": "F5D9AE", "a": "AF855C"}
	CHARACTERS["grunt"].over = {}
	CHARACTERS["spitter"].pal = {"N": "609942", "n": "304F31", "M": "A0C959", "Y": "FFE56B", "A": "DDD789", "a": "6B783A", "Q": "CFED49", "q": "F4FFAA", "j": "6C902F"}
	CHARACTERS["spitter"].over = {}
	CHARACTERS["wrecker"].pal = {"N": "577787", "n": "304551", "M": "8BAAB4", "Y": "FFCF65", "R": "809EAA", "r": "435E6D", "A": "C8D4CC", "a": "71858B"}
	# A narrow head, long throat and hunched abdomen replace the Grunt's round skull.
	var spitter = NOX.duplicate()
	spitter["down"] = [7, ["......MMMM......", ".....MNNNNn.....", ".....NYYNYN.....", ".....NNNNNn.....", ".....nDDDDn.....", "......NQNN......", "......nNNn......", "......nNNn......", ".....MNQNNn.....", "....MNNQNNNn....", "...MNNNNNNNNn...", "MNnNNNNNNNNNNNn", "MNn.nNNNNNNn.Nn", "MNn.nNNNNNNn.Nn", "NNn.nNNNNNNn.Nn", "Nn..nNNNNNNn.nn", "AA...nNnnNNn..AA", ".....nNn.nNn....", ".....nNn.nNn....", "....nnNn.nNnn...", "....nnnn.nnnn...", "................"]]
	spitter["up"] = [7, ["......MMMM......", ".....MNNNNn.....", ".....NNNNNn.....", ".....NNNNNn.....", ".....nNNNNn.....", "......NNNN......", "......nNNn......", "......nNNn......", ".....MNNNNn.....", "....MNNNNNNn....", "...MNNNNNNNNn...", "MNnNNNNNNNNNNNn", "MNn.nNNNNNNn.Nn", "MNn.nNNNNNNn.Nn", "NNn.nNNNNNNn.Nn", "Nn..nNNNNNNn.nn", "AA...nNnnNNn..AA", ".....nNn.nNn....", ".....nNn.nNn....", "....nnNn.nNnn...", "....nnnn.nnnn...", "................"]]
	spitter["side"] = [7, [".......MMMM.....", "......MNNNNn....", "......NNNYNn....", "......NNNNNNnn..", "......nNNNDDDDn.", ".......nNNn.....", ".......nNNn.....", ".......nNNn.....", "......MNNNn.....", ".....MNNNNn.....", "....MNNNNNNn....", "...MNNNNNNNNNn..", "...nNNNNNNnNNn..", "...nNNNNNn.nNn..", "...nNNnnNn.nNn..", "...nNn..nNn.AAA.", "...nNn..nNn.....", "...nNn..nNn.....", "...nNn..nNn.....", "..nnNn.nnNn.....", "..nnnn.nnnn.....", "................"]]
	BODIES["spitter"] = spitter
	CHARACTERS["spitter"].body = "spitter"

static func paint_enemy_details(name, view, im, shut: bool) -> void:
	if name != "grunt" and name != "spitter" and name != "wrecker":
		return
	var side: bool = view == "side"
	var back: bool = view.begins_with("up")
	if name == "grunt":
		# Swept-back bony crest and exposed ribs: a fast, lean claw fighter.
		for i in range(3):
			var x: int = 10 + i * 3 if side else 11 + i * 4
			_paint_enemy_details_box(x, 7 + i % 2, 2, 5, "F5D9AE", im)
			_paint_enemy_details_box(x, 6 + i % 2, 1, 2, "FFE8C2", im)
		for i in range(3):
			_paint_enemy_details_box(15 if side else 13, 20 + i * 2, 4 if side else 7, 1, "ED8D48" if back else "E6AB72", im)
	elif name == "spitter":
		# Large segmented acid sacs have their own silhouette, visible from every direction.
		for p in [Vector2i(10, 16), Vector2i(9, 20)] if side else [Vector2i(10, 17), Vector2i(21, 17)]:
			_paint_enemy_details_box(p.x - 2, p.y - 2, 5, 5, "6C902F", im)
			_paint_enemy_details_box(p.x - 1, p.y - 3, 3, 7, "CFED49", im)
			_paint_enemy_details_box(p.x - 1, p.y - 2, 2, 2, "F4FFAA", im)
			_paint_enemy_details_box(p.x - 2, p.y + 1, 5, 1, "8AAA36", im)
		if not back:
			_paint_enemy_details_box(24 if side else 16, 13, 1, 3, "CFED49", im)
	else:
		# Square shoulder slabs and cracked breastplate distinguish the heavy demolisher.
		for x in [23] if side else [8, 27]:
			_paint_enemy_details_box(x, 20, 6, 7, "435E6D", im)
			_paint_enemy_details_box(x, 19, 6, 2, "C8D4CC", im)
			_paint_enemy_details_box(x, 21, 2, 4, "9EB8BF", im)
		_paint_enemy_details_box(22 if side else 16, 25, 3 if side else 8, 5, "435E6D", im)
		_paint_enemy_details_box(23 if side else 18, 25, 1, 3, "C8D4CC", im)
		_paint_enemy_details_box(24 if side else 19, 28, 2, 1, "C8D4CC", im)

# local function Box of the method above
static func _paint_enemy_details_box(x: int, y: int, w: int, h: int, hex, im) -> void:
	im.fill_rect(Rect2i(x, y, w, h), Color(hex))
