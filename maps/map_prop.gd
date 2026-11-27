@tool
class_name MapProp
extends Sprite2D
## Position is the ground anchor of the nature sprite.

@export var solid_radius: float = 20

@export var blocks_attacks: bool = true

func _ready() -> void:
	texture_changed.connect(align_feet)
	align_feet()

func align_feet() -> void:
	if texture == null:
		return
	var index = JSON.parse_string(FileAccess.get_file_as_string("res://art/nature/index.json"))
	var key = texture.resource_path.get_file().get_basename()
	if not index.has(key):
		return
	var anchor = index[key]
	centered = false
	offset = -Vector2(float(anchor[0]), float(anchor[1]))
