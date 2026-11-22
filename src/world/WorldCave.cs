using Godot;
using System;
using System.Collections.Generic;

public partial class WorldGeometry
{
    // A cutaway cavern: the eastern opening leads west into a walkable chamber.
    // The ground footprint is three times the previous cave clearing's area.
    public const float CAVE_AREA_SCALE = 3f;
    public static readonly Vector2 CaveRadii = new Vector2(7.5f, 5.2f) * LANDMARK_SCALE * MathF.Sqrt(CAVE_AREA_SCALE) * TL;
    public Vector2 CaveCentre { get; private set; }
    public Vector2 CaveEntrance => CaveCentre + Tl(12, 0);
    public Vector2[] CaveFloor { get; private set; } = Array.Empty<Vector2>();
    public Vector2[] CaveRoof { get; private set; } = Array.Empty<Vector2>();
    public readonly List<Vector2[]> CaveRoofFacets = new(), CaveRoofSeams = new();
    public readonly List<Vector2[]> CaveExteriorMasks = new();
    public Vector2[] CaveVisibilityBoundary { get; private set; } = Array.Empty<Vector2>();
    public readonly List<(Vector2[] Face, Vector2[] Cap, Vector2[] Facet, Vector2[] Strata, Vector2[] Crack)> CaveWalls = new();

    public static bool CaveGroundContains(Vector2 Offset, float Margin = 0)
    {
        Vector2 R = CaveRadii + Vector2.One * Margin;
        if (MathF.Abs(Offset.X) > R.X || MathF.Abs(Offset.Y) > R.Y) return false;
        float D = new Vector2(Offset.X / R.X, Offset.Y / R.Y).Length();
        float A = MathF.Atan2(Offset.Y, Offset.X);
        return D <= .94f + .035f * MathF.Sin(A * 5 + 16) + .025f * MathF.Sin(A * 9 - 16);
    }

    public bool InCaveRegion(Vector2 P, float Margin = 0) => CaveGroundContains(P - CaveCentre, Margin);
    public bool InCaveFloor(Vector2 P) => IslandMap.InPolygon(CaveFloor, P);
    public bool IsCaveInterior(Vector2 P) => P.X < CaveEntrance.X - 32 && InCaveFloor(P);

    public Color CaveGroundColour(Vector2 P)
    {
        if (!InCaveFloor(P)) return new Color("65645D");
        float Depth = Math.Clamp((CaveEntrance.X - P.X) / (24 * TL), 0, 1);
        return new Color("50544A").Lerp(new Color("303D38"), Depth);
    }

    void BuildWalkableCave()
    {
        CaveCentre = (At(16) - Tl(12, 0)).Round();
        LmAt[16] = CaveEntrance;
        CaveWalls.Clear();
        Vector2[] Shape = {
            Tl(12, -2.4f), Tl(8, -2.6f), Tl(6, -5.1f), Tl(1, -6), Tl(-4, -5.8f), Tl(-10, -3.8f),
            Tl(-11, -.8f), Tl(-10.2f, 3.5f), Tl(-6, 5.6f), Tl(0, 6.2f), Tl(6, 5.2f), Tl(8.6f, 2.7f), Tl(12, 2.4f)
        };
        CaveFloor = Array.ConvertAll(Shape, P => (CaveCentre + P).Round());
        BuildCaveRoof();
        // No closing wall on the east: an opening almost five tiles wide, with an unobstructed central aisle.
        for (int I = 0; I < CaveFloor.Length - 1; I++)
        {
            Vector2 A = CaveFloor[I], B = CaveFloor[I + 1];
            CompoundLine(A, B, 21);
            int N = Math.Max(1, (int)MathF.Ceiling(A.DistanceTo(B) / (2.1f * TL)));
            for (int J = 0; J < N; J++)
            {
                Vector2 Along = (B - A).Normalized();
                Vector2 P = A.Lerp(B, J / (float)N) - Along * 6, Q = A.Lerp(B, (J + 1f) / N) + Along * 6;
                Vector2 Across = (B - A).Normalized().Orthogonal() * 23;
                Vector2 Lift = new(0, -39 - (I * 13 + J * 7) % 22);
                Vector2[] Cap = { (P + Across + Lift + new Vector2(3, -7)).Round(),
                    (P.Lerp(Q, .47f) + Across + Lift * 1.1f).Round(),
                    (Q + Across + Lift * .9f + new Vector2(-3, -4)).Round(),
                    (Q - Across + Lift * .75f).Round(),
                    (P.Lerp(Q, .38f) - Across + Lift * .93f).Round(),
                    (P - Across + Lift * .85f).Round() };
                Vector2[] Face = { Cap[5], Cap[4], Cap[3], (Q - Across + new Vector2(0, 8)).Round(),
                    (P.Lerp(Q, .6f) - Across + new Vector2(2, 11)).Round(), (P - Across + new Vector2(0, 5)).Round() };
                Vector2[] Facet = { Cap[0], Cap[1], Cap[4], Cap[5] };
                Vector2[] Strata = { Face[0].Lerp(Face[5], .57f).Round(), Face[1].Lerp(Face[4], .58f).Round(), Face[2].Lerp(Face[3], .55f).Round() };
                Vector2[] Crack = { Cap[1], Cap[4].Lerp(Cap[3], .24f).Round(), Face[4].Lerp(Face[3], .34f).Round() };
                CaveWalls.Add((Face, Cap, Facet, Strata, Crack));
            }
        }
        // Geological dressing stays outside the mining lanes; only loose resource rocks can be harvested.
        foreach (var (X, Y) in new[] { (-9f, -4.7f), (-6f, -6.4f), (2f, -6.9f), (5f, 6.4f), (-7f, 6.5f) })
            Prop(CaveCentre + Tl(X, Y), "lm_rubble_0");
        Prop(CaveCentre + Tl(13, -4.7f), "lm_warning_0");
        Prop(CaveCentre + Tl(12, 5.1f), "lm_expedition_0");
        CompoundEntrances[16] = CaveEntrance + Tl(1.5f, 0);
        Clear(CaveCentre, CaveRadii.X + TL);
        BuildCaveExteriorMasks();
    }

    void BuildCaveExteriorMasks()
    {
        var Points = new List<Vector2>(CaveFloor);
        foreach (var (Face, Cap, _, _, _) in CaveWalls) { Points.AddRange(Face); Points.AddRange(Cap); }
        var Hull = new List<Vector2>(Geometry2D.ConvexHull(Points.ToArray()));
        if (Hull[0].IsEqualApprox(Hull[^1])) Hull.RemoveAt(Hull.Count - 1);
        CaveVisibilityBoundary = Hull.ToArray();
        int Left = 0, Right = 0;
        for (int I = 1; I < Hull.Count; I++)
        {
            if (Hull[I].X < Hull[Left].X) Left = I;
            if (Hull[I].X > Hull[Right].X) Right = I;
        }
        List<Vector2> Chain(int Step)
        {
            var Result = new List<Vector2>();
            for (int I = Left; ; I = (I + Step + Hull.Count) % Hull.Count)
            { Result.Add(Hull[I]); if (I == Right) break; }
            return Result;
        }
        var A = Chain(1); var B = Chain(-1);
        float AverageY(List<Vector2> C) { float Y = 0; foreach (var P in C) Y += P.Y; return Y / C.Count; }
        if (AverageY(A) > AverageY(B)) (A, B) = (B, A);
        const float Far = 32768;
        A.Add(new(Hull[Right].X, -Far)); A.Add(new(Hull[Left].X, -Far));
        B.Add(new(Hull[Right].X, Far)); B.Add(new(Hull[Left].X, Far));
        CaveExteriorMasks.Clear(); CaveExteriorMasks.Add(A.ToArray()); CaveExteriorMasks.Add(B.ToArray());
        CaveExteriorMasks.Add(new[] { new Vector2(-Far, -Far), new(Hull[Left].X, -Far), new(Hull[Left].X, Far), new(-Far, Far) });
        CaveExteriorMasks.Add(new[] { new Vector2(Hull[Right].X, -Far), new(Far, -Far), new(Far, Far), new(Hull[Right].X, Far) });
    }

    void BuildCaveRoof()
    {
        // The rock canopy stops short of the eastern lip, exposing a dark, east-facing opening.
        Vector2[] Outline = { Tl(10.5f, -3), Tl(5.8f, -6), Tl(0, -7.5f), Tl(-6.7f, -6.4f),
            Tl(-11.3f, -3.3f), Tl(-12, 0), Tl(-10.8f, 3.8f), Tl(-6, 5.6f), Tl(0, 6.1f),
            Tl(6, 5.2f), Tl(10.5f, 2.7f), Tl(9.6f, .8f), Tl(9.6f, -1) };
        CaveRoof = Array.ConvertAll(Outline, P => (CaveCentre + P).Round());
        CaveRoofFacets.Clear(); CaveRoofSeams.Clear();
        var Samples = new List<Vector2>(CaveRoof);
        for (int Y = -5; Y <= 4; Y += 2)
            for (int X = -10; X <= 8; X += 3)
            {
                Vector2 P = (CaveCentre + Tl(X + .6f * MathF.Sin(X * 7 + Y), Y + .4f * MathF.Cos(X + Y * 3))).Round();
                if (IslandMap.InPolygon(CaveRoof, P)) Samples.Add(P);
            }
        Vector2[] Points = Samples.ToArray();
        int[] Triangles = Geometry2D.TriangulateDelaunay(Points);
        for (int I = 0; I < Triangles.Length; I += 3)
        {
            var Triangle = new[] { Points[Triangles[I]], Points[Triangles[I + 1]], Points[Triangles[I + 2]] };
            foreach (var Patch in Geometry2D.IntersectPolygons(Triangle, CaveRoof)) CaveRoofFacets.Add(Patch);
        }
        // Irregular sediment bands and fissures, generated once rather than rebuilt while drawing.
        for (int I = 0; I < 37; I++)
        {
            float X = -11 + I * 7.31f % 20, Y = -6 + I * 3.17f % 11;
            Vector2 A = CaveCentre + Tl(X, Y), B = A + Tl(2.1f + I % 3 * .6f, -.12f + I % 4 * .1f);
            Vector2 Mid = A.Lerp(B, .45f) + new Vector2(0, I % 2 == 0 ? 5 : -4);
            if (IslandMap.InPolygon(CaveRoof, A) && IslandMap.InPolygon(CaveRoof, B))
                CaveRoofSeams.Add(new[] { A.Round(), Mid.Round(), B.Round() });
        }
    }

    void AddCaveResources()
    {
        bool Free(Vector2 P, float Radius)
        {
            if (!IslandMap.OnLand(P, Radius + 12) || IslandMap.InRiver(P, IslandMap.RIVER_BANK + Radius)) return false;
            foreach (var (C, R) in LmSolids) if (P.DistanceTo(C) < R + Radius + 13) return false;
            foreach (var Site in HarvestSites) if (P.DistanceTo(Site.Pos) < Radius + Site.Radius + 12) return false;
            return true;
        }
        int Ore = 0, Stone = 0;
        foreach (float Y in new[] { -3.5f, -1.8f, 1.8f, 3.5f })
            foreach (float X in new[] { -8f, -5f, -2f, 1f, 4f, 7f })
            {
                Vector2 P = (CaveCentre + Tl(X + .45f * MathF.Sin(X * 1.7f + Y * .4f), Y + .27f * MathF.Cos(X * 1.23f))).Round();
                if (!InCaveFloor(P) || !Free(P, 19.2f)) continue;
                HarvestSites.Add(new(P, "node_titanium_" + Ore++ % 2, 3, 19.2f));
            }
        // Scree fields wrap the chamber; leave the east-west entrance and its approach entirely clear.
        for (int I = 0; I < 204 && Stone < 32; I++)
        {
            float A = I * 2.399963f;
            float Ring = I % 3 == 0 ? .74f : I % 3 == 1 ? .86f : .94f;
            Vector2 P = (CaveCentre + new Vector2(MathF.Cos(A) * CaveRadii.X, MathF.Sin(A) * CaveRadii.Y) * Ring).Round();
            if (!InCaveRegion(P) || InCaveFloor(P) || (P.X > CaveCentre.X + 7 * TL && MathF.Abs(P.Y - CaveCentre.Y) < 1.7f * TL) || !Free(P, 19.84f)) continue;
            HarvestSites.Add(new(P, "rock_" + Stone++ % 5, 1, 19.84f));
        }
    }
}
