extends RefCounted
## Length-prefixed loopback control. See multiplayer/worker-control.schema.json.

const VERSION := "1"
const MAX_FRAME := 8192
const DEADLINE_MS := 5000
const DOMAIN := "hold-til-dawn/control"
const SESSION_DOMAIN := "hold-til-dawn/session"

var room_id: String
var generation: String
var port: int
var control_port: int
var secret: PackedByteArray
var pid := OS.get_process_id()
var stream: StreamPeerTCP
var incoming := PackedByteArray()
var received := 0
var sent := 0
var last_seen := 0
var retry_at := 0
var status := "offline"
var worker_nonce := ""
var key := PackedByteArray()


func _init(room: String, gen: String, udp: int, tcp: int, recovery_key: PackedByteArray) -> void:
	room_id = room
	generation = gen
	port = udp
	control_port = tcp
	secret = recovery_key


static func mac(key_bytes: PackedByteArray, fields: Array) -> PackedByteArray:
	return Crypto.new().hmac_digest(HashingContext.HASH_SHA256, key_bytes, "\n".join(PackedStringArray(fields)).to_utf8_buffer())


static func equal_mac(expected: PackedByteArray, actual: PackedByteArray) -> bool:
	return expected.size() == 32 and actual.size() == 32 and Crypto.new().constant_time_compare(expected, actual)


static func random_hex(bytes: int) -> String:
	return Crypto.new().generate_random_bytes(bytes).hex_encode()


static func is_hex(value: Variant, bytes: int) -> bool:
	if not value is String or value.length() != bytes * 2:
		return false
	for c in value:
		if c not in "0123456789abcdef":
			return false
	return true


static func token(value: Variant) -> bool:
	if not value is String or value.length() < 1 or value.length() > 64:
		return false
	for c in value:
		if c not in "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_-":
			return false
	return true


static func integer(value: Variant, low: int, high: int) -> bool:
	return (value is int or value is float) and value >= low and value <= high and int(value) == value


func _fields(direction: String, counter: int, kind: String, body: String) -> Array:
	return [DOMAIN, VERSION, direction, room_id, generation, str(port), str(counter), kind, body]


func send(kind: String, data: Dictionary, signing_key := PackedByteArray()) -> bool:
	if stream == null:
		return false
	var body := Marshalls.raw_to_base64(JSON.stringify(data).to_utf8_buffer())
	var fields := _fields("worker", sent + 1, kind, body)
	var record := fields.slice(1)
	record.append(mac(key if signing_key.is_empty() else signing_key, fields).hex_encode())
	var bytes := JSON.stringify(record).to_utf8_buffer()
	if bytes.size() > MAX_FRAME:
		return false
	var frame := PackedByteArray([bytes.size() >> 24, (bytes.size() >> 16) & 255, (bytes.size() >> 8) & 255, bytes.size() & 255])
	frame.append_array(bytes)
	# Partial writes are abandoned with this session, so backpressure cannot stall gameplay.
	var written := stream.put_partial_data(frame)
	if written[0] != OK or written[1] != frame.size():
		return false
	sent += 1
	return true


func disconnect_control(now: int) -> void:
	if stream != null:
		stream.disconnect_from_host()
	stream = null
	status = "offline"
	incoming.clear()
	key.clear()
	retry_at = now + 250


func _verify(record: Variant) -> Dictionary:
	if not record is Array or record.size() != 9 or record.any(func(v): return not v is String):
		return {}
	var kind: String = record[6]
	var fields := _fields("manager", received + 1, kind, record[7])
	if record.slice(0, 8) != fields.slice(1) or not token(kind) or not is_hex(record[8], 32):
		return {}
	if not equal_mac(mac(secret if status == "challenge" else key, fields), record[8].hex_decode()):
		return {}
	var body_bytes := Marshalls.base64_to_raw(record[7])
	if Marshalls.raw_to_base64(body_bytes) != record[7]:
		return {}
	var data: Variant = JSON.parse_string(body_bytes.get_string_from_utf8())
	if not data is Dictionary:
		return {}
	return {"type": kind, "data": data}


func poll(now: int, report_sequence: int) -> Array:
	var messages := []
	if stream == null:
		if now < retry_at:
			return messages
		stream = StreamPeerTCP.new()
		if stream.connect_to_host("127.0.0.1", control_port) != OK:
			disconnect_control(now)
			return messages
		status = "connecting"
		last_seen = now
	stream.poll()
	var tcp_status := stream.get_status()
	if tcp_status in [StreamPeerTCP.STATUS_NONE, StreamPeerTCP.STATUS_ERROR] or now - last_seen >= DEADLINE_MS:
		disconnect_control(now)
		return messages
	if tcp_status != StreamPeerTCP.STATUS_CONNECTED:
		return messages
	if status == "connecting":
		stream.set_no_delay(true)
		sent = 0
		received = 0
		worker_nonce = random_hex(32)
		if worker_nonce.length() != 64 or not send("hello", {"pid": pid, "nonce": worker_nonce, "reportSequence": report_sequence}, secret):
			disconnect_control(now)
			return messages
		status = "challenge"
	# Read and authenticate a bounded amount per frame, including when TCP coalesces records.
	var available := mini(stream.get_available_bytes(), MAX_FRAME + 4 - incoming.size())
	if available > 0:
		var read := stream.get_partial_data(available)
		if read[0] != OK:
			disconnect_control(now)
			return messages
		incoming.append_array(read[1])
	while incoming.size() >= 4:
		var size := (int(incoming[0]) << 24) | (int(incoming[1]) << 16) | (int(incoming[2]) << 8) | int(incoming[3])
		if size < 1 or size > MAX_FRAME:
			return _protocol_error(now)
		if incoming.size() < size + 4:
			break
		var message := _verify(JSON.parse_string(incoming.slice(4, size + 4).get_string_from_utf8()))
		incoming = incoming.slice(size + 4)
		if message.is_empty():
			return _protocol_error(now)
		received += 1
		last_seen = now
		match status:
			"challenge":
				var data: Dictionary = message.data
				if message.type != "challenge" or data.get("workerNonce") != worker_nonce or not is_hex(data.get("managerNonce"), 32):
					return _protocol_error(now)
				var sequence: int = report_sequence
				key = mac(secret, [SESSION_DOMAIN, VERSION, room_id, generation, str(port), str(pid), str(sequence), worker_nonce, data.managerNonce])
				if not send("register", {"pid": pid, "reportSequence": sequence}):
					disconnect_control(now)
					return []
				status = "registering"
			"registering":
				if message.type != "registered":
					return _protocol_error(now)
				status = "online"
			"online":
				messages.append(message)
	return messages


func _protocol_error(now: int) -> Array:
	disconnect_control(now)
	return [{"type": "protocol_error", "data": {}}]
