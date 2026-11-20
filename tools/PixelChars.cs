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
    // dark outline, limited shading, light from the upper left, GDD palette, a walk cycle in 8 directions
    // (left is the mirrored right). The diagonals are 3/4 views: the same silhouette as the front / back view,
    // with the face redrawn turned toward the right (far eye thinner, face shifted, hair or an ear showing). Not part of the game. Run: godot --headless --path . tools/PixelChars.tscn
    // Writes art/chars/<character>.png (1x pixels; the game scales them up without smoothing) and art/chars/chars.json.
    // Sheet layout: rows = right, down, left, up, down-right, down-left, up-left, up-right; columns = idle 0-1,
    // walk 2-5, then the fight frames of tools/PixelPoses.cs, 4 per attack (players: bare hands, stone spear,
    // metal blade, hammer, bow; enemies: their own attack), and last 4 frames knocked down (hit, knees give,
    // falling, lying with the eyes shut). chars.json lists them under "anims". A cell is the body plus
    // PixelPoses.M pixels on every side, so a weapon at full reach fits; "body" is the character's own size.
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
    public static readonly dynamic VIEWS = new GArray("side", "down", "side", "up", "down3q", "down3q", "up3q", "up3q");
    // rows drawn mirrored (facing left) and rows turned into a 3/4 view
    public static readonly int[] MIRRORED = { 2, 5, 6 };
    // the attack each enemy has (tools/PixelPoses.cs); players get one attack per weapon (PixelPoses.PLAYER_ATTACKS)
    public static readonly System.Collections.Generic.Dictionary<string, string> ENEMY_ATTACK = new() { { "grunt", "claw" }, { "spitter", "spit" }, { "wrecker", "smash" } };
    public const float ENEMY_ATTACK_FPS = 10.0f;
    public const float DOWN_FPS = 8.0f;

    public override void _Ready()
    {
        string Out = "res://art/chars";
        foreach (string outputArgument in OS.GetCmdlineUserArgs())
            if (outputArgument.StartsWith("--out=", StringComparison.Ordinal)) Out = outputArgument[6..];
        DirAccess.MakeDirRecursiveAbsolute(Out);
        this.AddThreeQuarterViews();
        dynamic Info = new GDict { { "frames", new GDict { { "idle", new GArray(0, 2) }, { "run", new GArray(2, 4) } } }, { "fps", new GDict { { "idle", 2.0f }, { "run", 10.0f } } }, { "dirs", new GArray("right", "down", "left", "up", "down_right", "down_left", "up_left", "up_right") }, { "chars", new GDict {  } } };
        foreach (dynamic N in Gd.Iter(CHARACTERS))
        {
            string Name = N;
            string BodyName = Gd.At(CHARACTERS, N).Body;
            dynamic Body = Gd.At(BODIES, BodyName);
            int Size = Gd.Int(Body.Size), Legs = Gd.Int(Body.Legs), Feet = Gd.Int(Body.Feet);
            float Rx = Gd.Float(Body.Shadow);
            // the body sits in the middle of a bigger cell, so a weapon at full reach still fits
            int Cell = (Size + (2 * PixelPoses.M));
            float Ground = ((Feet + PixelPoses.M) + 1);
            bool Player = (BodyName == "human");
            string[] Attacks = (Player ? PixelPoses.PLAYER_ATTACKS : new[] { ENEMY_ATTACK[Name] });
            int Cols = ((6 + (PixelPoses.ATTACK_FRAMES * Attacks.Length)) + 4);
            var Sheet = Image.CreateEmpty((Cell * Cols), (Cell * 8), false, Image.Format.Rgba8);
            dynamic Pal = this.Palette(N);
            for (int Row = 0; Row < 8; Row++)
            {
                string View = Gd.At(VIEWS, Row);
                dynamic Base = this.Compose(N, View);
                var Frames = new System.Collections.Generic.List<Image>();
                // idle 0-1, walk 2-5: the body dips a pixel on each stride and rises as the legs pass (a light bounce)
                foreach (dynamic F in Gd.Iter(new GArray(Base, this.Bob(Base, Body), this.Bob(this.Step(Base, Body, View, 1), Body), Base, this.Bob(this.Step(Base, Body, View, (-1)), Body), Base)))
                {
                    Frames.Add(PixelPoses.Finish(PixelPoses.Embed(Raw(F), Cell), Ground, Rx));
                }
                // the attacks (4 frames each), then knocked down (4 frames, the last one lying with the eyes shut)
                var R = Rig(BodyName, View, Pal);
                foreach (string Kind in Attacks)
                {
                    Frames.AddRange(PixelPoses.Attack(Kind, Raw(Base), View, R, Legs, Feet, Cell, Rx));
                }
                (Image Im, float Rx)[] Fallen = PixelPoses.Fall(Raw(Base), Raw(this.Compose(N, View, true)), View, Legs, Feet, Cell, Rx);
                foreach (var (Im, Srx) in Fallen)
                {
                    Frames.Add(PixelPoses.Finish(Im, Ground, Srx));
                }
                for (int Col = 0; Col < Cols; Col++)
                {
                    var F = Frames[Col];
                    if ((Array.IndexOf(MIRRORED, Row) >= 0))
                    {
                        F.FlipX();
                    }
                    Sheet.BlitRect(F, new Rect2I(0, 0, Cell, Cell), new Vector2I((Col * Cell), (Row * Cell)));
                }
            }
            Sheet.SavePng(Out.PathJoin((Name + ".png")));
            // one-shot animations: [first column, frames, frames per second]
            dynamic Anims = new GDict {  };
            for (int I = 0; I < Attacks.Length; I++)
            {
                int First = (6 + (I * PixelPoses.ATTACK_FRAMES));
                if (Player)
                {
                    Anims[("attack_" + Attacks[I])] = new GArray(First, PixelPoses.ATTACK_FRAMES, PixelPoses.PLAYER_ATTACK_FPS[I]);
                }
                else
                {
                    Anims["attack"] = new GArray(First, PixelPoses.ATTACK_FRAMES, ENEMY_ATTACK_FPS);
                }
            }
            Anims["down"] = new GArray((Cols - 4), 4, DOWN_FPS);
            // size: the sheet cell; body: the drawn character (what the game scales to its size in tiles)
            Info.Chars[N] = new GDict { { "size", Cell }, { "body", Size }, { "anchor", new GArray((Cell / 2.0f), Ground) }, { "anims", Anims } };
            GD.Print("drew ", N, " ", Cell, "x", Cell, ", ", Cols, " columns");
        }
        using var Fa = FileAccess.Open(Out.PathJoin("chars.json"), FileAccess.ModeFlags.Write);
        Fa.StoreString(Json.Stringify(Gd.ToVariant(Info), "\t"));
        Fa.Close();
        GetTree().Quit();
    }

    static Image Raw(dynamic Im)
    {
        return (Image)Gd.Unwrap(Im);
    }

    // Where the arms are in each view (see PixelPoses.Rig), in body pixels. The weapon arm is the character's
    // right: on the left of the screen seen from the front, in front of the body seen from the side.
    static PixelPoses.Rig Rig(string BodyName, string View, dynamic Pal)
    {
        bool Side = (View == "side");
        bool Front = View.StartsWith("down");
        Color C(string K) => Gd.MakeColor(Gd.At(Pal, K));
        PixelPoses.Rig R;
        Vector2 L, Rr;
        Rect2I El, Er;
        switch (BodyName)
        {
            case "human":
                R = new PixelPoses.Rig { ArmW = 2, HandW = 2, Arm = C("c"), Hand = C("S") };
                L = new Vector2(11, 18); Rr = new Vector2(21, 18);
                El = new Rect2I(10, 17, 2, 5); Er = new Rect2I(20, 17, 2, 5);
                if (Side)
                {
                    R.Main = new Vector2(15, 18); R.Off = new Vector2(16, 17.5f);
                    // the resting hand in front of the belly is painted over with the shirt
                    R.Patch = new Rect2I(15, 21, 2, 1); R.PatchCol = C("C");
                    return R;
                }
                break;
            case "nox":
                R = new PixelPoses.Rig { ArmW = 3, HandW = 3, Reach = 1.1f, Arm = C("N"), Hand = C("N"), Claw = C("A") };
                L = new Vector2(9.5f, 19.5f); Rr = new Vector2(23, 19.5f);
                El = new Rect2I(8, 19, 3, 6); Er = new Rect2I(22, 19, 2, 6);
                R.Mouth = (Side ? new Vector2(21.5f, 17.5f) : ((View == "down") ? new Vector2(16, 17.5f) : ((View == "down3q") ? new Vector2(18, 17.5f) : new Vector2(16, 13))));
                if (Side)
                {
                    R.Main = new Vector2(21.5f, 21.5f); R.Off = new Vector2(19, 21);
                    R.EraseMain = new Rect2I(20, 22, 3, 4);
                    return R;
                }
                break;
            default:
                R = new PixelPoses.Rig { ArmW = 4, HandW = 4, Reach = 1.4f, Arm = C("N"), Hand = C("N"), Claw = C("A") };
                L = new Vector2(11.5f, 24); Rr = new Vector2(28.5f, 24);
                El = new Rect2I(10, 24, 3, 8); Er = new Rect2I(27, 24, 3, 8);
                if (Side)
                {
                    R.Main = new Vector2(25.5f, 24); R.Off = new Vector2(22, 24);
                    R.EraseMain = new Rect2I(24, 25, 4, 6);
                    return R;
                }
                break;
        }
        // from the front the weapon arm is on the screen's left; from the back, on its right
        R.Main = (Front ? L : Rr); R.EraseMain = (Front ? El : Er);
        R.Off = (Front ? Rr : L); R.EraseOff = (Front ? Er : El);
        return R;
    }

    public dynamic Palette(dynamic N)
    {
        dynamic Pal = new GDict {  };
        foreach (dynamic P in Gd.Iter(new GArray(SKIN_L, BOOTS, NOX_PAL, Gd.At(CHARACTERS, N).Pal)))
        {
            Gd.Merge(Pal, P, true);
        }
        return Pal;
    }

    // Shut: the eyes closed (lying knocked down)
    public dynamic Compose(dynamic N, dynamic View, bool Shut = false)
    {
        dynamic Ch = Gd.At(CHARACTERS, N);
        dynamic Body = Gd.At(BODIES, Ch.Body);
        int Size = Gd.Int(Body.Size);
        dynamic Pal = this.Palette(N);
        if (Shut)
        {
            Pal["E"] = Gd.At(Pal, "s");
            Pal["Y"] = Gd.At(Pal, "n");
        }
        dynamic Im = Gd.Static(typeof(Image), "create_empty", Size, Size, false, Gd.EnumValue(typeof(Image), "FORMAT_RGBA8", 5));
        int Pad = Gd.Int(((Size - Gd.Length(Gd.At(Gd.At(Gd.At(Body, View), 1), 0))) / 2));
        // gear is the same in a 3/4 view as in the front / back view
        string GearView = ((string)View).Replace("3q", "");
        foreach (dynamic Layer in Gd.Iter(new GArray(Gd.At(Body, View), Gd.Get(Ch.Over, GearView, new GArray(0, new GArray())))))
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

    // 3/4 views facing right (the left ones are mirrored): the front / back pixel map with a few rows redrawn.
    // The face slides one pixel toward the turn and the hair shows on the far side; both eyes keep the front
    // view's size (2 x 2 for players, 2 or 3 wide for NOX), so they never look mismatched at this small size.
    public void AddThreeQuarterViews()
    {
        Turned(HUMAN, "down", new GDict {
            { 4, "..HHHHSSSSSSSh.." },
            { 5, "..HHHSSSSSSSSS.." },
            { 6, "..hHSSEESSEESS.." },
            { 7, "..hHSSEESSEESS.." },
            { 8, "..hhSSSSSSSSSs.." },
            { 9, "...hsSSSSSSSSs.." } });
        Turned(HUMAN, "up", new GDict {
            { 6, "..HHHHHHHHHHSs.." },
            { 7, "..hHHHHHHHHHSs.." },
            { 8, "..shHHHHHHHHSs.." } });
        Turned(NOX, "down", new GDict {
            { 4, ".MNNNYYNNNNYYNn." },
            { 5, ".NNNNYYNNNNYYNn." },
            { 7, ".nNNNNDTDTTDTDn." } });
        Turned(NOX, "up", new GDict {  });
        Turned(WRECKER_BODY, "down", new GDict {
            { 6, "..NNNNYYYNNNNYYYNn.." },
            { 8, "..nNNNNDTDTDTDTDNn.." } });
        Turned(WRECKER_BODY, "up", new GDict {  });
    }

    static void Turned(dynamic Body, string View, dynamic Rows)
    {
        dynamic Map = Gd.At(Body, View);
        dynamic Lines = Gd.Duplicate(Gd.At(Map, 1));
        foreach (dynamic R in Gd.Iter(Rows))
        {
            Lines[R] = Gd.At(Rows, R);
        }
        Body[(View + "3q")] = new GArray(Gd.At(Map, 0), Lines);
        Body.Split[(View + "3q")] = Gd.At(Body.Split, View);
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
}
