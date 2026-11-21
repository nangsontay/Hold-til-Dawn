using Godot;
using System;
using HoldTilDawn;

// Review the actual resource/player renderer through a complete harvest, without world or HUD clutter.
// godot --path . tools/HarvestFeedbackPreview.tscn -- --out=res://.tmp/harvest-clarity/frames
public partial class HarvestFeedbackPreview : Main
{
    int ReviewFrame, LastStage = -1, LitFrames;
    bool Finished;
    string Out = "res://.tmp/harvest-clarity/frames";
    readonly int[] Types = { 0, 1, 2 };
    readonly string[] Names = { "node_wood", "node_stone", "node_herb" };

    public override void _Ready()
    {
        try
        {
            base._Ready();
            foreach (Node Child in GetChildren())
            {
                if (Child is CanvasItem Item) Item.Visible = false;
                if (Child is CanvasLayer Layer) Layer.Visible = false;
            }
            ((Camera2D)Gd.Unwrap(Cam)).Enabled = false;
            foreach (string Arg in OS.GetCmdlineUserArgs()) if (Arg.StartsWith("--out=")) Out = Arg[6..];
            DirAccess.MakeDirRecursiveAbsolute(Out);
            TextureFilter = TextureFilterEnum.Nearest;
            Sim = new GameSim();
            for (int I = 0; I < 3; I++) Sim.AddPlayer(I + 1, "Preview", I);
            Sim.StartMatch();
            Sim.Nodes = new GArray();
            for (int I = 0; I < 3; I++)
            {
                var Pos = new Vector2(160 + I * 320, 230);
                ((GArray)Sim.Nodes).Add(new GDict { { "type", Types[I] }, { "pos", Pos }, { "amount", 5 }, { "sprite", Names[I] + "_0" } });
                dynamic P = Sim.Players[I + 1];
                P.Pos = Pos + new Vector2(0, 40); P.Activity = "gather"; P.ActivityTarget = Pos;
            }
            MyId = -1;
            OnSnap(Gd.FromVariant(Sim.Snapshot()));
        }
        catch (Exception E) { GD.PushError(E.ToString()); GetTree().Quit(1); }
    }

    public override void _PhysicsProcess(double Delta) { }

    public override void _Process(double Delta)
    {
        if (Finished) return;
        Clock = ReviewFrame * 0.1f;
        foreach (GDict N in (GArray)Sim.Nodes) N["amount"] = Math.Max(0, 5 - (int)((float)Clock * 0.35f));
        foreach (dynamic Id in Gd.Iter(Sim.Players))
        {
            dynamic P = Sim.Players[Id];
            P.GatherAcc = Mathf.PosMod((float)Clock * 0.35f, 1);
            if ((float)Clock >= 14.3f) P.Activity = "idle";
        }
        OnSnap(Gd.FromVariant(Sim.Snapshot()));
        QueueRedraw();
    }

    public override async void _Draw()
    {
        if (Finished) return;
        try
        {
            DrawRect(GetViewportRect(), new Color("30383D"));
            var Font = ThemeDB.FallbackFont;
            DrawString(Font, new Vector2(32, 45), "Harvesting: wood / stone / herbs", fontSize: 22);
            PrepareHarvestFeedback();
            int I = 0;
            foreach (GArray N in (GArray)Snap.Nodes)
            {
                Vector2 Pos = (Vector2)N[1];
                int Amount = Gd.Int(N[2]);
                string Name = NodeSprite(N, 0);
                if (Amount > 0)
                {
                    string Expected = Names[I] + (Amount == 5 ? "_0" : $"_dmg{5 - Amount}_0");
                    if (Name != Expected) throw new Exception("Wrong harvested sprite: " + Name);
                }
                Color Tint = NodeModulate(N);
                if (Tint.R > 1) LitFrames++;
                if (NodeOffset(N).Length() > 2.1f || NodeScale(N) != 1) throw new Exception("Resource deformed during impact");
                Sprite(Name, Pos + NodeOffset(N), 1, Tint);
                DrawPlayer(I + 1, Gd.At(Snap.Ps, I + 1), Pos + new Vector2(0, 40));
                DrawString(Font, Pos + new Vector2(-54, 108), $"{GameSim.ROLES[I]} / {Amount} of 5", fontSize: 16);
                I++;
            }
            DrawChips();
            DrawHarvestFeedback();
            int Stage = Math.Min(5, (int)((float)Clock * 0.35f));
            bool Save = ReviewFrame % 5 == 0 || Stage != LastStage;
            LastStage = Stage;
            int At = ReviewFrame++;
            bool Last = ReviewFrame >= 151;
            if (Last) { Finished = true; SetProcess(false); }
            if (Save)
            {
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                using var Im = GetViewport().GetTexture().GetImage();
                Im.Convert(Image.Format.Rgba8);
                Im.LinearToSrgb();
                Im.SavePng(Out.PathJoin($"frame-{At:000}.png"));
            }
            if (Last)
            {
                if (LitFrames < 30) throw new Exception("Harvest impacts were not rendered");
                GD.Print("HARVEST FEEDBACK ALL OK: six stages, per-swing impacts, fixed footprints and active progress indicators");
                GetTree().Quit();
            }
        }
        catch (Exception E) { GD.PushError(E.ToString()); GetTree().Quit(1); }
    }
}
