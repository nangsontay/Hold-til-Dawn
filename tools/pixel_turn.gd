class_name PixelTurn
extends RefCounted
# Used by tools/PixelBosses.cs for the diagonal (3/4) views of the boss sheets (the players and enemies of
# tools/PixelChars.cs have hand-written 3/4 pixel maps instead).
# A front or back view is turned toward the right by taking a few pixel columns out of its far (left) half:
# the body narrows on the side that turns away and the face slides toward the right, which is how a 3/4 view is
# usually drawn in pixel art. The left diagonals are this, mirrored.

# Cx: the body's centre column; Offsets: how far left of Cx each dropped column is; rows above HeadBottom
# (the head) move one more pixel right, since a head turns further than the body.
# Eye: pixels of this colour are never dropped. In a row where a dropped column would cut an eye, the nearest
# column beside it is dropped instead, so both eyes keep the front view's size (a 1-pixel far eye next to a
# 2-pixel near eye looks like a mistake at this size, not like perspective).
static func turn_right(src, cx: int, offsets, head_bottom: int = 0, eye = null) -> Variant:
	var w: int = src.get_width()
	var h: int = src.get_height()
	var out = Image.create_empty(w, h, false, Image.FORMAT_RGBA8)
	for y in range(h):
		var drop = {}
		for o in offsets:
			var c: int = cx - o
			if _turn_right_is_eye(c, y, eye, src, w):
				# nearest free column on either side that is not part of an eye
				for d in range(1, w):
					if c - d >= 0 and not _turn_right_is_eye(c - d, y, eye, src, w) and not drop.has(c - d):
						c -= d
						break
					if c + d < cx and not _turn_right_is_eye(c + d, y, eye, src, w) and not drop.has(c + d):
						c += d
						break
			Cs.set_add(drop, c)
		for x in range(w):
			if drop.has(x):
				continue
			# every dropped column to the right of this one moves it one pixel right
			var shift: int = 0
			for d in drop.keys():
				if d > x:
					shift += 1
			var to: int = x + shift + (1 if y < head_bottom else 0)
			if to >= w:
				continue
			var col: Color = src.get_pixel(x, y)
			if col.a > 0:
				out.set_pixel(to, y, col)
	return out

# local function IsEye of the method above
static func _turn_right_is_eye(x: int, y: int, eye, src, w) -> bool:
	return eye != null and x >= 0 and x < w and src.get_pixel(x, y).is_equal_approx(eye)
