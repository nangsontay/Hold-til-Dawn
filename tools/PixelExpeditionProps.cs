using Godot;

// Hand-authored pixel geometry using the existing Atlas palettes, anchors and outline pipeline.
public partial class PixelNature
{
    void DrawExpeditionProps()
    {
        Lm("lm_tent", 0, SurveyTent(), 12);
        Lm("lm_console", 0, ResearchConsole(), 12);
        Lm("lm_cart", 0, BrokenCart(), 10);
        Lm("lm_warning", 0, QuarantineSign(), 3);
        Lm("lm_cargo_stack", 0, CargoStack(), 12);
        Lm("lm_waterpump", 0, WaterPump(), 8);
    }
    Canvas SurveyTent()
    {
        var C = DetailCanvas(102, 75, 51, 66);
        // Side-lit orange survey canvas, dark entrance, guy lines and pegs.
        for (int Y = 10; Y < 65; Y++)
        {
            int Half = (Y - 10) * 43 / 55;
            Rect(C, 48 - Half, Y, Half + 1, 1, TIN, 3);
            Rect(C, 49, Y, Half + 1, 1, TIN, 2);
        }
        for (int Y = 28; Y < 65; Y++)
        {
            int Half = (Y - 28) * 17 / 37;
            Rect(C, 49 - Half, Y, Half * 2, 1, HULL, 0);
        }
        Line(C, new Vector2(48, 10), new Vector2(49, 65), TIMBER[4]);
        Line(C, new Vector2(18, 30), new Vector2(4, 68), TIMBER[4]);
        Line(C, new Vector2(80, 30), new Vector2(96, 68), TIMBER[4]);
        Rect(C, 3, 65, 3, 6, STEELS, 1); Rect(C, 95, 65, 3, 6, STEELS, 1);
        Rect(C, 25, 40, 9, 5, WHITEWASH, 3); Rect(C, 28, 38, 3, 9, WHITEWASH, 3);
        return FinishDetail(C);
    }
    Canvas ResearchConsole()
    {
        var C = DetailCanvas(66, 64, 33, 57);
        Box(C, 8, 35, 46, 14, 15, STEELS, 4801);
        Box(C, 14, 9, 34, 6, 25, OLIVE, 4802);
        Rect(C, 18, 16, 25, 13, GLASSY, 1);
        for (int X = 20; X < 40; X += 4) Rect(C, X, 21 + X % 5, 2, 3, GLASSY, 3);
        Rect(C, 18, 40, 21, 5, TYRES, 1);
        for (int X = 20; X < 38; X += 4) Rect(C, X, 41, 2, 1, STEELS, 3);
        Disc(C, 46, 41, 3, 2, HULL_RED[3]);
        Rect(C, 11, 52, 5, 8, STEELS, 0); Rect(C, 48, 52, 5, 8, STEELS, 0);
        Line(C, new Vector2(52, 47), new Vector2(62, 57), TYRES[0]);
        return FinishDetail(C);
    }
    Canvas BrokenCart()
    {
        var C = DetailCanvas(82, 53, 41, 46);
        Box(C, 10, 20, 54, 16, 10, TIMBER, 4811);
        for (int X = 13; X < 64; X += 10) Rect(C, X, 24, 2, 13, TIMBER, 1);
        Box(C, 23, 7, 22, 12, 13, OLIVE, 4812);
        Line(C, new Vector2(60, 32), new Vector2(79, 43), STEELS[1]);
        Wheel(C, 23, 42, 8); Wheel(C, 58, 45, 5);
        Rect(C, 47, 37, 10, 2, TIMBER, 4);
        return FinishDetail(C);
    }
    Canvas QuarantineSign()
    {
        var C = DetailCanvas(48, 64, 24, 58);
        Rect(C, 22, 15, 5, 44, TIMBER, 2);
        Box(C, 4, 7, 39, 3, 26, TIN, 4813);
        Rect(C, 7, 14, 33, 19, WHITEWASH, 2);
        for (int Y = 15; Y < 25; Y++) Rect(C, 23 - (Y - 15), Y, (Y - 15) * 2 + 1, 1, RUSTY, 2);
        Rect(C, 22, 19, 3, 4, TYRES, 0); Rect(C, 23, 25, 1, 1, TYRES, 0);
        Rect(C, 10, 29, 26, 2, TYRES, 1);
        return FinishDetail(C);
    }
    Canvas CargoStack()
    {
        var C = DetailCanvas(85, 69, 42, 61);
        Box(C, 7, 39, 70, 17, 8, TIMBER, 4821);
        Box(C, 11, 21, 29, 15, 20, BOX_BLUE, 4822);
        Box(C, 43, 24, 30, 12, 18, BOX_ORANGE, 4823);
        Box(C, 27, 6, 30, 16, 17, OLIVE, 4824);
        foreach (int X in new[] { 17, 32, 50, 66 }) Rect(C, X, 32, 2, 14, TYRES, 1);
        Rect(C, 35, 16, 9, 6, WHITEWASH, 3);
        return FinishDetail(C);
    }
    Canvas WaterPump()
    {
        var C = DetailCanvas(58, 72, 29, 64);
        Box(C, 7, 56, 44, 9, 5, CONC, 4831);
        Box(C, 20, 23, 14, 10, 33, BOX_BLUE, 4832);
        Box(C, 17, 13, 20, 10, 11, STEELS, 4833);
        Rect(C, 28, 5, 4, 12, STEELS, 3);
        Line(C, new Vector2(29, 6), new Vector2(49, 20), STEELS[3]);
        Rect(C, 34, 29, 14, 5, STEELS, 2); Rect(C, 44, 31, 4, 8, STEELS, 2);
        Disc(C, 13, 55, 9, 4, TIN[2]); Rect(C, 5, 50, 16, 5, TIN, 3);
        Stain(C, 20, 25, 15, 30, RUSTY, 4834, .15f, 3);
        return FinishDetail(C);
    }
}
