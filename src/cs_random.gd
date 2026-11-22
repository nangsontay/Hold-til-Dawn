class_name CsRandom
extends RefCounted
## .NET's seeded System.Random (Knuth's subtractive generator), so the island built from fixed seeds
## (landmarks, farms, exploration props) comes out exactly as it did in the C# version.

const MBIG := 2147483647
const MSEED := 161803398

var _seed_array: PackedInt64Array
var _inext := 0
var _inextp := 21


func _init(seed_value: int = 0) -> void:
	_seed_array.resize(56)
	var subtraction := MBIG if seed_value == -2147483648 else absi(seed_value)
	var mj := MSEED - subtraction
	_seed_array[55] = mj
	var mk := 1
	var ii := 0
	for i in range(1, 55):
		ii += 21
		if ii >= 55:
			ii -= 55
		_seed_array[ii] = mk
		mk = mj - mk
		if mk < 0:
			mk += MBIG
		mj = _seed_array[ii]
	for k in range(1, 5):
		for i in range(1, 56):
			var n := i + 30
			if n >= 55:
				n -= 55
			var v := _seed_array[i] - _seed_array[1 + n]
			if v < 0:
				v += MBIG
			_seed_array[i] = v
	_inext = 0
	_inextp = 21


func _internal_sample() -> int:
	var loc_inext := _inext + 1
	if loc_inext >= 56:
		loc_inext = 1
	var loc_inextp := _inextp + 1
	if loc_inextp >= 56:
		loc_inextp = 1
	var ret := _seed_array[loc_inext] - _seed_array[loc_inextp]
	if ret == MBIG:
		ret -= 1
	if ret < 0:
		ret += MBIG
	_seed_array[loc_inext] = ret
	_inext = loc_inext
	_inextp = loc_inextp
	return ret


func _sample() -> float:
	return _internal_sample() * (1.0 / MBIG)


## Next(), Next(max) and Next(min, max) of System.Random.
func next(a = null, b = null) -> int:
	if a == null:
		return _internal_sample()
	if b == null:
		return int(_sample() * a)
	var span: int = b - a
	if span <= MBIG:
		return int(_sample() * span) + a
	return int(_large_sample() * span) + a


func _large_sample() -> float:
	var result := _internal_sample()
	if _internal_sample() % 2 == 0:
		result = -result
	var d := float(result)
	d += (MBIG - 1)
	d /= 2.0 * MBIG - 1
	return d


func next_double() -> float:
	return _sample()


func next_single() -> float:
	return _sample()
