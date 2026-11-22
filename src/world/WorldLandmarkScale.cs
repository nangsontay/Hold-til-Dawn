using Godot;
using System;
using System.Collections.Generic;

public partial class WorldGeometry
{
    public const float LANDMARK_AREA_SCALE = 1.5f;
    // Area grows by 50%, so lengths grow by sqrt(1.5). Props retain their human scale.
    public const float LANDMARK_SCALE = 1.2247449f;
    public readonly Dictionary<int, Rect2> Compounds = new();
    public readonly Dictionary<int, Vector2> CompoundEntrances = new();

    void BuildExpandedSite(int Id, Action Build)
    {
        int Props = LmProps.Count, Solids = LmSolids.Count, Clears = LmClear.Count, StepStart = Steps.Count, ReefStart = Coral.Count;
        Build();
        if (!LmAt.TryGetValue(Id, out Vector2 Centre)) return;
        var Old = LmProps.GetRange(Props, LmProps.Count - Props);
        var Shifts = new Vector2[Old.Count];
        for (int I = 0; I < Old.Count; I++)
        {
            var (P, Sprite) = Old[I];
            Vector2 New = Centre + (P - Centre) * LANDMARK_SCALE;
            // Coastal and river-bank sites grow inland; never push a prop into water or onto a trail.
            if (!IslandMap.OnLand(New, 12) || IslandMap.InRiver(New, 12) || NearPath(New, 16)) New = P;
            Shifts[I] = New - P; LmProps[Props + I] = (New.Round(), Sprite);
        }
        Vector2 ShiftFor(Vector2 P)
        {
            int Nearest = -1; float Best = float.MaxValue;
            for (int I = 0; I < Old.Count; I++)
            {
                float D = P.DistanceSquaredTo(Old[I].Pos);
                if (D < Best) { Nearest = I; Best = D; }
            }
            return Nearest < 0 ? Vector2.Zero : Shifts[Nearest];
        }
        for (int I = Solids; I < LmSolids.Count; I++)
        {
            var (P, Radius) = LmSolids[I]; LmSolids[I] = (P + ShiftFor(P), Radius);
        }
        for (int I = Clears; I < LmClear.Count; I++)
        {
            var (P, Radius) = LmClear[I];
            LmClear[I] = (P + ShiftFor(P), Radius > 2.8f * TL ? Radius * LANDMARK_SCALE : Radius);
        }
        for (int I = StepStart; I < Steps.Count; I++)
        {
            var (P, Across) = Steps[I]; Steps[I] = (Centre + (P - Centre) * LANDMARK_SCALE, Across);
        }
        for (int I = ReefStart; I < Coral.Count; I++)
        {
            Vector2 P = Centre + (Coral[I] - Centre) * LANDMARK_SCALE;
            if (!IslandMap.OnLand(P)) Coral[I] = P;
        }
    }

    // Find room for the entire compound, not just its centre. Keep all corners and walls on dry ground.
    Vector2 CompoundSpot(int Id, Vector2 Half)
    {
        Vector2 Want = At(Id), Best = Want; float Score = float.PositiveInfinity;
        for (int Y = -18; Y <= 18; Y++)
            for (int X = -18; X <= 18; X++)
            {
                Vector2 P = Want + Tl(X * .5f, Y * .5f);
                float Cost = P.DistanceSquaredTo(Want);
                if (Cost >= Score || IslandMap.InRiver(P, 6 * TL)) continue;
                bool Fits = true;
                for (int Dy = -1; Dy <= 1 && Fits; Dy++)
                    for (int Dx = -1; Dx <= 1; Dx++)
                    {
                        Vector2 Q = P + new Vector2(Half.X * Dx, Half.Y * Dy);
                        if (!IslandMap.OnLand(Q, 20) || IslandMap.InRiver(Q, IslandMap.RIVER_BANK + 20) || NearPad(Q) || InClear(Q, 16)) { Fits = false; break; }
                    }
                // The rear wall and the west return must not occupy an existing route.
                if (Fits && (NearPath(P + new Vector2(0, -Half.Y + 14), Half.X * .45f) ||
                    NearPath(P + new Vector2(-Half.X + 12, 0), Half.Y * .45f))) Cost += 200 * TL * TL;
                if (Fits && Cost < Score) { Score = Cost; Best = P; }
            }
        if (float.IsPositiveInfinity(Score)) throw new InvalidOperationException($"No dry footprint for landmark {Id}");
        return Best.Round();
    }

    void CompoundLine(Vector2 A, Vector2 B, float Radius)
    {
        int N = Math.Max(1, (int)MathF.Ceiling(A.DistanceTo(B) / (Radius * 1.45f)));
        for (int I = 0; I <= N; I++) Solid(A.Lerp(B, (float)I / N), Radius);
    }

    void ConnectCompounds()
    {
        foreach (var (_, Entry) in CompoundEntrances)
        {
            var (Road, _) = OnPath(Entry);
            Footpath(Road, Entry);
        }
        CutPathsAtRivers();
    }
}
