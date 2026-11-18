using Godot;
using System.Collections.Generic;

// Used by tools/PixelBosses.cs for the diagonal (3/4) views of the boss sheets (the players and enemies of
// tools/PixelChars.cs have hand-written 3/4 pixel maps instead).
// A front or back view is turned toward the right by taking a few pixel columns out of its far (left) half:
// the body narrows on the side that turns away, the far eye moves in and gets thinner, and the face slides toward
// the right, which is how a 3/4 view is usually drawn in pixel art. The left diagonals are this, mirrored.
public static class PixelTurn
{
    // Cx: the body's centre column; Offsets: how far left of Cx each dropped column is; rows above HeadBottom
    // (the head) move one more pixel right, since a head turns further than the body.
    public static Image TurnRight(Image Src, int Cx, int[] Offsets, int HeadBottom = 0)
    {
        int W = Src.GetWidth(), H = Src.GetHeight();
        var Drop = new HashSet<int>();
        foreach (int O in Offsets) Drop.Add(Cx - O);
        var Out = Image.CreateEmpty(W, H, false, Image.Format.Rgba8);
        for (int X = 0; X < W; X++)
        {
            if (Drop.Contains(X)) continue;
            // every dropped column to the right of this one moves it one pixel right
            int Shift = 0;
            foreach (int D in Drop) if (D > X) Shift++;
            for (int Y = 0; Y < H; Y++)
            {
                int To = X + Shift + (Y < HeadBottom ? 1 : 0);
                if (To >= W) continue;
                Color C = Src.GetPixel(X, Y);
                if (C.A > 0) Out.SetPixel(To, Y, C);
            }
        }
        return Out;
    }
}
