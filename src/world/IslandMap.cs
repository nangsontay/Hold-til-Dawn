using Godot;
using System;
using System.Collections.Generic;
using FileAccess = Godot.FileAccess;

// Owner: Vu Duc Viet Anh. The island from the level-design map (maps/island_map.json, made from the PDF by
// tools/island_from_pdf.py): its coast with 4-6 tile beaches, the shallows, hills, woods, the three main gathering
// routes and the exploration trails, and the 18 landmarks. Positions in the file are tiles from the outpost
// centre; here they are pixels, scaled up by SCALE. Shared by every peer (no rendering): the sea and the two rivers
// (src/world/IslandRivers.cs) stop everyone, and scenery,
// nodes and spawns are placed from it.
public static partial class IslandMap
{
    const float TL = 32.0f;
    // The island is drawn 1.5 times the size of the design map, round the outpost (which keeps its size, as do the
    // safe zone, the door pads, the night spawn bands and the boss distance in GameSim).
    public const float SCALE = 1.5f;
    // how far the depth grid reaches each way, in tiles, and its step
    const int REACH = 126;
    const float STEP = 1.0f;

    public sealed class Landmark
    {
        public int Id;
        public string Name, What, Kind;
        public Vector2 Pos;
    }

    public sealed class Hill
    {
        public Vector2 Top;
        public List<Vector2[]> Rings = new();   // foot first, top last
    }

    public static readonly Vector2[] Coast, Grass;
    public static readonly List<Vector2[]> Shallows = new(), Shade = new(), Routes = new(), Trails = new();
    public static readonly List<Hill> Hills = new();
    public static readonly List<Vector2> Trees = new();
    public static readonly List<Landmark> Landmarks = new();
    public static readonly List<(string Name, Vector2 Pos)> Bays = new();
    // walkable decks out over the water (the Old Fishing Pier, src/world/WorldLandmarks.cs)
    public static readonly List<Rect2> Docks = new();
    // the island's box, in pixels
    public static readonly Rect2 Box;
    static readonly float[] CoastDepth, GrassDepth;
    static readonly int N;

    static IslandMap()
    {
        var Root = Json.ParseString(FileAccess.GetFileAsString("res://maps/island_map.json")).AsGodotDictionary();
        Coast = Poly(Root["coast"]);
        Grass = Poly(Root["grass"]);
        foreach (var P in Root["shallows"].AsGodotArray()) Shallows.Add(Poly(P));
        foreach (var P in Root["shade"].AsGodotArray()) Shade.Add(Poly(P));
        foreach (var P in Root["routes"].AsGodotArray()) Routes.Add(Smooth(Poly(P)));
        foreach (var P in Root["trails"].AsGodotArray()) Trails.Add(Smooth(Poly(P)));
        foreach (var H in Root["hills"].AsGodotArray())
        {
            var D = H.AsGodotDictionary();
            var Hl = new Hill { Top = Pt(D["top"]) };
            foreach (var R in D["rings"].AsGodotArray()) Hl.Rings.Add(Poly(R));
            Hills.Add(Hl);
        }
        foreach (var P in Root["trees"].AsGodotArray()) Trees.Add(Pt(P));
        foreach (var L in Root["landmarks"].AsGodotArray())
        {
            var D = L.AsGodotDictionary();
            Landmarks.Add(new Landmark { Id = D["id"].AsInt32(), Name = D["name"].AsString(), What = D["what"].AsString(), Kind = D["kind"].AsString(), Pos = Pt(D["pos"]) });
        }
        foreach (var B in Root["bays"].AsGodotArray())
        {
            var D = B.AsGodotDictionary();
            Bays.Add((D["name"].AsString(), Pt(D["pos"])));
        }
        Box = new Rect2(Coast[0], Vector2.Zero);
        foreach (var P in Coast) Box = Box.Expand(P);
        // signed distance to the coast and to the edge of the grass, one sample per tile
        N = (int)(2 * REACH / STEP) + 1;
        CoastDepth = DepthGrid(Coast);
        GrassDepth = DepthGrid(Grass);
        // the two rivers and their bridges (src/world/IslandRivers.cs)
        BuildRivers();
    }

    static Vector2 Pt(Variant V)
    {
        var A = V.AsGodotArray();
        return new Vector2(A[0].AsSingle(), A[1].AsSingle()) * TL * SCALE;
    }

    static Vector2[] Poly(Variant V)
    {
        var A = V.AsGodotArray();
        var Out = new Vector2[A.Count];
        for (int I = 0; I < A.Count; I++) Out[I] = Pt(A[I]);
        return Out;
    }

    // Catmull-Rom through the points, one every half tile
    static Vector2[] Smooth(Vector2[] Ctrl)
    {
        if (Ctrl.Length < 3) return Ctrl;
        var Pts = new List<Vector2>();
        for (int I = 0; I < Ctrl.Length - 1; I++)
        {
            Vector2 P0 = Ctrl[Math.Max(0, I - 1)], P1 = Ctrl[I], P2 = Ctrl[I + 1], P3 = Ctrl[Math.Min(Ctrl.Length - 1, I + 2)];
            int Steps = Math.Max(1, (int)(P1.DistanceTo(P2) / (0.5f * TL)));
            for (int S = 0; S < Steps; S++)
            {
                float T = (float)S / Steps;
                Pts.Add(0.5f * ((2 * P1) + (-P0 + P2) * T + (2 * P0 - 5 * P1 + 4 * P2 - P3) * T * T + (-P0 + 3 * P1 - 3 * P2 + P3) * T * T * T));
            }
        }
        Pts.Add(Ctrl[^1]);
        return Pts.ToArray();
    }

    public static bool InPolygon(Vector2[] Poly, Vector2 P)
    {
        bool In = false;
        for (int I = 0, J = Poly.Length - 1; I < Poly.Length; J = I++)
            if ((Poly[I].Y > P.Y) != (Poly[J].Y > P.Y) &&
                P.X < (Poly[J].X - Poly[I].X) * (P.Y - Poly[I].Y) / (Poly[J].Y - Poly[I].Y) + Poly[I].X)
                In = !In;
        return In;
    }

    static float SegDist(Vector2 P, Vector2 A, Vector2 B)
    {
        Vector2 Ab = B - A;
        float T = Math.Clamp((P - A).Dot(Ab) / Math.Max(Ab.LengthSquared(), 0.001f), 0, 1);
        return P.DistanceTo(A + Ab * T);
    }

    public static float PathDist(Vector2 P, Vector2[] Path, bool Closed = false)
    {
        float Best = float.PositiveInfinity;
        for (int I = 0; I < Path.Length - 1; I++) Best = MathF.Min(Best, SegDist(P, Path[I], Path[I + 1]));
        if (Closed) Best = MathF.Min(Best, SegDist(P, Path[^1], Path[0]));
        return Best;
    }

    static float[] DepthGrid(Vector2[] Poly)
    {
        var G = new float[N * N];
        for (int Y = 0; Y < N; Y++)
            for (int X = 0; X < N; X++)
            {
                var P = new Vector2((X * STEP - REACH) * TL, (Y * STEP - REACH) * TL);
                float D = PathDist(P, Poly, true);
                G[Y * N + X] = InPolygon(Poly, P) ? D : -D;
            }
        return G;
    }

    static float Sample(float[] G, Vector2 P)
    {
        float Fx = (P.X / TL + REACH) / STEP, Fy = (P.Y / TL + REACH) / STEP;
        if (Fx < 0 || Fy < 0 || Fx >= N - 1 || Fy >= N - 1) return -REACH * TL;
        int X = (int)Fx, Y = (int)Fy;
        float Tx = Fx - X, Ty = Fy - Y;
        float A = Mathf.Lerp(G[Y * N + X], G[Y * N + X + 1], Tx);
        float B = Mathf.Lerp(G[(Y + 1) * N + X], G[(Y + 1) * N + X + 1], Tx);
        return Mathf.Lerp(A, B, Ty);
    }

    // pixels inland from the waterline (negative out at sea)
    public static float Depth(Vector2 P) => Sample(CoastDepth, P);

    // the rivers are water like the sea: only their bridges cross them (src/world/IslandRivers.cs)
    public static bool OnLand(Vector2 P, float Margin = 0) => (Depth(P) >= Margin && !InRiver(P, Margin)) || OnDock(P, Margin) || OnBridge(P, Margin);

    static bool OnDock(Vector2 P, float Margin)
    {
        foreach (var D in Docks)
            if (D.Grow(-Math.Max(Margin, 0)).HasPoint(P)) return true;
        return false;
    }

    // grass stops at the rivers' banks too
    public static bool OnGrass(Vector2 P, float Margin = 0) => Sample(GrassDepth, P) >= Margin && !InRiver(P, RIVER_BANK + Margin);

    // pixels in from the edge of the grass (negative on the beach and at sea)
    public static float GrassIn(Vector2 P) => Sample(GrassDepth, P);

    // the nearest point at least Margin inland, walking in toward the outpost
    public static Vector2 PullInland(Vector2 P, float Margin)
    {
        if (OnLand(P, Margin)) return P;
        Vector2 Dir = (-P).Normalized();
        for (int I = 0; I < 400 && !OnLand(P, Margin); I++)
        {
            if (P.Length() < TL) break;
            P += Dir * (0.25f * TL);
        }
        return P;
    }

    // A body of this radius moving From -> To stays out of the sea: it slides along the coast.
    public static Vector2 Keep(Vector2 From, Vector2 To, float Radius)
    {
        if (OnLand(To, Radius)) return To;
        var Sx = new Vector2(To.X, From.Y);
        var Sy = new Vector2(From.X, To.Y);
        bool Ox = OnLand(Sx, Radius), Oy = OnLand(Sy, Radius);
        if (Ox && Oy) return (Depth(Sx) >= Depth(Sy)) ? Sx : Sy;
        if (Ox) return Sx;
        if (Oy) return Sy;
        return OnLand(From, Radius) ? From : PullInland(From, Radius);
    }

    // the landmark whose area this is (within Reach of its marker), or null
    public static Landmark Near(Vector2 P, float Reach)
    {
        Landmark Best = null;
        float Bd = Reach;
        foreach (var L in Landmarks)
        {
            float D = L.Pos.DistanceTo(P);
            if (D < Bd)
            {
                Bd = D;
                Best = L;
            }
        }
        return Best;
    }

    public static Landmark ById(int Id) => Landmarks.Find(L => L.Id == Id);
}
