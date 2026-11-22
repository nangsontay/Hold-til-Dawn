using Godot;
using HoldTilDawn;

public partial class Main
{
    WorldGeometry WeatherRoofWorld;
    float WeatherRoofAlpha = 1, WeatherRoofClock;
    public bool WeatherInteriorActive { get; private set; }
    public float WeatherRoofOpacity => WeatherRoofAlpha;
    bool InteriorActive => CaveInteriorActive || WeatherInteriorActive;

    public bool HiddenOutsideInterior(Vector2 P) => HiddenOutsideCave(P) ||
        (WeatherInteriorActive && !((WorldGeometry)Geometry).WeatherRoom.HasPoint(P));

    void DrawWeatherGround(WorldGeometry W, Rect2 View)
    {
        if (!W.WeatherVisibility.Grow(80).Intersects(View)) return;
        Rect2 Room = W.WeatherRoom;
        if (WeatherInteriorActive) DrawRect(W.WeatherVisibility, new Color("080C10"));
        DrawRect(Room.Grow(9), new Color("484F4C"));
        DrawRect(Room, new Color("92958A"));
        for (float Y = Room.Position.Y; Y < Room.End.Y; Y += 32)
            for (float X = Room.Position.X; X < Room.End.X; X += 32)
            {
                var Tile = new Rect2(X + 1, Y + 1, 30, 30);
                DrawRect(Tile, new Color(H01((int)X, (int)Y) > .5f ? "A2A497" : "989D91"));
            }
        Vector2 C = W.WeatherCentre;
        // Worn entrance mat and a desk chair. These are inert room furnishings.
        DrawRect(new Rect2(C + new Vector2(-28, 67), new Vector2(56, 33)), new Color("5B6658"));
        for (int I = 0; I < 5; I++) DrawLine(C + new Vector2(-23, 73 + I * 5), C + new Vector2(23, 73 + I * 5), new Color("78806A"), 1);
        DrawRect(new Rect2(C + new Vector2(-87, -18), new Vector2(23, 21)), new Color("484E48"));
        DrawRect(new Rect2(C + new Vector2(-84, -16), new Vector2(17, 15)), new Color("726C51"));
        DrawRect(new Rect2(C + new Vector2(-84, -22), new Vector2(17, 5)), new Color("9B9070"));
        DrawRect(new Rect2(W.WeatherEntrance + new Vector2(-41, -7), new Vector2(82, 19)), new Color("8A8778"));
        DrawLine(W.WeatherEntrance + new Vector2(-37, 6), W.WeatherEntrance + new Vector2(37, 6), new Color("BBB59A"), 3);
    }

    void WeatherWallItems(GArray Items, Rect2 View)
    {
        var W = (WorldGeometry)Geometry;
        if (CaveInteriorActive || !W.WeatherVisibility.Intersects(View)) return;
        for (int I = 0; I < W.WeatherWalls.Count; I++)
        {
            var (A, B) = W.WeatherWalls[I];
            Items.Add(new GArray(Mathf.Max(A.Y, B.Y), 14, I));
        }
    }

    void DrawWeatherWall(int Index)
    {
        var W = (WorldGeometry)Geometry;
        var (A, B) = W.WeatherWalls[Index];
        Vector2 Lift = new(0, -56);
        FillStatic(W.WeatherWallFaces[Index], new Color(Index == 0 ? "B5B59E" : "888F80"));
        DrawLine(A, B, new Color("424E49"), 5);
        DrawLine(A + Lift, B + Lift, new Color("D0CBB0"), 7);
        DrawLine(A + new Vector2(0, -18), B + new Vector2(0, -18), new Color("737E70"), 2);
        for (int I = 1; I < 6; I++)
        {
            Vector2 P = A.Lerp(B, I / 6f).Round();
            DrawLine(P + Lift + new Vector2(0, 5), P - new Vector2(0, 3), new Color("939B88"), 1);
        }
        if (Index == 0)
        {
            Vector2 C = W.WeatherCentre;
            // Sealed windows show opaque blue glass, never exterior scenery.
            foreach (int Side in new[] { -1, 1 })
            {
                var Window = new Rect2(C + new Vector2(Side * 88 - 26, -161), new Vector2(52, 30));
                DrawRect(Window.Grow(3), new Color("555F57")); DrawRect(Window, new Color("729699"));
                DrawLine(Window.Position + new Vector2(26, 0), Window.Position + new Vector2(26, 30), new Color("C2C2A5"), 3);
                DrawLine(Window.Position + new Vector2(3, 6), Window.Position + new Vector2(20, 6), new Color("B0C7B9"), 2);
            }
            DrawRect(new Rect2(C + new Vector2(-24, -163), new Vector2(48, 33)), new Color("DBD4B2"));
            for (int I = 0; I < 4; I++) DrawLine(C + new Vector2(-17, -155 + I * 5), C + new Vector2(15 - I * 4, -155 + I * 5), new Color("7C8B84"), 2);
        }
        if (Index >= 3)
        {
            Vector2 Jamb = Index == 3 ? B : A;
            DrawLine(Jamb, Jamb + Lift, new Color("48594F"), 10);
            DrawLine(Jamb + new Vector2(-2, 0), Jamb + Lift + new Vector2(-2, 0), new Color("A7AA8C"), 3);
        }
    }

    void DrawWeatherRoof(WorldGeometry W, Rect2 View)
    {
        if (!ReferenceEquals(WeatherRoofWorld, W)) { WeatherRoofWorld = W; WeatherRoofAlpha = 1; WeatherRoofClock = (float)Clock; }
        float Now = (float)Clock, Dt = Mathf.Clamp(Now - WeatherRoofClock, 0, .1f); WeatherRoofClock = Now;
        WeatherRoofAlpha = Mathf.MoveToward(WeatherRoofAlpha, WeatherInteriorActive ? 0 : 1, Dt * 5);
        if (WeatherRoofAlpha <= 0 || !W.WeatherVisibility.Intersects(View)) return;
        // A raised metal roof covers furniture from outside and peels away on entry.
        Rect2 R = new(W.WeatherRoom.Position - new Vector2(14, 65), W.WeatherRoom.Size + new Vector2(28, 10));
        Color Roof = new("617C72", WeatherRoofAlpha), Seam = new("405E56", WeatherRoofAlpha);
        DrawRect(R.Grow(3), Seam); DrawRect(R, Roof);
        for (float X = R.Position.X + 7; X < R.End.X; X += 16)
        {
            DrawLine(new(X, R.Position.Y), new(X, R.End.Y), Seam, 2);
            DrawLine(new(X + 3, R.Position.Y), new(X + 3, R.End.Y), new Color("879B85", WeatherRoofAlpha), 1);
        }
        DrawLine(new(R.Position.X, R.GetCenter().Y), new(R.End.X, R.GetCenter().Y), new Color("A2AD90", WeatherRoofAlpha), 6);
        DrawRect(new Rect2(R.GetCenter() + new Vector2(70, -28), new Vector2(31, 25)), Seam);
        for (int I = 0; I < 4; I++) DrawLine(R.GetCenter() + new Vector2(74, -22 + I * 5), R.GetCenter() + new Vector2(96, -22 + I * 5), new Color("88998A", WeatherRoofAlpha), 2);
    }

    void DrawWeatherIsolation(WorldGeometry W)
    {
        if (!WeatherInteriorActive) return;
        Rect2 R = W.WeatherVisibility;
        Color Black = new("080C10");
        // Overlap the four panels so fractional camera zoom cannot expose a one-pixel seam.
        DrawRect(new Rect2(-32768, -32768, 65536, R.Position.Y + 32770), Black);
        DrawRect(new Rect2(-32768, R.End.Y - 2, 65536, 32770 - R.End.Y), Black);
        DrawRect(new Rect2(-32768, R.Position.Y - 2, R.Position.X + 32769, R.Size.Y + 4), Black);
        DrawRect(new Rect2(R.End.X - 1, R.Position.Y - 2, 32769 - R.End.X, R.Size.Y + 4), Black);
        // Fill the narrow apron up to the wall foundations, including the open doorway's outside view.
        DrawRect(new Rect2(R.Position.X, W.WeatherRoom.End.Y + 5, R.Size.X, R.End.Y - W.WeatherRoom.End.Y - 5), Black);
    }
}
