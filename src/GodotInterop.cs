using Godot;
using FileAccess = Godot.FileAccess;
using System.Collections;
using System.Collections.Concurrent;
using System.Dynamic;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;

namespace HoldTilDawn;

/// <summary>
/// Converts the simulation's flexible records to Godot Variants at engine/network boundaries.
/// No GDScript is loaded or evaluated by this adapter.
/// </summary>
public static class Gd
{
    // Record fields are read and written by name thousands of times a tick (E.Pos, E.NavT...): each name is
    // converted once and remembered, not run through string splitting or a regex on every access.
    static readonly ConcurrentDictionary<string, string> PascalNames = new(), SnakeNames = new();
    const int NAME_CACHE = 20000;
    public static string Pascal(string value)
    {
        if (PascalNames.TryGetValue(value, out var Known)) return Known;
        string Name = value == "t" ? "TimeLeft" : value.All(c => !char.IsLetter(c) || char.IsUpper(c)) ? value : string.Concat(value.Trim('_').Split('_').Select(s => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..]));
        if (PascalNames.Count < NAME_CACHE) PascalNames[value] = Name;
        return Name;
    }
    public static string Snake(string value)
    {
        if (SnakeNames.TryGetValue(value, out var Known)) return Known;
        string Name = Regex.Replace(value, "(?<=[a-z0-9])([A-Z])", "_$1").ToLowerInvariant();
        if (SnakeNames.Count < NAME_CACHE) SnakeNames[value] = Name;
        return Name;
    }
    public static bool IsNumber(object value) => value is byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal;
    public static int Int(object value) => value is string s ? int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i) ? i : 0 : (int)Convert.ToDouble(value, CultureInfo.InvariantCulture);
    public static float Float(object value) => Convert.ToSingle(value, CultureInfo.InvariantCulture);
    public static bool Truth(object value) => value switch { null => false, bool b => b, string s => s.Length > 0, GArray a => a.Count > 0, GDict d => d.Count > 0, _ when IsNumber(value) => Convert.ToDouble(value) != 0, _ => true };
    public static bool Equal(object a, object b) => IsNumber(a) && IsNumber(b) ? Convert.ToDouble(a) == Convert.ToDouble(b) : Equals(a, b);
    public static int ValueHash(object value) => IsNumber(value) ? Convert.ToDouble(value).GetHashCode() : value?.GetHashCode() ?? 0;
    public static string Str(object value) => value switch { null => "<null>", float f => f.ToString(CultureInfo.InvariantCulture), double d => d.ToString(CultureInfo.InvariantCulture), bool b => b ? "true" : "false", _ => value.ToString() };
    public static bool IsType(object value, Type type) => type.IsInstanceOfType(Unwrap(value));
    public static object Unwrap(object value) => value is NativeObject n ? n.Target : value;
    public static dynamic Wrap(object value) => value is Variant variant ? FromVariant(variant) : value is GodotObject obj && value is not GameSim && value is not WorldGeometry && value is not GameplaySettings && value is not AiSettings && value is not WorldSettings && value is not DisplaySettings ? new NativeObject(obj) : value;
    public static dynamic Load(string path) => Wrap(GD.Load(path));

    public static Variant ToVariant(object value)
    {
        value = Unwrap(value);
        return value switch
        {
            null => default, Variant v => v,
            bool v => v, byte v => v, int v => v, uint v => v, long v => v, ulong v => v,
            float v => v, double v => v, decimal v => (long)v, string v => v, StringName v => v,
            Vector2 v => v, Vector2I v => v, Vector3 v => v, Vector3I v => v, Rect2 v => v, Rect2I v => v,
            Color v => v, Transform3D v => v, Basis v => v, Aabb v => v,
            GodotObject v => v,
            Godot.Collections.Array v => v, Godot.Collections.Dictionary v => v,
            byte[] v => v, int[] v => v, long[] v => v, float[] v => v, double[] v => v, string[] v => v, Vector2[] v => v, Color[] v => v,
            GArray v => new Godot.Collections.Array(v.Select(ToVariant).ToArray()),
            GDict v => ToDictionary(v),
            Enum v => Convert.ToInt64(v),
            _ => throw new ArgumentException($"Unsupported Godot value: {value.GetType()}")
        };
    }
    private static Godot.Collections.Dictionary ToDictionary(GDict source)
    {
        var result = new Godot.Collections.Dictionary();
        foreach (var pair in source.Entries) result[ToVariant(pair.Key)] = ToVariant(pair.Value);
        return result;
    }
    public static dynamic FromVariant(object value) => value is Variant v ? FromVariant(v) : value is Godot.Collections.Dictionary or Godot.Collections.Array ? FromVariant(ToVariant(value)) : value;
    public static dynamic FromVariant(Variant value)
    {
        if (value.VariantType == Variant.Type.Array)
        {
            var result = new GArray();
            foreach (var item in value.AsGodotArray()) result.Add(FromVariant(item));
            return result;
        }
        if (value.VariantType == Variant.Type.Dictionary)
        {
            var result = new GDict();
            foreach (var pair in value.AsGodotDictionary()) result.Add(FromVariant(pair.Key), FromVariant(pair.Value));
            return result;
        }
        if (value.VariantType == Variant.Type.Float) return value.AsSingle();
        if (value.VariantType == Variant.Type.Int) { long n = value.AsInt64(); return n >= int.MinValue && n <= int.MaxValue ? (object)(int)n : n; }
        return Wrap(value.Obj);
    }
    public static dynamic Native(object target, string method, params object[] args)
    {
        var obj = (GodotObject)Unwrap(target);
        return FromVariant(obj.Call(method, args.Select(ToVariant).ToArray()));
    }
    public static void SetNative(GodotObject obj, string name, object value) => obj.Set(name, ToVariant(value));
    public static dynamic EnumValue(Type owner, string name, long value) => value;
    private static readonly ConcurrentDictionary<(Type, string), MethodInfo[]> staticMethods = new();
    public static dynamic Static(Type owner, string method, params object[] args)
    {
        string name = method.Replace("_", "");
        var candidates = staticMethods.GetOrAdd((owner, method), k => owner.GetMethods(BindingFlags.Public | BindingFlags.Static).Where(m => m.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).ToArray());
        foreach (var candidate in candidates.OrderBy(m => m.GetParameters().Length))
        {
            var ps = candidate.GetParameters();
            if (args.Length > ps.Length || ps.Skip(args.Length).Any(p => !p.HasDefaultValue)) continue;
            var converted = new object[ps.Length];
            try
            {
                for (int i = 0; i < ps.Length; i++) converted[i] = i < args.Length ? ConvertArgument(args[i], ps[i].ParameterType) : ps[i].DefaultValue;
            }
            catch (Exception e) when (e is InvalidCastException or ArgumentException or FormatException) { continue; }
            return Wrap(candidate.Invoke(null, converted));
        }
        throw new MissingMethodException(owner.FullName, method + "(" + string.Join(",", args.Select(a => a?.GetType().Name)) + ")");
    }
    private static object ConvertArgument(object value, Type type)
    {
        value = Unwrap(value);
        if (type == typeof(Variant)) return ToVariant(value);
        if (value == null || type.IsInstanceOfType(value)) return value;
        if (type.IsEnum) return Enum.ToObject(type, Convert.ToInt64(value));
        if (type == typeof(StringName)) return new StringName(Str(value));
        if (type.IsArray)
        {
            var items = Iter(value).ToArray();
            var array = Array.CreateInstance(type.GetElementType(), items.Length);
            for (int i = 0; i < items.Length; i++) array.SetValue(ConvertArgument(items[i], type.GetElementType()), i);
            return array;
        }
        return Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
    }
    public static void Connect(object source, string signal, Action<dynamic[]> handler)
    {
        var target = (GodotObject)Unwrap(source);
        bool oneArg = signal is "peer_disconnected" or "item_selected";
        var callable = oneArg ? Callable.From<Variant>(v => handler(new dynamic[] { FromVariant(v) })) : Callable.From(() => handler(Array.Empty<dynamic>()));
        target.Connect(signal, callable);
    }

    public static dynamic At(object source, object index) => source switch
    {
        GArray a => a[index], GDict d => d[index], string s => s[Int(index)].ToString(),
        Vector2 v => v[Int(index)], Vector2I v => v[Int(index)], Vector3 v => v[Int(index)],
        Array a => a.GetValue(Int(index)),
        _ => throw new ArgumentException($"Cannot index {source?.GetType()}")
    };
    public static IEnumerable<dynamic> Iter(object source)
    {
        if (IsNumber(source)) { for (int i = 0; i < Int(source); i++) yield return i; yield break; }
        if (source is string s) { foreach (char c in s) yield return c.ToString(); yield break; }
        foreach (var item in (IEnumerable)source) yield return item;
    }
    public static GArray Range(object end) => Range(0, end, 1);
    public static GArray Range(object start, object end) => Range(start, end, 1);
    public static GArray Range(object start, object end, object step)
    {
        int a = Int(start), b = Int(end), d = Int(step);
        if (d == 0) throw new ArgumentException("Range step cannot be zero");
        var result = new GArray();
        for (int i = a; d > 0 ? i < b : i > b; i += d) result.Add(i);
        return result;
    }
    public static int Size(object source) => source switch { GArray a => a.Count, GDict d => d.Count, string s => s.Length, Array a => a.Length, _ => Int(Native(source, "size")) };
    public static dynamic Length(object source) => source switch { Vector2 v => v.Length(), Vector3 v => v.Length(), _ => Size(source) };
    public static bool IsEmpty(object source) => Size(source) == 0;
    public static bool Contains(object source, object value) => source is GDict d ? d.Has(value) : source is string s ? s.Contains(Str(value), StringComparison.Ordinal) : Iter(source).Any(x => Equal(x, value));
    public static bool Has(object source, object key) => Contains(source, key);
    public static dynamic Get(object source, object key, object fallback = null) => source is GDict d ? d.Get(key, fallback) : throw new ArgumentException("Get requires a dictionary");
    public static GArray Keys(GDict source) => source.Keys();
    public static GArray Values(GDict source) => source.Values();
    public static void Append(GArray source, object value) => source.Add(value);
    public static void AppendArray(GArray source, object values) { foreach (var item in Iter(values)) source.Add(item); }
    public static void Clear(object source) { if (source is GArray a) a.Clear(); else ((GDict)source).Clear(); }
    public static void Erase(object source, object key) { if (source is GDict d) d.Erase(key); else { var a = (GArray)source; int i = Find(a, key); if (i >= 0) a.RemoveAt(i); } }
    public static GArray Filter(object source, Func<dynamic, dynamic> predicate) => new(Iter(source).Where(x => Truth(predicate(x))).Cast<object>().ToArray());
    public static GArray Map(object source, Func<dynamic, dynamic> transform) => new(Iter(source).Select(x => (object)transform(x)).ToArray());
    public static bool Any(object source, Func<dynamic, dynamic> predicate) => Iter(source).Any(x => Truth(predicate(x)));
    public static bool All(object source, Func<dynamic, dynamic> predicate) => Iter(source).All(x => Truth(predicate(x)));
    public static dynamic Duplicate(object source, bool deep = false)
    {
        if (source is GArray a) return new GArray(a.Select(x => deep && x is GArray or GDict ? (object)Duplicate(x, true) : (object)x).ToArray());
        if (source is GDict d) { var copy = new GDict(); foreach (var p in d.Entries) copy.Add(p.Key, deep && p.Value is GArray or GDict ? Duplicate(p.Value, true) : p.Value); return copy; }
        return Native(source, "duplicate");
    }
    public static void Sort(GArray source) => SortCustom(source, (a,b) => Comparer.DefaultInvariant.Compare(a,b) < 0);
    public static void SortCustom(GArray source, Func<dynamic, dynamic, dynamic> before)
    {
        var ordered = source.Cast<object>().ToList();
        ordered.Sort((a,b) => Truth(before(a,b)) ? -1 : Truth(before(b,a)) ? 1 : 0);
        source.Clear(); foreach (var item in ordered) source.Add(item);
    }
    public static int Find(object source, object item) { int i=0; foreach (var value in Iter(source)) { if (Equal(value,item)) return i; i++; } return -1; }
    public static dynamic Front(GArray source) => source[0];
    public static dynamic Back(GArray source) => source[-1];
    public static dynamic PopBack(GArray source) { var value = source[-1]; source.RemoveAt(source.Count-1); return value; }
    public static void RemoveAt(GArray source, object index) => source.RemoveAt(Int(index));
    public static void Resize(GArray source, object length) { int n = Int(length); while (source.Count<n) source.Add(0); while (source.Count>n) source.RemoveAt(source.Count-1); }
    public static void Fill(GArray source, object value) { for (int i=0;i<source.Count;i++) source[i]=value; }
    public static void Reverse(GArray source) => source.Reverse();
    public static void Shuffle(GArray source)
    {
        for (int i = source.Count - 1; i > 0; i--)
        {
            int j = GD.RandRange(0, i);
            object value = source[i]; source[i] = source[j]; source[j] = value;
        }
    }
    public static void Merge(GDict source, GDict other, bool overwrite = false) { foreach (var p in other.Entries) if (overwrite || !source.Has(p.Key)) source[p.Key]=p.Value; }
    public static GArray Slice(GArray source, object begin, object end = null) => new(Iter(source).Skip(Int(begin)).Take(end == null ? source.Count : Int(end)-Int(begin)).Cast<object>().ToArray());

    public static bool BeginsWith(string value, string prefix) => value.StartsWith(prefix, StringComparison.Ordinal);
    public static bool EndsWith(string value, string suffix) => value.EndsWith(suffix, StringComparison.Ordinal);
    public static string Substr(string value, object start, object length = null) { int i = Math.Clamp(Int(start),0,value.Length); return value.Substring(i, length == null ? value.Length-i : Math.Min(Int(length),value.Length-i)); }
    public static GArray Split(string value, string separator, bool allowEmpty = true, int maxSplit = 0) => new(value.Split(new[]{separator}, maxSplit>0 ? maxSplit+1 : int.MaxValue, allowEmpty ? StringSplitOptions.None : StringSplitOptions.RemoveEmptyEntries).Cast<object>().ToArray());
    public static GArray Rsplit(string value, string separator, bool allowEmpty = true, int maxSplit = 0)
    {
        if (maxSplit==0) return Split(value,separator,allowEmpty);
        var parts = new List<object>(); int end=value.Length;
        for (int i=0;i<maxSplit;i++) { int at=value.LastIndexOf(separator,end-1,StringComparison.Ordinal); if(at<0) break; parts.Insert(0,value[(at+separator.Length)..end]); end=at; }
        parts.Insert(0,value[..end]); return new(parts.ToArray());
    }
    public static int ToInt(string value) => Int(value);
    public static float ToFloat(string value) => float.TryParse(value,NumberStyles.Float,CultureInfo.InvariantCulture,out float f)?f:0;
    public static string StripEdges(string value) => value.Trim();
    public static string PathJoin(string a, string b) => a.TrimEnd('/','\\')+"/"+b;
    public static string GetFile(string value) => value.Replace('\\','/').Split('/')[^1];
    public static string GetBasename(string value) { int at=value.LastIndexOf('.'); return at<0?value:value[..at]; }
    public static string Join(string separator, object values) => string.Join(separator,Iter(values).Select(x=>Str(x)));
    public static string Format(string format, object args)
    {
        var values=args is GArray a ? a.Cast<object>().ToArray() : new[]{args}; int index=0;
        return Regex.Replace(format,@"%%|%([0-9]*)(?:\.([0-9]+))?([sdf])",m=>
        {
            if(m.Value=="%%") return "%";
            object value=values[index++]; string text=m.Groups[3].Value switch { "d"=>Math.Truncate(Convert.ToDouble(value)).ToString(CultureInfo.InvariantCulture), "f"=>Convert.ToDouble(value).ToString("F"+(m.Groups[2].Success?m.Groups[2].Value:"6"),CultureInfo.InvariantCulture), _=>Str(value) };
            return m.Groups[1].Length>0 ? text.PadLeft(int.Parse(m.Groups[1].Value),m.Groups[1].Value[0]=='0'?'0':' ') : text;
        });
    }

    public static Vector2 MakeVector2(object x, object y) => new(Float(x),Float(y));
    public static Vector2 MakeVector2(Vector2I v) => v;
    public static Vector2 MakeVector2(Vector2 v) => v;
    public static Vector2I MakeVector2I(object x, object y) => new(Int(x),Int(y));
    public static Vector2I MakeVector2I(Vector2 v) => (Vector2I)v;
    public static Vector2I MakeVector2I(Vector2I v) => v;
    public static Vector3 MakeVector3(object x,object y,object z) => new(Float(x),Float(y),Float(z));
    public static Rect2 MakeRect2(object x,object y,object w,object h) => new(Float(x),Float(y),Float(w),Float(h));
    public static Rect2 MakeRect2(Vector2 position,Vector2 size) => new(position,size);
    public static Rect2I MakeRect2I(object x,object y,object w,object h) => new(Int(x),Int(y),Int(w),Int(h));
    public static Rect2I MakeRect2I(Vector2I position,Vector2I size) => new(position,size);
    public static Color MakeColor(string hex) => new(hex);
    public static Color MakeColor(object r,object g,object b,object a=null) => new(Float(r),Float(g),Float(b),a==null?1:Float(a));
    public static GArray PackedVector2Array(object values=null) => values==null?new():new(Iter(values).Cast<object>().ToArray());
    public static GArray PackedByteArray(object values=null) => values==null?new():new(Iter(values).Cast<object>().ToArray());
    public static string[] PackedStringArray(object values) => Iter(values).Select(x=>(string)Str((object)x)).ToArray();
    public static float[] PackedFloat32Array(object values) => Iter(values).Select(x=>(float)Float((object)x)).ToArray();
    public static Color[] PackedColorArray(object values) => Iter(values).Select(x=>(Color)x).ToArray();
    public static float Maxf(object a,object b) => Math.Max(Float(a),Float(b));
    public static float Minf(object a,object b) => Math.Min(Float(a),Float(b));
    public static int Maxi(object a,object b) => Math.Max(Int(a),Int(b));
    public static int Mini(object a,object b) => Math.Min(Int(a),Int(b));
    public static int Clampi(object a,object lo,object hi) => Math.Clamp(Int(a),Int(lo),Int(hi));
    public static float Clampf(object a,object lo,object hi) => Math.Clamp(Float(a),Float(lo),Float(hi));
    public static float Absf(object a) => Math.Abs(Float(a));
    public static long Absi(object a) => Math.Abs(Convert.ToInt64(a));
    public static int Ceili(object a) => (int)Math.Ceiling(Float(a));
    public static int Floori(object a) => (int)Math.Floor(Float(a));
    public static int Roundi(object a) => (int)Math.Round(Float(a),MidpointRounding.AwayFromZero);
    public static float Roundf(object a) => Roundi(a);
    public static float Signf(object a) => Math.Sign(Float(a));
    public static float Lerpf(object a,object b,object weight) => Mathf.Lerp(Float(a),Float(b),Float(weight));
    public static float Snappedf(object a,object step) => Mathf.Snapped(Float(a),Float(step));
    public static float Sqrt(object a) => MathF.Sqrt(Float(a));
    public static float Sin(object a) => MathF.Sin(Float(a));
    public static float Cos(object a) => MathF.Cos(Float(a));
    public static float Exp(object a) => MathF.Exp(Float(a));
    public static float Pow(object a,object b) => MathF.Pow(Float(a),Float(b));
    public static float Atan2(object a,object b) => MathF.Atan2(Float(a),Float(b));
    public static float Fmod(object a,object b) => Float(a)%Float(b);
    public static float MoveToward(object a,object b,object delta) => Mathf.MoveToward(Float(a),Float(b),Float(delta));
    public static bool IsFinite(object a) => float.IsFinite(Float(a));
    public static float DegToRad(object a) => Mathf.DegToRad(Float(a));
    public static int Wrapi(object a,object lo,object hi) => Mathf.Wrap(Int(a),Int(lo),Int(hi));
}

/// <summary>Uses Godot's own Variant binding for native properties and method defaults.</summary>
internal sealed class NativeObject : DynamicObject
{
    public GodotObject Target { get; }
    private readonly Dictionary<string,string> properties;
    private static readonly ConcurrentDictionary<Type,Dictionary<string,string>> propertyMaps = new();
    public NativeObject(GodotObject target)
    {
        Target=target;
        properties=propertyMaps.GetOrAdd(target.GetType(),_=>target.GetPropertyList().GroupBy(p=>Gd.Pascal(p["name"].AsString())).ToDictionary(g=>g.Key,g=>g.Last()["name"].AsString()));
    }
    public override bool TryGetMember(GetMemberBinder binder,out object result)
    {
        string name=properties.GetValueOrDefault(binder.Name,Gd.Snake(binder.Name));
        result=Gd.FromVariant(Target.Get(name));return true;
    }
    public override bool TrySetMember(SetMemberBinder binder,object value)
    {
        Target.Set(properties.GetValueOrDefault(binder.Name,Gd.Snake(binder.Name)),Gd.ToVariant(value));return true;
    }
    public override bool TryInvokeMember(InvokeMemberBinder binder,object[] args,out object result)
    {
        result=Gd.Native(Target,Gd.Snake(binder.Name),args);return true;
    }
}
