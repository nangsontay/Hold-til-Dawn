using Godot;
using System;
using HoldTilDawn;

// Owner: Mai Thien Ngon. Where your teammates and tonight's supplies are. A teammate on screen gets a marker in
// their role colour over the head; one off screen (or out of sight inside the base) is pinned to the screen edge
// with their name, pointing their way. A dead teammate's marker shows a cross, so you can bring a Revival kit.
// Expedition chest markers stay on the map; their hidden radio parts have no edge pointers.
public partial class Main : Node2D
{
    // in HUD units (the HUD is drawn at 960 x 540 and scaled to the window)
    const float EDGE_PAD = 24f;

    public void DrawTeamMarkers(CanvasItem C, Transform2D Ct, Vector2 Vs)
    {
        var Screen = new Rect2(Vector2.Zero, Vs);
        // inside the HUD: below the generator and night bars, above the hotbar
        bool Night = Gd.Get(Snap, "ph", "") == "night";
        float Top = Night ? 66 + NIGHT_BAR_H : 66;
        var Edge = new Rect2(EDGE_PAD, Top, Vs.X - 2 * EDGE_PAD, Vs.Y - Top - 84);
        Vector2 Mid = Vs / 2;
        float Bob = 2.0f * Mathf.Sin((float)Clock * 4.0f);
        dynamic Cw = Gd.Wrap(C);
        Placed.Clear();
        foreach (dynamic Id in Gd.Iter(Snap.Ps))
        {
            if (Id == MyId)
            {
                continue;
            }
            dynamic P = Gd.At(Snap.Ps, Id);
            int State = Gd.Int(Gd.At(P, 5));
            Color Col = (Color)Gd.At(ROLE_COL, Gd.At(P, 1));
            Vector2 Foot = (Vector2)Gd.Get(ShownP, Id, Gd.At(P, 2));
            Vector2 Head = Ct * (Foot - new Vector2(0, (State == 0 ? (float)CHAR_H + 16 : 18)));
            bool Hidden = this.HiddenInBase(Foot);
            if (Screen.Grow(-8).HasPoint(Head) && !Hidden)
            {
                // on screen: a small chevron over the name
                Vector2 Tip = Head + new Vector2(0, 2 + Bob);
                var Chev = new[] { Tip, Tip + new Vector2(-6, -7), Tip + new Vector2(6, -7) };
                C.DrawColoredPolygon(Chev, Col);
                C.DrawPolyline(new[] { Chev[0], Chev[1], Chev[2], Chev[0] }, new Color(0.08f, 0.07f, 0.1f), 1.5f);
                if (State == 2)
                {
                    this.DeadCross(C, Head + new Vector2(0, -16));
                }
                continue;
            }
            // off screen: pinned to the edge, pointing at them
            Vector2 Dir = (Head - Mid).Normalized();
            Vector2 At = Hidden && Screen.HasPoint(Head) ? Head : Spread(EdgePoint(Mid, Dir, Edge), Edge);
            var Fill = State == 0 ? Col : Col.Darkened(0.45f);
            C.DrawCircle(At, 11, new Color(0.08f, 0.07f, 0.1f, 0.85f));
            C.DrawArc(At, 11, 0, Mathf.Tau, 20, Fill, 2.0f);
            Vector2 Side = new Vector2(-Dir.Y, Dir.X);
            Vector2 Nose = At + Dir * 17;
            C.DrawColoredPolygon(new[] { Nose, At + Dir * 10 + Side * 6, At + Dir * 10 - Side * 6 }, Fill);
            if (State == 2)
            {
                this.DeadCross(C, At);
            }
            else
            {
                C.DrawCircle(At, 5, Fill);
            }
            string Name = Gd.Str(Gd.At(P, 0));
            Vector2 Lp = At + new Vector2(-50, 25);
            this.Text(Cw, Lp, Name, 10, Fill.Lightened(0.25f), 1, 100);
        }

    }

    // markers already placed this frame, so two never sit on top of each other
    readonly System.Collections.Generic.List<Vector2> Placed = new();

    // slides an edge marker along its edge until it is clear of the others
    Vector2 Spread(Vector2 At, Rect2 Edge)
    {
        bool Upright = Mathf.Abs(At.X - Edge.Position.X) < 1f || Mathf.Abs(At.X - Edge.End.X) < 1f;
        Vector2 Along = Upright ? Vector2.Down : Vector2.Right;
        for (int Tries = 0; Tries < 12; Tries++)
        {
            bool Clear = true;
            foreach (var P in Placed)
            {
                if (P.DistanceTo(At) < 30f)
                {
                    Clear = false;
                    break;
                }
            }
            if (Clear)
            {
                break;
            }
            At = (At - Along * 30f).Clamp(Edge.Position, Edge.End);
            if (Tries == 5)
            {
                Along = -Along;
            }
        }
        Placed.Add(At);
        return At;
    }

    // where the ray from Mid along Dir leaves the rectangle R
    static Vector2 EdgePoint(Vector2 Mid, Vector2 Dir, Rect2 R)
    {
        float Tx = Mathf.Abs(Dir.X) > 0.0001f ? ((Dir.X > 0 ? R.End.X : R.Position.X) - Mid.X) / Dir.X : float.MaxValue;
        float Ty = Mathf.Abs(Dir.Y) > 0.0001f ? ((Dir.Y > 0 ? R.End.Y : R.Position.Y) - Mid.Y) / Dir.Y : float.MaxValue;
        return Mid + Dir * Mathf.Min(Tx, Ty);
    }

    void DeadCross(CanvasItem C, Vector2 At)
    {
        var Red = (Color)NEON_RED;
        C.DrawLine(At + new Vector2(-4, -4), At + new Vector2(4, 4), Red, 2.5f);
        C.DrawLine(At + new Vector2(4, -4), At + new Vector2(-4, 4), Red, 2.5f);
    }
}
