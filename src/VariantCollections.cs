using Godot;
using System.Collections;
using System.Dynamic;

namespace HoldTilDawn;

/// <summary>Snapshot list with value equality, matching Godot Array semantics.</summary>
public sealed class GArray : IEnumerable<object>
{
    private readonly List<object> items;
    public GArray(params object[] values) => items = new(values);
    public dynamic this[object index]
    {
        get => items[Index(index)];
        set => items[Index(index)] = value;
    }
    private int Index(object index)
    {
        int i = Gd.Int(index);
        return i < 0 ? items.Count + i : i;
    }
    public int Count => items.Count;
    public void Add(object value) => items.Add(value);
    public void Clear() => items.Clear();
    public void RemoveAt(int index) => items.RemoveAt(index);
    public void Reverse() => items.Reverse();
    public IEnumerator<dynamic> GetEnumerator() => items.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    public override bool Equals(object obj) => obj is GArray other && items.Count == other.Count && items.Zip(other.items).All(p => Gd.Equal(p.First, p.Second));
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var item in items) hash.Add(Gd.ValueHash(item));
        return hash.ToHashCode();
    }
    public static bool operator ==(GArray a, GArray b) => ReferenceEquals(a, b) || a is not null && a.Equals(b);
    public static bool operator !=(GArray a, GArray b) => !(a == b);
    public static GArray operator +(GArray a, GArray b) => new(a.items.Concat(b.items).ToArray());
    public override string ToString() => "[" + string.Join(", ", items.Select(Gd.Str)) + "]";
}

/// <summary>Named simulation records and dictionaries used by the snapshot protocol.</summary>
public sealed class GDict : DynamicObject, IEnumerable<object>
{
    private readonly Dictionary<object, object> values = new();
    private readonly Dictionary<string, object> members = new();
    private static object Key(object key) => Gd.IsNumber(key) ? Convert.ToDecimal(key) : key;
    public dynamic this[object key]
    {
        get => values[Key(key)];
        set
        {
            values[Key(key)] = value;
            if (key is string name) members[Gd.Pascal(name)] = name;
        }
    }
    public int Count => values.Count;
    public void Add(object key, object value) => this[key] = value;
    public bool Has(object key) => values.ContainsKey(Key(key));
    public dynamic Get(object key, object fallback = null) => values.TryGetValue(Key(key), out var value) ? value : fallback;
    public bool Erase(object key) => values.Remove(Key(key));
    public void Clear() { values.Clear(); members.Clear(); }
    public GArray Keys() => new(values.Keys.ToArray());
    public GArray Values() => new(values.Values.ToArray());
    public IEnumerable<KeyValuePair<object, object>> Entries => values;
    private object MemberKey(string name)
    {
        return members.TryGetValue(name, out var key) ? key : Gd.Snake(name);
    }
    public override bool TryGetMember(GetMemberBinder binder, out object result)
    {
        result = Get(MemberKey(binder.Name));
        return true;
    }
    public override bool TrySetMember(SetMemberBinder binder, object value)
    {
        this[MemberKey(binder.Name)] = value;
        return true;
    }
    public IEnumerator<dynamic> GetEnumerator() => values.Keys.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    public override string ToString() => "{" + string.Join(", ", values.Select(p => Gd.Str(p.Key) + ": " + Gd.Str(p.Value))) + "}";
}
