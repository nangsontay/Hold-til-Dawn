using Godot;
using System;
using System.Collections.Generic;

// Static terrain queries for a small decoration patch. Keep only nearby route segments, instead of testing
// every route on the island for each painted pixel. This never changes gameplay collision or navigation.
public sealed class LandmarkGroundMask
{
    readonly WorldGeometry World;
    readonly List<(Vector2 A, Vector2 Delta, float Length2, float HalfWidth)> Segments = new();
    public int SegmentCount => Segments.Count;

    public LandmarkGroundMask(WorldGeometry World, Rect2 Area, float MaxMargin = 8)
    {
        this.World = World;
        foreach (var (Path, Width) in World.Trails)
            for (int I = 0; I < Path.Length - 1; I++)
            {
                Vector2 A = Path[I], B = Path[I + 1], Delta = B - A;
                Rect2 Bounds = new Rect2(A.Min(B), Delta.Abs()).Grow(Width / 2 + MaxMargin);
                if (Bounds.Intersects(Area)) Segments.Add((A, Delta, Math.Max(Delta.LengthSquared(), 0.001f), Width / 2));
            }
    }

    // Same accepted terrain types as TerrainAt(P, Margin) is 0 or 4 or 9, within the constructor's area.
    public bool CanPaint(Vector2 P, float Margin)
    {
        if (!IslandMap.OnLand(P) || !IslandMap.OnGrass(P, Margin)) return false;
        foreach (var (Floor, _) in World.Floors) if (Floor.Grow(Margin).HasPoint(P)) return false;
        foreach (var Pond in World.Ponds)
            if (P.DistanceTo(Pond.Centre) < Pond.Radius * 1.6f + Margin && IslandMap.InPolygon(Pond.Edge, P)) return false;
        foreach (var (A, Delta, Length2, HalfWidth) in Segments)
        {
            float T = Math.Clamp((P - A).Dot(Delta) / Length2, 0, 1);
            float Reach = HalfWidth + Margin;
            if (P.DistanceSquaredTo(A + Delta * T) < Reach * Reach) return false;
        }
        return true;
    }
}
