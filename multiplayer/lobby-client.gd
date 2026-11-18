extends RefCounted
## Directory transport. ENet admission and gameplay use a separate connection.

signal changed
signal assigned(ticket: Dictionary)
signal rejected(code: String)

enum State { CLOSED, CONNECTING, ONLINE, OFFLINE, DEGRADED }
const DEADLINE_MS := 5000
const RESERVATION_MS := 9000
const DEFAULT_URL := "wss://game.example.com"
const SDK = preload("res://addons/colyseus/colyseus.gd")
const Wire = preload("res://worker-control.gd")
const MatchWorker = preload("res://match-worker.gd")

var state := State.CLOSED
var rooms := {}
var create_available := false
var create_code := ""
var request_id := ""
var _client: SDK.Client
var _room: SDK.Room
var _session := 0
var _connect_by := 0
var _request_by := 0
var _target := ""


static func endpoint(args: PackedStringArray, debug: bool) -> String:
	var url := OS.get_environment("LOBBY_URL")
	for arg in args:
		if arg.begins_with("--lobby-url="):
			url = arg.trim_prefix("--lobby-url=")
	if url.is_empty():
		return DEFAULT_URL
	if url.begins_with("wss://") or (debug and url.begins_with("ws://")):
		var address := url.substr(url.find("://") + 3).trim_suffix("/")
		if not address.is_empty() and not ["/", "@", "?", "#", " ", "\n", "\r", "\t"].any(func(c): return address.contains(c)):
			return url
	return ""


func open(url: String) -> void:
	close()
	state = State.CONNECTING
	_connect_by = Time.get_ticks_msec() + DEADLINE_MS
	if not url.is_empty():
		# The native extension loads Godot's certificate bundle setting at project startup.
		_client = SDK.Client.new(url)
		if _client._native:
			_room = _client.join_or_create("directory")
		if _room:
			_room.set_reconnection_options({"enabled": false})
			_room.joined.connect(_refresh_session.bind(_session))
			_room.message_received.connect(_on_message.bind(_session))
			_room.error.connect(_lost.bind(_session))
			_room.left.connect(_lost.bind(_session))
			_room.dropped.connect(_lost.bind(_session))
	changed.emit()


func close() -> void:
	cancel_request()
	_session += 1
	var room := _room
	_room = null
	_client = null
	if room:
		room.leave()
	state = State.CLOSED
	rooms.clear()
	create_available = false
	create_code = ""


func tick(now: int) -> void:
	if state == State.CONNECTING and now >= _connect_by:
		_offline()
	elif _request_by > 0 and now >= _request_by:
		cancel_request()
		rejected.emit("reservation_timeout")


func refresh() -> void:
	if state == State.ONLINE:
		_refresh_session(_session)


func _refresh_session(session: int) -> void:
	if session == _session and _room and _room.connected:
		_room.send_message("list", {})


func request(room_id: String, player_name: String, role: int) -> bool:
	if state != State.ONLINE or not request_id.is_empty() or not _room.connected:
		return false
	if room_id.is_empty():
		if not create_available:
			return false
	elif not rooms.get(room_id, {}).get("joinable", false):
		refresh()
		return false
	request_id = Wire.random_hex(16)
	if request_id.is_empty():
		return false
	_target = room_id
	_request_by = Time.get_ticks_msec() + RESERVATION_MS
	var data := {"requestId": request_id, "name": player_name, "role": role,
		"protocol": MatchWorker.PROTOCOL, "clientBuild": MatchWorker.CLIENT_BUILD}
	if not room_id.is_empty():
		data.roomId = room_id
	_room.send_message("create" if room_id.is_empty() else "join", data)
	return true


func cancel_request() -> void:
	if not request_id.is_empty() and _room and _room.connected:
		_room.send_message("cancel", {"requestId": request_id})
	finish_request()


func finish_request() -> void:
	request_id = ""
	_target = ""
	_request_by = 0


func _lost(_code: int, _reason: String, session: int) -> void:
	if session == _session and state in [State.CONNECTING, State.ONLINE, State.DEGRADED]:
		_offline()


func _offline() -> void:
	var pending := _request_by > 0
	finish_request()
	close()
	state = State.OFFLINE
	changed.emit()
	if pending:
		rejected.emit("lobby_offline")


func _on_message(kind: Variant, data: Variant, session: int) -> void:
	if session != _session or not data is Dictionary:
		return
	if kind == "rooms":
		if not data.get("rooms") is Array or data.rooms.size() > 3 or not data.get("createAvailable") is bool:
			return
		var listed := {}
		for entry in data.rooms:
			if not entry is Dictionary or not Wire.token(entry.get("roomId")) or not entry.get("name") is String \
					or not Wire.integer(entry.get("humans"), 0, 5) or entry.get("capacity") != 5 \
					or not Wire.integer(entry.get("mode"), 0, 2) or not Wire.integer(entry.get("available"), 0, 5) \
					or entry.get("state") not in ["waiting", "playing", "timeout", "result", "recovering"] \
					or not entry.get("joinable") is bool:
				return
			listed[entry.roomId] = {"name": entry.name.substr(0, 16), "humans": int(entry.humans),
				"mode": int(entry.mode), "state": entry.state, "available": int(entry.available),
				"joinable": entry.joinable and entry.state == "waiting" and entry.available > 0 and entry.humans < 5}
		state = State.DEGRADED if data.get("createCode") == "degraded" else State.ONLINE
		rooms = listed
		create_available = data.createAvailable
		create_code = str(data.createCode) if data.get("createCode") in ["server_full", "server_draining", "degraded"] else ""
		changed.emit()
	elif not request_id.is_empty() and data.get("requestId") == request_id:
		if kind == "room_started" or kind == "admission_error":
			finish_request()
			rejected.emit("room_started" if kind == "room_started" else str(data.get("code", "")))
		elif kind == "admission" and _request_by > 0:
			if not _target.is_empty() and data.get("roomId") != _target:
				cancel_request()
				rejected.emit("invalid_assignment")
			else:
				_request_by = 0
				assigned.emit(data)
