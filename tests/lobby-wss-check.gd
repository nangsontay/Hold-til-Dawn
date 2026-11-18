extends SceneTree

const Lobby = preload("res://multiplayer/lobby-client.gd")

var directory: Lobby


func _initialize() -> void:
	directory = Lobby.new()
	directory.changed.connect(_check_state)
	directory.open(OS.get_environment("WSS_TEST_URL"))
	if directory._room:
		directory._room.error.connect(_print_sdk_error)
	process_frame.connect(func(): directory.tick(Time.get_ticks_msec()))


func _print_sdk_error(code: int, reason: String) -> void:
	print("SDK error ", code, ": ", reason)


func _check_state() -> void:
	if directory.state not in [Lobby.State.ONLINE, Lobby.State.OFFLINE]:
		return
	var state := directory.state
	print("WSS directory state ", Lobby.State.keys()[state])
	directory.close()
	quit(int(state != Lobby.State.ONLINE))
