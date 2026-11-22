using Godot;
using System;

// Native pixel assets for credible abandoned infrastructure and eroded limestone, at the game's 32px tile scale.
public partial class PixelNature
{
    void DrawRebuiltLandmarks()
    {
        Lm("lm_roof_fallen", 0, CollapsedRoof(), 0);
        Lm("lm_rubble", 0, CollapseDebris(0), 0);
        Lm("lm_rubble", 1, CollapseDebris(1), 0);
        Lm("lm_forklift", 0, YardForklift(), 0);
        Lm("lm_chainfence", 0, ChainFence(), 0);
    }

    Canvas ErodedCave()
    {
        var C = new Canvas(328, 192) { Anchor = new Vector2I(164, 172) };
        var R = new Random(5601);
        Color[] Rock = Hex("454944", "62685D", "818575", "9C9F8C", "B7B7A1", "CDCAB2");
        C.Shadow(171, 175, 151, 12);
        Vector2[] Outline = { new(10, 169), new(16, 135), new(36, 111), new(46, 85), new(74, 75), new(89, 48),
            new(123, 37), new(141, 22), new(173, 29), new(195, 17), new(226, 33), new(240, 54),
            new(265, 63), new(278, 89), new(298, 105), new(305, 139), new(319, 168), new(275, 177),
            new(233, 172), new(188, 178), new(128, 174), new(83, 180), new(46, 173) };
        Poly(C, Outline, (X, Y) => {
            float Light = .48f - X / 600f + (172 - Y) / 340f;
            float N = Value(X, Y, 19, 5602);
            return Pick(Rock, (int)(2 + Light * 2 + (N > .7f ? 1 : N < .25f ? -1 : 0)));
        });
        // Large sloping planes and bedding, rather than evenly spaced vertical columns.
        Poly(C, new Vector2[] { new(25, 137), new(91, 67), new(172, 35), new(137, 89), new(73, 131), new(46, 169) }, (X, Y) => Rock[3]);
        Poly(C, new Vector2[] { new(172, 35), new(195, 17), new(226, 33), new(231, 89), new(201, 129), new(175, 82) }, (X, Y) => Rock[4]);
        Poly(C, new Vector2[] { new(241, 68), new(278, 89), new(302, 132), new(309, 166), new(270, 172), new(252, 124) }, (X, Y) => Rock[1]);
        for (int Band = 0; Band < 7; Band++)
        {
            int Y = 60 + Band * 16;
            for (int X = 18; X < 311; X++)
            {
                int Py = Y + (int)(6 * MathF.Sin(X * .019f + Band) + X * .045f);
                if (!C.Body(X, Py) || !C.Body(X, Py - 4)) continue;
                if ((X / 13 + Band * 3) % 9 != 0) C.Set(X, Py, Rock[1]);
                if (C.Body(X, Py - 1)) C.Set(X, Py - 1, Rock[4]);
            }
        }
        foreach (var (X, Y, Dx, Dy) in new[] { (91, 53, -19, 59), (219, 42, 13, 67), (58, 102, -21, 53), (271, 91, 21, 60) })
        {
            Vector2 A = new(X, Y), B = A + new Vector2(Dx, Dy * .5f), D = A + new Vector2(Dx * .6f, Dy);
            Line(C, A, B, Rock[0]); Line(C, B, D, Rock[0]);
            Line(C, A + Vector2.Left, B + Vector2.Left, Rock[4]);
        }
        // Uneven weathered lip, then two recessed planes; the roof mass hangs over the opening.
        Vector2[] Rim = { new(99, 171), new(106, 140), new(121, 111), new(141, 100), new(160, 102),
            new(181, 94), new(202, 108), new(216, 135), new(225, 173) };
        Poly(C, Rim, (X, Y) => Rock[0]);
        Vector2[] Mouth = { new(108, 171), new(116, 142), new(128, 121), new(143, 111), new(161, 114),
            new(180, 105), new(195, 117), new(207, 143), new(214, 172) };
        Poly(C, Mouth, (X, Y) => new Color(X < 128 || X > 199 ? "242B29" : Y < 149 ? "101815" : "18221C"));
        Poly(C, new Vector2[] { new(110, 170), new(142, 156), new(181, 155), new(214, 172) },
            (X, Y) => new Color(Y < 162 ? "30362B" : "555344"));
        Line(C, new Vector2(121, 171), new Vector2(146, 160), new Color("6D6D59"));
        for (int I = 0; I < 20; I++)
        {
            int X = 14 + R.Next(298), Y = 162 + R.Next(12);
            if (X > 101 && X < 228) continue;
            int Size = 3 + R.Next(7);
            Poly(C, new Vector2[] { new(X, Y), new(X + Size, Y - Size), new(X + Size * 2, Y - 2), new(X + Size, Y + 3) },
                (Px, Py) => Py < Y - 2 ? Rock[4] : Rock[2]);
        }
        // Thin damp seams and lichen follow cracks, leaving the pale rock readable.
        for (int I = 0; I < 34; I++)
        {
            int X = 30 + R.Next(268), Y = 40 + R.Next(105);
            if (!C.Body(X, Y) || !C.Body(X, Y + 6) || X > 101 && X < 221 && Y > 95) continue;
            for (int K = 0; K < 3 + R.Next(5); K++) if (C.Body(X + K / 3, Y + K)) C.Set(X + K / 3, Y + K, MOSS[0]);
        }
        C.Outline(new Color("30362E")); return C;
    }

    Canvas BrokenDepotWall(int Variant)
    {
        int Seed = 5701 + Variant;
        var C = new Canvas(154, 106) { Anchor = new Vector2I(77, 96) };
        C.Shadow(83, 97, 72, 5);
        int[] Tops = new int[144];
        for (int X = 0; X < 144; X++)
        {
            int Top = Variant == 0 ? (X < 21 ? 53 - X : X > 110 ? 26 + (X - 110) : 25) :
                (X < 28 ? 64 - X : X > 101 ? 24 + (X - 101) / 2 : 22);
            Top += (int)(Value(X, 0, 7, Seed) * 6);
            Tops[X] = Top;
            for (int Y = Top; Y <= 96; Y++)
            {
                Color Col = Y < Top + 7 ? CONC[4] : Y > 76 ? new Color("606D60") : new Color("A3A49A");
                if (Y > Top + 7 && Y < 77 && Value(X, Y, 13, Seed) < .28f) Col = CONC[2];
                if (Y == Top + 7 || Y > 91) Col = CONC[1];
                C.Put(X + 5, Y, Col);
            }
        }
        // Exposed brick infill where plaster has peeled; broken edges reveal the concrete aggregate.
        for (int Y = 45; Y < 90; Y++)
            for (int X = 7; X < 35; X++)
                if (Y > Tops[X - 5] + 8 && X < 24 + 8 * MathF.Sin(Y * .15f))
                    C.Set(X, Y, Y % 7 == 0 || (X + (Y / 7 % 2) * 6) % 12 == 0 ? CONC[1] : new Color("8A7560"));
        int Wx = Variant == 0 ? 46 : 68;
        Rect(C, Wx - 3, 43, 43, 37, CONC, 1);
        Rect(C, Wx, 46, 37, 29, Hex("202B2C", "35433F", "596661"), 0);
        Rect(C, Wx - 3, 79, 43, 3, CONC, 4);
        Line(C, new Vector2(Wx + 16, 45), new Vector2(Wx + 16, 75), STEELS[1]);
        Line(C, new Vector2(Wx, 59), new Vector2(Wx + 35, 59), STEELS[1]);
        Poly(C, new Vector2[] { new(Wx + 1, 47), new(Wx + 12, 47), new(Wx + 1, 60) }, (X, Y) => GLASSY[2]);
        foreach (int X in new[] { 17, 103, 124, 131 })
        {
            int Y = Tops[X - 5];
            Line(C, new Vector2(X, Y + 2), new Vector2(X + 2, Y - 12), RUSTY[1]);
            Line(C, new Vector2(X + 2, Y - 12), new Vector2(X + 9, Y - 16), RUSTY[2]);
        }
        Line(C, new Vector2(112, 42), new Vector2(105, 60), CONC[0]);
        Line(C, new Vector2(105, 60), new Vector2(114, 88), CONC[0]);
        // Old service stripe and a weathered junction box.
        Rect(C, 38, 85, 58, 2, CONC, 1);
        Box(C, 127, 67, 12, 4, 16, STEELS, Seed, 2, 1, .025f);
        Line(C, new Vector2(130, 86), new Vector2(130, 94), TYRES[1]);
        C.Outline(new Color("303632")); return C;
    }

    Canvas DepotReturnWall()
    {
        var C = new Canvas(57, 155) { Anchor = new Vector2I(29, 146) };
        Poly(C, new Vector2[] { new(9, 29), new(26, 8), new(42, 19), new(45, 126), new(28, 147), new(10, 134) }, (X, Y) => CONC[2]);
        Poly(C, new Vector2[] { new(9, 29), new(26, 8), new(34, 14), new(18, 34), new(20, 128), new(10, 134) }, (X, Y) => CONC[4]);
        Poly(C, new Vector2[] { new(28, 147), new(28, 104), new(45, 86), new(45, 126) }, (X, Y) => CONC[1]);
        for (int Y = 43; Y < 126; Y += 20) Line(C, new Vector2(22, Y), new Vector2(42, Y - 18), CONC[1]);
        Line(C, new Vector2(29, 39), new Vector2(23, 65), CONC[0]);
        Line(C, new Vector2(23, 65), new Vector2(29, 82), CONC[0]);
        Line(C, new Vector2(26, 15), new Vector2(27, 2), RUSTY[2]);
        C.Outline(new Color("303632")); return C;
    }

    Canvas FreightContainer(int Variant, bool Stacked)
    {
        var C = new Canvas(178, Stacked ? 136 : 80) { Anchor = new Vector2I(89, Stacked ? 126 : 70) };
        C.Shadow(95, C.Anchor.Y + 1, 81, 6);
        void One(int Ground, Color[] Paint, int Seed)
        {
            int Top = Ground - 57;
            // Long freight-container proportions, a visible roof and a shaded short end.
            Box(C, 6, Top, 149, 14, 43, Paint, Seed, 3, 2, .02f);
            Poly(C, new Vector2[] { new(155, Top + 14), new(169, Top + 5), new(169, Ground - 9), new(155, Ground) }, (X, Y) => Paint[1]);
            Poly(C, new Vector2[] { new(6, Top), new(20, Top - 9), new(169, Top - 9), new(155, Top + 14) }, (X, Y) => Paint[3]);
            for (int X = 15; X < 149; X += 6)
            {
                Rect(C, X, Top + 17, 2, 37, Paint, 1);
                Rect(C, X + 2, Top + 17, 1, 36, Paint, 3);
                Line(C, new Vector2(X, Top + 10), new Vector2(X + 11, Top - 4), Paint[2]);
            }
            Rect(C, 7, Ground - 4, 148, 4, Paint, 0);
            Rect(C, 7, Top + 13, 148, 3, Paint, 4);
            foreach (int X in new[] { 7, 149 })
                foreach (int Y in new[] { Top + 13, Ground - 6 })
                {
                    Rect(C, X, Y, 5, 5, STEELS, 2); C.Put(X + 2, Y + 2, TYRES[0]);
                }
            // Door locking rods on the short end, hinges, streaked rust at welds and base.
            for (int K = 0; K < 2; K++)
                Line(C, new Vector2(158 + K * 6, Top + 16 - K * 3), new Vector2(158 + K * 6, Ground - 5 - K * 3), STEELS[3]);
            for (int I = 0; I < 29; I++)
            {
                int X = 10 + (I * 37 + Seed) % 140;
                int H = 2 + I % 8;
                for (int Y = Ground - H; Y < Ground - 2; Y++) C.Set(X, Y, RUSTY[I % 3]);
            }
            Rect(C, 26, Top + 25, 23, 9, Paint, 2);
            for (int I = 0; I < 6; I++) Rect(C, 28 + I * 3, Top + 27, 2, 4, WHITEWASH, 2);
            Rect(C, 117, Top + 28, 11, 7, WHITEWASH, 2); Rect(C, 120, Top + 30, 5, 3, TYRES, 1);
        }
        One(C.Anchor.Y, Variant == 0 ? BOX_BLUE : BOX_ORANGE, 5801);
        if (Stacked) One(C.Anchor.Y - 57, BOX_RED, 5802);
        C.Outline(new Color("20282B")); return C;
    }

    Canvas OpenFreightContainer()
    {
        var C = new Canvas(112, 147) { Anchor = new Vector2I(52, 133) };
        C.Shadow(59, 132, 47, 8);
        // Roof runs away from the open end; dark interior has depth and a ribbed wooden floor.
        Poly(C, new Vector2[] { new(15, 72), new(34, 14), new(91, 14), new(78, 72) }, (X, Y) => BOX_RED[3]);
        for (int I = 0; I < 7; I++) Line(C, new Vector2(22 + I * 8, 67), new Vector2(38 + I * 8, 18), BOX_RED[2]);
        Rect(C, 14, 73, 66, 58, BOX_RED, 1);
        Rect(C, 20, 78, 54, 49, TYRES, 0);
        Poly(C, new Vector2[] { new(21, 126), new(35, 105), new(65, 105), new(73, 126) }, (X, Y) => DECK[1]);
        for (int Y = 111; Y < 127; Y += 4) Line(C, new Vector2(29, Y), new Vector2(70, Y), DECK[2]);
        Box(C, 35, 93, 16, 5, 17, OLIVE, 5820, 2, 1, .02f);
        Box(C, 55, 102, 12, 5, 10, TIMBER, 5821, 3, 2, .02f);
        // Both open doors swing away from the central walkable opening.
        Poly(C, new Vector2[] { new(14, 75), new(2, 83), new(2, 141), new(14, 131) }, (X, Y) => BOX_RED[2]);
        Poly(C, new Vector2[] { new(80, 75), new(106, 85), new(106, 141), new(80, 131) }, (X, Y) => BOX_RED[2]);
        for (int X = 85; X < 106; X += 5) Line(C, new Vector2(X, 85), new Vector2(X, 132), BOX_RED[1]);
        Line(C, new Vector2(96, 89), new Vector2(96, 132), STEELS[3]);
        Rect(C, 13, 72, 67, 4, BOX_RED, 4); Rect(C, 13, 129, 67, 4, STEELS, 1);
        C.Outline(new Color("20282B")); return C;
    }

    Canvas CollapsedRoof()
    {
        var C = new Canvas(116, 64) { Anchor = new Vector2I(58, 54) };
        C.Shadow(61, 55, 52, 7);
        Poly(C, new Vector2[] { new(7, 36), new(37, 9), new(99, 15), new(110, 47), new(67, 58), new(43, 45) }, (X, Y) => STEELS[2]);
        for (int X = 18; X < 101; X += 6) Line(C, new Vector2(X, 34), new Vector2(X + 13, 18), STEELS[0]);
        Line(C, new Vector2(10, 45), new Vector2(102, 20), RUSTY[1]);
        Line(C, new Vector2(18, 48), new Vector2(97, 25), STEELS[3]);
        Line(C, new Vector2(30, 14), new Vector2(59, 50), RUSTY[2]);
        Box(C, 18, 44, 23, 6, 5, CONC, 5851, 3, 1, .05f);
        C.Outline(new Color("303632")); return C;
    }

    Canvas CollapseDebris(int Variant)
    {
        var C = new Canvas(83, 42) { Anchor = new Vector2I(41, 33) };
        var R = new Random(5861 + Variant); C.Shadow(44, 34, 36, 5);
        for (int I = 0; I < 12; I++)
        {
            int X = 5 + R.Next(63), Y = 15 + R.Next(18), Size = 4 + R.Next(8);
            Poly(C, new Vector2[] { new(X, Y), new(X + Size, Y - Size), new(X + Size + 5, Y - 2), new(X + 3, Y + 4) },
                (Px, Py) => Variant == 0 ? Pick(LIME, Py < Y - 2 ? 3 : 1) : Pick(CONC, Py < Y - 2 ? 4 : 2));
        }
        if (Variant == 1) { Line(C, new Vector2(20, 30), new Vector2(48, 11), RUSTY[1]); Line(C, new Vector2(48, 11), new Vector2(69, 15), RUSTY[2]); }
        C.Outline(new Color("303632")); return C;
    }

    Canvas YardForklift()
    {
        var C = new Canvas(94, 84) { Anchor = new Vector2I(47, 74) };
        C.Shadow(50, 75, 41, 5);
        Color[] Paint = Hex("69502C", "93713C", "BD9652", "D8B56E", "E3C58B");
        Wheel(C, 21, 69, 9); Wheel(C, 62, 69, 9);
        Box(C, 14, 45, 49, 9, 15, Paint, 5870, 3, 2, .03f);
        Box(C, 14, 37, 19, 8, 9, Paint, 5871, 3, 2, .02f);
        Rect(C, 37, 37, 14, 13, TYRES, 1); Rect(C, 34, 46, 19, 4, TYRES, 2);
        foreach (int X in new[] { 32, 59 }) Rect(C, X, 12, 3, 39, STEELS, 1);
        Box(C, 28, 9, 38, 7, 3, STEELS, 5872, 2, 1, .02f);
        foreach (int X in new[] { 69, 76 }) Rect(C, X, 19, 4, 53, STEELS, 1);
        Rect(C, 67, 40, 16, 4, STEELS, 3);
        for (int I = 0; I < 2; I++) Line(C, new Vector2(76, 66 + I * 7), new Vector2(91, 64 + I * 7), STEELS[3]);
        Line(C, new Vector2(56, 31), new Vector2(49, 44), STEELS[2]);
        Rect(C, 16, 54, 8, 4, WHITEWASH, 2); C.Outline(new Color("242A29")); return C;
    }

    Canvas ChainFence()
    {
        var C = new Canvas(158, 62) { Anchor = new Vector2I(79, 55) };
        Color Wire = new("6C7976", .72f);
        for (int Y = 10; Y < 52; Y++)
            for (int X = 5; X < 153; X++)
                if ((X + Y) % 10 == 0 || (X - Y + 200) % 10 == 0) C.Put(X, Y, Wire, 3);
        foreach (int X in new[] { 5, 78, 151 }) { Rect(C, X, 5, 3, 51, STEELS, 2); C.Put(X, 5, STEELS[4]); }
        Line(C, new Vector2(5, 10), new Vector2(153, 10), STEELS[2]);
        Line(C, new Vector2(5, 51), new Vector2(153, 51), STEELS[1]);
        return C;
    }
}
