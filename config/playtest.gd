extends Resource
## Edit config/playtest.tres in the Inspector; the same values are used by every peer.
@export var day_min: float = 0.0
@export var dusk_seconds: float = 5.0
@export var enemy_speed: float = 0.5
@export var first_wave: float = 5.0
@export var wave_interval: float = 30.0
@export var final_wave_interval: float = 15.0
@export var spawn_multiplier: float = 3.0
@export var roamer_multiplier: float = 2.0
@export var drop_multiplier: float = 3.0
@export var node_multiplier: int = 2
@export var camera_dead_zone: Vector2 = Vector2(30, 18)
@export var camera_follow_rate: float = 8.0
