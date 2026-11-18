using Godot;
using System;
using System.Collections.Generic;
using HoldTilDawn;

// Owner: Vu Duc Viet Anh. Terrain of the outer island (GDD 4.3: "tree clusters and rock outcrops break up movement"):
// streams that run from a pond to the sea with a wooden bridge, raised plateaus ringed by cliffs with ramps up,
// ridges of rock spires with a gap, and worn dirt trails along the three door corridors.
// Everything is fixed (same on every run and every peer) and kept out of the bands the night waves walk through
// to their door (|y| < 22 tiles west and east of the base, |x| < 22 tiles north of it), out of the safe zone and
// off the door corridors, so the three doors stay equally hard to defend (H10).
// Collision uses the same solid circles as trees and rocks, so players, enemies, bots (A* grid) and the
// minimap all see the same terrain. Water and cliff edges stop movement but not arrows or spit.
public partial class WorldGeometry
{
    const float TL = 32.0f;
    public const float WATER_W = 1.7f * TL;     // stream width
    public const float BRIDGE_HALF = 1.5f * TL; // open span of a bridge along its stream
    public const float TRAIL_W = 2.2f * TL;
    public const float CLIFF_H = 0.9f * TL;     // drawn height of a cliff face

    public sealed class Stream
    {
        public Vector2[] Path;        // smoothed centre line, pond first, ending in the sea
        public Vector2 Pond;
        public float PondR;
        public List<(Vector2 At, Vector2 Across)> Bridges = new();
    }

    public sealed class Plateau
    {
        public Vector2 Centre;
        public Vector2[] Edge;        // closed outline, clockwise on screen
        public bool[] Ramp;           // edge i (Edge[i] -> Edge[i+1]) is a slope instead of a cliff
    }

    public readonly List<Stream> Streams = new();
    public readonly List<Plateau> Plateaus = new();
    public readonly List<Vector2[]> Trails = new();
    public readonly List<Vector2> Crags = new();

    static Vector2 Tiles(float X, float Y) => new Vector2(X, Y) * TL;

    void BuildTerrain()
    {
        Streams.Clear();
        Plateaus.Clear();
        Trails.Clear();
        Crags.Clear();
        var R = new Random(23);
        // south-west and north-east streams
        Streams.Add(MakeStream(R, new[] { Tiles(-17, 33), Tiles(-21, 40), Tiles(-19, 47), Tiles(-24, 54), Tiles(-23, 61), Tiles(-28, 68), Tiles(-31, 78) }, 2.6f, 0.42f));
        Streams.Add(MakeStream(R, new[] { Tiles(32, -28), Tiles(36, -35), Tiles(34, -42), Tiles(41, -49), Tiles(45, -56), Tiles(52, -62), Tiles(59, -71) }, 2.2f, 0.45f));
        // plateaus: south-east and north-west, each with a ramp facing the base and one on the far side
        Plateaus.Add(MakePlateau(R, Tiles(31, 42), 7.5f, new[] { Mathf.DegToRad(-126), Mathf.DegToRad(155) }));
        Plateaus.Add(MakePlateau(R, Tiles(-38, -37), 7.0f, new[] { Mathf.DegToRad(44), Mathf.DegToRad(-100) }));
        // rock ridges (spires every 1.3 tiles, a 3-tile gap somewhere along each)
        Ridge(new[] { Tiles(1, 37), Tiles(5, 43), Tiles(8, 49), Tiles(14, 53) }, 0.55f);
        Ridge(new[] { Tiles(-39, 31), Tiles(-44, 27), Tiles(-50, 26), Tiles(-55, 29) }, 0.4f);
        Ridge(new[] { Tiles(45, -24), Tiles(50, -27), Tiles(55, -32) }, 0.5f);
        // trails along the three door corridors
        for (int I = 0; I < 3; I++)
        {
            Vector2 Door = (Vector2)Gd.At(GameSim.DOOR_POS, I), Dir = (Vector2)Gd.At(GameSim.DOOR_DIR, I);
            var Side = new Vector2(-Dir.Y, Dir.X);
            var Pts = new List<Vector2>();
            for (float D = 1.5f; D <= 48; D += 0.5f)
                Pts.Add(Door + Dir * D * TL + Side * MathF.Sin(D * 0.23f + I) * 0.45f * TL);
            Trails.Add(Pts.ToArray());
        }
    }

    // Catmull-Rom through the control points, one point every 0.4 tiles; a bridge part of the way along.
    static Stream MakeStream(Random R, Vector2[] Ctrl, float PondTiles, float BridgeAt)
    {
        var Pts = new List<Vector2>();
        for (int I = 0; I < Ctrl.Length - 1; I++)
        {
            Vector2 P0 = Ctrl[Math.Max(0, I - 1)], P1 = Ctrl[I], P2 = Ctrl[I + 1], P3 = Ctrl[Math.Min(Ctrl.Length - 1, I + 2)];
            int Steps = Math.Max(2, (int)(P1.DistanceTo(P2) / (0.4f * TL)));
            for (int S = 0; S < Steps; S++)
            {
                float T = (float)S / Steps;
                Pts.Add(0.5f * ((2 * P1) + (-P0 + P2) * T + (2 * P0 - 5 * P1 + 4 * P2 - P3) * T * T + (-P0 + 3 * P1 - 3 * P2 + P3) * T * T * T));
            }
        }
        Pts.Add(Ctrl[^1]);
        var St = new Stream { Path = Pts.ToArray(), Pond = Ctrl[0], PondR = PondTiles * TL };
        int K = Math.Clamp((int)(Pts.Count * BridgeAt), 2, Pts.Count - 3);
        Vector2 Along = (Pts[K + 1] - Pts[K - 1]).Normalized();
        St.Bridges.Add((Pts[K], new Vector2(-Along.Y, Along.X)));
        return St;
    }

    // An irregular round plateau; edges whose direction from the centre is within 22 degrees of a ramp angle
    // are slopes.
    static Plateau MakePlateau(Random R, Vector2 Centre, float RadiusTiles, float[] RampAngles)
    {
        const int N = 30;
        var Bumps = new float[6];
        for (int I = 0; I < Bumps.Length; I++) Bumps[I] = (float)R.NextDouble() * MathF.Tau;
        var Edge = new Vector2[N];
        var Ramp = new bool[N];
        for (int I = 0; I < N; I++)
        {
            float A = I * MathF.Tau / N;
            float Wob = 0.88f + 0.08f * MathF.Sin(2 * A + Bumps[0]) + 0.05f * MathF.Sin(3 * A + Bumps[1]) + 0.03f * MathF.Sin(5 * A + Bumps[2]);
            Edge[I] = Centre + Vector2.FromAngle(A) * RadiusTiles * TL * Wob;
            float Mid = (I + 0.5f) * MathF.Tau / N;
            foreach (float Ra in RampAngles)
                if (MathF.Abs(Mathf.AngleDifference(Mid, Ra)) < Mathf.DegToRad(22)) Ramp[I] = true;
        }
        return new Plateau { Centre = Centre, Edge = Edge, Ramp = Ramp };
    }

    void Ridge(Vector2[] Ctrl, float GapAt)
    {
        var Pts = new List<Vector2>();
        for (int I = 0; I < Ctrl.Length - 1; I++)
        {
            float Len = Ctrl[I].DistanceTo(Ctrl[I + 1]);
            int Steps = Math.Max(1, (int)MathF.Round(Len / (1.3f * TL)));
            for (int S = 0; S < Steps; S++) Pts.Add(Ctrl[I].Lerp(Ctrl[I + 1], (float)S / Steps));
        }
        Pts.Add(Ctrl[^1]);
        int Gap = Math.Clamp((int)(Pts.Count * GapAt), 1, Pts.Count - 3);
        for (int I = 0; I < Pts.Count; I++)
            if (I != Gap && I != Gap + 1) Crags.Add(Pts[I]);
    }

    // --- queries ----------------------------------------------------------------
    static float SegDist(Vector2 P, Vector2 A, Vector2 B)
    {
        Vector2 Ab = B - A;
        float T = Math.Clamp((P - A).Dot(Ab) / Math.Max(Ab.LengthSquared(), 0.001f), 0, 1);
        return P.DistanceTo(A + Ab * T);
    }

    static float PathDist(Vector2 P, Vector2[] Path)
    {
        float Best = float.PositiveInfinity;
        for (int I = 0; I < Path.Length - 1; I++) Best = MathF.Min(Best, SegDist(P, Path[I], Path[I + 1]));
        return Best;
    }

    static bool InPolygon(Vector2[] Poly, Vector2 P)
    {
        bool In = false;
        for (int I = 0, J = Poly.Length - 1; I < Poly.Length; J = I++)
            if ((Poly[I].Y > P.Y) != (Poly[J].Y > P.Y) &&
                P.X < (Poly[J].X - Poly[I].X) * (P.Y - Poly[I].Y) / (Poly[J].Y - Poly[I].Y) + Poly[I].X)
                In = !In;
        return In;
    }

    public bool OnBridge(Vector2 P)
    {
        foreach (var St in Streams)
            foreach (var (At, Across) in St.Bridges)
            {
                Vector2 D = P - At;
                if (MathF.Abs(D.Dot(Across)) < WATER_W / 2 + 0.7f * TL && MathF.Abs(D.Dot(new Vector2(Across.Y, -Across.X))) < 0.8f * TL) return true;
            }
        return false;
    }

    // What lies at P: 0 open ground, 1 water, 2 bridge, 3 cliff (edge or the face below it), 4 plateau top,
    // 5 trail, 6 ramp. Used for the minimap and to keep trees, grass and reeds off the terrain.
    public int TerrainAt(Vector2 P, float Margin = 0)
    {
        if (OnBridge(P)) return 2;
        foreach (var St in Streams)
            if (P.DistanceTo(St.Pond) < St.PondR + Margin || PathDist(P, St.Path) < WATER_W / 2 + Margin) return 1;
        foreach (var Pl in Plateaus)
        {
            if (P.DistanceTo(Pl.Centre) > 11 * TL) continue;
            for (int I = 0; I < Pl.Edge.Length; I++)
            {
                Vector2 A = Pl.Edge[I], B = Pl.Edge[(I + 1) % Pl.Edge.Length];
                // the face hangs below the edge, so look a cliff height up as well
                float D = MathF.Min(SegDist(P, A, B), SegDist(P - new Vector2(0, CLIFF_H * 0.5f), A, B));
                if (D < 0.5f * TL + Margin) return Pl.Ramp[I] ? 6 : 3;
            }
            if (InPolygon(Pl.Edge, P)) return 4;
        }
        foreach (var Tr in Trails)
            if (PathDist(P, Tr) < TRAIL_W / 2 + Margin) return 5;
        return 0;
    }

    // True where scenery (trees, rocks, grass tufts) must not be placed.
    public bool TerrainBlocked(Vector2 P, float Margin)
    {
        int K = TerrainAt(P, Margin);
        if (K != 0 && K != 4) return true;
        foreach (Vector2 C in Crags)
            if (P.DistanceTo(C) < TL + Margin) return true;
        return false;
    }

    // Solid circles: water every 0.4 tiles (open at the bridges), ponds, cliff edges every 0.45 tiles (open at
    // the ramps). Crags are added with the other scenery from their sprites' radius.
    void AddTerrainSolids(dynamic Into)
    {
        foreach (var St in Streams)
        {
            Gd.Append(Into, new GArray(St.Pond, St.PondR, false));
            float Walked = 0;
            for (int I = 0; I < St.Path.Length; I++)
            {
                if (I > 0) Walked += St.Path[I].DistanceTo(St.Path[I - 1]);
                Vector2 P = St.Path[I];
                if (P.Length() > (float)ISLAND + TL) break;
                bool Open = false;
                foreach (var (At, _) in St.Bridges)
                    if (P.DistanceTo(At) < BRIDGE_HALF) Open = true;
                if (!Open) Gd.Append(Into, new GArray(P, WATER_W / 2, false));
            }
        }
        foreach (var Pl in Plateaus)
            for (int I = 0; I < Pl.Edge.Length; I++)
            {
                if (Pl.Ramp[I]) continue;
                Vector2 A = Pl.Edge[I], B = Pl.Edge[(I + 1) % Pl.Edge.Length];
                int Steps = Math.Max(1, (int)MathF.Ceiling(A.DistanceTo(B) / (0.45f * TL)));
                for (int S = 0; S <= Steps; S++) Gd.Append(Into, new GArray(A.Lerp(B, (float)S / Steps), 0.42f * TL, false));
            }
    }
}
