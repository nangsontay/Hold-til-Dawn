extends RefCounted
## One room and generation. All admission and gameplay transitions run on the main thread.

const Protocol = preload("res://worker-control.gd")
const PROTOCOL := 1
const CONTENT := "island-1"
const CLIENT_BUILD := CONTENT + "-client-2"
const CAPACITY := 5
const TRANSPORT_PEERS := 13
const HANDSHAKE_MS := 5000
const CHALLENGE_MS := 3000
const ACTIVE_IDLE_MS := 900
const MAX_INPUT_SILENCE_MS := 12000
const ENET_PING_MS := 200
const ENET_TIMEOUT_MIN_MS := 1500
const ENET_TIMEOUT_MAX_MS := 3000
const HEARTBEAT_MS := 1000
const TICKET_MS := 30000
const EMPTY_MS := 60000
const VOTE_MS := 30000
const COUNTDOWN_MS := 3000

var sim: GameSim
var control: Protocol
var creator_admitted := false
var admitted := {} # peer -> identity
var last_input := {}
var input_acks := {}
var reservations := {} # ticket -> reservation
var requests := {} # request ID -> outcome retained until its original expiry
var started_tickets := {} # tickets invalidated by Start, retained for their original lifetime
var attempts := {} # peer -> handshake/deadline
var ready := {}
var development := false
var vacancies := {}
var votes := {}
var timeout := ""
var connection_paused := false
var deadline := 0
var use_bots := false
var empty_since: int
var empty_paused_at := -1
var initial_deadline: int
var started := false
var exit_code := -1
var report_sequence := 0
var next_heartbeat := 0
var last_control_frame := -1
var max_frame_ms := 0
var last_report := {}
var last_name: String


static func config_from_environment() -> Dictionary:
	var room := OS.get_environment("MATCH_ROOM_ID")
	var gen := OS.get_environment("MATCH_GENERATION")
	var room_name := OS.get_environment("MATCH_ROOM_NAME").strip_edges()
	var udp := OS.get_environment("MATCH_UDP_PORT")
	var endpoint := OS.get_environment("CONTROL_ENDPOINT").split(":")
	if not Protocol.token(room) or not Protocol.token(gen) or not _valid_name(room_name) or udp not in ["7778", "7779", "7780"]:
		return {}
	if endpoint.size() != 2 or endpoint[0] != "127.0.0.1" or not endpoint[1].is_valid_int() or not Protocol.integer(endpoint[1].to_int(), 1, 65535):
		return {}
	var source := OS.get_environment("ADMISSION_SECRET_FILE")
	var file := FileAccess.open(source, FileAccess.READ)
	if file == null or file.get_length() > 128:
		return {}
	var recovery := file.get_as_text().strip_edges()
	if not Protocol.is_hex(recovery, 32):
		return {}
	return {"roomId": room, "generation": gen, "name": room_name.substr(0, 16), "udp": udp.to_int(),
		"tcp": endpoint[1].to_int(), "key": recovery.hex_decode(), "development": OS.is_debug_build()}


static func _valid_name(value: Variant) -> bool:
	if not value is String or value.length() > 64 or value.strip_edges().is_empty():
		return false
	for i in value.length():
		if value.unicode_at(i) < 32 or value.unicode_at(i) == 127:
			return false
	return true


func _init(game: GameSim, config: Dictionary, now: int) -> void:
	sim = game
	sim.managed = true
	development = OS.is_debug_build() and config.get("development", false)
	control = Protocol.new(config.roomId, config.generation, config.udp, config.tcp, config.key)
	empty_since = now
	initial_deadline = now + HANDSHAKE_MS
	last_name = config.name


func state() -> String:
	if not timeout.is_empty() or connection_paused:
		return "timeout"
	match sim.phase:
		"lobby":
			return "waiting"
		"result":
			return "result"
		_:
			return "playing"


func _report() -> Dictionary:
	var leader := sim.leader()
	if admitted.has(leader):
		last_name = sim.players[leader].name
	var bots := sim.players.values().filter(func(p): return p.bot).size()
	return {"name": last_name, "state": state(), "mode": sim.mode, "humans": admitted.size(), "pending": reservations.size(),
		"bots": bots, "capacity": CAPACITY, "available": CAPACITY - admitted.size() - reservations.size() - bots - vacancies.size(),
		"creatorAdmitted": creator_admitted, "leader": leader, "pid": control.pid, "protocol": PROTOCOL, "content": CONTENT}


func _send(kind: String, data: Dictionary, now: int) -> bool:
	if control.status != "online":
		return false
	if control.send(kind, data):
		return true
	control.disconnect_control(now)
	return false


func update_control(now: int, wall_ms: int) -> void:
	if last_control_frame >= 0:
		max_frame_ms = maxi(max_frame_ms, now - last_control_frame)
	last_control_frame = now
	var before := control.status
	for message in control.poll(now, report_sequence):
		match message.type:
			"protocol_error":
				if not started:
					exit_code = 1
			"reserve":
				var response := reserve(message.data, now, wall_ms)
				response.requestId = message.data.get("requestId") if Protocol.token(message.data.get("requestId")) else ""
				_send("reservation", response, now)
			"cancel":
				var request = message.data.get("requestId")
				var ticket = message.data.get("ticketId")
				var by_request := Protocol.token(request)
				if by_request:
					ticket = requests.get(request, {}).get("ticket")
				var valid := Protocol.is_hex(ticket, 16)
				var creator: bool = valid and reservations.get(ticket, {}).get("creator", false)
				var code := "unknown_ticket"
				if valid and _release(ticket, "cancelled", now):
					code = "cancelled"
				var reply := {"ticketId": ticket if valid else "", "code": code}
				if by_request:
					reply.requestId = request
				_send("cancellation", reply, now)
				if code == "cancelled" and creator and not creator_admitted:
					exit_code = 0
					return
			"close":
				_send("closing", {"code": "closed"}, now)
				exit_code = 0
				return
			"ping":
				pass
			_:
				_send("error", {"code": "unknown_command"}, now)
	if control.status != "online":
		if not started and now >= initial_deadline:
			exit_code = 1
		return
	var report := _report()
	if before != "online":
		if not _send("ready", report, now):
			return
		started = true
		last_report = {}
	if now >= next_heartbeat or report != last_report:
		var data := report.duplicate()
		report_sequence += 1
		data.sequence = report_sequence
		data.maxFrameMs = max_frame_ms
		_send("heartbeat", data, now)
		max_frame_ms = 0
		next_heartbeat = now + HEARTBEAT_MS
		last_report = report


func reserve(data: Dictionary, now: int, wall_ms: int) -> Dictionary:
	_expire_tickets(now)
	var request = data.get("requestId")
	var identity = data.get("identity")
	var pname = data.get("name")
	var creator = data.get("creator")
	var role = data.get("role")
	var expiry = data.get("expiresAt")
	if not Protocol.token(request) or not Protocol.token(identity) or not _valid_name(pname) \
			or not creator is bool or not Protocol.integer(role, 0, 2) \
			or not Protocol.integer(expiry, 1, 9007199254740991):
		return {"code": "invalid_request"}
	if expiry <= wall_ms:
		return {"code": "expired"}
	if expiry > wall_ms + TICKET_MS:
		return {"code": "invalid_request"}
	if control.status != "online" or not started:
		return {"code": "recovering"}
	if _empty_window_elapsed(now):
		return {"code": "unavailable"}
	if requests.has(request):
		var prior: Dictionary = requests[request]
		if prior.data != data:
			return {"code": "request_conflict"}
		var existing: Dictionary = reservations.get(prior.ticket, {})
		return {"code": prior.code} if existing.is_empty() else _credential(existing, now)
	if state() != "waiting":
		return {"code": "room_started"}
	if not creator_admitted and (not creator or not reservations.is_empty()):
		return {"code": "creator_pending"}
	if creator_admitted and creator:
		return {"code": "creator_exists"}
	if _report().available <= 0:
		return {"code": "full"}
	if admitted.values().has(identity) or reservations.values().any(func(r): return r.identity == identity):
		return {"code": "identity_in_use"}
	if requests.size() >= 256:
		return {"code": "busy"}
	var ticket := Protocol.random_hex(16)
	var secret := Protocol.random_hex(32)
	if ticket.length() != 32 or secret.length() != 64 or reservations.has(ticket):
		return {"code": "unavailable"}
	var until := now + int(expiry) - wall_ms
	var reservation := {"ticket": ticket, "secret": secret, "identity": identity, "name": pname.strip_edges().substr(0, 16),
		"creator": creator, "role": int(role), "until": until, "request": request, "expiresAt": int(expiry)}
	if empty_since >= 0 and reservations.is_empty():
		empty_paused_at = now
	reservations[ticket] = reservation
	requests[request] = {"ticket": ticket, "data": data.duplicate(), "until": until, "code": "reserved"}
	return _credential(reservation, now)


func _credential(r: Dictionary, now: int) -> Dictionary:
	return {"code": "reserved", "ticketId": r.ticket, "secret": r.secret, "expiresAt": r.expiresAt, "ttlMs": r.until - now}


func _release(ticket: String, code: String, now: int) -> bool:
	if not reservations.has(ticket):
		return false
	requests[reservations[ticket].request].code = code
	reservations.erase(ticket)
	if empty_paused_at >= 0 and reservations.is_empty():
		empty_since += now - empty_paused_at
		empty_paused_at = -1
	return true


func _expire_tickets(now: int) -> void:
	var expired_at := empty_paused_at
	for ticket in reservations.keys():
		if now >= reservations[ticket].until:
			expired_at = maxi(expired_at, reservations[ticket].until)
			_release(ticket, "expired", expired_at)
	for ticket in started_tickets.keys():
		if now >= started_tickets[ticket].until:
			started_tickets.erase(ticket)
	for request in requests.keys():
		if now >= requests[request].until:
			requests.erase(request)


func connected(peer: int, now: int) -> void:
	attempts[peer] = {"until": now + HANDSHAKE_MS}


func challenge(peer: int, protocol: int, content: String, ticket: String, nonce: String, now: int) -> Dictionary:
	_expire_tickets(now)
	var attempt: Dictionary = attempts.get(peer, {})
	if attempt.is_empty() or now >= attempt.until or attempt.get("spent", false):
		return {"code": "invalid_attempt"}
	attempt.spent = true
	if protocol != PROTOCOL or content != CLIENT_BUILD:
		return {"code": "incompatible_content"}
	if Protocol.is_hex(nonce, 32) and started_tickets.has(ticket):
		return {"code": "room_started"}
	if not started or control.status != "online" or state() != "waiting":
		return {"code": "room_unavailable"}
	if not Protocol.is_hex(ticket, 16) or not Protocol.is_hex(nonce, 32) or not reservations.has(ticket):
		return {"code": "invalid_ticket"}
	var random := Protocol.random_hex(32)
	if random.length() != 64:
		return {"code": "unavailable"}
	attempt.merge({"ticket": ticket, "nonce": nonce, "challenge": random}, true)
	attempt.until = mini(attempt.until, now + CHALLENGE_MS)
	return {"code": "challenge", "challenge": random, "roomId": control.room_id, "generation": control.generation}


static func ticket_proof(secret: String, room: String, generation: String, ticket: String, peer: int, nonce: String, random: String) -> PackedByteArray:
	return Protocol.mac(secret.hex_decode(), [str(PROTOCOL), room, generation, ticket, str(peer), nonce, random])


func admit(peer: int, proof: String, now: int) -> String:
	_expire_tickets(now)
	var attempt: Dictionary = attempts.get(peer, {})
	if not attempt.has("ticket") or now >= attempt.until or not Protocol.is_hex(proof, 32):
		if not attempt.is_empty():
			attempt.spent = true
			attempt.erase("ticket")
		return "invalid_proof"
	var ticket: String = attempt.ticket
	attempt.erase("ticket")
	var reservation: Dictionary = reservations.get(ticket, started_tickets.get(ticket, {}))
	if reservation.is_empty():
		return "room_unavailable" if state() != "waiting" else "invalid_ticket"
	var expected := ticket_proof(reservation.secret, control.room_id, control.generation, ticket, peer, attempt.nonce, attempt.challenge)
	if not Protocol.equal_mac(expected, proof.hex_decode()):
		return "invalid_proof"
	if started_tickets.has(ticket):
		return "room_started"
	if not started or control.status != "online" or state() != "waiting":
		return "room_unavailable"
	_release(ticket, "admitted", now)
	if not sim.add_player(peer, reservation.name, reservation.role):
		requests[reservation.request].code = "insertion_failed"
		return "insertion_failed"
	admitted[peer] = reservation.identity
	attempts.erase(peer)
	empty_since = -1
	if reservation.creator:
		creator_admitted = true
		_send("creator_admitted", _report(), now)
	return "admitted"


func disconnected(peer: int, now: int) -> void:
	attempts.erase(peer)
	if not admitted.erase(peer):
		return
	last_input.erase(peer)
	input_acks.erase(peer)
	ready.erase(peer)
	votes.erase(peer)
	_report()
	if admitted.is_empty():
		empty_since = now
		if not reservations.is_empty():
			empty_paused_at = now
	if state() not in ["playing", "timeout"]:
		sim.remove_player(peer)
		if admitted.is_empty():
			for id in sim.order.duplicate():
				if sim.players[id].bot:
					sim.remove_player(id)
		_report()
		return
	var pname: String = sim.players[peer].name
	vacancies[peer] = pname
	sim.mark_vacant(peer)
	sim.frozen = true
	if admitted.is_empty():
		_end_timeout("All players left")
	elif timeout.is_empty():
		connection_paused = false
		timeout = "continue"
		deadline = now + VOTE_MS
	_report()


func action(peer: int, kind: String, arg: Variant, now := 0) -> void:
	if not admitted.has(peer):
		return
	match state():
		"waiting":
			match kind:
				"ready":
					if arg is bool:
						if arg:
							ready[peer] = true
						else:
							ready.erase(peer)
				"role":
					if arg is int and arg in [0, 1, 2] and sim.players[peer].role != arg:
						sim.action(peer, kind, arg)
						ready.clear()
				"mode":
					if peer == sim.leader() and arg is int and arg in [0, 1, 2] and sim.mode != arg:
						sim.action(peer, kind, arg)
						ready.clear()
				"add_bot", "remove_bot":
					if development and peer == sim.leader() and (kind == "remove_bot" or _report().available > 0):
						sim.action(peer, kind, arg)
				"start":
					var normal_start := ready.size() == admitted.size() and admitted.size() >= 1
					var development_start := development and ready.size() >= 1 and sim.players.values().any(func(p): return p.bot)
					if peer == sim.leader() and (normal_start or development_start):
						var error := sim.start_match()
						if error.is_empty():
							for human in admitted:
								last_input[human] = now
							for ticket in reservations.keys():
								started_tickets[ticket] = reservations[ticket]
								_release(ticket, "room_started", now)
								_send("reservation_cancelled", {"requestId": started_tickets[ticket].request, "reason": "room_started"}, now)
						else:
							sim._say(error)
		"playing":
			if kind in ["craft", "tower", "bandage", "flask", "vote"]:
				sim.action(peer, kind, arg)
		"timeout":
			if kind == "timeout_vote" and arg is Array and arg.size() == 2 and timeout in ["continue", "bot"] \
					and arg[0] == timeout and arg[1] is bool and now < deadline:
				votes[peer] = arg[1]


func receive_input(peer: int, sequence: int, now: int) -> bool:
	if not admitted.has(peer) or sequence <= input_acks.get(peer, 0):
		return false
	last_input[peer] = now
	input_acks[peer] = sequence
	return true


func update_game(dt: float, now: int) -> Array:
	_expire_tickets(now)
	var timed_out := []
	for peer in attempts.keys():
		if now >= attempts[peer].until:
			attempts.erase(peer)
			timed_out.append(peer)
	if timeout.is_empty():
		var silent := sim.phase in ["day", "dusk", "night"] and admitted.keys().any(
			func(peer): return now - last_input.get(peer, now) >= ACTIVE_IDLE_MS)
		if silent:
			connection_paused = true
			sim.frozen = true
			var stalled := admitted.keys().filter(
				func(peer): return now - last_input.get(peer, now) >= MAX_INPUT_SILENCE_MS)
			for peer in stalled:
				disconnected(peer, now)
				timed_out.append(peer)
			return timed_out
		if connection_paused:
			_resume()
		var was_result := sim.phase == "result"
		sim.tick(dt)
		if was_result and sim.phase == "lobby":
			ready.clear()
	elif timeout == "countdown":
		if now >= deadline:
			for peer in vacancies:
				sim.resolve_vacancy(peer, use_bots)
			_resume()
	elif now >= deadline or votes.size() == admitted.size():
		var yes := votes.values().count(true)
		var no := votes.size() - yes
		if timeout == "continue":
			if no > yes:
				_end_timeout("Crew ended the match")
			else:
				timeout = "bot"
				deadline = now + VOTE_MS
		else:
			use_bots = yes > no
			timeout = "countdown"
			deadline = now + COUNTDOWN_MS
		votes.clear()
	if _empty_window_elapsed(now):
		exit_code = 0
	return timed_out


func _empty_window_elapsed(now: int) -> bool:
	return empty_since >= 0 and empty_paused_at < 0 and now - empty_since >= EMPTY_MS


func _resume() -> void:
	timeout = ""
	connection_paused = false
	votes.clear()
	vacancies.clear()
	sim.frozen = false
	for peer in admitted:
		var p: Dictionary = sim.players[peer]
		p.tp += 1 # Fence queued inputs sampled before the pause.
		p.atk = false
		p.inter = false
		p.ent = false
		p.awaiting_resume_input = true


func _end_timeout(why: String) -> void:
	for peer in vacancies:
		sim.resolve_vacancy(peer, false, false)
	_resume()
	sim.end_without_winner(why)


func snapshot(now: int) -> Dictionary:
	var data := sim.snapshot()
	data.room = {"name": last_name, "state": state(), "ready": ready.keys(), "bots": sim.players.values().filter(func(p): return p.bot).map(func(p): return p.id),
		"timeout": "connection" if connection_paused else timeout, "remainingMs": maxi(0, deadline - now) if not timeout.is_empty() else 0, "vacancies": vacancies.values(),
		"votes": votes.values().count(true), "votesCast": votes.size(), "inputAcks": input_acks,
		"noEngineer": not sim.players.values().any(func(p): return p.role == 2 and not vacancies.has(p.id))}
	data.seed = sim.seed_at_start
	data.result = sim.result
	data.storage = sim.storage
	return data
