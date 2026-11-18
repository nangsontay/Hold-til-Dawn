extends SceneTree

const Wire = preload("wire.gd")
const ORDERED = MultiplayerPeer.TRANSFER_MODE_UNRELIABLE_ORDERED
const RELIABLE = MultiplayerPeer.TRANSFER_MODE_RELIABLE

var server := OS.get_environment("BENCH_ROLE") == "server"
var port := int(OS.get_environment("BENCH_PORT"))
var frames: Array[PackedInt32Array]
var peers: Array[ENetMultiplayerPeer] = []
var clients: Array[Dictionary] = []
var members := {}
var attacks := []
var live_path := OS.get_environment("BENCH_LIVE")
var current_frame := PackedInt32Array()
var frame_tick := 0
var simulation_tick := 0
var start := 0
var duration := int(float(OS.get_environment("BENCH_SECONDS")) * 1e6)
var warmup := int(float(OS.get_environment("BENCH_WARMUP")) * 1e6) if OS.has_environment("BENCH_WARMUP") else 2000000
var verify := OS.get_environment("BENCH_VERIFY") == "1"
var failed := false

func _initialize() -> void:
	Engine.max_fps = 240
	OS.low_processor_usage_mode = false
	if not OS.get_environment("BENCH_ROLE") in ["client", "server"] or port <= 0 or duration <= 0:
		fail("BENCH_ROLE, BENCH_PORT and BENCH_SECONDS are required")
		return
	if server or verify:
		frames = Wire.read_trace(OS.get_environment("BENCH_TRACE"), not live_path.is_empty())
		if frames.is_empty():
			fail("Invalid 74-entity BENCH_TRACE")
			return
	if server:
		current_frame = frames[0]
	var count := 1 if server else 5
	if not server and OS.has_environment("BENCH_CLIENTS"):
		count = int(OS.get_environment("BENCH_CLIENTS"))
	if count < 1 or count > 5:
		fail("BENCH_CLIENTS must be between 1 and 5")
		return
	for index in count:
		var peer := ENetMultiplayerPeer.new()
		var error: Error
		if server:
			peer.set_bind_ip(OS.get_environment("BENCH_BIND") if OS.has_environment("BENCH_BIND") else "127.0.0.1")
			error = peer.create_server(port, 5)
			peer.peer_connected.connect(on_join)
			peer.peer_disconnected.connect(func(id): members.erase(id))
		else:
			var host := OS.get_environment("BENCH_HOST")
			error = peer.create_client(host if host else "127.0.0.1", port)
			clients.append({"base": PackedInt32Array(), "last_tick": -1, "last_update": 0, "updates": 0, "acks": 0, "bytes": 0,
				"since": 0, "move_at": 0, "attack_at": 0, "moves": 0, "seq": 0, "sent": {}})
		if error != OK:
			fail("ENet setup: " + error_string(error))
			return
		peers.append(peer)
	start = Time.get_ticks_usec()
	if server:
		print(JSON.stringify({"ready": true, "port": port}))

func fail(message: String) -> void:
	failed = true
	printerr(message)
	quit(1)

func send(peer: ENetMultiplayerPeer, id: int, mode: int, packet: PackedByteArray) -> void:
	peer.set_target_peer(id)
	peer.transfer_mode = mode
	var error := peer.put_packet(packet)
	if error != OK:
		fail("ENet send: " + error_string(error))

func on_join(id: int) -> void:
	members[id] = true
	send(peers[0], id, RELIABLE, Wire.baseline(frames[0]))
	print(JSON.stringify({"joined": id, "port": port}))

func _process(_delta: float) -> bool:
	if failed:
		return true
	var now := Time.get_ticks_usec()
	if now - start >= duration:
		if not server:
			for index in clients.size():
				var client := clients[index]
				print(JSON.stringify({"client": index, "updates": client.updates, "acks": client.acks, "received_app_bytes": client.bytes}))
				if not client.updates or not client.acks:
					fail("Client received no updates or no combat acknowledgments")
		return true
	for index in peers.size():
		var peer := peers[index]
		if peer.get_connection_status() == MultiplayerPeer.CONNECTION_DISCONNECTED:
			fail("ENet disconnected before timeout")
			return true
		peer.poll()
		while peer.get_available_packet_count():
			var sender := peer.get_packet_peer()
			var packet := peer.get_packet()
			if packet.is_empty():
				fail("Empty ENet packet")
				return true
			if server:
				if packet[0] == Wire.ATTACK and packet.size() == 6 and packet[5] == 5:
					attacks.append([sender, packet.decode_u32(1)])
				elif packet[0] != Wire.MOVE or packet.size() != 7:
					fail("Invalid input")
			else:
				receive(clients[index], packet, index)
		if not server and clients[index].since:
			inputs(peer, clients[index], now)
	if server:
		var elapsed_ticks := floori((now - start) * 60.0 / 1e6)
		while simulation_tick < elapsed_ticks:
			simulation_tick += 1
			if simulation_tick % 3 == 0:
				frame_tick += 1
				if live_path:
					current_frame = Wire.read_live(live_path)
					if current_frame.is_empty():
						fail("Invalid 74-entity live simulation frame")
						return true
				else:
					current_frame = frames[frame_tick % frames.size()]
				if not members.is_empty():
					send(peers[0], 0, ORDERED, Wire.update(frame_tick, current_frame, frames[0]))
			for attack in attacks:
				if members.has(attack[0]):
					var ack := PackedByteArray([Wire.ACK, 0, 0, 0, 0, 0, 0])
					ack.encode_u32(1, attack[1])
					ack.encode_u16(5, current_frame[5 * 3 + 2])
					send(peers[0], attack[0], RELIABLE, ack)
			attacks.clear()
	for peer in peers:
		if peer.host != null:
			peer.host.flush()
	return failed

func receive(client: Dictionary, packet: PackedByteArray, index: int) -> void:
	var now := Time.get_ticks_usec()
	client.bytes += packet.size()
	match int(packet[0]):
		Wire.BASELINE:
			client.base = Wire.decode_base(packet)
			if client.base.is_empty() or (verify and client.base != frames[0]):
				fail("Invalid ENet baseline")
				return
			client.since = now
			client.move_at = now
			client.attack_at = now + 47000 * index
			print(JSON.stringify({"joined_client": index, "port": port}))
		Wire.UPDATE:
			if not client.since:
				return
			var decoded := Wire.decode_update(packet, client.base)
			if decoded.is_empty():
				fail("Invalid ENet update")
				return
			var tick := packet.decode_u32(1)
			if tick <= client.last_tick or (verify and not live_path and decoded != frames[tick % frames.size()]):
				fail("ENet update sequence or trace mismatch")
				return
			client.last_tick = tick
			client.updates += 1
			if client.last_update and now - int(client.since) >= warmup:
				var age_ms := (Time.get_unix_time_from_system() * 1e6 - packet.decode_u64(5)) / 1000.0
				print(JSON.stringify({"age_ms": age_ms, "gap_ms": (now - int(client.last_update)) / 1000.0, "client": index, "tick": tick}))
			client.last_update = now
		Wire.ACK:
			if packet.size() != 7:
				fail("Invalid combat ack")
				return
			var seq := packet.decode_u32(1)
			if not client.sent.has(seq):
				fail("No matching attack")
				return
			var sent: int = client.sent[seq]
			if sent - int(client.since) >= warmup:
				print(JSON.stringify({"rtt_ms": (now - sent) / 1000.0, "client": index, "seq": seq}))
			client.sent.erase(seq)
			client.acks += 1
		_:
			fail("Unknown ENet message")

func inputs(peer: ENetMultiplayerPeer, client: Dictionary, now: int) -> void:
	if now >= int(client.move_at):
		var move := PackedByteArray([Wire.MOVE, 0, 0, 0, 0, 0, 0])
		move.encode_u32(1, client.moves)
		move.encode_s8(5, 1 if int(client.moves) % 40 < 20 else -1)
		send(peer, 1, ORDERED, move)
		client.moves += 1
		client.move_at += 50000
	if now >= int(client.attack_at):
		var attack := PackedByteArray([Wire.ATTACK, 0, 0, 0, 0, 5])
		client.seq += 1
		attack.encode_u32(1, client.seq)
		client.sent[client.seq] = Time.get_ticks_usec()
		send(peer, 1, RELIABLE, attack)
		client.attack_at += 250000
