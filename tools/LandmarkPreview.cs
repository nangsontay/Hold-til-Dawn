using Godot;
using System;
using System.Threading.Tasks;
using System.Linq;
using HoldTilDawn;

// Atlas review tool: actual game renderer, all 18 landmarks, no HUD. Not part of the shipping scene.
// godot --path . tools/LandmarkPreview.tscn -- --out=.tmp/landmark-review [--landmark=9] [--night]
public partial class LandmarkPreview : Node
{
    public override async void _Ready()
    {
        try { await Capture(); }
        catch (Exception Error) { GD.PushError(Error.ToString()); GetTree().Quit(1); }
    }

    async Task Capture()
    {
        string Out = "res://.tmp/landmark-review";
        int Only = 0;
        bool Night = false;
        foreach (string Arg in OS.GetCmdlineUserArgs())
        {
            if (Arg.StartsWith("--out=")) Out = ProjectSettings.GlobalizePath(Arg[6..]);
            if (Arg.StartsWith("--landmark=")) Only = int.Parse(Arg[11..]);
            if (Arg == "--night") Night = true;
        }
        DirAccess.MakeDirRecursiveAbsolute(Out);
        var Game = new Main();
        AddChild(Game);
        Game.StartLocal();
        Game.Sim.Action(1, "start", 0);
        Game.Sim.NextWave = float.PositiveInfinity;
        Game.Sim.SetCount(Game.Sim.Players[1], "titanium_armour", 1);
        Game.SetPhysicsProcess(false);
        var W = (WorldGeometry)Game.Geometry;
        // Catch silent placement rejection of the large new silhouettes during every visual review.
        foreach (string Hero in new[] { "lm_radar_0", "lm_crane_0", "lm_arch_0", "lm_scope_0" })
            if (!W.LmProps.Any(P => P.Sprite == Hero)) throw new InvalidOperationException($"Landmark prop was not placed: {Hero}");
        foreach (var (P, Sprite) in W.LmProps.Concat(W.LmGround))
            if (!Godot.FileAccess.FileExists($"res://art/landmarks/{Sprite}.png") && !Godot.FileAccess.FileExists($"res://art/nature/{Sprite}.png"))
                throw new InvalidOperationException($"Missing landmark sprite: {Sprite}");
        foreach (var Landmark in IslandMap.Landmarks)
        {
            int Id = Landmark.Id;
            if (Only != 0 && Only != Id) continue;
            Vector2 Centre = Id == 1 ? Vector2.Zero : W.LmAt.TryGetValue(Id, out Vector2 At) ? At : Landmark.Pos;
            if (Id == 8) Centre = W.SwampCentre;
            if (Id == 15 && W.Ponds.Count > 0) Centre = W.Ponds[0].Centre;
            // Tall props need room above their foot. The wreck and outpost need a wider framing.
            Centre += new Vector2(0, Id is 6 or 9 or 13 or 14 or 16 ? -32 : -12);
            Game.Sim.Teleport(Game.Sim.Players[1], IslandMap.PullInland(Centre + new Vector2(0, 100), 12));
            Game.Sim.Phase = Night ? "night" : "day";
            Game.Sim.Active = new GArray();
            Game.OnSnap(Gd.FromVariant(Game.Sim.Snapshot()));
            Game.LocalPos = Game.Sim.Players[1].Pos;
            Game._Process(0.016);
            Game.SetProcess(false);
            Game.Hud.Visible = false;
            Game.Banner.Visible = false;
            var Camera = (Camera2D)Gd.Unwrap(Game.Cam);
            Camera.Position = Centre;
            Camera.Zoom = Vector2.One * (Id is 1 or 8 or 14 ? 1.0f : 1.5f);
            Camera.ResetSmoothing();
            Game.QueueRedraw();
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            // Normal gameplay advances these jobs in _Process; this tool freezes the game for screenshots.
            while (Game.HasPendingLandmarkGround)
            {
                Game.TickLandmarkGround();
                Game.QueueRedraw();
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            }
            using var Img = GetViewport().GetTexture().GetImage();
            Img.Convert(Image.Format.Rgba8);
            Img.LinearToSrgb();
            string Path = Out.PathJoin($"{(Night ? "night" : "after")}-{Id:00}.png");
            Error Result = Img.SavePng(Path);
            if (Result != Error.Ok) throw new InvalidOperationException($"Cannot save {Path}: {Result}");
            GD.Print($"LANDMARK {Id:00}: {Landmark.Name} -> {Path}");
            // Let the engine flush the rendering resources before the next teleport.
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        GetTree().Quit();
    }
}
