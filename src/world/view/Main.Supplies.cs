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
            DrawSupplyIcon(this, Gd.Int(S[0]), Icon.Round(), 2.0f);
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

    // top right at night, under the timer: three slots, lit as each comes in. Right is the panel's right edge.
    public void DrawSupplyRow(CanvasItem C, float Right, float Y)
    {
        var Rows = SupplyRows();
        if (Rows.Count == 0)
        {
            return;
        }
        var Box = new Rect2(Right - 112, Y, 112, 30);
        dynamic Cw = Gd.Wrap(C);
        this.Panel(Cw, Box);
        for (int I = 0; I < Rows.Count; I++)
        {
            var S = (GArray)Rows[I];
            bool Got = Gd.Truth(S[2]);
            var Slot = new Rect2(Box.Position.X + 6 + I * 34, Y + 5, 30, 20);
            C.DrawRect(Slot, Got ? new Color(0.25f, 0.5f, 0.3f) : new Color(0.15f, 0.16f, 0.2f));
            C.DrawRect(Slot, Got ? new Color("3DBE5A") : new Color(1, 1, 1, 0.25f), false, 1);
            DrawSupplyIcon(C, Gd.Int(S[0]), Slot.GetCenter().Round(), 1.0f, Got ? 1.0f : 0.35f);
        }
    }
}
