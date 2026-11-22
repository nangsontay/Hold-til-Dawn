using Godot;
using System;
using System.Collections.Generic;
using HoldTilDawn;

// Every tree, loose rock and herb-like plant scattered over the island is a resource, never a decorative duplicate.
// A landmark's own props (src/world/WorldLandmarks.cs) are not: they stay as scenery (WorldGeometry.cs).
public static class Harvestables
{
    public static int TypeOf(string Sprite)
    {
        foreach (string Prefix in new[] { "pine_", "lm_palm_", "lm_mangrove_", "lm_log_", "lm_sticks_" })
            if (Sprite.StartsWith(Prefix, StringComparison.Ordinal)) return 0;
        foreach (string Prefix in new[] { "rock_", "crag_", "lm_shelf_", "lm_fossil_", "lm_cairn_" })
            if (Sprite.StartsWith(Prefix, StringComparison.Ordinal)) return 1;
        foreach (string Prefix in new[] { "shrub_", "reed_", "lm_broadleaf_", "lm_ginger_", "lm_fern_", "lm_flowers_", "lm_bed_", "lm_pots_" })
            if (Sprite.StartsWith(Prefix, StringComparison.Ordinal)) return 2;
        return -1;
    }

    public static string StageSprite(string Sprite, int Stage)
    {
        if (Stage == 5 && Sprite.StartsWith("node_titanium_", StringComparison.Ordinal)) return "ore_left_0";
        if (Stage == 5 && Sprite.StartsWith("node_herb_", StringComparison.Ordinal)) return "herb_cut_0";
        int Split = Sprite.LastIndexOf('_');
        return Sprite[..Split] + (Stage == 5 ? "_empty_" : "_dmg" + Stage + "_") + Sprite[(Split + 1)..];
    }

    public static float Radius(GDict Node)
    {
        object Custom = Node.Get("radius");
        return Custom != null ? Gd.Float(Custom) : Gd.Int(Node.Get("type")) switch { 0 => 8, 1 => 19.84f, 3 => 19.2f, _ => 0 };
    }

    public static GDict FromSnapshot(GArray N) => new GDict {
        { "type", N[0] }, { "pos", N[1] }, { "amount", N[2] },
        { "radius", N.Count > 4 ? (object)N[4] : null }
    };

    public readonly record struct Site(Vector2 Pos, string Sprite, int Type, float Radius)
    {
        public GDict CreateNode() => new GDict { { "type", Type }, { "pos", Pos }, { "amount", 5 }, { "sprite", Sprite }, { "radius", Radius }, { "ambient", true } };
    }
}

public partial class GameSim
{
    GArray HarvestSnapshotSource, HarvestSnapshot = new();
    readonly List<(int Type, Vector2 Pos, int Amount, string Sprite, float Radius)> HarvestSnapshotState = new();

    // Most resources are unchanged between snapshots; reuse their immutable records instead of allocating
    // hundreds of dictionaries/arrays every 50 ms now that the whole forest can be gathered.
    public GArray SnapshotNodes()
    {
        var Current = (GArray)Nodes;
        if (!ReferenceEquals(Current, HarvestSnapshotSource) || Current.Count != HarvestSnapshotState.Count)
        {
            HarvestSnapshotSource = Current;
            HarvestSnapshot = new GArray();
            HarvestSnapshotState.Clear();
        }
        for (int I = 0; I < Current.Count; I++)
        {
            var N = (GDict)Current[I];
            var State = ((int)Gd.Int(N.Get("type")), (Vector2)N.Get("pos"), (int)Gd.Int(N.Get("amount")), (string)Gd.Str(N.Get("sprite", "")), Harvestables.Radius(N));
            if (I < HarvestSnapshotState.Count && HarvestSnapshotState[I] == State) continue;
            var Record = new GArray(State.Item1, State.Item2, State.Item3, State.Item4, State.Item5);
            if (I < HarvestSnapshotState.Count) { HarvestSnapshotState[I] = State; HarvestSnapshot[I] = Record; }
            else { HarvestSnapshotState.Add(State); HarvestSnapshot.Add(Record); }
        }
        return HarvestSnapshot;
    }
}

public partial class WorldGeometry
{
    public readonly List<Harvestables.Site> HarvestSites = new();

    void ExtractHarvestables(dynamic Variants)
    {
        var Kept = new GArray();
        foreach (GArray D in (GArray)Deco)
        {
            string Sprite = Gd.Str(D[1]);
            if (Sprite.StartsWith("dead_", StringComparison.Ordinal)) continue;
            int Type = Harvestables.TypeOf(Sprite);
            if (Type < 0) { Kept.Add(D); continue; }
            float Radius = Gd.Float(Gd.At(Gd.Get(Variants, Sprite, new GArray(0, 0, 0)), 2));
            HarvestSites.Add(new Harvestables.Site((Vector2)D[0], Sprite, Type, Radius));
        }
        Deco = Kept;
    }
}
