using Godot;
using HoldTilDawn;

public partial class Main
{
    void DrawGardenGround(WorldGeometry W, Rect2 View)
    {
        if (!W.GardenBounds.Grow(70).Intersects(View)) return;
        Color Rim = new("776449"), Soil = new("514A34"), Furrow = new("3C3E2D");
        // Packed, muted earth along the gate and a wide central aisle between six cultivated beds.
        DrawRect(new Rect2(W.GardenCentre + new Vector2(-37, -171), new Vector2(74, 364)), new Color("8B8060"));
        foreach (var Bed in W.GardenBeds)
        {
            DrawRect(Bed.Grow(4), Rim); DrawRect(Bed, Soil);
            for (float Y = Bed.Position.Y + 13; Y < Bed.End.Y - 6; Y += 14)
                DrawLine(new(Bed.Position.X + 6, Y), new(Bed.End.X - 6, Y + 1), Furrow, 2);
            DrawLine(Bed.Position, new(Bed.End.X, Bed.Position.Y), new Color("9D8961"), 3);
            DrawLine(new(Bed.Position.X, Bed.End.Y), Bed.End, new Color("564A36"), 3);
        }
    }

    void GardenFenceItems(GArray Items, Rect2 View)
    {
        var W = (WorldGeometry)Geometry;
        if (!W.GardenBounds.Grow(70).Intersects(View) || InteriorActive) return;
        for (int I = 0; I < W.GardenFence.Count; I++)
        {
            var (A, B) = W.GardenFence[I];
            if (new Rect2(A.Min(B), (B - A).Abs()).Grow(64).Intersects(View))
                Items.Add(new GArray(Mathf.Max(A.Y, B.Y), 13, I));
        }
    }

    void DrawGardenFence(int Index)
    {
        var (A, B) = ((WorldGeometry)Geometry).GardenFence[Index];
        Color Shade = new("403C2B"), Wood = new("84734C"), Lit = new("AC9564");
        DrawLine(A + new Vector2(0, -17), B + new Vector2(0, -17), Shade, 6);
        DrawLine(A + new Vector2(0, -31), B + new Vector2(0, -31), Wood, 5);
        for (int I = 0; I <= 3; I++)
        {
            Vector2 P = A.Lerp(B, I / 3f).Round();
            float H = I == 0 ? 49 : 36 + (Index * 7 + I * 3) % 9;
            DrawLine(P, P + new Vector2(I == 2 ? 2 : 0, -H), Shade, 7);
            DrawLine(P + new Vector2(-1, -2), P + new Vector2(-1, -H + 3), Wood, 4);
            DrawLine(P + new Vector2(-2, -H), P + new Vector2(2, -H), Lit, 2);
        }
        if (Index % 3 == 0)
        {
            Vector2 P = A.Lerp(B, .5f);
            DrawLine(P - new Vector2(4, 5), P + new Vector2(5, -31), new Color("475D36"), 2);
            for (int I = 0; I < 4; I++)
                DrawRect(new Rect2((P + new Vector2(I % 2 == 0 ? -5 : 3, -6 - I * 7)).Round(), new Vector2(7, 3)), new Color("65834A"));
        }
    }
}
