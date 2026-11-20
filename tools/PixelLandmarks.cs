using Godot;
using System;
using System.Collections.Generic;
using FileAccess = Godot.FileAccess;

// Owner: Vu Duc Viet Anh. Pixel art for the 18 landmarks of the island, after the concept art of the Landmark Atlas
// (art/landmarks/ref/, from .tmp/Hold_til_Dawn_Landmark_Atlas.pdf): coconut palms and fallen trunks (02),
// limestone shelves (03), broad-leaf herbs (04), the military utility vehicle wreck (05), the ruined depot's broken
// walls, shelves, crates and radio mast (06), the convoy trucks (07), mangroves on stilt roots (08), the weather
// station (09), the old pier and fish traps (10), the coral lookout shelter (12), shipping containers, drums and
// pallets (13), the beached cargo ship and machinery washed up (14), red ginger by the pond (15), the limestone
// bluff with the cave mouth (16), the concrete lookout platform (17) and the herb garden's raised beds, broken
// fence and watering can (18). Same rules as the rest of tools/PixelNature.cs (this is part of that tool):
// 1 px = 1 game px, light from the upper left, a dark outline, a soft shadow to the lower right.
// Writes art/landmarks/<name>_<variant>.png and art/landmarks/index.json ({name: [anchor_x, anchor_y, solid_radius_px]}).
// Placed on the island by src/world/WorldLandmarks.cs.
public partial class PixelNature : Node
{
    static readonly Color[] LIME = Hex("5E5B4F", "817D6D", "A5A08C", "C6C1AA", "E2DDC8");
    static readonly Color[] PALM_BARK = Hex("4A3A28", "6A5338", "8C7150", "AD9268");
    static readonly Color[] PALM_LEAF = Hex("1E4526", "2C6230", "3F823A", "62A348", "8FC45A");
    static readonly Color[] COCO = Hex("3E2E16", "5E4822", "7E6630");
    static readonly Color[] OLIVE = Hex("272C1C", "3A4128", "4F5835", "687246", "848E5A");
    static readonly Color[] RUSTY = Hex("41200F", "673318", "8E4C22", "B06A30");
    static readonly Color[] TYRES = Hex("141519", "23252B", "363940");
    static readonly Color[] GLASSY = Hex("142027", "21363F", "355561", "6E98A2");
    static readonly Color[] ALLOY = Hex("5B6B7C", "8FA3B8", "B4C4D4", "DCE6F0");
    static readonly Color[] CANVAS_C = Hex("353B28", "4A5236", "626C46", "7C8758", "98A26C");
    static readonly Color[] CONC = Hex("4B4D4F", "67696B", "838684", "A0A29D", "BDBEB7");
    static readonly Color[] TIN = Hex("4E2E20", "77462A", "9A6038", "BB7E4E", "D29C6A");
    static readonly Color[] TIMBER = Hex("3B3026", "574735", "766148", "957C5C", "B09775");
    static readonly Color[] PLASTER = Hex("6F6A5E", "928C7C", "B2AC98", "CDC7B2", "E4DFCC");
    static readonly Color[] STEELS = Hex("3A424C", "56606C", "77828E", "9AA5AF", "C2CBD2");
    static readonly Color[] BOX_BLUE = Hex("172A40", "223D5C", "305478", "446F96", "5E8AB2");
    static readonly Color[] BOX_ORANGE = Hex("4E2412", "79381A", "A34E25", "C66A35", "DE8A50");
    static readonly Color[] BOX_RED = Hex("3E1814", "5E241C", "823427", "A04633", "B85E47");
    static readonly Color[] HULL = Hex("15191D", "23292F", "353E46", "4C5760", "66737D");
    static readonly Color[] HULL_RED = Hex("3A1512", "5A2119", "7C3022", "99412D");
    static readonly Color[] DECK = Hex("3A2C22", "54402E", "6E553C", "8A6D4C");
    static readonly Color[] WHITEWASH = Hex("7C7A72", "A09E95", "C2C0B6", "DDDBD1", "F0EEE5");
    static readonly Color[] VINES = Hex("1C3F22", "2A5A2C", "3C7838", "58974A");
    static readonly Color[] MANG_BARK = Hex("33291F", "4B3D2D", "66533D", "826B4F", "9C8566");
    static readonly Color[] MANG_LEAF = Hex("1A3A24", "27512E", "386B39", "4F8746", "6CA257");
    static readonly Color[] BROADLEAF = Hex("1D4A28", "2D6A34", "438A40", "64A94E", "8CC664");
    static readonly Color[] PURPLE = Hex("3E2668", "5E3E98", "8462C4", "AE90E6");
    static readonly Color[] GINGER = Hex("6A1420", "9A2230", "C83A40", "E8665C");
    static readonly Color[] SOIL = Hex("2A2018", "3C2E22", "52402E");
    static readonly Color CAVE_DARK = new Color("0E0C12");

    readonly Godot.Collections.Dictionary LmIndex = new();
    string LmOut = "res://art/landmarks";

    void DrawLandmarks()
    {
        foreach (string Arg in OS.GetCmdlineUserArgs())
            if (Arg.StartsWith("--landmarks-out=", StringComparison.Ordinal)) LmOut = Arg[16..];
        DirAccess.MakeDirRecursiveAbsolute(LmOut);
        // 02 Coconut Grove
        for (int I = 0; I < 4; I++) Lm("lm_palm", I, Palm(2001 + I, 66 + (I % 3) * 8, (I % 2 == 0) ? 1 : -1), 6);
        Lm("lm_log", 0, FallenTrunk(2101), 0);
        Lm("lm_sticks", 0, Sticks(2111), 0);
        // 03 Limestone Flats
        for (int I = 0; I < 3; I++) Lm("lm_shelf", I, LimeShelf(2201 + I, I), 18 + 3 * I);
        // 04 Herb Meadow, 15, 18
        for (int I = 0; I < 2; I++) Lm("lm_broadleaf", I, BroadLeaf(2301 + I, I == 0), 0);
        Lm("lm_ginger", 0, Ginger(2311), 0);
        // 05 Bayside Wreck
        Lm("lm_jeep", 0, Jeep(2401), 0);
        // 06 Ruined Base
        Lm("lm_ruin_h", 0, RuinWallH(2501, 0), 0);
        Lm("lm_ruin_h", 1, RuinWallH(2502, 1), 0);
        Lm("lm_ruin_v", 0, RuinWallV(2511), 0);
        Lm("lm_rack", 0, Rack(2521), 0);
        Lm("lm_crate", 0, Crate(2531), 8);
        Lm("lm_mast", 0, Mast(2541), 6);
        // 07 Stranded Convoy
        Lm("lm_truck", 0, Truck(2601, false), 0);
        Lm("lm_truck", 1, Truck(2602, true), 0);
        // 08 Mangrove Swamp
        for (int I = 0; I < 3; I++) Lm("lm_mangrove", I, Mangrove(2701 + I, I), 7);
        // 09 Weather Station
        Lm("lm_station", 0, Station(2801), 0);
        // 10 Old Fishing Pier (flat, on the ground layer, walked on)
        Lm("lm_pier", 0, Pier(2901), 0);
        Lm("lm_trap", 0, Trap(2911, false), 0);
        Lm("lm_trap", 1, Trap(2912, true), 0);
        // 12 Coral Lookout
        Lm("lm_lookout", 0, Lookout(3001), 0);
        // 13 Container Yard
        Lm("lm_container", 0, Container(3101, BOX_BLUE, false), 0);
        Lm("lm_container", 1, Container(3102, BOX_ORANGE, true), 0);
        Lm("lm_container_end", 0, ContainerEnd(3111, BOX_RED), 0);
        Lm("lm_drum", 0, Drum(3121, BOX_BLUE), 6);
        Lm("lm_drum", 1, Drum(3122, RUSTY), 6);
        Lm("lm_pallet", 0, Pallet(3131), 0);
        // 14 Beached Shipwreck
        Lm("lm_ship", 0, Ship(3201), 0);
        Lm("lm_gear", 0, Gear(3211), 8);
        // 16 Southwest Cave
        Lm("lm_bluff", 0, Bluff(3301), 0);
        // 17 South Cape Lookout (flat, on the ground layer)
        Lm("lm_platform", 0, Platform(3401), 0);
        // 18 Abandoned Herb Garden
        Lm("lm_bed", 0, Bed(3501, 0), 0);
        Lm("lm_bed", 1, Bed(3502, 1), 0);
        Lm("lm_fence", 0, Fence(3511), 0);
        Lm("lm_can", 0, Can(3521), 0);

        DrawLandmarkDetails();

        using var Fa = FileAccess.Open(LmOut.PathJoin("index.json"), FileAccess.ModeFlags.Write);
        Fa.StoreString(Json.Stringify(LmIndex, "\t"));
    }

    void Lm(string Name, int Variant, Canvas C, int Radius)
    {
        string N = Name + "_" + Variant;
        C.ToImage().SavePng(LmOut.PathJoin(N + ".png"));
        LmIndex[N] = new Godot.Collections.Array { C.Anchor.X, C.Anchor.Y, Radius };
        GD.Print("drew ", N, " (", C.W, ", ", C.H, ")");
    }

    // --- building blocks ----------------------------------------------------------
    static Color Pick(Color[] Pal, int B) => Pal[Math.Clamp(B, 0, Pal.Length - 1)];

    // a rectangle in one band, with grain: a pixel one band lighter or darker now and then
    static void Rect(Canvas C, int X0, int Y0, int W, int H, Color[] Pal, int B, int Seed = 0, float Grain = 0, byte K = 2)
    {
        for (int Y = Y0; Y < Y0 + H; Y++)
            for (int X = X0; X < X0 + W; X++)
            {
                int Bb = B;
                if (Grain > 0)
                {
                    float G = Hash(X, Y, Seed);
                    if (G < Grain * 0.5f) Bb--;
                    else if (G > 1 - Grain * 0.5f) Bb++;
                }
                C.Put(X, Y, Pick(Pal, Bb), K);
            }
    }

    // A box seen from the 3/4 camera: a lit top face D rows deep over a front face H rows tall. The edge between
    // them catches the light; the right end and the foot are in shade.
    static void Box(Canvas C, int X, int Y, int W, int D, int H, Color[] Pal, int Seed, int TopB = 3, int FrontB = 2, float Grain = 0.14f)
    {
        Rect(C, X, Y, W, D, Pal, TopB, Seed, Grain);
        Rect(C, X, Y + D, W, H, Pal, FrontB, Seed + 7, Grain);
        for (int Xx = X; Xx < X + W; Xx++)
        {
            C.Put(Xx, Y, Pick(Pal, TopB + 1));
            if (H > 0) C.Put(Xx, Y + D, Pick(Pal, FrontB + 1));
            if (H > 1) C.Put(Xx, Y + D + H - 1, Pick(Pal, FrontB - 1));
        }
        for (int Yy = Y; Yy < Y + D + H; Yy++) C.Put(X + W - 1, Yy, Pick(Pal, (Yy < Y + D ? TopB : FrontB) - 1));
        for (int Yy = Y; Yy < Y + D; Yy++) C.Put(X, Yy, Pick(Pal, TopB + 1));
    }

    // vertical ribs (corrugated steel, planks): a dark line and a lit line every Every px
    static void Ribs(Canvas C, int X0, int Y0, int W, int H, Color[] Pal, int B, int Every)
    {
        for (int Y = Y0; Y < Y0 + H; Y++)
            for (int X = X0; X < X0 + W; X++)
            {
                if (!C.Body(X, Y)) continue;
                int M = (X - X0) % Every;
                if (M == 0) C.Set(X, Y, Pick(Pal, B - 1));
                else if (M == 1) C.Set(X, Y, Pick(Pal, B + 1));
            }
    }

    // rust and grime: patches of another palette where a smooth noise is high, and drips running down
    static void Stain(Canvas C, int X0, int Y0, int W, int H, Color[] Pal, int Seed, float Amount, int Drips)
    {
        for (int Y = Y0; Y < Y0 + H; Y++)
            for (int X = X0; X < X0 + W; X++)
                if (C.Body(X, Y) && Value(X, Y, 6, Seed) + Hash(X, Y, Seed) * 0.06f > 1 - Amount * 0.55f)
                    C.Set(X, Y, Pick(Pal, 1 + (int)(Hash(X, Y, Seed + 1) * 2)));
        var R = new Random(Seed);
        for (int I = 0; I < Drips / 2; I++)
        {
            int X = X0 + R.Next(Math.Max(1, W));
            int Y = Y0 + R.Next(Math.Max(1, H / 2));
            int Len = 3 + R.Next(Math.Max(2, H / 2));
            for (int S = 0; S < Len; S++)
                if (C.Body(X, Y + S)) C.Set(X, Y + S, Pick(Pal, S < 2 ? 2 : 1));
        }
    }

    // vines hanging from the top edge of what is drawn in X0..X1, with leaves
    static void Vines(Canvas C, Random R, int X0, int X1, int Count, int MaxLen)
    {
        for (int I = 0; I < Count; I++)
        {
            int X = X0 + R.Next(Math.Max(1, X1 - X0));
            int Y = 0;
            while (Y < C.H && !C.Body(X, Y)) Y++;
            if (Y >= C.H) continue;
            int Len = 4 + R.Next(Math.Max(2, MaxLen));
            for (int S = 0; S < Len; S++)
            {
                if (!C.Solid(X, Y)) break;
                C.Put(X, Y, Pick(VINES, 1 + (S % 3 == 0 ? 1 : 0)), 3);
                if (R.Next(3) == 0)
                {
                    int Lx = X + (R.Next(2) == 0 ? -1 : 1);
                    if (C.Solid(Lx, Y)) C.Put(Lx, Y, Pick(VINES, 2 + R.Next(2)), 3);
                }
                Y++;
                if (R.Next(4) == 0) X += R.Next(3) - 1;
            }
        }
    }

    static void Wheel(Canvas C, float Cx, float Cy, float Rad)
    {
        Disc(C, Cx, Cy, Rad, Rad, TYRES[0]);
        Disc(C, Cx - 0.5f, Cy - 0.5f, Rad - 1.2f, Rad - 1.2f, TYRES[1]);
        Disc(C, Cx, Cy, Rad * 0.45f, Rad * 0.45f, OLIVE[2]);
        C.Put((int)(Cx - 1), (int)(Cy - 1), OLIVE[3]);
        for (int I = 0; I < 3; I++) C.Put((int)(Cx - Rad + 1 + I), (int)(Cy - Rad * 0.5f + I), TYRES[2]);
    }

    static void Line(Canvas C, Vector2 A, Vector2 B, Color Col, byte K = 2)
    {
        int N = (int)Math.Max(1, Math.Ceiling(A.DistanceTo(B)));
        for (int I = 0; I <= N; I++)
        {
            var P = A.Lerp(B, (float)I / N);
            C.Put((int)MathF.Floor(P.X), (int)MathF.Floor(P.Y), Col, K);
        }
    }

    static void Poly(Canvas C, Vector2[] Pts, Func<int, int, Color> Paint, byte K = 2)
    {
        float Y0 = float.MaxValue, Y1 = float.MinValue, X0 = float.MaxValue, X1 = float.MinValue;
        foreach (var P in Pts) { Y0 = MathF.Min(Y0, P.Y); Y1 = MathF.Max(Y1, P.Y); X0 = MathF.Min(X0, P.X); X1 = MathF.Max(X1, P.X); }
        for (int Y = (int)Y0; Y <= (int)Y1; Y++)
            for (int X = (int)X0; X <= (int)X1; X++)
                if (Inside(Pts, new Vector2(X + 0.5f, Y + 0.5f))) C.Put(X, Y, Paint(X, Y), K);
    }

    // the drawing turned by Deg round (Px, Py), nearest pixel (before outlining)
    static Canvas Turned(Canvas Src, float Deg, float Px, float Py)
    {
        var C = new Canvas(Src.W, Src.H) { Anchor = Src.Anchor };
        float A = Mathf.DegToRad(-Deg), Co = MathF.Cos(A), Si = MathF.Sin(A);
        for (int Y = 0; Y < C.H; Y++)
            for (int X = 0; X < C.W; X++)
            {
                float Dx = X + 0.5f - Px, Dy = Y + 0.5f - Py;
                int Sx = (int)MathF.Floor(Px + Dx * Co - Dy * Si), Sy = (int)MathF.Floor(Py + Dx * Si + Dy * Co);
                if (!Src.In(Sx, Sy) || !Src.Solid(Sx, Sy)) continue;
                C.Put(X, Y, Src.At(Sx, Sy), Src.Body(Sx, Sy) ? (byte)2 : (byte)3);
            }
        return C;
    }

    // --- 02 Coconut Grove ---------------------------------------------------------
    // A coconut palm: a ringed trunk curving up, a crown of drooping feathered fronds and a bunch of coconuts.
    Canvas Palm(int Seed, int H, int Lean)
    {
        var R = new Random(Seed);
        var C = new Canvas(76, H + 22);
        int Gx = 38, Gy = C.H - 4;
        C.Anchor = new Vector2I(Gx, Gy);
        C.Shadow(Gx + 10, Gy, 22, 4);
        var Top = new Vector2(Gx + Lean * (7 + R.Next(5)), Gy - H + 12);
        var Mid = new Vector2(Gx + Lean * 1, Gy - H * 0.5f);
        // the trunk: a quadratic curve, 4 px wide at the foot, 3 at the crown, rings every 3 px
        Vector2 Prev = new Vector2(Gx, Gy);
        for (int S = 0; S <= 80; S++)
        {
            float T = S / 80f;
            var P = new Vector2(Gx, Gy).Lerp(Mid, T).Lerp(Mid.Lerp(Top, T), T);
            float Hw = Mathf.Lerp(2.6f, 1.6f, T);
            for (int Dx = -3; Dx <= 3; Dx++)
            {
                if (MathF.Abs(Dx) > Hw) continue;
                int X = (int)MathF.Round(P.X + Dx), Y = (int)MathF.Round(P.Y);
                bool Ring = ((Gy - Y) % 3) == 0;
                int B = Dx < -0.5f ? 3 : Dx > 0.8f ? 1 : 2;
                if (Ring) B -= 1;
                C.Put(X, Y, Pick(PALM_BARK, B));
            }
            Prev = P;
        }
        // coconuts under the crown
        for (int I = 0; I < 4; I++)
            Ball(C, R, Top.X - 3 + I * 2 + (I % 2), Top.Y + 4 + (I % 2) * 2, 2.2f, COCO, 50 + I);
        // fronds: a midrib arcing out and down, leaflets either side shortening to the tip
        int N = 9;
        var Order = new List<float>();
        for (int I = 0; I < N; I++) Order.Add(-MathF.PI + I * MathF.Tau / N + (float)(R.NextDouble() - 0.5) * 0.3f);
        // the fronds at the back first
        Order.Sort((A, B) => MathF.Sin(A).CompareTo(MathF.Sin(B)));
        foreach (float A in Order)
        {
            var Dir = new Vector2(MathF.Cos(A), MathF.Sin(A) * 0.45f);
            float Len = 27 + (float)R.NextDouble() * 6;
            bool Back = MathF.Sin(A) < -0.2f;
            int Steps = (int)Len;
            for (int S = 1; S <= Steps; S++)
            {
                float T = (float)S / Steps;
                var P = Top + Dir * Len * T + new Vector2(0, (T * T) * 20 - T * 5);
                var Along = (Dir * Len + new Vector2(0, 2 * T * 20 - 5)).Normalized();
                var Perp = new Vector2(-Along.Y, Along.X);
                int Base = Back ? 1 : 2;
                C.Put((int)P.X, (int)P.Y, Pick(PALM_LEAF, Base - 1));
                float Lf = (1 - T * 0.8f) * 7f;
                for (int Side = -1; Side <= 1; Side += 2)
                {
                    var Dir2 = (Perp * Side + Along * 0.9f + new Vector2(0, 0.7f)).Normalized();
                    for (int K = 1; K <= (int)Lf; K++)
                    {
                        var Q = P + Dir2 * K;
                        bool Lit = (Perp * Side).Dot(LIGHT2) > 0;
                        C.Put((int)Q.X, (int)Q.Y, Pick(PALM_LEAF, Base + (Lit ? 1 : 0) + (K == 1 && !Back ? 1 : 0)));
                    }
                }
            }
        }
        // a few dry brown fronds hanging under the crown
        for (int I = 0; I < 2; I++)
        {
            float X = Top.X + (I == 0 ? -4 : 4);
            for (int S = 0; S < 10; S++) C.Put((int)(X + (I == 0 ? -S * 0.4f : S * 0.4f)), (int)(Top.Y + 3 + S), Pick(DRY, 1 + (S % 2)));
        }
        C.Outline(new Color("10261A"));
        return C;
    }

    // a fallen palm trunk with its sawn pale end and a dead frond, lying across the sand
    Canvas FallenTrunk(int Seed)
    {
        var R = new Random(Seed);
        var C = new Canvas(84, 28);
        C.Anchor = new Vector2I(42, 22);
        C.Shadow(46, 22, 38, 4);
        var A = new Vector2(10, 18);
        var B = new Vector2(72, 12);
        for (int Y = 0; Y < C.H; Y++)
            for (int X = 0; X < C.W; X++)
            {
                var P = new Vector2(X + 0.5f, Y + 0.5f);
                var Ab = B - A;
                float T = Math.Clamp((P - A).Dot(Ab) / Ab.LengthSquared(), 0, 1);
                var Q = A + Ab * T;
                float Rr = Mathf.Lerp(5.2f, 4.0f, T);
                float D = (P - Q).Y / Rr;
                if (P.DistanceTo(Q) > Rr) continue;
                int Bb = D < -0.45f ? 3 : D < 0.3f ? 2 : 1;
                if (((int)(T * 62)) % 4 == 0) Bb--;
                C.Put(X, Y, Pick(PALM_BARK, Bb));
            }
        // the sawn end: a pale disc with rings
        Disc(C, 10, 18, 3, 5, CUT[1]);
        Disc(C, 9.6f, 17.6f, 2, 3.4f, CUT[2]);
        C.Put(9, 18, CUT[0]);
        // a dead frond off the far end
        for (int S = 0; S < 16; S++)
        {
            int X = 72 + S / 2, Y = 11 - S / 3;
            C.Put(X, Y, Pick(DRY, 1));
            if (S % 2 == 0) { C.Put(X + 1, Y + 2, Pick(DRY, 2)); C.Put(X - 1, Y - 2, Pick(DRY, 0)); }
        }
        C.Outline(PALM_BARK[0].Darkened(0.6f));
        return C;
    }

    Canvas Sticks(int Seed)
    {
        var R = new Random(Seed);
        var C = new Canvas(36, 18);
        C.Anchor = new Vector2I(18, 12);
        for (int I = 0; I < 4; I++)
        {
            var A = new Vector2(4 + R.Next(8), 6 + R.Next(8));
            var B = new Vector2(24 + R.Next(9), 4 + R.Next(10));
            Line(C, A, B, Pick(WOOD_CHIPS, 1));
            Line(C, A + new Vector2(0, -1), B + new Vector2(0, -1), Pick(WOOD_CHIPS, 2));
            Line(C, A + new Vector2(0, 1), B + new Vector2(0, 1), Pick(WOOD_CHIPS, 0));
        }
        C.Outline(new Color("2A1C12"));
        return C;
    }

    // --- 03 Limestone Flats -------------------------------------------------------
    // Low limestone shelves breaking through the grass: two or three flat, pitted layers stepping up, each with a
    // pale top and a banded front face, the edges broken.
    Canvas LimeShelf(int Seed, int Shape)
    {
        var R = new Random(Seed);
        int W = 92 + Shape * 16;
        var C = new Canvas(W + 12, 50);
        C.Anchor = new Vector2I(C.W / 2, 44);
        C.Shadow(C.W / 2 + 6, 44, W * 0.5f, 6);
        int Layers = 2 + (Shape == 2 ? 1 : 0);
        for (int L = 0; L < Layers; L++)
        {
            int Lw = (int)(W * (1 - L * 0.28f)) - R.Next(6);
            int X0 = 4 + (W - Lw) / 2 + R.Next(9) - 4 - L * 3;
            int Base = 44 - L * 9;
            int Depth = 13 - L * 2, H = 8;
            for (int X = X0; X < X0 + Lw; X++)
            {
                float U = (float)(X - X0) / Lw;
                // broken, rounded ends and a ragged top edge
                float End = MathF.Min(U, 1 - U) * Lw;
                int Cut = End < 4 ? (int)(4 - End) : 0;
                int Rag = (int)(Value(X, L, 3, Seed) * 3);
                int Ytop = Base - H - Depth + Cut + Rag;
                int Yfront = Base - H + (Cut > 2 ? Cut - 2 : 0);
                for (int Y = Ytop; Y < Base; Y++)
                {
                    if (Y < Yfront)
                    {
                        // the top: pale with pits and a lit back rim
                        int B = Y == Ytop ? 4 : 3;
                        if (Hash(X, Y, Seed) > 0.9f) B = 2;
                        C.Put(X, Y, Pick(LIME, B), 2, L + 1);
                    }
                    else
                    {
                        // the front: bands of the layers in the rock, darker to the foot
                        int B = (Y - Yfront) % 3 == 2 ? 1 : 2;
                        if (Y >= Base - 1) B = 1;
                        if (X >= X0 + Lw - 2) B--;
                        C.Put(X, Y, Pick(LIME, B), 2, L + 1);
                    }
                }
            }
        }
        // a crack or two, and lichen
        for (int I = 0; I < 3; I++)
        {
            int X = 10 + R.Next(W - 10), Y = 20;
            while (Y < 44 && !C.Body(X, Y)) Y++;
            for (int S = 0; S < 6 && C.Body(X, Y); S++) { C.Set(X, Y, LIME[1]); Y++; X += R.Next(3) - 1; }
        }
        for (int I = 0; I < 14; I++)
        {
            int X = 6 + R.Next(W), Y = 20 + R.Next(22);
            if (C.Body(X, Y)) C.Set(X, Y, Pick(MOSS, 1 + R.Next(2)));
        }
        C.Outline(LIME[0].Darkened(0.55f));
        return C;
    }

    // --- 04 Herb Meadow -------------------------------------------------------------
    // a broad-leaf plant, some with spikes of purple flowers
    Canvas BroadLeaf(int Seed, bool Flowers)
    {
        var R = new Random(Seed);
        var C = new Canvas(38, 40);
        float Cx = 19, Gy = 36;
        C.Anchor = new Vector2I(19, 35);
        C.Shadow(Cx + 3, Gy, 13, 3);
        if (Flowers)
            for (int I = 0; I < 3; I++)
            {
                int X = 12 + I * 6 + R.Next(2), H = 13 + R.Next(8);
                for (int S = 0; S < H; S++)
                {
                    int Y = (int)Gy - 6 - S;
                    C.Put(X, Y, BROADLEAF[1]);
                    if (S > H / 3)
                    {
                        C.Put(X - 1, Y, Pick(PURPLE, (S % 2 == 0) ? 3 : 2));
                        C.Put(X + 1, Y, Pick(PURPLE, 1));
                        C.Put(X, Y, Pick(PURPLE, S % 3 == 0 ? 3 : 2));
                    }
                }
            }
        var Leaves = new List<float>();
        for (int I = 0; I < 9; I++) Leaves.Add(-MathF.PI * 0.98f + I * MathF.PI * 0.96f / 8 + (float)(R.NextDouble() - 0.5) * 0.2f);
        Leaves.Sort((A, B) => MathF.Abs(MathF.Sin(B)).CompareTo(MathF.Abs(MathF.Sin(A))));
        foreach (float A in Leaves)
        {
            var Dir = new Vector2(MathF.Cos(A), MathF.Sin(A) * 0.8f);
            Leaf(C, new Vector2(Cx, Gy - 3), Dir, 10 + MathF.Abs(MathF.Sin(A)) * 5 + (float)R.NextDouble() * 2, 3.6f, BROADLEAF);
        }
        C.Outline(new Color("0F2A16"));
        return C;
    }

    // red ginger by the pond and the cave: tall leaves and red flower cones
    Canvas Ginger(int Seed)
    {
        var R = new Random(Seed);
        var C = new Canvas(30, 40);
        C.Anchor = new Vector2I(15, 36);
        C.Shadow(18, 36, 10, 2.5f);
        for (int I = 0; I < 6; I++)
        {
            float A = -MathF.PI / 2 + (I - 2.5f) * 0.32f;
            Leaf(C, new Vector2(15, 34), new Vector2(MathF.Cos(A), MathF.Sin(A)), 16 + R.Next(6), 2.6f, BROADLEAF);
        }
        for (int I = 0; I < 2; I++)
        {
            int X = 11 + I * 8, Y = 10 + R.Next(5);
            for (int S = 0; S < 7; S++)
                for (int Dx = -1; Dx <= 1; Dx++)
                    if (MathF.Abs(Dx) <= 1 - S / 7f + 0.5f) C.Put(X + Dx, Y + S, Pick(GINGER, Dx < 0 ? 3 : S % 2 == 0 ? 2 : 1));
        }
        C.Outline(new Color("0F2A16"));
        return C;
    }

    // --- 05 Bayside Wreck -----------------------------------------------------------
    // A military utility vehicle slid off the road, nose down: olive paint gone to rust, the windows broken, the
    // door panel stripped to bare alloy (where the titanium comes from), the spare wheel on the back.
    Canvas Jeep(int Seed)
    {
        var R = new Random(Seed);
        var Raw = new Canvas(96, 66);
        // body: the low hull, the bonnet, the cab
        Box(Raw, 10, 28, 74, 6, 16, OLIVE, Seed, 3, 2);
        Box(Raw, 34, 12, 40, 5, 16, OLIVE, Seed + 1, 3, 2);
        // windscreen and side windows, broken
        Rect(Raw, 37, 19, 15, 8, GLASSY, 1, Seed + 2, 0.2f);
        Rect(Raw, 55, 19, 15, 8, GLASSY, 1, Seed + 3, 0.2f);
        for (int I = 0; I < 6; I++) { Raw.Put(39 + I, 25 - I, GLASSY[3]); Raw.Put(58 + I, 24 - I, GLASSY[2]); }
        Raw.Put(48, 20, CAVE_DARK); Raw.Put(49, 21, CAVE_DARK); Raw.Put(47, 22, CAVE_DARK);
        // the stripped door: bare alloy with rivets
        Rect(Raw, 54, 35, 14, 9, ALLOY, 2, Seed + 4, 0.25f);
        for (int X = 55; X < 68; X += 3) { Raw.Put(X, 36, ALLOY[0]); Raw.Put(X, 42, ALLOY[0]); }
        for (int X = 54; X < 68; X++) Raw.Put(X, 35, ALLOY[3]);
        // grille and headlight on the nose
        for (int Y = 35; Y < 43; Y++) for (int X = 11; X < 17; X += 2) Raw.Put(X, Y, OLIVE[0]);
        Disc(Raw, 20, 37, 2, 2, new Color("D8D2A6"));
        Raw.Put(19, 36, new Color("F4EFCB"));
        // the spare wheel on the tail
        Wheel(Raw, 86, 36, 6);
        Stain(Raw, 10, 12, 76, 34, RUSTY, Seed + 5, 0.32f, 10);
        Wheel(Raw, 24, 47, 8);
        Wheel(Raw, 70, 47, 8);
        // a broken tow hitch, a dent
        Raw.Put(8, 42, STEELS[1]); Raw.Put(7, 42, STEELS[2]);
        var C = Turned(Raw, -9, 48, 50);
        C.Anchor = new Vector2I(48, 57);
        C.Shadow(54, 56, 40, 6);
        Vines(C, R, 30, 80, 5, 6);
        C.Outline(new Color("12140C"));
        return C;
    }

    // --- 06 Ruined Base -------------------------------------------------------------
    // A broken concrete wall running across: jagged top, a window, rebar sticking out, vines and stains.
    Canvas RuinWallH(int Seed, int Shape)
    {
        var R = new Random(Seed);
        int W = Shape == 0 ? 128 : 96;
        var C = new Canvas(W + 12, 86);
        int Gy = 78, Thick = 10;
        C.Anchor = new Vector2I(W / 2 + 4, Gy);
        C.Shadow(W / 2 + 12, Gy + 1, W * 0.5f, 5);
        var Tops = new int[W];
        for (int X = 0; X < W; X++)
        {
            float U = (float)X / W;
            // tall in the middle stretches, broken down toward the ends and at a gap
            float Hgt = 56 - 30 * Value(X, 0, 14, Seed) - (Shape == 1 && U > 0.55f && U < 0.72f ? 40 : 0);
            if (U < 0.06f || U > 0.94f) Hgt -= 18;
            Tops[X] = Gy - Math.Max(6, (int)Hgt) - (int)(Hash(X, 1, Seed) * 2);
        }
        for (int X = 0; X < W; X++)
        {
            int Xx = 4 + X;
            int Top = Tops[X];
            // the top of the wall (its thickness seen from above), then the face
            for (int Y = Top; Y < Top + Thick && Y < Gy; Y++) C.Put(Xx, Y, Pick(CONC, Y == Top ? 4 : 3 - (int)(Hash(Xx, Y, Seed) * 1.3f)));
            for (int Y = Top + Thick; Y <= Gy; Y++)
            {
                int B = 2;
                if (Y >= Gy - 1) B = 1;
                if (((Y - Gy) % 12) == 0) B = 1;
                if (Hash(Xx, Y, Seed + 3) > 0.92f) B--;
                if (Hash(Xx, Y, Seed + 4) > 0.95f) B++;
                C.Put(Xx, Y, Pick(CONC, B));
            }
        }
        // a window with the dark room behind
        int Wx = 4 + W / 3, Wy = Gy - 40;
        if (Tops[W / 3 + 6] < Wy - 4)
        {
            Rect(C, Wx, Wy, 16, 14, new[] { CAVE_DARK, new Color("1D1A20"), new Color("2B2730") }, 1, Seed + 6, 0.25f);
            for (int X = Wx - 1; X <= Wx + 16; X++) C.Put(X, Wy + 14, CONC[4]);
        }
        // a door gap at the right on the long wall
        if (Shape == 0)
            for (int X = 4 + W - 34; X < 4 + W - 18; X++)
                for (int Y = Math.Max(Tops[X - 4] + Thick, Gy - 32); Y <= Gy; Y++) C.Put(X, Y, Y > Gy - 30 ? new Color("1D1A20") : CONC[2]);
        // a patch of old green paint, stains, rebar
        Rect(C, 4 + (int)(W * 0.68f), Gy - 34, 14, 18, Hex("3E5536", "4F6A43", "62804F"), 1, Seed + 7, 0.3f);
        Stain(C, 4, Gy - 60, W, 60, Hex("2E2C28", "3E3B35", "4E4A42"), Seed + 8, 0.2f, 14);
        for (int I = 0; I < 6; I++)
        {
            int X = 6 + R.Next(W - 4);
            int Top = Tops[X - 4];
            for (int S = 1; S < 6 + R.Next(6); S++) C.Put(X + (S > 5 ? 1 : 0), Top - S, Pick(RUSTY, 1 + (S % 2)));
        }
        Vines(C, R, 4, 4 + W, 9, 26);
        // rubble at the foot
        for (int I = 0; I < 7; I++)
        {
            int X = 4 + R.Next(W), Y = Gy - 1 + R.Next(3);
            C.Put(X, Y, CONC[3]); C.Put(X + 1, Y, CONC[2]); C.Put(X, Y - 1, CONC[4]);
        }
        C.Outline(new Color("1A1B1E"));
        return C;
    }

    // the same wall running down the screen: its long top, broken, and the end face
    Canvas RuinWallV(int Seed)
    {
        var R = new Random(Seed);
        var C = new Canvas(26, 128);
        int Gy = 122, Thick = 10;
        C.Anchor = new Vector2I(12, Gy);
        C.Shadow(18, Gy - 40, 7, 44);
        for (int Y = 8; Y < Gy - 30; Y++)
        {
            // the wall's height drops away in broken steps toward the far end
            int Lift = (int)(26 + 22 * Value(0, Y, 16, Seed));
            for (int X = 7; X < 7 + Thick; X++) C.Put(X, Y - Lift + 30, Pick(CONC, X == 7 ? 4 : 3 - (int)(Hash(X, Y, Seed) * 1.4f)));
            C.Put(7 + Thick, Y - Lift + 30, CONC[1]);
        }
        Box(C, 7, Gy - 44, Thick, 4, 40, CONC, Seed + 1, 3, 2);
        Stain(C, 6, 0, 14, Gy, Hex("2E2C28", "3E3B35", "4E4A42"), Seed + 2, 0.2f, 6);
        Vines(C, R, 7, 17, 4, 18);
        C.Outline(new Color("1A1B1E"));
        return C;
    }

    // steel shelving with boxes on it
    Canvas Rack(int Seed)
    {
        var R = new Random(Seed);
        var C = new Canvas(52, 60);
        C.Anchor = new Vector2I(26, 56);
        C.Shadow(30, 56, 22, 3);
        for (int Y = 4; Y <= 56; Y++) { C.Put(4, Y, STEELS[3]); C.Put(5, Y, STEELS[1]); C.Put(46, Y, STEELS[2]); C.Put(47, Y, STEELS[0]); }
        foreach (int Sy in new[] { 18, 34, 50 })
        {
            Box(C, 4, Sy - 3, 44, 2, 2, STEELS, Seed + Sy, 3, 1);
            int X = 7;
            while (X < 42)
            {
                int Bw = 7 + R.Next(6), Bh = 7 + R.Next(5);
                if (X + Bw > 45) break;
                Color[] Pal = R.Next(3) == 0 ? OLIVE : R.Next(2) == 0 ? Hex("5A4630", "7E6444", "A08458", "BFA16E") : BOX_BLUE;
                Box(C, X, Sy - 3 - Bh - 2, Bw, 2, Bh, Pal, Seed + X, 3, 2);
                X += Bw + 1 + R.Next(2);
            }
        }
        Stain(C, 0, 0, 52, 60, RUSTY, Seed + 9, 0.12f, 4);
        C.Outline(new Color("15171C"));
        return C;
    }

    // a green military crate with a stencilled mark
    Canvas Crate(int Seed)
    {
        var C = new Canvas(30, 26);
        C.Anchor = new Vector2I(15, 22);
        C.Shadow(18, 22, 13, 3);
        Box(C, 3, 4, 24, 6, 13, OLIVE, Seed, 3, 2);
        for (int X = 3; X < 27; X++) C.Put(X, 15, OLIVE[1]);
        for (int Y = 10; Y < 23; Y++) { C.Put(5, Y, OLIVE[1]); C.Put(24, Y, OLIVE[1]); }
        foreach (var (X, Y) in new[] { (12, 12), (13, 12), (14, 12), (13, 13), (16, 12), (16, 13), (17, 13), (18, 12) }) C.Put(X, Y, new Color("C9C08A"));
        C.Put(5, 11, STEELS[3]); C.Put(24, 11, STEELS[3]);
        C.Outline(new Color("12140C"));
        return C;
    }

    // a lattice radio mast with a dish
    Canvas Mast(int Seed)
    {
        var C = new Canvas(40, 150);
        int Gy = 146;
        C.Anchor = new Vector2I(20, Gy);
        C.Shadow(28, Gy, 12, 3);
        var Lb = new Vector2(10, Gy); var Rb = new Vector2(30, Gy);
        var Lt = new Vector2(17, 10); var Rt = new Vector2(23, 10);
        Line(C, Lb, Lt, STEELS[3]); Line(C, Lb + new Vector2(1, 0), Lt + new Vector2(1, 0), STEELS[2]);
        Line(C, Rb, Rt, STEELS[1]); Line(C, Rb + new Vector2(-1, 0), Rt + new Vector2(-1, 0), STEELS[2]);
        for (int I = 0; I < 9; I++)
        {
            float T0 = I / 9f, T1 = (I + 1) / 9f;
            var A = Lb.Lerp(Lt, T0); var B = Rb.Lerp(Rt, T1);
            var A2 = Rb.Lerp(Rt, T0); var B2 = Lb.Lerp(Lt, T1);
            Line(C, A, B, STEELS[1]); Line(C, A2, B2, STEELS[1]);
            Line(C, Lb.Lerp(Lt, T1), Rb.Lerp(Rt, T1), STEELS[2]);
        }
        Line(C, new Vector2(20, 10), new Vector2(20, 0), STEELS[3]);
        C.Put(20, 0, new Color("D9534A"));
        // the dish, turned toward the light
        Disc(C, 31, 30, 6, 7, STEELS[3]);
        Disc(C, 31.5f, 30.5f, 4, 5, STEELS[2]);
        C.Put(29, 27, STEELS[4]);
        Line(C, new Vector2(24, 30), new Vector2(27, 30), STEELS[1]);
        // a rusty cable box at the foot
        Box(C, 14, Gy - 12, 12, 3, 8, RUSTY, Seed, 3, 2);
        Stain(C, 0, 0, 40, 150, RUSTY, Seed + 1, 0.1f, 0);
        C.Outline(new Color("15171C"));
        return C;
    }

    // --- 07 Stranded Convoy ---------------------------------------------------------
    // A military truck facing left: the cab with its bonnet, the cargo bed under a sagging canvas cover. The
    // second one has the canvas torn open and the tailgate down, crates showing (the titanium cargo).
    Canvas Truck(int Seed, bool Open)
    {
        var R = new Random(Seed);
        var C = new Canvas(142, 92);
        int Gy = 84;
        C.Anchor = new Vector2I(72, Gy);
        C.Shadow(80, Gy, 64, 6);
        // chassis and bumper
        Rect(C, 8, Gy - 22, 126, 6, OLIVE, 1, Seed, 0.1f);
        // bonnet and cab
        Box(C, 6, Gy - 44, 22, 6, 18, OLIVE, Seed + 1, 3, 2);
        Box(C, 26, Gy - 62, 26, 6, 38, OLIVE, Seed + 2, 3, 2);
        Rect(C, 29, Gy - 54, 20, 12, GLASSY, 1, Seed + 3, 0.2f);
        for (int I = 0; I < 7; I++) C.Put(31 + I, Gy - 44 - I, GLASSY[3]);
        for (int Y = Gy - 36; Y < Gy - 26; Y++) for (int X = 7; X < 14; X += 2) C.Put(X, Y, OLIVE[0]);
        Disc(C, 17, Gy - 33, 2, 2, new Color("D8D2A6"));
        // the bed
        Box(C, 54, Gy - 40, 80, 4, 18, OLIVE, Seed + 4, 3, 2);
        Ribs(C, 54, Gy - 36, 80, 18, OLIVE, 2, 8);
        if (!Open)
        {
            // the canvas cover: a rounded top, sagging between the hoops
            for (int X = 54; X < 134; X++)
            {
                float U = (X - 54) % 20 / 20f;
                int Sag = (int)(MathF.Sin(U * MathF.PI) * 3);
                int Top = Gy - 70 + Sag;
                for (int Y = Top; Y < Gy - 40; Y++)
                {
                    int B = Y < Top + 8 ? 3 : 2;
                    if (Y == Top) B = 4;
                    if ((X - 54) % 20 == 0) B = Y < Top + 8 ? 4 : 3;
                    if (Hash(X, Y, Seed) > 0.93f) B--;
                    C.Put(X, Y, Pick(CANVAS_C, B));
                }
            }
            // a tear
            for (int I = 0; I < 8; I++) { C.Put(100 + I, Gy - 58 + I / 2, CAVE_DARK); C.Put(100 + I, Gy - 57 + I / 2, CANVAS_C[0]); }
        }
        else
        {
            // hoops left bare, shreds of canvas, crates in the bed, the tailgate down
            for (int H = 0; H < 4; H++)
            {
                int X = 56 + H * 24;
                for (int Y = Gy - 70; Y < Gy - 40; Y++) C.Put(X, Y, STEELS[2]);
                for (int Xx = X; Xx < X + 24 && H < 3; Xx++) C.Put(Xx, Gy - 70, STEELS[3]);
            }
            for (int X = 56; X < 82; X++) for (int Y = Gy - 70; Y < Gy - 62 + (X % 5); Y++) C.Put(X, Y, Pick(CANVAS_C, 2 + (Y == Gy - 70 ? 1 : 0)));
            Box(C, 64, Gy - 56, 18, 5, 11, OLIVE, Seed + 5, 3, 2);
            Box(C, 86, Gy - 52, 14, 4, 8, ALLOY, Seed + 6, 2, 1);
            Box(C, 104, Gy - 58, 20, 5, 13, OLIVE, Seed + 7, 3, 2);
            Box(C, 132, Gy - 24, 6, 2, 18, OLIVE, Seed + 8, 3, 1);
        }
        Stain(C, 6, Gy - 70, 128, 50, RUSTY, Seed + 9, 0.22f, 12);
        Stain(C, 6, Gy - 30, 130, 14, Hex("2E2418", "4A3A26", "5E4A30"), Seed + 10, 0.4f, 0);
        Wheel(C, 22, Gy - 9, 10);
        Wheel(C, 86, Gy - 9, 10);
        Wheel(C, 112, Gy - 9, 10);
        Vines(C, R, 30, 130, 7, 10);
        C.Outline(new Color("12140C"));
        return C;
    }

    // --- 08 Mangrove Swamp ----------------------------------------------------------
    // a mangrove: a dark leafy crown on a short trunk standing on arched stilt roots
    Canvas Mangrove(int Seed, int Shape)
    {
        var R = new Random(Seed);
        var C = new Canvas(72, 80);
        float Cx = 36, Gy = 76;
        C.Anchor = new Vector2I(36, 75);
        C.Shadow(Cx + 6, Gy - 1, 26, 4);
        var Segs = new List<(Vector2, Vector2, float, float)>();
        var Fork = new Vector2(Cx, Gy - 20 - Shape * 3);
        // arched roots from the fork out to the mud
        int Roots = 7 + Shape;
        for (int I = 0; I < Roots; I++)
        {
            float U = (I + 0.5f) / Roots;
            float Fx = Mathf.Lerp(-27, 27, U) + (float)(R.NextDouble() - 0.5) * 4;
            var Foot = new Vector2(Cx + Fx, Gy - 1 - (float)R.NextDouble() * 3);
            var Knee = new Vector2(Cx + Fx * 0.6f, Fork.Y - 4 - (float)R.NextDouble() * 4);
            Segs.Add((Fork, Knee, 1.6f, 1.3f));
            Segs.Add((Knee, Foot, 1.3f, 1.1f));
        }
        Segs.Add((Fork, new Vector2(Cx + 1, Gy - 40), 2.8f, 2.2f));
        Segs.Add((new Vector2(Cx + 1, Gy - 40), new Vector2(Cx - 8, Gy - 50), 1.6f, 1.0f));
        Segs.Add((new Vector2(Cx + 1, Gy - 40), new Vector2(Cx + 10, Gy - 52), 1.6f, 1.0f));
        Raster(C, Segs, MANG_BARK);
        // the crown: overlapping leafy balls, the far ones first
        var Balls = new List<(float X, float Y, float Rr)>();
        for (int I = 0; I < 9; I++)
            Balls.Add((Cx + (float)(R.NextDouble() - 0.5) * 40, Gy - 54 - (float)R.NextDouble() * 14, 7 + (float)R.NextDouble() * 4));
        Balls.Sort((A, B) => A.Y.CompareTo(B.Y));
        int Tag = 10;
        foreach (var B in Balls) Ball(C, R, B.X, B.Y, B.Rr, MANG_LEAF, Tag++);
        C.Outline(new Color("101C12"));
        return C;
    }

    // --- 09 Weather Station ---------------------------------------------------------
    // A small plastered hut under a rusty tin roof, a steel door and a louvred vent, a wind vane and an aerial
    // on the roof, vines up the corner and a planter by the door.
    Canvas Station(int Seed)
    {
        var R = new Random(Seed);
        var C = new Canvas(96, 120);
        int Gy = 114;
        C.Anchor = new Vector2I(46, Gy);
        C.Shadow(54, Gy, 40, 6);
        // walls
        Box(C, 14, Gy - 50, 64, 4, 46, PLASTER, Seed, 3, 2, 0.2f);
        // door, vent, window
        Rect(C, 24, Gy - 38, 14, 34, STEELS, 2, Seed + 1, 0.15f);
        for (int Y = Gy - 38; Y < Gy - 4; Y++) C.Put(37, Y, STEELS[0]);
        C.Put(34, Gy - 22, STEELS[4]); C.Put(34, Gy - 21, STEELS[0]);
        Rect(C, 26, Gy - 34, 10, 6, STEELS, 1, Seed + 2);
        Rect(C, 50, Gy - 36, 18, 12, STEELS, 1, Seed + 3);
        for (int Y = Gy - 35; Y < Gy - 24; Y += 2) for (int X = 51; X < 67; X++) C.Put(X, Y, STEELS[3]);
        Stain(C, 14, Gy - 50, 64, 46, Hex("4E4A40", "6A6556", "857F6D"), Seed + 4, 0.25f, 12);
        // the tin roof: a sloping sheet, overhanging, ribbed and rusting
        for (int X = 8; X < 86; X++)
        {
            int Top = Gy - 66 + (X - 8) / 12;
            for (int Y = Top; Y < Gy - 48; Y++)
            {
                int B = Y > Gy - 51 ? 1 : 3;
                if ((X - 8) % 5 == 0) B--;
                if ((X - 8) % 5 == 1) B++;
                C.Put(X, Y, Pick(TIN, B));
            }
        }
        Stain(C, 8, Gy - 68, 78, 20, RUSTY, Seed + 5, 0.3f, 4);
        // the wind vane: a mast, the arrow and the cross of N-S-E-W
        int Mx = 30;
        for (int Y = Gy - 106; Y < Gy - 64; Y++) { C.Put(Mx, Y, STEELS[3]); C.Put(Mx + 1, Y, STEELS[1]); }
        Line(C, new Vector2(Mx - 10, Gy - 100), new Vector2(Mx + 12, Gy - 100), STEELS[2]);
        for (int I = 0; I < 4; I++) { C.Put(Mx - 10 + I, Gy - 101 - I / 2, STEELS[3]); C.Put(Mx - 10 + I, Gy - 99 + I / 2, STEELS[3]); }
        Rect(C, Mx + 10, Gy - 104, 4, 8, STEELS, 3, Seed + 6);
        Line(C, new Vector2(Mx - 6, Gy - 90), new Vector2(Mx + 7, Gy - 90), STEELS[1]);
        Line(C, new Vector2(Mx - 3, Gy - 93), new Vector2(Mx + 4, Gy - 87), STEELS[1]);
        // anemometer cups and the aerial
        int Ax = 68;
        for (int Y = Gy - 98; Y < Gy - 60; Y++) C.Put(Ax, Y, STEELS[2]);
        for (int I = 0; I < 4; I++) Line(C, new Vector2(Ax - 7 + I, Gy - 94 + I * 6), new Vector2(Ax + 7 - I, Gy - 94 + I * 6), STEELS[2]);
        // vines up the right corner, a planter
        for (int I = 0; I < 30; I++)
        {
            int X = 72 + R.Next(6), Y = Gy - 4 - R.Next(46);
            if (C.Body(X, Y)) C.Put(X, Y, Pick(VINES, 1 + R.Next(3)), 3);
        }
        Box(C, 40, Gy - 10, 12, 3, 7, TIMBER, Seed + 7, 3, 2);
        for (int I = 0; I < 6; I++) C.Put(41 + I * 2, Gy - 11 - (I % 2), Pick(BROADLEAF, 2 + (I % 2)));
        C.Outline(new Color("1A1714"));
        return C;
    }

    // --- 10 Old Fishing Pier ----------------------------------------------------------
    // The pier seen from above, running out to sea to the left: boards across it, some missing (the sea shows
    // through), posts down the sides, the far end gone to bare posts. Flat on the ground layer; the anchor is the
    // land end, the middle of the deck.
    Canvas Pier(int Seed)
    {
        var R = new Random(Seed);
        var C = new Canvas(300, 84);
        int Y0 = 16, Y1 = 64;
        C.Anchor = new Vector2I(296, (Y0 + Y1) / 2);
        // the shade on the water under the deck
        for (int X = 70; X < 300; X++) for (int Y = Y1; Y < Y1 + 6; Y++) C.Put(X, Y, SHADOW, 1);
        for (int X = 72; X < 298; X += 7)
        {
            int Plank = (X - 72) / 7;
            // gaps: a few missing near the middle, many near the broken end
            float Lose = X < 120 ? 0.55f : X < 170 ? 0.18f : 0.06f;
            if (Hash(Plank, 0, Seed) < Lose) continue;
            int Sag = Hash(Plank, 2, Seed) > 0.8f ? 1 : 0;
            for (int Xx = X; Xx < X + 6; Xx++)
                for (int Y = Y0 + Sag; Y < Y1 + Sag; Y++)
                {
                    int B = 2 + (Hash(Plank, 4, Seed) > 0.65f ? 1 : 0) - (Hash(Plank, 3, Seed) > 0.8f ? 1 : 0);
                    if (Xx == X + 5) B = 0;
                    else if (Y == Y0 + Sag) B++;
                    if (Value(Xx, Y, 3, Seed) > 0.72f) B--;
                    C.Put(Xx, Y, Pick(TIMBER, B + 1));
                }
            // the boards' front edge
            for (int Xx = X; Xx < X + 6; Xx++) { C.Put(Xx, Y1 + Sag, TIMBER[1]); C.Put(Xx, Y1 + Sag + 1, TIMBER[0]); }
        }
        // stringers under the deck show through the gaps, and posts along both sides out to the end
        for (int X = 72; X < 296; X++) { C.Put(X, Y0 + 8, TIMBER[1], 1); C.Put(X, Y1 - 8, TIMBER[1], 1); }
        for (int X = 10; X < 296; X += 30)
            foreach (int Y in new[] { Y0 - 2, Y1 + 2 })
            {
                if (X < 72 && Hash(X, Y, Seed) < 0.3f) continue;
                int H = X < 72 ? 10 + (int)(Hash(X, Y + 1, Seed) * 10) : 6;
                Box(C, X, Y - H + 4, 6, 3, H, TIMBER, Seed + X + Y, 3, 2);
            }
        C.Outline(new Color("1C150F"));
        return C;
    }

    // a fish trap (a slatted barrel basket), lying or standing
    Canvas Trap(int Seed, bool Standing)
    {
        var C = new Canvas(30, 24);
        C.Anchor = new Vector2I(15, 20);
        C.Shadow(18, 20, 12, 3);
        if (Standing)
        {
            Box(C, 6, 4, 16, 4, 14, TIMBER, Seed, 3, 2);
            for (int X = 7; X < 21; X += 3) for (int Y = 9; Y < 18; Y++) C.Put(X, Y, CAVE_DARK);
            for (int X = 6; X < 22; X++) { C.Put(X, 12, TIMBER[3]); C.Put(X, 15, TIMBER[3]); }
        }
        else
        {
            for (int X = 4; X < 26; X++)
                for (int Y = 8; Y < 20; Y++)
                {
                    float Dy = (Y - 14) / 6f;
                    int B = Dy < -0.4f ? 3 : Dy < 0.4f ? 2 : 1;
                    bool Gap = (X % 4 == 0) && Y > 9 && Y < 19;
                    C.Put(X, Y, Gap ? CAVE_DARK : Pick(TIMBER, B));
                }
            Disc(C, 5, 14, 2.5f, 6, TIMBER[2]);
            Disc(C, 5, 14, 1.2f, 3, CAVE_DARK);
            for (int Y = 8; Y < 20; Y++) { C.Put(12, Y, TIMBER[4]); C.Put(20, Y, TIMBER[4]); }
        }
        C.Outline(new Color("1C150F"));
        return C;
    }

    // --- 12 Coral Lookout -------------------------------------------------------------
    // A wooden shelter on stilts with a rusty tin roof, a railing and steps down to the ground.
    Canvas Lookout(int Seed)
    {
        var R = new Random(Seed);
        var C = new Canvas(104, 124);
        int Gy = 118;
        C.Anchor = new Vector2I(52, Gy);
        C.Shadow(62, Gy, 42, 6);
        // stilts
        foreach (int X in new[] { 18, 82 }) Box(C, X, Gy - 46, 5, 2, 44, TIMBER, Seed + X, 3, 2);
        // the floor deck
        Box(C, 12, Gy - 58, 80, 14, 6, TIMBER, Seed + 1, 3, 1);
        Ribs(C, 12, Gy - 58, 80, 14, TIMBER, 3, 6);
        // back posts and the roof posts
        foreach (int X in new[] { 14, 50, 86 }) for (int Y = Gy - 100; Y < Gy - 56; Y++) { C.Put(X, Y, TIMBER[3]); C.Put(X + 1, Y, TIMBER[1]); }
        // bench inside
        Box(C, 30, Gy - 68, 30, 3, 6, TIMBER, Seed + 2, 4, 2);
        // the railing along the front
        for (int X = 12; X < 92; X++) { C.Put(X, Gy - 64, TIMBER[4]); C.Put(X, Gy - 63, TIMBER[2]); C.Put(X, Gy - 56, TIMBER[2]); }
        for (int X = 14; X < 90; X += 6) for (int Y = Gy - 63; Y < Gy - 52; Y++) C.Put(X, Y, TIMBER[3]);
        // the roof: rusty tin sloping down to the front, ribbed, overhanging
        for (int X = 4; X < 100; X++)
            for (int Y = Gy - 112; Y < Gy - 96; Y++)
            {
                int B = Y > Gy - 99 ? 1 : 3;
                if ((X - 4) % 6 == 0) B--;
                if ((X - 4) % 6 == 1) B++;
                C.Put(X, Y, Pick(TIN, B));
            }
        Stain(C, 4, Gy - 112, 96, 16, RUSTY, Seed + 3, 0.35f, 6);
        // steps down on the left
        for (int I = 0; I < 6; I++) Box(C, 2 + I * 2, Gy - 46 + I * 7, 12, 3, 2, TIMBER, Seed + 10 + I, 3, 1);
        Line(C, new Vector2(2, Gy - 50), new Vector2(12, Gy - 2), TIMBER[1]);
        Vines(C, R, 14, 90, 4, 10);
        C.Outline(new Color("1C150F"));
        return C;
    }

    // --- 13 Container Yard -----------------------------------------------------------
    // a shipping container side on: ribbed steel, corner castings, rust; Stacked = two high
    Canvas Container(int Seed, Color[] Pal, bool Stacked)
    {
        var C = new Canvas(142, Stacked ? 118 : 70);
        int Gy = C.H - 6;
        C.Anchor = new Vector2I(70, Gy);
        C.Shadow(78, Gy, 66, 6);
        void One(int Y, Color[] P, int S)
        {
            Box(C, 4, Y, 132, 10, 38, P, S, 3, 2, 0.08f);
            Ribs(C, 4, Y + 11, 132, 36, P, 2, 4);
            foreach (int X in new[] { 4, 130 }) { Rect(C, X, Y, 6, 10, STEELS, 2); Rect(C, X, Y + 42, 6, 6, STEELS, 1); }
            Stain(C, 4, Y, 132, 48, RUSTY, S + 1, 0.22f, 12);
            // a faded stencil
            for (int X = 20; X < 44; X += 3) C.Put(X, Y + 18, P[4]);
        }
        if (Stacked)
        {
            One(Gy - 48, Pal, Seed);
            One(Gy - 96, BOX_BLUE, Seed + 3);
        }
        else One(Gy - 48, Pal, Seed);
        C.Outline(new Color("101216"));
        return C;
    }

    // a container end on, its doors open: one swung out, the dark inside with crates (titanium cargo)
    Canvas ContainerEnd(int Seed, Color[] Pal)
    {
        var C = new Canvas(92, 76);
        int Gy = 70;
        C.Anchor = new Vector2I(36, Gy);
        C.Shadow(44, Gy, 40, 6);
        Box(C, 6, Gy - 64, 58, 16, 48, Pal, Seed, 3, 2, 0.08f);
        // the open end: dark inside, a crate and a sheet of alloy
        Rect(C, 10, Gy - 44, 50, 38, new[] { CAVE_DARK, new Color("1A1A20"), new Color("26262E") }, 1, Seed + 1, 0.2f);
        Box(C, 14, Gy - 22, 18, 4, 14, OLIVE, Seed + 2, 3, 2);
        Box(C, 36, Gy - 28, 8, 2, 20, ALLOY, Seed + 3, 3, 2);
        Box(C, 46, Gy - 18, 12, 3, 10, TIMBER, Seed + 4, 3, 2);
        // the door swung open to the right, seen edge-on-ish
        for (int X = 64; X < 86; X++)
            for (int Y = Gy - 46 + (X - 64) / 4; Y < Gy - 4 + (X - 64) / 8; Y++)
            {
                int B = (X - 64) % 4 == 0 ? 1 : 2;
                C.Put(X, Y, Pick(Pal, B));
            }
        for (int Y = Gy - 46; Y < Gy - 4; Y += 10) C.Put(70, Y, STEELS[3]);
        Stain(C, 6, Gy - 64, 80, 64, RUSTY, Seed + 5, 0.25f, 10);
        C.Outline(new Color("101216"));
        return C;
    }

    // an oil drum
    Canvas Drum(int Seed, Color[] Pal)
    {
        var C = new Canvas(20, 28);
        C.Anchor = new Vector2I(10, 25);
        C.Shadow(12, 25, 8, 2.5f);
        for (int Y = 6; Y < 25; Y++)
            for (int X = 3; X < 17; X++)
            {
                float Dx = (X - 9.5f) / 7f;
                int B = Dx < -0.4f ? 3 : Dx < 0.35f ? 2 : 1;
                if (Y == 11 || Y == 19) B--;
                C.Put(X, Y, Pick(Pal, B));
            }
        Disc(C, 10, 6, 7, 3, Pick(Pal, 3));
        Disc(C, 10, 6, 5, 2, Pick(Pal, 2));
        C.Put(12, 5, STEELS[1]);
        Stain(C, 0, 0, 20, 28, RUSTY, Seed, 0.25f, 3);
        C.Outline(new Color("101216"));
        return C;
    }

    Canvas Pallet(int Seed)
    {
        var C = new Canvas(40, 20);
        C.Anchor = new Vector2I(20, 15);
        C.Shadow(22, 15, 18, 3);
        for (int I = 0; I < 5; I++) Box(C, 3 + I * 7, 4, 6, 7, 3, TIMBER, Seed + I, 3, 2);
        for (int X = 3; X < 37; X++) { C.Put(X, 14, TIMBER[1]); C.Put(X, 15, TIMBER[0]); }
        C.Outline(new Color("1C150F"));
        return C;
    }

    // --- 14 Beached Shipwreck --------------------------------------------------------
    // A small cargo ship run aground, bow to the left: the black upper hull and the red below, rust running down,
    // a few containers on the deck, the white bridge and funnel at the stern, a crane mast, and the hull split
    // open toward the shore.
    Canvas Ship(int Seed)
    {
        var R = new Random(Seed);
        var Raw = new Canvas(470, 230);
        int Keel = 206;
        // the hull: deck line high at the bow, bottom curving up at the bow
        int DeckAt(int X) => 112 - (X < 80 ? (80 - X) / 3 : 0);
        int BottomAt(int X) => Keel - (X < 110 ? (int)((110 - X) * (110 - X) / 140f) : 0) - (X > 440 ? (X - 440) : 0);
        int Split = 250;
        for (int X = 12; X < 462; X++)
        {
            int Dk = DeckAt(X), Bt = BottomAt(X);
            if (Bt <= Dk + 6) continue;
            if (X >= Split - 4 && X <= Split + 5) continue;
            // the deck seen from above
            for (int Y = Dk - 14; Y < Dk; Y++)
            {
                int B = Y == Dk - 14 ? 3 : 2;
                if (Hash(X, Y, Seed) > 0.92f) B--;
                Raw.Put(X, Y, Pick(DECK, B));
            }
            // the side: black above the waterline, red below
            int Water = Dk + (int)((Bt - Dk) * 0.55f);
            for (int Y = Dk; Y <= Bt; Y++)
            {
                Color[] P = Y < Water ? HULL : HULL_RED;
                int B = Y == Dk ? 4 : Y == Dk + 1 ? 3 : 2;
                if (Y > Bt - 3) B = 0;
                if (((X - 12) % 34) == 0) B--;
                if (Hash(X, Y, Seed + 1) > 0.95f) B--;
                Raw.Put(X, Y, Pick(P, B));
            }
            Raw.Put(X, Water, Pick(WHITEWASH, 2));
        }
        // the torn edges of the split: jagged plates and the dark hold behind
        for (int Y = 96; Y < Keel; Y++)
        {
            int J = (int)(Hash(0, Y, Seed) * 4);
            for (int X = Split - 6 - J; X <= Split + 7 + J; X++)
            {
                bool Edge = X < Split - 3 - J / 2 || X > Split + 4 + J / 2;
                if (Edge) Raw.Put(X, Y, Pick(RUSTY, 1 + (Y % 3 == 0 ? 1 : 0)));
                else Raw.Put(X, Y, Y > 150 ? CAVE_DARK : new Color("1C1A1E"));
            }
        }
        // containers on the deck, a hatch, the crane mast
        Box(Raw, 120, 72, 46, 8, 26, BOX_ORANGE, Seed + 2, 3, 2, 0.06f);
        Ribs(Raw, 120, 80, 46, 26, BOX_ORANGE, 2, 4);
        Box(Raw, 170, 76, 40, 8, 22, BOX_BLUE, Seed + 3, 3, 2, 0.06f);
        Ribs(Raw, 170, 84, 40, 22, BOX_BLUE, 2, 4);
        Box(Raw, 262, 84, 40, 6, 14, BOX_RED, Seed + 4, 3, 2, 0.06f);
        Box(Raw, 214, 92, 30, 6, 8, DECK, Seed + 5, 3, 1);
        for (int Y = 20; Y < 100; Y++) { Raw.Put(92, Y, STEELS[3]); Raw.Put(93, Y, STEELS[1]); }
        Line(Raw, new Vector2(93, 26), new Vector2(150, 70), STEELS[2]);
        Line(Raw, new Vector2(93, 30), new Vector2(40, 92), STEELS[1]);
        // the bridge, its windows, the funnel
        Box(Raw, 340, 34, 84, 8, 70, WHITEWASH, Seed + 6, 3, 2, 0.1f);
        Box(Raw, 352, 14, 60, 6, 22, WHITEWASH, Seed + 7, 3, 2, 0.1f);
        for (int X = 356; X < 408; X += 8) Rect(Raw, X, 26, 6, 6, GLASSY, 1);
        for (int Row = 0; Row < 3; Row++) for (int X = 348; X < 418; X += 10) Rect(Raw, X, 52 + Row * 16, 5, 6, GLASSY, 1);
        Box(Raw, 426, 22, 18, 5, 40, HULL_RED, Seed + 8, 3, 2);
        Rect(Raw, 426, 34, 18, 5, WHITEWASH, 3);
        for (int Y = 0; Y < 14; Y++) Raw.Put(372, Y, STEELS[2]);
        Line(Raw, new Vector2(372, 2), new Vector2(392, 2), STEELS[2]);
        // rust running down the hull and the white superstructure, barnacles at the foot
        Stain(Raw, 12, 96, 450, 112, RUSTY, Seed + 9, 0.2f, 70);
        Stain(Raw, 340, 14, 104, 92, Hex("4A3020", "6E4A30", "8E6440"), Seed + 10, 0.18f, 20);
        for (int I = 0; I < 160; I++)
        {
            int X = 20 + R.Next(440), Y = Keel - 14 + R.Next(12);
            if (Raw.Body(X, Y)) Raw.Set(X, Y, Pick(LIME, 2 + R.Next(2)));
        }
        var C = Turned(Raw, 3.5f, 235, Keel);
        C.Anchor = new Vector2I(235, Keel);
        C.Shadow(250, Keel + 2, 220, 12);
        C.Outline(new Color("0C0E10"));
        return C;
    }

    // machinery washed onto the sand: an engine block with pipes
    Canvas Gear(int Seed)
    {
        var C = new Canvas(38, 30);
        C.Anchor = new Vector2I(19, 25);
        C.Shadow(22, 25, 16, 3);
        Box(C, 6, 6, 24, 6, 13, STEELS, Seed, 3, 2);
        for (int X = 9; X < 28; X += 4) Rect(C, X, 12, 2, 6, STEELS, 0);
        Disc(C, 30, 14, 4, 4, ALLOY[1]);
        Disc(C, 30, 14, 2, 2, STEELS[0]);
        Line(C, new Vector2(4, 8), new Vector2(10, 2), RUSTY[2]);
        Line(C, new Vector2(5, 9), new Vector2(11, 3), RUSTY[1]);
        Stain(C, 0, 0, 38, 30, RUSTY, Seed + 1, 0.3f, 4);
        C.Outline(new Color("101216"));
        return C;
    }

    // --- 16 Southwest Cave -----------------------------------------------------------
    // A limestone bluff with a shallow cave at its foot: big pale blocks, ferns and vines hanging, the dark mouth
    // with a fringe of dripstone and a dirt floor running out.
    Canvas Bluff(int Seed)
    {
        var R = new Random(Seed);
        var C = new Canvas(240, 156);
        int Gy = 150;
        C.Anchor = new Vector2I(120, Gy);
        C.Shadow(132, Gy, 112, 8);
        // the cliff face in blocks of limestone: each block its own height, a lit top ledge with moss, a lit left
        // edge and a shaded right one, bedding lines across
        var Edges = new List<int> { 8 };
        while (Edges[^1] < 232) Edges.Add(Math.Min(232, Edges[^1] + 18 + R.Next(18)));
        for (int B = 0; B < Edges.Count - 1; B++)
        {
            int X0 = Edges[B], X1 = Edges[B + 1];
            float Mid = (X0 + X1) / 2f;
            float Taper = Math.Clamp(MathF.Min(Mid - 8, 232 - Mid) / 50f, 0.35f, 1);
            int Hgt = (int)((70 + R.Next(46)) * Taper);
            int Ledge = 7 + R.Next(5);
            int Bed = R.Next(6);
            for (int X = X0; X < X1; X++)
            {
                int Top = Gy - Hgt + (int)(Hash(X, B, Seed) * 2) + (X == X0 || X == X1 - 1 ? 2 : 0);
                for (int Y = Top; Y <= Gy; Y++)
                {
                    int Bb;
                    if (Y < Top + Ledge) Bb = Y == Top ? 4 : 3;
                    else
                    {
                        Bb = 2;
                        if (X < X0 + 2) Bb = 3;
                        if (X >= X1 - 3) Bb = 1;
                        if (((Y - Top - Ledge + Bed) % 15) == 0) Bb = 1;
                        if (Y >= Gy - 2) Bb = 1;
                        if (Hash(X, Y, Seed) > 0.94f) Bb--;
                    }
                    C.Put(X, Y, Pick(LIME, Bb), 2, B + 1);
                    if (Y < Top + Ledge && Y > Top && Value(X, Y, 4, Seed + B) > 0.6f) C.Set(X, Y, Pick(MOSS, 1 + (int)(Hash(X, Y, Seed) * 2)));
                }
                // the seam between blocks
                if (X == X0) for (int Y = Top + Ledge; Y <= Gy; Y++) C.Set(X, Y, LIME[0]);
            }
        }
        // fallen blocks at the foot, either side of the mouth
        int Tag = 50;
        foreach (var (X, Rx, Ry) in new[] { (30, 16, 13), (66, 12, 10), (176, 14, 12), (214, 15, 12) })
            Boulder(C, R, X, Gy + 2, Rx, Ry, LIME, false, Tag++, 0.1f);
        // the cave mouth: a dark arch, darkest at the back, teeth of dripstone along its lip
        float Mx = 120, My = Gy - 1, Mw = 32, Mh = 46;
        for (int Y = (int)(My - Mh); Y <= My; Y++)
            for (int X = (int)(Mx - Mw); X <= Mx + Mw; X++)
            {
                float Dx = (X + 0.5f - Mx) / Mw, Dy = (Y + 0.5f - My) / Mh;
                if (Dx * Dx + Dy * Dy > 1) continue;
                float D = MathF.Sqrt(Dx * Dx + Dy * Dy);
                Color Col = D > 0.88f ? LIME[0] : D > 0.68f ? new Color("221E22") : CAVE_DARK;
                C.Put(X, Y, Col, 2, 99);
            }
        for (int X = (int)(Mx - Mw + 6); X < Mx + Mw - 6; X += 4)
        {
            float Dx = (X - Mx) / Mw;
            int Lip = (int)(My - Mh * MathF.Sqrt(Math.Max(0, 1 - Dx * Dx)));
            int L = 2 + R.Next(5);
            for (int S = 0; S < L; S++) { C.Put(X, Lip + S, LIME[2]); if (S < L - 2) C.Put(X + 1, Lip + S, LIME[1]); }
        }
        // the dirt floor of the mouth
        for (int Y = (int)My - 6; Y <= My; Y++)
            for (int X = (int)(Mx - 24); X < Mx + 24; X++)
                if (MathF.Abs(X - Mx) < 24 - (My - Y) * 2) C.Put(X, Y, Pick(Hex("3A2E22", "54432F", "6E5A40"), (Y - (int)My + 6) / 3), 3);
        Vines(C, R, 12, 228, 30, 40);
        // ferns at the foot
        for (int I = 0; I < 7; I++)
        {
            float Fx = 20 + I * 32 + R.Next(10);
            if (MathF.Abs(Fx - Mx) < 34) continue;
            for (int K = 0; K < 5; K++)
            {
                float A = -MathF.PI / 2 + (K - 2) * 0.5f;
                Leaf(C, new Vector2(Fx, Gy), new Vector2(MathF.Cos(A), MathF.Sin(A) * 0.8f), 9 + R.Next(4), 2, BROADLEAF);
            }
        }
        C.Outline(LIME[0].Darkened(0.6f));
        return C;
    }

    // --- 17 South Cape Lookout ------------------------------------------------------
    // A concrete observation platform from above: a six-sided slab with a low parapet, open at the back where the
    // trail comes up. Flat on the ground layer; the anchor is its middle.
    Canvas Platform(int Seed)
    {
        var C = new Canvas(150, 112);
        var Ctr = new Vector2(75, 54);
        var Hex6 = new Vector2[6];
        for (int I = 0; I < 6; I++)
        {
            float A = I * MathF.Tau / 6;
            Hex6[I] = Ctr + new Vector2(MathF.Cos(A) * 66, MathF.Sin(A) * 44);
        }
        C.Anchor = new Vector2I((int)Ctr.X, (int)Ctr.Y);
        // the slab's front face (the platform stands a little proud of the rock)
        var Low = Array.ConvertAll(Hex6, P => P + new Vector2(0, 8));
        Poly(C, Low, (X, Y) => Pick(CONC, 1));
        Poly(C, Hex6, (X, Y) =>
        {
            int B = 3;
            if (((X + 2 * Y) % 22) == 0 || ((X - 2 * Y) % 22) == 0) B = 2;
            if (Hash(X, Y, Seed) > 0.93f) B = 2;
            if (Value(X, Y, 6, Seed) > 0.8f) return Pick(MOSS, 1);
            return Pick(CONC, B);
        });
        // the parapet: a low wall round five sides, a gap on the side facing the trail (the top)
        for (int I = 0; I < 6; I++)
        {
            if (I == 4) continue;
            var A = Hex6[I]; var B = Hex6[(I + 1) % 6];
            int N = (int)A.DistanceTo(B);
            for (int S = 0; S <= N; S++)
            {
                var P = A.Lerp(B, (float)S / N);
                var In = (Ctr - P).Normalized();
                for (int K = 0; K < 5; K++)
                {
                    var Q = P + In * K;
                    bool Front = P.Y > Ctr.Y + 4;
                    C.Put((int)Q.X, (int)Q.Y - (Front ? 0 : 6), Pick(CONC, K == 0 ? 4 : 3));
                    if (!Front) for (int H = 0; H < 6; H++) C.Put((int)Q.X, (int)Q.Y - H, Pick(CONC, H == 5 ? 4 : 2));
                }
            }
        }
        Stain(C, 0, 0, 150, 112, Hex("3E3E38", "55554C", "6A6A60"), Seed + 1, 0.15f, 10);
        C.Outline(new Color("1A1B1E"));
        return C;
    }

    // --- 18 Abandoned Herb Garden ----------------------------------------------------
    // a raised timber bed of dark soil, overgrown with herbs (one with purple flowers)
    Canvas Bed(int Seed, int Shape)
    {
        var R = new Random(Seed);
        var C = new Canvas(84, 48);
        int Gy = 42;
        C.Anchor = new Vector2I(42, Gy);
        C.Shadow(46, Gy, 40, 4);
        Box(C, 4, Gy - 30, 74, 24, 6, TIMBER, Seed, 3, 2);
        Rect(C, 8, Gy - 26, 66, 17, SOIL, 1, Seed + 1, 0.3f);
        // plants in rows
        for (int Row = 0; Row < 2; Row++)
            for (int I = 0; I < 6; I++)
            {
                var Base = new Vector2(13 + I * 11 + R.Next(3), Gy - 21 + Row * 8);
                for (int K = 0; K < 5; K++)
                {
                    float A = -MathF.PI / 2 + (K - 2) * 0.6f;
                    Leaf(C, Base, new Vector2(MathF.Cos(A), MathF.Sin(A) * 0.7f), 5 + R.Next(3), 1.8f, BROADLEAF);
                }
                if (Shape == 1 && R.Next(2) == 0)
                    for (int S = 0; S < 6; S++) C.Put((int)Base.X, (int)Base.Y - 6 - S, Pick(PURPLE, 2 + (S % 2)));
            }
        // a broken board
        for (int X = 50; X < 62; X++) C.Put(X, Gy - 6, SOIL[0]);
        C.Outline(new Color("1C150F"));
        return C;
    }

    // a broken picket fence, leaning, with gaps
    Canvas Fence(int Seed)
    {
        var R = new Random(Seed);
        var Raw = new Canvas(108, 44);
        int Gy = 38;
        for (int X = 4; X < 104; X++) { Raw.Put(X, Gy - 22, TIMBER[3]); Raw.Put(X, Gy - 21, TIMBER[1]); }
        for (int X = 4; X < 70; X++) { Raw.Put(X, Gy - 10, TIMBER[3]); Raw.Put(X, Gy - 9, TIMBER[1]); }
        for (int X = 6; X < 102; X += 7)
        {
            if (Hash(X, 0, Seed) < 0.22f) continue;
            int H = 26 + R.Next(5);
            Box(Raw, X, Gy - H, 4, 2, H - 2, TIMBER, Seed + X, 3, 2);
        }
        var C = Turned(Raw, 4, 20, Gy);
        C.Anchor = new Vector2I(54, Gy);
        C.Shadow(58, Gy, 50, 3);
        Vines(C, R, 50, 104, 6, 12);
        C.Outline(new Color("1C150F"));
        return C;
    }

    Canvas Can(int Seed)
    {
        var C = new Canvas(28, 22);
        C.Anchor = new Vector2I(12, 18);
        C.Shadow(14, 18, 10, 2.5f);
        for (int Y = 7; Y < 18; Y++)
            for (int X = 6; X < 17; X++)
            {
                float Dx = (X - 11) / 5.5f;
                C.Put(X, Y, Pick(STEELS, Dx < -0.4f ? 4 : Dx < 0.4f ? 3 : 2));
            }
        Disc(C, 11, 7, 5.5f, 2, STEELS[4]);
        Line(C, new Vector2(16, 14), new Vector2(24, 6), STEELS[2]);
        Rect(C, 23, 4, 3, 3, STEELS, 3);
        Line(C, new Vector2(8, 6), new Vector2(11, 2), STEELS[2]);
        Line(C, new Vector2(11, 2), new Vector2(15, 6), STEELS[2]);
        Stain(C, 0, 0, 28, 22, RUSTY, Seed, 0.2f, 2);
        C.Outline(new Color("101216"));
        return C;
    }
}
