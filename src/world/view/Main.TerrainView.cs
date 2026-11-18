using Godot;
using System;
using System.Collections.Generic;
using HoldTilDawn;

// Owner: Vu Duc Viet Anh. Draws the terrain of src/world/WorldTerrain.cs on the ground layer (under everything
// with height): dirt trails, plateaus with their cliff faces and ramps, streams and ponds with moving ripples,
// and the wooden bridges. Same pixel-art palette as art/nature (tools/PixelNature.cs); light from the upper left.
public partial class Main : Node2D
{
    static readonly Color TRAIL_EDGE = new Color("75694A"), TRAIL_MID = new Color("8C7F5B"), TRAIL_LIT = new Color("9C8F69");
    static readonly Color BANK = new Color("4A5638"), WATER = new Color("3B6A86"), DEEP = new Color("2F5A75"), RIPPLE = new Color(0.62f, 0.8f, 0.88f, 0.75f);
    static readonly Color[] FACE = { new Color("4F4A42"), new Color("6E675B"), new Color("8A8273") };
    static readonly Color FACE_FOOT = new Color("37332D"), RIM = new Color("A9B482");
    static readonly Color RAMP = new Color("958060"), RAMP_STEP = new Color("6E5D42");
    static readonly Color PLANK = new Color("8A6440"), PLANK_GAP = new Color("5E4128"), RAIL = new Color("3E2B1E");

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

    public void DrawTerrain(Rect2 View)
    {
        WorldGeometry W = (WorldGeometry)Geometry;
        float Tl = (float)T;
        foreach (var Tr in W.Trails)
        {
            if (!Bounds(Tr, Tl * 2).Intersects(View)) continue;
            DrawPolyline(Tr, TRAIL_EDGE, WorldGeometry.TRAIL_W);
            DrawPolyline(Tr, TRAIL_MID, WorldGeometry.TRAIL_W - 6);
            // wheel ruts / footpath wear
            DrawPolyline(Offset(Tr, -0.45f * Tl), TRAIL_LIT, 2);
            DrawPolyline(Offset(Tr, 0.45f * Tl), TRAIL_EDGE, 2);
        }
        foreach (var Pl in W.Plateaus)
            if (Bounds(Pl.Edge, Tl * 2).Intersects(View)) DrawPlateau(Pl);
        foreach (var St in W.Streams)
            if (Bounds(St.Path, Tl * 3).Intersects(View)) DrawStream(St);
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

    void DrawPlateau(WorldGeometry.Plateau Pl)
    {
        float H = WorldGeometry.CLIFF_H, Tl = (float)T;
        int N = Pl.Edge.Length;
        // the raised top catches more light than the ground around it
        this.Ground(Pl.Edge, (Gd.MakeColor("7C8D58") * 1.15f));
        // outward direction at each corner, so neighbouring slope pieces meet without gaps
        var Vn = Array.ConvertAll(Pl.Edge, P => (P - Pl.Centre).Normalized());
        for (int I = 0; I < N; I++)
        {
            Vector2 A = Pl.Edge[I], B = Pl.Edge[(I + 1) % N];
            Vector2 Na = Vn[I], Nb = Vn[(I + 1) % N];
            Vector2 Out = (((A + B) / 2) - Pl.Centre).Normalized();
            bool Faces = Out.Y > 0.12f;
            if (Pl.Ramp[I])
            {
                if (Faces)
                {
                    // a slope down the front: packed dirt with steps
                    DrawColoredPolygon(new[] { A - Na * 0.4f * Tl, B - Nb * 0.4f * Tl, B + new Vector2(0, H), A + new Vector2(0, H) }, RAMP);
                    for (int S = 1; S <= 3; S++)
                    {
                        Vector2 Dy = new Vector2(0, H * S / 4f);
                        DrawLine(A + Dy, B + Dy, RAMP_STEP, 1);
                    }
                }
                else
                {
                    // a slope going down away from the camera: a dirt band across the edge with contour lines
                    DrawColoredPolygon(new[] { A - Na * 0.6f * Tl, B - Nb * 0.6f * Tl, B + Nb * 0.9f * Tl, A + Na * 0.9f * Tl }, RAMP);
                    for (int S = 0; S < 3; S++)
                    {
                        float K = (-0.3f + S * 0.45f) * Tl;
                        DrawLine(A + Na * K, B + Nb * K, RAMP_STEP, 1);
                    }
                }
                continue;
            }
            if (Faces)
            {
                // the cliff face below a front edge, lit from the left
                int Shade = Out.X < -0.35f ? 2 : (Out.X > 0.35f ? 0 : 1);
                var Foot = new Vector2(0, H);
                // lit upper band, darker lower band, a stratum line between them
                DrawColoredPolygon(new[] { A, B, B + Foot * 0.55f, A + Foot * 0.55f }, FACE[Shade]);
                DrawColoredPolygon(new[] { A + Foot * 0.55f, B + Foot * 0.55f, B + Foot, A + Foot }, FACE[Math.Max(0, Shade - 1)]);
                DrawLine(A + Foot * 0.55f, B + Foot * 0.55f, FACE[0], 1);
                // cracks and chipped highlights, fixed per edge
                for (int K = 0; K < 2; K++)
                {
                    float Kx = (((I * 37) + (K * 53)) % 10) / 10f;
                    Vector2 Ck = A.Lerp(B, 0.15f + 0.7f * Kx);
                    DrawLine(Ck + new Vector2(0, 2 + K * 6), Ck + new Vector2(K == 0 ? 1 : -1, H * (0.5f + 0.25f * K)), FACE[0], 1);
                    DrawLine(Ck + new Vector2(-2, 4 + K * 9), Ck + new Vector2(1, 4 + K * 9), FACE[2].Lightened(0.15f), 1);
                }
                DrawLine(A + Foot, B + Foot, FACE_FOOT, 2);
                // grass hanging over the top of the cliff
                float Len = A.DistanceTo(B);
                for (float S = 3; S < Len - 2; S += 6)
                {
                    Vector2 G = A.Lerp(B, S / Len);
                    DrawColoredPolygon(new[] { G - new Vector2(3, 0), G + new Vector2(3, 0), G + new Vector2(0, 3 + ((int)S % 3)) }, new Color("6F8450"));
                }
                // a soft shadow on the ground at the foot of the cliff
                DrawColoredPolygon(new[] { A + Foot, B + Foot, B + Foot + new Vector2(3, 5), A + Foot + new Vector2(3, 5) }, new Color(0.04f, 0.06f, 0.1f, 0.28f));
            }
            // rim of the top
            DrawLine(A, B, Faces ? RIM : FACE[0], Faces ? 2 : 1.5f);
        }
    }

    void DrawStream(WorldGeometry.Stream St)
    {
        float Wd = WorldGeometry.WATER_W;
        // banks, water and the deep middle; circles at the joints keep the bends round
        foreach (var (Col, Width) in new[] { (BANK, Wd + 8), (WATER, Wd), (DEEP, Wd * 0.45f) })
        {
            DrawPolyline(St.Path, Col, Width);
            for (int I = 0; I < St.Path.Length; I += 2) DrawCircle(St.Path[I], Width / 2, Col);
        }
        DrawCircle(St.Pond, St.PondR + 4, BANK);
        DrawCircle(St.Pond, St.PondR, WATER);
        DrawCircle(St.Pond, St.PondR * 0.55f, DEEP);
        // ripples drifting downstream
        float Clk = (float)Clock;
        for (int I = 2; I < St.Path.Length - 2; I += 3)
        {
            Vector2 Along = (St.Path[I + 1] - St.Path[I - 1]).Normalized();
            Vector2 Across = new Vector2(-Along.Y, Along.X);
            float Ph = (Clk * 0.6f + I * 0.37f) % 1.0f;
            Vector2 P = St.Path[I] + Along * (Ph * 12) + Across * MathF.Sin(I * 1.7f) * Wd * 0.28f;
            var Col = RIPPLE;
            Col.A *= MathF.Sin(Ph * MathF.PI);
            DrawLine(P.Round(), (P + Along * 4).Round(), Col, 1);
        }
        for (int K = 0; K < 5; K++)
        {
            float A = K * 1.3f + Clk * 0.25f;
            Vector2 P = St.Pond + Vector2.FromAngle(A) * St.PondR * (0.35f + 0.12f * K);
            var Col = RIPPLE;
            Col.A *= 0.5f + 0.5f * MathF.Sin(Clk * 1.5f + K);
            DrawLine(P.Round(), (P + new Vector2(4, 0)).Round(), Col, 1);
        }
        foreach (var (At, Across) in St.Bridges) DrawBridge(At, Across);
    }

    void DrawBridge(Vector2 At, Vector2 Across)
    {
        float Tl = (float)T;
        Vector2 Along = new Vector2(Across.Y, -Across.X);
        float L = WorldGeometry.WATER_W / 2 + 0.7f * Tl, Wd = 0.75f * Tl;
        Vector2[] Deck = { At - Across * L - Along * Wd, At + Across * L - Along * Wd, At + Across * L + Along * Wd, At - Across * L + Along * Wd };
        var Shadow = Array.ConvertAll(Deck, P => P + new Vector2(3, 5));
        DrawColoredPolygon(Shadow, new Color(0.02f, 0.05f, 0.08f, 0.35f));
        DrawColoredPolygon(Deck, PLANK);
        // planks across the walking direction
        for (float S = -L + 5; S < L; S += 5)
            DrawLine(At + Across * S - Along * Wd, At + Across * S + Along * Wd, PLANK_GAP, 1);
        // rails and posts
        foreach (float Side in new[] { -1f, 1f })
        {
            Vector2 O = Along * (Wd - 1.5f) * Side;
            DrawLine(At - Across * L + O, At + Across * L + O, RAIL, 2);
            foreach (float E in new[] { -L, 0, L })
                DrawRect(new Rect2(At + Across * E + O - new Vector2(2, 2), new Vector2(4, 4)), RAIL);
        }
    }
}
