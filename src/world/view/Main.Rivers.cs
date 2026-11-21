using Godot;
using System;
using System.Collections.Generic;
using HoldTilDawn;

// Owner: Vu Duc Viet Anh. Draws the two rivers of src/world/IslandRivers.cs on the ground layer, over the paths
// (a path the water cuts ends at the bank): a muddy bank, the water, a deeper channel down the middle and
// ripples drifting downstream, running out over the beach into the sea. Then the bridges: stone abutments on
// both banks, a plank deck on two beams, piles in the water under it, a rail on each side and a lantern on
// each corner post. Light from the upper left, like the rest of the island.
public partial class Main : Node2D
{
    static readonly Color RIVER_MUD = new Color("4E5636"), RIVER_MUD_EDGE = new Color("5F6A3E"), RIVER_WATER = new Color("3A6E85"),
        RIVER_DEEP = new Color("2E5C74"), RIVER_RIM = new Color(0.05f, 0.09f, 0.1f, 0.4f), RIVER_GLINT = new Color(0.7f, 0.86f, 0.92f, 0.7f);

    sealed class RiverLook
    {
        public Vector2[] Bank, Water, Deep, RimL, RimR, EdgeL, EdgeR;
        public Vector2[] Path;
        public Rect2 Box;
    }
    List<RiverLook> RiverLooks;

    static Vector2[] Ribbon(Vector2[] Path, float Half)
    {
        var L = Offset(Path, -Half);
        var R = Offset(Path, Half);
        var Out = new Vector2[L.Length + R.Length];
        L.CopyTo(Out, 0);
        for (int I = 0; I < R.Length; I++) Out[L.Length + I] = R[R.Length - 1 - I];
        return Out;
    }

    // the stretch of a river from where it leaves the sea to where it reaches it again, Out tiles past the coast
    static Vector2[] Landward(Vector2[] Path, float Out)
    {
        int A = 0, B = Path.Length - 1;
        while (A < B && IslandMap.Depth(Path[A]) < -Out) A++;
        while (B > A && IslandMap.Depth(Path[B]) < -Out) B--;
        return Path[A..(B + 1)];
    }

    void BuildRiverLooks()
    {
        float Tl = (float)T;
        RiverLooks = new List<RiverLook>();
        foreach (var Path in IslandMap.Rivers)
        {
            var Wet = Landward(Path, 1.5f * Tl);
            var Dry = Landward(Path, -0.6f * Tl);
            RiverLooks.Add(new RiverLook
            {
                Path = Wet,
                Bank = Ribbon(Dry, IslandMap.RIVER_HALF + IslandMap.RIVER_BANK),
                Water = Ribbon(Wet, IslandMap.RIVER_HALF),
                Deep = Ribbon(Wet, IslandMap.RIVER_HALF * 0.45f),
                RimL = Offset(Dry, -IslandMap.RIVER_HALF), RimR = Offset(Dry, IslandMap.RIVER_HALF),
                EdgeL = Offset(Dry, -(IslandMap.RIVER_HALF + IslandMap.RIVER_BANK)), EdgeR = Offset(Dry, IslandMap.RIVER_HALF + IslandMap.RIVER_BANK),
                Box = Bounds(Path, IslandMap.RIVER_HALF + IslandMap.RIVER_BANK + Tl),
            });
        }
    }

    public void DrawRivers(Rect2 View)
    {
        if (RiverLooks == null) BuildRiverLooks();
        float Clk = (float)Clock;
        foreach (var R in RiverLooks)
        {
            if (!R.Box.Intersects(View)) continue;
            FillStatic(R.Bank, RIVER_MUD * 1.08f, true);
            DrawPolyline(R.EdgeL, RIVER_MUD_EDGE, 2);
            DrawPolyline(R.EdgeR, RIVER_MUD_EDGE, 2);
            FillStatic(R.Water, RIVER_WATER, false);
            FillStatic(R.Deep, RIVER_DEEP, false);
            DrawPolyline(R.RimL, RIVER_RIM, 2);
            DrawPolyline(R.RimR, RIVER_RIM, 2);
            // ripples drifting downstream (south), only those in view
            var P = R.Path;
            int Shift = (int)(Clk * 3.0f);
            for (int I = (Shift % 5); I < P.Length - 1; I += 5)
            {
                Vector2 At = P[I];
                if (!View.HasPoint(At)) continue;
                Vector2 Along = (P[I + 1] - At).Normalized();
                Vector2 Across = new Vector2(-Along.Y, Along.X);
                int K = I / 5 + Shift / 5;
                float Side = ((K * 7) % 5 - 2) * 0.32f * IslandMap.RIVER_HALF;
                var Col = RIVER_GLINT;
                Col.A *= 0.35f + 0.35f * MathF.Sin(Clk * 2.0f + K);
                Vector2 Q = (At + Across * Side).Round();
                DrawLine(Q, (Q + Along * 5).Round(), Col, 1);
            }
        }
        for (int I = 0; I < IslandMap.Bridges.Count; I++)
        {
            Rect2 Deck = IslandMap.BridgeDeck(I);
            if (Deck.Grow(2 * (float)T).Intersects(View)) DrawBridge(Deck.GetCenter(), Clk);
        }
    }

    // a bridge east-west across its river, centred on C
    void DrawBridge(Vector2 C, float Clk)
    {
        float L = Mathf.Round(IslandMap.BRIDGE_HALF_LEN), W = Mathf.Round(IslandMap.BRIDGE_HALF_W), Rh = IslandMap.RIVER_HALF;
        DrawSetTransform(C.Round());
        // shadow on the water and the piles under the deck, with a ring of ripples round each
        DrawRect(new Rect2(-Rh + 4, W + 2, 2 * Rh - 4, 14), new Color(0, 0, 0, 0.28f));
        foreach (float X in new[] { -Mathf.Round(Rh * 0.45f), Mathf.Round(Rh * 0.45f) })
        {
            DrawRect(new Rect2(X - 5, W - 4, 10, 18), new Color("3F3122"));
            DrawRect(new Rect2(X - 5, W - 4, 3, 18), new Color("5E4A33"));
            float Wob = MathF.Sin(Clk * 2.2f + X) * 1.5f;
            DrawArc(new Vector2(X, W + 15), 8 + Wob, 0, MathF.PI, 8, new Color(0.75f, 0.88f, 0.92f, 0.45f), 1);
        }
        // stone abutments on both banks: blocks, a pale top edge and a dark front face
        foreach (int End in new[] { -1, 1 })
        {
            float X0 = End < 0 ? -L : Rh - 8, X1 = End < 0 ? -Rh + 8 : L;
            var Block = new Rect2(X0, -W - 6, X1 - X0, 2 * W + 12);
            DrawRect(Block, new Color("858069"));
            DrawRect(new Rect2(X0, W + 6, X1 - X0, 7), new Color("5A5646"));
            for (float Y = -W - 6, Row = 0; Y < W + 6; Y += 10, Row++)
            {
                DrawLine(new Vector2(X0, Y), new Vector2(X1, Y), new Color("6A6655"), 1);
                for (float X = X0 + (Row % 2 == 0 ? 6 : 14); X < X1; X += 16)
                    DrawLine(new Vector2(X, Y), new Vector2(X, Mathf.Min(Y + 10, W + 6)), new Color("6A6655"), 1);
            }
            DrawLine(new Vector2(X0, -W - 6), new Vector2(X1, -W - 6), new Color("B4AE93"), 2);
            DrawRect(new Rect2(X0, W + 13, X1 - X0, 2), new Color(0, 0, 0, 0.25f));
        }
        // the deck: planks across it on a dark frame, worn lighter down the middle where everyone walks
        float D0 = -L + 8, D1 = L - 8;
        DrawRect(new Rect2(D0, -W + 2, D1 - D0, 2 * W - 4), new Color("3A2D21"));
        string[] Wood = { "A47F4F", "967349", "8B6A43", "AB8A59", "9C7A4C" };
        int N = 0;
        for (float X = D0 + 1; X < D1 - 1; X += 9, N++)
        {
            int H = (N * 37 + 11) % 23;
            var Plank = new Color(Wood[H % Wood.Length]);
            if (H == 0) Plank = Plank.Darkened(0.2f);
            float Wd = Mathf.Min(8, D1 - 1 - X);
            DrawRect(new Rect2(X, -W + 4, Wd, 2 * W - 8), Plank);
            DrawLine(new Vector2(X, -W + 4), new Vector2(X, W - 4), Plank.Lightened(0.18f), 1);
            DrawRect(new Rect2(X + 3, -W + 8, 2, 2), new Color("3A3430"));
            DrawRect(new Rect2(X + 3, W - 10, 2, 2), new Color("3A3430"));
            if (H % 7 == 3) DrawLine(new Vector2(X + 2, -6 + H), new Vector2(X + 2, 4 + H), Plank.Darkened(0.25f), 1);
        }
        DrawRect(new Rect2(D0, -16, D1 - D0, 32), new Color(1, 1, 0.9f, 0.06f));
        // the two beams along its sides; the near one shows its face
        DrawRect(new Rect2(D0 - 2, -W, D1 - D0 + 4, 6), new Color("5C4630"));
        DrawLine(new Vector2(D0 - 2, -W), new Vector2(D1 + 2, -W), new Color("8C6C46"), 1);
        DrawRect(new Rect2(D0 - 2, W - 6, D1 - D0 + 4, 6), new Color("5C4630"));
        DrawRect(new Rect2(D0 - 2, W, D1 - D0 + 4, 5), new Color("3A2C1E"));
        // rails: posts with a top rail and a lower rail, the far side first
        foreach (int Side in new[] { -1, 1 })
        {
            float Base = Side < 0 ? -W + 2 : W - 2;
            float Top = Base - 15;
            int Posts = 7;
            for (int K = 0; K < Posts; K++)
            {
                float X = Mathf.Round(Mathf.Lerp(D0 + 6, D1 - 6, (float)K / (Posts - 1)));
                bool Corner = K == 0 || K == Posts - 1;
                float Pw = Corner ? 7 : 5;
                DrawRect(new Rect2(X - Pw / 2, Top - (Corner ? 3 : 0), Pw, Base - Top + (Corner ? 3 : 0)), new Color("54412C"));
                DrawRect(new Rect2(X - Pw / 2, Top - (Corner ? 3 : 0), 1, Base - Top + (Corner ? 3 : 0)), new Color("8A6C47"));
                if (Corner)
                {
                    // a lantern on each corner post, glowing after dark
                    DrawRect(new Rect2(X - 3, Top - 9, 6, 6), new Color("2C2620"));
                    DrawRect(new Rect2(X - 2, Top - 8, 4, 4), new Color("F2C94C"));
                    float Glow = 0.12f + 0.04f * MathF.Sin(Clk * 3.0f + X);
                    DrawCircle(new Vector2(X, Top - 6), 9, new Color(1.0f, 0.82f, 0.4f, Glow));
                }
            }
            DrawRect(new Rect2(D0 + 4, Top, D1 - D0 - 8, 4), new Color("B08E5C"));
            DrawLine(new Vector2(D0 + 4, Top), new Vector2(D1 - 4, Top), new Color("D4B47C"), 1);
            DrawLine(new Vector2(D0 + 4, Top + 4), new Vector2(D1 - 4, Top + 4), new Color(0, 0, 0, 0.3f), 1);
            DrawRect(new Rect2(D0 + 4, Top + 8, D1 - D0 - 8, 2), new Color("8E7048"));
        }
        DrawSetTransform(Vector2.Zero);
    }
}
