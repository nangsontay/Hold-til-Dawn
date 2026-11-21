using Godot;
using System;
using System.Collections.Generic;
using HoldTilDawn;

public partial class WorldGeometry
{
    public readonly List<(Vector2 At, Vector2 Along)> Ramps = new();

    // Stone steps where a path climbs a hill. (The rivers and their bridges are src/world/IslandRivers.cs.)
    void BuildRamps()
    {
        Ramps.Clear();
        foreach (var H in IslandMap.Hills)
        {
            var (At, Along) = OnPath(H.Top);
            if (At.DistanceTo(H.Top) > 18 * TL) continue;
            Ramps.Add((At, Along));
            for (int I = -4; I <= 4; I++) Steps.Add((At + Along * I * .7f * TL, Along.Orthogonal()));
        }
    }
    void DressExploration()
    {
        var R = new Random(802);
        // Every named site gets a distinct supporting scene; these props never become resource nodes.
        for (int Id = 2; Id <= 18; Id++)
        {
            string[] Props = Id switch {
                5 or 7 => new[] { "lm_cart_0", "lm_warning_0", "lm_cargo_stack_0" },
                6 or 9 or 13 => new[] { "lm_console_0", "lm_cargo_stack_0", "lm_warning_0" },
                8 or 15 or 18 => new[] { "lm_waterpump_0", "lm_rainbarrel_0", "lm_signpost_0" },
                10 or 12 or 14 => new[] { "lm_cargo_stack_0", "lm_rope_0", "lm_buoys_0" },
                _ => new[] { "lm_tent_0", "lm_warning_0", "lm_expedition_0" } };
            for (int I = 0; I < Props.Length; I++)
                for (int Try = 0; Try < 12; Try++)
                {
                    float A = I * 2.1f + Try * .52f;
                    Vector2 P = LmAt.GetValueOrDefault(Id, At(Id)) + Vector2.FromAngle(A) * (4.8f + Try * .3f) * TL;
                    if (DressAt(P, Props[I], I == 0 ? 12 : 0)) break;
                }
        }
        // Survey camps, broken carts and signs in unnamed spaces, along trails rather than isolated clutter.
        foreach (var (Path, Width) in Trails)
        {
            if (Path.Length < 18) continue;
            for (int I = 12; I < Path.Length - 2; I += 32)
            {
                Vector2 Along = (Path[I + 1] - Path[I]).Normalized();
                Vector2 P = Path[I] + Along.Orthogonal() * (Width / 2 + 2 * TL) * (R.Next(2) == 0 ? -1 : 1);
                if (IslandMap.Near(P, 10 * TL) != null || P.Length() < 26 * TL || InClear(P, 2 * TL)) continue;
                string Prop = R.Next(4) switch { 0 => "lm_tent_0", 1 => "lm_cart_0", 2 => "lm_warning_0", _ => "lm_cargo_stack_0" };
                if (DressAt(P, Prop, 12)) DressAt(P + Along * 1.6f * TL, "lm_expedition_0");
            }
        }
    }
    void FillResourceGaps(dynamic Variants)
    {
        var R = new Random(317);
        var Occupied = new List<Vector2>();
        foreach (GArray D in (GArray)Deco) Occupied.Add((Vector2)D[0]);
        // One small cluster per grid cell, with clear corridors between them. Fixed seed keeps world parity.
        Rect2 B = IslandMap.Box;
        for (float Y = B.Position.Y + 3 * TL; Y < B.End.Y; Y += 6 * TL)
            for (float X = B.Position.X + 3 * TL; X < B.End.X; X += 6 * TL)
                for (int K = 0; K < 2; K++)
                {
                    Vector2 P = new Vector2(X, Y) + new Vector2((float)R.NextDouble() * 4 - 2, (float)R.NextDouble() * 4 - 2) * TL;
                    if (WallDist(P) < 10 * TL || !IslandMap.OnGrass(P, TL) || TerrainBlocked(P, TL)
                        || Gd.Truth(Lane(P)) || NearPad(P)) continue;
                    bool Free = true;
                    foreach (Vector2 Q in Occupied) if (P.DistanceSquaredTo(Q) < 2.3f * TL * 2.3f * TL) { Free = false; break; }
                    if (!Free) continue;
                    int Type = R.Next(10);
                    string Name = Type < 5 ? "pine_" + R.Next(6) : Type < 8 ? "rock_" + R.Next(5) : "shrub_" + R.Next(4);
                    if (!Gd.Has(Variants, Name)) continue;
                    ((GArray)Deco).Add(new GArray(P.Round(), Name, 1f, COLD)); Occupied.Add(P);
                }
    }
}
