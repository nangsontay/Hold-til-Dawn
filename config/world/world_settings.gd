class_name WorldSettings
extends Resource
## Island and resource tuning. Owner: Vu Duc Viet Anh. Edit config/world/world.tres in the Godot Inspector; restart the game after a change.
static var _current = null

## The shared settings file, loaded once (defaults when the file is missing).
static func get_current():
	var _v1 = load("res://config/world/world.tres") as WorldSettings
	if _current == null: _current = _v1 if _v1 != null else WorldSettings.new()
	return _current

@export var node_multiplier: int = 1
