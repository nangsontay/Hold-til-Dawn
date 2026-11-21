using Godot;
using System;
using HoldTilDawn;

public partial class Main
{
    public void DrawTacticalAreas()
    {
        foreach (dynamic A in Gd.Iter(Gd.Get(Snap, "tactical", new GArray())))
        {
            string Kind = Gd.Str(Gd.At(A, 0)); Vector2 At = (Vector2)Gd.At(A, 1);
            if (Kind == "trap")
            {
                DrawArc(At, 17f, 0, Mathf.Tau, 16, new Color("98BE56"), 2f);
                DrawItemIcon(this, "vine_trap", At, 1f);
                continue;
            }
            Color Tint = new(Kind == "heal" ? "6CE4BA" : Kind == "poison" ? "A1C64A" : "FF9B45");
            float R = Kind == "blast" ? 1.5f * T : 1.8f * T;
            float Left = Gd.Float(Gd.At(A, 2));
            DrawCircle(At, R, new Color(Tint, Kind == "blast" ? Math.Clamp(Left, 0f, 0.3f) : 0.13f));
            DrawArc(At, R, 0, Mathf.Tau, 32, new Color(Tint, 0.8f), 2f);
            for (int I = 0; I < 5; I++)
            {
                Vector2 P = At + Vector2.FromAngle(I * Mathf.Tau / 5 + (float)Clock * 0.25f) * (R * 0.55f);
                P.Y -= 3f + 3f * MathF.Sin((float)Clock * 3 + I);
                if (Kind == "heal") { DrawLine(P - Vector2.Right * 3, P + Vector2.Right * 3, Tint, 2f); DrawLine(P - Vector2.Down * 3, P + Vector2.Down * 3, Tint, 2f); }
                else DrawRect(new Rect2(P.Round(), new Vector2(3, 3)), Tint);
            }
        }
    }

    void DrawTacticalWeapon(dynamic P, Vector2 Foot, float Aim)
    {
        string W = Gd.Str(Gd.At(P, 8));
        if (!Gd.Truth(Gd.At(P, 11)) || W is not ("long_spear" or "stun_maul")) return;
        Vector2 Dir = Vector2.FromAngle(Aim), Side = Dir.Orthogonal(), Hand = Foot - new Vector2(0, 18);
        if (W == "long_spear")
        {
            Vector2 Tip = Hand + Dir * 58;
            DrawLine(Hand - Dir * 10, Tip, new Color("382B23"), 5);
            DrawLine(Hand - Dir * 10, Tip, new Color("C39058"), 3);
            DrawColoredPolygon(new[] { Tip + Dir * 13, Tip + Side * 5, Tip - Side * 5 }, new Color("DBE9D7"));
        }
        else
        {
            Vector2 Head = Hand + Dir * 30;
            DrawLine(Hand, Head, new Color("B48550"), 5);
            DrawColoredPolygon(new[] { Head + Dir * 6 + Side * 12, Head + Dir * 6 - Side * 12, Head - Dir * 6 - Side * 12, Head - Dir * 6 + Side * 12 }, new Color("A0CEE0"));
        }
    }
}
