class_name GameplaySettings
extends Resource
## Match rules tuning (day, dusk). Owner: Pham Minh Hieu. Edit config/gameplay/gameplay.tres in the Godot Inspector; restart the game after a change.
static var _current = null

## The shared settings file, loaded once (defaults when the file is missing).
static func get_current():
	var _v1 = load("res://config/gameplay/gameplay.tres") as GameplaySettings
	if _current == null: _current = _v1 if _v1 != null else GameplaySettings.new()
	return _current

@export var day_min: float = 180

# FML-14: skip vote counts only after 180 s of day
@export var dusk_seconds: float = 15
