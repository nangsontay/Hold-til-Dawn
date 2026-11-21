using Godot;
using System.Collections.Generic;
using HoldTilDawn;

public partial class Main
{
    // Positions are fixed for the match. Only amounts/sprites change, so index positions once and read
    // the latest row by index. Rendering a small camera no longer visits the entire forest every frame.
    sealed class SceneIndex
    {
        public int Count = -1;
        public object Source;
        readonly Dictionary<Vector2I, List<int>> Cells = new();
        public IEnumerable<GArray> Visible(GArray Rows, int PositionField, Rect2 View, bool FixedSource = false)
        {
            if (Count != Rows.Count || (FixedSource && !ReferenceEquals(Source, Rows)))
            {
                Count = Rows.Count; Source = Rows; Cells.Clear();
                for (int I = 0; I < Rows.Count; I++)
                {
                    var Cell = (Vector2I)(((Vector2)((GArray)Rows[I])[PositionField]) / 256).Floor();
                    if (!Cells.TryGetValue(Cell, out var Bucket)) Cells[Cell] = Bucket = new();
                    Bucket.Add(I);
                }
            }
            var Lo = (Vector2I)(View.Position / 256).Floor();
            var Hi = (Vector2I)(View.End / 256).Floor();
            for (int Y = Lo.Y; Y <= Hi.Y; Y++)
                for (int X = Lo.X; X <= Hi.X; X++)
                    if (Cells.TryGetValue(new Vector2I(X, Y), out var Bucket))
                        foreach (int I in Bucket)
                        {
                            var Row = (GArray)Rows[I];
                            if (View.HasPoint((Vector2)Row[PositionField])) yield return Row;
                        }
        }
    }
    readonly SceneIndex ResourceSceneIndex = new(), DecoSceneIndex = new();
}
