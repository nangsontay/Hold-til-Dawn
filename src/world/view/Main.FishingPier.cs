using Godot;
using System;
using HoldTilDawn;

public partial class Main
{
    static readonly Vector2[] PierFishBody = { new(-10, 0), new(-4, -5), new(6, -4), new(11, 0), new(6, 4), new(-4, 4) };
    static readonly Vector2[] PierFishTail = { new(-7, 0), new(-15, -6), new(-15, 6) };
    void FishingRodItems(GArray Items, Rect2 View)
    {
        var W = (WorldGeometry)Geometry;
        if (InteriorActive) return;
        for (int I = 0; I < W.FishingRods.Count; I++)
            if (View.Grow(90).HasPoint(W.FishingRods[I].Foot)) Items.Add(new GArray(W.FishingRods[I].Foot.Y, 15, I));
    }

    void DrawFishingRod(int Index)
    {
        var (Foot, Side) = ((WorldGeometry)Geometry).FishingRods[Index];
        Vector2 Tip = Foot + new Vector2(-32, Side * 35 - 22);
        Vector2 Mid = Foot.Lerp(Tip, .6f) + new Vector2(5, -7);
        DrawLine(Foot + new Vector2(0, 6), Foot + new Vector2(0, -13), new Color("605B45"), 4);
        DrawLine(Foot + new Vector2(6, 4), Foot + new Vector2(-5, -7), new Color("AB9466"), 3);
        DrawLine(Foot, Mid, new Color("B69A64"), 3); DrawLine(Mid, Tip, new Color("D0BA84"), 2);
        DrawLine(Foot, Foot.Lerp(Mid, .24f), new Color("4B4438"), 5);
        DrawCircle(Foot.Lerp(Mid, .25f) + new Vector2(4, 0), 4, new Color("BEC3B0"));
        DrawCircle(Foot.Lerp(Mid, .25f) + new Vector2(4, 0), 2, new Color("535D58"));
        Vector2 Float = Foot + new Vector2(-37, Side * 56);
        Vector2 Sag = Tip.Lerp(Float, .5f) + new Vector2(8, 5);
        DrawLine(Tip, Sag, new Color("C3CCB3", .7f), 1); DrawLine(Sag, Float, new Color("C3CCB3", .7f), 1);
        Float.Y += MathF.Sin((float)Clock * 2 + Index * 2) * 1.5f;
        DrawRect(new Rect2(Float.Round() - new Vector2(2, 3), new Vector2(4, 7)), new Color("E8DAB6"));
        DrawRect(new Rect2(Float.Round() - new Vector2(2, 3), new Vector2(4, 3)), new Color("C55B42"));
    }

    void DrawFishingLife(WorldGeometry W, Rect2 View, float Time)
    {
        if (InteriorActive || !W.LmAt.TryGetValue(10, out Vector2 Pier) || !View.Grow(360).HasPoint(Pier)) return;
        for (int L = 0; L < W.FishingLitter.Count; L++)
        {
            var (P, Kind) = W.FishingLitter[L];
            if (!View.HasPoint(P)) continue;
            DrawRect(new Rect2(P + new Vector2(-5, 2), new Vector2(12, 3)), new Color("343F35", .4f));
            switch (Kind)
            {
                case 0: // Crumpled bait wrappers.
                    FillStatic(W.FishingLitterFaces[L], new Color("C9C09A"));
                    DrawLine(P + new Vector2(-2, -2), P + new Vector2(4, 1), new Color("987C55"), 2); break;
                case 1: // Empty drink tins.
                    DrawRect(new Rect2(P - new Vector2(4, 5), new Vector2(8, 10)), new Color("9AABA2"));
                    DrawRect(new Rect2(P - new Vector2(4, 2), new Vector2(8, 4)), new Color("AB6450"));
                    DrawLine(P + new Vector2(-3, -5), P + new Vector2(3, -5), new Color("D0D2B8"), 2); break;
                case 2: // Discarded green bottles.
                    DrawRect(new Rect2(P - new Vector2(5, 2), new Vector2(10, 5)), new Color("4D7B68"));
                    DrawRect(new Rect2(P + new Vector2(5, -1), new Vector2(4, 3)), new Color("729680"));
                    DrawLine(P - new Vector2(3, 1), P + new Vector2(3, -1), new Color("ABC1A1"), 1); break;
                default: // Coiled fishing line and a rusty hook.
                    DrawArc(P, 6, 0, Mathf.Tau * 1.7f, 16, new Color("B7AE85"), 1);
                    DrawLine(P + new Vector2(5, 0), P + new Vector2(11, 4), new Color("A4A18B"), 1); break;
            }
        }
        // At most eight visual fish, with cached sea positions and staggered jumps. No physics, AI or spawning.
        for (int I = 0; I < W.FishingFish.Count; I++)
        {
            Vector2 Water = W.FishingFish[I];
            if (!View.Grow(45).HasPoint(Water)) continue;
            float Cycle = 5.2f + I % 3 * .9f;
            float Phase = Mathf.PosMod(Time + I * 1.37f, Cycle);
            if (Phase > 2.1f) continue;
            float T = Mathf.Min(Phase / 1.15f, 1), Arc = MathF.Sin(T * MathF.PI);
            int Sign = I % 2 == 0 ? 1 : -1;
            Vector2 Fish = (Water + new Vector2((T - .5f) * 42 * Sign, -Arc * (30 + I % 3 * 8))).Round();
            Color Foam = new("BCDAD1", (1 - Phase / 2.1f) * .75f);
            Vector2 Ripple = Water + new Vector2(Phase < 1.15f ? -21 * Sign : 21 * Sign, 0);
            float Radius = 5 + (Phase < 1.15f ? Phase : Phase - 1.15f) * 16;
            DrawSetTransform(Ripple.Round(), 0, new Vector2(1, .4f));
            DrawArc(Vector2.Zero, Radius, 0, Mathf.Tau, 16, Foam, 1);
            DrawSetTransform(Vector2.Zero);
            if (Phase >= 1.15f)
            {
                float Splash = (Phase - 1.15f) / .95f;
                for (int K = 0; K < 4; K++) DrawRect(new Rect2((Ripple + new Vector2((K - 1.5f) * Splash * 15, -MathF.Sin(Splash * MathF.PI) * (K % 2 == 0 ? 14 : 22))).Round(), new Vector2(2, 3)), Foam);
                continue;
            }
            DrawSetTransform(Fish, (T - .5f) * 1.3f * Sign, new Vector2(Sign, 1));
            DrawColoredPolygon(PierFishBody, new Color(I % 2 == 0 ? "9FC7BA" : "C4C7A5"));
            DrawColoredPolygon(PierFishTail, new Color("5F9997"));
            DrawLine(new(-4, 2), new(6, 2), new Color("E0E2BF"), 2);
            DrawRect(new Rect2(6, -2, 2, 2), new Color("273F43"));
            DrawSetTransform(Vector2.Zero);
        }
    }
}
