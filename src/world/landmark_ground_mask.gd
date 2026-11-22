class_name LandmarkGroundMask
extends RefCounted
# Static terrain queries for a small decoration patch. Keep only nearby route segments, instead of testing
# every route on the island for each painted pixel. This never changes gameplay collision or navigation.
var world
var segments = []

var segment_count: int:
	get:
		return segments.size()

func _init(world_, area: Rect2, max_margin: float = 8) -> void:
	self.world = world_
	for _it1 in world_.trails:
		var path = _it1[0]
		var width = _it1[1]
		var i: int = 0
		while i < path.size() - 1:
			var a: Vector2 = path[i]
			var b: Vector2 = path[i + 1]
			var delta: Vector2 = b - a
			var bounds: Rect2 = Rect2(a.min(b), delta.abs()).grow(width / 2 + max_margin)
			if bounds.intersects(area):
				segments.append([a, delta, maxf(delta.length_squared(), 0.001), width / 2])
			i += 1

# Same accepted terrain types as TerrainAt(P, Margin) is 0 or 4 or 9, within the constructor's area.
func can_paint(p: Vector2, margin: float, allow_beach: bool = false, preserve_trails: bool = true) -> bool:
	if not IslandMap.on_land(p) or IslandMap.in_river(p, IslandMap.RIVER_BANK + margin):
		return false
	if not allow_beach and not IslandMap.on_grass(p, margin):
		return false
	for _it1 in world.floors:
		var floor_ = _it1[0]
		if Cs.has_point(floor_.grow(margin), p):
			return false
	for pond in world.ponds:
		if p.distance_to(pond.centre) < pond.radius * 1.6 + margin and IslandMap.in_polygon(pond.edge, p):
			return false
	# Hidden routes should not leave grass-coloured slashes across a landmark's soil.
	if not preserve_trails:
		return true
	for _it2 in segments:
		var a = _it2[0]
		var delta = _it2[1]
		var length2 = _it2[2]
		var half_width = _it2[3]
		var t: float = clampf((p - a).dot(delta) / length2, 0, 1)
		var reach: float = half_width + margin
		if p.distance_squared_to(a + delta * t) < reach * reach:
			return false
	return true
