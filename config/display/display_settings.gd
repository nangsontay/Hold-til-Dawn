class_name DisplaySettings
extends Resource
## Camera and display tuning (client only). Owner: Mai Thien Ngon. Edit config/display/display.tres in the Godot Inspector; restart the game after a change.
static var _current = null

## The shared settings file, loaded once (defaults when the file is missing).
static func get_current():
	var _v1 = load("res://config/display/display.tres") as DisplaySettings
	if _current == null: _current = _v1 if _v1 != null else DisplaySettings.new()
	return _current

@export var camera_dead_zone: Vector2 = Vector2(30, 18)

@export var camera_follow_rate: float = 8

## Most frames drawn per second (0 = one per screen refresh). Without a cap a 120-165 Hz laptop screen keeps the CPU and GPU flat out all match long.
@export var max_fps: int = 60
