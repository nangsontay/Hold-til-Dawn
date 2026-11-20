using Godot;
using System;
using System.Collections.Generic;

// Pixel-art sprite sheets of the seven NOX bosses (AGT-05..11, table 4.2), in the same sheet layout as
// tools/PixelChars.cs so the game animates them the same way: rows = right, down, left (mirrored), up;
// columns = idle 0-1, walk 2-5, attack 6-9 (wind-up, slam, impact, recover), knocked down 10-13 (staggers,
// knees give, falling, lying with the eyes shut). 48 x 48 px (a boss is 1.8 tiles across). Every boss is the same hulking NOX
// brute drawn from shaded shapes, with the parts that show its move: Juggernaut horns (charge), Broodmother egg
// sacs (summons), Bio-Cannon spore pods (volley), Burrower claws (burrow), Gatebreaker ram fist (door blow),
// Phantom hood and cloak (cloak), Ironhide front plates (front armour; its back is bare, where it is weak).
// Not part of the game. Run: godot --headless --path . tools/PixelBosses.tscn  (-- --out=<dir> to preview)
// Writes art/chars/boss_<name>.png and art/chars/bosses.json. The PNGs can be repainted by hand.
public partial class PixelBosses : Node
{
    const int S = 48;
    static readonly Color Ink = new("141019");
    static readonly Color Shade = new(0, 0, 0, 0.32f);
    static readonly Color Clear = new(0, 0, 0, 0);

    // [light, mid, dark]
    static Color[] Tones(string L, string M, string D, float A = 1.0f)
    {
        return new[] { new Color(L) { A = A }, new Color(M) { A = A }, new Color(D) { A = A } };
    }

    class Boss
    {
        public string Name;
        public Color[] Body;
        public Color[] Part;
        public Color Eye;
        public float TorsoRx = 11, TorsoRy = 10, ShoulderR = 5, HeadR = 6;
        public bool Legs = true;

        // the same boss with its eyes closed (lying knocked down)
        public Boss EyesShut()
        {
            var C = (Boss)MemberwiseClone();
            C.Eye = Body[2];
            return C;
        }
    }

    static readonly Boss[] Bosses =
    {
        new() { Name = "juggernaut", Body = Tones("8E3E66", "6B2A4A", "47182F"), Part = Tones("F4F0DC", "D8D0B8", "9A927A"), Eye = new("FF5A5A"), ShoulderR = 6.5f },
        new() { Name = "broodmother", Body = Tones("7E5294", "5A3670", "35204A"), Part = Tones("F0FFA0", "C8E05A", "7E9A2A"), Eye = new("F0FFA0"), TorsoRx = 13, TorsoRy = 11, HeadR = 5 },
        new() { Name = "bio_cannon", Body = Tones("6E9460", "4E6E44", "2E4A2C"), Part = Tones("D8FFC0", "7BE36A", "3FA54E"), Eye = new("C8FFB0") },
        new() { Name = "burrower", Body = Tones("B08858", "8A6640", "5A4028"), Part = Tones("F4F0DC", "D8D0B8", "9A927A"), Eye = new("1A1418"), HeadR = 5.5f },
        new() { Name = "gatebreaker", Body = Tones("94604A", "6E4434", "46281E"), Part = Tones("D2DEEA", "8FA3B8", "5B6B7C"), Eye = new("FFB070"), ShoulderR = 6 },
        new() { Name = "phantom", Body = Tones("E4DAFF", "B8A8E8", "7E6CB8", 0.82f), Part = Tones("9A88D0", "6E5CA8", "4A3C78", 0.9f), Eye = new("6FF0FF"), Legs = false, ShoulderR = 4 },
        new() { Name = "ironhide", Body = Tones("6A7486", "4C5464", "2E3440"), Part = Tones("D8E4EE", "A8B8C8", "6A7A8C"), Eye = new("F2B544"), ShoulderR = 6 },
    };

    // columns: idle 2, walk 4, attack 4, knocked down 4
    const int COLS = 14;
    // the top of the legs (the knees give above it)
    const int LEGS = 36;
    // the body rises for the wind-up and sinks into the slam
    static readonly int[] ATTACK_BOB = { -1, 1, 2, 1 };

    public override void _Ready()
    {
        string Out = "res://art/chars";
        foreach (string A in OS.GetCmdlineUserArgs())
        {
            if (A.StartsWith("--out=", StringComparison.Ordinal))
            {
                Out = A[6..];
            }
        }
        DirAccess.MakeDirRecursiveAbsolute(Out);
        var Info = new Godot.Collections.Dictionary();
        var Chars = new Godot.Collections.Dictionary();
        // rows: right, down, left, up, then the 3/4 diagonals down-right, down-left, up-left, up-right
        string[] Views = { "side", "down", "side", "up", "down", "down", "up", "up" };
        int[] Mirrored = { 2, 5, 6 };
        int[] Turn = { 3, 7, 11 };
        // idle: (bob, step) = (0,0), (1,0); walk: the body dips on each stride: (1,+1), (0,0), (1,-1), (0,0)
        (int Bob, int Step)[] Frames = { (0, 0), (1, 0), (1, 1), (0, 0), (1, -1), (0, 0) };
        foreach (var B in Bosses)
        {
            var Sheet = Image.CreateEmpty((S * COLS), (S * 8), false, Image.Format.Rgba8);
            var Shut = B.EyesShut();
            for (int Row = 0; Row < 8; Row++)
            {
                string View = Views[Row];
                Image Turned(Image Raw, Color? Eye) => ((Row >= 4) ? PixelTurn.TurnRight(Raw, S / 2, Turn, 0, Eye) : Raw);
                var Cells = new List<Image>();
                // idle 0-1, walk 2-5
                foreach (var F in Frames)
                {
                    Cells.Add(Finish(Turned(Draw(B, View, F.Bob, F.Step), B.Eye)));
                }
                // attack 6-9: wind-up (arms up), slam, impact (dust), recover
                for (int P = 1; P <= 4; P++)
                {
                    var Im = Finish(Turned(Draw(B, View, ATTACK_BOB[(P - 1)], 0, P), B.Eye));
                    if (P >= 3)
                    {
                        Dust(Im, View, (P == 3));
                    }
                    Cells.Add(Im);
                }
                // knocked down 10-13: staggers back, knees give, falling, lying still with the eyes shut
                var Stand = Turned(Draw(B, View, 0, 0), B.Eye);
                var Back = new Vector2I(((View == "side") ? -1 : 0), 0);
                Cells.Add(Finish(PixelPoses.Kneel(PixelPoses.Lean(Stand, LEGS, Back), LEGS, 1)));
                Cells.Add(Finish(PixelPoses.Kneel(PixelPoses.Lean(Stand, LEGS, Back), LEGS, 4)));
                Cells.Add(Finish(PixelPoses.Lie(Stand, S, 41), 16));
                Cells.Add(Finish(PixelPoses.Lie(Turned(Draw(Shut, View, 0, 0), null), S, 45), 20));
                for (int Col = 0; Col < COLS; Col++)
                {
                    var Im = Cells[Col];
                    if (Array.IndexOf(Mirrored, Row) >= 0)
                    {
                        Im.FlipX();
                    }
                    Sheet.BlitRect(Im, new Rect2I(0, 0, S, S), new Vector2I((Col * S), (Row * S)));
                }
            }
            string Key = "boss_" + B.Name;
            Sheet.SavePng(Out.PathJoin(Key + ".png"));
            // one-shot animations, as in art/chars/chars.json: [first column, frames, frames per second]
            var Anims = new Godot.Collections.Dictionary { { "attack", new Godot.Collections.Array { 6, 4, 10.0f } }, { "down", new Godot.Collections.Array { 10, 4, 8.0f } } };
            Chars[Key] = new Godot.Collections.Dictionary { { "size", S }, { "body", S }, { "anchor", new Godot.Collections.Array { (S / 2.0f), 46 } }, { "anims", Anims } };
            GD.Print("drew ", Key);
        }
        Info["chars"] = Chars;
        using var Fa = Godot.FileAccess.Open(Out.PathJoin("bosses.json"), Godot.FileAccess.ModeFlags.Write);
        Fa.StoreString(Json.Stringify(Info, "\t"));
        GetTree().Quit();
    }

    // --- one frame -----------------------------------------------------------------
    // Pose: 0 standing / walking; attack 1 wind-up (arms up), 2 slam, 3 impact, 4 recover
    static Image Draw(Boss B, string View, int Bob, int Step, int Pose = 0)
    {
        var Im = Image.CreateEmpty(S, S, false, Image.Format.Rgba8);
        bool Side = (View == "side");
        bool Front = (View == "down");
        int Y = Bob;  // the upper body sinks a pixel on the idle breath
        // legs (or the Phantom's cloak hem)
        if (B.Legs)
        {
            if (Side)
            {
                Leg(Im, (19 - (2 * Step)), B.Body, 0);
                Leg(Im, (25 + (2 * Step)), B.Body, 0);
            }
            else
            {
                Leg(Im, 16, B.Body, ((Step > 0) ? 2 : 0));
                Leg(Im, 27, B.Body, ((Step < 0) ? 2 : 0));
            }
        }
        // back arm on the side view
        if (Side)
        {
            Arm(Im, 17, (22 + Y), B, Front, true, Pose, View);
        }
        // torso
        float Tx = (Side ? 22 : 24);
        if (B.Legs)
        {
            Ell(Im, Tx, (28 + Y), (Side ? (B.TorsoRx - 2) : B.TorsoRx), B.TorsoRy, B.Body);
        }
        else
        {
            Cloak(Im, Tx, Y, Step, B, Side);
        }
        // the parts that sit on the back are drawn under the head and arms
        Back(Im, B, View, Y);
        // shoulders and arms
        if (Side)
        {
            Ell(Im, 26, (21 + Y), B.ShoulderR, (B.ShoulderR - 1), B.Body);
            Arm(Im, 27, (22 + Y), B, Front, false, Pose, View);
        }
        else
        {
            Ell(Im, 13, (21 + Y), B.ShoulderR, (B.ShoulderR - 1), B.Body);
            Ell(Im, 35, (21 + Y), B.ShoulderR, (B.ShoulderR - 1), B.Body);
            Arm(Im, 10, (22 + Y), B, Front, false, Pose, View);
            Arm(Im, 38, (22 + Y), B, Front, true, Pose, View);
        }
        // head, sunk between the shoulders
        // the Phantom's hood sits on its cloak; the brutes lean their head forward
        float Hx = (Side ? (B.Legs ? 31 : 26) : 24);
        float Hy = (14 + Y);
        Ell(Im, Hx, Hy, B.HeadR, (B.HeadR - 1), B.Body);
        if (Front)
        {
            Px(Im, (int)(Hx - 3), (int)Hy, B.Eye, 2);
            Px(Im, (int)(Hx + 2), (int)Hy, B.Eye, 2);
            Fill(Im, (int)(Hx - 2), (int)(Hy + 3), 5, 1, new Color("120A16"));
            Px(Im, (int)(Hx - 1), (int)(Hy + 3), new Color("E8E3C8"));
            Px(Im, (int)(Hx + 1), (int)(Hy + 3), new Color("E8E3C8"));
        }
        else if (Side)
        {
            Px(Im, (int)(Hx + 2), (int)Hy, B.Eye, 2);
            Fill(Im, (int)(Hx + 1), (int)(Hy + 3), 4, 1, new Color("120A16"));
        }
        Front_(Im, B, View, Y, Hx, Hy);
        return Im;
    }

    static void Leg(Image Im, int X, Color[] T, int Lift)
    {
        int Top = 36;
        int Bottom = (45 - Lift);
        Fill(Im, X, Top, 5, (Bottom - Top), T[1]);
        Fill(Im, X, Top, 1, (Bottom - Top), T[0]);
        Fill(Im, (X + 4), Top, 1, (Bottom - Top), T[2]);
        // foot
        Fill(Im, (X - 1), (Bottom - 1), 7, 2, T[2]);
    }

    static void Arm(Image Im, int X, int Y, Boss B, bool Front, bool Right, int Pose, string View)
    {
        var T = (B.Name == "phantom") ? B.Part : B.Body;
        int W = ((B.Name == "phantom") ? 3 : 4);
        if (Pose == 0)
        {
            Fill(Im, X, Y, W, 13, T[1]);
            Fill(Im, X, Y, 1, 13, T[0]);
            Fill(Im, ((X + W) - 1), Y, 1, 13, T[2]);
            Fist(Im, (X + (W / 2.0f)), (Y + 14), B, Right);
            return;
        }
        // attacking: the arm goes from the shoulder to where the fist is in this pose
        var Sh = new Vector2((X + (W / 2.0f)), Y);
        float In = ((X < (S / 2)) ? 1 : -1);
        Vector2 To;
        if (View == "side")
        {
            To = Pose switch { 1 => new Vector2(-3, -13), 2 => new Vector2(12, 6), 3 => new Vector2(13, 18), _ => new Vector2(9, 13) };
        }
        else if (View == "down")
        {
            To = Pose switch { 1 => new Vector2(-In, -13), 2 => new Vector2((In * 4), 12), 3 => new Vector2((In * 5), 18), _ => new Vector2((In * 3), 15) };
        }
        else
        {
            // seen from the back, reaching forward is reaching away from the camera (up the screen)
            To = Pose switch { 1 => new Vector2(-In, -13), 2 => new Vector2((In * 3), -6), 3 => new Vector2((In * 3), -2), _ => new Vector2((In * 2), 4) };
        }
        var H = (Sh + To);
        var Pp = (H - Sh).Normalized().Orthogonal();
        // the lit edge faces the upper left
        if (Pp.Dot(new Vector2(-0.6f, -0.8f)) < 0)
        {
            Pp = -Pp;
        }
        int N = Mathf.CeilToInt((Sh.DistanceTo(H) * 2.0f));
        for (int I = 0; I <= N; I++)
        {
            var P = Sh.Lerp(H, (I / (float)N));
            Fill(Im, (int)(P.X - (W / 2.0f)), (int)(P.Y - (W / 2.0f)), W, W, T[1]);
        }
        for (int I = 0; I <= N; I++)
        {
            var P = Sh.Lerp(H, (I / (float)N));
            Px(Im, (int)(P.X + (Pp.X * ((W / 2.0f) - 0.5f))), (int)(P.Y + (Pp.Y * ((W / 2.0f) - 0.5f))), T[0]);
            Px(Im, (int)(P.X - (Pp.X * ((W / 2.0f) - 0.5f))), (int)(P.Y - (Pp.Y * ((W / 2.0f) - 0.5f))), T[2]);
        }
        Fist(Im, H.X, H.Y, B, Right);
    }

    // the end of an arm: fist, claws, ram or ghost fingers
    static void Fist(Image Im, float Fx, float Fy, Boss B, bool Right)
    {
        var T = (B.Name == "phantom") ? B.Part : B.Body;
        switch (B.Name)
        {
            case "burrower":
                // digging claws: three bone hooks
                Ell(Im, Fx, Fy, 3.5f, 2.5f, B.Body);
                for (int K = -1; K <= 1; K++)
                {
                    Fill(Im, (int)(Fx + (K * 2)), (int)(Fy + 2), 1, 3, B.Part[1]);
                    Px(Im, (int)(Fx + (K * 2)), (int)(Fy + 5), B.Part[2]);
                }
                break;
            case "gatebreaker" when Right:
                // the ram: a steel block on the right fist
                Ell(Im, Fx, Fy, 5.5f, 4.5f, B.Part);
                Fill(Im, (int)(Fx - 4), (int)(Fy - 1), 9, 1, B.Part[2]);
                Px(Im, (int)(Fx - 2), (int)(Fy - 3), new Color("D9534A"), 2);
                break;
            case "phantom":
                // long ghostly fingers
                Ell(Im, Fx, Fy, 2, 2, B.Part);
                for (int K = -1; K <= 1; K++)
                {
                    Fill(Im, (int)(Fx + K), (int)(Fy + 1), 1, 3, B.Part[1]);
                }
                break;
            default:
                Ell(Im, Fx, Fy, 3, 3, T);
                break;
        }
    }

    // The Phantom has no legs: a hooded cloak that narrows to a ragged hem which sways when it moves.
    static void Cloak(Image Im, float X, int Y, int Step, Boss B, bool Side)
    {
        for (int Row = (18 + Y); Row <= 43; Row++)
        {
            float K = ((Row - (18 + Y)) / 25.0f);
            float Half = ((Side ? 8.0f : 11.0f) - (K * 3.0f));
            float Sway = (Step * K * 2.0f);
            for (int Col = (int)((X - Half) + Sway); Col <= (int)((X + Half) + Sway); Col++)
            {
                bool Rag = ((Row > 39) && (((Col + Row) % 3) == 0));
                if (Rag)
                {
                    continue;
                }
                float U = ((Col - (X + Sway)) / Half);
                Color C = (U < -0.5f) ? B.Body[0] : ((U > 0.45f) ? B.Body[2] : B.Body[1]);
                Px(Im, Col, Row, C);
            }
        }
    }

    // Parts on the back (drawn before the arms and head).
    static void Back(Image Im, Boss B, string View, int Y)
    {
        switch (B.Name)
        {
            case "broodmother":
                // egg sacs on the abdomen (more of them on the back)
                if (View == "up")
                {
                    foreach (var (X, Ey) in new[] { (17, 26), (24, 24), (31, 26), (20, 32), (28, 32) })
                    {
                        Ell(Im, X, (Ey + Y), 3, 3, B.Part);
                    }
                }
                else if (View == "down")
                {
                    foreach (var (X, Ey) in new[] { (18, 32), (24, 34), (30, 32) })
                    {
                        Ell(Im, X, (Ey + Y), 3, 2.5f, B.Part);
                    }
                }
                else
                {
                    Ell(Im, 16, (30 + Y), 4, 4, B.Part);
                    Ell(Im, 20, (34 + Y), 3, 3, B.Part);
                }
                break;
            case "bio_cannon":
                // spore pods on the shoulders and a row of tubes down the back
                if (View == "side")
                {
                    Ell(Im, 18, (17 + Y), 4, 4, B.Part);
                    Ell(Im, 15, (24 + Y), 3, 3, B.Part);
                }
                else
                {
                    Ell(Im, 13, (15 + Y), 4, 4, B.Part);
                    Ell(Im, 35, (15 + Y), 4, 4, B.Part);
                    if (View == "up")
                    {
                        for (int K = 0; K < 3; K++)
                        {
                            Ell(Im, 24, ((22 + (K * 5)) + Y), 2.5f, 2, B.Part);
                        }
                    }
                }
                break;
            case "juggernaut":
                // bone plates on the shoulders and spine
                if (View != "side")
                {
                    Ell(Im, 13, (18 + Y), 6, 3, B.Part);
                    Ell(Im, 35, (18 + Y), 6, 3, B.Part);
                }
                if (View == "up")
                {
                    for (int K = 0; K < 4; K++)
                    {
                        Fill(Im, 23, ((21 + (K * 4)) + Y), 3, 2, B.Part[1]);
                    }
                }
                else if (View == "side")
                {
                    Ell(Im, 22, (18 + Y), 6, 3, B.Part);
                }
                break;
        }
    }

    // Parts in front (drawn last).
    static void Front_(Image Im, Boss B, string View, int Y, float Hx, float Hy)
    {
        switch (B.Name)
        {
            case "juggernaut":
                // horns for the charge
                if (View == "side")
                {
                    Fill(Im, (int)(Hx + 3), (int)(Hy - 4), 4, 1, B.Part[1]);
                    Fill(Im, (int)(Hx + 6), (int)(Hy - 6), 1, 3, B.Part[0]);
                }
                else
                {
                    foreach (int Dir in new[] { -1, 1 })
                    {
                        Fill(Im, (int)(Hx + (Dir * 5)), (int)(Hy - 3), 1, 2, B.Part[1]);
                        Fill(Im, (int)(Hx + (Dir * 6)), (int)(Hy - 6), 1, 4, B.Part[0]);
                        Px(Im, (int)(Hx + (Dir * 7)), (int)(Hy - 7), B.Part[0]);
                    }
                }
                break;
            case "bio_cannon":
                // the cannon maw in the chest
                if (View == "down")
                {
                    Ell(Im, 24, (27 + Y), 4, 4, Tones("2E4A2C", "1A2A18", "0E160C"));
                    Ell(Im, 24, (27 + Y), 2, 2, B.Part);
                }
                else if (View == "side")
                {
                    Fill(Im, 28, (25 + Y), 7, 5, B.Body[2]);
                    Fill(Im, 34, (26 + Y), 2, 3, B.Part[1]);
                }
                break;
            case "burrower":
                // a pink snout for digging
                if (View == "down")
                {
                    Ell(Im, Hx, (Hy + 2), 3, 2, Tones("F0B8A0", "D98F7A", "A86452"));
                }
                else if (View == "side")
                {
                    Ell(Im, (Hx + 5), (Hy + 1), 3, 2, Tones("F0B8A0", "D98F7A", "A86452"));
                }
                break;
            case "gatebreaker":
                // a steel helm with a red crest (every view)
                for (int Col = (int)(Hx - 6); Col <= (int)(Hx + 6); Col++)
                {
                    for (int Row = (int)(Hy - 6); Row <= (int)(Hy - 1); Row++)
                    {
                        float Dx = ((Col - Hx) / 6.5f);
                        float Dy = ((Row - Hy) / 6.0f);
                        if (((Dx * Dx) + (Dy * Dy)) <= 1.0f)
                        {
                            Px(Im, Col, Row, ((Dx < -0.4f) ? B.Part[0] : ((Dx > 0.4f) ? B.Part[2] : B.Part[1])));
                        }
                    }
                }
                Fill(Im, (int)(Hx - 1), (int)(Hy - 8), 2, 3, new Color("D9534A"));
                break;
            case "phantom":
                // a deep hood; only the eyes glow out of it
                for (int Col = (int)(Hx - 7); Col <= (int)(Hx + 7); Col++)
                {
                    for (int Row = (int)(Hy - 7); Row <= (int)(Hy + 4); Row++)
                    {
                        float Dx = ((Col - Hx) / 7.0f);
                        float Dy = ((Row - (Hy - 1)) / 6.5f);
                        float D = ((Dx * Dx) + (Dy * Dy));
                        if ((D <= 1.0f) && (D > 0.42f || View == "up"))
                        {
                            Px(Im, Col, Row, ((Dx < -0.3f) ? B.Part[0] : ((Dx > 0.4f) ? B.Part[2] : B.Part[1])));
                        }
                        else if (D <= 0.42f)
                        {
                            Px(Im, Col, Row, new Color("1A1030"));
                        }
                    }
                }
                if (View == "down")
                {
                    Px(Im, (int)(Hx - 2), (int)Hy, B.Eye, 2);
                    Px(Im, (int)(Hx + 1), (int)Hy, B.Eye, 2);
                }
                else if (View == "side")
                {
                    Px(Im, (int)(Hx + 2), (int)Hy, B.Eye, 2);
                }
                break;
            case "ironhide":
                // thick front plates and pauldrons; the back stays bare (its weak side)
                if (View == "down")
                {
                    Ell(Im, 24, (28 + Y), 9, 8, B.Part);
                    Fill(Im, 16, (28 + Y), 17, 1, B.Part[2]);
                    foreach (int X in new[] { 18, 24, 30 })
                    {
                        Px(Im, X, (24 + Y), new Color("F2B544"));
                        Px(Im, X, (32 + Y), new Color("F2B544"));
                    }
                }
                else if (View == "side")
                {
                    Fill(Im, 27, (19 + Y), 5, 18, B.Part[1]);
                    Fill(Im, 27, (19 + Y), 1, 18, B.Part[0]);
                    Fill(Im, 31, (19 + Y), 1, 18, B.Part[2]);
                }
                if (View != "up")
                {
                    Ell(Im, ((View == "side") ? 26 : 13), (19 + Y), 6, 3.5f, B.Part);
                    if (View != "side")
                    {
                        Ell(Im, 35, (19 + Y), 6, 3.5f, B.Part);
                    }
                }
                break;
        }
    }

    // --- drawing helpers ----------------------------------------------------------
    // A shaded ellipse: light on the upper left, dark on the lower right.
    static void Ell(Image Im, float Cx, float Cy, float Rx, float Ry, Color[] T)
    {
        for (int Y = (int)Math.Floor(Cy - Ry); Y <= (int)Math.Ceiling(Cy + Ry); Y++)
        {
            for (int X = (int)Math.Floor(Cx - Rx); X <= (int)Math.Ceiling(Cx + Rx); X++)
            {
                float Dx = (((X + 0.5f) - Cx) / Rx);
                float Dy = (((Y + 0.5f) - Cy) / Ry);
                if (((Dx * Dx) + (Dy * Dy)) > 1.0f)
                {
                    continue;
                }
                float Lit = ((-0.6f * Dx) - (0.8f * Dy));
                Px(Im, X, Y, ((Lit > 0.45f) ? T[0] : ((Lit < -0.35f) ? T[2] : T[1])));
            }
        }
    }

    static void Fill(Image Im, int X, int Y, int W, int H, Color C)
    {
        for (int Row = Y; Row < (Y + H); Row++)
        {
            for (int Col = X; Col < (X + W); Col++)
            {
                Px(Im, Col, Row, C);
            }
        }
    }

    static void Px(Image Im, int X, int Y, Color C, int W = 1)
    {
        for (int K = 0; K < W; K++)
        {
            if (((X + K) >= 1) && (Y >= 1) && ((X + K) < (S - 1)) && (Y < (S - 1)))
            {
                Im.SetPixel((X + K), Y, C);
            }
        }
    }

    // Dark outline around the silhouette, then a soft ground shadow under the feet (as tools/PixelChars.cs).
    // ground dust in front of a slam (none seen from the back: the body hides it)
    static void Dust(Image Im, string View, bool Fresh)
    {
        if (View == "up")
        {
            return;
        }
        var C = ((View == "side") ? new Vector2(41, 44) : new Vector2(24, 46));
        float Rx = (Fresh ? 9.0f : 13.0f);
        var Col = new Color(0.80f, 0.74f, 0.62f, (Fresh ? 0.85f : 0.45f));
        for (int I = 0; I < 18; I++)
        {
            float A = ((I / 18.0f) * Mathf.Tau);
            int X = (int)(C.X + (Mathf.Cos(A) * Rx));
            int Y = (int)(C.Y + (Mathf.Sin(A) * Rx * 0.3f));
            if ((X >= 0) && (Y >= 0) && (X < S) && (Y < S) && (Im.GetPixel(X, Y).A < 1))
            {
                Im.SetPixel(X, Y, Col);
            }
        }
        if (Fresh)
        {
            foreach (var D in new[] { new Vector2(1, 0.2f), new Vector2(-1, 0.25f), new Vector2(0.4f, 0.4f) })
            {
                for (int K = 0; K < 5; K++)
                {
                    int X = (int)(C.X + (D.X * K)), Y = (int)(C.Y + (D.Y * K));
                    if ((X >= 0) && (Y >= 0) && (X < S) && (Y < S) && (Im.GetPixel(X, Y).A < 1))
                    {
                        Im.SetPixel(X, Y, new Color(0.08f, 0.06f, 0.1f, 0.75f));
                    }
                }
            }
        }
    }

    static Image Finish(Image Src, float Rx = 13.0f)
    {
        var Im = (Image)Src.Duplicate();
        for (int Y = 0; Y < S; Y++)
        {
            for (int X = 0; X < S; X++)
            {
                if (Src.GetPixel(X, Y).A > 0)
                {
                    continue;
                }
                foreach (var D in new[] { new Vector2I(1, 0), new Vector2I(-1, 0), new Vector2I(0, 1), new Vector2I(0, -1) })
                {
                    var Q = new Vector2I(X, Y) + D;
                    if (Q.X >= 0 && Q.Y >= 0 && Q.X < S && Q.Y < S && Src.GetPixel(Q.X, Q.Y).A > 0)
                    {
                        Im.SetPixel(X, Y, Ink);
                        break;
                    }
                }
            }
        }
        for (int Y = 43; Y < S; Y++)
        {
            for (int X = 0; X < S; X++)
            {
                float Dx = (((X + 0.5f) - (S / 2.0f)) / Rx);
                float Dy = (((Y + 0.5f) - 46.0f) / 2.4f);
                if ((((Dx * Dx) + (Dy * Dy)) <= 1.0f) && (Im.GetPixel(X, Y).A == 0))
                {
                    Im.SetPixel(X, Y, Shade);
                }
            }
        }
        return Im;
    }
}
