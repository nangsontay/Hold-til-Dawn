class_name AiSettings
extends Resource
## Enemy, wave, roamer and drop tuning. Owner: Vu Duc Viet Anh. Edit config/ai/ai.tres in the Godot Inspector; restart the game after a change.
static var _current = null

## The shared settings file, loaded once (defaults when the file is missing).
static func get_current():
	var _v1 = load("res://config/ai/ai.tres") as AiSettings
	if _current == null: _current = _v1 if _v1 != null else AiSettings.new()
	return _current

@export var enemy_speed: float = 1

# multiplier on BAL-29..32 speeds (GDD: 1)
@export var first_wave: float = 15

# first arrival on nights 3-5; nights 1-2 ease in later
@export var wave_interval: float = 45

# night-3 interval; early nights slower, night 4 slightly faster
@export var final_wave_interval: float = 30

# night 5
@export var spawn_multiplier: float = 1

# multiplier on the FML-04 budget (GDD: 1)
@export var roamer_multiplier: float = 1

# multiplier on the FML-16 roamer chance (GDD: 1)
@export var drop_multiplier: float = 1

# multiplier on BAL-41 drop chances (GDD: 1)
@export var early_night_first_wave: float = 20

@export var early_night_wave_interval: float = 60

@export var spawn_spacing: float = 2

# night 1; decreases to 0.5 s on night 5
@export var max_night_enemies: int = 40
