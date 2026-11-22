using Godot;
using HoldTilDawn;

// Shared by every idle, walking, attack and fallen frame through PixelChars.Compose.
public partial class PixelChars
{
    static void ConfigureEnemyDesigns()
    {
        Gd.At(CHARACTERS, "grunt").Pal = new GDict {
            { "N", "BA4E32" }, { "n", "713025" }, { "M", "ED8D48" },
            { "Y", "FFE6A0" }, { "A", "F5D9AE" }, { "a", "AF855C" } };
        Gd.At(CHARACTERS, "grunt").Over = new GDict();
        Gd.At(CHARACTERS, "spitter").Pal = new GDict {
            { "N", "609942" }, { "n", "304F31" }, { "M", "A0C959" },
            { "Y", "FFE56B" }, { "A", "DDD789" }, { "a", "6B783A" },
            { "Q", "CFED49" }, { "q", "F4FFAA" }, { "j", "6C902F" } };
        Gd.At(CHARACTERS, "spitter").Over = new GDict();
        Gd.At(CHARACTERS, "wrecker").Pal = new GDict {
            { "N", "577787" }, { "n", "304551" }, { "M", "8BAAB4" },
            { "Y", "FFCF65" }, { "R", "809EAA" }, { "r", "435E6D" },
            { "A", "C8D4CC" }, { "a", "71858B" } };

        // A narrow head, long throat and hunched abdomen replace the Grunt's round skull.
        dynamic Spitter = Gd.Duplicate(NOX);
        Spitter["down"] = new GArray(7, new GArray(
            "......MMMM......", ".....MNNNNn.....", ".....NYYNYN.....", ".....NNNNNn.....",
            ".....nDDDDn.....", "......NQNN......", "......nNNn......", "......nNNn......",
            ".....MNQNNn.....", "....MNNQNNNn....", "...MNNNNNNNNn...", "MNnNNNNNNNNNNNn",
            "MNn.nNNNNNNn.Nn", "MNn.nNNNNNNn.Nn", "NNn.nNNNNNNn.Nn", "Nn..nNNNNNNn.nn",
            "AA...nNnnNNn..AA", ".....nNn.nNn....", ".....nNn.nNn....", "....nnNn.nNnn...",
            "....nnnn.nnnn...", "................"));
        Spitter["up"] = new GArray(7, new GArray(
            "......MMMM......", ".....MNNNNn.....", ".....NNNNNn.....", ".....NNNNNn.....",
            ".....nNNNNn.....", "......NNNN......", "......nNNn......", "......nNNn......",
            ".....MNNNNn.....", "....MNNNNNNn....", "...MNNNNNNNNn...", "MNnNNNNNNNNNNNn",
            "MNn.nNNNNNNn.Nn", "MNn.nNNNNNNn.Nn", "NNn.nNNNNNNn.Nn", "Nn..nNNNNNNn.nn",
            "AA...nNnnNNn..AA", ".....nNn.nNn....", ".....nNn.nNn....", "....nnNn.nNnn...",
            "....nnnn.nnnn...", "................"));
        Spitter["side"] = new GArray(7, new GArray(
            ".......MMMM.....", "......MNNNNn....", "......NNNYNn....", "......NNNNNNnn..",
            "......nNNNDDDDn.", ".......nNNn.....", ".......nNNn.....", ".......nNNn.....",
            "......MNNNn.....", ".....MNNNNn.....", "....MNNNNNNn....", "...MNNNNNNNNNn..",
            "...nNNNNNNnNNn..", "...nNNNNNn.nNn..", "...nNNnnNn.nNn..", "...nNn..nNn.AAA.",
            "...nNn..nNn.....", "...nNn..nNn.....", "...nNn..nNn.....", "..nnNn.nnNn.....",
            "..nnnn.nnnn.....", "................"));
        BODIES["spitter"] = Spitter;
        Gd.At(CHARACTERS, "spitter").Body = "spitter";
    }

    static void PaintEnemyDetails(string Name, string View, Image Im, bool Shut)
    {
        if (Name != "grunt" && Name != "spitter" && Name != "wrecker") return;
        bool Side = View == "side", Back = View.StartsWith("up");
        void Box(int X, int Y, int W, int H, string Hex) => Im.FillRect(new Rect2I(X, Y, W, H), new Color(Hex));
        if (Name == "grunt")
        {
            // Swept-back bony crest and exposed ribs: a fast, lean claw fighter.
            for (int I = 0; I < 3; I++)
            {
                int X = Side ? 10 + I * 3 : 11 + I * 4;
                Box(X, 7 + I % 2, 2, 5, "F5D9AE");
                Box(X, 6 + I % 2, 1, 2, "FFE8C2");
            }
            for (int I = 0; I < 3; I++)
                Box(Side ? 15 : 13, 20 + I * 2, Side ? 4 : 7, 1, Back ? "ED8D48" : "E6AB72");
        }
        else if (Name == "spitter")
        {
            // Large segmented acid sacs have their own silhouette, visible from every direction.
            foreach (var P in Side ? new[] { new Vector2I(10, 16), new Vector2I(9, 20) }
                : new[] { new Vector2I(10, 17), new Vector2I(21, 17) })
            {
                Box(P.X - 2, P.Y - 2, 5, 5, "6C902F");
                Box(P.X - 1, P.Y - 3, 3, 7, "CFED49");
                Box(P.X - 1, P.Y - 2, 2, 2, "F4FFAA");
                Box(P.X - 2, P.Y + 1, 5, 1, "8AAA36");
            }
            if (!Back) Box(Side ? 24 : 16, 13, 1, 3, "CFED49");
        }
        else
        {
            // Square shoulder slabs and cracked breastplate distinguish the heavy demolisher.
            foreach (int X in Side ? new[] { 23 } : new[] { 8, 27 })
            {
                Box(X, 20, 6, 7, "435E6D");
                Box(X, 19, 6, 2, "C8D4CC");
                Box(X, 21, 2, 4, "9EB8BF");
            }
            Box(Side ? 22 : 16, 25, Side ? 3 : 8, 5, "435E6D");
            Box(Side ? 23 : 18, 25, 1, 3, "C8D4CC");
            Box(Side ? 24 : 19, 28, 2, 1, "C8D4CC");
        }
    }
}
