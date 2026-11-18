using Godot;
using System;
using System.Collections.Generic;
using FileAccess = Godot.FileAccess;

// Nature pixel art for the island (GDD 4.3 / 6.1): pines and dead trees for the outer forest, rock outcrops,
// shrubs, grass tufts and pebbles, the four resource nodes and what is left of them once gathered, and the
// tileable ground texture. Same rules as tools/PixelChars.cs and tools/PixelProps.cs: 1 px = 1 game px
// (32 px tile), muted GDD palette, light from the upper left, a dark outline, a soft shadow to the lower right.
// Shapes are built from a fixed seed, so every run writes the same PNGs.
// Not part of the game. Run: godot --headless --path . tools/PixelNature.tscn   (then --import once)
// Writes art/nature/<name>_<variant>.png, art/nature/ground.png and art/nature/index.json
// ({name: [anchor_x, anchor_y, solid_radius_px]}: the anchor is the pixel on the ground point; radius 0 = walk through).
// The PNGs are plain pixel art and can be repainted by hand; keep the size or update the anchor in index.json.
public partial class PixelNature : Node
{
    // dark to light
    static readonly Color[] PINE = Hex("16302B", "21453A", "2F5C47", "437554", "5E9062", "7FAA72");
    static readonly Color[] FIR = Hex("1B3A2C", "285239", "3A6B45", "52864F", "6FA25C", "93BE6E");
    static readonly Color[] BARK = Hex("3E2B1E", "5A3E26", "7A5634");
    static readonly Color[] DEAD = Hex("3A3431", "4F4843", "675E56", "857B70", "A39A8E");
    static readonly Color[] ROCK = Hex("3F474F", "566068", "707A81", "8C959B", "A9B1B5");
    static readonly Color[] STONE = Hex("565B66", "7D838C", "A3A8B0", "BEC3CA", "D5DAE1");
    static readonly Color[] MOSS = Hex("4B5E3A", "5F7446", "778C52");
    static readonly Color[] SHRUB = Hex("2A3F2D", "3A5538", "4E6C44", "668650", "819E5E");
    static readonly Color[] HERB = Hex("2E7D3E", "4FB862", "7ACC6C", "9BE37A");
    static readonly Color[] GRASS = Hex("4F6438", "667E45", "819A55", "9CB267");
    static readonly Color[] DRY = Hex("7A6E45", "978B56", "B5A96E", "CFC48A");
    static readonly Color[] PAINT = Hex("3E4A5C", "55657A", "6F8399", "8FA3B8", "B4C4D4");
    static readonly Color[] RUST = Hex("4E2A1A", "6E3B22", "8E4E2C", "B0663A", "C9844E");
    static readonly Color[] CUT = Hex("A0703C", "C08A50", "E0B070");
    static readonly Color GLASS = new Color("2B3A48");
    static readonly Color GLINT = new Color("9FB4C4");
    static readonly Color TYRE = new Color("202228");
    static readonly Color RIM = new Color("6A6F7C");
    static readonly Color PETAL = new Color("F2F2F2");
    static readonly Color PETAL_SHADE = new Color("C8D2CC");
    static readonly Color INK = new Color("141019");
    static readonly Color SHADOW = new Color(0.04f, 0.06f, 0.1f, 0.38f);
    // light comes from the upper left and a little towards the viewer
    static readonly Vector3 LIGHT = new Vector3(-0.55f, -0.62f, 0.56f).Normalized();
    static readonly Vector2 LIGHT2 = new Vector2(-0.7f, -0.7f).Normalized();
    static readonly float[] BAYER = { 0.0f, 0.5f, 0.75f, 0.25f };

    readonly Godot.Collections.Dictionary Index = new();
    string Out = "res://art/nature";

    public override void _Ready()
    {
        foreach (string Arg in OS.GetCmdlineUserArgs())
            if (Arg.StartsWith("--out=", StringComparison.Ordinal)) Out = Arg[6..];
        DirAccess.MakeDirRecursiveAbsolute(Out);

        // outer forest (GDD 4.3: only pines and dead trees on the cold island)
        int[][] Pines = { new[] { 36, 56 }, new[] { 40, 62 }, new[] { 44, 68 }, new[] { 38, 60 }, new[] { 42, 66 }, new[] { 34, 52 } };
        for (int I = 0; I < Pines.Length; I++)
            Save("pine", I, Pine(101 + I, Pines[I][0], Pines[I][1], PINE, false), 7);
        int[][] Deads = { new[] { 40, 56 }, new[] { 44, 62 }, new[] { 36, 50 }, new[] { 42, 60 } };
        for (int I = 0; I < Deads.Length; I++)
            Save("dead", I, DeadTree(201 + I, Deads[I][0], Deads[I][1]), 6);
        for (int I = 0; I < 5; I++)
        {
            var (Rock, R) = Outcrop(301 + I, I);
            Save("rock", I, Rock, R);
        }
        for (int I = 0; I < 3; I++)
            Save("shrub", I, Shrub(401 + I), 0);
        // rock spires that line the ridges (impassable) and reeds along the streams (walk-through)
        for (int I = 0; I < 3; I++)
            Save("crag", I, Crag(451 + I, I), 12);
        for (int I = 0; I < 3; I++)
            Save("reed", I, Reed(481 + I), 0);
        // ground scatter, drawn flat under everything
        for (int I = 0; I < 4; I++)
            Save("grass", I, Tuft(501 + I, GRASS, 4 + I % 3), 0);
        for (int I = 0; I < 2; I++)
            Save("grass_dry", I, Tuft(551 + I, DRY, 5 + I), 0);
        for (int I = 0; I < 4; I++)
            Save("pebble", I, Pebble(601 + I, 3 + I % 3, 2 + I % 2), 0);

        // resource nodes (MEC-08): each kind has a look of its own so it is never mistaken for scenery
        for (int I = 0; I < 3; I++)
            Save("node_wood", I, Pine(701 + I, 32 + 2 * I, 50 + 3 * I, FIR, true), 8);
        for (int I = 0; I < 2; I++)
            Save("node_stone", I, StoneNode(801 + I), 20);
        for (int I = 0; I < 2; I++)
            Save("node_herb", I, HerbNode(901 + I), 0);
        Save("node_metal", 0, Wreck(1001), 19);
        Save("node_metal", 1, ScrapHeap(1002), 19);
        // what is left of a gathered node until dawn
        Save("stump", 0, Stump(1101), 0);
        Save("rubble", 0, Rubble(1201), 0);
        Save("herb_cut", 0, HerbCut(1301), 0);
        Save("scrap", 0, ScrapLeft(1401), 0);

        Ground(1501).SavePng(Out.PathJoin("ground.png"));
        GD.Print("drew ground (256, 256)");

        using var Fa = FileAccess.Open(Out.PathJoin("index.json"), FileAccess.ModeFlags.Write);
        Fa.StoreString(Json.Stringify(Index, "\t"));
        GetTree().Quit();
    }

    void Save(string Name, int Variant, Canvas C, int Radius)
    {
        string N = Name + "_" + Variant;
        C.ToImage().SavePng(Out.PathJoin(N + ".png"));
        Index[N] = new Godot.Collections.Array { C.Anchor.X, C.Anchor.Y, Radius };
        GD.Print("drew ", N, " (", C.W, ", ", C.H, ")");
    }

    // --- trees ------------------------------------------------------------------
    // A conifer seen from the 3/4 camera: a trunk and 4-5 drooping tiers of needles, lit from the left.
    // A wood node (Node = true) uses the fresher FIR greens, shows an axe notch and has chips at its foot.
    Canvas Pine(int Seed, int W, int H, Color[] Pal, bool Node)
    {
        var R = new Random(Seed);
        var C = new Canvas(W + 10, H + 5);
        int Cx = W / 2 + 2;
        int Gy = C.H - 3;
        C.Anchor = new Vector2I(Cx, Gy);
        C.Shadow(Cx + 4, Gy, W * 0.44f, 3.4f);
        int Tw = W >= 40 ? 5 : 4;
        int Left = Cx - Tw / 2;
        for (int Y = Gy - 12; Y <= Gy; Y++)
        {
            int Flare = Y >= Gy - 1 ? 1 : 0;
            for (int X = Left - Flare; X < Left + Tw + Flare; X++)
            {
                int B = X <= Left ? 2 : (X >= Left + Tw - 1 ? 0 : 1);
                C.Put(X, Y, BARK[B]);
            }
        }
        if (Node)
        {
            // axe notch: pale cut wood on the lit side, and chips on the ground
            C.Put(Left + Tw - 2, Gy - 3, CUT[2]);
            C.Put(Left + Tw - 1, Gy - 3, CUT[1]);
            C.Put(Left + Tw - 1, Gy - 4, CUT[0]);
            C.Put(Left + Tw - 2, Gy - 2, BARK[0]);
            C.Put(Cx + 5, Gy, CUT[2], 3);
            C.Put(Cx + 6, Gy + 1, CUT[0], 3);
            C.Put(Cx - 6, Gy + 1, CUT[1], 3);
        }
        int N = H >= 60 ? 5 : 4;
        float Top = Gy - H + 1, Bottom = Gy - 7;
        var Band = new int[C.W * C.H];
        for (int I = N - 1; I >= 0; I--)
        {
            float Yb = Top + (Bottom - Top) * (I + 1) / N;
            float Yt = I == 0 ? Top : Top + (Bottom - Top) * (I - 0.85f) / N;
            float Hw = W * 0.5f * (0.34f + 0.66f * (I + 1f) / N);
            int Phase = R.Next(4);
            for (int Y = (int)Yt; Y <= (int)Yb + 3; Y++)
            {
                float V = Math.Clamp((Y - Yt) / (Yb - Yt), 0, 1);
                float Half = 1.0f + Hw * MathF.Pow(V, 0.8f);
                for (int X = (int)(Cx - Hw - 2); X <= (int)(Cx + Hw + 2); X++)
                {
                    float Dx = X - Cx;
                    if (MathF.Abs(Dx) > Half) continue;
                    // drooping bough tips along the bottom edge, lower towards the outside
                    bool Tip = ((int)MathF.Abs(Dx) + Phase) % 4 < 2;
                    float Edge = Yb + 2.0f * MathF.Abs(Dx) / Hw - (Tip ? 0 : 1.6f);
                    if (Y > Edge) continue;
                    float T = Dx / Hw;
                    float L = 0.62f - 0.4f * T + 0.16f * (1 - V) + Noise(R) * 0.2f;
                    if (Y > Edge - 1.5f) L -= 0.22f;
                    int B = Quant(L, Pal.Length - 1, X, Y);
                    C.Put(X, Y, Pal[B], 2, I + 1);
                    Band[Y * C.W + X] = B;
                }
            }
        }
        // a tier hangs over the one below it: darken the boughs just under the upper tier's edge
        for (int Y = C.H - 1; Y >= 1; Y--)
            for (int X = 0; X < C.W; X++)
            {
                int T = C.TagAt(X, Y);
                if (T <= 1) continue;
                int Above = C.TagAt(X, Y - 1), Above2 = C.TagAt(X, Y - 2);
                if ((Above > 0 && Above < T) || (Above2 > 0 && Above2 < T))
                {
                    int B = Math.Max(0, Band[Y * C.W + X] - 2);
                    C.Put(X, Y, Pal[B], 2, T);
                }
            }
        // the very top catches the most light
        C.Put(Cx, (int)Top, Pal[^1], 2, 1);
        C.Outline(Pal[0].Darkened(0.55f));
        return C;
    }

    // A bare grey tree: a leaning trunk that splits into thinning limbs, shaded along each limb.
    Canvas DeadTree(int Seed, int W, int H)
    {
        var R = new Random(Seed);
        var C = new Canvas(W + 10, H + 5);
        int Cx = W / 2 + 2;
        int Gy = C.H - 3;
        C.Anchor = new Vector2I(Cx, Gy);
        C.Shadow(Cx + 4, Gy, W * 0.3f, 3.0f);
        var Segs = new List<(Vector2 A, Vector2 B, float Ra, float Rb)>();
        float Lean = (float)(R.NextDouble() - 0.5) * 6;
        var Foot = new Vector2(Cx + 0.5f, Gy + 0.5f);
        var Crown = new Vector2(Cx + 0.5f + Lean, Gy - H * 0.45f);
        Segs.Add((Foot, Crown, 2.9f, 2.0f));
        // roots
        Segs.Add((Foot + new Vector2(0, -1), Foot + new Vector2(-4, 1), 1.4f, 0.6f));
        Segs.Add((Foot + new Vector2(0, -1), Foot + new Vector2(4, 1), 1.4f, 0.6f));
        // a side limb from the trunk
        var Mid = Foot.Lerp(Crown, 0.55f);
        float Side = R.Next(2) == 0 ? -1 : 1;
        Grow(Segs, R, Mid, -MathF.PI / 2 + Side * 1.0f, H * 0.2f, 1.3f, 1, W * 0.5f);
        // the crown splits in two or three
        int Limbs = 2 + R.Next(2);
        for (int K = 0; K < Limbs; K++)
        {
            float A = -MathF.PI / 2 + (K - (Limbs - 1) / 2f) * 0.8f + (float)(R.NextDouble() - 0.5) * 0.25f;
            Grow(Segs, R, Crown, A, H * 0.24f, 1.9f, 2, W * 0.5f);
        }
        // shrink the crown about the foot until every twig fits inside the sprite
        float MinX = Foot.X, MaxX = Foot.X, MinY = Foot.Y;
        foreach (var S in Segs)
            foreach (var P in new[] { S.A, S.B })
            {
                MinX = MathF.Min(MinX, P.X); MaxX = MathF.Max(MaxX, P.X); MinY = MathF.Min(MinY, P.Y);
            }
        float Fit = MathF.Min(1, MathF.Min((Foot.Y - 3) / MathF.Max(1, Foot.Y - MinY),
            MathF.Min((Foot.X - 3) / MathF.Max(1, Foot.X - MinX), (C.W - 4 - Foot.X) / MathF.Max(1, MaxX - Foot.X))));
        for (int I = 0; I < Segs.Count; I++)
        {
            var S = Segs[I];
            Segs[I] = (Foot + (S.A - Foot) * Fit, Foot + (S.B - Foot) * Fit, S.Ra, S.Rb);
        }
        Raster(C, Segs, DEAD);
        // a dark knot hole on the trunk
        var Knot = Foot.Lerp(Crown, 0.35f);
        C.Put((int)Knot.X, (int)Knot.Y, DEAD[0]);
        C.Outline(DEAD[0].Darkened(0.6f));
        return C;
    }

    void Grow(List<(Vector2, Vector2, float, float)> Segs, Random R, Vector2 P, float Ang, float Len, float Rad, int Depth, float MaxHalf)
    {
        Ang = Math.Clamp(Ang, -MathF.PI + 0.35f, -0.35f);
        var Q = P + new Vector2(MathF.Cos(Ang), MathF.Sin(Ang)) * Len;
        float Rq = MathF.Max(0.55f, Rad * 0.66f);
        Segs.Add((P, Q, Rad, Rq));
        if (Depth == 0) return;
        int Kids = R.Next(3) == 0 ? 1 : 2;
        for (int K = 0; K < Kids; K++)
        {
            float Spread = (K - (Kids - 1) / 2f) * (0.55f + (float)R.NextDouble() * 0.3f);
            Grow(Segs, R, Q, Ang + Spread, Len * (0.62f + (float)R.NextDouble() * 0.16f), Rq, Depth - 1, MaxHalf);
        }
    }

    // Thick tapered segments, each pixel shaded by which side of its limb it lies on.
    static void Raster(Canvas C, List<(Vector2 A, Vector2 B, float Ra, float Rb)> Segs, Color[] Pal)
    {
        for (int Y = 0; Y < C.H; Y++)
            for (int X = 0; X < C.W; X++)
            {
                var P = new Vector2(X + 0.5f, Y + 0.5f);
                float Best = 2;
                float Side = 0, Rad = 1;
                foreach (var S in Segs)
                {
                    var Ab = S.B - S.A;
                    float T = Math.Clamp((P - S.A).Dot(Ab) / Math.Max(Ab.LengthSquared(), 0.001f), 0, 1);
                    var Q = S.A + Ab * T;
                    float Rr = Mathf.Lerp(S.Ra, S.Rb, T);
                    float D = P.DistanceTo(Q) / Rr;
                    if (D < Best)
                    {
                        Best = D;
                        Rad = Rr;
                        Side = (P - Q).Dot(LIGHT2) / Rr;
                    }
                }
                if (Best > 1) continue;
                int B;
                if (Rad < 1.0f) B = Side > 0.2f ? 2 : 1;
                else B = Side > 0.55f ? 4 : Side > 0.15f ? 3 : Side > -0.35f ? 2 : 1;
                C.Put(X, Y, Pal[B]);
            }
    }

    // --- rocks ------------------------------------------------------------------
    // One faceted boulder: a lumpy dome whose sides are flat facets lit from the upper left, with a flatter top
    // face. Where it overlaps a boulder drawn before it, its rim is drawn as a dark seam.
    // Taper narrows the upper half toward a point (0 = a dome, 0.8 = a spire).
    static void Boulder(Canvas C, Random R, float Cx, float By, float Rx, float Ry, Color[] Pal, bool Moss, int Tag, float Taper = 0)
    {
        const int K = 7;
        var Rad = new float[K];
        var Facet = new float[K];
        for (int I = 0; I < K; I++)
        {
            Rad[I] = 0.84f + (float)R.NextDouble() * 0.16f;
            Facet[I] = (I + 0.5f) / K * MathF.Tau - MathF.PI + (float)(R.NextDouble() - 0.5) * 0.3f;
        }
        float Cy = By - Ry * 0.8f;
        float TopR = 0.5f + (float)R.NextDouble() * 0.12f;
        int MossSeed = R.Next();
        for (int Y = (int)(Cy - Ry - 1); Y <= (int)By; Y++)
            for (int X = (int)(Cx - Rx - 1); X <= (int)(Cx + Rx + 1); X++)
            {
                float Dy = (Y + 0.5f - Cy) / Ry;
                float Dx = (X + 0.5f - Cx) / (Rx * (1 - Taper * Math.Clamp(-Dy, 0, 1)));
                float Th = MathF.Atan2(Dy, Dx);
                float U = (Th + MathF.PI) / MathF.Tau * K;
                int J = (int)MathF.Floor(U) % K;
                float Rr = Mathf.Lerp(Rad[J], Rad[(J + 1) % K], U - MathF.Floor(U));
                float D = MathF.Sqrt(Dx * Dx + Dy * Dy);
                if (D > Rr) continue;
                bool Seam = D / Rr > 0.9f && C.Body(X, Y) && C.TagAt(X, Y) != Tag;
                if (Seam)
                {
                    C.Put(X, Y, Pal[0], 2, Tag);
                    continue;
                }
                // the top face sits a little higher than the dome's centre (3/4 view)
                float Tdx = Dx, Tdy = Dy + 0.3f;
                bool TopFace = MathF.Sqrt(Tdx * Tdx + Tdy * Tdy) < TopR * Rr;
                Vector3 N;
                if (TopFace) N = new Vector3(-0.08f, -0.3f, 1).Normalized();
                else
                {
                    float F = Facet[J];
                    N = new Vector3(MathF.Cos(F) * 0.9f, MathF.Sin(F) * 0.9f - 0.1f, 0.42f).Normalized();
                }
                float L = Math.Clamp((N.Dot(LIGHT) + 0.3f) / 1.2f, 0, 1);
                // the foot of the rock sits in its own shade
                if (Y >= By - 1) L -= 0.25f;
                int B = Quant(L, Pal.Length - 1, X, Y, 0f);
                Color Col = Pal[B];
                if (Moss && TopFace && Value(X, Y, 4, MossSeed) > 0.52f)
                    Col = MOSS[Math.Clamp(B - 2, 0, MOSS.Length - 1)];
                C.Put(X, Y, Col, 2, Tag);
            }
        // a crack or two running down from the top face, lit on its left edge
        int Cracks = 1 + R.Next(2);
        for (int I = 0; I < Cracks; I++)
        {
            int X = (int)(Cx + (R.NextDouble() - 0.5) * Rx * 0.8);
            int Y = (int)(Cy - Ry * 0.1f);
            int Len = 3 + R.Next((int)Math.Max(2, Ry * 0.6f));
            for (int S = 0; S < Len; S++)
            {
                if (!C.Body(X, Y) || C.TagAt(X, Y) != Tag) break;
                C.Put(X, Y, Pal[0], 2, Tag);
                if (C.Body(X - 1, Y) && C.TagAt(X - 1, Y) == Tag) C.Put(X - 1, Y, Pal[^1], 2, Tag);
                Y++;
                X += R.Next(3) - 1;
            }
        }
    }

    // Scenery outcrops: a single boulder, a big one with a small one in front, a pair, a tall standing stone,
    // a low wide slab. Returns the sprite and its solid radius.
    (Canvas, int) Outcrop(int Seed, int Shape)
    {
        var R = new Random(Seed);
        Canvas C;
        float Cx, By;
        int Radius;
        switch (Shape)
        {
            case 0:
                C = new Canvas(40, 30); Cx = 18; By = 26;
                Boulder(C, R, Cx, By, 15, 11, ROCK, true, 1);
                Radius = 13; break;
            case 1:
                C = new Canvas(52, 38); Cx = 25; By = 33;
                Boulder(C, R, Cx + 2, By - 2, 19, 14, ROCK, true, 1);
                Boulder(C, R, Cx - 11, By + 1, 8, 6, ROCK, false, 2);
                Radius = 18; break;
            case 2:
                C = new Canvas(50, 32); Cx = 24; By = 28;
                Boulder(C, R, Cx - 8, By - 1, 13, 10, ROCK, true, 1);
                Boulder(C, R, Cx + 9, By + 1, 11, 8, ROCK, true, 2);
                Radius = 17; break;
            case 3:
                C = new Canvas(32, 42); Cx = 15; By = 38;
                Boulder(C, R, Cx, By, 11, 17, ROCK, true, 1);
                Radius = 10; break;
            default:
                C = new Canvas(56, 30); Cx = 27; By = 26;
                Boulder(C, R, Cx, By, 23, 10, ROCK, true, 1);
                Boulder(C, R, Cx + 14, By + 2, 6, 5, ROCK, false, 2);
                Radius = 19; break;
        }
        C.ShadowUnder(Cx + 4, By, C.W * 0.4f, 4);
        C.Anchor = new Vector2I((int)Cx, (int)(By - 4));
        C.Outline(ROCK[0].Darkened(0.6f));
        return (C, Radius);
    }

    // A crag: two or three tall jagged spires with rubble at the foot, the pieces of a rock ridge.
    Canvas Crag(int Seed, int Shape)
    {
        var R = new Random(Seed);
        var C = new Canvas(38, 54);
        float Cx = 18, By = 49;
        C.Shadow(Cx + 5, By, 15, 4);
        float[][] Spires = Shape switch
        {
            0 => new[] { new[] { -5f, 0f, 7f, 20f }, new[] { 5f, 2f, 6f, 15f }, new[] { 0f, 4f, 5f, 9f } },
            1 => new[] { new[] { 3f, 0f, 8f, 22f }, new[] { -7f, 2f, 6f, 13f } },
            _ => new[] { new[] { -6f, 0f, 6f, 16f }, new[] { 4f, 0f, 7f, 21f }, new[] { -1f, 4f, 6f, 10f } },
        };
        int Tag = 1;
        foreach (var Sp in Spires)
            Boulder(C, R, Cx + Sp[0], By - Sp[1], Sp[2], Sp[3], ROCK, false, Tag++, 0.85f);
        // loose rocks at the foot
        foreach (var (X, Y) in new[] { (6, 48), (29, 49), (10, 50) })
        {
            C.Put(X, Y, ROCK[3]); C.Put(X + 1, Y, ROCK[2]); C.Put(X, Y + 1, ROCK[1]); C.Put(X + 1, Y + 1, ROCK[0]);
        }
        C.Anchor = new Vector2I((int)Cx, (int)By - 3);
        C.Outline(ROCK[0].Darkened(0.6f));
        return C;
    }

    // Reeds by the water: thin stems, a few with brown seed heads.
    Canvas Reed(int Seed)
    {
        var R = new Random(Seed);
        var C = new Canvas(16, 22);
        int Gy = 20;
        Color[] Stem = Hex("3F5A33", "56733F", "6F8C4A", "8CA65C");
        Color Head = new Color("6B4A2B"), HeadLit = new Color("8C6239");
        int N = 5 + R.Next(3);
        for (int I = 0; I < N; I++)
        {
            float X0 = 8 + (I - (N - 1) / 2f) * 1.5f;
            int H = 8 + R.Next(9);
            float Lean = (X0 - 8) * 0.25f + (float)(R.NextDouble() - 0.5);
            int Tx = 0, Ty = 0;
            for (int S = 0; S <= H; S++)
            {
                Tx = (int)MathF.Round(X0 + Lean * S / H);
                Ty = Gy - S;
                C.Put(Tx, Ty, Stem[Math.Min(Stem.Length - 1, S * Stem.Length / (H + 1))], 3);
            }
            if (R.Next(2) == 0)
            {
                C.Put(Tx, Ty + 1, Head, 3); C.Put(Tx, Ty + 2, Head, 3); C.Put(Tx, Ty + 3, Head, 3);
                C.Put(Tx, Ty + 1, HeadLit, 3);
            }
        }
        C.Anchor = new Vector2I(8, Gy);
        return C;
    }

    // The stone node: a pale quarry boulder with fresh pick marks and loose chips, unlike the dark mossy outcrops.
    Canvas StoneNode(int Seed)
    {
        var R = new Random(Seed);
        var C = new Canvas(48, 36);
        float Cx = 22, By = 31;
        Boulder(C, R, Cx, By - 1, 19, 13, STONE, false, 1);
        if (Seed % 2 == 1) Boulder(C, R, Cx + 12, By + 1, 7, 5, STONE, false, 2);
        // pick marks: a bright chipped face with a dark notch under it
        for (int I = 0; I < 3; I++)
        {
            int X = (int)(Cx - 10 + I * 8 + R.Next(3)), Y = (int)(By - 14 + R.Next(6));
            if (!C.Body(X, Y)) continue;
            C.Put(X, Y, STONE[^1]);
            C.Put(X + 1, Y, STONE[^1]);
            C.Put(X, Y + 1, STONE[0]);
            C.Put(X + 1, Y + 1, STONE[1]);
        }
        C.ShadowUnder(Cx + 4, By, 18, 4);
        // loose chips around the foot
        foreach (var (X, Y) in new[] { (5, 30), (9, 33), (37, 33), (41, 30) })
        {
            C.Put(X, Y, STONE[3]);
            C.Put(X + 1, Y, STONE[1]);
        }
        C.Anchor = new Vector2I((int)Cx, (int)(By - 5));
        C.Outline(STONE[0].Darkened(0.6f));
        return C;
    }

    Canvas Rubble(int Seed)
    {
        var R = new Random(Seed);
        var C = new Canvas(30, 16);
        C.ShadowUnder(16, 11, 11, 3);
        foreach (var (X, Y, W) in new[] { (6, 9, 4), (12, 11, 3), (17, 8, 5), (22, 11, 3), (14, 6, 2) })
        {
            for (int Dx = 0; Dx < W; Dx++)
            {
                C.Put(X + Dx, Y - 1, STONE[Dx == 0 ? 4 : 3]);
                C.Put(X + Dx, Y, STONE[Dx == W - 1 ? 1 : 2]);
            }
            if (R.Next(2) == 0) C.Put(X + 1, Y - 2, STONE[3]);
        }
        C.Anchor = new Vector2I(15, 10);
        C.Outline(STONE[0].Darkened(0.6f));
        return C;
    }

    // --- plants -----------------------------------------------------------------
    // A low round bush: a few leafy balls lit from the upper left, seams where they overlap.
    Canvas Shrub(int Seed)
    {
        var R = new Random(Seed);
        var C = new Canvas(30, 22);
        float Cx = 13, By = 18;
        C.Shadow(Cx + 3, By, 12, 3);
        int Balls = 4 + R.Next(2);
        var List = new List<(float X, float Y, float R)>();
        for (int I = 0; I < Balls; I++)
        {
            float T = I / (Balls - 1f) - 0.5f;
            float Rad = 4.2f + (float)R.NextDouble() * 2.2f - MathF.Abs(T) * 2;
            List.Add((Cx + T * 16 + (float)(R.NextDouble() - 0.5) * 2, By - Rad - (0.5f - MathF.Abs(T)) * 5, Rad));
        }
        List.Sort((A, B) => A.Y.CompareTo(B.Y));
        for (int I = 0; I < List.Count; I++) Ball(C, R, List[I].X, List[I].Y, List[I].R, SHRUB, I + 1);
        C.Anchor = new Vector2I((int)Cx, (int)By - 2);
        C.Outline(SHRUB[0].Darkened(0.5f));
        return C;
    }

    static void Ball(Canvas C, Random R, float Cx, float Cy, float Rad, Color[] Pal, int Tag)
    {
        for (int Y = (int)(Cy - Rad - 1); Y <= (int)(Cy + Rad + 1); Y++)
            for (int X = (int)(Cx - Rad - 1); X <= (int)(Cx + Rad + 1); X++)
            {
                float Dx = (X + 0.5f - Cx) / Rad, Dy = (Y + 0.5f - Cy) / Rad;
                float D2 = Dx * Dx + Dy * Dy;
                if (D2 > 1) continue;
                if (D2 > 0.75f && C.Body(X, Y) && C.TagAt(X, Y) != Tag)
                {
                    C.Put(X, Y, Pal[0], 2, Tag);
                    continue;
                }
                var N = new Vector3(Dx, Dy, MathF.Sqrt(MathF.Max(0, 1 - D2)));
                float L = Math.Clamp((N.Dot(LIGHT) + 0.3f) / 1.3f + Noise(R) * 0.22f, 0, 1);
                C.Put(X, Y, Pal[Quant(L, Pal.Length - 1, X, Y)], 2, Tag);
            }
    }

    // The herb node: a bright medicinal plant with broad leaves and small white flowers (matches the herb icon).
    Canvas HerbNode(int Seed)
    {
        var R = new Random(Seed);
        var C = new Canvas(30, 24);
        float Cx = 14.5f, Gy = 20.5f;
        C.Shadow(Cx + 3, Gy, 10, 2.6f);
        int N = 8;
        var Leaves = new List<float>();
        for (int I = 0; I < N; I++)
            Leaves.Add(-MathF.PI * 0.97f + I * (MathF.PI * 0.94f / (N - 1)) + (float)(R.NextDouble() - 0.5) * 0.2f);
        // the upright leaves are behind, the flat ones in front
        Leaves.Sort((A, B) => MathF.Abs(MathF.Sin(B)).CompareTo(MathF.Abs(MathF.Sin(A))));
        var Base = new Vector2(Cx, Gy - 2);
        var Tips = new List<Vector2>();
        foreach (float A in Leaves)
        {
            float Len = 7 + MathF.Abs(MathF.Sin(A)) * 5 + (float)R.NextDouble() * 2;
            var Dir = new Vector2(MathF.Cos(A), MathF.Sin(A) * 0.85f);
            Leaf(C, Base, Dir, Len, 2.4f, HERB);
            Tips.Add(Base + Dir * Len);
        }
        C.Outline(new Color("123A22"));
        // three small flower heads over the top leaves
        Tips.Sort((A, B) => A.Y.CompareTo(B.Y));
        for (int I = 0; I < 3 && I < Tips.Count; I++)
        {
            int X = (int)Tips[I].X, Y = (int)Tips[I].Y + 1;
            C.Put(X, Y - 1, PETAL);
            C.Put(X - 1, Y, PETAL);
            C.Put(X + 1, Y, PETAL_SHADE);
            C.Put(X, Y + 1, PETAL_SHADE);
            C.Put(X, Y, HERB[^1]);
        }
        C.Anchor = new Vector2I((int)Cx, (int)Gy - 1);
        return C;
    }

    static void Leaf(Canvas C, Vector2 Base, Vector2 Dir, float Len, float Width, Color[] Pal)
    {
        Dir = Dir.Normalized();
        var Perp = new Vector2(-Dir.Y, Dir.X);
        for (int Y = (int)(Base.Y - Len - 2); Y <= (int)(Base.Y + Len + 2); Y++)
            for (int X = (int)(Base.X - Len - 2); X <= (int)(Base.X + Len + 2); X++)
            {
                var P = new Vector2(X + 0.5f, Y + 0.5f) - Base;
                float U = P.Dot(Dir), V = P.Dot(Perp);
                if (U < 0 || U > Len) continue;
                float Hw = Width * MathF.Sin(MathF.PI * MathF.Pow(U / Len, 0.8f));
                if (MathF.Abs(V) > Hw) continue;
                float Lit = (Perp * MathF.Sign(V)).Dot(LIGHT2);
                int B = MathF.Abs(V) < 0.6f && U > 1.5f ? 3 : Lit > 0.1f ? 2 : 1;
                if (U < 1.5f) B = 0;
                C.Put(X, Y, Pal[B]);
            }
    }

    Canvas HerbCut(int Seed)
    {
        var R = new Random(Seed);
        var C = new Canvas(18, 12);
        C.Shadow(10, 9, 6, 2);
        for (int I = 0; I < 4; I++)
        {
            int X = 5 + I * 2 + R.Next(2), H = 2 + R.Next(3);
            for (int Y = 0; Y < H; Y++) C.Put(X, 9 - Y, HERB[Y == H - 1 ? 1 : 0]);
            C.Put(X, 9 - H, HERB[2]);
        }
        C.Put(13, 9, HERB[1]);
        C.Put(14, 8, HERB[2]);
        C.Anchor = new Vector2I(9, 8);
        C.Outline(new Color("123A22"));
        return C;
    }

    // A wood stump with a pale cut face and growth rings.
    Canvas Stump(int Seed)
    {
        var C = new Canvas(18, 14);
        float Cx = 8.5f;
        C.Shadow(Cx + 3, 11, 7, 2.4f);
        for (int Y = 6; Y <= 11; Y++)
            for (int X = 4; X <= 12; X++)
            {
                bool Flare = Y >= 10;
                if (!Flare && (X == 4 || X == 12)) continue;
                C.Put(X, Y, BARK[X <= 5 ? 2 : X >= 11 ? 0 : 1]);
            }
        for (int Y = 3; Y <= 8; Y++)
            for (int X = 4; X <= 13; X++)
            {
                float Dx = (X + 0.5f - Cx) / 4.2f, Dy = (Y + 0.5f - 5.8f) / 2.4f;
                float D = Dx * Dx + Dy * Dy;
                if (D > 1) continue;
                C.Put(X, Y, D > 0.62f ? BARK[1] : D > 0.3f ? CUT[1] : D > 0.12f ? CUT[0] : CUT[2]);
            }
        C.Put(6, 5, CUT[2]);
        C.Anchor = new Vector2I(8, 10);
        C.Outline(BARK[0].Darkened(0.5f));
        return C;
    }

    // Grass tuft: a handful of blades, dark at the root and light at the tip; no outline so it sits in the ground.
    Canvas Tuft(int Seed, Color[] Pal, int Blades)
    {
        var R = new Random(Seed);
        var C = new Canvas(13, 11);
        int Gy = 9;
        for (int I = 0; I < Blades; I++)
        {
            float X0 = 6 + (I - (Blades - 1) / 2f) * 1.4f + (float)(R.NextDouble() - 0.5);
            int H = 3 + R.Next(5);
            float Lean = (X0 - 6) * 0.35f + (float)(R.NextDouble() - 0.5) * 1.5f;
            for (int S = 0; S <= H; S++)
            {
                int X = (int)MathF.Round(X0 + Lean * S / H), Y = Gy - S;
                int B = Math.Min(Pal.Length - 1, S * Pal.Length / (H + 1));
                C.Put(X, Y, Pal[B], 3);
            }
        }
        C.Anchor = new Vector2I(6, Gy);
        return C;
    }

    Canvas Pebble(int Seed, int W, int H)
    {
        var C = new Canvas(W + 3, H + 3);
        for (int Y = 0; Y < H; Y++)
            for (int X = 0; X < W; X++)
            {
                if ((X == 0 || X == W - 1) && (Y == 0 || Y == H - 1) && W > 2) continue;
                int B = Y == H - 1 ? 1 : (X == 0 || Y == 0) ? 4 : 3;
                C.Put(X + 1, Y + 1, ROCK[B], 3);
            }
        for (int X = 1; X < W; X++) C.Put(X + 1, H + 1, SHADOW, 1);
        C.Anchor = new Vector2I(W / 2 + 1, H);
        return C;
    }

    // --- metal: vehicle wrecks and scrap (GDD 4.3) ------------------------------
    // A rusted car seen from the 3/4 camera: the roof and bonnet on top, the near side panel below, one wheel gone.
    Canvas Wreck(int Seed)
    {
        var R = new Random(Seed);
        var C = new Canvas(52, 34);
        int Rust = R.Next();
        C.Shadow(28, 28, 22, 4);
        // wheels peeking out under the side panel (the rear one is missing: just the hub)
        Disc(C, 13, 27, 4.2f, 2.8f, TYRE);
        Disc(C, 38, 27, 4.2f, 2.8f, TYRE);
        C.Put(38, 27, RIM);
        C.Put(39, 27, RIM);
        // side panel
        for (int Y = 17; Y <= 26; Y++)
            for (int X = 4; X <= 47; X++)
            {
                if ((X == 4 || X == 47) && (Y == 17 || Y == 26)) continue;
                if (Y >= 24 && ((X >= 9 && X <= 17) || (X >= 34 && X <= 42))) continue; // wheel arches
                int B = Y <= 18 ? 3 : Y >= 25 ? 0 : 1;
                if (X <= 5) B = Math.Min(4, B + 1);
                C.Put(X, Y, Paint(B, X, Y, Rust, Y >= 21 ? 0.38f : 0.5f));
            }
        // door seams and a dent
        for (int Y = 18; Y <= 24; Y++) { C.Put(20, Y, PAINT[0]); C.Put(31, Y, PAINT[0]); }
        C.Put(26, 21, PAINT[0]); C.Put(27, 22, PAINT[0]); C.Put(25, 21, PAINT[3]);
        // bonnet (right) and boot (left): the flat top
        for (int Y = 10; Y <= 16; Y++)
            for (int X = 5; X <= 46; X++)
            {
                if ((X == 5 || X == 46) && Y == 10) continue;
                int B = Y <= 11 ? 4 : 3;
                C.Put(X, Y, Paint(B, X, Y, Rust, 0.48f));
            }
        // cabin: roof on top, windows along its near side, a smashed windscreen
        for (int Y = 3; Y <= 15; Y++)
            for (int X = 14; X <= 35; X++)
            {
                int Inset = Y <= 4 ? 2 : 0;
                if (X < 14 + Inset || X > 35 - Inset) continue;
                Color Col;
                if (Y >= 11) Col = (X == 14 || X == 25 || X == 35) ? PAINT[1] : GLASS;
                else if (X >= 33) Col = GLASS;
                else Col = Paint(Y <= 4 ? 4 : 3, X, Y, Rust, 0.5f);
                C.Put(X, Y, Col);
            }
        C.Put(16, 12, GLINT); C.Put(17, 12, GLINT); C.Put(27, 12, GLINT);
        C.Put(33, 6, GLINT); C.Put(34, 8, PAINT[4]); C.Put(33, 9, GLINT);
        // headlight and a rust streak down from the roof
        C.Put(46, 17, new Color("C8C2A6"));
        C.Put(45, 17, new Color("8F8A74"));
        C.Anchor = new Vector2I(26, 21);
        C.Outline(INK);
        return C;
    }

    Color Paint(int B, int X, int Y, int Seed, float RustAbove)
    {
        float N = Value(X, Y, 5, Seed);
        if (N > RustAbove + 0.25f) return RUST[Math.Clamp(B, 0, RUST.Length - 1)];
        if (N > RustAbove + 0.18f) return RUST[Math.Clamp(B - 1, 0, RUST.Length - 1)];
        return PAINT[B];
    }

    // A heap of scrap: a rusty drum, leaning steel sheets and a pipe.
    Canvas ScrapHeap(int Seed)
    {
        var R = new Random(Seed);
        var C = new Canvas(46, 34);
        int Rust = R.Next();
        C.Shadow(25, 28, 19, 4);
        // back sheet, leaning
        Sheet(C, new[] { new Vector2(18, 6), new Vector2(38, 10), new Vector2(40, 27), new Vector2(17, 27) }, 3, Rust);
        // pipe across the front of the sheet
        var Pipe = new List<(Vector2, Vector2, float, float)> { (new Vector2(14, 25), new Vector2(43, 18), 1.8f, 1.8f) };
        Raster(C, Pipe, PAINT);
        // front sheet
        Sheet(C, new[] { new Vector2(22, 17), new Vector2(36, 15), new Vector2(38, 28), new Vector2(24, 29) }, 2, Rust + 1);
        // the drum, standing on the left
        for (int Y = 9; Y <= 27; Y++)
            for (int X = 4; X <= 15; X++)
            {
                float Dx = (X + 0.5f - 10) / 6f;
                if (MathF.Abs(Dx) > 1) continue;
                if (Y <= 11)
                {
                    float Dy = (Y + 0.5f - 10.5f) / 2.2f;
                    if (Dx * Dx + Dy * Dy > 1.05f) continue;
                    C.Put(X, Y, Dx * Dx + Dy * Dy > 0.5f ? RUST[3] : RUST[1]);
                    continue;
                }
                int B = Dx < -0.5f ? 4 : Dx < 0.1f ? 3 : Dx < 0.6f ? 2 : 1;
                if (Y == 16 || Y == 22) B = Math.Max(0, B - 2);
                if (Y >= 26) B = Math.Max(0, B - 1);
                C.Put(X, Y, RUST[B]);
            }
        // a tyre lying in front
        Disc(C, 31, 30, 5, 2.4f, TYRE);
        C.Put(30, 30, RUST[0]); C.Put(31, 30, RUST[0]); C.Put(32, 30, RUST[0]);
        C.Anchor = new Vector2I(24, 22);
        C.Outline(INK);
        return C;
    }

    void Sheet(Canvas C, Vector2[] Pts, int B, int Rust)
    {
        for (int Y = 0; Y < C.H; Y++)
            for (int X = 0; X < C.W; X++)
            {
                if (!Inside(Pts, new Vector2(X + 0.5f, Y + 0.5f))) continue;
                int Bb = B;
                // the top edge of a sheet catches the light, a riveted seam runs across it
                if (!Inside(Pts, new Vector2(X + 0.5f, Y - 0.5f))) Bb = 4;
                else if (!Inside(Pts, new Vector2(X + 1.5f, Y + 0.5f))) Bb = Math.Max(0, B - 2);
                Color Col = Paint(Bb, X, Y, Rust, 0.45f);
                C.Put(X, Y, Col);
            }
    }

    Canvas ScrapLeft(int Seed)
    {
        var R = new Random(Seed);
        var C = new Canvas(30, 16);
        C.Shadow(16, 11, 11, 3);
        Sheet(C, new[] { new Vector2(5, 7), new Vector2(17, 6), new Vector2(18, 11), new Vector2(6, 12) }, 2, R.Next());
        Sheet(C, new[] { new Vector2(15, 9), new Vector2(25, 8), new Vector2(25, 12), new Vector2(16, 13) }, 3, R.Next());
        C.Put(9, 13, PAINT[3]); C.Put(22, 6, RUST[3]); C.Put(23, 6, RUST[1]);
        C.Anchor = new Vector2I(15, 10);
        C.Outline(INK);
        return C;
    }

    static void Disc(Canvas C, float Cx, float Cy, float Rx, float Ry, Color Col)
    {
        for (int Y = (int)(Cy - Ry - 1); Y <= (int)(Cy + Ry + 1); Y++)
            for (int X = (int)(Cx - Rx - 1); X <= (int)(Cx + Rx + 1); X++)
            {
                float Dx = (X + 0.5f - Cx) / Rx, Dy = (Y + 0.5f - Cy) / Ry;
                if (Dx * Dx + Dy * Dy <= 1) C.Put(X, Y, Col);
            }
    }

    static bool Inside(Vector2[] Poly, Vector2 P)
    {
        bool In = false;
        for (int I = 0, J = Poly.Length - 1; I < Poly.Length; J = I++)
            if ((Poly[I].Y > P.Y) != (Poly[J].Y > P.Y) &&
                P.X < (Poly[J].X - Poly[I].X) * (P.Y - Poly[I].Y) / (Poly[J].Y - Poly[I].Y) + Poly[I].X)
                In = !In;
        return In;
    }

    // --- ground -----------------------------------------------------------------
    // A 256 x 256 tile (8 x 8 game tiles) that repeats without seams. Near-white values: the game multiplies it
    // by the day-grass and outer-zone colours, so it only adds light patches, dark patches and grass marks.
    Image Ground(int Seed)
    {
        const int S = 256;
        var R = new Random(Seed);
        var Im = Image.CreateEmpty(S, S, false, Image.Format.Rgba8);
        float[] Levels = { 0.9f, 0.94f, 0.97f, 1.0f };
        for (int Y = 0; Y < S; Y++)
            for (int X = 0; X < S; X++)
            {
                // flat patches with hard edges, like the sprites (no dither: it reads as grain when scaled up)
                float V = Tiled(X, Y, S, 64, Seed) * 0.55f + Tiled(X, Y, S, 32, Seed + 7) * 0.3f + Tiled(X, Y, S, 16, Seed + 13) * 0.15f;
                int B = Quant((V - 0.5f) * 1.6f + 0.5f, Levels.Length - 1, X, Y, 0f);
                float L = Levels[B];
                Im.SetPixel(X, Y, new Color(L, L, L * 0.985f));
            }
        // grass marks: a dark root under a light blade, scattered over the tile
        for (int I = 0; I < 1040; I++)
        {
            int X = R.Next(S), Y = R.Next(S);
            bool Tall = R.Next(3) == 0;
            Mark(Im, X, Y, 0.82f);
            Mark(Im, X, Y - 1, 1.0f);
            if (Tall) Mark(Im, X + (R.Next(2) == 0 ? -1 : 1), Y - 2, 1.0f);
        }
        // a few dark specks of soil
        for (int I = 0; I < 280; I++) Mark(Im, R.Next(S), R.Next(S), 0.84f);
        return Im;
    }

    static void Mark(Image Im, int X, int Y, float L)
    {
        int S = Im.GetWidth();
        Im.SetPixel(((X % S) + S) % S, ((Y % S) + S) % S, new Color(L, L, L * 0.985f));
    }

    // --- helpers ----------------------------------------------------------------
    static Color[] Hex(params string[] Codes) => Array.ConvertAll(Codes, H => new Color(H));

    static float Noise(Random R) => (float)R.NextDouble() - 0.5f;

    // value 0..1 to a palette index 0..Top with a light 2 x 2 ordered dither on the band edges
    static int Quant(float V, int Top, int X, int Y, float Dither = 0.45f)
    {
        float D = (BAYER[(X & 1) + 2 * (Y & 1)] - 0.375f) * Dither;
        return Math.Clamp((int)MathF.Floor(V * (Top + 1) + D), 0, Top);
    }

    static float Hash(int X, int Y, int Seed)
    {
        unchecked
        {
            uint H = (uint)(X * 374761393 + Y * 668265263 + Seed * 1442695041);
            H = (H ^ (H >> 13)) * 1274126177u;
            return ((H ^ (H >> 16)) & 0xFFFF) / 65535f;
        }
    }

    // smooth value noise with a cell size in pixels
    static float Value(int X, int Y, int Cell, int Seed)
    {
        float Fx = (float)X / Cell, Fy = (float)Y / Cell;
        int X0 = (int)MathF.Floor(Fx), Y0 = (int)MathF.Floor(Fy);
        float Tx = Smooth(Fx - X0), Ty = Smooth(Fy - Y0);
        float A = Mathf.Lerp(Hash(X0, Y0, Seed), Hash(X0 + 1, Y0, Seed), Tx);
        float B = Mathf.Lerp(Hash(X0, Y0 + 1, Seed), Hash(X0 + 1, Y0 + 1, Seed), Tx);
        return Mathf.Lerp(A, B, Ty);
    }

    // value noise that wraps every Size pixels (Size must be a multiple of Cell)
    static float Tiled(int X, int Y, int Size, int Cell, int Seed)
    {
        int N = Size / Cell;
        float Fx = (float)X / Cell, Fy = (float)Y / Cell;
        int X0 = (int)MathF.Floor(Fx), Y0 = (int)MathF.Floor(Fy);
        float Tx = Smooth(Fx - X0), Ty = Smooth(Fy - Y0);
        float H(int I, int J) => Hash(((I % N) + N) % N, ((J % N) + N) % N, Seed);
        return Mathf.Lerp(Mathf.Lerp(H(X0, Y0), H(X0 + 1, Y0), Tx), Mathf.Lerp(H(X0, Y0 + 1), H(X0 + 1, Y0 + 1), Tx), Ty);
    }

    static float Smooth(float T) => T * T * (3 - 2 * T);

    // A sprite being drawn: colours plus what each pixel is (empty, shadow, body, flat detail, outline).
    sealed class Canvas
    {
        public readonly int W, H;
        public Vector2I Anchor;
        readonly Color[] Px;
        readonly byte[] Kind;
        readonly int[] Tags;

        public Canvas(int W, int H)
        {
            this.W = W;
            this.H = H;
            Px = new Color[W * H];
            Kind = new byte[W * H];
            Tags = new int[W * H];
        }

        public bool In(int X, int Y) => X >= 0 && Y >= 0 && X < W && Y < H;
        public bool Body(int X, int Y) => In(X, Y) && Kind[Y * W + X] == 2;
        public int TagAt(int X, int Y) => In(X, Y) ? Tags[Y * W + X] : -1;

        // kind: 1 shadow, 2 body (gets an outline), 3 flat detail (no outline)
        public void Put(int X, int Y, Color C, byte K = 2, int Tag = 0)
        {
            if (!In(X, Y)) return;
            Px[Y * W + X] = C;
            Kind[Y * W + X] = K;
            Tags[Y * W + X] = Tag;
        }

        // a soft ground shadow on empty pixels; drawn first, the body covers it
        public void Shadow(float Cx, float Cy, float Rx, float Ry)
        {
            for (int Y = 0; Y < H; Y++)
                for (int X = 0; X < W; X++)
                {
                    float Dx = (X + 0.5f - Cx) / Rx, Dy = (Y + 0.5f - Cy) / Ry;
                    if (Dx * Dx + Dy * Dy <= 1 && Kind[Y * W + X] == 0) Put(X, Y, SHADOW, 1);
                }
        }

        // the same, for shapes drawn before their shadow
        public void ShadowUnder(float Cx, float Cy, float Rx, float Ry) => Shadow(Cx, Cy, Rx, Ry);

        public void Outline(Color Ink)
        {
            var Marks = new List<int>();
            for (int Y = 0; Y < H; Y++)
                for (int X = 0; X < W; X++)
                {
                    byte K = Kind[Y * W + X];
                    if (K == 2 || K == 3 || K == 4) continue;
                    if (Body(X - 1, Y) || Body(X + 1, Y) || Body(X, Y - 1) || Body(X, Y + 1)) Marks.Add(Y * W + X);
                }
            foreach (int I in Marks)
            {
                Px[I] = Ink;
                Kind[I] = 4;
            }
        }

        public Image ToImage()
        {
            var Im = Image.CreateEmpty(W, H, false, Image.Format.Rgba8);
            for (int I = 0; I < W * H; I++)
                if (Kind[I] != 0) Im.SetPixel(I % W, I / W, Px[I]);
            return Im;
        }
    }
}
