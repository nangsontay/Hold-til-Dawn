using Godot;
using System;

// Owner: Vu Duc Viet Anh. Additional Atlas set dressing, generated with the same canvas, palette, pixel scale
// and anchors as PixelLandmarks.cs. Decorative machinery is abandoned, never an interactive crafting station.
public partial class PixelNature
{
    void DrawLandmarkDetails()
    {
        Lm("lm_radio", 0, FieldRadio(), 0);
        Lm("lm_generator", 0, OldGenerator(), 12);
        Lm("lm_coconuts", 0, CoconutPile(), 0);
        Lm("lm_fossil", 0, FossilSlab(), 0);
        for (int I = 0; I < 3; I++) Lm("lm_flowers", I, Wildflowers(I), 0);
        Lm("lm_signpost", 0, TrailSign(), 4);
        Lm("lm_scrap", 0, SalvagePile(), 0);
        Lm("lm_cable", 0, CableReel(), 9);
        Lm("lm_barricade", 0, Sandbags(), 0);
        Lm("lm_radar", 0, RadarDish(), 12);
        Lm("lm_solar", 0, SolarPanel(), 12);
        Lm("lm_windsock", 0, Windsock(), 4);
        Lm("lm_boat", 0, FishingBoat(), 0);
        Lm("lm_buoys", 0, BuoyRack(), 7);
        Lm("lm_rope", 0, RopeCoil(), 0);
        Lm("lm_cairn", 0, TrailCairn(), 7);
        Lm("lm_scope", 0, Telescope(), 6);
        Lm("lm_crane", 0, YardCrane(), 0);
        Lm("lm_lifering", 0, LifeRing(), 0);
        Lm("lm_anchor", 0, ShipAnchor(), 0);
        Lm("lm_fern", 0, FernFan(), 0);
        Lm("lm_expedition", 0, ExpeditionCache(), 0);
        Lm("lm_arch", 0, GardenArch(), 0);
        Lm("lm_rainbarrel", 0, RainBarrel(), 10);
        Lm("lm_pots", 0, GardenPots(), 0);
    }

    static Canvas DetailCanvas(int W, int H, int Ax, int Ay)
    {
        var C = new Canvas(W, H) { Anchor = new Vector2I(Ax, Ay) };
        C.Shadow(Ax + 4, Ay + 1, Math.Min(W * 0.4f, 38), 3);
        return C;
    }

    static Canvas FinishDetail(Canvas C)
    {
        C.Outline(new Color("211F24"));
        return C;
    }

    Canvas FieldRadio()
    {
        var C = DetailCanvas(54, 51, 27, 45);
        Box(C, 6, 32, 40, 6, 7, TIMBER, 3701);
        for (int X = 10; X < 45; X += 30) Box(C, X, 40, 3, 1, 5, TIMBER, 3702);
        Box(C, 12, 20, 23, 4, 9, OLIVE, 3703);
        Rect(C, 15, 25, 10, 5, GLASSY, 1);
        for (int X = 16; X < 24; X += 3) C.Put(X, 26, new Color("8AAC8A"));
        Disc(C, 30, 28, 2, 2, STEELS[3]);
        Line(C, new Vector2(31, 20), new Vector2(37, 4), STEELS[3]);
        Rect(C, 38, 31, 6, 3, WHITEWASH, 3);
        return FinishDetail(C);
    }

    Canvas OldGenerator()
    {
        var C = DetailCanvas(69, 46, 34, 40);
        Box(C, 9, 11, 49, 14, 14, OLIVE, 3711);
        Box(C, 14, 8, 20, 9, 3, OLIVE, 3712);
        for (int X = 36; X < 54; X += 3) Rect(C, X, 28, 1, 8, TYRES, 0);
        Box(C, 47, 3, 4, 3, 10, STEELS, 3713);
        Wheel(C, 18, 38, 5); Wheel(C, 51, 38, 5);
        Rect(C, 16, 26, 14, 6, TIN, 3);
        for (int X = 17; X < 29; X++) if (X % 5 < 2) Line(C, new Vector2(X, 26), new Vector2(X - 3, 31), TYRES[0]);
        Stain(C, 9, 20, 49, 17, RUSTY, 3714, 0.25f, 6);
        return FinishDetail(C);
    }

    Canvas CoconutPile()
    {
        var C = DetailCanvas(39, 24, 19, 19);
        var R = new Random(3721);
        foreach (var (X, Y) in new[] { (8, 16), (16, 12), (24, 17), (30, 11) })
            Ball(C, R, X, Y, 4.5f, COCO, X);
        Disc(C, 13, 16, 4, 3, WHITEWASH[3]);
        Disc(C, 13, 16, 2, 1.5f, COCO[0]);
        return FinishDetail(C);
    }

    Canvas FossilSlab()
    {
        var C = DetailCanvas(94, 48, 47, 41);
        Box(C, 7, 10, 78, 24, 6, LIME, 3731, 3, 1, 0.15f);
        var Centre = new Vector2(46, 22);
        Vector2 Prev = Centre;
        for (int I = 0; I < 150; I++)
        {
            float A = I * 0.12f, R = 1 + I * 0.11f;
            Vector2 P = Centre + new Vector2(MathF.Cos(A) * R, MathF.Sin(A) * R * 0.55f);
            Line(C, Prev, P, LIME[0]);
            Line(C, Prev + Vector2.Up, P + Vector2.Up, LIME[4]);
            Prev = P;
        }
        return FinishDetail(C);
    }

    Canvas Wildflowers(int Variant)
    {
        var C = new Canvas(54, 34) { Anchor = new Vector2I(27, 27) };
        var R = new Random(3741 + Variant);
        Color[] Petals = { PURPLE[2], new Color("F4D581"), new Color("ECADBE") };
        for (int I = 0; I < 16; I++)
        {
            int X = 4 + R.Next(44), Y = 15 + R.Next(14), H = 3 + R.Next(10);
            Line(C, new Vector2(X, Y), new Vector2(X - 1, Y - H), BROADLEAF[2]);
            C.Put(X + 1, Y - 3, BROADLEAF[3]);
            Disc(C, X - 1, Y - H, 2.2f, 1.5f, Petals[Variant]);
            C.Put(X - 1, Y - H, WHITEWASH[4]);
        }
        return C;
    }

    Canvas TrailSign()
    {
        var C = DetailCanvas(48, 61, 24, 55);
        Box(C, 23, 12, 4, 2, 42, TIMBER, 3751);
        Poly(C, new[] { new Vector2(5, 13), new Vector2(34, 13), new Vector2(43, 19), new Vector2(34, 25), new Vector2(5, 25) }, (X, Y) => TIMBER[Y < 15 ? 4 : 2]);
        Line(C, new Vector2(11, 19), new Vector2(31, 19), WHITEWASH[2]);
        Line(C, new Vector2(27, 16), new Vector2(32, 19), WHITEWASH[2]);
        Line(C, new Vector2(27, 22), new Vector2(32, 19), WHITEWASH[2]);
        Box(C, 11, 30, 26, 3, 9, OLIVE, 3752);
        return FinishDetail(C);
    }

    Canvas SalvagePile()
    {
        var C = DetailCanvas(70, 32, 35, 26);
        for (int I = 0; I < 6; I++)
        {
            int X = 7 + I * 9, Y = 13 + I % 3 * 3;
            Box(C, X, Y, 15, 5, 3, I % 2 == 0 ? ALLOY : RUSTY, 3761 + I);
            Line(C, new Vector2(X + 1, Y + 1), new Vector2(X + 11, Y + 4), STEELS[0]);
        }
        Wheel(C, 14, 22, 6);
        return FinishDetail(C);
    }

    Canvas CableReel()
    {
        var C = DetailCanvas(44, 39, 22, 33);
        Disc(C, 22, 12, 17, 8, TIMBER[3]);
        Rect(C, 12, 14, 20, 15, TYRES, 1);
        for (int Y = 15; Y < 29; Y += 3) Line(C, new Vector2(12, Y), new Vector2(32, Y), STEELS[1]);
        Disc(C, 22, 27, 18, 8, TIMBER[2]);
        Disc(C, 22, 26, 15, 6, TIMBER[3]);
        Disc(C, 22, 26, 4, 2, TIMBER[0]);
        return FinishDetail(C);
    }

    Canvas Sandbags()
    {
        var C = DetailCanvas(90, 32, 45, 27);
        for (int Row = 0; Row < 2; Row++)
            for (int I = 0; I < 4 - Row; I++)
            {
                int X = 6 + I * 20 + Row * 9, Y = 20 - Row * 10;
                Disc(C, X + 9, Y, 11, 6, CANVAS_C[1]);
                Disc(C, X + 8, Y - 2, 9, 4, CANVAS_C[3]);
                Line(C, new Vector2(X + 4, Y), new Vector2(X + 13, Y + 1), CANVAS_C[2]);
            }
        return FinishDetail(C);
    }

    Canvas RadarDish()
    {
        var C = DetailCanvas(100, 130, 50, 120);
        Box(C, 28, 110, 45, 8, 5, CONC, 3781);
        for (int D = -1; D <= 1; D += 2)
        {
            Line(C, new Vector2(50, 59), new Vector2(50 + D * 18, 113), STEELS[1]);
            Line(C, new Vector2(50, 60), new Vector2(51 + D * 18, 113), STEELS[3]);
        }
        for (int Y = 73; Y < 110; Y += 12)
        {
            float W = (Y - 59) * 0.32f;
            Line(C, new Vector2(50 - W, Y), new Vector2(50 + W + 4, Y + 12), STEELS[2]);
            Line(C, new Vector2(50 + W, Y), new Vector2(46 - W, Y + 12), STEELS[2]);
        }
        Disc(C, 46, 36, 36, 27, STEELS[0]);
        Disc(C, 44, 34, 33, 25, WHITEWASH[2]);
        Disc(C, 40, 31, 27, 20, WHITEWASH[3]);
        for (int I = 0; I < 8; I++)
        {
            Vector2 Edge = new Vector2(44, 34) + Vector2.FromAngle(I * Mathf.Tau / 8) * new Vector2(31, 23);
            Line(C, new Vector2(43, 35), Edge, STEELS[2]);
        }
        Line(C, new Vector2(44, 35), new Vector2(76, 13), STEELS[0]);
        Line(C, new Vector2(43, 34), new Vector2(75, 12), STEELS[4]);
        Box(C, 73, 9, 8, 4, 4, RUSTY, 3782);
        Stain(C, 10, 12, 67, 50, RUSTY, 3783, 0.09f, 2);
        return FinishDetail(C);
    }

    Canvas SolarPanel()
    {
        var C = DetailCanvas(74, 57, 37, 51);
        Box(C, 12, 34, 3, 2, 14, STEELS, 3791);
        Box(C, 58, 34, 3, 2, 14, STEELS, 3792);
        Box(C, 5, 10, 62, 25, 5, STEELS, 3793);
        Rect(C, 8, 12, 56, 21, BOX_BLUE, 1);
        for (int X = 8; X < 64; X += 9) Rect(C, X, 12, 1, 21, STEELS, 3);
        for (int Y = 12; Y < 33; Y += 7) Rect(C, 8, Y, 56, 1, STEELS, 3);
        Line(C, new Vector2(47, 14), new Vector2(35, 23), STEELS[4]);
        Line(C, new Vector2(35, 23), new Vector2(42, 30), TYRES[0]);
        return FinishDetail(C);
    }

    Canvas Windsock()
    {
        var C = DetailCanvas(72, 106, 22, 99);
        Box(C, 20, 12, 3, 1, 86, STEELS, 3801);
        Disc(C, 22, 99, 10, 3, CONC[2]);
        Poly(C, new[] { new Vector2(24, 13), new Vector2(40, 15), new Vector2(61, 30), new Vector2(62, 35), new Vector2(38, 25), new Vector2(24, 27) },
            (X, Y) => (X / 8) % 2 == 0 ? TIN[4] : WHITEWASH[3]);
        Line(C, new Vector2(23, 12), new Vector2(23, 28), STEELS[4]);
        return FinishDetail(C);
    }

    Canvas FishingBoat()
    {
        var C = DetailCanvas(124, 49, 62, 35);
        Poly(C, new[] { new Vector2(5, 20), new Vector2(31, 7), new Vector2(100, 12), new Vector2(118, 27), new Vector2(98, 41), new Vector2(29, 37) }, (X, Y) => BOX_BLUE[Y < 23 ? 3 : 1]);
        Poly(C, new[] { new Vector2(17, 21), new Vector2(33, 13), new Vector2(95, 17), new Vector2(106, 27), new Vector2(95, 33), new Vector2(32, 30) }, (X, Y) => TIMBER[1 + (Y % 5 == 0 ? 1 : 0)]);
        for (int X = 40; X < 95; X += 22) Box(C, X, 15, 6, 17, 2, TIMBER, 3811 + X);
        Line(C, new Vector2(30, 37), new Vector2(97, 40), WHITEWASH[3]);
        return FinishDetail(C);
    }

    Canvas BuoyRack()
    {
        var C = DetailCanvas(69, 64, 34, 57);
        foreach (int X in new[] { 9, 56 }) Box(C, X, 9, 4, 2, 46, TIMBER, 3821 + X);
        Box(C, 6, 11, 57, 3, 4, TIMBER, 3822);
        for (int I = 0; I < 4; I++)
        {
            int X = 19 + I * 10, Y = 28 + I % 2 * 7;
            Line(C, new Vector2(X, 14), new Vector2(X, Y), TIMBER[3]);
            Disc(C, X, Y + 4, 4, 7, I % 2 == 0 ? TIN[4] : WHITEWASH[3]);
            Rect(C, X - 3, Y + 3, 7, 3, BOX_RED, 3);
        }
        return FinishDetail(C);
    }

    Canvas RopeCoil()
    {
        var C = new Canvas(44, 23) { Anchor = new Vector2I(22, 16) };
        for (int I = 5; I >= 0; I--)
        {
            Disc(C, 21, 12, 5 + I * 2.5f, 2 + I, TIMBER[1]);
            Disc(C, 21, 11, 4 + I * 2.5f, 1 + I, TIMBER[4]);
        }
        Disc(C, 21, 11, 4, 1, TIMBER[0]);
        Line(C, new Vector2(35, 13), new Vector2(41, 19), TIMBER[3], 3);
        return C;
    }

    Canvas TrailCairn()
    {
        var C = DetailCanvas(45, 61, 22, 55);
        var R = new Random(3841);
        for (int I = 0; I < 4; I++) Boulder(C, R, 22 + I % 2 * 2, 55 - I * 10, 17 - I * 3, 8 - I, LIME, I == 0, I + 1);
        Line(C, new Vector2(25, 22), new Vector2(25, 3), TIMBER[2]);
        Poly(C, new[] { new Vector2(26, 4), new Vector2(40, 8), new Vector2(26, 12) }, (X, Y) => TIN[4]);
        return FinishDetail(C);
    }

    Canvas Telescope()
    {
        var C = DetailCanvas(54, 61, 26, 55);
        for (int X = 12; X <= 40; X += 14) Line(C, new Vector2(27, 30), new Vector2(X, 55), STEELS[2]);
        Box(C, 23, 29, 7, 3, 12, STEELS, 3851);
        Poly(C, new[] { new Vector2(9, 15), new Vector2(14, 8), new Vector2(44, 23), new Vector2(39, 31) }, (X, Y) => STEELS[(X + Y) % 6 == 0 ? 4 : 2]);
        Line(C, new Vector2(9, 15), new Vector2(14, 8), GLASSY[0]);
        return FinishDetail(C);
    }

    Canvas YardCrane()
    {
        var C = DetailCanvas(218, 196, 48, 184);
        Box(C, 23, 164, 54, 14, 6, CONC, 3861);
        foreach (int X in new[] { 32, 61 }) Box(C, X, 40, 6, 3, 133, TIN, 3862 + X);
        for (int Y = 49; Y < 165; Y += 23)
        {
            Line(C, new Vector2(38, Y), new Vector2(61, Y + 22), TIN[3]);
            Line(C, new Vector2(61, Y), new Vector2(38, Y + 22), TIN[2]);
        }
        Box(C, 22, 24, 174, 8, 10, TIN, 3863);
        for (int X = 26; X < 191; X += 18) Line(C, new Vector2(X, 33), new Vector2(X + 8, 40), TYRES[0]);
        Line(C, new Vector2(44, 28), new Vector2(52, 5), STEELS[2]);
        Line(C, new Vector2(52, 5), new Vector2(190, 25), STEELS[2]);
        Line(C, new Vector2(177, 40), new Vector2(177, 110), TYRES[0]);
        Line(C, new Vector2(179, 40), new Vector2(179, 110), STEELS[2]);
        Disc(C, 178, 111, 5, 7, TIN[3]);
        Line(C, new Vector2(178, 117), new Vector2(173, 125), STEELS[2]);
        Line(C, new Vector2(173, 125), new Vector2(181, 125), STEELS[3]);
        Box(C, 18, 124, 24, 9, 22, TIN, 3864);
        Rect(C, 20, 134, 16, 9, GLASSY, 1);
        Stain(C, 22, 24, 174, 20, RUSTY, 3865, 0.25f, 8);
        return FinishDetail(C);
    }

    Canvas LifeRing()
    {
        var C = DetailCanvas(34, 28, 17, 22);
        Disc(C, 16, 13, 12, 8, TIN[4]);
        Disc(C, 16, 13, 6, 4, TIMBER[0]);
        Rect(C, 14, 6, 4, 3, WHITEWASH, 4);
        Rect(C, 14, 18, 4, 3, WHITEWASH, 4);
        Rect(C, 5, 12, 4, 3, WHITEWASH, 4);
        Rect(C, 23, 12, 4, 3, WHITEWASH, 4);
        return FinishDetail(C);
    }

    Canvas ShipAnchor()
    {
        var C = DetailCanvas(60, 49, 30, 39);
        for (int D = -2; D <= 2; D++)
        {
            Line(C, new Vector2(30 + D, 10), new Vector2(30 + D, 38), STEELS[2]);
            Line(C, new Vector2(12, 30 + D), new Vector2(30, 39 + D), STEELS[1]);
            Line(C, new Vector2(48, 30 + D), new Vector2(30, 39 + D), STEELS[1]);
        }
        Disc(C, 30, 8, 6, 5, STEELS[2]);
        Disc(C, 30, 8, 3, 2, TYRES[0]);
        Line(C, new Vector2(17, 19), new Vector2(43, 19), STEELS[3]);
        Poly(C, new[] { new Vector2(9, 24), new Vector2(12, 35), new Vector2(20, 31) }, (X, Y) => RUSTY[2]);
        Poly(C, new[] { new Vector2(51, 24), new Vector2(48, 35), new Vector2(40, 31) }, (X, Y) => RUSTY[2]);
        return FinishDetail(C);
    }

    Canvas FernFan()
    {
        var C = DetailCanvas(57, 43, 28, 36);
        for (int I = 0; I < 7; I++)
        {
            float A = -Mathf.Pi + 0.2f + I * 0.46f;
            Vector2 Dir = Vector2.FromAngle(A);
            for (int K = 1; K <= 7; K++)
            {
                Vector2 P = new Vector2(28, 36) + Dir * K * 3;
                float Length = (8 - K) * 1.4f;
                Line(C, P, P + Dir.Orthogonal() * Length - Dir * 2, BROADLEAF[2]);
                Line(C, P, P - Dir.Orthogonal() * Length - Dir * 2, BROADLEAF[3]);
            }
            Line(C, new Vector2(28, 36), new Vector2(28, 36) + Dir * 23, BROADLEAF[4]);
        }
        return C;
    }

    Canvas ExpeditionCache()
    {
        var C = DetailCanvas(73, 43, 36, 36);
        Box(C, 6, 19, 30, 9, 9, OLIVE, 3891);
        for (int X = 12; X < 33; X += 15) Rect(C, X, 20, 3, 15, STEELS, 3);
        Box(C, 43, 8, 16, 8, 22, TIMBER, 3892);
        Rect(C, 45, 12, 12, 19, WHITEWASH, 2);
        Line(C, new Vector2(47, 15), new Vector2(54, 23), TIN[4]);
        Line(C, new Vector2(54, 15), new Vector2(47, 23), TIN[4]);
        Line(C, new Vector2(61, 33), new Vector2(65, 9), TIMBER[3]);
        Line(C, new Vector2(59, 10), new Vector2(69, 12), STEELS[3]);
        return FinishDetail(C);
    }

    Canvas GardenArch()
    {
        var C = DetailCanvas(121, 114, 60, 105);
        foreach (int X in new[] { 11, 102 }) Box(C, X, 24, 6, 3, 78, TIMBER, 3901 + X);
        Poly(C, new[] { new Vector2(8, 26), new Vector2(28, 8), new Vector2(88, 8), new Vector2(111, 26), new Vector2(108, 35), new Vector2(84, 19), new Vector2(33, 19), new Vector2(11, 35) }, (X, Y) => TIMBER[Y % 4 == 0 ? 3 : 2]);
        foreach (int X in new[] { 16, 102 })
            for (int Y = 24; Y < 99; Y += 6)
            {
                int Dx = (Y / 6) % 2 == 0 ? -3 : 2;
                Disc(C, X + Dx, Y, 5, 3, BROADLEAF[2]);
                C.Put(X + Dx - 2, Y - 1, BROADLEAF[4]);
                if (Y % 18 == 0) Disc(C, X + Dx + 2, Y - 2, 2, 2, PURPLE[3]);
            }
        Vines(C, new Random(3902), 20, 98, 18, 24);
        return FinishDetail(C);
    }

    Canvas RainBarrel()
    {
        var C = DetailCanvas(45, 48, 22, 40);
        Box(C, 8, 12, 28, 8, 22, TIMBER, 3911);
        Ribs(C, 8, 20, 28, 22, TIMBER, 2, 5);
        for (int Y = 24; Y <= 36; Y += 12) Rect(C, 8, Y, 28, 3, STEELS, 1);
        Disc(C, 22, 14, 14, 7, TIMBER[4]);
        Disc(C, 22, 14, 11, 5, GLASSY[1]);
        Line(C, new Vector2(15, 12), new Vector2(25, 12), GLASSY[3]);
        Box(C, 32, 32, 7, 2, 3, STEELS, 3912);
        return FinishDetail(C);
    }

    Canvas GardenPots()
    {
        var C = DetailCanvas(61, 39, 30, 32);
        for (int I = 0; I < 3; I++)
        {
            int X = 12 + I * 17, Y = 22 + I % 2 * 6;
            Box(C, X - 6, Y - 5, 13, 5, 10, TIN, 3921 + I);
            Disc(C, X, Y - 4, 7, 3, SOIL[0]);
            for (int K = -1; K <= 1; K++) Leaf(C, new Vector2(X, Y - 4), new Vector2(K * 0.7f, -1).Normalized(), 10, 2, BROADLEAF);
        }
        return FinishDetail(C);
    }
}
