using Godot;
using Environment = Godot.Environment;
using FileAccess = Godot.FileAccess;
using System;
using System.Threading.Tasks;
using HoldTilDawn;

public partial class PixelProps : Node
{
    // Pixel-art props for the base (GDD 4.3 / 5.1 / 6): the old lighthouse that carries the beacon, the workbench,
    // the defence tower (AGT-12) standing and wrecked, and the helicopter landing pad (FML-13).
    // Same rules as the characters: 1 px = 1 game px, GDD palette, light from the upper left, 1 px dark outline.
    // Not part of the game. Run: godot --headless --path . tools/PixelProps.tscn
    // Writes art/props/<name>.png and art/props/props.json ({name: [anchor_x, anchor_y]}, the pixel on the ground point).
    // The PNGs are plain pixel art and can be repainted by hand.
    public static readonly dynamic INK = Gd.MakeColor("141019");
    public static readonly dynamic SHADOW = Gd.MakeColor(0.04f, 0.06f, 0.1f, 0.38f);
    public static readonly dynamic CONCRETE = new GArray(Gd.MakeColor("A3A8B0"), Gd.MakeColor("8A8F98"), Gd.MakeColor("6A6F7C"));
    public static readonly dynamic STEEL = new GArray(Gd.MakeColor("B4C4D4"), Gd.MakeColor("8FA3B8"), Gd.MakeColor("5B6B7C"));
    public static readonly dynamic RED = new GArray(Gd.MakeColor("D9534A"), Gd.MakeColor("B8322E"), Gd.MakeColor("7E2320"));
    public static readonly dynamic WHITE = new GArray(Gd.MakeColor("FFFFFF"), Gd.MakeColor("E8E3D8"), Gd.MakeColor("B9B4AA"));
    public static readonly dynamic WOOD = new GArray(Gd.MakeColor("C08A50"), Gd.MakeColor("A0703C"), Gd.MakeColor("7A5634"), Gd.MakeColor("5A3E26"));
    public static readonly dynamic AMBER = Gd.MakeColor("F2B544");
    public static readonly dynamic GLOW = Gd.MakeColor("FFF1B8");
    public static readonly dynamic DARK = Gd.MakeColor("2B2F36");
    public dynamic Props = new GDict {  };
    public override void _Ready()
    {
        dynamic Out = "res://art/props";
        foreach (string outputArgument in OS.GetCmdlineUserArgs())
            if (outputArgument.StartsWith("--out=", StringComparison.Ordinal)) Out = outputArgument[6..];
        Gd.Static(typeof(DirAccess), "make_dir_recursive_absolute", Out);
        this.Save(Out, "lighthouse", this.Lighthouse(), Gd.MakeVector2(14, (73 + LH_EXTRA)));
        this.Save(Out, "workbench", this.Workbench(), Gd.MakeVector2(31, 45));
        // the shared storage chest: 4 frames side by side, shut to wide open (src/world/view/Main.BaseProps.cs)
        this.Save(Out, "chest", this.ChestSheet(), Gd.MakeVector2(18, 35));
        this.Save(Out, "tower", this.Tower(false), Gd.MakeVector2(13, (57 + TOWER_EXTRA)));
        this.Save(Out, "tower_wreck", this.Tower(true), Gd.MakeVector2(13, (57 + TOWER_EXTRA)));
        this.Save(Out, "pad", this.Pad(), Gd.MakeVector2(128, 128));
        // built walls (ITM-07, ITM-11): 3 x 1 tiles, anchor = centre of the footprint
        foreach (bool Stone in new[] { false, true })
        {
            string Kind = (Stone ? "stone" : "wood");
            this.Save(Out, ("wall_" + Kind + "_h"), this.Wall(Stone, false), Gd.MakeVector2(49, 31));
            this.Save(Out, ("wall_" + Kind + "_v"), this.Wall(Stone, true), Gd.MakeVector2(17, 63));
        }
        dynamic Fa = Gd.Static(typeof(FileAccess), "open", Gd.PathJoin(Out, "props.json"), Gd.EnumValue(typeof(FileAccess), "WRITE", 2));
        Fa.StoreString(Json.Stringify(Gd.ToVariant(Props), "\t"));
        Fa.Close();
        GetTree().Quit();
    }

    public void Save(dynamic Out, dynamic N, dynamic Im, dynamic Anchor)
    {
        Im.SavePng(Gd.PathJoin(Out, (N + ".png")));
        Props[N] = new GArray(Anchor.X, Anchor.Y);
        GD.Print("drew ", N, " ", Im.GetSize());
    }

    // --- the props ---------------------------------------------------------------
    // extra pixels of tower body: tall enough to stand well above the base's roof, so it is seen from outside
    public const int LH_EXTRA = 36;

    public dynamic Lighthouse()
    {
        // The old lighthouse at the heart of Lighthouse Outpost; its lamp is the beacon (GDD 5.1).
        int D = LH_EXTRA;
        dynamic Im = Gd.Static(typeof(Image), "create_empty", 28, (76 + D), false, Gd.EnumValue(typeof(Image), "FORMAT_RGBA8", 5));
        this.Shadow(Im, Gd.MakeVector2(19, (72 + D)), 12, 3);
        // plinth
        this.Block(Im, Gd.MakeRect2I(2, (66 + D), 24, 8), CONCRETE);
        foreach (dynamic Y in Gd.Iter(Gd.Range(22, (66 + D))))
        {
            dynamic Half = (6 + Gd.Int(((3.0f * (Y - 22)) / (43.0f + D))));
            dynamic Band = (((((65 + D - Y) / 9) % 2) == 0) ? (dynamic)(RED) : WHITE);
            foreach (dynamic X in Gd.Iter(Gd.Range((14 - Half), (14 + Half))))
            {
                Color C = (Color)(Gd.At(Band, 1));
                if ((X < ((14 - Half) + 2)))
                {
                    C = Gd.At(Band, 0);
                }
                else if ((X >= ((14 + Half) - 3)))
                {
                    C = Gd.At(Band, 2);
                }
                Im.SetPixel(X, Y, C);
            }
        }
        // door
        Im.FillRect(Gd.MakeRect2I(12, (58 + D), 4, 8), Gd.MakeColor("2B2024"));
        // windows up the stair
        foreach (int Wy in new[] { 34, (34 + (D / 2) + 6), (40 + D) })
        {
            Im.FillRect(Gd.MakeRect2I(13, Wy, 2, 3), Gd.MakeColor("2B2024"));
        }
        // gallery rail
        Im.FillRect(Gd.MakeRect2I(4, 18, 20, 1), Gd.MakeColor("5B5F6B"));
        Im.FillRect(Gd.MakeRect2I(4, 21, 20, 1), Gd.MakeColor("3A3F48"));
        foreach (dynamic X in Gd.Iter(Gd.Range(4, 24, 3)))
        {
            Im.FillRect(Gd.MakeRect2I(X, 19, 1, 2), Gd.MakeColor("3A3F48"));
        }
        // lamp room
        Im.FillRect(Gd.MakeRect2I(9, 9, 10, 9), AMBER);
        Im.FillRect(Gd.MakeRect2I(12, 11, 4, 4), GLOW);
        foreach (dynamic X in Gd.Iter(new GArray(9, 18)))
        {
            Im.FillRect(Gd.MakeRect2I(X, 9, 1, 9), Gd.MakeColor("3A3F48"));
        }
        foreach (dynamic R in Gd.Iter(5))
        {
            Im.FillRect(Gd.MakeRect2I((8 + R), (8 - R), (12 - (2 * R)), 1), ((R > 0) ? (dynamic)(Gd.At(RED, 1)) : Gd.At(RED, 2)));
        }
        Im.FillRect(Gd.MakeRect2I(9, 7, 2, 1), Gd.At(RED, 0));
        Im.FillRect(Gd.MakeRect2I(13, 2, 2, 2), Gd.MakeColor("3A3F48"));
        this.Outline(Im);
        return Im;
    }

    // small typed helper for the furniture below
    static void Fill(Image Im, int X, int Y, int W, int H, Color C)
    {
        Im.FillRect(new Rect2I(X, Y, W, H), C);
    }

    public Image Workbench()
    {
        // The team's workbench (crafting, MEC-10), about 2 tiles wide: a pegboard of hanging tools at the back, a deep
        // plank top with a vise, a blueprint, a hammer and a red tool box, a front apron with two drawers, and logs
        // and metal bars on the low shelf. Stands against the base's north wall.
        var Im = Image.CreateEmpty(62, 48, false, Image.Format.Rgba8);
        Color W0 = WOOD[0], W1 = WOOD[1], W2 = WOOD[2], W3 = WOOD[3];
        Color S0 = STEEL[0], S1 = STEEL[1], S2 = STEEL[2];
        this.Shadow(Im, Gd.MakeVector2(34, 45), 29, 3);
        // pegboard with its posts
        Fill(Im, 4, 2, 54, 19, W2);
        Fill(Im, 4, 2, 54, 1, W1);
        for (int Y = 5; Y < 20; Y += 4)
        {
            for (int X = 8 + ((Y / 4) % 2) * 2; X < 56; X += 4)
            {
                Im.SetPixel(X, Y, W3);
            }
        }
        Fill(Im, 3, 1, 3, 24, W3);
        Fill(Im, 56, 1, 3, 24, W3);
        Fill(Im, 3, 1, 1, 24, W2);
        // hanging tools: wrench, saw, pliers, a coil of rope
        Fill(Im, 10, 5, 2, 11, S1);
        Fill(Im, 9, 4, 4, 2, S1);
        Fill(Im, 10, 4, 2, 1, W2);
        Fill(Im, 9, 15, 4, 2, S1);
        Fill(Im, 11, 15, 1, 1, W2);
        Fill(Im, 17, 6, 12, 5, S0);
        Fill(Im, 17, 10, 12, 1, S2);
        for (int X = 17; X < 29; X += 2)
        {
            Im.SetPixel(X, 11, S2);
        }
        Fill(Im, 29, 6, 4, 5, Gd.At(RED, 1));
        Fill(Im, 30, 7, 2, 3, Gd.At(RED, 2));
        Fill(Im, 37, 5, 1, 9, S1);
        Fill(Im, 40, 5, 1, 9, S1);
        Fill(Im, 37, 13, 1, 4, Gd.At(RED, 1));
        Fill(Im, 40, 13, 1, 4, Gd.At(RED, 1));
        Fill(Im, 38, 8, 2, 2, S2);
        Color Rope = new Color("D8C08A"), RopeD = new Color("A88F5A");
        Fill(Im, 45, 6, 8, 8, Rope);
        Fill(Im, 47, 8, 4, 4, W2);
        Fill(Im, 45, 12, 8, 2, RopeD);
        Fill(Im, 48, 4, 2, 2, W3);
        // the top: planks running along the bench
        Fill(Im, 1, 21, 60, 10, W1);
        Fill(Im, 1, 21, 60, 1, W0);
        Fill(Im, 1, 24, 60, 1, W2);
        Fill(Im, 1, 28, 60, 1, W2);
        Fill(Im, 1, 21, 1, 10, W0);
        Fill(Im, 60, 21, 1, 10, W2);
        // apron with two drawers
        Fill(Im, 1, 31, 60, 6, W2);
        Fill(Im, 1, 31, 60, 1, W3);
        foreach (int X in new[] { 8, 37 })
        {
            Fill(Im, X, 32, 17, 4, W1);
            Fill(Im, X, 35, 17, 1, W3);
            Fill(Im, (X + 7), 33, 3, 1, S0);
        }
        // legs, a low shelf and what is on it
        Fill(Im, 3, 37, 4, 9, W3);
        Fill(Im, 55, 37, 4, 9, W3);
        Fill(Im, 3, 37, 1, 9, W2);
        Fill(Im, 7, 42, 48, 2, W3);
        foreach (int X in new[] { 11, 16, 13 })
        {
            int Y = ((X == 13) ? 37 : 39);
            Fill(Im, X, Y, 6, 3, W1);
            Fill(Im, X, Y, 2, 3, new Color("E0B880"));
            Im.SetPixel(X, (Y + 1), W2);
        }
        Fill(Im, 38, 39, 7, 3, S1);
        Fill(Im, 38, 39, 7, 1, S0);
        Fill(Im, 40, 37, 7, 2, S0);
        Fill(Im, 40, 38, 7, 1, S1);
        // vise at the left end, sticking up
        Fill(Im, 4, 16, 9, 6, S2);
        Fill(Im, 4, 16, 9, 1, S1);
        Fill(Im, 5, 15, 3, 1, S0);
        Fill(Im, 13, 18, 4, 1, S1);
        Fill(Im, 8, 22, 3, 3, S2);
        // blueprint
        Color Paper = new Color("E8E3D8"), Ink = new Color("4F7FC0");
        Fill(Im, 17, 22, 15, 6, Paper);
        Fill(Im, 17, 27, 15, 1, new Color("B9B4AA"));
        Fill(Im, 19, 23, 6, 1, Ink);
        Fill(Im, 19, 23, 1, 3, Ink);
        Fill(Im, 22, 25, 8, 1, Ink);
        Fill(Im, 27, 23, 1, 3, Ink);
        // hammer lying on the top
        Fill(Im, 34, 26, 8, 1, W3);
        Fill(Im, 42, 24, 2, 4, S1);
        Fill(Im, 42, 24, 2, 1, S0);
        // tool box at the right end
        Fill(Im, 46, 15, 11, 7, Gd.At(RED, 1));
        Fill(Im, 46, 15, 11, 1, Gd.At(RED, 0));
        Fill(Im, 46, 19, 11, 1, Gd.At(RED, 2));
        Fill(Im, 50, 13, 3, 1, DARK);
        Fill(Im, 49, 14, 1, 1, DARK);
        Fill(Im, 53, 14, 1, 1, DARK);
        Fill(Im, 51, 17, 1, 1, AMBER);
        this.Outline(Im);
        return Im;
    }

    public Image ChestSheet()
    {
        // The shared storage chest (BAL-18): a banded wooden chest with a brass lock. Frame 0 shut, 1 the lid lifting,
        // 2 half open (the underside of the lid shows), 3 wide open with the team's materials inside.
        const int Fw = 36, Fh = 37;
        var Sheet = Image.CreateEmpty((Fw * 4), Fh, false, Image.Format.Rgba8);
        for (int F = 0; F < 4; F++)
        {
            var Im = this.Chest(F, Fw, Fh);
            Sheet.BlitRect(Im, new Rect2I(0, 0, Fw, Fh), new Vector2I((F * Fw), 0));
        }
        return Sheet;
    }

    public Image Chest(int F, int Fw, int Fh)
    {
        var Im = Image.CreateEmpty(Fw, Fh, false, Image.Format.Rgba8);
        Color W0 = WOOD[0], W1 = WOOD[1], W2 = WOOD[2], W3 = WOOD[3];
        Color S0 = STEEL[0], S1 = STEEL[1], S2 = STEEL[2];
        Color Brass = AMBER, BrassD = new Color("B8862E");
        Color Hollow = new Color("2B2024");
        this.Shadow(Im, Gd.MakeVector2(20, 34), 16, 3);
        const int L = 3, R = 33;
        int Wd = (R - L);
        // body: front face 22..34 with planks, steel bands and corner brackets
        Fill(Im, L, 22, Wd, 12, W2);
        Fill(Im, L, 22, Wd, 1, W1);
        Fill(Im, L, 27, Wd, 1, W3);
        Fill(Im, L, 31, Wd, 1, W3);
        Fill(Im, L, 22, 1, 12, W1);
        foreach (int X in new[] { 7, 28 })
        {
            Fill(Im, X, 22, 2, 12, S1);
            Fill(Im, X, 22, 1, 12, S0);
        }
        Fill(Im, L, 32, 3, 2, S2);
        Fill(Im, (R - 3), 32, 3, 2, S2);
        // feet
        Fill(Im, 4, 34, 4, 1, W3);
        Fill(Im, 28, 34, 4, 1, W3);
        // brass catch on the body
        Fill(Im, 16, 22, 4, 3, BrassD);
        Fill(Im, 17, 23, 2, 1, Brass);
        if (F == 0)
        {
            // shut: the lid's top face and its front face with the lock plate
            Fill(Im, L, 11, Wd, 6, W1);
            Fill(Im, L, 11, Wd, 1, W0);
            Fill(Im, L, 14, Wd, 1, W2);
            Fill(Im, L, 17, Wd, 5, W2);
            Fill(Im, L, 17, Wd, 1, W0);
            Fill(Im, L, 21, Wd, 1, W3);
            foreach (int X in new[] { 7, 28 })
            {
                Fill(Im, X, 11, 2, 11, S1);
                Fill(Im, X, 11, 1, 11, S0);
            }
            Fill(Im, 15, 18, 6, 6, Brass);
            Fill(Im, 15, 23, 6, 1, BrassD);
            Fill(Im, 17, 20, 2, 2, Hollow);
        }
        else
        {
            // open: the hollow inside, back wall and side walls of the body around it
            Fill(Im, L, 15, Wd, 7, Hollow);
            Fill(Im, L, 15, Wd, 1, W3);
            Fill(Im, L, 15, 2, 7, W2);
            Fill(Im, (R - 2), 15, 2, 7, W3);
            Fill(Im, L, 21, Wd, 1, W1);
            if (F >= 2)
            {
                // the team's materials: a log, stones, herbs, metal bars; lit warm when wide open
                bool Lit = (F == 3);
                Fill(Im, 7, 18, 7, 3, (Lit ? W0 : W1));
                Fill(Im, 7, 18, 2, 3, new Color("E0B880"));
                Fill(Im, 15, 17, 4, 4, (Lit ? CONCRETE[0] : CONCRETE[1]));
                Fill(Im, 15, 17, 4, 1, CONCRETE[0]);
                Fill(Im, 18, 19, 2, 2, CONCRETE[2]);
                Fill(Im, 21, 16, 1, 4, new Color("3E8E4A"));
                Fill(Im, 22, 17, 2, 1, new Color("6CC070"));
                Fill(Im, 20, 17, 1, 1, new Color("6CC070"));
                Fill(Im, 24, 19, 6, 2, (Lit ? S0 : S1));
                Fill(Im, 25, 18, 5, 1, S0);
                if (Lit)
                {
                    Fill(Im, 6, 20, 24, 1, new Color(1.0f, 0.82f, 0.45f, 1.0f).Lerp(Hollow, 0.55f));
                }
            }
            else
            {
                // just lifting: a glint of what is inside
                Fill(Im, 12, 19, 10, 1, BrassD);
            }
            // the lid, hinged at the back edge (row 15)
            if (F == 1)
            {
                // tilted up a little: its front face, raised, over a sliver of the inside
                Fill(Im, L, 9, Wd, 5, W1);
                Fill(Im, L, 9, Wd, 1, W0);
                Fill(Im, L, 14, Wd, 4, W2);
                Fill(Im, L, 14, Wd, 1, W0);
                Fill(Im, L, 17, Wd, 1, W3);
                foreach (int X in new[] { 7, 28 })
                {
                    Fill(Im, X, 9, 2, 9, S1);
                    Fill(Im, X, 9, 1, 9, S0);
                }
                Fill(Im, 15, 15, 6, 4, Brass);
                Fill(Im, 15, 18, 6, 1, BrassD);
            }
            else
            {
                // standing up (2) or leaning back (3): its underside faces you
                int Top = ((F == 2) ? 6 : 2);
                int Edge = ((F == 2) ? 3 : 2);
                Fill(Im, L, Top, Wd, Edge, W1);
                Fill(Im, L, Top, Wd, 1, W0);
                Fill(Im, L, (Top + Edge), Wd, (15 - Top - Edge), W3);
                Fill(Im, (L + 2), (Top + Edge + 1), (Wd - 4), (15 - Top - Edge - 2), W2);
                for (int Y = (Top + Edge + 3); Y < 14; Y += 3)
                {
                    Fill(Im, (L + 2), Y, (Wd - 4), 1, W3);
                }
                foreach (int X in new[] { 7, 28 })
                {
                    Fill(Im, X, Top, 2, (15 - Top), S2);
                }
                Fill(Im, 16, Top, 4, 2, BrassD);
            }
        }
        this.Outline(Im);
        return Im;
    }

    // extra pixels of tower mast (team change: taller towers); Main.TOWER_EXTRA must match
    public const int TOWER_EXTRA = 18;

    public dynamic Tower(dynamic Wreck)
    {
        // Defence tower (AGT-12): concrete footing, steel lattice mast, platform, floodlight (amber) and a small gun.
        int D = TOWER_EXTRA;
        dynamic Im = Gd.Static(typeof(Image), "create_empty", 26, (60 + D), false, Gd.EnumValue(typeof(Image), "FORMAT_RGBA8", 5));
        this.Shadow(Im, Gd.MakeVector2(17, (56 + D)), 11, 3);
        this.Block(Im, Gd.MakeRect2I(3, (50 + D), 20, 8), CONCRETE);
        dynamic Top = (Gd.Truth(Wreck) ? (dynamic)((32 + D)) : 20);
        foreach (dynamic Y in Gd.Iter(Gd.Range(Top, (50 + D))))
        {
            dynamic TimeLeft = (Gd.Float((Y - 20)) / (30.0f + D));
            dynamic L = Gd.Int(Gd.Lerpf(9, 6, TimeLeft));
            dynamic R = Gd.Int(Gd.Lerpf(16, 19, TimeLeft));
            Im.FillRect(Gd.MakeRect2I(L, Y, 2, 1), Gd.At(STEEL, 1));
            Im.FillRect(Gd.MakeRect2I(R, Y, 2, 1), Gd.At(STEEL, 2));
            dynamic K = ((Y - 20) % 10);
            Im.SetPixel(((L + 2) + Gd.Int(((((R - L) - 2) * K) / 10.0f))), Y, Gd.At(STEEL, 2));
            Im.SetPixel(((R - 1) - Gd.Int(((((R - L) - 2) * K) / 10.0f))), Y, Gd.At(STEEL, 2));
        }
        if (Gd.Truth(Wreck))
        {
            Im.FillRect(Gd.MakeRect2I(9, (29 + D), 2, 3), Gd.At(STEEL, 2));
            Im.FillRect(Gd.MakeRect2I(15, (30 + D), 3, 2), Gd.At(STEEL, 2));
            this.Block(Im, Gd.MakeRect2I(16, (51 + D), 9, 6), new GArray(Gd.MakeColor("6E7682"), Gd.MakeColor("5B6B7C"), Gd.MakeColor("3A4250")));
            Im.FillRect(Gd.MakeRect2I(17, (53 + D), 4, 2), Gd.MakeColor("3A3530"));
            foreach (dynamic P in Gd.Iter(new GArray(Gd.MakeVector2I(1, (55 + D)), Gd.MakeVector2I(4, (57 + D)), Gd.MakeVector2I(22, (49 + D)))))
            {
                Im.FillRect(Gd.MakeRect2I(P.X, P.Y, 2, 2), Gd.At(CONCRETE, 2));
            }
        }
        else
        {
            // platform
            Im.FillRect(Gd.MakeRect2I(4, 15, 18, 5), Gd.MakeColor("3A4250"));
            Im.FillRect(Gd.MakeRect2I(4, 15, 18, 1), Gd.At(STEEL, 1));
            // head
            Im.FillRect(Gd.MakeRect2I(8, 6, 10, 9), Gd.At(STEEL, 2));
            Im.FillRect(Gd.MakeRect2I(8, 6, 10, 1), Gd.At(STEEL, 0));
            // floodlight
            Im.FillRect(Gd.MakeRect2I(10, 8, 6, 4), AMBER);
            Im.FillRect(Gd.MakeRect2I(11, 9, 3, 2), GLOW);
            // gun
            Im.FillRect(Gd.MakeRect2I(18, 10, 6, 2), DARK);
            // antenna
            Im.FillRect(Gd.MakeRect2I(12, 2, 1, 4), Gd.MakeColor("3A3F48"));
            Im.SetPixel(12, 1, Gd.At(RED, 0));
        }
        this.Outline(Im);
        return Im;
    }

    // A built wall seen from above at the game's 2.5D angle: a top face over a 14 px front face.
    // Wood = a palisade of sharpened logs; stone = mortared blocks with a capstone row.
    public Image Wall(bool Stone, bool Vert)
    {
        const int H = 14;
        int Fw = (Vert ? 32 : 96);
        int Fd = (Vert ? 96 : 32);
        var Im = Image.CreateEmpty((Fw + 2), ((Fd + H) + 2), false, Image.Format.Rgba8);
        var Top = new Rect2I(1, 1, Fw, Fd);
        var Front = new Rect2I(1, (Fd + 1), Fw, H);
        Color Light = (Stone ? new Color("A3A8B0") : new Color("C08A50"));
        Color Mid = (Stone ? new Color("8A8F98") : new Color("A0703C"));
        Color Dark = (Stone ? new Color("6A6F7C") : new Color("7A5634"));
        Color Deep = (Stone ? new Color("4A4E58") : new Color("5A3E26"));
        Im.FillRect(Top, Mid);
        Im.FillRect(Front, Dark);
        if (Stone)
        {
            // blocks on the top face and the front, offset every other row
            for (int Y = Top.Position.Y; Y < Top.End.Y; Y += 8)
            {
                Im.FillRect(new Rect2I(Top.Position.X, Y, Fw, 1), Deep);
                int Shift = ((((Y - Top.Position.Y) / 8) % 2) * 6);
                for (int X = (Top.Position.X + Shift); X < Top.End.X; X += 12)
                {
                    Im.FillRect(new Rect2I(X, Y, 1, Math.Min(8, (Top.End.Y - Y))), Deep);
                    Im.FillRect(new Rect2I((X + 1), (Y + 1), Math.Min(10, (Top.End.X - X - 1)), 1), Light);
                }
            }
            for (int Y = Front.Position.Y; Y < Front.End.Y; Y += 7)
            {
                Im.FillRect(new Rect2I(Front.Position.X, Y, Fw, 1), Deep);
                int Shift = ((((Y - Front.Position.Y) / 7) % 2) * 6);
                for (int X = (Front.Position.X + Shift); X < Front.End.X; X += 12)
                {
                    Im.FillRect(new Rect2I(X, Y, 1, Math.Min(7, (Front.End.Y - Y))), Deep);
                }
            }
        }
        else
        {
            // log tops along the wall, logs on the front face
            bool Across = !Vert;
            int Len = (Across ? Fw : Fd);
            for (int I = 0; I < Len; I += 8)
            {
                if (Across)
                {
                    Im.FillRect(new Rect2I((Top.Position.X + I), Top.Position.Y, 1, Fd), Deep);
                    Im.FillRect(new Rect2I((Top.Position.X + I + 1), Top.Position.Y, 2, Fd), Light);
                    Im.FillRect(new Rect2I((Front.Position.X + I), Front.Position.Y, 1, H), Deep);
                    Im.FillRect(new Rect2I((Front.Position.X + I + 1), Front.Position.Y, 2, H), Mid);
                }
                else
                {
                    Im.FillRect(new Rect2I(Top.Position.X, (Top.Position.Y + I), Fw, 1), Deep);
                    Im.FillRect(new Rect2I(Top.Position.X, (Top.Position.Y + I + 1), Fw, 2), Light);
                }
            }
            // rope bands
            for (int K = 1; K <= 2; K++)
            {
                if (Across)
                {
                    Im.FillRect(new Rect2I(Top.Position.X, (Top.Position.Y + ((Fd * K) / 3)), Fw, 2), new Color("D8C08A"));
                }
                else
                {
                    Im.FillRect(new Rect2I((Top.Position.X + ((Fw * K) / 3)), Top.Position.Y, 2, Fd), new Color("D8C08A"));
                }
            }
            if (!Across)
            {
                for (int X = Front.Position.X; X < Front.End.X; X += 8)
                {
                    Im.FillRect(new Rect2I(X, Front.Position.Y, 1, H), Deep);
                }
            }
        }
        // light upper-left edges, dark lower-right edges
        Im.FillRect(new Rect2I(Top.Position.X, Top.Position.Y, Fw, 1), Light.Lightened(0.15f));
        Im.FillRect(new Rect2I(Top.Position.X, Top.Position.Y, 1, Fd), Light.Lightened(0.15f));
        Im.FillRect(new Rect2I((Top.End.X - 1), Top.Position.Y, 1, Fd), Deep);
        Im.FillRect(new Rect2I(Front.Position.X, Front.Position.Y, Fw, 1), Deep);
        this.Outline(Im);
        return Im;
    }

    public dynamic Pad()
    {
        // Landing pad (FML-13): 8 tiles across, dark asphalt, amber ring, white H, blue edge lamps.
        dynamic N = 256;
        dynamic Im = Gd.Static(typeof(Image), "create_empty", N, N, false, Gd.EnumValue(typeof(Image), "FORMAT_RGBA8", 5));
        dynamic C = Gd.MakeVector2((N / 2.0f), (N / 2.0f));
        foreach (dynamic Y in Gd.Iter(N))
        {
            foreach (dynamic X in Gd.Iter(N))
            {
                dynamic D = Gd.MakeVector2((X + 0.5f), (Y + 0.5f)).DistanceTo(C);
                if ((D <= 126))
                {
                    dynamic Col = Gd.MakeColor("3A3F48");
                    if ((D > 122))
                    {
                        Col = Gd.MakeColor("2B2F36");
                    }
                    else if (((D > 104) && (D <= 112)))
                    {
                        // dashed ring
                        Col = (((Gd.Int((((Gd.Atan2((Y - C.Y), (X - C.X)) / Mathf.Tau) * 32.0f) + 32)) % 2) == 0) ? (dynamic)(AMBER) : Gd.MakeColor("2B2F36"));
                    }
                    else if (((((X * 7) + (Y * 13)) % 23) == 0))
                    {
                        // grit
                        Col = Gd.MakeColor("444955");
                    }
                    Im.SetPixel(X, Y, Col);
                }
            }
        }
        dynamic W = Gd.At(WHITE, 1);
        // H
        Im.FillRect(Gd.MakeRect2I(96, 72, 18, 112), W);
        Im.FillRect(Gd.MakeRect2I(142, 72, 18, 112), W);
        Im.FillRect(Gd.MakeRect2I(114, 119, 28, 18), W);
        foreach (dynamic I in Gd.Iter(8))
        {
            dynamic P = (C + (Vector2.FromAngle(((I * Mathf.Tau) / 8.0f)) * 117.0f));
            Im.FillRect(Gd.MakeRect2I((Gd.Int(P.X) - 2), (Gd.Int(P.Y) - 2), 4, 4), Gd.MakeColor("4FA3FF"));
        }
        this.Outline(Im);
        return Im;
    }

    // --- helpers ---------------------------------------------------------------
    public void Block(dynamic Im, dynamic R, dynamic Shades)
    {
        // A lit box: light left edge and top, dark right edge.
        Im.FillRect(R, Gd.At(Shades, 1));
        Im.FillRect(Gd.MakeRect2I(R.Position.X, R.Position.Y, 2, R.Size.Y), Gd.At(Shades, 0));
        Im.FillRect(Gd.MakeRect2I(R.Position.X, R.Position.Y, R.Size.X, 1), Gd.At(Shades, 0));
        Im.FillRect(Gd.MakeRect2I((R.End.X - 2), (R.Position.Y + 1), 2, (R.Size.Y - 1)), Gd.At(Shades, 2));
    }

    public void Shadow(dynamic Im, dynamic C, dynamic Rx, dynamic Ry)
    {
        // Ground shadow to the lower right, like the baked trees (sun from the upper left).
        foreach (dynamic Y in Gd.Iter(Im.GetHeight()))
        {
            foreach (dynamic X in Gd.Iter(Im.GetWidth()))
            {
                dynamic D = Gd.MakeVector2((((X + 0.5f) - C.X) / Rx), (((Y + 0.5f) - C.Y) / Ry));
                if ((D.LengthSquared() <= 1.0f))
                {
                    Im.SetPixel(X, Y, SHADOW);
                }
            }
        }
    }

    public void Outline(dynamic Im)
    {
        dynamic Src = Gd.Duplicate(Im);
        foreach (dynamic Y in Gd.Iter(Im.GetHeight()))
        {
            foreach (dynamic X in Gd.Iter(Im.GetWidth()))
            {
                if (Gd.Truth(this.Solid(Src, X, Y)))
                {
                    continue;
                }
                foreach (dynamic D in Gd.Iter(new GArray(Gd.MakeVector2I(1, 0), Gd.MakeVector2I((-1), 0), Gd.MakeVector2I(0, 1), Gd.MakeVector2I(0, (-1)))))
                {
                    if (Gd.Truth(this.Solid(Src, (X + D.X), (Y + D.Y))))
                    {
                        Im.SetPixel(X, Y, INK);
                        break;
                    }
                }
            }
        }
    }

    public dynamic Solid(dynamic Im, dynamic X, dynamic Y)
    {
        return ((X >= 0) && (Y >= 0) && (X < Im.GetWidth()) && (Y < Im.GetHeight()) && (Im.GetPixel(X, Y).A > 0.6f));
    }

}
