using Godot;
using System;
using System.Linq;
using HoldTilDawn;

public partial class ExplorationChestPreview : Node
{
    public override async void _Ready()
    {
        try
        {
            const string Out = "res://.tmp/expedition-chests";
            DirAccess.MakeDirRecursiveAbsolute(Out);
            var Game = new Main(); AddChild(Game); Game.SetPhysicsProcess(false); Game.SetProcess(false);
            Game.Sim = new GameSim(); Game.Sim.AddPlayer(1,"Scout",0); Game.Sim.AddPlayer(2,"Engineer",2);
            Game.Sim.StartMatch(); Game.MyId = 1;
            dynamic P = Game.Sim.Players[1];
            var Chest = Game.Sim.ExplorationChests.First(C => C.Landmark == 5);
            var Camera = (Camera2D)Gd.Unwrap(Game.Cam);
            string[] Names = { "sealed", "unlocking", "opened", "storage", "radio-build", "radio-online", "map" };
            for (int Stage = 0; Stage < Names.Length; Stage++)
            {
                Vector2 At = Stage < 4 ? Chest.Pos : GameSim.WORKBENCH_POS;
                Game.Sim.Teleport(P, Stage < 4 ? At + new Vector2(0,32) : At + new Vector2(0,24));
                if (Stage == 0) Game.Sim.SetCount(P,"chest_key",2);
                if (Stage == 1) { P.Inter = true; Game.Sim.Interact(P,1f); }
                if (Stage == 2) { P.Inter = true; Game.Sim.Interact(P,1.1f); }
                Game.BagOpen = Stage == 3; Game.BagExplorationChest = Stage == 3 ? Chest.Id : -1;
                if (Stage == 4)
                {
                    for (int I=0; I<3; I++) Game.Sim.RescueComponents[I] = 1;
                    P.Inter = true; Game.Sim.TryBuildRadio(P,1.5f);
                }
                if (Stage == 5) Game.Sim.TryBuildRadio(P,1.6f);
                Game.LocalPos = P.Pos; Game.MapBig = Stage == 6;
                Game.Sim.Clock += .5f;
                Game.OnSnap(Game.Sim.Snapshot());
                if (Stage == 2 && (!Chest.Opened || !Gd.Truth(Game.BagOpen) || Game.BagExplorationChest != Chest.Id))
                    throw new Exception("Unlocking must automatically open the correct storage panel");
                Game.BagOpen = Stage == 3; Game.BagExplorationChest = Stage == 3 ? Chest.Id : -1;
                if (Stage == 3 && Game.BagSlots(new Vector2(960,540),Game.Sim.Snapshot().Get("ps").Get(1)).Count != 16)
                    throw new Exception("Chest UI must have eight bag and eight chest slots");
                Game.LocalTp = P.Tp; Game.PendingInputs.Clear();
                for (int Frame=0; Frame<20; Frame++)
                {
                    Game._Process(.016); Game.TickLandmarkGround();
                    Camera.Position = At; Camera.Zoom = Vector2.One * (Stage < 4 ? 2 : 1.5f); Camera.ResetSmoothing();
                    Game.Hud.Visible = true; Game.Banner.Visible = false;
                    Game.QueueRedraw(); Game.Hud.QueueRedraw();
                    await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                }
                using var Shot = GetViewport().GetTexture().GetImage(); Shot.SavePng($"{Out}/{Names[Stage]}.png");
                GD.Print("CHEST PREVIEW OK: " + Names[Stage]);
            }
            GetTree().Quit();
        }
        catch (Exception E) { GD.PushError(E.ToString()); GetTree().Quit(1); }
    }
}
