using Godot;
using System;
using System.Collections.Generic;
using HoldTilDawn;

// Owner: Vu Duc Viet Anh. Titanium nodes are dark rocks grown with blue crystals (tools/PixelNature.cs). Over the
// node the game draws its crystal layer (art/nature/node_titanium_glow_<k>.png) with a slow breathing light, and
// now and then a crystal tip (the pure white pixels of that layer) twinkles.
public partial class Main : Node2D
{
    const string ORE_NODE = "node_titanium_";
    const string ORE_GLOW = "node_titanium_glow_";
    readonly Dictionary<string, Vector2[]> OreTips = new();

    // call right after drawing a nature sprite; does nothing for anything but a titanium node
    public void DrawOreGlow(string N, Vector2 Pos, float S)
    {
        if (!N.StartsWith(ORE_NODE) || N.StartsWith(ORE_GLOW))
        {
            return;
        }
        string Gn = ORE_GLOW + N.Substring(ORE_NODE.Length);
        dynamic Sp = Gd.Get(Spr, Gn);
        if (Sp == null)
        {
            return;
        }
        var Tex = (Texture2D)Gd.Unwrap(Gd.At(Sp, 0));
        Rect2 R = (Rect2)this.SpriteRect(Gn, Pos, S);
        float Now = (float)Clock;
        float Seed = ((Pos.X * 0.37f) + (Pos.Y * 0.61f));
        float Pulse = (0.5f + (0.5f * Mathf.Sin(((Now * 1.8f) + Seed))));
        DrawTextureRect(Tex, R, false, new Color(1.1f, 1.25f, 1.7f, (0.06f + (0.2f * Pulse))));
        Vector2[] Tips = this.OreTipsOf(Gn, Tex);
        float Px = (R.Size.X / Tex.GetWidth());
        for (int K = 0; K < Tips.Length; K++)
        {
            // each tip flashes for a moment once every few seconds, out of step with the others
            float Phase = Mathf.PosMod(((Now * 0.45f) + (Seed * 0.13f) + (K * 0.37f)), 1.0f);
            if (Phase > 0.12f)
            {
                continue;
            }
            float A = Mathf.Sin(((Phase / 0.12f) * Mathf.Pi));
            Vector2 At = (R.Position + ((Tips[K] + new Vector2(0.5f, 0.5f)) * Px));
            var Hot = new Color(1.8f, 2.0f, 2.6f, A);
            var Soft = new Color(1.2f, 1.4f, 2.2f, (A * 0.5f));
            float Arm = Mathf.Round((1.0f + (2.0f * A)));
            DrawRect(new Rect2((At - new Vector2(0.5f, 0.5f)).Round(), Vector2.One), Hot);
            DrawRect(new Rect2((At + new Vector2(-0.5f - Arm, -0.5f)).Round(), new Vector2(Arm, 1)), Soft);
            DrawRect(new Rect2((At + new Vector2(0.5f, -0.5f)).Round(), new Vector2(Arm, 1)), Soft);
            DrawRect(new Rect2((At + new Vector2(-0.5f, -0.5f - Arm)).Round(), new Vector2(1, Arm)), Soft);
            DrawRect(new Rect2((At + new Vector2(-0.5f, 0.5f)).Round(), new Vector2(1, Arm)), Soft);
        }
    }

    Vector2[] OreTipsOf(string Gn, Texture2D Tex)
    {
        if (OreTips.TryGetValue(Gn, out var Tips))
        {
            return Tips;
        }
        var List = new List<Vector2>();
        Image Im = Tex.GetImage();
        if (Im != null)
        {
            if (Im.IsCompressed())
            {
                Im.Decompress();
            }
            for (int Y = 0; Y < Im.GetHeight(); Y++)
            {
                for (int X = 0; X < Im.GetWidth(); X++)
                {
                    Color C = Im.GetPixel(X, Y);
                    if ((C.A > 0.99f) && (C.R > 0.99f) && (C.G > 0.99f) && (C.B > 0.99f))
                    {
                        List.Add(new Vector2(X, Y));
                    }
                }
            }
        }
        Tips = List.ToArray();
        OreTips[Gn] = Tips;
        return Tips;
    }
}
