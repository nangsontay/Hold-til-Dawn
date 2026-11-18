extends SceneTree
## Run via tests/hosted-client-integration.py.

class Client extends "res://main.gd":
	var callsign: String
	var initial_role: int
	var assignment_behavior := ""
	var assignments := 0
	var assigned_port := 0
	var proof_held := false
	var send_inputs := true

	func _ready() -> void:
		super._ready()
		for skin in ROLE_SKIN:
			csheet[skin] = load(CHARS + skin + ".png")
		_build_ui()
		name_edit.text = callsign
		role_opt.selected = initial_role
		_open_directory()

	func _on_assignment(ticket: Dictionary) -> void:
		assignments += 1
		assigned_port = int(ticket.udpEndpoint.port)
		# An absolute timestamp older than any valid client clock must not shorten the TTL.
		ticket.expiresAt = 1
		if assignment_behavior == "invalid":
			var secret: String = ticket.secret
			ticket.secret = ("a" if secret[0] != "a" else "b") + secret.substr(1)
		elif assignment_behavior == "blocked":
			ticket.udpEndpoint.port = 7780
		super._on_assignment(ticket)
		if assignment_behavior == "cancel":
			assert(connection == Connection.CONNECTING)
			_back_to_menu("Join cancelled")

	func _on_admission_challenge(room: String, generation: String, challenge: String) -> void:
		if assignment_behavior == "hold":
			proof_held = true
		else:
			super._on_admission_challenge(room, generation, challenge)

	func _move(dt: float) -> void:
		if send_inputs:
			super._move(dt)


func _initialize() -> void:
	_run.call_deferred()


func _client(callsign: String, role := 0) -> Client:
	var branch := Node.new()
	branch.name = callsign
	root.add_child(branch)
	set_multiplayer(SceneMultiplayer.new(), branch.get_path())
	var client := Client.new()
	client.name = "Main"
	client.callsign = callsign
	client.initial_role = role
	branch.add_child(client)
	return client


func _wait_for(label: String, condition: Callable, seconds := 8.0) -> void:
	var end := Time.get_ticks_msec() + int(seconds * 1000)
	while not condition.call():
		if Time.get_ticks_msec() >= end:
			push_error("Timed out: " + label)
			quit(1)
			assert(false)
			return
		await process_frame


func _phase(client: Client, state: String) -> bool:
	return client.snap.get("room", {}).get("state") == state


func _step(client: Client, stage: String) -> bool:
	return client.snap.get("room", {}).get("timeout") == stage


func _run() -> void:
	create_timer(65.0).timeout.connect(func(): quit(1))
	assert(Client.LobbyClient.endpoint(PackedStringArray(["--lobby-url=ws://127.0.0.1:2567"]), false).is_empty())
	var alice := _client("Alice", 2)
	var bob := _client("Bob")
	var dana := _client("Dana", 1)
	var ellis := _client("Ellis")
	var finn := _client("Finn")
	var carol := _client("Carol", 2)
	var guest := _client("Guest", 2)
	var offline := _client("Offline")
	var offline_event := {"at": 0}
	offline.directory.changed.connect(func():
		if offline.directory.state == Client.LobbyClient.State.OFFLINE:
			offline_event.at = Time.get_ticks_msec()
	)
	offline.directory_url = "ws://127.0.0.1:1"
	offline.directory.open(offline.directory_url)
	var offline_at := Time.get_ticks_msec()
	assert(offline.status.text == "Connecting…")
	await _wait_for("directory recovered", func(): return alice.directory.create_available)
	alice.create_btn.pressed.emit()
	var request_id := alice.directory.request_id
	alice.create_btn.pressed.emit()
	assert(not request_id.is_empty() and alice.directory.request_id == request_id)
	await _wait_for("creator admitted to crew", func(): return _phase(alice, "waiting"))
	await _wait_for("creator published once", func(): return bob.directory.rooms.size() == 1)
	var room: String = bob.directory.rooms.keys()[0]
	assert(alice.assignments == 1 and alice.snap.leader == alice.my_id and alice.my_id != 1)

	bob._request_room("stale-room")
	assert(bob.connection == Client.Connection.DIRECTORY and bob.notice == "Room is no longer available")
	bob._request_room(room)
	bob._back_to_menu("Join cancelled")
	bob._request_room(room)
	await _wait_for("same session immediately retries a cancelled request", func(): return _phase(bob, "waiting"), 3.0)
	assert(bob.assignments == 1)
	bob._back_to_menu("Left the room")
	await _wait_for("seat released after leaving", func(): return bob.directory.rooms[room].available == 4)
	await _wait_for("offline deadline", func(): return offline.status.text == "Server offline")
	assert(offline.refresh_btn.text == "Retry" and offline_event.at - offline_at <= 5350)
	assert(bob.connection == Client.Connection.DIRECTORY)
	await create_timer(1.05).timeout # Give the next request burst its own directory rate-limit window.
	bob.assignment_behavior = "cancel"
	bob._request_room(room)
	await _wait_for("cancellation after ticket", func(): return bob.assignments == 2)
	assert(bob.my_id == 0 and bob.admission.is_empty() and bob.admission_deadline == 0 and bob.snap.is_empty())
	bob.assignment_behavior = ""
	bob._request_room(room)
	await _wait_for("same session immediately retries a cancelled ticket", func(): return _phase(bob, "waiting"), 3.0)
	bob._back_to_menu("Left the room")
	await _wait_for("released seat after retry", func(): return bob.directory.rooms[room].available == 4)
	bob.assignment_behavior = "invalid"
	bob._request_room(room)
	await _wait_for("credential denied", func(): return bob.notice == "Worker rejected the seat credential")
	assert(bob.snap.is_empty() and bob.connection == Client.Connection.DIRECTORY)
	await _wait_for("denied ticket cancelled", func(): return bob.directory.rooms[room].available == 4)
	bob.assignment_behavior = "blocked"
	bob._request_room(room)
	await _wait_for("blocked UDP distinguished from offline", func(): return bob.notice.contains("UDP may be blocked"), 10.0)
	assert(bob.directory.state == Client.LobbyClient.State.ONLINE and bob.admission.is_empty())
	await _wait_for("unreachable ticket expired", func(): return bob.directory.rooms[room].available == 4)
	print("Immediate retries, skewed expiry, offline, blocked UDP and invalid credential passed")

	bob.assignment_behavior = ""
	bob._request_room(room)
	await _wait_for("second human admitted", func(): return _phase(bob, "waiting"))
	dana._request_room(room)
	ellis._request_room(room)
	finn._request_room(room)
	carol.create_btn.pressed.emit()
	await _wait_for("five-human crew", func(): return alice.snap.ps.size() == 5)
	await _wait_for("full room published", func(): return not guest.directory.rooms[room].joinable)
	assert(guest.room_rows.any(func(row): return row.id == room and row.join.disabled and row.info.text.ends_with("Full")))
	guest._request_room(room)
	assert(guest.connection == Client.Connection.DIRECTORY and guest.assignments == 0)
	guest.directory.rooms[room].joinable = true
	guest._request_room(room)
	await _wait_for("worker service refuses a stale full-room click", func(): return guest.notice == "Room full")
	await _wait_for("independent room admitted", func(): return _phase(carol, "waiting"))

	guest.assignment_behavior = "cancel"
	guest.create_btn.pressed.emit()
	await _wait_for("cancel assigned Create", func(): return guest.assignments == 1)
	var cancelled_port := guest.assigned_port
	await _wait_for("cancelled Create frees its port early", func(): return guest.directory.create_available, 3.0)
	guest.assignment_behavior = ""
	guest.create_btn.pressed.emit()
	await _wait_for("third room admitted", func(): return _phase(guest, "waiting"))
	assert(guest.assigned_port == cancelled_port)
	ellis._back_to_menu("Left the room")
	finn._back_to_menu("Left the room")
	await _wait_for("directory at capacity", func(): return finn.directory.create_code == "server_full")
	assert(finn.create_btn.disabled and finn.status.text.begins_with("Server full") and finn.refresh_btn.text == "Refresh")
	await _wait_for("three humans in waiting crew", func(): return alice.snap.ps.size() == 3)
	await _wait_for("room has both seats available", func(): return guest.directory.rooms[room].available == 2)
	alice._lobby_ui()
	assert(alice.bot_row.visible)
	bob._act("add_bot", 0)
	await create_timer(0.2).timeout
	assert(bob.snap.room.bots.is_empty())
	alice.bot_buttons[0].pressed.emit()
	await _wait_for("demo bot appears", func(): return alice.snap.room.bots.size() == 1 and guest.directory.rooms[room].available == 1)
	alice._lobby_ui()
	assert(alice.roster.any(func(row): return row.badge.text == "BOT"))
	alice.bot_buttons[1].pressed.emit()
	await _wait_for("demo bot removed", func(): return alice.snap.room.bots.is_empty() and guest.directory.rooms[room].available == 2)
	var stale_waiting: Dictionary = guest.directory.rooms[room].duplicate()
	var crew := [alice, bob, dana]
	for client in crew:
		client.ready_btn.pressed.emit()
	await _wait_for("all humans ready", func(): return alice.snap.room.ready.size() == 3)
	bob._act("role", 1)
	await _wait_for("role change clears every Ready flag", func():
		return crew.all(func(client): return client.snap.room.ready.is_empty() and client.snap.ps[bob.my_id][1] == 1)
	)
	alice._lobby_ui()
	assert(alice.start_btn.disabled and alice.lobby_hint.text.contains("Ready"))
	for client in crew:
		client.ready_btn.pressed.emit()
	await _wait_for("all humans readied again", func(): return alice.snap.room.ready.size() == 3)
	var next_mode: int = (alice.snap.mode + 1) % 3
	alice._act("mode", next_mode)
	await _wait_for("mode change clears every Ready flag", func():
		return crew.all(func(client): return client.snap.room.ready.is_empty() and client.snap.mode == next_mode)
	)
	for client in crew:
		client.ready_btn.pressed.emit()
	await _wait_for("crew can start", func(): return alice.snap.room.ready.size() == 3)
	alice._lobby_ui()
	assert(not alice.start_btn.disabled)
	var waiting_tp: int = bob.local_tp
	ellis.assignment_behavior = "hold"
	ellis._request_room(room)
	await _wait_for("assignment held before ENet proof", func(): return ellis.proof_held)
	alice.start_btn.pressed.emit()
	await _wait_for("started room reports terminal notice over the directory", func(): return ellis.notice == "Match already running")
	assert(ellis.connection == Client.Connection.DIRECTORY and ellis.admission.is_empty())
	await _wait_for("active match", func(): return _phase(bob, "playing") and bob.snap.ps[bob.my_id][24] > 0)
	assert(bob.local_tp != waiting_tp and bob.worker_ack > 0)
	var before_move: Vector2 = bob.snap.ps[bob.my_id][2]
	bob.local_pos += Vector2.RIGHT * 900.0
	await _wait_for("worker corrects arbitrary client position", func(): return bob.local_pos.distance_to(before_move) < 100.0)
	await _wait_for("active match closed to admission", func(): return guest.directory.rooms[room].state == "playing")
	finn.directory.rooms[room] = stale_waiting
	finn._request_room(room)
	await _wait_for("server rejects stale mid-match join", func(): return finn.notice == "Match already running")
	print("Full room and server, Ready resets, start, teleport, movement correction and mid-match denial passed")

	bob.directory.close()
	await _wait_for("worker acknowledged input after lobby socket closed", func(): return bob.snap.ps[bob.my_id][24] > 10)
	var alice_id := alice.my_id
	alice._back_to_menu("Left the room")
	await _wait_for("leader succession and first vote", func(): return _step(bob, "continue") and bob.snap.leader == bob.my_id)
	await _wait_for("directory shows the successor's name", func(): return guest.directory.rooms[room].name == "Bob")
	var paused_tp: int = bob.local_tp
	var match_time: float = bob.snap.t
	bob._timeout_ui()
	assert(bob.vote_panel.visible and bob.vote_warning.visible and bob.vote_details.text.contains("Alice"))
	bob.vote_buttons[1].pressed.emit()
	await _wait_for("first vote counted", func(): return bob.snap.room.votesCast == 1 and bob.snap.room.votes == 0)
	bob.vote_buttons[0].pressed.emit()
	await _wait_for("vote change counted", func(): return bob.snap.room.votesCast == 1 and bob.snap.room.votes == 1)
	assert(bob.snap.t == match_time and bob.local_pos == bob.snap.ps[bob.my_id][2])
	dana._cast_timeout_vote(true)
	await _wait_for("replacement vote", func(): return _step(bob, "bot") and _step(dana, "bot"))
	bob._cast_timeout_vote(true)
	dana._cast_timeout_vote(true)
	await _wait_for("resume countdown", func(): return _step(bob, "countdown"))
	bob._timeout_ui()
	assert(bob.vote_title.text.begins_with("Resume in") and bob.snap.t == match_time)
	await _wait_for("play resumed with replacement", func(): return _phase(bob, "playing") and alice_id in bob.snap.room.bots)
	assert(bob.local_tp > paused_tp and bob.snap.t < match_time and not bob.snap.room.noEngineer)
	assert(_phase(carol, "waiting") and carol.snap.leader == carol.my_id)
	dana._back_to_menu("Left the room")
	await _wait_for("second pause", func(): return _step(bob, "continue"))
	bob._cast_timeout_vote(false)
	await _wait_for("result without winner", func(): return _phase(bob, "result") and bob.snap.result == "no_winner")
	await _wait_for("same room returns to crew", func(): return _phase(bob, "waiting"), 12.0)
	assert(bob.snap.leader == bob.my_id and bob.snap.ps.size() == 1 and bob.snap.room.ready.is_empty())
	print("Directory socket loss, succession, freeze, changed vote, bot, result and return to crew passed")

	bob.send_inputs = false
	var last_packet := bob.last_worker_packet
	await create_timer(0.5).timeout
	var last_ack := bob.last_worker_ack
	assert(bob.last_worker_packet > last_packet and bob.worker_ack > 0)
	await _wait_for("one-way liveness deadline", func(): return bob.connection == Client.Connection.DIRECTORY, 6.0)
	assert(bob.notice == "Connection to the match worker lost" and Time.get_ticks_msec() - last_ack >= Client.WORKER_LIVE_MS)
	multiplayer_poll = false
	last_packet = guest.last_worker_packet
	await _wait_for("no-worker-traffic deadline", func(): return guest.connection == Client.Connection.DIRECTORY, 6.0)
	assert(guest.notice == "Connection to the match worker lost" and Time.get_ticks_msec() - last_packet >= Client.WORKER_LIVE_MS)
	print("One-way and absent worker traffic deadlines passed")
	for client in [alice, bob, dana, ellis, finn, carol, guest, offline]:
		client._back_to_menu("")
		client.directory.close()
	await create_timer(0.1).timeout
	quit(0)
