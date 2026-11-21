using Godot;
using System;
using HoldTilDawn;

// Owner: Vu Duc Viet Anh. Tonight's three supplies (src/world/GameSim.Supplies.cs): each floats over a gold
// ring with a beam of light rising from it and its own light at night, so it can be seen from far off. The
// screen edge points to them, and the top right corner shows which are in.
public partial class Main : Node2D
{
    static readonly Color SUPPLY_COL = new Color("FFD45C");
    PointLight2D[] SupplyLights;

    // snapshot rows [kind, pos, picked up]
    GArray SupplyRows() => (Gd.Get(Snap, "sup", null) as GArray) ?? new GArray();

    // called with the other lights each frame
    public void TickSupplyLights(float Night)
    {
        if (SupplyLights == null)
        {
            SupplyLights = new PointLight2D[GameSim.SUPPLY_COUNT];
            for (int I = 0; I < SupplyLights.Length; I++)
            {
                var L = new PointLight2D
                {
                    Texture = (Texture2D)Gd.Unwrap(MyLight.Texture),
                    Color = SUPPLY_COL,
                    TextureScale = (3.0f * T * 2.0f) / 256.0f,
                    Enabled = false,
                    TextureFilter = CanvasItem.TextureFilterEnum.Linear,
                    TextureRepeat = CanvasItem.TextureRepeatEnum.Disabled,
                };
                AddChild(L);
                SupplyLights[I] = L;
            }
        }
        var Rows = SupplyRows();
        for (int I = 0; I < SupplyLights.Length; I++)
        {
            bool On = (I < Rows.Count) && !Gd.Truth(((GArray)Rows[I])[2]) && (Night > 0.02f);
            SupplyLights[I].Enabled = On;
            if (On)
            {
                SupplyLights[I].Position = (Vector2)((GArray)Rows[I])[1];
                SupplyLights[I].Energy = 0.7f * Night * (0.85f + 0.15f * Mathf.Sin((float)Clock * 3.0f + I));
            }
        }
    }

    // the world pass (ground layer, under everything standing)
    public void DrawSupplies()
    {
        float Now = (float)Clock;
        foreach (GArray S in SupplyRows())
        {
            if (Gd.Truth(S[2]))
            {
                continue;
            }
            Vector2 At = (Vector2)S[1];
            // a beam of light up into the sky
            var Beam = new[] { At + new Vector2(-5, 0), At + new Vector2(5, 0), At + new Vector2(2, -150), At + new Vector2(-2, -150) };
            var Glow = new Color(2.0f, 1.6f, 0.6f, 0.45f);
            var Fade = new Color(2.0f, 1.6f, 0.6f, 0.0f);
            DrawPolygon(Beam, new[] { Glow, Glow, Fade, Fade });
            // a pulsing ring on the ground
            float P = (Now * 0.8f) % 1.0f;
            DrawSetTransform(At, 0, new Vector2(1.0f, 0.45f));
            DrawArc(Vector2.Zero, 14, 0, Mathf.Tau, 24, new Color(SUPPLY_COL, 0.9f), 2.0f);
            DrawArc(Vector2.Zero, 14 + 16 * P, 0, Mathf.Tau, 24, new Color(SUPPLY_COL, 0.7f * (1 - P)), 2.0f);
            DrawSetTransform(Vector2.Zero);
            // the supply itself, bobbing
            Vector2 Icon = At - new Vector2(0, 16 + 3 * Mathf.Sin(Now * 3.0f));
            this.Shadow(At - new Vector2(0, 2), 6);
            DrawCircle(Icon, 12, new Color(1.0f, 0.85f, 0.4f, 0.18f));
            DrawComponentIcon(this, Gd.Int(S[0]), Icon.Round(), 2.0f);
        }
    }

    // a small pixel drawing of each supply, S screen pixels per art pixel, centred on At
    public static void DrawSupplyIcon(CanvasItem C, int Kind, Vector2 At, float S, float A = 1.0f)
    {
        var Ink = new Color(0.08f, 0.07f, 0.1f, A);
        void Box(float X, float Y, float W, float H, string Hex, bool Rim = false)
        {
            var R = new Rect2(At + new Vector2(X, Y) * S, new Vector2(W, H) * S);
            if (Rim)
            {
                C.DrawRect(R.Grow(S), Ink);
            }
            C.DrawRect(R, new Color(new Color(Hex), A));
        }
        switch (Kind)
        {
            case 0:
                // radio battery: a chunky cell with a bright band and terminals
                Box(-1.5f, -6, 3, 2, "C9CDD2", true);
                Box(-3.5f, -4, 7, 9, "3E4A3C", true);
                Box(-3.5f, -1, 7, 2, "F2C230");
                Box(-2.5f, -3, 1, 6, "6E7E66");
                Box(-0.5f, 2, 1, 2, "F2C230");
                break;
            case 1:
                // signal flare: a red tube with a pale cap and a spark
                Box(-1.5f, -4, 3, 9, "D8402E", true);
                Box(-1.5f, -6, 3, 2, "F3E6C8", true);
                Box(-1.5f, 0, 3, 1, "8E2A20");
                Box(-0.5f, -8, 1, 1, "FFE38A");
                Box(-1.5f, -9, 1, 1, "FF9B45");
                Box(0.5f, -9, 1, 1, "FF9B45");
                break;
            default:
                // lab data drive: a slate case with a label and a green light
                Box(-5, -3.5f, 10, 7, "56636F", true);
                Box(-3.5f, -2, 6, 2, "9FD3DB");
                Box(-3.5f, 1, 4, 1, "3A444D");
                Box(2.5f, 1, 1, 1, "6CE48A");
                Box(4, -1, 2, 2, "8C969F");
                break;
        }
    }

    public static void DrawComponentIcon(CanvasItem C, int Kind, Vector2 At, float Scale)
    {
        void Box(float X, float Y, float W, float H, string Hex) =>
            C.DrawRect(new Rect2(At + new Vector2(X, Y) * Scale, new Vector2(W, H) * Scale), new Color(Hex));
        if (Kind == 0)
        {
            foreach (int X in new[] { -5, 1 })
            {
                Box(X - 1, -5, 6, 11, "171D22"); Box(X, -4, 4, 9, "AAB7B6");
                Box(X + 1, -6, 2, 2, "D8DFD5"); Box(X, 1, 4, 3, "507856");
            }
        }
        else if (Kind == 1)
        {
            Box(-5, -3, 10, 9, "4A3527"); Box(-4, -2, 8, 7, "B8955C");
            Box(-3, -5, 6, 4, "6A4940"); Box(-2, -3, 4, 2, "E7B24F");
            Box(-1, 1, 2, 3, "E7B24F");
        }
        else
        {
            Box(-6, -5, 12, 10, "171D22"); Box(-5, -4, 10, 8, "386C4C");
            Box(-2, -2, 4, 4, "222D36"); Box(-4, -3, 2, 1, "C2A45D");
            Box(2, 1, 2, 1, "C2A45D");
            for (int X = -4; X <= 3; X += 2) Box(X, 4, 1, 2, "C2A45D");
        }
    }

    // top right at night, under the timer: three slots, lit as each comes in. Right is the panel's right edge.
    public void DrawSupplyRow(CanvasItem C, float Right, float Y)
    {
        var Rescue = Gd.Get(Snap, "rescue", null) as GArray;
        if (Rescue == null) return;
        var Box = new Rect2(Right - 174, Y + 12, 174, 44);
        dynamic Cw = Gd.Wrap(C);
        C.DrawRect(Box, new Color("192027"));
        C.DrawRect(Box, SUPPLY_COL, false, 1);
        Text(Cw, Box.Position + new Vector2(6, 12), "RESCUE - craft all 3 by day", 10, SUPPLY_COL);
        for (int I = 0; I < 3; I++)
        {
            bool Done = Gd.Truth(Gd.At(Rescue[1], I));
            bool Found = Gd.Int(Gd.At(Rescue[0], I)) > 0;
            var Slot = new Rect2(Box.Position + new Vector2(6 + I * 55, 18), new Vector2(51, 22));
            C.DrawRect(Slot, Done ? new Color("285637") : new Color("30363D"));
            DrawSupplyIcon(C, I, Slot.Position + new Vector2(11, 11), 1, Done || Found ? 1 : .4f);
            Text(Cw, Slot.Position + new Vector2(22, 15), Done ? "OK" : Found ? "C" : "?", 10, Done ? Colors.LightGreen : SUPPLY_COL);
        }

    }
}
