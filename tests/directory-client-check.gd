extends SceneTree

const SDK = preload("res://addons/colyseus/colyseus.gd")
var client
var room
var received := false

func _initialize() -> void:
	client = SDK.Client.new(OS.get_environment("DIRECTORY_TEST_URL"))
	room = client.join_or_create("directory")
	if room == null:
		quit(1)
		return
	create_timer(8.0).timeout.connect(func(): quit(1))
	room.error.connect(func(_code, _message): quit(1))
	room.joined.connect(func(): room.send_message("list", {}))
	room.message_received.connect(_on_message)

func _on_message(kind: Variant, data: Variant) -> void:
	if not received and kind == "rooms" and data is Dictionary and data.get("rooms") is Array and data.rooms.size() >= 2:
		for entry in data.rooms:
			if not entry is Dictionary or not entry.has("joinable") or entry.has("secret") or entry.has("ticketId"):
				quit(1)
				return
		received = true
		print("Godot Colyseus SDK received the room directory")
		room.left.connect(func(_code, _reason): quit(0))
		room.leave()
