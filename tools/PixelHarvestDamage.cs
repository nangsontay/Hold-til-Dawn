using Godot;
using System;

// Harvesting leaves a recognisable object: axe cuts in a rigid trunk, planar fractures in rock and cut stems.
// Never remove random circles or flood-fill away diagonally connected foliage.
public partial class PixelNature
{
    void SaveHarvestVariants(string Name, int Variant, Canvas Full, int Radius, string Folder, Godot.Collections.Dictionary Into)
    {
        for (int Stage = 1; Stage <= 5; Stage++)
        {
            string Key = Name + (Stage == 5 ? "_empty_" : "_dmg" + Stage + "_") + Variant;
            var C = HarvestStage(Name, Variant, Full, Stage);
            C.ToImage().SavePng(Folder.PathJoin(Key + ".png"));
            Into[Key] = new Godot.Collections.Array { C.Anchor.X, C.Anchor.Y, Stage == 5 ? 0 : Radius };
        }
    }

    Canvas HarvestStage(string Name, int Variant, Canvas Full, int Stage)
    {
        int Type = Name.StartsWith("node_wood") ? 0 : Name.StartsWith("node_stone") ? 1 : Name.StartsWith("node_herb") ? 2 : Name.StartsWith("node_titanium") ? 3 : Harvestables.TypeOf(Name + "_" + Variant);
        if (Stage == 5 && Type != 2)
        {
            var Empty = new Canvas(Full.W, Full.H) { Anchor = Full.Anchor };
            Canvas Remains = Type == 0 ? Stump(1101 + Variant) : Rubble(1201 + Variant);
            Empty.Paste(Remains, Full.Anchor - Remains.Anchor);
            if (Name is "lm_log" or "lm_sticks")
            {
                Empty = new Canvas(Full.W, Full.H) { Anchor = Full.Anchor };
                HarvestChips(Empty, Stage, WOOD_CHIPS, Variant);
            }
            return Empty;
        }
        var C = Full.Clone();
        C.Unoutline();
        if (Type == 0) { TrimBranches(C, Name, Stage); AxeCut(C, Name, Stage, Variant); }
        else if (Type is 1 or 3) RockFracture(C, Stage, Variant, Type == 3 ? ORE_ROCK : Name.StartsWith("lm_") ? LIME : Name.StartsWith("node_") ? STONE : ROCK);
        else CutPlants(C, Name, Stage, Variant);
        HarvestChips(C, Stage, Type == 0 ? WOOD_CHIPS : Type == 1 ? STONE : Type == 3 ? TI : HERB, Variant);
        if (Full.Ink.A > 0) C.Outline(Full.Ink);
        return C;
    }

    static void AxeCut(Canvas C, string Name, int Stage, int Variant)
    {
        if (Name is "lm_log" or "lm_sticks")
        {
            var B = HarvestBounds(C);
            int Edge = B.End.X - (int)(B.Size.X * Stage * 0.16f);
            for (int Y = B.Position.Y; Y < B.End.Y; Y++)
                for (int X = B.Position.X; X < B.End.X; X++)
                    if (C.Solid(X, Y))
                    {
                        if (X > Edge) C.Clear(X, Y);
                        else if (X >= Edge - 2) C.Set(X, Y, CUT[(X + Y) % CUT.Length]);
                    }
            return;
        }
        int Cy = C.Anchor.Y - (Name == "lm_mangrove" ? 19 : 7);
        bool BarkAt(int X, int Y)
        {
            if (!C.Body(X, Y)) return false;
            Color Col = C.At(X, Y);
            return Col.R > Col.G * 1.1f && Col.G > Col.B * 1.08f;
        }
        int Left = -1, Right = -1;
        // Choose the trunk nearest the anchor, not a branch at the silhouette's edge.
        for (int D = 0; D < 18 && Left < 0; D++)
            foreach (int Sign in new[] { -1, 1 })
            {
                int X = C.Anchor.X + D * Sign;
                if (!BarkAt(X, Cy)) continue;
                Left = Right = X;
                while (BarkAt(Left - 1, Cy)) Left--;
                while (BarkAt(Right + 1, Cy)) Right++;
                break;
            }
        if (Left < 0) return;
        int Depth = Math.Max(1, (int)MathF.Ceiling((Right - Left + 1) * (0.12f + Stage * 0.16f)));
        int HalfHeight = 2 + Stage;
        for (int Dy = -HalfHeight; Dy <= HalfHeight; Dy++)
            for (int Dx = 0; Dx < Math.Max(1, Depth - Math.Abs(Dy)); Dx++)
            {
                int X = Left + Dx, Y = Cy + Dy;
                if (!BarkAt(X, Y)) continue;
                // A triangular pale cut face, with a shaded inner lip and one intact strip of bark.
                C.Set(X, Y, Dy < 0 ? CUT[2] : Dy == 0 ? BARK[0].Darkened(0.3f) : CUT[1]);
            }
    }

    static Rect2I HarvestBounds(Canvas C, bool Leaves = false)
    {
        int X0 = C.W, X1 = -1, Y0 = C.H, Y1 = -1;
        for (int Y = 0; Y < C.H; Y++)
            for (int X = 0; X < C.W; X++)
                if (C.Solid(X, Y) && (!Leaves || IsLeaf(C.At(X, Y))))
                { X0 = Math.Min(X0, X); X1 = Math.Max(X1, X); Y0 = Math.Min(Y0, Y); Y1 = Math.Max(Y1, Y); }
        return X1 < X0 ? new Rect2I() : new Rect2I(X0, Y0, X1 - X0 + 1, Y1 - Y0 + 1);
    }

    static bool IsLeaf(Color Col) => Col.G > Col.R * 1.08f && Col.G > Col.B * 0.9f;

    static void TrimBranches(Canvas C, string Name, int Stage)
    {
        if (Name is "lm_log" or "lm_sticks") return;
        var B = HarvestBounds(C, true);
        if (B.Size == Vector2I.Zero) return;
        bool Pine = Name is "pine" or "node_wood";
        int Tiers = 0;
        if (Pine)
            for (int Y = 0; Y < C.H; Y++)
                for (int X = 0; X < C.W; X++)
                    if (C.Body(X, Y) && IsLeaf(C.At(X, Y))) Tiers = Math.Max(Tiers, C.TagAt(X, Y));
        float Centre = Pine ? C.Anchor.X : B.Position.X + B.Size.X * 0.5f;
        for (int Y = B.Position.Y; Y < B.End.Y; Y++)
            for (int X = B.Position.X; X < B.End.X; X++)
            {
                if (!C.Solid(X, Y) || !IsLeaf(C.At(X, Y))) continue;
                float Half;
                if (Pine)
                {
                    int Tier = C.TagAt(X, Y);
                    float Removed = Math.Clamp(Tier - (Tiers - Stage * 0.4f), 0, 1);
                    if (Removed >= 0.999f) { C.Clear(X, Y); continue; }
                    Half = B.Size.X * 0.5f * (0.34f + 0.66f * Tier / Math.Max(1, Tiers)) * (1 - Removed);
                }
                else
                {
                    // A whole frond or leafy cluster breaks away, keeping the curved tips of the others.
                    int Tag = C.TagAt(X, Y);
                    bool Remove = Name == "lm_palm" ? Tag >= 100 && Tag < 100 + Stage :
                        Y > B.End.Y - B.Size.Y * Stage * 0.12f + MathF.Abs(X - Centre) * 0.12f;
                    if (Remove) C.Clear(X, Y);
                    continue;
                }
                if (MathF.Abs(X - Centre) > Math.Max(1, Half)) C.Clear(X, Y);
            }
        if (Name == "lm_mangrove")
        {
            // Leaves used to cover this part of the fork: expose its wood as the crown is pruned.
            int Top = (int)(B.End.Y - B.Size.Y * Stage * 0.12f) - 2;
            for (int Y = Top; Y < C.Anchor.Y - 36; Y++)
                for (int Dx = -1; Dx <= 1; Dx++)
                {
                    int X = (int)Centre + Dx;
                    if (!C.Solid(X, Y)) C.Put(X, Y, MANG_BARK[Dx < 0 ? 3 : 1]);
                }
        }
        if (Pine)
        {
            // Expose the trunk where lower boughs have been taken, rather than leaving a floating crown.
            int Top = B.Position.Y + (int)(B.Size.Y * 0.25f);
            for (int Y = Top; Y < C.Anchor.Y; Y++)
                for (int Dx = -1; Dx <= 1; Dx++)
                {
                    int X = C.Anchor.X + Dx;
                    if (!C.Solid(X, Y))
                        C.Put(X, Y, BARK[Dx < 0 ? 2 : Dx > 0 ? 0 : 1]);
                }
        }
    }

    static void RockFracture(Canvas C, int Stage, int Variant, Color[] Palette)
    {
        var Bounds = HarvestBounds(C);
        int X0 = Bounds.Position.X, X1 = Bounds.End.X - 1, Y0 = Bounds.Position.Y, Y1 = Bounds.End.Y - 1;
        float Width = X1 - X0, Height = Y1 - Y0;
        if (Width <= 0 || Height <= 0) return;
        float Edge = X1 - Width * Stage * 0.12f;
        for (int Y = Y0; Y <= Y1; Y++)
            for (int X = X0; X <= X1; X++)
            {
                if (!C.Solid(X, Y)) continue;
                // Each unit removes a substantial angular slab: the base stays on the same ground anchor.
                float Top = Y0 + Height * Stage * 0.15f + (X - X0 - Width * 0.4f) * 0.16f + (X / 7 + Variant) % 2;
                float Cut = Edge + Math.Max(0, Y - Top) * 0.25f;
                if (X > Cut || Y < Top) C.Clear(X, Y);
                else if (Y < Top + 3) C.Set(X, Y, Palette[Y < Top + 1 ? ^1 : ^2]);
                else if (X > Cut - 3) C.Set(X, Y, Palette[1]);
            }
        Vector2 Root = new Vector2(X0 + Width * (0.35f + Variant * 0.07f), Y0 + Height * Stage * 0.15f);
        Vector2 Prev = Root;
        for (int I = 1; I <= Stage + 1; I++)
        {
            Vector2 Next = Root + new Vector2((I % 2 == 0 ? 1 : -1) * (2 + Stage), Height * I * 0.14f);
            RockSeam(C, Prev, Next, Palette[0], Palette[^1]);
            if (Stage >= 2 && I == 2) RockSeam(C, Next, Next + new Vector2(Width * 0.24f, Height * 0.07f), Palette[0], Palette[^2]);
            Prev = Next;
        }
    }

    static void RockSeam(Canvas C, Vector2 A, Vector2 B, Color Dark, Color Light)
    {
        int N = Math.Max(1, (int)A.DistanceTo(B));
        for (int I = 0; I <= N; I++)
        {
            Vector2I P = (Vector2I)A.Lerp(B, (float)I / N).Round();
            if (C.Body(P.X, P.Y)) C.Set(P.X, P.Y, Dark);
            if (C.Body(P.X - 1, P.Y)) C.Set(P.X - 1, P.Y, Light);
        }
    }

    static void CutPlants(Canvas C, string Name, int Stage, int Variant)
    {
        bool Container = Name is "lm_bed" or "lm_pots";
        var B = HarvestBounds(C, true);
        if (B.Size == Vector2I.Zero) return;
        float RootX = C.Anchor.X, RootY = B.End.Y - 1;
        float HalfAngle = Mathf.Pi * 0.5f * new[] { 1f, 0.72f, 0.52f, 0.33f, 0.14f, 0f }[Stage];
        for (int Y = 0; Y < C.H; Y++)
            for (int X = 0; X < C.W; X++)
            {
                if (!C.Solid(X, Y)) continue;
                Color Col = C.At(X, Y);
                bool Vegetation = IsLeaf(Col) || Col.B > Col.R * 1.12f || Col.R > Col.G * 1.25f && Col.B > Col.G * 1.12f;
                if (Container && !Vegetation) continue;
                bool Cut = Stage == 5;
                if (Container) Cut |= X < B.Position.X + B.Size.X * Stage / 5f;
                else
                {
                    // Remove outer leaves in complete fans, including flowers and their stems.
                    float Angle = MathF.Abs(MathF.Atan2(X - RootX, Math.Max(1, RootY - Y)));
                    Cut |= Angle > HalfAngle || Y < B.Position.Y + B.Size.Y * Stage * 0.045f;
                }
                if (Cut)
                {
                    C.Clear(X, Y);
                    if (Container && Name == "lm_bed" && Y >= 16 && Y <= 29) C.Put(X, Y, SOIL[1]);
                }
            }
        if (!Container)
            for (int I = 0; I < Math.Min(4, Stage + 1); I++)
            {
                int X = C.Anchor.X - 4 + I * 2, Y = Math.Min(C.H - 3, (int)RootY);
                C.Put(X, Y, HERB[0]); C.Put(X, Y - 1, HERB[2]);
            }
    }

    static void HarvestChips(Canvas C, int Stage, Color[] Palette, int Variant)
    {
        for (int I = 0; I < Stage + 1; I++)
        {
            int X = C.Anchor.X + (I % 2 == 0 ? -1 : 1) * (5 + I * 2 + Variant % 2);
            int Y = Math.Min(C.H - 2, C.Anchor.Y + I % 3);
            if (C.Solid(X, Y)) continue;
            C.Put(X, Y, Palette[Math.Min(Palette.Length - 1, 1 + I % 2)], 3);
            if (Stage >= 2 && !C.Solid(X, Y - 1)) C.Put(X, Y - 1, Palette[^1], 3);
            if (Stage >= 3 && !C.Solid(X + 1, Y)) C.Put(X + 1, Y, Palette[^1], 3);
        }
    }
}
