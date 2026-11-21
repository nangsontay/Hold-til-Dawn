using Godot;
using System;

// Compare the fast, spatially filtered decoration queries against the existing terrain rules.
public partial class LandmarkGroundCheck : Node
{
    public override void _Ready()
    {
        try
        {
            var W = new WorldGeometry();
            int Compared = 0, Segments = 0;
            foreach (var (_, Centre) in W.LmAt)
            {
                var Area = new Rect2(Centre - new Vector2(250, 180), new Vector2(500, 360));
                var Mask = new LandmarkGroundMask(W, Area);
                Segments += Mask.SegmentCount;
                foreach (float Margin in new[] { 4.0f, 8.0f })
                    for (float Y = Area.Position.Y; Y < Area.End.Y; Y += 16)
                        for (float X = Area.Position.X; X < Area.End.X; X += 16)
                        {
                            Vector2 P = new Vector2(X, Y);
                            bool Expected = W.TerrainAt(P, Margin) is 0 or 4 or 9;
                            if (Mask.CanPaint(P, Margin) != Expected) throw new InvalidOperationException($"Ground mask differs at {P}, margin {Margin}");
                            Compared++;
                        }
            }
            GD.Print($"LANDMARK GROUND ALL OK: {Compared} queries match existing terrain; average {Segments / W.LmAt.Count} nearby path segments per location");
            GetTree().Quit();
        }
        catch (Exception Error) { GD.PushError(Error.ToString()); GetTree().Quit(1); }
    }
}
