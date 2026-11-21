using Godot;
using System;
using HoldTilDawn;

// Owner: Vu Duc Viet Anh. What the landmarks of src/world/WorldLandmarks.cs lay flat on the ground layer: the
// cracked concrete of the Ruined Base and the Container Yard with grass in the joints, the Old Fishing Pier and
// the South Cape platform (sprites from tools/PixelLandmarks.cs), the stone steps up the Northwind Slope, the
// coral reef under the Coral Lookout and the lily pads on the Rainwater Pond. Their standing parts (walls,
// containers, the ship, the bluff...) are depth-sorted scenery like the trees.
public partial class Main : Node2D
{
    static readonly Color SLAB = new Color("8B8D88"), SLAB_LIT = new Color("A2A49E"), SLAB_JOINT = new Color("6C6E6A"), SLAB_EDGE = new Color("55574F");
    static readonly Color STEP = new Color("B3AE98"), STEP_LIT = new Color("D3CEB8"), STEP_SHADE = new Color(0.1f, 0.1f, 0.06f, 0.4f);
    static readonly Color[] CORAL = { new Color("D9788A"), new Color("E09A5A"), new Color("5FB8A8"), new Color("9A7AC8"), new Color("E3C46A") };
    static readonly Color LILY = new Color("4E8C46"), LILY_LIT = new Color("6FAE5A");

    static float H01(int X, int Y) => (float)(((uint)(X * 374761393 + Y * 668265263) ^ 0x5bd1e995u) % 1000u) / 1000.0f;

    void DrawLandmarkGround(WorldGeometry W, Rect2 View)
    {
        DrawCrossings(W, View);
        float Tl = (float)T;
        foreach (var (F, Drawn) in W.Floors)
            if (Drawn && F.Intersects(View)) DrawSlab(F);
        foreach (var (P, Name) in W.LmGround)
            if (View.Grow(10 * Tl).HasPoint(P)) this.Sprite(Name, P, 1.0f, COLD);
        foreach (var (At, Across) in W.Steps)
        {
            if (!View.HasPoint(At)) continue;
            Vector2 A = (At - Across * 0.55f * Tl).Round(), B = (At + Across * 0.55f * Tl).Round();
            DrawLine(A + new Vector2(0, 3), B + new Vector2(0, 3), STEP_SHADE, 3);
            DrawLine(A, B, STEP, 5);
            DrawLine(A + new Vector2(0, -2), B + new Vector2(0, -2), STEP_LIT, 1);
        }
        float Clk = (float)Clock;
        for (int I = 0; I < W.Coral.Count; I++)
        {
            Vector2 P = W.Coral[I].Round();
            if (!View.HasPoint(P)) continue;
            Color Col = CORAL[I % CORAL.Length];
            // seen through the water: soft, and shimmering a little
            Col.A = 0.5f + 0.1f * MathF.Sin(Clk * 1.3f + I);
            int Lobes = 3 + I % 3;
            for (int K = 0; K < Lobes; K++)
            {
                Vector2 Q = P + Vector2.FromAngle(K * 2.4f + I) * (3 + (I * 7 + K * 3) % 6);
                DrawCircle(Q.Round(), 3 + (I + K) % 3, Col);
            }
            var Lit = Col.Lightened(0.35f);
            DrawRect(new Rect2(P + new Vector2(-1, -2), new Vector2(2, 1)), Lit);
        }
    }

    // a concrete slab: joints every two tiles, a darker edge, cracks and grass growing through
    void DrawSlab(Rect2 F)
    {
        float Tl = (float)T;
        DrawRect(F.Grow(2), SLAB_EDGE);
        DrawRect(F, SLAB);
        // a few panels lighter or darker
        for (float Y = F.Position.Y; Y < F.End.Y - 1; Y += 2 * Tl)
            for (float X = F.Position.X; X < F.End.X - 1; X += 2 * Tl)
            {
                float H = H01((int)X, (int)Y);
                var Panel = new Rect2(X, Y, Mathf.Min(2 * Tl, F.End.X - X), Mathf.Min(2 * Tl, F.End.Y - Y));
                if (H > 0.7f) DrawRect(Panel, SLAB_LIT);
                else if (H < 0.18f) DrawRect(Panel, new Color("7D7F7A"));
                // a crack across the panel
                if (H > 0.45f && H < 0.65f)
                {
                    Vector2 A = Panel.Position + new Vector2(Panel.Size.X * H, 2);
                    Vector2 B = A + new Vector2(10 - 20 * H01((int)Y, (int)X), Panel.Size.Y * 0.45f);
                    Vector2 C = B + new Vector2(8 * (H - 0.5f) * 10, Panel.Size.Y * 0.4f);
                    DrawLine(A.Round(), B.Round(), SLAB_JOINT, 1);
                    DrawLine(B.Round(), C.Round(), SLAB_JOINT, 1);
                }
            }
        for (float X = F.Position.X + 2 * Tl; X < F.End.X - 1; X += 2 * Tl)
            DrawLine(new Vector2(X, F.Position.Y).Round(), new Vector2(X, F.End.Y).Round(), SLAB_JOINT, 1);
        for (float Y = F.Position.Y + 2 * Tl; Y < F.End.Y - 1; Y += 2 * Tl)
            DrawLine(new Vector2(F.Position.X, Y).Round(), new Vector2(F.End.X, Y).Round(), SLAB_JOINT, 1);
        // grass in the joints and along the edge
        for (int I = 0; I < (int)(F.Size.X * F.Size.Y / (Tl * Tl) * 0.8f); I++)
        {
            float Hx = H01(I, (int)F.Position.X), Hy = H01((int)F.Position.Y, I);
            bool Edge = I % 3 == 0;
            Vector2 P = Edge
                ? new Vector2(F.Position.X + Hx * F.Size.X, (Hy > 0.5f) ? F.Position.Y + 1 : F.End.Y - 2)
                : new Vector2(F.Position.X + Mathf.Round(Hx * F.Size.X / (2 * Tl)) * 2 * Tl, F.Position.Y + Hy * F.Size.Y);
            if (!F.HasPoint(P)) continue;
            P = P.Round();
            DrawRect(new Rect2(P, new Vector2(1, 3)), new Color("5F7A3A"));
            DrawRect(new Rect2(P + new Vector2(1, 1), new Vector2(1, 2)), new Color("7A9648"));
            DrawRect(new Rect2(P + new Vector2(-1, 1), new Vector2(1, 2)), new Color("4C6530"));
        }
    }

    void DrawLilies(WorldGeometry.Pond Pd)
    {
        for (int I = 0; I < 9; I++)
        {
            Vector2 P = (Pd.Centre + Vector2.FromAngle(I * 2.3f + 0.6f) * Pd.Radius * (0.35f + 0.07f * I) * new Vector2(1.2f, 0.85f)).Round();
            float Rr = 4 + I % 3;
            DrawCircle(P + new Vector2(1, 1), Rr, new Color(0.05f, 0.1f, 0.12f, 0.35f));
            DrawCircle(P, Rr, LILY);
            DrawCircle(P + new Vector2(-1, -1), Rr - 2, LILY_LIT);
            // the notch
            DrawLine(P, P + Vector2.FromAngle(I * 1.7f) * Rr, new Color("2F5A75"), 2);
            if (I % 4 == 1)
            {
                DrawRect(new Rect2(P + new Vector2(-1, -2), new Vector2(3, 2)), new Color("F0B8D0"));
                DrawRect(new Rect2(P + new Vector2(0, -3), new Vector2(1, 1)), new Color("FFF0F6"));
            }
        }
    }
}
