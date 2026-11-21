using Godot;
using System;
using System.Collections.Generic;

// Owner: Vu Duc Viet Anh. The island's two rivers, one in the west and one in the east, each running from the
// north coast to the south coast, and the two bridges across each. The water stops everyone like the sea does
// (IslandMap.OnLand): players, enemies, bosses and bots cross only on a bridge, and the bots' and enemies' route
// grids mark the water solid the same way they mark the sea. Grass, trees, rocks and landmarks keep off the
// water and its muddy banks (IslandMap.OnGrass). Each river runs straight north-south under its bridges, so the
// decks are plain east-west boxes. Drawn by src/world/view/Main.Rivers.cs.
public static partial class IslandMap
{
    // half the width of the water, and the muddy bank on each side
    public const float RIVER_HALF = 2.2f * TL;
    public const float RIVER_BANK = 0.9f * TL;
    // a bridge's deck: across the water and onto both banks, a little over three tiles wide
    public const float BRIDGE_HALF_LEN = RIVER_HALF + RIVER_BANK + 1.2f * TL;
    public const float BRIDGE_HALF_W = 1.6f * TL;
    // a big body (a boss) may overhang the rails by the rest of its size, so it still fits over
    const float BRIDGE_OVERHANG = 0.55f * TL;

    // the course of each river in tiles from the outpost, north to south, from the sea to the sea
    static readonly Vector2[][] RIVER_COURSE =
    {
        // west: past the Weather Station and the Bayside Wreck, under door A's road, out by the Southwest Cave
        new Vector2[] { new(-74, -118), new(-73, -95), new(-69, -75), new(-60, -55), new(-67, -35), new(-65, -15),
            new(-47, 15), new(-51, 35), new(-50, 75), new(-43, 95), new(-46, 118) },
        // east: down from the north coast west of the Ruined Base, under door C's road, out to the South Cove
        new Vector2[] { new(26, -118), new(25.5f, -95), new(25.5f, -75), new(27, -55), new(48.5f, -15),
            new(52.5f, 15), new(41, 35), new(34.5f, 55), new(36.5f, 75), new(39.5f, 95), new(36, 118) },
    };
    // the bridges in tiles: [river, centre]; the river runs straight north-south through each
    static readonly (int River, Vector2 At)[] BRIDGE_SPOTS =
    {
        (0, new(-57, 0)),      // door A's road
        (0, new(-55, 55)),     // the trail to the Southwest Cave
        (1, new(51, 0)),       // door C's road
        (1, new(38, -36)),     // the main route north of the Herb Meadow
    };

    // centre lines (pixels, a point every half tile) and bridge centres (pixels)
    public static readonly List<Vector2[]> Rivers = new();
    public static readonly List<(int River, Vector2 At)> Bridges = new();

    // distance to the nearest river's centre line, sampled every half tile
    const float RSTEP = 0.5f;
    static int RN;
    static float[] RiverGrid;

    static void BuildRivers()
    {
        Rivers.Clear();
        Bridges.Clear();
        for (int K = 0; K < RIVER_COURSE.Length; K++)
        {
            var Ctrl = new List<Vector2>(RIVER_COURSE[K]);
            foreach (var (River, At) in BRIDGE_SPOTS)
            {
                if (River != K) continue;
                // straight north-south for a stretch either side of the deck
                Ctrl.RemoveAll(C => MathF.Abs(C.Y - At.Y) < 9);
                Ctrl.Add(At + new Vector2(0, -5));
                Ctrl.Add(At);
                Ctrl.Add(At + new Vector2(0, 5));
                Bridges.Add((K, At * TL));
            }
            Ctrl.Sort((A, B) => A.Y.CompareTo(B.Y));
            Rivers.Add(Smooth(Ctrl.ConvertAll(C => C * TL).ToArray()));
        }
        RN = (int)(2 * REACH / RSTEP) + 1;
        RiverGrid = new float[RN * RN];
        Array.Fill(RiverGrid, 1e6f);
        float Reach = RIVER_HALF + RIVER_BANK + 8 * TL;
        foreach (var Path in Rivers)
            for (int I = 0; I < Path.Length - 1; I++)
            {
                Vector2 A = Path[I], B = Path[I + 1];
                int X0 = Math.Max(0, (int)((MathF.Min(A.X, B.X) - Reach) / TL / RSTEP + REACH / RSTEP));
                int X1 = Math.Min(RN - 1, (int)((MathF.Max(A.X, B.X) + Reach) / TL / RSTEP + REACH / RSTEP) + 1);
                int Y0 = Math.Max(0, (int)((MathF.Min(A.Y, B.Y) - Reach) / TL / RSTEP + REACH / RSTEP));
                int Y1 = Math.Min(RN - 1, (int)((MathF.Max(A.Y, B.Y) + Reach) / TL / RSTEP + REACH / RSTEP) + 1);
                for (int Y = Y0; Y <= Y1; Y++)
                    for (int X = X0; X <= X1; X++)
                    {
                        var P = new Vector2((X * RSTEP - REACH) * TL, (Y * RSTEP - REACH) * TL);
                        float D = SegDist(P, A, B);
                        if (D < RiverGrid[Y * RN + X]) RiverGrid[Y * RN + X] = D;
                    }
            }
    }

    // pixels from P to the middle of the nearest river
    public static float RiverDist(Vector2 P)
    {
        if (RiverGrid == null) return 1e6f;
        float Fx = (P.X / TL + REACH) / RSTEP, Fy = (P.Y / TL + REACH) / RSTEP;
        if (Fx < 0 || Fy < 0 || Fx >= RN - 1 || Fy >= RN - 1) return 1e6f;
        int X = (int)Fx, Y = (int)Fy;
        float Tx = Fx - X, Ty = Fy - Y;
        float A = Mathf.Lerp(RiverGrid[Y * RN + X], RiverGrid[Y * RN + X + 1], Tx);
        float B = Mathf.Lerp(RiverGrid[(Y + 1) * RN + X], RiverGrid[(Y + 1) * RN + X + 1], Tx);
        return Mathf.Lerp(A, B, Ty);
    }

    // in the water, or within Margin of it
    public static bool InRiver(Vector2 P, float Margin = 0) => RiverDist(P) < RIVER_HALF + Margin;

    // a bridge's deck, east-west across its river
    public static Rect2 BridgeDeck(int I) => new Rect2(Bridges[I].At - new Vector2(BRIDGE_HALF_LEN, BRIDGE_HALF_W), new Vector2(2 * BRIDGE_HALF_LEN, 2 * BRIDGE_HALF_W));

    // on a deck with Margin to spare on its sides (its ends run onto the banks)
    public static bool OnBridge(Vector2 P, float Margin = 0)
    {
        foreach (var (_, At) in Bridges)
            if (MathF.Abs(P.X - At.X) <= BRIDGE_HALF_LEN && MathF.Abs(P.Y - At.Y) <= BRIDGE_HALF_W - Math.Clamp(Margin, 0, BRIDGE_OVERHANG)) return true;
        return false;
    }

    // within Reach of any bridge's deck
    public static bool NearBridge(Vector2 P, float Reach = 0)
    {
        for (int I = 0; I < Bridges.Count; I++)
            if (BridgeDeck(I).Grow(Reach).HasPoint(P)) return true;
        return false;
    }
}
