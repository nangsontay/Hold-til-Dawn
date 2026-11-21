using Godot;
using System.Collections.Generic;

// Used by tools/PixelBosses.cs for the diagonal (3/4) views of the boss sheets (the players and enemies of
// tools/PixelChars.cs have hand-written 3/4 pixel maps instead).
// A front or back view is turned toward the right by taking a few pixel columns out of its far (left) half:
// the body narrows on the side that turns away and the face slides toward the right, which is how a 3/4 view is
// usually drawn in pixel art. The left diagonals are this, mirrored.
public static class PixelTurn
{
    // Cx: the body's centre column; Offsets: how far left of Cx each dropped column is; rows above HeadBottom
    // (the head) move one more pixel right, since a head turns further than the body.
    // Eye: pixels of this colour are never dropped. In a row where a dropped column would cut an eye, the nearest
    // column beside it is dropped instead, so both eyes keep the front view's size (a 1-pixel far eye next to a
    // 2-pixel near eye looks like a mistake at this size, not like perspective).
    public static Image TurnRight(Image Src, int Cx, int[] Offsets, int HeadBottom = 0, Color? Eye = null)
    {
        int W = Src.GetWidth(), H = Src.GetHeight();
        var Out = Image.CreateEmpty(W, H, false, Image.Format.Rgba8);
        bool IsEye(int X, int Y) => Eye.HasValue && X >= 0 && X < W && Src.GetPixel(X, Y).IsEqualApprox(Eye.Value);
        for (int Y = 0; Y < H; Y++)
        {
            var Drop = new HashSet<int>();
            foreach (int O in Offsets)
            {
                int C = Cx - O;
                if (IsEye(C, Y))
                {
                    // nearest free column on either side that is not part of an eye
                    for (int D = 1; D < W; D++)
                    {
                        if (C - D >= 0 && !IsEye(C - D, Y) && !Drop.Contains(C - D)) { C -= D; break; }
                        if (C + D < Cx && !IsEye(C + D, Y) && !Drop.Contains(C + D)) { C += D; break; }
                    }
                }
                Drop.Add(C);
            }
            for (int X = 0; X < W; X++)
            {
                if (Drop.Contains(X)) continue;
                // every dropped column to the right of this one moves it one pixel right
                int Shift = 0;
                foreach (int D in Drop) if (D > X) Shift++;
                int To = X + Shift + (Y < HeadBottom ? 1 : 0);
                if (To >= W) continue;
                Color Col = Src.GetPixel(X, Y);
                if (Col.A > 0) Out.SetPixel(To, Y, Col);
            }
        }
        return Out;
    }
}
