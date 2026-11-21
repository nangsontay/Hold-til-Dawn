using Godot;
using System;
using HoldTilDawn;

// Review the shipped sheets through the game's renderer, at the game's actual drawing scales.
// --headless validates all cells; otherwise saves idle/attack/corpse screenshots and exercises 8 facings.
public partial class EnemyDesignPreview : Main
{
    int ReviewFrame;
    bool Finished;
    readonly string[] Names = { "grunt", "spitter", "wrecker", "boss_juggernaut", "boss_broodmother",
        "boss_bio_cannon", "boss_burrower", "boss_gatebreaker", "boss_phantom", "boss_ironhide" };

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
            Sim = new GameSim(); Sim.AddPlayer(1, "Scale reference", 0); Sim.StartMatch();
            MyId = -1; OnSnap(Gd.FromVariant(Sim.Snapshot()));
            int Checked = 0;
            foreach (string Name in Names)
            {
                dynamic Info = Gd.At(Cinfo.Chars, Name);
                int Cell = Gd.Int(Info.Size);
                using var Sheet = GD.Load<Texture2D>($"res://art/chars/{Name}.png").GetImage();
                if (Sheet.GetWidth() != Cell * 14 || Sheet.GetHeight() != Cell * 8)
                    throw new Exception($"Unexpected sheet layout: {Name}");
                for (int Row = 0; Row < 8; Row++)
                    for (int Col = 0; Col < 14; Col++)
                    {
                        using var Pose = Sheet.GetRegion(new Rect2I(Col * Cell, Row * Cell, Cell, Cell));
                        if (Pose.IsInvisible()) throw new Exception($"Missing pose: {Name}/{Row}/{Col}");
                        Checked++;
                    }
            }
            if (Mathf.Abs(BOSS_DRAW_SCALE / 2f - 1.5f) > 0.001f) throw new Exception("Incorrect boss scale");
            GD.Print($"ENEMY DESIGN ASSETS OK: {Checked} cells, 10 sheets, boss scale 1.5 times previous");
            DirAccess.MakeDirRecursiveAbsolute("res://.tmp/enemy-redesign/rendered");
            if (DisplayServer.GetName() == "headless") { Finished = true; GetTree().Quit(); }
        }
        catch (Exception E) { Finished = true; GD.PushError(E.ToString()); GetTree().Quit(1); }
    }

    public override void _PhysicsProcess(double Delta) { }
    public override void _Process(double Delta) { if (!Finished) { Clock = ReviewFrame * 0.05f; QueueRedraw(); } }

    public override async void _Draw()
    {
        if (Finished) return;
        try
        {
            DrawRect(GetViewportRect(), new Color("252D35"));
            var Font = ThemeDB.FallbackFont;
            DrawString(Font, new Vector2(24, 28), "ENEMY REDESIGN / actual in-game size", fontSize: 20);
            DrawString(Font, new Vector2(650, 65), "Scout / size reference", fontSize: 13);
            Char("reference", "scout", new Vector2(715, 110), Mathf.Pi / 2, 1f, Colors.White);
            var Shown = new GDict();
            for (int I = 0; I < Names.Length; I++)
            {
                bool Boss = I >= 3;
                int Index = I - 3;
                var Foot = Boss ? new Vector2(120 + Index % 4 * 230, 300 + Index / 4 * 200)
                    : new Vector2(100 + I * 200, 110);
                string Key = $"e{100 + I}";
                float Angle = (ReviewFrame / 12 % 8) * Mathf.Pi / 4;
                // Supply a stable foot and facing just as the interpolation cache does in a match.
                Cmove[Key] = new GArray(Foot, ReviewFrame < 96 ? -1f : (float)Clock, Angle, -1);
                if (ReviewFrame < 192)
                {
                    var Enemy = new GArray(100 + I, Boss ? GameSim.BOSS : I, Foot, 100f, 100f,
                        Boss ? 28.8f : I == 2 ? 20f : 16f, Boss ? Index : -1,
                        ReviewFrame >= 96 && ReviewFrame % 12 < 4 ? "glow" : "", Angle, ReviewFrame >= 96 && ReviewFrame % 12 < 4);
                    DrawEnemy(Enemy, Foot); Shown[100 + I] = true;
                }
                DrawString(Font, Foot + new Vector2(-55, 20), Names[I].Replace("boss_", ""), fontSize: 13);
            }
            NoteFallen(Shown);
            var Corpses = new GArray(); CorpseItems(Corpses);
            foreach (dynamic Item in Gd.Iter(Corpses)) DrawCorpse(Gd.Int(Gd.At(Item, 2)));
            int CapturedFrame = ReviewFrame++;
            if (CapturedFrame == 12 || CapturedFrame == 99 || CapturedFrame == 210)
            {
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                using var Shot = GetViewport().GetTexture().GetImage();
                Shot.Convert(Image.Format.Rgba8); Shot.LinearToSrgb();
                Shot.SavePng($"res://.tmp/enemy-redesign/rendered/frame-{CapturedFrame:000}.png");
            }
            if (ReviewFrame >= 216)
            {
                Finished = true;
                GD.Print("ENEMY DESIGN RENDER OK: idle, walk, attacks, wind-ups and corpses across eight directions");
                GetTree().Quit();
            }
        }
        catch (Exception E) { Finished = true; GD.PushError(E.ToString()); GetTree().Quit(1); }
    }
}
