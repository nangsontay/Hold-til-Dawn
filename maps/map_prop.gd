@tool
extends Sprite2D
## Position is the foot/growth point, not the centre of the image.
@export var solid_radius: float = 20.0
@export var blocks_attacks: bool = true


func _ready() -> void:
	texture_changed.connect(_align_feet)
	_align_feet()


func _align_feet() -> void:
	if texture == null: return
	var index: Dictionary = JSON.parse_string(FileAccess.get_file_as_string("res://art/nature/index.json"))
	var name_key := texture.resource_path.get_file().get_basename()
	if index.has(name_key):
		centered = false
		offset = -Vector2(index[name_key][0], index[name_key][1])
