using Godot;
using System;
using HoldTilDawn;

// Review the real game renderer after changes to geography or landmark surfaces.
public partial class TerrainPreview : Node
{
    public override async void _Ready()
    {
        try
        {
            const string Out = "res://.tmp/terrain-review";
            DirAccess.MakeDirRecursiveAbsolute(Out);
            var Game = new Main(); AddChild(Game);
            Game.SetPhysicsProcess(false); Game.SetProcess(false);
            Game.Sim = new GameSim(); Game.Sim.AddPlayer(1, "Engineer", 2); Game.Sim.StartMatch(); Game.MyId = 1;
            if (Game.Sim.Phase != "day") throw new InvalidOperationException("Preview failed to start a match");
            dynamic Player = Game.Sim.Players[1];
            var World = (WorldGeometry)Game.Geometry;
            var Camera = (Camera2D)Gd.Unwrap(Game.Cam);
            bool CaveReview = Array.IndexOf(OS.GetCmdlineUserArgs(), "--cave-review") >= 0;
            bool GardenReview = Array.IndexOf(OS.GetCmdlineUserArgs(), "--garden-review") >= 0;
            bool StationReview = Array.IndexOf(OS.GetCmdlineUserArgs(), "--station-review") >= 0;
            bool FarmReview = Array.IndexOf(OS.GetCmdlineUserArgs(), "--farm-review") >= 0;
            bool ExpeditionReview = Array.IndexOf(OS.GetCmdlineUserArgs(), "--expedition-review") >= 0;
            bool CompoundReview = Array.IndexOf(OS.GetCmdlineUserArgs(), "--compound-review") >= 0;
            bool LookoutReview = Array.IndexOf(OS.GetCmdlineUserArgs(), "--lookout-review") >= 0;
            bool Focus = CaveReview || GardenReview || StationReview || FarmReview || ExpeditionReview || CompoundReview || LookoutReview;
            if (Focus) Game.Sim.ExplorationChests.Clear(); // Keep random chests out of focused landmark art review.
            bool Night = Array.IndexOf(OS.GetCmdlineUserArgs(), "--night") >= 0;
            Game.Sim.Phase = Night ? "night" : "day";
            for (int Stage = 0; Stage < (Focus ? 32 : 23); Stage++)
            {
                if (Focus && !((CaveReview && Stage is 14 or 18 or 22 or 23 or 24 or 26) || (GardenReview && Stage is 16 or 22 or 25)
                    || (StationReview && Stage is 7 or 8 or 27 or 28 or 29 or 30 or 31) || (FarmReview && Stage is 0 or 1 or 2 or 16 or 17 or 18 or 19 or 20 or 21 or 22)
                    || (ExpeditionReview && Stage is 3 or 5 or 9 or 22) || (CompoundReview && Stage is 4 or 11 or 22)
                    || (LookoutReview && Stage is 10 or 15 or 22))) continue;
                int Id = Stage >= 30 ? 10 : Stage >= 27 ? 9 : Stage < 17 ? Stage + 2 : Stage == 25 ? 18 : Stage >= 23 ? 16 : 0;
                Vector2 At = Id > 0 ? World.LmAt[Id] : Stage < 22 ? IslandMap.Bridges[Stage - 17].At : Vector2.Zero;
                if (Id == 8) At = World.SwampCentre;
                if (Id == 15) At = World.Ponds[0].Centre;
                Vector2 PlayerAt = Id == 16 ? World.CaveEntrance + new Vector2(24, 0) : At + new Vector2(0, 120);
                if (Stage == 24) PlayerAt = World.CaveCentre - new Vector2(3 * 32, 0);
                if (Id == 18) PlayerAt = World.GardenCentre;
                if (Stage == 25) PlayerAt = World.GardenEntrance + new Vector2(0, 32);
                if (Id == 9) PlayerAt = Stage == 28 ? World.WeatherCentre : World.WeatherEntrance + new Vector2(0, 50);
                if (Id == 10) PlayerAt = World.LmAt[10] - new Vector2(90, 0);
                if (Id is 2 or 3 or 4) PlayerAt = At;
                if (Id is 6 or 13) PlayerAt = At + new Vector2(0,32);
                if (Id is 12 or 17) PlayerAt = At;
                Game.Sim.Teleport(Player, IslandMap.PullInland(PlayerAt, 12));
                Game.LocalPos = Player.Pos; Game.OnSnap(Game.Sim.Snapshot());
                Game.LocalTp = Player.Tp; Game.PendingInputs.Clear();
                Game.MapBig = Stage == 22;
                for (int Frame = 0; Frame < (Focus ? 36 : 12) || Game.HasPendingLandmarkGround; Frame++)
                {
                    Game._Process(.016); Game.TickLandmarkGround();
                    Camera.Position = Id == 16 ? World.CaveCentre : At;
                    if (Id == 18) Camera.Position -= new Vector2(0, 24);
                    if (Id is 2 or 3 or 4) { Camera.Position -= new Vector2(0, 22); Camera.Zoom = Vector2.One * 1.2f; Camera.ResetSmoothing(); }
                    Camera.Zoom = Vector2.One * (Id == 16 ? .9f : Id == 18 ? 1f : Id is 6 or 13 ? 1.25f : 1.5f); Camera.ResetSmoothing();
                    if (Stage is 23 or 26)
                    {
                        Camera.Position = World.CaveEntrance - new Vector2(2 * 32, 0);
                        Camera.Zoom = Vector2.One * 1.5f; Camera.ResetSmoothing();
                    }
                    if (Stage == 25) { Camera.Position = World.GardenEntrance - new Vector2(0, 55); Camera.Zoom = Vector2.One * 1.5f; Camera.ResetSmoothing(); }
                    if (Id == 9) { Camera.Position = World.WeatherCentre - new Vector2(0, 8); Camera.Zoom = Vector2.One * 1.3f; Camera.ResetSmoothing(); }
                    if (Id == 10) { Camera.Position = World.LmAt[10] - new Vector2(60, 15); Camera.Zoom = Vector2.One * 1.65f; Camera.ResetSmoothing(); }
                    if (ExpeditionReview && Id is 5 or 7 or 11)
                    {
                        Camera.Position = World.LmAt[Id] - new Vector2(0, 18);
                        Camera.Zoom = Vector2.One * (Id == 7 ? 1.35f : Id == 11 ? .95f : 1.3f);
                        Camera.ResetSmoothing();
                    }
                    if (CompoundReview && Id is 6 or 13)
                    {
                        Camera.Position = At - new Vector2(0,30);
                        Camera.Zoom = Vector2.One * (Id==13 ? 1.1f : 1.45f);
                        Camera.ResetSmoothing();
                    }
                    if (LookoutReview && Id is 12 or 17)
                    {
                        Camera.Position = At + new Vector2(Id==12 ? 35 : 0,Id==12 ? -90 : 35);
                        Camera.Zoom = Vector2.One * (Id==12 ? 1.0f : 1.1f);
                        Camera.ResetSmoothing();
                    }
                    if (Stage >= 30) Game.Clock = Stage == 30 ? 5.8f : 6.65f; // Repeatable airborne fish / landing splash captures.
                    Game.Hud.Visible = Stage == 22; Game.Banner.Visible = false;
                    Game.QueueRedraw(); Game.Hud.QueueRedraw();
                    await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                }
                using var Shot = GetViewport().GetTexture().GetImage();
                if (Stage == 24 && Game.CaveRoofOpacity > .01f) throw new InvalidOperationException("The cave roof did not reveal its interior");
                if (Stage == 24)
                {
                    if (!Game.CaveInteriorActive || !Game.HiddenOutsideCave(World.CaveEntrance + new Vector2(80, 0)) || Game.HiddenOutsideCave((Vector2)Player.Pos))
                        throw new InvalidOperationException("Cave isolation hides the wrong objects");
                    foreach (Vector2I P in new[] { new Vector2I(5, 5), new(Shot.GetWidth() - 6, 5), new(5, Shot.GetHeight() - 6), new(Shot.GetWidth() - 6, Shot.GetHeight() - 6) })
                    {
                        Color Pixel = Shot.GetPixelv(P);
                        if (Pixel.R > .1f || Pixel.G > .1f || Pixel.B > .1f) throw new InvalidOperationException("Exterior scenery remains visible from inside the cave");
                    }
                }
                if (Stage == 26 && (Game.CaveInteriorActive || Game.CaveRoofOpacity < .99f)) throw new InvalidOperationException("The exterior did not reappear after exiting the cave");
                if (Stage == 28)
                {
                    if (!Game.WeatherInteriorActive || Game.WeatherRoofOpacity > .01f || !Game.HiddenOutsideInterior(World.WeatherEntrance + new Vector2(0, 80))
                        || Game.HiddenOutsideInterior(World.WeatherCentre)) throw new InvalidOperationException("Weather hut cutaway hides the wrong scene");
                    void CheckOutside(Vector2I P)
                    {
                        Color Pixel = Shot.GetPixelv(P);
                        if (Pixel.R > .1f || Pixel.G > .1f || Pixel.B > .1f) throw new InvalidOperationException($"Exterior scenery is visible from inside the weather hut at {P}");
                    }
                    for (int X = 0; X < Shot.GetWidth(); X++) { CheckOutside(new(X, 5)); CheckOutside(new(X, Shot.GetHeight() - 6)); }
                    for (int Y = 0; Y < Shot.GetHeight(); Y++) { CheckOutside(new(5, Y)); CheckOutside(new(Shot.GetWidth() - 6, Y)); }
                }
                if (Stage == 29 && (Game.WeatherInteriorActive || Game.WeatherRoofOpacity < .99f)) throw new InvalidOperationException("Weather hut exterior did not return on exit");
                string Name = Stage == 27 ? "weather-entry" : Stage == 28 ? "weather-interior" : Stage == 29 ? "weather-exit" : Stage >= 30 ? $"fishing-jump-{Stage - 29}"
                    : Stage == 23 ? "cave-entry" : Stage == 24 ? "cave-interior" : Stage == 25 ? "garden-entry" : Stage == 26 ? "cave-exit" : Id > 0 ? $"landmark-{Id:00}" : Stage < 22 ? $"bridge-{Stage - 17}" : "map";
                if (Night) Name += "-night";
                Shot.SavePng($"{Out}/{Name}.png"); GD.Print($"TERRAIN PREVIEW {Name}: {At / 32}");
            }
            GD.Print("TERRAIN PREVIEW ALL OK"); GetTree().Quit();
        }
        catch (Exception Error) { GD.PushError(Error.ToString()); GetTree().Quit(1); }
    }
}
