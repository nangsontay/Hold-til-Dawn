using Godot;
using System;
using System.Collections.Generic;
using HoldTilDawn;

// Owner: Vu Duc Viet Anh. Draws the island of src/world/IslandMap.cs and the terrain of src/world/WorldTerrain.cs on
// the ground layer (under everything with height): the sea stepping up through the shallows, the beach with its
// wet edge and a line of surf, the grass (lighter in the safe zone), darker woodland floor, hills with contour
// contours, the Rainwater Pond and the Mangrove Swamp. Route surfaces are hidden. Same pixel-art palette
// as art/nature (tools/PixelNature.cs); light from the upper left.
public partial class Main : Node2D
{
    static readonly Color SEA = new Color("1F4257");
    static readonly Color[] SHALLOW = { new Color("27546A"), new Color("2E6679"), new Color("3B7E8A") };
    static readonly Color SAND = new Color("CDB688"), WET_SAND = new Color("A99872"), FOAM = new Color(0.92f, 0.97f, 1.0f, 0.55f);
    static readonly Color OUTER_GRASS = new Color("5B7434") * 1.1f, SAFE_GRASS = new Color("728C40") * 1.1f, WOOD_FLOOR = new Color("4C6530") * 1.1f;
    static readonly Color HILL_FOOT = new Color("6E8048"), HILL_TOP = new Color("A49C7E"), CONTOUR = new Color(0.93f, 0.92f, 0.8f, 0.45f);
    static readonly Color BANK = new Color("4A5638"), WATER = new Color("3B6A86"), DEEP = new Color("2F5A75"), RIPPLE = new Color(0.62f, 0.8f, 0.88f, 0.75f);
    static readonly Color MUD = new Color("4F5233"), MUD_DARK = new Color("3E4129");
    // the coast pushed out a little, for the surf
    Vector2[] Surf;

    // The island's polygons never change: each is cut into triangles once (with its ground-texture UVs) and handed
    // to the renderer as it is every frame, instead of draw_colored_polygon cutting up the coast's thousand points
    // again each frame. Only for arrays that live as long as the map (a new array each frame would pile up here).
    sealed class Fill
    {
        public int[] Idx;
        public Vector2[] Uv;
    }
    readonly Dictionary<Vector2[], Fill> Fills = new();
    Rid GroundRid;

    void FillStatic(Vector2[] Pts, Color Col, bool Textured = true)
    {
        if (!Fills.TryGetValue(Pts, out var F))
        {
            F = new Fill { Idx = Geometry2D.TriangulatePolygon(Pts), Uv = Array.ConvertAll(Pts, P => P / 256.0f) };
            Fills[Pts] = F;
        }
        if (F.Idx.Length == 0)
        {
            // not a simple polygon: leave it to Godot as before
            if (Textured) this.Ground(Pts, Col);
            else DrawColoredPolygon(Pts, Col);
            return;
        }
        if (Textured && !GroundRid.IsValid) GroundRid = ((Texture2D)Gd.Unwrap(GroundTex)).GetRid();
        RenderingServer.CanvasItemAddTriangleArray(GetCanvasItem(), F.Idx, Pts, new[] { Col }, Textured ? F.Uv : null, null, null, Textured ? GroundRid : default);
    }

    Vector2[] SafeRing;
    readonly Dictionary<Vector2[], (Vector2[] Ring, Vector2[] Low)> HillRing = new();
    readonly Dictionary<Vector2[], (Vector2[] Bank, Vector2[] Deep, Vector2[] Rim)> PondLook = new();

    Rect2 Bounds(IEnumerable<Vector2> Pts, float Grow)
    {
        var R = new Rect2(Vector2.Inf, Vector2.Zero);
        bool First = true;
        foreach (var P in Pts)
        {
            if (First) { R = new Rect2(P, Vector2.Zero); First = false; }
            else R = R.Expand(P);
        }
        return R.Grow(Grow);
    }

    static Vector2[] Closed(Vector2[] Poly)
    {
        var Out = new Vector2[Poly.Length + 1];
        Poly.CopyTo(Out, 0);
        Out[^1] = Poly[0];
        return Out;
    }

    // sea, beach and grass; called first on the ground layer
    public void DrawIsland(Rect2 View)
    {
        float Tl = (float)T;
        float Clk = (float)Clock;
        DrawRect(View.Grow(4 * Tl), SEA);
        for (int I = IslandMap.Shallows.Count - 1; I >= 0; I--)
            FillStatic(IslandMap.Shallows[I], SHALLOW[Math.Min(SHALLOW.Length - 1, IslandMap.Shallows.Count - 1 - I)], false);
        // surf rolling in and out over the wet sand
        if (Surf == null)
        {
            var C = IslandMap.Coast;
            Surf = new Vector2[C.Length];
            for (int I = 0; I < C.Length; I++)
            {
                Vector2 Along = (C[(I + 1) % C.Length] - C[(I + C.Length - 1) % C.Length]).Normalized();
                Vector2 N = new Vector2(-Along.Y, Along.X);
                if (IslandMap.Depth(C[I] + N * 8) > IslandMap.Depth(C[I])) N = -N;
                Surf[I] = C[I] + N * 0.35f * Tl;
            }
        }
        FillStatic(IslandMap.Coast, SAND);
        DrawPolyline(Closed(IslandMap.Coast), WET_SAND, 10);
        float Wave = 0.5f + 0.5f * MathF.Sin(Clk * 0.9f);
        var Foam = FOAM;
        Foam.A *= 0.5f + 0.5f * Wave;
        var Rolled = new Vector2[Surf.Length + 1];
        for (int I = 0; I < Surf.Length; I++) Rolled[I] = IslandMap.Coast[I].Lerp(Surf[I], 0.3f + 0.7f * Wave);
        Rolled[^1] = Rolled[0];
        DrawPolyline(Rolled, Foam, 2);
        // grass, darker past the safe zone and under the woods
        FillStatic(IslandMap.Grass, OUTER_GRASS);
        foreach (var Wd in IslandMap.Shade)
            if (Bounds(Wd, Tl).Intersects(View)) FillStatic(Wd, WOOD_FLOOR);
        if (SafeRing == null)
        {
            var Ring = new List<Vector2>();
            foreach (dynamic P in Gd.Iter(this.RoundRect(GameSim.HALF, GameSim.SAFE_D))) Ring.Add((Vector2)P);
            SafeRing = Ring.ToArray();
        }
        FillStatic(SafeRing, SAFE_GRASS);
        DrawPolyline(Closed(SafeRing), new Color(1, 1, 0.8f, 0.12f), 2);
        foreach (var H in IslandMap.Hills)
            if (H.Rings.Count > 0 && Bounds(H.Rings[0], Tl).Intersects(View)) DrawHill(H);
    }

    // stepped rings from the foot to the top, each lighter, a pale contour line round each and a shade line on
    // the side away from the light
    void DrawHill(IslandMap.Hill H)
    {
        int N = H.Rings.Count;
        for (int I = 0; I < N; I++)
        {
            float K = (N <= 1) ? 1 : (float)I / (N - 1);
            FillStatic(H.Rings[I], HILL_FOOT.Lerp(HILL_TOP, K) * 1.08f);
            if (!HillRing.TryGetValue(H.Rings[I], out var Look))
            {
                var Ring = Closed(H.Rings[I]);
                var Low = new Vector2[Ring.Length];
                for (int J = 0; J < Ring.Length; J++) Low[J] = Ring[J] + new Vector2(1.5f, 2.5f);
                Look = (Ring, Low);
                HillRing[H.Rings[I]] = Look;
            }
            DrawPolyline(Look.Low, new Color(0.1f, 0.12f, 0.08f, 0.22f), 2);
            DrawPolyline(Look.Ring, CONTOUR, 1);
        }
    }

    public void DrawTerrain(Rect2 View)
    {
        WorldGeometry W = (WorldGeometry)Geometry;
        float Tl = (float)T;
        // the Mangrove Swamp's mud, under the paths through it
        if (new Rect2(W.SwampCentre - Vector2.One * W.SwampR, Vector2.One * 2 * W.SwampR).Intersects(View))
            DrawSwamp(W);
        // Route data still reserves clear walking corridors; their yellow surface is no longer rendered.
        DrawLandmarkSoils(W, View);
        // Rivers and their functional crossing bridges remain visible.
        DrawRivers(View);
        // the landmarks' floors, the pier, the platform and coral (Main.LandmarkView.cs)
        DrawLandmarkGround(W, View);
        for (int I = 0; I < W.Ponds.Count; I++)
            if (Bounds(W.Ponds[I].Edge, Tl * 2).Intersects(View)) DrawPond(W.Ponds[I], I == 0);
        DrawLandmarkLife(W, View, (float)Clock);
    }

    Vector2[] SwampEdge;
    List<Vector2[]> SwampWet;

    // mud inside the grass (the swamp runs down to the beach of the Mangrove Inlet), darker wet patches in it
    void DrawSwamp(WorldGeometry W)
    {
        float Tl = (float)T;
        if (SwampEdge == null)
        {
            const int N = 48;
            SwampEdge = new Vector2[N];
            for (int I = 0; I < N; I++)
            {
                float A = I * MathF.Tau / N;
                float Wob = 0.82f + 0.1f * MathF.Sin(3 * A + 1.3f) + 0.06f * MathF.Sin(7 * A + 0.4f);
                Vector2 P = W.SwampCentre + Vector2.FromAngle(A) * W.SwampR * Wob;
                while (!IslandMap.OnGrass(P, 0.6f * Tl) && P.DistanceTo(W.SwampCentre) > Tl) P = P.MoveToward(W.SwampCentre, 0.25f * Tl);
                SwampEdge[I] = P;
            }
            SwampWet = new List<Vector2[]>();
            for (int K = 0; K < 14; K++)
            {
                Vector2 C = W.SwampCentre + Vector2.FromAngle(K * 2.4f) * W.SwampR * (0.12f + 0.055f * K);
                float Size = 0.7f + 0.25f * (K % 3);
                if (!IslandMap.OnGrass(C, (Size + 1.0f) * Tl)) continue;
                var Blob = new Vector2[12];
                for (int I = 0; I < 12; I++)
                    Blob[I] = C + Vector2.FromAngle(I * MathF.Tau / 12) * Tl * Size * (1.0f + 0.3f * MathF.Sin(I * 1.7f + K)) * new Vector2(1.4f, 0.8f);
                SwampWet.Add(Blob);
            }
        }
        FillStatic(SwampEdge, MUD * 1.2f);
        foreach (var Blob in SwampWet)
        {
            FillStatic(Blob, MUD_DARK * 1.1f);
            // a glint of standing water
            Vector2 C = (Blob[0] + Blob[6]) / 2;
            DrawLine((C + new Vector2(-4, -2)).Round(), (C + new Vector2(3, -2)).Round(), new Color(0.6f, 0.75f, 0.7f, 0.35f), 1);
        }
    }

    static Vector2[] Offset(Vector2[] Path, float By)
    {
        var Out = new Vector2[Path.Length];
        for (int I = 0; I < Path.Length; I++)
        {
            Vector2 Along = (Path[Math.Min(I + 1, Path.Length - 1)] - Path[Math.Max(I - 1, 0)]).Normalized();
            Out[I] = Path[I] + new Vector2(-Along.Y, Along.X) * By;
        }
        return Out;
    }

    void DrawPond(WorldGeometry.Pond Pd, bool Lilies = false)
    {
        float Clk = (float)Clock;
        var Edge = Pd.Edge;
        if (!PondLook.TryGetValue(Edge, out var Look))
        {
            Look = (Array.ConvertAll(Edge, P => Pd.Centre + (P - Pd.Centre) * 1.0f + (P - Pd.Centre).Normalized() * 4),
                    Array.ConvertAll(Edge, P => Pd.Centre + (P - Pd.Centre) * 0.55f), Closed(Edge));
            PondLook[Edge] = Look;
        }
        FillStatic(Look.Bank, BANK, false);
        FillStatic(Edge, WATER, false);
        FillStatic(Look.Deep, DEEP, false);
        DrawPolyline(Look.Rim, new Color(0.04f, 0.08f, 0.1f, 0.35f), 1);
        // ripples
        for (int K = 0; K < 5; K++)
        {
            float A = K * 1.3f + Clk * 0.25f;
            Vector2 P = Pd.Centre + Vector2.FromAngle(A) * Pd.Radius * (0.35f + 0.12f * K);
            var Col = RIPPLE;
            Col.A *= 0.5f + 0.5f * MathF.Sin(Clk * 1.5f + K);
            DrawLine(P.Round(), (P + new Vector2(4, 0)).Round(), Col, 1);
        }
        // lily pads on the Rainwater Pond
        if (Lilies) DrawLilies(Pd);
    }
}
