extends RefCounted

enum { BASELINE, UPDATE, MOVE, ATTACK, ACK }
const ENTITIES := 74

static func read_trace(path: String, baseline_only: bool) -> Array[PackedInt32Array]:
	var frames: Array[PackedInt32Array] = []
	var file := FileAccess.open(path, FileAccess.READ)
	if file == null:
		return frames
	while file.get_position() < file.get_length():
		var row = JSON.parse_string(file.get_line())
		var values := parse_frame(row)
		if values.is_empty():
			return []
		frames.append(values)
		if baseline_only:
			break
	return frames

static func read_live(path: String) -> PackedInt32Array:
	var file := FileAccess.open(path, FileAccess.READ)
	if file == null:
		return PackedInt32Array()
	return parse_frame(JSON.parse_string(file.get_as_text()))

static func parse_frame(row: Variant) -> PackedInt32Array:
	if not row is Dictionary or not row.get("entities") is Array or row.entities.size() != ENTITIES:
		return PackedInt32Array()
	var values := PackedInt32Array()
	for entity in row.entities:
		if not entity is Array or entity.size() != 3:
			return PackedInt32Array()
		for i in 3:
			var value = entity[i]
			if not (value is float or value is int) or int(value) != value:
				return PackedInt32Array()
			if (i < 2 and (value < -32768 or value > 32767)) or (i == 2 and (value < 0 or value > 65535)):
				return PackedInt32Array()
			values.append(int(value))
	return values

static func baseline(frame: PackedInt32Array) -> PackedByteArray:
	var packet := PackedByteArray([BASELINE])
	packet.resize(1 + ENTITIES * 6)
	for i in frame.size():
		packet.encode_u16(1 + 2 * i, frame[i] & 65535)
	return packet

# Every delta references the reliable initial frame, so a dropped update cannot corrupt later state.
static func update(tick: int, frame: PackedInt32Array, base: PackedInt32Array) -> PackedByteArray:
	var packet := PackedByteArray([UPDATE])
	packet.resize(13 + ENTITIES * 8)
	packet.encode_u32(1, tick)
	var offset := 13
	for entity in ENTITIES:
		var start := entity * 3
		var mask := 0
		for field in 3:
			if frame[start + field] != base[start + field]:
				mask |= 1 << field
		if mask == 0:
			continue
		packet[offset] = entity
		packet[offset + 1] = mask
		offset += 2
		for field in 3:
			if mask & (1 << field):
				packet.encode_u16(offset, frame[start + field] & 65535)
				offset += 2
	packet.resize(offset)
	packet.encode_u64(5, roundi(Time.get_unix_time_from_system() * 1e6))
	return packet

static func decode_base(packet: PackedByteArray) -> PackedInt32Array:
	var base := PackedInt32Array()
	if packet.size() != 1 + ENTITIES * 6:
		return base
	base.resize(ENTITIES * 3)
	for i in base.size():
		base[i] = packet.decode_u16(1 + 2 * i) if i % 3 == 2 else packet.decode_s16(1 + 2 * i)
	return base

static func decode_update(packet: PackedByteArray, base: PackedInt32Array) -> PackedInt32Array:
	if packet.size() < 13 or base.size() != ENTITIES * 3:
		return PackedInt32Array()
	var frame := base.duplicate()
	var offset := 13
	var last := -1
	while offset < packet.size():
		if offset + 2 > packet.size():
			return PackedInt32Array()
		var entity := int(packet[offset])
		var mask := int(packet[offset + 1])
		if entity <= last or entity >= ENTITIES or mask < 1 or mask > 7:
			return PackedInt32Array()
		offset += 2
		for field in 3:
			if mask & (1 << field):
				if offset + 2 > packet.size():
					return PackedInt32Array()
				frame[entity * 3 + field] = packet.decode_u16(offset) if field == 2 else packet.decode_s16(offset)
				offset += 2
		last = entity
	return frame
