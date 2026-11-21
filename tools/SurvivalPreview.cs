using Godot;
using System;
using HoldTilDawn;

// Capture the actual renderer and HUD for the exploration / rescue update.
public partial class SurvivalPreview : Node
{
    public override async void _Ready()
    {
        try
        {
            DirAccess.MakeDirRecursiveAbsolute("res://.tmp/survival-preview");
            var Game = new Main(); AddChild(Game); Game.SetPhysicsProcess(false); Game.SetProcess(false);
            Game.Sim = new GameSim(); Game.Sim.AddPlayer(1, "Engineer", 2); Game.Sim.StartMatch(); Game.MyId = 1;
            dynamic P = Game.Sim.Players[1];
            var W = (WorldGeometry)Game.Geometry;
            for (int Stage = 0; Stage < 6; Stage++)
            {
                Vector2 At = Stage switch {
                    0 => W.Crossings[0].At, 1 => W.SwampCentre,
                    2 => W.LmAt[9], _ => GameSim.WORKBENCH_POS };
                Game.Sim.Teleport(P, At + new Vector2(0, Stage >= 3 ? 24 : 65));
                P.Hp = Stage == 4 ? P.Maxhp * .2f : P.Maxhp;
                if (Stage == 5)
                {
                    P.Pos = Game.Sim.FreeTowerSpot(0) + new Vector2(0, 40);
                    At = Game.Sim.FreeTowerSpot(0);
                    Game.Sim.BuildTower(P, At); Game.Sim.TickTowerConstruction(P, 3);
                }
                Game.LocalPos = P.Pos; Game.CraftOpen = Stage == 3;
                Game.CraftTier = 3; Game.CraftSel = 19;
                Game.OnSnap(Game.Sim.Snapshot());
                Game.LocalTp = P.Tp; Game.PendingInputs.Clear();
                var Camera = (Camera2D)Gd.Unwrap(Game.Cam);
                for (int Frame = 0; Frame < 20; Frame++)
                {
                    Game._Process(.016); Game.TickLandmarkGround();
                    Camera.Position = At - new Vector2(0, Stage == 2 ? 30 : 0);
                    Camera.Zoom = Vector2.One * (Stage is 1 or 2 ? 1.25f : 1.5f);
                    Camera.ResetSmoothing();
                    Game.Hud.Visible = Stage >= 3; Game.Banner.Visible = false;
                    Game.QueueRedraw(); Game.Hud.QueueRedraw();
                    await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                }
                using var Shot = GetViewport().GetTexture().GetImage();
                Shot.SavePng($"res://.tmp/survival-preview/{Stage}.png");
                GD.Print($"SURVIVAL PREVIEW {Stage} OK");
            }
            GD.Print("SURVIVAL RENDER ALL OK"); GetTree().Quit();
        }
        catch (Exception Error) { GD.PushError(Error.ToString()); GetTree().Quit(1); }
    }
}
