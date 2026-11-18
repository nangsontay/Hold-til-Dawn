using Godot;
using Environment = Godot.Environment;
using FileAccess = Godot.FileAccess;
using System;
using System.Threading.Tasks;
using HoldTilDawn;

public partial class PixelChars : Node
{
    // Pixel-art sprites of the six GDD characters (tab:characters: CHR-01..03 players, CHR-05..07 enemies),
    // following the style guide of GDD ch. 6 (tab:style-guide): 32 x 32 px at tile scale (Wrecker 1.25 tiles = 40 x 40),
    // dark outline, limited shading, light from the upper left, GDD palette, a walk cycle in 4 directions
    // (left is the mirrored right). Not part of the game. Run: godot --headless --path . tools/PixelChars.tscn
    // Writes art/chars/<character>.png (1x pixels; the game scales them up without smoothing) and art/chars/chars.json.
    // Sheet layout: rows = right, down, left, up; columns = idle 0-1, walk 0-3.
    // The PNGs can be repainted by hand (Aseprite, Piskel, ...): the game reads them as they are.
    // #
    // Each body is a pixel map: one letter per pixel, looked up in the character's palette ('.' = empty).
    // A character = a body (human / nox / wrecker) + its palette + overlays (gear) drawn on top.
    public static readonly dynamic INK = Gd.MakeColor("141019");
    public static readonly dynamic SHADOW = Gd.MakeColor(0, 0, 0, 0.32f);
    // --- bodies: [top row, rows]; rows are 16 px wide (20 for the Wrecker), centred on the canvas --------
    public static readonly dynamic HUMAN = new GDict { { "size", 32 }, { "feet", 28 }, { "legs", 23 }, { "shadow", 6 }, { "split", new GDict { { "down", new GArray(2, 8, 14) }, { "up", new GArray(2, 8, 14) }, { "side", new GArray(4, 8, 14) } } }, { "down", new GArray(6, new GArray("....HHHHHHHH....", "...HHHHHHHHHH...", "..HHHHHHHHHHHh..", "..HHHHHHHHHHHh..", "..HHSSSSSSSSHh..", "..HSSSSSSSSSSh..", "..HSSEESSEESSh..", "..hSSEESSEESSh..", "..hSSSSSSSSSSs..", "...sSSSSSSSSs...", "....ssSSSSss....", "..ccLCCCCCCCcc..", "..ccLCCCCCCCcc..", "..ccLCCCCCCCcc..", "..SSLCCCCCCCSS..", "..SSCCCCCCCCSS..", "...GGGGGGGGGG...", "...PPPPPPPPPp...", "...PPPp..PPPp...", "...PPPp..PPPp...", "...PPPp..PPPp...", "...BBBb..BBBb...", "..BBBBb..BBBBb..")) }, { "up", new GArray(6, new GArray("....HHHHHHHH....", "...HHHHHHHHHH...", "..HHHHHHHHHHHh..", "..HHHHHHHHHHHh..", "..HHHHHHHHHHHh..", "..HHHHHHHHHHHh..", "..HHHHHHHHHHHh..", "..hHHHHHHHHHHh..", "..shHHHHHHHHhs..", "...shhhhhhhhs...", "....ssSSSSss....", "..ccLCCCCCCCcc..", "..ccLCCCCCCCcc..", "..ccLCCCCCCCcc..", "..SSLCCCCCCCSS..", "..SSCCCCCCCCSS..", "...GGGGGGGGGG...", "...PPPPPPPPPp...", "...PPPp..PPPp...", "...PPPp..PPPp...", "...PPPp..PPPp...", "...BBBb..BBBb...", "..BBBBb..BBBBb..")) }, { "side", new GArray(6, new GArray(".....HHHHHHH....", "....HHHHHHHHH...", "...HHHHHHHHHHH..", "...HHHHHHHHHHH..", "...HHHHHHSSSSS..", "...HHHHHSSSSSS..", "...hHHHHSSSSES..", "...hHHHSSSSSES..", "...hhHSSSSSSSS..", "....hhsSSSSSs...", "......sSSSs.....", ".....cLCCCCc....", ".....cLCCCCc....", ".....cLccCCc....", ".....cLccCCc....", ".....CCSSCCc....", ".....GGGGGGG....", ".....PPPPPPp....", ".....PPp.PPp....", ".....PPp.PPp....", ".....PPp.PPp....", ".....BBb.BBb....", ".....BBBB.BBBB..")) } };
    public static readonly dynamic NOX = new GDict { { "size", 32 }, { "feet", 28 }, { "legs", 25 }, { "shadow", 7 }, { "split", new GDict { { "down", new GArray(4, 8, 13) }, { "up", new GArray(4, 8, 13) }, { "side", new GArray(2, 7, 12) } } }, { "down", new GArray(10, new GArray("....nMMMMMMn....", "...MMNNNNNNNn...", "..MNNNNNNNNNNn..", ".MNNNNNNNNNNNNn.", ".MNYYNNNNNNYYNn.", ".NNYYNNNNNNYYNn.", ".NNNNNNNNNNNNNn.", ".nNNDTDTTDTDNNn.", "MNnNNDDDDDDNNnNn", "MNnnNNNNNNNNnnNn", "MNn.nNNNNNNNn.Nn", "MNn.nNNNNNNNn.Nn", "NNn.nNNNNNNNn.Nn", "Nn..nNNNNNNNn.nn", "AA...nNnnNNn..AA", ".....nNn.nNn....", ".....nNn.nNn....", "....nnNn.nNnn...", "....nnnn.nnnn...")) }, { "up", new GArray(10, new GArray("....nMMMMMMn....", "...MMNNNNNNNn...", "..MNNNNNNNNNNn..", ".MNNNNNNNNNNNNn.", ".MNNNNNNNNNNNNn.", ".NNNNNNNNNNNNNn.", ".NNNNNNNNNNNNNn.", ".nNNNNNNNNNNNNn.", "MNnNNNNNNNNNNnNn", "MNnnNNNNNNNNnnNn", "MNn.nNNNNNNNn.Nn", "MNn.nNNNNNNNn.Nn", "NNn.nNNNNNNNn.Nn", "Nn..nNNNNNNNn.nn", "AA...nNnnNNn..AA", ".....nNn.nNn....", ".....nNn.nNn....", "....nnNn.nNnn...", "....nnnn.nnnn...")) }, { "side", new GArray(10, new GArray("...nMMMMMMn.....", "..MNNNNNNNNMn...", ".MNNNNNNNNNNNn..", ".NNNNNNNNNNNNNn.", ".NNNNNNNNNNYYNn.", ".nNNNNNNNNNYYNNn", ".nNNNNNNNNNNNNNn", ".nNNNNNNNNNNDTDn", "..nNNNNNNNNNNDDn", "..nNNNNNNNNNNNn.", "..nnNNNNNNNNNNn.", "...nNNNNNNNnNNn.", "...nNNNNNNn.nNn.", "...nNNnnNNn.nNn.", "...nNn..nNn.nNn.", "...nNn..nNn.AAA.", "...nNn..nNn.....", "..nnNn.nnNn.....", "..nnnn.nnnn.....")) } };
    public static readonly dynamic WRECKER_BODY = new GDict { { "size", 40 }, { "feet", 37 }, { "legs", 32 }, { "shadow", 10 }, { "split", new GDict { { "down", new GArray(4, 10, 17) }, { "up", new GArray(4, 10, 17) }, { "side", new GArray(3, 10, 17) } } }, { "down", new GArray(12, new GArray("......A......A......", "......AA....AA......", ".....RrRRRRRRrR.....", "....RRRRRRRRRRRr....", "...RRRRRRRRRRRRRr...", "...NNNNNNNNNNNNNn...", "..NNYYYNNNNNNYYYNn..", "..NNNNNNNNNNNNNNNn..", "..nNNDTDTDTDTDNNNn..", ".RRRnNDDDDDDDDNnRRR.", "RRRRRRNNNNNNNNRRRRRr", "ARRRRRRRRRRRRRRRRRrA", "NNn.RRRRRRRRRRRr.nNn", "NNn.RRRRAARRRRRr.nNn", "NNn.RRRRRRRRRRRr.nNn", "NNn.rrrrrrrrrrrr.nNn", "NNn.nNNNNNNNNNNn.nNn", "NNn.nNNNNNNNNNNn.nNn", "Nnn..nNNNNNNNNn..nNn", "AAA..nNNNnnNNNn..AAA", ".....nNNn..nNNn.....", ".....nNNn..nNNn.....", ".....RRRr..RRRr.....", ".....nNNn..nNNn.....", "....nnNNn..nNNnn....", "....nnnnn..nnnnn....")) }, { "up", new GArray(12, new GArray("......A......A......", "......AA....AA......", ".....RrRRRRRRrR.....", "....RRRRRRRRRRRr....", "...RRRRRRRRRRRRRr...", "...NNNNNNNNNNNNNn...", "..NNNNNNNNNNNNNNNn..", "..NNNNNNNNNNNNNNNn..", "..nNNNNNNNNNNNNNNn..", ".RRRnNNNNNNNNNNnRRR.", "RRRRRRRRRRRRRRRRRRRr", "ARRRRRRRRRRRRRRRRRrA", "NNn.RRRRRAARRRRr.nNn", "NNn.RRRRRAARRRRr.nNn", "NNn.RRRRRAARRRRr.nNn", "NNn.rrrrrrrrrrrr.nNn", "NNn.nNNNNNNNNNNn.nNn", "NNn.nNNNNNNNNNNn.nNn", "Nnn..nNNNNNNNNn..nNn", "AAA..nNNNnnNNNn..AAA", ".....nNNn..nNNn.....", ".....nNNn..nNNn.....", ".....RRRr..RRRr.....", ".....nNNn..nNNn.....", "....nnNNn..nNNnn....", "....nnnnn..nnnnn....")) }, { "side", new GArray(12, new GArray(".........A....A.....", ".........AA...AA....", "......RRRRRRRRRr....", ".....RRRRRRRRRRRr...", "....RRRRRRRRRRRRRr..", "....NNNNNNNNNNNNNn..", "...NNNNNNNNNNNYYYNn.", "...NNNNNNNNNNNNNNNn.", "...nNNNNNNNNNNNDTDn.", "..RRRRnNNNNNNNNNDDn.", ".RRRRRRRRNNNNNNNNn..", ".ARRRRRRRRRRRRRRRr..", "..RRRRRRRRRRRRRRRr..", "..RRRRRRRRRRRnNNNn..", "..RRRRRRRRRRRnNNNn..", "..nrrrrrrrrrrnNNNn..", "...nNNNNNNNNNnNNNn..", "...nNNNNNNNNNnNNNn..", "....nNNNNNNNNnAAAA..", "....nNNNNnNNNn......", "....nNNNn.nNNNn.....", "....nNNNn.nNNNn.....", "....RRRr..RRRRr.....", "....nNNNn.nNNNn.....", "...nnNNNn.nNNNNn....", "...nnnnnn.nnnnnn....")) } };
    // --- characters: body, palette (GDD tab:style-guide colours first), gear overlays -------------------
    public static readonly dynamic SKIN_L = new GDict { { "S", "E9B48A" }, { "s", "C98F66" }, { "E", "1A1418" } };
    public static readonly dynamic BOOTS = new GDict { { "B", "2E2620" }, { "b", "1C1612" } };
    public static readonly dynamic NOX_PAL = new GDict { { "N", "4A2C5A" }, { "n", "2F1B3B" }, { "M", "6B4380" }, { "Y", "E8E3C8" }, { "D", "120A16" }, { "T", "E8E3C8" }, { "A", "C63FA8" }, { "a", "8A2A75" } };
    public static readonly dynamic CHARACTERS = new GDict { { "scout", new GDict { { "body", "human" }, { "pal", new GDict { { "H", "6B4A2B" }, { "h", "4E3520" }, { "C", "2BB3B1" }, { "L", "6FD9D5" }, { "c", "1E7F7D" }, { "G", "4A3626" }, { "P", "5A6B34" }, { "p", "3E4B23" }, { "T", "36C9C6" }, { "W", "7A5634" }, { "w", "E8E3C8" }, { "F", "36C9C6" } } }, { "over", new GDict { { "down", new GArray(9, new GArray("..TTTTTTTTTTTT..", "................", "...............W", "..............WW", "..............W.", "..............W.", "..............W.", "..............W.", ".....W........W.", "......W.......W.", ".......W......W.", "........W.....W.", ".........W.....W")) }, { "up", new GArray(9, new GArray("..TTTTTTTTTTTT..", ".......TT.......", ".......TT.......", "................", "..............W.", "..............W.", ".............W..", "...........WW...", "..........W.....", ".........W......", "........W.......", ".......W........", "......W.........", "....WW..........")) }, { "side", new GArray(9, new GArray("...TTTTTTTTTTT..", "...TT...........", "..TT............", "...W............", "..Ww............", "..Ww............", "..Ww............", "..Ww............", "..Ww............", "..Ww............", "..Ww............", "..Ww............", "..Ww............", "...W............")) } } } } }, { "healer", new GDict { { "body", "human" }, { "pal", new GDict { { "H", "2B1E1A" }, { "h", "1A120F" }, { "S", "A86B45" }, { "s", "82502F" }, { "C", "F2F2F2" }, { "L", "FFFFFF" }, { "c", "C3CAD3" }, { "G", "8A96A3" }, { "P", "4A535E" }, { "p", "353C45" }, { "X", "3DBE5A" }, { "K", "F2F2F2" }, { "k", "8A96A3" }, { "Z", "9BE3B0" } } }, { "over", new GDict { { "down", new GArray(14, new GArray("..H..........H..", "..H..........H..", "..H..........H..", "..k.........k...", "...k.......k....", ".......X........", "......XXX.......", ".......X........", "................", "..............Z.", "..............Z.")) }, { "up", new GArray(14, new GArray("..hHHHHHHHHHHh..", "...HHHHHHHHHH...", "....HHHHHHHH....", "...kkkkkkkkkk...", "...kKKKKKKKKk...", "...kKKKXXKKKk...", "...kKKXXXXKKk...", "...kKKKXXKKKk...", "...kkkkkkkkkk...")) }, { "side", new GArray(14, new GArray("...HH...........", "...HHH..........", "..kkkk..........", "..kKKk..........", "..kXKk..........", "..XXXk.XX.......", "..kXKk..........", "..kKKk..........", "..kkkk..........")) } } } } }, { "engineer", new GDict { { "body", "human" }, { "pal", new GDict { { "H", "3A2A20" }, { "h", "261A13" }, { "C", "FF7A1A" }, { "L", "FFA65C" }, { "c", "2B3A55" }, { "G", "5A3E26" }, { "P", "2B3A55" }, { "p", "1E2A3F" }, { "O", "FF7A1A" }, { "o", "C2560A" }, { "Q", "FFB070" }, { "V", "D9DDE0" }, { "M", "8A8F98" }, { "m", "5B5F6B" }, { "W", "7A5634" }, { "D", "3A4250" }, { "Y", "F2B544" } } }, { "over", new GDict { { "down", new GArray(4, new GArray(".....OOQOO......", "...OOOOQOOOO....", "..OOOOOQOOOOo...", "..OOOOOQOOOOo...", "..OOOOOOOOOOo...", ".oooooooooooooo.", "................", "................", "................", "................", "................", "................", "MMM.............", "MmM.............", ".W..............", ".W..VVVVVVVV....", ".W..............", ".W..............", ".W.....YY.......", ".W..............")) }, { "up", new GArray(4, new GArray("......OOQOO.....", "....OOOOQOOOO...", "...OOOOOQOOOOo..", "...OOOOOQOOOOo..", "..OOOOOOOOOOOo..", ".oooooooooooooo.", "................", "................", "................", "................", "................", "................", "...DDDDDDDDDD.MM", "...DOOOOOOOOD.Mm", "...DDDDDDDDDD..W", "...DDDDDDDDDD..W", "...DDDDDDDDDD..W", "...............W", "...............W")) }, { "side", new GArray(4, new GArray(".....OOOQOO.....", "....OOOOQOOOO...", "...OOOOOQOOOOo..", "...OOOOOQOOOOo..", "...OOOOOOOOOOo..", "..ooooooooooooo.", "................", "................", "................", "................", "................", "................", "...DD...........", "..DDD...........", "..DOD.VVVV......", "..DDD...........", "..DDD...........", "...........MM...", ".........WWMm...", "................")) } } } } }, { "grunt", new GDict { { "body", "nox" }, { "pal", new GDict {  } }, { "over", new GDict { { "down", new GArray(8, new GArray("....A......A....", "....aA....Aa....", "................", "................", "................", "................", "................", "................", "................", "................", "................", "................", "......A...A.....", ".......A...A....", "........A...A...")) }, { "up", new GArray(8, new GArray("....A......A....", "....aA....Aa....", "................", ".......AA.......", "......aAAa......", "................", ".......AA.......", "......aAAa......", "................", ".......AA.......", "......aAAa......")) }, { "side", new GArray(8, new GArray("................", "...A..A.........", "..aAaaAa........", "................", "................", "................", "................", "................", "................", "................", "....A.A.........")) } } } } }, { "spitter", new GDict { { "body", "nox" }, { "pal", new GDict { { "Q", "D8E03A" }, { "q", "F6F9A8" }, { "j", "8F9620" } } }, { "over", new GDict { { "down", new GArray(8, new GArray("..........jQQj..", ".........jQqQQj.", ".........QQQQQj.", ".........jQQQj..", "................", "................", "................", "................", "................", ".......QQ.......", "........Q.......", "........Q.......")) }, { "up", new GArray(11, new GArray("................", ".....jQQQQj.....", "....jQqqQQQj....", "....QQqQQQQQ....", "....QQQQQQQQ....", "....jQQQQQQj....", ".....jQQQQj.....")) }, { "side", new GArray(8, new GArray("..jQQj..........", ".jQqQQj.........", ".QQQQQQ.........", ".jQQQQj.........", "..jQQj..........", "................", "................", "................", "..............Q.", "..............Q.")) } } } } }, { "wrecker", new GDict { { "body", "wrecker" }, { "pal", new GDict { { "R", "6E5A7A" }, { "r", "463650" } } }, { "over", new GDict {  } } } } };
    public static readonly dynamic BODIES = new GDict { { "human", HUMAN }, { "nox", NOX }, { "wrecker", WRECKER_BODY } };
    public static readonly dynamic VIEWS = new GArray("side", "down", "side", "up");
    public override void _Ready()
    {
        dynamic Out = "res://art/chars";
        foreach (string outputArgument in OS.GetCmdlineUserArgs())
            if (outputArgument.StartsWith("--out=", StringComparison.Ordinal)) Out = outputArgument[6..];
        Gd.Static(typeof(DirAccess), "make_dir_recursive_absolute", Out);
        dynamic Info = new GDict { { "frames", new GDict { { "idle", new GArray(0, 2) }, { "run", new GArray(2, 4) } } }, { "fps", new GDict { { "idle", 2.0f }, { "run", 8.0f } } }, { "dirs", new GArray("right", "down", "left", "up") }, { "chars", new GDict {  } } };
        foreach (dynamic N in Gd.Iter(CHARACTERS))
        {
            dynamic Body = Gd.At(BODIES, Gd.At(CHARACTERS, N).Body);
            int Size = Gd.Int(Body.Size);
            dynamic Sheet = Gd.Static(typeof(Image), "create_empty", (Size * 6), (Size * 4), false, Gd.EnumValue(typeof(Image), "FORMAT_RGBA8", 5));
            foreach (dynamic Row in Gd.Iter(4))
            {
                string View = Gd.At(VIEWS, Row);
                dynamic Base = this.Compose(N, View);
                dynamic Frames = new GArray(Base, this.Bob(Base, Body), Base, this.Step(Base, Body, View, 1), Base, this.Step(Base, Body, View, (-1)));
                foreach (dynamic Col in Gd.Iter(6))
                {
                    dynamic F = this.Finish(Gd.At(Frames, Col), Body);
                    if ((Row == 2))
                    {
                        F.FlipX();
                    }
                    Sheet.BlitRect(F, Gd.MakeRect2I(0, 0, Size, Size), Gd.MakeVector2I((Col * Size), (Row * Size)));
                }
            }
            Sheet.SavePng(Gd.PathJoin(Out, (N + ".png")));
            Info.Chars[N] = new GDict { { "size", Size }, { "anchor", new GArray((Size / 2.0f), (Body.Feet + 1)) } };
            GD.Print("drew ", N, " ", Size, "x", Size);
        }
        dynamic Fa = Gd.Static(typeof(FileAccess), "open", Gd.PathJoin(Out, "chars.json"), Gd.EnumValue(typeof(FileAccess), "WRITE", 2));
        Fa.StoreString(Json.Stringify(Gd.ToVariant(Info), "\t"));
        Fa.Close();
        GetTree().Quit();
    }

    public dynamic Compose(dynamic N, dynamic View)
    {
        dynamic Ch = Gd.At(CHARACTERS, N);
        dynamic Body = Gd.At(BODIES, Ch.Body);
        int Size = Gd.Int(Body.Size);
        dynamic Pal = new GDict {  };
        foreach (dynamic P in Gd.Iter(new GArray(SKIN_L, BOOTS, NOX_PAL, Ch.Pal)))
        {
            Gd.Merge(Pal, P, true);
        }
        dynamic Im = Gd.Static(typeof(Image), "create_empty", Size, Size, false, Gd.EnumValue(typeof(Image), "FORMAT_RGBA8", 5));
        int Pad = Gd.Int(((Size - Gd.Length(Gd.At(Gd.At(Gd.At(Body, View), 1), 0))) / 2));
        foreach (dynamic Layer in Gd.Iter(new GArray(Gd.At(Body, View), Gd.Get(Ch.Over, View, new GArray(0, new GArray())))))
        {
            int Top = Gd.Int(Gd.At(Layer, 0));
            foreach (dynamic R in Gd.Iter(Gd.Size(Gd.At(Layer, 1))))
            {
                string Line = Gd.At(Gd.At(Layer, 1), R);
                foreach (dynamic I in Gd.Iter(Gd.Length(Line)))
                {
                    dynamic K = Gd.At(Line, I);
                    if ((K == "."))
                    {
                        continue;
                    }
                    if (!(Gd.Truth(Gd.Has(Pal, K))))
                    {
                        GD.PushError(Gd.Format("%s %s: no colour for '%s'", new GArray(N, View, K)));
                        continue;
                    }
                    Im.SetPixel((Pad + I), (Top + R), Gd.MakeColor(Gd.At(Pal, K)));
                }
            }
        }
        return Im;
    }

    public dynamic Bob(dynamic Src, dynamic Body)
    {
        // Idle breath: everything above the legs sinks one pixel.
        dynamic Im = Gd.Duplicate(Src);
        int Size = Gd.Int(Body.Size);
        int Legs = Gd.Int(Body.Legs);
        Im.FillRect(Gd.MakeRect2I(0, 0, Size, Legs), Gd.MakeColor(0, 0, 0, 0));
        Im.BlitRect(Src, Gd.MakeRect2I(0, 0, Size, Legs), Gd.MakeVector2I(0, 1));
        return Im;
    }

    public dynamic Step(dynamic Src, dynamic Body, dynamic View, dynamic Side)
    {
        // Walk pose. Front/back views: one foot lifts a pixel. Side view: the legs open into a stride (or cross).
        dynamic Im = Gd.Duplicate(Src);
        int Size = Gd.Int(Body.Size);
        int Pad = Gd.Int(((Size - ((Size == 32) ? (dynamic)(16) : 20)) / 2));
        dynamic Sp = Gd.At(Body.Split, View);
        int Top = Gd.Int(Body.Legs);
        int H = Gd.Int(((Body.Feet + 1) - Top));
        // left / back leg
        dynamic A = Gd.MakeRect2I((Pad + Gd.At(Sp, 0)), Top, (Gd.At(Sp, 1) - Gd.At(Sp, 0)), H);
        // right / front leg
        dynamic B = Gd.MakeRect2I((Pad + Gd.At(Sp, 1)), Top, (Gd.At(Sp, 2) - Gd.At(Sp, 1)), H);
        Im.FillRect(A, Gd.MakeColor(0, 0, 0, 0));
        Im.FillRect(B, Gd.MakeColor(0, 0, 0, 0));
        if ((View == "side"))
        {
            Im.BlendRect(Src, A, (A.Position + Gd.MakeVector2I((-Side), 0)));
            Im.BlendRect(Src, B, (B.Position + Gd.MakeVector2I(Side, 0)));
        }
        else
        {
            dynamic Lift = ((Side > 0) ? (dynamic)(A) : B);
            dynamic Stay = ((Side > 0) ? (dynamic)(B) : A);
            Im.BlendRect(Src, Stay, Stay.Position);
            Im.BlendRect(Src, Gd.MakeRect2I((Lift.Position + Gd.MakeVector2I(0, 1)), (Lift.Size - Gd.MakeVector2I(0, 1))), Lift.Position);
        }
        return Im;
    }

    public dynamic Finish(dynamic Src, dynamic Body)
    {
        // Dark outline around the silhouette, then a soft ground shadow under the feet.
        int Size = Gd.Int(Body.Size);
        dynamic Im = Gd.Duplicate(Src);
        foreach (dynamic Y in Gd.Iter(Size))
        {
            foreach (dynamic X in Gd.Iter(Size))
            {
                if ((Src.GetPixel(X, Y).A > 0.0f))
                {
                    continue;
                }
                foreach (dynamic D in Gd.Iter(new GArray(Gd.MakeVector2I(1, 0), Gd.MakeVector2I((-1), 0), Gd.MakeVector2I(0, 1), Gd.MakeVector2I(0, (-1)))))
                {
                    Vector2I Q = (Vector2I)((Gd.MakeVector2I(X, Y) + D));
                    if (((Q.X >= 0) && (Q.Y >= 0) && (Q.X < Size) && (Q.Y < Size) && (Src.GetPixel(Q.X, Q.Y).A > 0.0f)))
                    {
                        Im.SetPixel(X, Y, INK);
                        break;
                    }
                }
            }
        }
        float Cy = Gd.Float((Body.Feet + 1.0f));
        float Rx = Gd.Float(Body.Shadow);
        foreach (dynamic Y in Gd.Iter(Gd.Range((Gd.Int(Cy) - 2), Gd.Mini(Size, (Gd.Int(Cy) + 3)))))
        {
            foreach (dynamic X in Gd.Iter(Size))
            {
                dynamic Dx = (((X + 0.5f) - (Size / 2.0f)) / Rx);
                dynamic Dy = (((Y + 0.5f) - Cy) / 2.2f);
                if (((((Dx * Dx) + (Dy * Dy)) <= 1.0f) && (Im.GetPixel(X, Y).A == 0.0f)))
                {
                    Im.SetPixel(X, Y, SHADOW);
                }
            }
        }
        return Im;
    }

}
