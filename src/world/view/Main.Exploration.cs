using Godot;
using System;

public partial class Main
{
    Vector2[] WashoutBank, WashoutWater, WashoutDeep;
    static Vector2[] WashoutEdge(float Width, float Length)
    {
        var Points = new Vector2[42];
        for (int I = 0; I <= 20; I++)
        {
            float Y = -Length + I * Length / 10;
            float Tip = Mathf.Clamp((Length - MathF.Abs(Y)) / 24, .15f, 1);
            float Bend = 5 * MathF.Sin(I * .8f);
            Points[I] = new Vector2(-Width * Tip + Bend - I % 3 * 2, Y).Round();
            Points[41 - I] = new Vector2(Width * Tip + Bend + (I + 1) % 3 * 2, Y).Round();
        }
        return Points;
    }
    void DrawCrossings(WorldGeometry W, Rect2 View)
    {
        WashoutBank ??= WashoutEdge(45, 286);
        WashoutWater ??= WashoutEdge(34, 277);
        WashoutDeep ??= WashoutEdge(23, 261);
        foreach (var C in W.Crossings)
        {
            if (!View.Grow(10 * 32).HasPoint(C.At)) continue;
            DrawSetTransform(C.At, C.Along.Angle());
            FillStatic(WashoutBank, new Color("534C37"), false);
            FillStatic(WashoutWater, new Color("396772"), false);
            FillStatic(WashoutDeep, new Color("274A52"), false);
            for (int I = 0; I < 15; I++)
            {
                float Y = -252 + I * 35;
                float X = (I % 3 - 1) * 12;
                DrawLine(new Vector2(X, Y), new Vector2(X + 6, Y), new Color("628D8D"), 1);
                DrawRect(new Rect2(I % 2 == 0 ? -43 : 42, Y - 6, 4, 5), new Color("8C896B"));
            }
            // Mossy stone abutments, timber deck and two handrails. The deck matches the physics gap.
            foreach (int End in new[] { -1, 1 })
                DrawRect(new Rect2(End * 55 - 10, -49, 20, 98), new Color("8E8B72"));
            DrawRect(new Rect2(-65, -49, 130, 98), new Color("49392B"));
            for (int X = -63; X < 64; X += 9)
            {
                DrawRect(new Rect2(X, -47, 7, 94), new Color(X % 3 == 0 ? "A78755" : "8D714B"));
                DrawLine(new Vector2(X, -46), new Vector2(X, 46), new Color("C0A171"), 1);
                foreach (int Side in new[] { -1, 1 }) DrawRect(new Rect2(X + 2, Side * 39, 2, 2), new Color("3A3430"));
            }
            foreach (int Side in new[] { -1, 1 })
            {
                DrawLine(new Vector2(-69, Side * 48), new Vector2(69, Side * 48), new Color("C4AD78"), 3);
                foreach (int X in new[] { -64, 0, 64 }) DrawRect(new Rect2(X, Side * 48 - 4, 5, 9), new Color("5E4B32"));
            }
            DrawSetTransform(Vector2.Zero);
        }
    }
}
