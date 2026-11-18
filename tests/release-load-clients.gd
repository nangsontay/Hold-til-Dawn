extends SceneTree
## Real directory, admission, snapshot, input, and reliable action RPCs.

class Client extends "res://main.gd":
	var callsign: String
	var role: int
	var proxy_base: int
	var port := 0
	var admitted_at := 0
	var collecting := false
	var snapshots := []
	var input_times := {}
	var input_rtt := []
	var flasks_at := 0.0
	var flask_count := 0
	var action_ms := []

	func _name() -> String:
		return callsign

	func _ready() -> void:
		super._ready()
		role_opt = OptionButton.new()
		add_child(role_opt)
		for label in ["Scout", "Healer", "Engineer"]:
			role_opt.add_item(label)
		role_opt.select(role)
		_open_directory()

	func _key(key: Key) -> float:
		if key == KEY_A or key == KEY_D:
			return 1.0 if floori(Time.get_ticks_msec() / 750.0) % 2 == int(key == KEY_A) else 0.0
		return 0.0

	func join_managed(host: String, target: int, room: String, generation: String, ticket: String, secret: String) -> Error:
		port = target
		# The real assignment has already been validated. Only the test socket routes through a relay port.
		var peer := ENetMultiplayerPeer.new()
		var error := peer.create_client(host, proxy_base + target - 7778)
		if error == OK:
			connection = Connection.CONNECTING
			worker_linked = false
			proof_sent = false
			admission = {"room": room, "generation": generation, "ticket": ticket, "secret": secret,
				"nonce": MatchWorker.Protocol.random_hex(32)}
			admission_deadline = Time.get_ticks_msec() + MatchWorker.HANDSHAKE_MS
			multiplayer.multiplayer_peer = peer
		return error

	func _move(dt: float) -> void:
		var prior := input_sequence
		super._move(dt)
		if collecting and input_sequence != prior:
			input_times[input_sequence] = Time.get_unix_time_from_system() * 1000.0
			input_times.erase(input_sequence - 128)

	func _on_admission_result(code: String) -> void:
		super._on_admission_result(code)
		if code == "admitted":
			admitted_at = Time.get_ticks_msec()

	func _on_snap(data: Dictionary) -> void:
		super._on_snap(data)
		if not collecting or data.ph != "night":
			return
		var now := Time.get_unix_time_from_system() * 1000.0
		snapshots.append([now, roundi(data.t * 1000.0), data.base, data.doors, data.en.map(func(enemy):
			return [enemy[0], enemy[2].x, enemy[2].y]
		)])
		if input_times.has(worker_ack):
			input_rtt.append(now - input_times[worker_ack])
			input_times.erase(worker_ack)
		if flasks_at > 0.0 and data.ps[my_id][10] < flask_count:
			action_ms.append(now - flasks_at)
			flasks_at = 0.0

	func flask() -> void:
		flask_count = snap.ps[my_id][10]
		flasks_at = Time.get_unix_time_from_system() * 1000.0
		_act("flask", snap.ps[my_id][2])


var directory := OS.get_environment("HTD_TEST_DIR")
var duration := OS.get_environment("HTD_LOAD_SECONDS").to_float()
var run_recovery := OS.get_environment("HTD_LOAD_RECOVERY") == "1"


func _initialize() -> void:
	_run.call_deferred()


func _client(player: String, role: int, index: int) -> Client:
	var branch := Node.new()
	branch.name = player
	root.add_child(branch)
	set_multiplayer(SceneMultiplayer.new(), branch.get_path())
	var client := Client.new()
	client.name = "Main"
	client.callsign = player
	client.role = role
	client.proxy_base = int(JSON.parse_string(FileAccess.get_file_as_string(directory + "/proxy.json"))[index])
	branch.add_child(client)
	return client


func _wait_for(label: String, check: Callable, seconds := 10.0) -> void:
	var until := Time.get_ticks_msec() + int(seconds * 1000.0)
	while not check.call():
		if Time.get_ticks_msec() >= until:
			push_error("Release verification timed out: " + label)
			quit(1)
			assert(false)
			return
		await process_frame


func _write(name: String, value: Variant) -> void:
	var file := FileAccess.open(directory + "/" + name, FileAccess.WRITE)
	file.store_string(JSON.stringify(value))


func _state(client: Client, state: String) -> bool:
	return client.snap.get("room", {}).get("state") == state


func _run() -> void:
	var opened_at := Time.get_ticks_msec()
	var observer := _client("Observer", 0, 15)
	var published := {}
	observer.directory.changed.connect(func():
		for room_id in observer.directory.rooms:
			if not published.has(room_id):
				published[room_id] = Time.get_ticks_msec()
	)
	await _wait_for("initial directory", func(): return observer.directory.state == Client.LobbyClient.State.ONLINE)
	var list_ms := Time.get_ticks_msec() - opened_at
	var crews := []
	var leads := []
	for room_index in 3:
		var crew := []
		for member in 5:
			crew.append(_client("Room%dPlayer%d" % [room_index, member], 2 if member == 0 else int(member == 1),
				room_index * 5 + member))
		crews.append(crew)
		leads.append(crew[0])
	await _wait_for("clients online", func():
		return crews.all(func(crew): return crew.all(func(client): return client.directory.create_available))
	)
	var publication_ms := []
	var create_ms := []
	for i in 3:
		var before := observer.directory.rooms.keys()
		var leader: Client = leads[i]
		var requested_at := Time.get_ticks_msec()
		leader._request_room()
		await _wait_for("creator admitted", func(): return _state(leader, "waiting"))
		await _wait_for("directory published room", func(): return observer.directory.rooms.size() == i + 1)
		var room_id: String = observer.directory.rooms.keys().filter(func(id): return id not in before)[0]
		assert(leader.admitted_at >= requested_at)
		publication_ms.append(maxi(0, published[room_id] - leader.admitted_at))
		create_ms.append(published[room_id] - requested_at)
		for member in crews[i].slice(1):
			await _wait_for("waiting seat published", func(): return member.directory.rooms.get(room_id, {}).get("joinable", false))
			member._request_room(room_id)
			if member == crews[i][1]:
				await _wait_for("successor admitted first", func(): return _state(member, "waiting"))
		await _wait_for("five human seats admitted", func(): return leader.snap.ps.size() == 5)
	assert(observer.directory.create_code == "server_full" and not observer.directory.create_available)
	for i in 3:
		for member in crews[i]:
			member._act("ready", true)
	await _wait_for("all crews ready", func(): return leads.all(func(lead): return lead.snap.room.ready.size() == 5))
	for lead in leads:
		lead._act("start")
	await _wait_for("all three rooms active", func(): return leads.all(func(lead): return _state(lead, "playing")))
	for crew in crews:
		for member in crew.slice(0, 3):
			member._act("vote")
	await _wait_for("all 15 clients see at least 60 enemies at night", func():
		return crews.all(func(crew): return crew.all(func(client):
			return client.snap.get("ph") == "night" and client.snap.en.size() >= 60
		))
	)
	print("Directory list_ms=", list_ms, " admission_to_publication_ms=", publication_ms, " create_to_publication_ms=", create_ms)
	assert(list_ms <= 3000 and publication_ms.max() <= 1000)
	for crew in crews:
		for member in crew:
			member.collecting = true
	_write("measure", true)
	for action in 3:
		await create_timer(duration / 4.0).timeout
		for crew in crews:
			crew[1].flask()
	await create_timer(duration / 4.0).timeout
	var result := {}
	for crew in crews:
		for member in crew:
			member.collecting = false
			if not result.has(member.port):
				result[member.port] = []
			result[member.port].append({"snapshots": member.snapshots, "input_ms": member.input_rtt, "action_ms": member.action_ms})
	_write("clients.json", result)
	assert(leads.all(func(lead): return _state(lead, "playing")))
	if run_recovery:
		await _recovery(crews, observer)
	for crew in crews:
		for member in crew:
			member._back_to_menu("")
			member.directory.close()
	observer._back_to_menu("")
	observer.directory.close()
	await create_timer(0.15).timeout
	quit(0)


func _recovery(crews: Array, observer: Client) -> void:
	var before: float = crews[0][0].snap.t
	var seeds := crews.map(func(crew): return crew[0].snap.seed)
	_write("restart", true)
	await _wait_for("manager restarted", func(): return FileAccess.file_exists(directory + "/restarted"))
	observer._open_directory()
	await _wait_for("all playing rooms recovered", func(): return observer.directory.rooms.size() == 3 and 		observer.directory.rooms.values().all(func(room): return room.humans == 5 and room.state == "playing"))
	assert(crews[0][0].snap.t < before and crews.map(func(crew): return crew[0].snap.seed) == seeds)
	assert(observer.directory.create_code == "server_full")
	print("Lobby crash/restart recovered three active rooms with unchanged seeds and full occupancy")
	_write("drain", true)
	await _wait_for("draining listing", func(): return observer.directory.create_code == "server_draining")
	assert(not observer.directory.create_available and crews.all(func(crew): return _state(crew[0], "playing")))
	_write("kill-worker", crews[0][0].port)
	await _wait_for("clients of killed worker return to menu", func():
		return crews[0].all(func(client): return client.connection == Client.Connection.DIRECTORY)
	)
	var lost_ms := Time.get_unix_time_from_system() * 1000.0 - float(FileAccess.get_file_as_string(directory + "/killed-at"))
	print("All 5 affected clients returned to menu in ", roundi(lost_ms), " ms")
	assert(lost_ms <= 5000 and crews[0].all(func(client): return client.notice.contains("Connection to the match worker lost")))
	assert(_state(crews[1][0], "playing") and _state(crews[2][0], "playing"))
	await _wait_for("dead room removed", func(): return observer.directory.rooms.size() == 2)
	_write("reopen", true)
	await _wait_for("capacity is available after drain", func(): return observer.directory.create_available)
	observer._request_room()
	await _wait_for("replacement starts in recovered slot", func(): return _state(observer, "waiting"))
	assert(observer.port == crews[0][0].port)
	var crew: Array = crews[1]
	var departed_at := Time.get_ticks_msec()
	crew[0]._back_to_menu("")
	await _wait_for("leader departure freezes match", func():
		return crew[1].snap.get("room", {}).get("timeout") == "continue"
	)
	var freeze_ms := Time.get_ticks_msec() - departed_at
	var frozen: Dictionary = crew[1].snap
	assert(freeze_ms <= 1000 and frozen.leader == crew[1].my_id and frozen.room.noEngineer)
	for member in crew.slice(1):
		member._act("timeout_vote", ["continue", true])
	await _wait_for("no-bot vote begins", func(): return crew[1].snap.room.timeout == "bot")
	assert(crew[1].snap.t == frozen.t and crew[1].snap.en == frozen.en and crew[1].snap.proj == frozen.proj)
	for member in crew.slice(1):
		member._act("timeout_vote", ["bot", false])
	await _wait_for("resume countdown", func(): return crew[1].snap.room.timeout == "countdown")
	var countdown_at := Time.get_ticks_msec()
	await _wait_for("crew resumes without Engineer", func(): return _state(crew[1], "playing"))
	var resume_ms := Time.get_ticks_msec() - countdown_at
	print("Freeze_ms=", freeze_ms, " countdown_ms=", resume_ms)
	assert(absi(resume_ms - 3000) <= 150 and crew[1].snap.room.noEngineer and crew[1].snap.ps.size() == 4)
	crew[4]._back_to_menu("")
	await _wait_for("second timeout before voted end", func(): return crew[1].snap.room.timeout == "continue")
	for member in crew.slice(1, 4):
		member._act("timeout_vote", ["continue", false])
	await _wait_for("no-winner result", func(): return crew[1].snap.get("result") == "no_winner")
	await _wait_for("same crew returns to waiting", func(): return _state(crew[1], "waiting"), 12.0)
	crew[1]._act("role", 2)
	await _wait_for("successor selects Engineer", func(): return crew[1].snap.ps[crew[1].my_id][1] == 2)
	for member in crew.slice(1, 4):
		member._act("ready", true)
	await _wait_for("rematch crew is ready", func(): return crew[1].snap.room.ready.size() == 3)
	crew[1]._act("start")
	await _wait_for("rematch seed reaches all clients", func():
		return crew.slice(1, 4).all(func(client): return _state(client, "playing") and client.snap.seed == crew[1].snap.seed)
	)
	assert(crew[1].snap.seed != seeds[1])
	print("Voted End, return to crew, and shared new seed passed")
	var stranded: Client = crews[2][0]
	var survivor: Client = crews[2][1]
	_write("blackhole", stranded.proxy_base + stranded.port - 7778)
	await _wait_for("UDP uplink silently blocked", func(): return FileAccess.file_exists(directory + "/blackholed-at"))
	var blocked_at := float(FileAccess.get_file_as_string(directory + "/blackholed-at"))
	await _wait_for("teammates freeze on silent UDP uplink outage", func(): return _state(survivor, "timeout"))
	var freeze_on_outage := Time.get_unix_time_from_system() * 1000.0 - blocked_at
	await _wait_for("isolated client leaves after its UDP uplink fails", func(): return stranded.connection == Client.Connection.DIRECTORY)
	var outage_ms := Time.get_unix_time_from_system() * 1000.0 - blocked_at
	print("One-way UDP freeze_ms=", roundi(freeze_on_outage), " client_return_ms=", roundi(outage_ms))
	assert(freeze_on_outage <= 1000 and outage_ms <= 5000 and _state(crew[1], "playing"))
