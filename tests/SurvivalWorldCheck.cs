using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
using HoldTilDawn;

public partial class SurvivalWorldCheck : Node
{
    int Checks;
    void Check(bool Pass, string Message) { if (!Pass) throw new Exception(Message); Checks++; GD.Print("ok   " + Message); }
    public override void _Ready()
    {
        try { Run(); GD.Print($"SURVIVAL WORLD ALL OK: {Checks} checks"); GetTree().Quit(); }
        catch (Exception Error) { GD.PushError(Error.ToString()); GetTree().Quit(1); }
    }
    void Run()
    {
        using var S = new GameSim(); S.AddPlayer(1, "Engineer", 2); S.AddPlayer(2, "Scout", 0); S.StartMatch();
        var W = (WorldGeometry)S.World;
        Check(IslandMap.ById(8).Pos.Y < -30 * 32 && W.SwampCentre.Y < 0, "swamp marker, pools and scene moved north");
        Check(W.HarvestSites.Count > 850, $"denser harvestable world: {W.HarvestSites.Count} sites");
        Check(W.Crossings.Count >= 3 && W.Ramps.Count >= 3, $"exploration has {W.Crossings.Count} bridges and {W.Ramps.Count} slopes");
        foreach (var C in W.Crossings)
        {
            Check(W.CrossingTerrain(C.At) == 5 && W.ResolveFast(C.At, 10).IsEqualApprox(C.At), "bridge deck is walkable");
            Vector2 Bank = C.At + C.Along.Orthogonal() * 4 * 32;
            Check(W.CrossingTerrain(Bank) == 1 && !W.ResolveFast(Bank, 10).IsEqualApprox(Bank), "water beside bridge blocks bodies");
        }
        foreach (string Name in new[] { "lm_tent_0", "lm_console_0", "lm_cart_0", "lm_warning_0", "lm_cargo_stack_0", "lm_waterpump_0" })
        {
            Check(W.LmProps.Any(P => P.Sprite == Name) && GD.Load<Texture2D>($"res://art/landmarks/{Name}.png") != null, Name + " is shipped and placed");
            Check(!W.HarvestSites.Any(P => P.Sprite == Name), Name + " cannot be farmed");
        }
        Check(Main.LowHealthStrength(90, 100) == 0 && Main.LowHealthStrength(10, 100) > Main.LowHealthStrength(35, 100), "red border increases as health falls");
        Check(GameSim.ROAM_CHECK < 15 && GameSim.ROAM_MAX > 8 && GameSim.RoamChance(new Vector2(0, 2200)) > .3f, "daytime encounters are more frequent and bounded");
        var Seen = new HashSet<int>();
        for (int Seed = 0; Seed < 64; Seed++)
        {
            S.Rng.Seed = (ulong)Seed; S.PlaceSupplies();
            var ThisRun = new HashSet<int>();
            foreach (GArray Sup in S.Supplies)
            {
                int Id = Gd.Int(Sup[2]); Seen.Add(Id); ThisRun.Add(Id);
                Vector2 At = (Vector2)Sup[1];
                if (Id == 1 || !IslandMap.OnLand(At, 32) || !W.ResolveFast(At, 10).IsEqualApprox(At)) throw new Exception("Component inaccessible");
            }
            if (ThisRun.Count != 3) throw new Exception("Component sites must be different");
        }
        Check(Seen.Count == 17, "random components can use every one of the 17 landmarks");
        dynamic P = Gd.At(S.Players, 1), Q = Gd.At(S.Players, 2);
        S.Phase = "night"; P.Pos = ((GArray)S.Supplies[0])[1]; S.TickSupplies();
        Check(S.SuppliesGot() == 0 && S.SupplySnapshot().Count == 0, "no collecting or objective markers at night");
        S.Phase = "day";
        foreach (GArray Sup in S.Supplies) { P.Pos = Sup[1]; S.TickSupplies(); }
        Check(S.SuppliesGot() == 3 && !S.SuppliesAllIn(), "finding components alone does not unlock extraction");
        S.Dawn(); Check(S.SuppliesGot() == 3 && S.RescueComponents.All(N => N == 1), "components persist through dawn");
        P.Pos = GameSim.WORKBENCH_POS + new Vector2(0, 24);
        foreach (string Id in new[] { "wood", "stone", "herb" }) S.SetCount(P, Id, 30);
        S.Phase = "night"; P.Inside = true;
        Check(!S.CanCraft(P, 19, false), "Engineer cannot craft rescue equipment at night");
        S.Phase = "day"; P.Inside = false;
        for (int I = 19; I < 22; I++)
        {
            Check(S.CanCraft(P, I, false), "rescue recipe available from day one with its component");
            S.Craft(P, I); S.TickCraft(P, 2.9f);
            Check(!S.RescueCrafted[I - 19], "rescue equipment requires its full crafting time");
            S.TickCraft(P, .2f);
            Check(S.RescueCrafted[I - 19] && S.RescueComponents[I - 19] == 0 && !S.CanCraft(P, I, false), "team installs equipment once and spends component once");
        }
        Check(S.SuppliesAllIn(), "all three crafted items unlock rescue");
        S.K = 5; S.Phase = "night"; S.TimeLeft = 0f; S.Enemies = new GArray(); S.CheckEnd(.05f);
        Check(S.Phase == "result" && Gd.Truth(Gd.At(S.Result, 0)), "complete equipment and a clear pad allow helicopter rescue");
        S.StartMatch(); S.K = 5; S.Phase = "night"; S.TimeLeft = 0f; S.CheckEnd(.05f);
        Check(S.Phase == "result" && !Gd.Truth(Gd.At(S.Result, 0)) && Gd.Str(Gd.At(S.Result, 1)).Contains("equipment"), "missing equipment prevents rescue even with clear pads");
        S.ToLobby(); S.StartMatch(); P = Gd.At(S.Players, 1); Q = Gd.At(S.Players, 2);
        // Isolate construction geometry from resource collisions.
        S.Nodes = new GArray(); W.Rebuild(S.Nodes, S.Towers); P.Pos = S.FreeTowerSpot(0); Q.Pos = Vector2.Zero;
        S.BuildTower(P); Check(S.Construction.Count == 1 && Gd.At(S.Towers, 0) == null && Gd.Truth(S.FreeTower), "tower starts as a reserved dirt foundation");
        S.TickTowerConstruction(P, 5.9f); Check(Gd.At(S.Towers, 0) == null, "tower cannot fire before six seconds");
        S.TickTowerConstruction(P, .2f); Check(Gd.At(S.Towers, 0) != null && !Gd.Truth(S.FreeTower), "six seconds completes the free tower");
        S.Unlock = 2; P.Pos = S.FreeTowerSpot(2); S.SetCount(P, "wood", 20); S.SetCount(P, "titanium", 10);
        S.BuildTower(P); P.Pos += new Vector2(10, 0); S.TickTowerConstruction(P, 1);
        Check(S.Construction.Count == 0 && S.Count(P, "wood") == 20, "movement cancels construction without spending materials");
        P.Pos = S.FreeTowerSpot(2); S.BuildTower(P); P.Taken += 1; S.TickTowerConstruction(P, 1);
        Check(S.Construction.Count == 0 && S.Count(P, "titanium") == 10, "damage cancels construction without spending titanium");
        S.BuildTower(P); S.TickTowerConstruction(P, 6.1f);
        Check(Gd.At(S.Towers, 1) != null && S.Count(P, "wood") == 10 && S.Count(P, "titanium") == 5, "paid tower spends materials at completion");
        int Before = S.EnemyBodySteps;
        for (int I = 0; I < 60; I++) S.TickEnemySimulation(1f / 60);
        Check(S.EnemyBodySteps - Before == 20, "60Hz simulation runs expensive body/AI work only 20 times per second");
    }
}
