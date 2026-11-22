extends RefCounted
## Shared world data. No rendering or SceneTree is needed on the dedicated server.
const T := 32.0
const ISLAND := 72.0 * T
const HALF := Vector2(5.5, 3.5) * T
const SAFE_D := 20.0 * T
const CELL := 128.0
const COLD := Color(0.8, 0.88, 0.92)
const LAYOUT = preload("res://maps/island.tscn")
var deco: Array = []
var scatter: Array = []
var solids: Array = []  # [centre, radius, blocks_attacks]
var static_solids: Array = []
var buckets := {}
var grids := {}
var paths := {}
var revision := 0


func _init() -> void:
	generate()


static func wall_dist(p: Vector2) -> float:
	return Vector2(maxf(absf(p.x) - HALF.x, 0), maxf(absf(p.y) - HALF.y, 0)).length()


static func lane(p: Vector2) -> bool:
	return (absf(p.y) < 2.5 * T and absf(p.x) > HALF.x) or (absf(p.x) < 2.5 * T and p.y < -HALF.y)


func generate() -> void:
	deco.clear()
	scatter.clear()
	var random := RandomNumberGenerator.new()
	random.seed = 7
	var pines := ["Pine_1", "Pine_2", "Pine_3", "Pine_4", "Pine_5"]
	var dead := ["DeadTree_1", "DeadTree_2", "DeadTree_3", "DeadTree_4", "DeadTree_5"]
	var rocks := ["Rock_Medium_1", "Rock_Medium_2", "Rock_Medium_3"]
	var variants: Dictionary = JSON.parse_string(FileAccess.get_file_as_string("res://art/nature/index.json"))
	var names := {}
	for n: String in variants:
		var base := n.rsplit("_", true, 1)
		names[base[0]] = maxi(names.get(base[0], 0), base[1].to_int() + 1)
	var variant := func(n: String, k: int) -> String: return "%s_%d" % [n, absi(k) % maxi(1, names.get(n, 1))]
	var outer := func(p: Vector2) -> bool: return wall_dist(p) > SAFE_D + 2 * T and not lane(p) and p.length() < ISLAND - 2 * T
	var ring := func() -> Vector2: return Vector2.from_angle(random.randf() * TAU) * random.randf_range(HALF.x + SAFE_D, ISLAND - 4 * T)
	for c in 44:
		var centre: Vector2 = ring.call()
		for k in random.randi_range(4, 10):
			var p := centre + Vector2.from_angle(random.randf() * TAU) * random.randf_range(0, 5.5 * T)
			if outer.call(p):
				var pool: Array = dead if random.randf() < 0.2 else pines
				deco.append([p, variant.call(pool[random.randi() % pool.size()], random.randi()), random.randf_range(0.8, 1.2), COLD])
	for c in 24:
		var centre: Vector2 = ring.call()
		for k in random.randi_range(3, 6):
			var p := centre + Vector2.from_angle(random.randf() * TAU) * random.randf_range(0, 2.2 * T)
			if outer.call(p):
				deco.append([p, variant.call(rocks[random.randi() % rocks.size()], random.randi()), random.randf_range(0.8, 1.6), COLD])
	for i in 40:
		var p: Vector2 = ring.call()
		if outer.call(p):
			deco.append([p, variant.call(dead[random.randi() % dead.size()], random.randi()), random.randf_range(0.8, 1.1), COLD])
	for i in 400:
		var p := Vector2.from_angle(random.randf() * TAU) * ISLAND * sqrt(random.randf())
		var wd := wall_dist(p)
		if wd > 3 * T and wd < SAFE_D and not lane(p) and random.randf() < 0.12:
			deco.append([p, "Bush_Common_0", random.randf_range(0.6, 0.9), COLD])
	var flat := ["Grass_Wispy_Short", "Grass_Wispy_Tall", "Grass_Common_Short", "Grass_Wispy_Short", "Grass_Wispy_Tall", "Pebble_Round_1", "Pebble_Round_3", "Pebble_Square_2", "Pebble_Square_4", "Pebble_Round_5"]
	while scatter.size() < 1400:
		var p := Vector2.from_angle(random.randf() * TAU) * ISLAND * sqrt(random.randf())
		if absf(p.x) > HALF.x + 2.6 * T or absf(p.y) > HALF.y + 2.6 * T:
			scatter.append([p, variant.call(flat[random.randi() % flat.size()], random.randi()), random.randf_range(0.8, 1.2)])
	# Hand-placed objects are editable with Godot's 2D editor.
	var layout := LAYOUT.instantiate()
	static_solids.clear()
	for d in deco:
		if not d[1].begins_with("Bush"):
			static_solids.append([d[0], (0.75 if d[1].begins_with("Rock") else 0.3) * T * d[2], true])
	for child in layout.get_children():
		if child is Sprite2D and child.texture != null:
			deco.append([child.position, child.texture.resource_path.get_file().get_basename(), child.scale.x, child.modulate])
			if child.solid_radius > 0:
				static_solids.append([child.position, child.solid_radius * absf(child.scale.x), child.blocks_attacks])
	layout.free()
	static_solids.append([Vector2(0, 0.3 * T - 6), 22.0, true])
	for x in [-10.0, 0.0, 10.0]:
		static_solids.append([Vector2(x, 1.6 * T - 4), 7.0, true])
	scatter.sort_custom(func(a, b): return a[0].y < b[0].y)
	rebuild([], [])


func rebuild(nodes: Array, towers: Array) -> void:
	var next: Array = static_solids.duplicate()
	for nd in nodes:
		var r: float = [0.25, 0.62, 0.0, 0.6][nd.type] * T
		if nd.amount > 0 and r > 0:
			next.append([nd.pos, r, true])
	for tw in towers:
		if tw != null:
			next.append([tw.pos, 0.7 * T, true])
	if next == solids:
		return
	solids = next
	buckets.clear()
	grids.clear()
	paths.clear()
	revision += 1
	for i in solids.size():
		var s: Array = solids[i]
		var lo := Vector2i((s[0] - Vector2.ONE * s[1]) / CELL)
		var hi := Vector2i((s[0] + Vector2.ONE * s[1]) / CELL)
		# floor is essential for negative world coordinates.
		lo = Vector2i(((s[0] - Vector2.ONE * s[1]) / CELL).floor())
		hi = Vector2i(((s[0] + Vector2.ONE * s[1]) / CELL).floor())
		for y in range(lo.y, hi.y + 1):
			for x in range(lo.x, hi.x + 1):
				var key := Vector2i(x, y)
				if not buckets.has(key): buckets[key] = []
				buckets[key].append(i)


func candidates(a: Vector2, b: Vector2, radius: float) -> Array:
	var lo := Vector2i(((a.min(b) - Vector2.ONE * radius) / CELL).floor())
	var hi := Vector2i(((a.max(b) + Vector2.ONE * radius) / CELL).floor())
	var found := {}
	for y in range(lo.y, hi.y + 1):
		for x in range(lo.x, hi.x + 1):
			for i in buckets.get(Vector2i(x, y), []): found[i] = true
	return found.keys()


func resolve(pos: Vector2, radius: float) -> Vector2:
	var nearby := candidates(pos, pos, radius + 32.0)
	for pass_index in 3:
		for i in nearby:
			var s: Array = solids[i]
			var d: Vector2 = pos - s[0]
			var need: float = radius + s[1]
			if d.length_squared() < need * need:
				pos = s[0] + (d.normalized() if d.length_squared() > 0.0001 else Vector2.DOWN) * need
	return pos


func move_body(from: Vector2, to: Vector2, radius: float, walls: Array) -> Vector2:
	var count := maxi(1, ceili(from.distance_to(to) / 4.0))
	var step := (to - from) / count
	var pos := from
	for i in count:
		var next := resolve(pos + step, radius)
		for w: Rect2 in walls:
			var q := next.clamp(w.position, w.end)
			var d := next - q
			if d.length_squared() < radius * radius and d.length_squared() > 0.000001:
				next = q + d.normalized() * radius
		pos = next
	return pos


static func circle_hit(a: Vector2, b: Vector2, centre: Vector2, radius: float) -> float:
	## Fraction of a finite segment at first contact; INF means no contact.
	var start := a - centre
	if start.length_squared() <= radius * radius: return 0.0
	var step := b - a
	var length2 := step.length_squared()
	if length2 < 0.000001: return INF
	var dot := start.dot(step)
	var disc := dot * dot - length2 * (start.length_squared() - radius * radius)
	if disc < 0: return INF
	var hit := (-dot - sqrt(disc)) / length2
	return hit if hit >= 0.0 and hit <= 1.0 else INF


func first_hit(a: Vector2, b: Vector2, radius := 0.0, attacks := false, ignore := Vector2.INF) -> float:
	var best := INF
	for i in candidates(a, b, radius):
		var s: Array = solids[i]
		if s[1] <= 0 or s[0].is_equal_approx(ignore) or (attacks and not s[2]): continue
		best = minf(best, circle_hit(a, b, s[0], s[1] + radius))
	return best


func waypoint(from: Vector2, to: Vector2, radius: float, walls: Array) -> Vector2:
	# A resource/tower is a valid destination: walk up to its surface, not its centre.
	if first_hit(from, to, radius, false, to) == INF and not walls.any(func(w): return segment_box(from, to, w.grow(radius)) < INF):
		return to
	var key := "%d:%s" % [roundi(radius * 10), str(walls)]
	var grid: AStarGrid2D = grids.get(key)
	if grid == null:
		grid = AStarGrid2D.new()
		grid.region = Rect2i(-73, -73, 147, 147)
		grid.cell_size = Vector2.ONE * T
		grid.diagonal_mode = AStarGrid2D.DIAGONAL_MODE_ONLY_IF_NO_OBSTACLES
		grid.update()
		for s in solids:
			var reach: float = s[1] + radius + 1.0
			var lo := Vector2i(((s[0] - Vector2.ONE * reach) / T).floor())
			var hi := Vector2i(((s[0] + Vector2.ONE * reach) / T).ceil())
			for y in range(lo.y, hi.y + 1):
				for x in range(lo.x, hi.x + 1):
					var cell := Vector2i(x, y)
					if grid.is_in_boundsv(cell) and (Vector2(cell) * T).distance_to(s[0]) < reach:
						grid.set_point_solid(cell)
		for w: Rect2 in walls:
			var box := w.grow(radius + 1)
			var lo := Vector2i((box.position / T).floor())
			var hi := Vector2i((box.end / T).ceil())
			for y in range(lo.y, hi.y + 1):
				for x in range(lo.x, hi.x + 1):
					var cell := Vector2i(x, y)
					if grid.is_in_boundsv(cell) and box.has_point(Vector2(cell) * T): grid.set_point_solid(cell)
		grids[key] = grid
	var start := _open_cell(grid, from)
	var end := _open_cell(grid, to)
	var path_key := [key, start, end]
	var path: Array = paths.get(path_key, [])
	if path.is_empty():
		path = grid.get_id_path(start, end, true)
		if paths.size() > 1024: paths.clear()
		paths[path_key] = path
	for j in range(mini(path.size() - 1, 5), -1, -1):
		var p := Vector2(path[j]) * T
		if from.distance_to(p) < 2: continue
		if first_hit(from, p, radius) == INF and not walls.any(func(w): return segment_box(from, p, w.grow(radius)) < INF): return p
	return from


func _open_cell(grid: AStarGrid2D, pos: Vector2) -> Vector2i:
	var cell := Vector2i((pos / T).round())
	for ring in 7:
		var best := cell
		var distance := INF
		for y in range(-ring, ring + 1):
			for x in range(-ring, ring + 1):
				var c := cell + Vector2i(x, y)
				if grid.is_in_boundsv(c) and not grid.is_point_solid(c):
					var d := (Vector2(c) * T).distance_squared_to(pos)
					if d < distance:
						distance = d
						best = c
		if distance < INF: return best
	return cell


static func segment_box(a: Vector2, b: Vector2, box: Rect2) -> float:
	var enter := 0.0
	var leave := 1.0
	var step := b - a
	for axis in 2:
		if absf(step[axis]) < 0.00001:
			if a[axis] < box.position[axis] or a[axis] > box.end[axis]: return INF
		else:
			var t0: float = (box.position[axis] - a[axis]) / step[axis]
			var t1: float = (box.end[axis] - a[axis]) / step[axis]
			enter = maxf(enter, minf(t0, t1))
			leave = minf(leave, maxf(t0, t1))
			if enter > leave: return INF
	return enter
