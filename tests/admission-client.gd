extends SceneTree
## Real ENet client driven by tests/match-worker-integration.py. Credentials arrive through the environment.

class ObservedWorker extends "res://main.gd":
	func _physics_process(dt: float) -> void:
		var before := multiplayer.get_peers()
		super._physics_process(dt)
		var after := multiplayer.get_peers()
		for peer in before:
			if peer not in after:
				print("WORKER peer_removed ", peer)


class Client extends "res://main.gd":
	var config: Dictionary
	var packets := 0
	var raw_packets := 0
	var pending_challenge := []
	var time := 0.0
	var last_state := ""
	var vote_step := ""
	var connected_at := 0.0
	var sample_at := 0.0
	var move_checked := false
	var leaving := false

	func _ready() -> void:
		multiplayer.connected_to_server.connect(_on_connected)
		multiplayer.connection_failed.connect(_disconnect_result)
		multiplayer.server_disconnected.connect(_disconnect_result)
		if config.kind in ["silent", "legacy"]:
			var peer := ENetMultiplayerPeer.new()
			if peer.create_client("127.0.0.1", int(config.port)) != OK:
				get_tree().quit(1)
			multiplayer.multiplayer_peer = peer
		elif join_managed("127.0.0.1", int(config.port), config.room, config.generation, config.ticket, config.secret) != OK:
			get_tree().quit(1)

	func _on_connected() -> void:
		print("CLIENT connected ", multiplayer.get_unique_id())
		connected_at = time
		if config.kind == "silent":
			c_join.rpc_id(1, "Unadmitted", 2)
			c_action.rpc_id(1, "mode", 2)
			c_action.rpc_id(1, "ready", true)
			c_action.rpc_id(1, "start", 0)
			c_input.rpc_id(1, Vector2(500, 500), 0.0, true, true, true, 0, 1)
		elif config.kind == "wrong_content":
			c_admission_begin.rpc_id(1, MatchWorker.PROTOCOL, "other-map", admission.ticket, admission.nonce)
		else:
			super._on_connected()

	func _on_admission_result(code: String) -> void:
		print("CLIENT result ", code)
		if config.expected != null:
			get_tree().quit(int(code != config.expected or raw_packets != 0))
		elif code != "admitted":
			get_tree().quit(1)
		else:
			super._on_admission_result(code)

	func _disconnect_result() -> void:
		if leaving:
			return
		var good: bool = config.kind == "silent" and raw_packets == 0 and time - connected_at >= 4.8 and time - connected_at <= 5.6
		if good:
			print("CLIENT preadmission_timeout")
		get_tree().quit(int(not good))

	func _physics_process(dt: float) -> void:
		time += dt
		if not pending_challenge.is_empty() and FileAccess.file_exists(config.proofFile):
			super._on_admission_challenge(pending_challenge[0], pending_challenge[1], pending_challenge[2])
			pending_challenge.clear()
		if config.has("leaveFile") and FileAccess.file_exists(config.leaveFile):
			leaving = true
			multiplayer.multiplayer_peer.close()
			get_tree().quit(0)
			set_physics_process(false)
			return
		if time >= 90:
			get_tree().quit(1)
		var me: Array = snap.get("ps", {}).get(my_id, [])
		if not me.is_empty() and snap.room.state in ["playing", "timeout"] and frame % 3 == 0:
			input_sequence += 1
			var target: Vector2 = me[2]
			if not move_checked:
				target += Vector2.RIGHT * 900
			c_input.rpc_id(1, target, 0.0, false, false, false, me[15], input_sequence)
		frame += 1

	func _on_admission_challenge(room: String, generation: String, challenge: String) -> void:
		if config.has("proofFile"):
			pending_challenge = [room, generation, challenge]
			print("CLIENT proof_paused")
		else:
			super._on_admission_challenge(room, generation, challenge)

	@rpc("authority", "call_remote", "unreliable_ordered")
	func s_snap(data: Dictionary) -> void:
		raw_packets += 1
		super.s_snap(data)

	func _on_snap(data: Dictionary) -> void:
		if leaving:
			return
		packets += 1
		if config.kind == "legacy":
			if not data.ps.has(my_id):
				return
			if data.ph == "lobby":
				c_action.rpc_id(1, "add_bot", 0)
				c_action.rpc_id(1, "start", 0)
			elif data.ph == "day":
				print("CLIENT legacy_started")
				get_tree().quit(int(data.ps.size() != 2 or data.has("room")))
			return
		if config.kind not in ["play", "idle"] or not data.ps.has(my_id) or data.mode != 1 or data.ps.size() > 5:
			get_tree().quit(1)
			return
		var room: Dictionary = data.room
		var current := str(room.state) + "/" + str(room.timeout)
		if current != last_state:
			print("CLIENT state ", current)
			last_state = current
		if config.kind == "play" and room.state == "waiting":
			if not room.ready.has(my_id):
				c_action.rpc_id(1, "ready", true)
			if data.leader == my_id and room.ready.size() == data.ps.size() and data.ps.size() >= 3:
				c_action.rpc_id(1, "start", 0)
		if room.state == "timeout" and room.timeout != vote_step:
			vote_step = room.timeout
			if room.timeout in ["continue", "bot"]:
				c_action.rpc_id(1, "timeout_vote", [room.timeout, room.timeout == "continue"])
		if room.state == "playing" and time >= sample_at:
			print("CLIENT sample %d %.3f %d" % [data.seed, data.t, packets])
			sample_at = time + 0.5
		var previous: Array = snap.get("ps", {}).get(my_id, [])
		if not move_checked and room.state == "playing" and not previous.is_empty() and previous[15] == data.ps[my_id][15] and data.ps[my_id][24] > 0:
			var travel: float = previous[2].distance_to(data.ps[my_id][2])
			move_checked = true
			if travel >= 900.0:
				get_tree().quit(1)
			else:
				print("CLIENT movement_validated")
		snap = data


func _initialize() -> void:
	if "--match-worker" in OS.get_cmdline_user_args():
		var worker := ObservedWorker.new()
		worker.name = "Main"
		root.add_child.call_deferred(worker)
		return
	var config: Variant = JSON.parse_string(OS.get_environment("ADMISSION_TEST_CONFIG"))
	if not config is Dictionary:
		quit(1)
		return
	var client := Client.new()
	client.config = config
	client.name = "Main"
	root.add_child.call_deferred(client)
