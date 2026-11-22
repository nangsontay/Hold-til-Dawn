using Godot;
using System;
using System.Collections.Generic;
using HoldTilDawn;

public partial class WorldGeometry
{
    public static readonly Vector2 HomeFarmRadii = new(6 * TL, 5 * TL);
    readonly List<Vector2>[] HomeFarmSlots = { new(), new(), new() };
    static readonly int[] HomeFarmMinimum = { 36, 32, 36 };

    static readonly Vector2[] HomeFarmCentres = { FindHomeFarmCentre(0), FindHomeFarmCentre(1), FindHomeFarmCentre(2) };
    public static Vector2 HomeFarmCentre(int Type) => HomeFarmCentres[Type];
    static Vector2 FindHomeFarmCentre(int Type)
    {
        Vector2 P = IslandMap.ById(Type + 2).Pos;
        while ((P.Abs() - new Vector2(5.5f, 3.5f) * TL).Max(Vector2.Zero).Length() > 14.5f * TL) P *= .96f;
        return P.Round();
    }

    public static bool InHomeFarm(Vector2 P, float Margin = 0)
    {
        for (int Type = 0; Type < 3; Type++) if (InHomeFarm(P, Type, Margin)) return true;
        return false;
    }

    public static bool InHomeFarm(Vector2 P, int Type, float Margin = 0) =>
        ((P - HomeFarmCentre(Type)) / (HomeFarmRadii + Vector2.One * Margin)).LengthSquared() <= 1;

    void BuildHomeFarmLandmarks()
    {
        var R = new Random(211);
        for (int Type = 0; Type < 3; Type++)
        {
            Vector2 C = HomeFarmCentre(Type); LmAt[Type + 2] = C;
            Clear(C, HomeFarmRadii.X + 24);
            var Slots = HomeFarmSlots[Type]; Slots.Clear();
            for (int Side = -1; Side <= 1; Side += 2)
                for (float Y = 26; Y < HomeFarmRadii.Y - 12; Y += 36)
                    for (float X = -162; X <= 162; X += 36)
                    {
                        Vector2 P = (C + new Vector2(X + (float)R.NextDouble() * 2 - 1, Side * Y + (float)R.NextDouble() * 2 - 1)).Round();
                        if (!InHomeFarm(P, Type, Type == 1 ? -22 : -14) || !IslandMap.OnGrass(P, 18) || NearPad(P) || Gd.Truth(Lane(P)) || WallDist(P) < 3 * TL) continue;
                        Slots.Add(P);
                    }
            // A fixed shuffle avoids rows of identical resources, while retaining a clear east-west aisle.
            for (int I = Slots.Count - 1; I > 0; I--) { int J = R.Next(I + 1); (Slots[I], Slots[J]) = (Slots[J], Slots[I]); }
            Prop(C + new Vector2(HomeFarmRadii.X + 14, 52), "lm_signpost_0");
            if (Type == 0) Prop(C + new Vector2(-HomeFarmRadii.X - 4, 26), "lm_coconuts_0");
        }
    }

    public void EnsureHomeFarmResources(int PrimaryCount = 0)
    {
        for (int Type = 0; Type < 3; Type++)
        {
            int Have = HarvestSites.FindAll(S => S.Type == Type && InHomeFarm(S.Pos, Type)).Count;
            int Want = Math.Max(HomeFarmMinimum[Type], PrimaryCount);
            var Slots = HomeFarmSlots[Type];
            if (Want > Slots.Count) throw new InvalidOperationException($"Home farm {Type} supports {Slots.Count} nodes; requested {Want}. Reduce NodeMultiplier.");
            for (int I = Have; I < Want; I++)
            {
                int Rock = MathF.Abs(Slots[I].Y - HomeFarmCentre(Type).Y) < 40 ? (I % 2 == 0 ? 0 : 3) : I % 5;
                float Radius = Type == 0 ? 6 : Type == 1 ? Rock switch { 0 => 13, 1 => 18, 2 => 17, 3 => 10, _ => 19 } : 0;
                string Sprite = Type == 0 ? "lm_palm_" + I % 4 : Type == 1 ? "rock_" + Rock : "node_herb_" + I % 2;
                HarvestSites.Add(new(Slots[I], Sprite, Type, Radius));
            }
        }
    }

    // Per material and region: exactly three quarters remain (rounding at most one node per group).
    public readonly int[] OutsideBefore = new int[3], OutsideAfter = new int[3];
    void ThinOutsideResources()
    {
        Array.Clear(OutsideBefore); Array.Clear(OutsideAfter);
        var Groups = new Dictionary<(int Type, int Region), List<Harvestables.Site>>();
        foreach (var S in HarvestSites)
        {
            if (S.Type > 2 || InHomeFarm(S.Pos)) continue;
            int Region = GardenBounds.HasPoint(S.Pos) ? 180 + GardenBeds.FindIndex(B => B.HasPoint(S.Pos)) : InCaveRegion(S.Pos) ? 16 : 0;
            var Key = (S.Type, Region);
            if (!Groups.TryGetValue(Key, out var Group)) Groups[Key] = Group = new();
            Group.Add(S); OutsideBefore[S.Type]++;
        }
        var Remove = new HashSet<Harvestables.Site>();
        foreach (var (Key, Group) in Groups)
        {
            int Drop = (int)MathF.Round(Group.Count * .25f);
            // Spatial hashing distributes removals across the world instead of deleting one contiguous patch.
            Group.Sort((A, B) => ResourceOrder(A.Pos).CompareTo(ResourceOrder(B.Pos)));
            for (int I = 0; I < Drop; I++) Remove.Add(Group[I]);
            OutsideAfter[Key.Type] += Group.Count - Drop;
        }
        HarvestSites.RemoveAll(S => Remove.Contains(S));
        var Flat = new GArray(); int Outside = 0;
        foreach (GArray D in (GArray)Scatter)
            if (InHomeFarm((Vector2)D[0]) || ++Outside % 4 != 0) Flat.Add(D);
        Scatter = Flat;
    }

    static uint ResourceOrder(Vector2 P)
    {
        uint H = unchecked((uint)((int)P.X * 73856093 ^ (int)P.Y * 19349663));
        H ^= H >> 16; H *= 0x7feb352d; H ^= H >> 15; return H;
    }
}
