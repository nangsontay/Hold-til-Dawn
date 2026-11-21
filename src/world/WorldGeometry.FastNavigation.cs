using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// Typed navigation shares the collision snapshot used by FastMove. No dictionary/Variant conversions
// inside the grid raster or segment checks; a coastline mask survives resource/tower changes.
public partial class WorldGeometry
{
    const int NAV_REACH = 128, NAV_WIDTH = 257;
    sealed class NavigationGrid
    {
        public AStarGrid2D Grid;
        public readonly Dictionary<(Vector2I From, Vector2I To), Vector2I[]> Paths = new();
    }
    readonly Dictionary<(float Radius, Rect2[] Walls), NavigationGrid> FastGrids = new();
    readonly Dictionary<float, Rect2I[]> SeaCells = new();
    public int NavigationGridBuilds { get; private set; }
    public int NavigationPathSearches { get; private set; }

    void InvalidateFastNavigation()
    {
        foreach (var Entry in FastGrids.Values) Entry.Grid.Dispose();
        FastGrids.Clear();
    }

    NavigationGrid GetNavigationGrid(float Radius, Rect2[] Walls)
    {
        if (FastGrids.TryGetValue((Radius, Walls), out var Cached)) return Cached;
        var Grid = new AStarGrid2D {
            Region = new Rect2I(-NAV_REACH, -NAV_REACH, NAV_WIDTH, NAV_WIDTH),
            CellSize = Vector2.One * 32,
            DiagonalMode = AStarGrid2D.DiagonalModeEnum.OnlyIfNoObstacles };
        Grid.Update();
        if (!SeaCells.TryGetValue(Radius, out var Sea))
        {
            var Cells = new List<Rect2I>();
            for (int Y = -NAV_REACH; Y <= NAV_REACH; Y++)
            {
                int Start = int.MinValue;
                for (int X = -NAV_REACH; X <= NAV_REACH + 1; X++)
                {
                    bool Solid = X <= NAV_REACH && !IslandMap.OnLand(new Vector2(X, Y) * 32, Radius);
                    if (Solid && Start == int.MinValue) Start = X;
                    if (!Solid && Start != int.MinValue)
                    {
                        Cells.Add(new Rect2I(Start, Y, X - Start, 1));
                        Start = int.MinValue;
                    }
                }
            }
            Sea = Cells.ToArray();
            SeaCells[Radius] = Sea;
        }
        // Fill contiguous sea spans; no interop call for every ocean cell.
        foreach (Rect2I Span in Sea) Grid.FillSolidRegion(Span);
        foreach (var (Centre, SolidRadius) in SolidT)
        {
            float Reach = SolidRadius + Radius + 1;
            var Lo = (Vector2I)((Centre - Vector2.One * Reach) / 32).Floor();
            var Hi = (Vector2I)((Centre + Vector2.One * Reach) / 32).Ceil();
            for (int Y = Math.Max(-NAV_REACH, Lo.Y); Y <= Math.Min(NAV_REACH, Hi.Y); Y++)
                for (int X = Math.Max(-NAV_REACH, Lo.X); X <= Math.Min(NAV_REACH, Hi.X); X++)
                    if ((new Vector2(X, Y) * 32).DistanceSquaredTo(Centre) < Reach * Reach)
                        Grid.SetPointSolid(new Vector2I(X, Y));
        }
        foreach (Rect2 Wall in Walls)
        {
            Rect2 Box = Wall.Grow(Radius + 1);
            var Lo = (Vector2I)(Box.Position / 32).Floor();
            var Hi = (Vector2I)(Box.End / 32).Ceil();
            for (int Y = Math.Max(-NAV_REACH, Lo.Y); Y <= Math.Min(NAV_REACH, Hi.Y); Y++)
                for (int X = Math.Max(-NAV_REACH, Lo.X); X <= Math.Min(NAV_REACH, Hi.X); X++)
                    if (Box.HasPoint(new Vector2(X, Y) * 32)) Grid.SetPointSolid(new Vector2I(X, Y));
        }
        var Result = new NavigationGrid { Grid = Grid };
        FastGrids[(Radius, Walls)] = Result;
        NavigationGridBuilds++;
        return Result;
    }

    public Vector2 WaypointFast(Vector2 From, Vector2 To, float Radius, Rect2[] Walls)
    {
        // The destination may be a resource/tower centre, as in the original Waypoint.
        if (NavigationSegmentClear(From, To, Radius, Walls, To)) return To;
        var Cache = GetNavigationGrid(Radius, Walls);
        Vector2I Start = OpenCellFast(Cache.Grid, From), End = OpenCellFast(Cache.Grid, To);
        if (!Cache.Paths.TryGetValue((Start, End), out var Path))
        {
            var Found = Cache.Grid.GetIdPath(Start, End, true);
            Path = Found.ToArray();
            NavigationPathSearches++;
            if (Cache.Paths.Count >= 512) Cache.Paths.Clear();
            Cache.Paths[(Start, End)] = Path;
        }
        for (int I = Math.Min(Path.Length - 1, 5); I >= 0; I--)
        {
            Vector2 P = (Vector2)Path[I] * 32;
            if (From.DistanceSquaredTo(P) >= 4 && NavigationSegmentClear(From, P, Radius, Walls, Vector2.Inf)) return P;
        }
        // Keep the original slide-out fallback when a body is pressed against scenery.
        for (int I = Math.Min(Path.Length - 1, 1); I >= 0; I--)
        {
            Vector2 P = (Vector2)Path[I] * 32;
            if (From.DistanceSquaredTo(P) >= 4) return P;
        }
        return From;
    }

    bool NavigationSegmentClear(Vector2 From, Vector2 To, float Radius, Rect2[] Walls, Vector2 Ignore)
    {
        Vector2 Step = To - From;
        float Length2 = Step.LengthSquared();
        foreach (int I in CandidatesFast(From, To, Radius))
        {
            var (Centre, R) = SolidT[I];
            if (R <= 0 || Centre.IsEqualApprox(Ignore)) continue;
            Vector2 Start = From - Centre;
            float Reach = Radius + R;
            if (Start.LengthSquared() <= Reach * Reach) return false;
            if (Length2 < 0.000001f) continue;
            float Dot = Start.Dot(Step), Disc = Dot * Dot - Length2 * (Start.LengthSquared() - Reach * Reach);
            if (Disc < 0) continue;
            float Hit = (-Dot - Mathf.Sqrt(Disc)) / Length2;
            if (Hit >= 0 && Hit <= 1) return false;
        }
        foreach (Rect2 Wall in Walls)
            if (SegmentBoxFast(From, To, Wall.Grow(Radius))) return false;
        // no short cut over the water: the sea, and the rivers beside their bridges (IslandMap.OnLand)
        int Samples = (int)(Mathf.Sqrt(Length2) / 12.0f) + 1;
        for (int K = 1; K <= Samples; K++)
            if (!IslandMap.OnLand(From + Step * ((float)K / Samples), Radius * 0.9f)) return false;
        return true;
    }

    static bool SegmentBoxFast(Vector2 A, Vector2 B, Rect2 Box)
    {
        float Enter = 0, Leave = 1;
        Vector2 Step = B - A;
        for (int Axis = 0; Axis < 2; Axis++)
        {
            if (Mathf.Abs(Step[Axis]) < 0.00001f)
            {
                if (A[Axis] < Box.Position[Axis] || A[Axis] > Box.End[Axis]) return false;
            }
            else
            {
                float T0 = (Box.Position[Axis] - A[Axis]) / Step[Axis], T1 = (Box.End[Axis] - A[Axis]) / Step[Axis];
                Enter = Math.Max(Enter, Math.Min(T0, T1));
                Leave = Math.Min(Leave, Math.Max(T0, T1));
                if (Enter > Leave) return false;
            }
        }
        return true;
    }

    static Vector2I OpenCellFast(AStarGrid2D Grid, Vector2 Pos)
    {
        var Cell = (Vector2I)(Pos / 32).Round();
        for (int Ring = 0; Ring < 7; Ring++)
        {
            Vector2I Best = Cell;
            float Distance = float.PositiveInfinity;
            for (int Y = -Ring; Y <= Ring; Y++)
                for (int X = -Ring; X <= Ring; X++)
                {
                    var C = Cell + new Vector2I(X, Y);
                    if (!Grid.IsInBoundsv(C) || Grid.IsPointSolid(C)) continue;
                    float D = ((Vector2)C * 32).DistanceSquaredTo(Pos);
                    if (D < Distance) { Distance = D; Best = C; }
                }
            if (float.IsFinite(Distance)) return Best;
        }
        return Cell;
    }
}
