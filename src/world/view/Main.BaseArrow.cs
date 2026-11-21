using Godot;
using System;
using HoldTilDawn;

// Owner: Vu Duc Viet Anh. Outside the base a small arrow circles your character and always points at the base
// (its centre, the lighthouse), so you never lose your way home.
public partial class Main : Node2D
{
    // how far from the character's centre the arrow sits, in screen pixels
    const float BASE_ARROW_R = 62f;
    static readonly Color BASE_ARROW_COL = new Color(1.0f, 0.78f, 0.32f);

    public void DrawBaseArrow(CanvasItem C, Transform2D Ct)
    {
        dynamic Me = Gd.Get(Snap.Ps, MyId);
        if ((Me == null) || (Gd.Int(Gd.At(Me, 5)) == 2) || Gd.Truth(GameSim.InsideBase(LocalPos)))
        {
            return;
        }
        Vector2 To = -LocalPos;
        float Dist = To.Length();
        if (Dist < 1f)
        {
            return;
        }
        Vector2 Dir = To / Dist;
        // the character's middle on screen (the feet are at LocalPos)
        Vector2 Mid = Ct * (LocalPos + new Vector2(0, -14));
        // fade in as you walk away from the walls, so it does not sit on top of the base right outside a door
        float Near = Mathf.Clamp(((Dist - ((Vector2)GameSim.HALF).Length()) / (4f * T)), 0.35f, 1f);
        Vector2 Tip = Mid + Dir * (BASE_ARROW_R + 9f);
        Vector2 Back = Mid + Dir * (BASE_ARROW_R - 7f);
        Vector2 Side = new Vector2(-Dir.Y, Dir.X);
        // a gentle pulse along its direction
        float Bob = 2.5f * Mathf.Sin((float)Clock * 4f);
        Tip += Dir * Bob;
        Back += Dir * Bob;
        Vector2[] Head = { Tip, Back + Side * 9f, Back + Dir * 4f, Back - Side * 9f };
        Vector2[] Rim = new Vector2[Head.Length + 1];
        Head.CopyTo(Rim, 0);
        Rim[Head.Length] = Head[0];
        var Col = BASE_ARROW_COL;
        Col.A = Near;
        var Ink = (Color)INK;
        Ink.A = 0.85f * Near;
        C.DrawColoredPolygon(Head, Col);
        C.DrawPolyline(Rim, Ink, 2.0f);
    }
}
