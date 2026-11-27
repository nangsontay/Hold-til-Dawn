class_name Cs
extends RefCounted
## Small helpers that keep the GDScript port behaving like the C# version it came from
## (number formatting, sized arrays, sets stored as dictionaries, LINQ-style helpers).


## Text like C#'s ToString(): whole floats print without ".0" (100.0 -> "100").
static func s(v) -> String:
	if v is float:
		if is_finite(v) and v == floorf(v) and absf(v) < 1e15:
			return str(int(v))
		return str(v)
	return str(v)


static func join(sep: String, values) -> String:
	var parts := PackedStringArray()
	for v in values:
		parts.append(s(v))
	return sep.join(parts)


static func is_null_or_empty(v) -> bool:
	return v == null or str(v) == ""


## Sets an object's properties from a dictionary (C# object initialisers).
static func init(obj, props: Dictionary):
	for k in props:
		obj.set(k, props[k])
	return obj


## A C# array of n elements, all set to value.
static func arr(n: int, value = null) -> Array:
	var a := []
	a.resize(n)
	if value != null:
		a.fill(value)
	return a


## A C# array of n new instances of a struct-like class.
static func arr_new(n: int, cls) -> Array:
	var a := []
	a.resize(n)
	for i in n:
		a[i] = cls.new()
	return a


## A rectangular C# array [w, h] as nested arrays.
static func arr2(w: int, h: int, value = null) -> Array:
	var a := []
	a.resize(w)
	for i in w:
		a[i] = arr(h, value)
	return a


static func resize0(a: Array, n: int) -> void:
	var old := a.size()
	a.resize(n)
	for i in range(old, n):
		a[i] = 0


static func array_copy(src: Array, dst: Array, n: int) -> void:
	for i in n:
		dst[i] = src[i]


## A C# 'throw': GDScript has no exceptions, so the error is logged (tests treat it as a failure) and the
## caller returns a default value.
static var failures := 0

static func fail(msg) -> Variant:
	failures += 1
	push_error("FAIL (exception): " + str(msg))
	return null


## HashSet<T> is stored as a Dictionary with true values.
static func set_of(values) -> Dictionary:
	var d := {}
	for v in values:
		d[v] = true
	return d


static func set_add(d: Dictionary, v) -> bool:
	if d.has(v):
		return false
	d[v] = true
	return true


static func set_union(d: Dictionary, values) -> void:
	for v in values:
		d[v] = true


static func set_overlaps(d: Dictionary, values) -> bool:
	for v in values:
		if d.has(v):
			return true
	return false


## Dictionary entries as [key, value] pairs (C# KeyValuePair).
static func pairs(d: Dictionary) -> Array:
	var out := []
	for k in d:
		out.append([k, d[k]])
	return out


static func try_add(d: Dictionary, k, v) -> bool:
	if d.has(k):
		return false
	d[k] = v
	return true


static func list_remove(a: Array, v) -> bool:
	var i := a.find(v)
	if i < 0:
		return false
	a.remove_at(i)
	return true


static func remove_all(a: Array, pred: Callable) -> int:
	var n := 0
	var i := a.size() - 1
	while i >= 0:
		if pred.call(a[i]):
			a.remove_at(i)
			n += 1
		i -= 1
	return n


static func remove_range(a: Array, start: int, count: int) -> void:
	for i in count:
		a.remove_at(start)


static func find_index(a: Array, pred: Callable) -> int:
	for i in a.size():
		if pred.call(a[i]):
			return i
	return -1


static func first_or_default(a, pred = null):
	for v in a:
		if pred == null or pred.call(v):
			return v
	return null


static func last_or_default(a, pred = null):
	var found = null
	for v in a:
		if pred == null or pred.call(v):
			found = v
	return found


static func sum(a) -> Variant:
	var total = 0
	for v in a:
		total += v
	return total


static func average(a) -> float:
	if a.is_empty():
		return 0.0
	return float(sum(a)) / a.size()


static func compare(a, b) -> int:
	if a < b:
		return -1
	if a > b:
		return 1
	return 0


## Sorts like List.Sort(Comparison): cmp(a, b) < 0 puts a first.
static func sort_cmp(a: Array, cmp: Callable) -> void:
	a.sort_custom(func(x, y): return cmp.call(x, y) < 0)


static func order_by(a, key: Callable, descending := false) -> Array:
	var out := Array(a).duplicate()
	if descending:
		out.sort_custom(func(x, y): return key.call(x) > key.call(y))
	else:
		out.sort_custom(func(x, y): return key.call(x) < key.call(y))
	return out


static func distinct(a) -> Array:
	var seen := {}
	var out := []
	for v in a:
		if not seen.has(v):
			seen[v] = true
			out.append(v)
	return out


static func select_i(a, f: Callable) -> Array:
	var out := []
	var i := 0
	for v in a:
		out.append(f.call(v, i))
		i += 1
	return out


static func select_many(a, f: Callable) -> Array:
	var out := []
	for v in a:
		out.append_array(f.call(v))
	return out


static func to_dict(a, key: Callable, value = null) -> Dictionary:
	var d := {}
	for v in a:
		d[key.call(v)] = v if value == null else value.call(v)
	return d


static func for_each(a, f: Callable) -> void:
	for v in a:
		f.call(v)


static func zip(a: Array, b: Array) -> Array:
	var out := []
	for i in mini(a.size(), b.size()):
		out.append([a[i], b[i]])
	return out


static func with_copy(obj, props: Dictionary):
	var c = obj.duplicate() if obj is Dictionary else obj.duplicate()
	for k in props:
		c[k] = props[k]
	return c


static func trunc(v: float) -> float:
	return float(int(v))


## Math.Round: banker's rounding (half to even), like .NET.
static func round_even(v: float) -> float:
	var r := roundf(v)
	if absf(v - floorf(v) - 0.5) < 1e-12 and fmod(r, 2.0) != 0.0:
		r -= signf(v)
	return r


static func log10(v: float) -> float:
	return log(v) / log(10.0)


static func log2(v: float) -> float:
	return log(v) / log(2.0)


static func exp2(v: float) -> float:
	return pow(2.0, v)


static func cbrt(v: float) -> float:
	return signf(v) * pow(absf(v), 1.0 / 3.0)


static func hypot(a: float, b: float) -> float:
	return sqrt(a * a + b * b)


## System.Diagnostics.Stopwatch for the perf probes.
class Stopwatch:
	var _start := 0
	var _elapsed := 0
	var _running := false

	static func start_new() -> Stopwatch:
		var w := Stopwatch.new()
		w.start()
		return w

	func start() -> void:
		if not _running:
			_start = Time.get_ticks_usec()
			_running = true

	func stop() -> void:
		if _running:
			_elapsed += Time.get_ticks_usec() - _start
			_running = false

	func restart() -> void:
		_elapsed = 0
		_start = Time.get_ticks_usec()
		_running = true

	func reset() -> void:
		_elapsed = 0
		_running = false

	func _usec() -> int:
		return _elapsed + (Time.get_ticks_usec() - _start if _running else 0)

	var elapsed_milliseconds: int:
		get:
			return _usec() / 1000

	var elapsed_ticks: int:
		get:
			return _usec() * 10

	var elapsed: TimeSpan:
		get:
			return TimeSpan.new(_usec())


## System.TimeSpan, only what the perf probes read.
class TimeSpan:
	var usec := 0

	func _init(p_usec: int = 0) -> void:
		usec = p_usec

	var total_milliseconds: float:
		get:
			return usec / 1000.0

	var total_seconds: float:
		get:
			return usec / 1000000.0


## Array.Clear: every element back to its type's default.
static func array_clear(a: Array) -> void:
	for i in a.size():
		var v = a[i]
		if v is bool:
			a[i] = false
		elif v is int:
			a[i] = 0
		elif v is float:
			a[i] = 0.0
		else:
			a[i] = null


## MemberwiseClone for script classes (inner classes need an _init without required arguments).
static func clone(obj):
	var c = obj.get_script().new()
	for p in obj.get_property_list():
		if p.usage & PROPERTY_USAGE_SCRIPT_VARIABLE:
			c.set(p.name, obj.get(p.name))
	return c


static func insert_range(a: Array, at: int, values) -> void:
	var i := at
	for v in values:
		a.insert(i, v)
		i += 1


## Rect2.HasPoint as C# computes it: false for a negative size, without Godot's error message.
static func has_point(r: Rect2, p: Vector2) -> bool:
	return p.x >= r.position.x and p.y >= r.position.y and p.x < r.position.x + r.size.x and p.y < r.position.y + r.size.y


## LINQ GroupBy: groups with .key, .items and size().
class Group:
	var key
	var items := []

	func size() -> int:
		return items.size()


static func group_by(a, key: Callable) -> Array:
	var groups := {}
	var order := []
	for v in a:
		var k = key.call(v)
		if not groups.has(k):
			var g := Group.new()
			g.key = k
			groups[k] = g
			order.append(g)
		groups[k].items.append(v)
	return order


## Array.CopyTo(dst, at): copies every element of src into dst starting at index at.
static func copy_to(src, dst: Array, at: int = 0) -> void:
	for i in src.size():
		dst[at + i] = src[i]


## C# int arithmetic wraps at 32 bits (GDScript ints are 64-bit): the value as a signed 32-bit int.
static func i32(v: int) -> int:
	v = v & 0xFFFFFFFF
	return v - 0x100000000 if v >= 0x80000000 else v


## ... and as an unsigned 32-bit int (C# uint).
static func u32(v: int) -> int:
	return v & 0xFFFFFFFF
