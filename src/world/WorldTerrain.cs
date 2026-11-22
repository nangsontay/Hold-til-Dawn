using Godot;
using System;
using System.Collections.Generic;
using HoldTilDawn;

// Owner: Vu Duc Viet Anh. Terrain of the island from the level-design map (src/world/IslandMap.cs): beaches and the
// sea around them, walkable hills, the three main gathering routes and the exploration trails, dirt roads out
// along the three door axes (the boss approaches), the Rainwater Pond and the pools of the Mangrove Swamp, and
// the rock spires round the Southwest Cave.
// Everything is fixed (same on every run and every peer). The roads keep the door corridors clear, so the three
// doors stay equally hard to defend (H10). Collision uses the same solid circles as trees and rocks; the sea is
// kept out by IslandMap.Keep. Water stops movement but not arrows or spit.
public partial class WorldGeometry
{
    const float TL = 32.0f;
    public const float ROUTE_W = 2.2f * TL;     // main gathering routes and the door roads
    public const float TRAIL_W = 1.3f * TL;     // exploration trails

    public sealed class Pond
    {
        public Vector2 Centre;
        public Vector2[] Edge;        // closed outline
        public float Radius;          // a little inside the outline, for collision
    }

    public readonly List<Pond> Ponds = new();
    // [path, width]: the main routes and the door roads first, then the exploration trails
    public readonly List<(Vector2[] Path, float Width)> Trails = new();
    public readonly List<Vector2> Crags = new();
    // the swamp's muddy ground
    public Vector2 SwampCentre = Vector2.Inf;
    public float SwampR = 6.0f * TL;

    static Vector2 Tiles(float X, float Y) => new Vector2(X, Y) * TL;
    // sizes round the landmarks grow with the island
    const float S = IslandMap.SCALE;

    void BuildTerrain()
    {
        Ponds.Clear();
        Trails.Clear();
        Crags.Clear();
        var R = new Random(23);
        // dirt roads out along the door axes to where the bosses land (the cross on the design map)
        for (int I = 0; I < 3; I++)
        {
            Vector2 Door = (Vector2)Gd.At(GameSim.DOOR_POS, I), Dir = (Vector2)Gd.At(GameSim.DOOR_DIR, I);
            var Side = new Vector2(-Dir.Y, Dir.X);
            var Pts = new List<Vector2>();
            for (float D = 1.5f; D <= 60; D += 0.5f)
            {
                Vector2 P = Door + Dir * D * TL + Side * MathF.Sin(D * 0.23f + I) * 0.45f * TL;
                // the road crosses a river on its bridge
                if (!IslandMap.OnGrass(P, TL) && !IslandMap.NearBridge(P, TL)) break;
                Pts.Add(P);
            }
            Trails.Add((Pts.ToArray(), ROUTE_W));
        }
        foreach (var Rt in IslandMap.Routes) Trails.Add((Rt, ROUTE_W));
        foreach (var Tr in IslandMap.Trails) Trails.Add((Tr, TRAIL_W));
        // paths cut by a river turn along its bank to a bridge (WorldRivers.cs)
        CutPathsAtRivers();
        // the Rainwater Pond, just off the trail at landmark 15
        var Rain = IslandMap.ById(15);
        if (Rain != null) Ponds.Add(MakePond(R, Rain.Pos + Tiles(4.2f, 0.8f) * S, 2.8f * 1.25f * LANDMARK_SCALE));
        // the Mangrove Swamp: muddy ground with small pools, kept off the paths
        var Swamp = IslandMap.ById(8);
        if (Swamp != null)
        {
            // the swamp lies round its marker, on the grass back from the inlet's beach
            float Best = float.NegativeInfinity;
            int Look = (int)(8 * S);
            for (int Dy = -Look; Dy <= Look; Dy++)
                for (int Dx = -Look; Dx <= Look; Dx++)
                {
                    Vector2 P = Swamp.Pos + Tiles(Dx, Dy);
                    float Score = MathF.Min(IslandMap.GrassIn(P), 7 * S * TL) - 0.35f * P.DistanceTo(Swamp.Pos);
                    if (Score > Best)
                    {
                        Best = Score;
                        SwampCentre = P;
                    }
                }
            SwampR = Math.Clamp(IslandMap.GrassIn(SwampCentre) - TL, 3 * TL, 7 * S * TL * LANDMARK_SCALE);
        }
        if (Swamp != null)
        {
            for (int I = 0, Tries = 0; I < 9 && Tries < 300; Tries++)
            {
                Vector2 C = SwampCentre + Vector2.FromAngle((float)R.NextDouble() * MathF.Tau) * (float)R.NextDouble() * (SwampR - 2 * TL);
                float Rad = 1.1f + (float)R.NextDouble() * 1.0f;
                if (!IslandMap.OnGrass(C, (Rad + 1.5f) * TL) || NearPath(C, (Rad + 1.6f) * TL)) continue;
                bool Clear = true;
                foreach (var Pd in Ponds) if (Pd.Centre.DistanceTo(C) < Pd.Radius + (Rad + 1.5f) * TL) Clear = false;
                if (!Clear) continue;
                Ponds.Add(MakePond(R, C, Rad));
                I++;
            }
        }
        // Southwest Cave now owns its walkable chamber and rock perimeter (WorldCave.cs).
    }

    static Pond MakePond(Random R, Vector2 Centre, float RadiusTiles)
    {
        const int N = 22;
        var B = new float[3];
        for (int I = 0; I < B.Length; I++) B[I] = (float)R.NextDouble() * MathF.Tau;
        var Edge = new Vector2[N];
        for (int I = 0; I < N; I++)
        {
            float A = I * MathF.Tau / N;
            float Wob = 1.0f + 0.12f * MathF.Sin(2 * A + B[0]) + 0.07f * MathF.Sin(3 * A + B[1]) + 0.04f * MathF.Sin(5 * A + B[2]);
            // a little wider than tall, like the rest of the 3/4 view
            Edge[I] = Centre + new Vector2(MathF.Cos(A) * 1.15f, MathF.Sin(A) * 0.85f) * RadiusTiles * TL * Wob;
        }
        return new Pond { Centre = Centre, Edge = Edge, Radius = RadiusTiles * TL * 0.8f };
    }

    bool NearPath(Vector2 P, float Reach)
    {
        foreach (var (Path, Width) in Trails)
            if (IslandMap.PathDist(P, Path) < Reach + Width / 2) return true;
        return false;
    }

    // --- queries ----------------------------------------------------------------
    // What lies at P: 0 grass, 1 water (pond or pool), 4 hill, 5 path, 6 built floor, 7 beach, 8 sea, 9 swamp mud.
    // Used for the minimap and to keep trees, grass and reeds where they belong.
    public int TerrainAt(Vector2 P, float Margin = 0)
    {
        if (!IslandMap.OnLand(P)) return 8;
        foreach (var (F, _) in Floors)
            if (F.Grow(Margin).HasPoint(P)) return 6;
        // a bridge's deck is a path; the rivers' muddy banks count as beach
        if (IslandMap.NearBridge(P, Margin) && IslandMap.InRiver(P, IslandMap.RIVER_BANK + Margin)) return 5;
        if (!IslandMap.OnGrass(P, Margin)) return 7;
        foreach (var Pd in Ponds)
            if (P.DistanceTo(Pd.Centre) < Pd.Radius * 1.6f + Margin && IslandMap.InPolygon(Pd.Edge, P)) return 1;
        foreach (var (Path, Width) in Trails)
            if (IslandMap.PathDist(P, Path) < Width / 2 + Margin) return 5;
        if (P.DistanceTo(SwampCentre) < SwampR) return 9;
        foreach (var H in IslandMap.Hills)
            if (H.Rings.Count > 0 && P.DistanceTo(H.Top) < 14 * S * TL && IslandMap.InPolygon(H.Rings[0], P)) return 4;
        return 0;
    }

    // True where scenery (trees, rocks, grass tufts) must not be placed.
    public bool TerrainBlocked(Vector2 P, float Margin)
    {
        if (InCaveRegion(P, Margin)) return true;
        if (IslandMap.InBridgeApproach(P, Margin)) return true;
        int K = TerrainAt(P, Margin);
        if (K != 0 && K != 4 && K != 9) return true;
        foreach (Vector2 C in Crags)
            if (P.DistanceTo(C) < TL + Margin) return true;
        return InClear(P, Margin);
    }

    // Solid circles: ponds and pools. Crags are added with the other scenery from their sprites' radius.
    void AddTerrainSolids(dynamic Into)
    {
        // three circles across each (they are wider than tall)
        foreach (var Pd in Ponds)
            foreach (float K in new[] { -0.45f, 0.0f, 0.45f })
                Gd.Append(Into, new GArray(Pd.Centre + new Vector2(K * Pd.Radius, 0), Pd.Radius * 0.85f, false));
        // the landmarks' big structures (WorldLandmarks.cs)
        foreach (var (C, Rr) in LmSolids)
            Gd.Append(Into, new GArray(C, Rr, true));
        // Continuous rail colliders include the dry abutments: land checks alone cannot stop side entry there.
        foreach (var (_, At) in IslandMap.Bridges)
            foreach (int Side in new[] { -1, 1 })
            {
                float Reach = IslandMap.BRIDGE_HALF_LEN - 10;
                int Steps = (int)MathF.Ceiling(Reach * 2 / (IslandMap.BRIDGE_RAIL_RADIUS * 1.4f));
                for (int I = 0; I <= Steps; I++)
                    Gd.Append(Into, new GArray(At + new Vector2(Mathf.Lerp(-Reach, Reach, I / (float)Steps), Side * IslandMap.BRIDGE_RAIL_OFFSET), IslandMap.BRIDGE_RAIL_RADIUS, false));
            }
    }
}
