using Godot;
using System;
using HoldTilDawn;

// Real craft panel and world effects at the normal logical viewport size.
public partial class TacticalCraftPreview : Main
{
    int Stage, WarmFrames;
    bool Capturing, Finished;
    readonly int[] Selected = { 17, 2, 6 };
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
            Material = new CanvasItemMaterial { LightMode = CanvasItemMaterial.LightModeEnum.Unshaded };
            ((CanvasModulate)Gd.Unwrap(Tint)).Color = Colors.White;
            var ReviewHud = (Control)Gd.Unwrap(Hud);
            var UiRoot = (Control)ReviewHud.GetParent();
            foreach (Node Child in UiRoot.GetChildren()) if (Child is CanvasItem Item) Item.Visible = Child == ReviewHud;
            UiRoot.Visible = true; ReviewHud.Visible = true;
            ((CanvasLayer)UiRoot.GetParent()).Visible = true;
            Sim = new GameSim(); Sim.AddPlayer(1, "Crafter", 2); Sim.StartMatch(); Sim.Unlock = 2;
            dynamic P = Gd.At(Sim.Players, 1); P.Pos = GameSim.WORKBENCH_POS + new Vector2(0, 24);
            foreach (string Id in new[] { "wood", "stone", "herb", "titanium" }) Sim.SetCount(P, Id, 30);
            MyId = 1; LocalPos = P.Pos; Snap = Gd.FromVariant(Sim.Snapshot()); CraftOpen = true;
            for (int T = 0; T < 3; T++)
            {
                SelectCraftTier(T);
                var Cards = CraftChoices();
                foreach (int I in Cards)
                {
                    Rect2 A = CraftCard(new Vector2(960, 540), I);
                    if (A.End.X > CraftDetailRightEdge()) throw new Exception("Card exceeds recipe column");
                    foreach (int J in Cards) if (I != J && A.Intersects(CraftCard(new Vector2(960, 540), J))) throw new Exception("Overlapping recipe cards");
                }
            }
            DirAccess.MakeDirRecursiveAbsolute("res://.tmp/tactical-craft");
            GD.Print("TACTICAL CRAFT LAYOUT OK: 18 equipment and maintenance cards in 3 tabs");
            if (DisplayServer.GetName() == "headless") { Finished = true; GetTree().Quit(); }
        }
        catch (Exception E) { GD.PushError(E.ToString()); Finished = true; GetTree().Quit(1); }
    }
    float CraftDetailRightEdge() => CraftPanelRect(new Vector2(960, 540)).Position.X + 20 + CraftPanelRect(new Vector2(960, 540)).Size.X * 0.54f;
    public override void _PhysicsProcess(double Delta) { }
    public override void _Process(double Delta)
    {
        if (Finished || Capturing) return;
        var ReviewHud = (Control)Gd.Unwrap(Hud);
        ReviewHud.Visible = Stage < 3;
        if (Stage < 3) { SelectCraftTier(Stage); CraftSel = Selected[Stage]; ReviewHud.QueueRedraw(); }
        QueueRedraw();
    }
    public override async void _Draw()
    {
        if (Finished || Capturing) return;
        try
        {
            DrawRect(new Rect2(0, 0, 960, 540), new Color("293631"));
            Clock = 1.5f;
            if (Stage < 3)
            {
                // DrawHud's real Control / CanvasLayer draws the panel, as it does during play.
            }
            else
            {
                Snap.Tactical = new GArray(new GArray("poison", new Vector2(240, 270), 3f), new GArray("heal", new Vector2(480, 270), 3f),
                    new GArray("trap", new Vector2(680, 270), 100f), new GArray("blast", new Vector2(800, 270), 0.25f));
                DrawTacticalAreas();
                DrawString(ThemeDB.FallbackFont, new Vector2(36, 40), "EXPEDITION LOOT / support gear and world effects", fontSize: 20);
                for (int I = 0; I < GameSim.CHEST_SUPPORT.Length; I++)
                {
                    string Id = GameSim.CHEST_SUPPORT[I];
                    Vector2 Pos = new(40 + I * 84, 440);
                    DrawItemIcon(this, Id, Pos, 3f);
                    DrawString(ThemeDB.FallbackFont, Pos + new Vector2(-42, 52), Id.Replace('_', ' '), fontSize: 10);
                }
                dynamic P = Gd.At(Snap.Ps, 1);
                P[8] = "long_spear"; P[11] = true; P[12] = 0f;
                DrawPlayer(2, P, new Vector2(140, 150));
                P[8] = "stun_maul"; DrawPlayer(3, P, new Vector2(380, 150));
            }
            if (++WarmFrames < 12) return;
            Capturing = true;
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using var Shot = GetViewport().GetTexture().GetImage(); Shot.SavePng($"res://.tmp/tactical-craft/preview-{Stage}.png");
            Stage++; WarmFrames = 0; Capturing = false;
            if (Stage == 4) { GD.Print("TACTICAL CRAFT RENDER OK"); Finished = true; GetTree().Quit(); }
        }
        catch (Exception E) { GD.PushError(E.ToString()); Finished = true; GetTree().Quit(1); }
    }
}
