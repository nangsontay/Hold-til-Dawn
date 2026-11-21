using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// Pixel-art icons for every inventory item (ITM-02..13, resources, bandage, arrows) and the small projectiles,
// in the style of tools/PixelChars.cs: one letter per pixel looked up in a palette, light from the upper left,
// a 1 px dark outline added by the script. Not part of the game.
// Run: godot --headless --path . tools/PixelItems.tscn   (add -- --out=<dir> to preview elsewhere)
// Writes art/items/<item id>.png (20 x 20) and art/fx/<name>.png. The PNGs can be repainted by hand.
public partial class PixelItems : Node
{
    static readonly Color Ink = new("141019");
    const int Icon = 20;

    static readonly Dictionary<char, string> Wood = new() { ['W'] = "C08A50", ['w'] = "A0703C", ['v'] = "7A5634", ['u'] = "5A3E26", ['r'] = "E0B070" };
    static readonly Dictionary<char, string> Stone = new() { ['S'] = "D5DAE1", ['s'] = "A3A8B0", ['t'] = "7D838C", ['q'] = "565B66" };
    static readonly Dictionary<char, string> Steel = new() { ['M'] = "EEF4F8", ['m'] = "B4C4D4", ['n'] = "8FA3B8", ['o'] = "5B6B7C" };

    static Dictionary<char, string> Pal(params Dictionary<char, string>[] Parts)
    {
        var All = new Dictionary<char, string>();
        foreach (var P in Parts)
        {
            foreach (var Kv in P)
            {
                All[Kv.Key] = Kv.Value;
            }
        }
        return All;
    }

    // name -> (rows, palette)
    static readonly Dictionary<string, (string[] Rows, Dictionary<char, string> Pal)> Items = new()
    {
        ["wood"] = (new[] {
            ".WWWWWWWWW.uu...",
            ".wwwwwwwwwurru..",
            ".vvvvvvvvvurru..",
            "...........uu...",
            "...WWWWWWWWW.uu.",
            "...wwwwwwwwwurru",
            "...vvvvvvvvvurru",
            ".............uu.",
            ".WWWWWWWWW.uu...",
            ".wwwwwwwwwurru..",
            ".vvvvvvvvvurru..",
            "...........uu...",
        }, Wood),
        ["stone"] = (new[] {
            "......SSS.......",
            "....SSSSsst.....",
            "...SSSssssstt...",
            "..SSsssssssttq..",
            "..Sssssssstttq..",
            "..ssssssttttqq..",
            "...tttttttqqq...",
            "....qqqqqqq.....",
        }, Stone),
        ["herb"] = (new[] {
            "......G.........",
            ".....GGg........",
            "....GGgg..G.....",
            "....Gggj.GGg....",
            ".G...gj.GGgg....",
            "GGg..kj.Ggj.....",
            "Gggj.k..gj......",
            ".gjj.k.kj.......",
            "..jj.kkk........",
            ".....kk.........",
            ".....k..........",
            ".....k..........",
        }, new() { ['G'] = "9BE37A", ['g'] = "4FB862", ['j'] = "2E7D3E", ['k'] = "6E8A3A" }),
        // titanium: two ingots stamped "Ti" in the blue of the crystal ore (tools/PixelNature.cs), violet at the ends, so they never pass for stone
        ["titanium"] = (new[] {
            "....MMMMMMMMMM..",
            "...MmmmmmmmmmMbp",
            "..MmmTTTmmmmmMbp",
            "..MmmmTmTmmmmMbp",
            ".nnnnnTnTnnnnnbg",
            ".nnnnnnnnnnnnnpg",
            ".oooooooooooooo.",
            "................",
            ".....MMMMMMMMMM.",
            "....MmmmmmmmmMbp",
            "...nnnnnnnnnnnbg",
            "...ooooooooooooo",
        }, new() { ['M'] = "D2E6FF", ['m'] = "7FA6F5", ['n'] = "4A6AD8", ['o'] = "283A8E", ['T'] = "3550B8", ['b'] = "A98CFF", ['p'] = "7C5CE0", ['g'] = "E2D6FF" }),
        ["bandage"] = (new[] {
            "....BBBBBBBB....",
            "...BBBBBBBBBbb..",
            "..BBBBBXXBBbbcc.",
            "..BBBBXXXXBbbcc.",
            "..BBBBBXXBBbbcc.",
            "..bbbbbbbbbbbcc.",
            "...cccccccccc...",
            ".............bb.",
            "..............bb",
        }, new() { ['B'] = "FFFFFF", ['b'] = "E8E2D0", ['c'] = "B9B4AA", ['X'] = "D9534A" }),
        // recipe icons for the two crafts that make no item: the Revival kit (a medic case with a heart and a
        // charge bolt) and Base repair (the generator with a green plus); the workbench panel shows them
        ["revival_kit"] = (new[] {
            "......kkkk......",
            ".....k....k.....",
            "..GGGGGGGGGGGG..",
            ".GGGGGGGGGGGGGg.",
            ".GGGHHGGHHGGGGg.",
            ".GGHHHHHHHHGGGg.",
            ".GGHHHHYHHHGGGg.",
            ".GGGHHYYHHGGGGg.",
            ".GGGGHHYHGGGGGg.",
            ".GGGGGHHGGGGGGg.",
            ".GGGGGGGGGGGGGg.",
            ".ggggggggggggggd",
            "..dddddddddddd..",
        }, new() { ['G'] = "4FA35A", ['g'] = "3E8E4A", ['d'] = "2F6B38", ['k'] = "8FA3B8", ['H'] = "F2F2F2", ['Y'] = "F2B544" }),
        ["base_repair"] = (new[] {
            "...........P....",
            "..........PPP...",
            "...........P....",
            ".oooooooooooo...",
            ".nmmmmmmmmmmo...",
            ".nmmmmmYYmmmo...",
            ".nmmmmYYmmmmo...",
            ".nmmmYYYYYmmo...",
            ".nmmmmmYYmmmo...",
            ".nmmmmYYmmmmo...",
            ".nmmmmmmmmmmo...",
            ".oooooooooooo...",
            "..qq......qq....",
        }, new() { ['m'] = "B4C4D4", ['n'] = "8FA3B8", ['o'] = "5B6B7C", ['Y'] = "F2B544", ['P'] = "6CC070", ['q'] = "565B66" }),
        ["arrow"] = (new[] {
            "............iI..",
            "...........iiI..",
            "..........hiI...",
            ".........h......",
            "........h.......",
            ".......h........",
            "......h.........",
            ".....h..........",
            "....h...........",
            "..FFh...........",
            "..fFF...........",
            "..ff............",
        }, new() { ['h'] = "A0703C", ['i'] = "D5DAE1", ['I'] = "8FA3B8", ['F'] = "D9534A", ['f'] = "E8E3C8" }),
        ["stone_spear"] = (new[] {
            "............SS..",
            "...........SSst.",
            "..........SsstT.",
            "...........ttT..",
            "..........w.....",
            ".........w......",
            "........w.......",
            ".......w........",
            "......w.........",
            ".....w..........",
            "....v...........",
            "...v............",
            "..v.............",
        }, Pal(Stone, Wood, new() { ['T'] = "565B66" })),
        ["titanium_blade"] = (new[] {
            "............MM..",
            "...........MMm..",
            "..........MMm...",
            ".........MMm....",
            "........MMm.....",
            ".......MMm......",
            "..YY..MMm.......",
            "...YYMMm........",
            "....YYm.........",
            "...hYY.Y........",
            "..hh............",
            ".hh.............",
        }, Pal(Steel, new() { ['Y'] = "F2B544", ['h'] = "5A3E26" })),
        ["starter_bow"] = (new[] {
            "...wv...........",
            "...s.ww.........",
            "...s..ww........",
            "...s...ww.......",
            "...s....w.......",
            "...s....ww......",
            "...s.....w......",
            "...s.....v......",
            "...s.....w......",
            "...s....ww......",
            "...s....w.......",
            "...s...ww.......",
            "...s..ww........",
            "...s.ww.........",
            "...wv...........",
        }, Pal(Wood, new() { ['s'] = "E8E3C8" })),
        ["bow"] = (new[] {
            "...Mn...........",
            "...s.uu.........",
            "...s..uu........",
            "...s...vu.......",
            "...s....v.......",
            "...s....vu......",
            "...s.....X......",
            "...s.....X......",
            "...s.....X......",
            "...s....vu......",
            "...s....v.......",
            "...s...vu.......",
            "...s..uu........",
            "...s.uu.........",
            "...Mn...........",
        }, Pal(Wood, Steel, new() { ['s'] = "E8E3C8", ['X'] = "D9534A" })),
        ["hammer"] = (new[] {
            "..MMMMMMMMm.....",
            "..Mmmmmmmmno....",
            "..Mmmmmmmmno....",
            "..ooooooooo.....",
            "......Wv........",
            "......Wv........",
            "......Wv........",
            "......Wv........",
            "......Wv........",
            "......Wv........",
            "......uu........",
            "......uu........",
        }, Pal(Steel, Wood)),
        ["light_armour"] = (Vest(false, false), new() { ['L'] = "C08A50", ['l'] = "8C6A48", ['d'] = "5A3E26", ['H'] = "E0B070", ['x'] = "5A3E26" }),
        ["stone_armour"] = (Vest(true, false), new() { ['L'] = "A3A8B0", ['l'] = "7D838C", ['d'] = "565B66", ['H'] = "D5DAE1", ['x'] = "565B66" }),
        ["titanium_armour"] = (Vest(false, true), new() { ['L'] = "B4C4D4", ['l'] = "8FA3B8", ['d'] = "5B6B7C", ['H'] = "EEF4F8", ['x'] = "F2B544" }),
    };

    // One chest-piece shape for the three armours; stone shows plates, titanium shows rivets and a shine.
    static string[] Vest(bool Plates, bool Rivets)
    {
        var Rows = new List<string> {
            "...LL......LL...",
            "..LLLL....LLLL..",
            "..LHLLL..LLLLl..",
            ".LHLLLLLLLLLLLl.",
            ".LHLLLLLLLLLLLl.",
            ".lHLLLLLLLLLLll.",
            "..LLLLLLLLLLLl..",
            "..LLLLLLLLLLLl..",
            "..lLLLLLLLLLll..",
            "..llllllllllll..",
            "...dddddddddd...",
        };
        if (Plates)
        {
            Rows[4] = ".LHxxxxxxxxxxLl.";
            Rows[7] = "..LxxxxxxxxxxL..";
        }
        if (Rivets)
        {
            Rows[3] = ".LHxLLLLLLLLxLl.";
            Rows[6] = "..LxLLLLLLLLxl..";
        }
        return Rows.ToArray();
    }

    // Projectiles and effects drawn in the world.
    static readonly Dictionary<string, (string[] Rows, Dictionary<char, string> Pal)> Fx = new()
    {
        // pointing right; the game turns it to the flight direction
        ["arrow"] = (new[] {
            "F..........I.",
            "fhhhhhhhhhhiI",
            "F..........I.",
        }, new() { ['h'] = "C08A50", ['i'] = "D5DAE1", ['I'] = "8FA3B8", ['F'] = "D9534A", ['f'] = "E8E3C8" }),
        // Spitter spit
        ["spit"] = (new[] {
            "..qQQ.",
            ".QQqQQ",
            "QQQQQj",
            ".QQQjj",
            "..jj..",
        }, new() { ['Q'] = "D8E03A", ['q'] = "F6F9A8", ['j'] = "8F9620" }),
        // Bio-Cannon spore
        ["orb"] = (new[] {
            "..pPPp..",
            ".pPWWPp.",
            "pPWWPPPp",
            "pPWPPPPp",
            "pPPPPPpk",
            ".pPPPpk.",
            "..kkkk..",
        }, new() { ['P'] = "7BE36A", ['p'] = "3FA54E", ['W'] = "E8FFD8", ['k'] = "1F6B33" }),
    };

    public override void _Ready()
    {
        AddTacticalArt();
        string Out = "res://art";
        foreach (string A in OS.GetCmdlineUserArgs())
        {
            if (A.StartsWith("--out=", StringComparison.Ordinal))
            {
                Out = A[6..];
            }
        }
        DirAccess.MakeDirRecursiveAbsolute(Out.PathJoin("items"));
        DirAccess.MakeDirRecursiveAbsolute(Out.PathJoin("fx"));
        foreach (var (Name, Art) in Items)
        {
            Save(Draw(Art.Rows, Art.Pal, Icon, Icon), Out.PathJoin("items").PathJoin(Name + ".png"));
        }
        foreach (var (Name, Art) in Fx)
        {
            int W = Art.Rows.Max(R => R.Length) + 2;
            Save(Draw(Art.Rows, Art.Pal, W, Art.Rows.Length + 2), Out.PathJoin("fx").PathJoin(Name + ".png"));
        }
        GetTree().Quit();
    }

    static void Save(Image Im, string Path)
    {
        Im.SavePng(Path);
        GD.Print("drew ", Path, " ", Im.GetSize());
    }

    // Centres the pixel map on a W x H canvas and adds the dark outline.
    static Image Draw(string[] Rows, Dictionary<char, string> Pal, int W, int H)
    {
        var Im = Image.CreateEmpty(W, H, false, Image.Format.Rgba8);
        int Wide = Rows.Max(R => R.Length);
        int X0 = (W - Wide) / 2;
        int Y0 = (H - Rows.Length) / 2;
        for (int Y = 0; Y < Rows.Length; Y++)
        {
            for (int X = 0; X < Rows[Y].Length; X++)
            {
                char K = Rows[Y][X];
                if (K == '.')
                {
                    continue;
                }
                if (!Pal.TryGetValue(K, out string Hex))
                {
                    GD.PushError($"no colour for '{K}'");
                    continue;
                }
                Im.SetPixel(X0 + X, Y0 + Y, new Color(Hex));
            }
        }
        var Src = (Image)Im.Duplicate();
        for (int Y = 0; Y < H; Y++)
        {
            for (int X = 0; X < W; X++)
            {
                if (Src.GetPixel(X, Y).A > 0)
                {
                    continue;
                }
                foreach (var D in new[] { new Vector2I(1, 0), new Vector2I(-1, 0), new Vector2I(0, 1), new Vector2I(0, -1) })
                {
                    var Q = new Vector2I(X, Y) + D;
                    if (Q.X >= 0 && Q.Y >= 0 && Q.X < W && Q.Y < H && Src.GetPixel(Q.X, Q.Y).A > 0)
                    {
                        Im.SetPixel(X, Y, Ink);
                        break;
                    }
                }
            }
        }
        return Im;
    }
}
