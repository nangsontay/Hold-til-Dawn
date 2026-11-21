using System.Collections.Generic;

// Uses the same hand-authored pixel maps and outline pass as the existing item art.
public partial class PixelItems
{
    static void AddTacticalArt()
    {
        Items["long_spear"] = (new[] {
            ".............MM.", "............MMm.", "...........MMm..", "............n...", "...........w....", "..........w.....", ".........w......", "........w.......", ".......w........", "......w.........", ".....w..........", "....w...........", "...v............", "..v.............", ".v..............", "v..............."
        }, Pal(Wood, Steel));
        Items["stun_maul"] = (new[] {
            ".......MMMMMMM..", "......MMMMMMMMm.", "......mMMnnnMMm.", "......mMMnYnMMm.", "......mMMnnnMMm.", ".......nnnnnnn..", ".........w......", "........w.......", ".......w........", "......w.........", ".....w..........", "....v...........", "...v............", "..v............."
        }, Pal(Wood, Steel, new() { ['Y'] = "68EBE0" }));
        Items["vine_trap"] = (new[] {
            "...G......G.....", "..Gg......gG....", "...gGGGGGGg.....", "..Gg......gG....", ".Gg..wwww..gG...", ".Gg.w....w.gG...", "..Ggw....wgG....", "...gwwwwwwg.....", "..Gg......gG....", "...GGggggGG.....", ".....w..w.......", "....w....w......"
        }, Pal(Wood, new() { ['G'] = "B7D879", ['g'] = "57894B" }));
        Items["poison_flask"] = Flask("B7D953", "648E34", "D7EE8C", false);
        Items["healing_flask"] = Flask("74E5CB", "319A91", "F2FFF1", true);
        Items["haste_tonic"] = (new[] {
            ".....wwww.......", ".....vvvv.......", ".....M..M.......", "....MM..MM......", "...MaaaaaaM.....", "...MaaYYaam.....", "...MaYYaaam.....", "...MaYYYYam.....", "...MaaaYaam.....", "...MaaYaaam.....", "....mmmmmm......"
        }, Pal(Wood, Steel, new() { ['a'] = "EDAA49", ['Y'] = "FFF3B4" }));
        Items["gathering_kit"] = (new[] {
            "....vvvvvvvv....", "...vwwwwwwwwv...", "...wWwwwwwwwv...", "...wWwwwwwwwv...", "...wWWWwwwwwv...", "...vwwwwwwwwv...", "....vvvvvvvv....", "..M....v........", ".MMM...v..MM....", "..Mmm..v.MMmm...", "...w...v..mm....", "...w...v..w.....", "...w......w.....", "...v......v....."
        }, Pal(Wood, Steel));
        Items["explosive_arrow"] = (new[] {
            "............YY..", "...........YRRR.", "..........hRrRR.", ".........h..rr..", "........h.......", ".......h........", "......h.........", ".....h..........", "....h...........", "..FFh...........", "..fFF...........", "..ff............"
        }, new() { ['h'] = "C08A50", ['Y'] = "FFE490", ['R'] = "F09145", ['r'] = "A84432", ['F'] = "D9534A", ['f'] = "E8E3C8" });
        Items["repair_kit"] = (new[] {
            ".....MMMMMM.....", "....M......m....", "..MMMMMMMMMMMM..", ".Mmmmmmmmmmmmmn.", ".MmmYYmmmmmmYmn.", ".MmmYYmmmmmYYmn.", ".MmmmmYYmmYYmmn.", ".MmmmmmYYYYmmmn.", ".MmmmmmmYYmmmmn.", ".Mmmmmmmmmmmmmn.", "..nnnnnnnnnnnn.."
        }, Pal(Steel, new() { ['Y'] = "F5BC65" }));
        Fx["explosive_arrow"] = (new[] { "F.........YY.", "fhhhhhhhhhRRR", "F.........rr." },
            new() { ['F'] = "D9534A", ['f'] = "E8E3C8", ['h'] = "C08A50", ['Y'] = "FFE490", ['R'] = "F09145", ['r'] = "A84432" });
    }

    static (string[] Rows, Dictionary<char, string> Pal) Flask(string Bright, string Dark, string Mark, bool Cross)
    {
        var Rows = new[] { ".....wwww.......", ".....vvvv.......", ".....M..M.......", "....MM..MM......", "...MaaaaaaM.....", "..MaaaXXaaam....", "..MaaXXXXaam....", "..MaaaXXaaam....", "..Maaaaaaaam....", "...mmmmmmmm....." };
        if (!Cross) { Rows[5] = "..MaaXaaXaam...."; Rows[6] = "..MaaaXXaaam...."; Rows[7] = "..MaaXaaXaam...."; }
        return (Rows, Pal(Wood, Steel, new() { ['a'] = Bright, ['m'] = Dark, ['X'] = Mark }));
    }
}
