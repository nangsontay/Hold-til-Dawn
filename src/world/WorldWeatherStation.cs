using Godot;
using System.Collections.Generic;

public partial class WorldGeometry
{
    public static readonly Vector2 WeatherHalf = new(144, 112);
    public Vector2 WeatherCentre { get; private set; }
    public Rect2 WeatherRoom => new(WeatherCentre - WeatherHalf, WeatherHalf * 2);
    public Vector2 WeatherEntrance => WeatherCentre + new Vector2(0, WeatherHalf.Y);
    public Rect2 WeatherVisibility => new(WeatherRoom.Position - new Vector2(12, 66), WeatherRoom.Size + new Vector2(24, 80));
    public readonly List<(Vector2 A, Vector2 B)> WeatherWalls = new();
    public readonly List<Vector2[]> WeatherWallFaces = new();
    public bool IsWeatherInterior(Vector2 P) => WeatherRoom.Grow(-10).HasPoint(P);

    void BuildWeatherStation()
    {
        WeatherCentre = CompoundSpot(9, WeatherHalf + new Vector2(56, 80));
        LmAt[9] = WeatherCentre; Compounds[9] = WeatherRoom;
        WeatherWalls.Clear(); WeatherWallFaces.Clear();
        Vector2 NW = WeatherRoom.Position, SE = WeatherRoom.End;
        void Wall(Vector2 A, Vector2 B)
        {
            WeatherWalls.Add((A, B)); CompoundLine(A, B, 9);
            WeatherWallFaces.Add(new[] { A, B, B - new Vector2(0, 56), A - new Vector2(0, 56) });
        }
        Wall(NW, new(SE.X, NW.Y)); Wall(NW, new(NW.X, SE.Y)); Wall(new(SE.X, NW.Y), SE);
        Wall(new(NW.X, SE.Y), WeatherEntrance - new Vector2(42, 0));
        Wall(WeatherEntrance + new Vector2(42, 0), SE);
        Floors.Add((WeatherRoom, false));
        Clear(WeatherCentre, 8 * TL);
        CompoundEntrances[9] = WeatherEntrance + new Vector2(0, 40);
        // Scenery only: these names have no harvest, use, loot or crafting behaviour.
        Prop(WeatherCentre + new Vector2(-76, -55), "lm_console_0");
        Prop(WeatherCentre + new Vector2(78, -61), "lm_rack_0");
        Solid(WeatherCentre + new Vector2(-76, -55), 24);
        Solid(WeatherCentre + new Vector2(78, -61), 22);
        Prop(WeatherCentre + new Vector2(104, 35), "lm_crate_0");
        Solid(WeatherCentre + new Vector2(104, 35), 16);
        Prop(WeatherCentre + new Vector2(-224, -16), "lm_radar_0");
        Solid(WeatherCentre + new Vector2(-224, -16), 14);
        Prop(WeatherCentre + new Vector2(220, 20), "lm_solar_0");
        Solid(WeatherCentre + new Vector2(220, 20), 15);
        Prop(WeatherCentre + new Vector2(205, -112), "lm_windsock_0");
        Solid(WeatherCentre + new Vector2(205, -112), 5);
    }
}
