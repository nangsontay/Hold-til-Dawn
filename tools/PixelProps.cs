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
        this.Save(Out, "lighthouse", this.Lighthouse(), Gd.MakeVector2(14, 73));
        this.Save(Out, "workbench", this.Workbench(), Gd.MakeVector2(17, 22));
        this.Save(Out, "tower", this.Tower(false), Gd.MakeVector2(13, 57));
        this.Save(Out, "tower_wreck", this.Tower(true), Gd.MakeVector2(13, 57));
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
    public dynamic Lighthouse()
    {
        // The old lighthouse at the heart of Lighthouse Outpost; its lamp is the beacon (GDD 5.1).
        dynamic Im = Gd.Static(typeof(Image), "create_empty", 28, 76, false, Gd.EnumValue(typeof(Image), "FORMAT_RGBA8", 5));
        this.Shadow(Im, Gd.MakeVector2(19, 72), 12, 3);
        // plinth
        this.Block(Im, Gd.MakeRect2I(2, 66, 24, 8), CONCRETE);
        foreach (dynamic Y in Gd.Iter(Gd.Range(22, 66)))
        {
            dynamic Half = (6 + Gd.Int(((3.0f * (Y - 22)) / 43.0f)));
            dynamic Band = (((((65 - Y) / 9) % 2) == 0) ? (dynamic)(RED) : WHITE);
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
        Im.FillRect(Gd.MakeRect2I(12, 58, 4, 8), Gd.MakeColor("2B2024"));
        // window
        Im.FillRect(Gd.MakeRect2I(13, 40, 2, 3), Gd.MakeColor("2B2024"));
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

    public dynamic Workbench()
    {
        // The team's workbench (crafting, MEC-10): planks, legs, a vise, hammer and saw, a red tool box.
        dynamic Im = Gd.Static(typeof(Image), "create_empty", 34, 24, false, Gd.EnumValue(typeof(Image), "FORMAT_RGBA8", 5));
        this.Shadow(Im, Gd.MakeVector2(20, 22), 14, 2);
        // legs
        Im.FillRect(Gd.MakeRect2I(2, 15, 3, 7), Gd.At(WOOD, 3));
        Im.FillRect(Gd.MakeRect2I(29, 15, 3, 7), Gd.At(WOOD, 3));
        Im.FillRect(Gd.MakeRect2I(4, 18, 25, 1), Gd.At(WOOD, 3));
        // top
        Im.FillRect(Gd.MakeRect2I(1, 6, 32, 6), Gd.At(WOOD, 1));
        Im.FillRect(Gd.MakeRect2I(1, 6, 32, 1), Gd.At(WOOD, 0));
        foreach (dynamic X in Gd.Iter(new GArray(11, 22)))
        {
            Im.FillRect(Gd.MakeRect2I(X, 6, 1, 6), Gd.At(WOOD, 2));
        }
        // front edge
        Im.FillRect(Gd.MakeRect2I(1, 12, 32, 3), Gd.At(WOOD, 2));
        // vise
        Im.FillRect(Gd.MakeRect2I(3, 3, 5, 3), Gd.At(STEEL, 2));
        Im.FillRect(Gd.MakeRect2I(4, 2, 3, 1), Gd.At(STEEL, 1));
        // hammer
        Im.FillRect(Gd.MakeRect2I(12, 8, 7, 1), Gd.At(WOOD, 3));
        Im.FillRect(Gd.MakeRect2I(19, 6, 2, 4), Gd.At(STEEL, 1));
        // saw
        Im.FillRect(Gd.MakeRect2I(23, 8, 7, 2), Gd.At(STEEL, 0));
        foreach (dynamic X in Gd.Iter(Gd.Range(23, 30, 2)))
        {
            Im.SetPixel(X, 10, Gd.At(STEEL, 2));
        }
        // tool box
        Im.FillRect(Gd.MakeRect2I(25, 2, 6, 4), Gd.At(RED, 1));
        Im.FillRect(Gd.MakeRect2I(26, 1, 4, 1), Gd.At(RED, 2));
        this.Outline(Im);
        return Im;
    }

    public dynamic Tower(dynamic Wreck)
    {
        // Defence tower (AGT-12): concrete footing, steel lattice mast, platform, floodlight (amber) and a small gun.
        dynamic Im = Gd.Static(typeof(Image), "create_empty", 26, 60, false, Gd.EnumValue(typeof(Image), "FORMAT_RGBA8", 5));
        this.Shadow(Im, Gd.MakeVector2(17, 56), 11, 3);
        this.Block(Im, Gd.MakeRect2I(3, 50, 20, 8), CONCRETE);
        dynamic Top = (Gd.Truth(Wreck) ? (dynamic)(32) : 20);
        foreach (dynamic Y in Gd.Iter(Gd.Range(Top, 50)))
        {
            dynamic TimeLeft = (Gd.Float((Y - 20)) / 30.0f);
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
            Im.FillRect(Gd.MakeRect2I(9, 29, 2, 3), Gd.At(STEEL, 2));
            Im.FillRect(Gd.MakeRect2I(15, 30, 3, 2), Gd.At(STEEL, 2));
            this.Block(Im, Gd.MakeRect2I(16, 51, 9, 6), new GArray(Gd.MakeColor("6E7682"), Gd.MakeColor("5B6B7C"), Gd.MakeColor("3A4250")));
            Im.FillRect(Gd.MakeRect2I(17, 53, 4, 2), Gd.MakeColor("3A3530"));
            foreach (dynamic P in Gd.Iter(new GArray(Gd.MakeVector2I(1, 55), Gd.MakeVector2I(4, 57), Gd.MakeVector2I(22, 49))))
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
