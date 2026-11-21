using Godot;
using System;
using System.Linq;
using HoldTilDawn;

// Exercises the actual player renderer with resource snapshots, all roles and all eight facings.
public partial class HarvestAnimCheck : Main
{
    int Pass;
    readonly string[] Expected = { "gather_wood", "gather_stone", "gather_herb", "gather_stone" };
    string Shot = "";

    public override void _Ready()
    {
        try
        {
            base._Ready();
            StartLocal();
            Sim.Action(1, "start", 0);
            Sim.Nodes = new GArray();
            for (int Type = 0; Type < 4; Type++)
                ((GArray)Sim.Nodes).Add(new GDict { { "type", Type }, { "pos", new Vector2(900 + Type * 100, 900) }, { "amount", 5 } });
            OnSnap(Gd.FromVariant(Sim.Snapshot()));
            for (int Type = 0; Type < 4; Type++)
                if (GatherAnimation(new Vector2(900 + Type * 100, 900)) != Expected[Type]) throw new Exception("Wrong resource animation");
            // A new world at the same node count must replace the cached resource classification too.
            ((GDict)((GArray)Sim.Nodes)[0])["type"] = 2;
            OnSnap(Gd.FromVariant(Sim.Snapshot()));
            if (GatherAnimation(new Vector2(900, 900)) != "gather_herb") throw new Exception("Stale resource animation cache");
            ((GDict)((GArray)Sim.Nodes)[0])["type"] = 0;
            OnSnap(Gd.FromVariant(Sim.Snapshot()));
            foreach (string Skin in new[] { "scout", "healer", "engineer" })
            {
                dynamic Info = Gd.At(Cinfo.Chars, Skin);
                using var Im = GD.Load<Texture2D>($"res://art/chars/{Skin}.png").GetImage();
                int Size = Gd.Int(Info.Size);
                foreach (string Name in Expected.Distinct())
                {
                    dynamic Anim = Gd.At(Info.Anims, Name);
                    int First = Gd.Int(Gd.At(Anim, 0)), Count = Gd.Int(Gd.At(Anim, 1));
                    if (First < 30 || (First + Count) * Size > Im.GetWidth() || Im.GetHeight() != 8 * Size) throw new Exception("Invalid sheet bounds");
                    for (int Dir = 0; Dir < 8; Dir++)
                    {
                        var Hashes = new System.Collections.Generic.HashSet<string>();
                        for (int Frame = 0; Frame < Count; Frame++)
                        {
                            using var Cell = Im.GetRegion(new Rect2I((First + Frame) * Size, Dir * Size, Size, Size));
                            if (Cell.IsInvisible()) throw new Exception("Empty harvesting frame");
                            Hashes.Add(Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(Cell.GetData())));
                        }
                        if (Hashes.Count < 4) throw new Exception("Harvesting pose is static");
                    }
                }
            }
            SetPhysicsProcess(false);
            foreach (Node Child in GetChildren())
            {
                if (Child is CanvasItem Item) Item.Visible = false;
                if (Child is CanvasLayer Layer) Layer.Visible = false;
            }
            var Camera = (Camera2D)Gd.Unwrap(Cam);
            Camera.Enabled = false;
            MyId = -999;
            foreach (string Arg in OS.GetCmdlineUserArgs()) if (Arg.StartsWith("--shot=")) Shot = Arg[7..];
            if (DisplayServer.GetName() == "headless")
            {
                GD.Print("HARVEST ANIM ALL OK: resource selection/cache and 432 sprite cells (run without --headless to exercise drawing)");
                GetTree().Quit();
            }
        }
        catch (Exception E) { GD.PushError(E.ToString()); GetTree().Quit(1); }
    }

    public override void _Process(double Delta)
    {
        Clock = Pass * 0.09f;
        QueueRedraw();
    }

    public override async void _Draw()
    {
        try
        {
            DrawRect(GetViewportRect(), new Color("252B34"));
            var Font = ThemeDB.FallbackFont;
            string[] Skins = { "Scout", "Healer", "Engineer" };
            for (int Role = 0; Role < 3; Role++)
                for (int Type = 0; Type < 4; Type++)
                {
                    int Key = 100 + Role * 4 + Type;
                    var Foot = new Vector2(125 + Type * 270, 140 + Role * 175);
                    var Target = new Vector2(900 + Type * 100, 900);
                    var P = (GArray)Gd.Duplicate(Gd.At(Snap.Ps, 1));
                    float Angle = (Pass / 12 % 8) * Mathf.Pi / 4;
                    P[1] = Role; P[2] = Target - Vector2.FromAngle(Angle) * 35;
                    P[5] = 0; P[11] = false; P[22] = "gather"; P[23] = Target;
                    DrawPlayer(Key, P, Foot);
                    DrawString(Font, Foot + new Vector2(-85, 32), $"{Skins[Role]} / {GameSim.RES[Type]}", fontSize: 16);
                    if (Gd.Str(Gd.At(Gd.At(AnimState, Key), 0)) != Expected[Type]) throw new Exception("Renderer did not choose harvesting sprite");
                }
            Pass++;
            if (Pass == 96)
            {
                GD.Print("HARVEST ANIM ALL OK: 3 characters, 4 resources, 8 directions, looping renderer and 432 sprite cells");
                if (Shot != "")
                {
                    await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                    GetViewport().GetTexture().GetImage().SavePng(Shot);
                }
                GetTree().Quit();
            }
        }
        catch (Exception E) { GD.PushError(E.ToString()); GetTree().Quit(1); }
    }
}
