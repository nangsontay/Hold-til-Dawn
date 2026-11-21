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
        Check(IslandMap.Rivers.Count == 2 && IslandMap.Bridges.Count == 4 && W.Ramps.Count >= 3, $"two rivers with {IslandMap.Bridges.Count} bridges and {W.Ramps.Count} slopes");
        for (int R = 0; R < 2; R++)
        {
            var Path = IslandMap.Rivers[R];
            Check(IslandMap.Bridges.Count(B => B.River == R) == 2, $"river {R} has two bridges");
            Check(IslandMap.Depth(Path[0]) < 0 && IslandMap.Depth(Path[^1]) < 0 && Path[0].Y < -60 * 32 && Path[^1].Y > 60 * 32, $"river {R} runs from the north coast to the south coast");
            Check(R == 0 ? Path.All(P => P.X < -25 * 32) : Path.All(P => P.X > 25 * 32), $"river {R} keeps to its side of the island");
        }
        foreach (var (_, At) in IslandMap.Bridges)
        {
            Check(IslandMap.OnLand(At, 35) && W.ResolveFast(At, 10).IsEqualApprox(At) && W.TerrainAt(At) == 5, "bridge deck is walkable, even for a boss");
            Vector2 Beside = At + new Vector2(0, IslandMap.BRIDGE_HALF_W + 1.5f * 32);
            Check(!IslandMap.OnLand(Beside, 10) && !IslandMap.OnLand(Beside), "water beside a bridge stops bodies");
            Vector2 Far = At + new Vector2(IslandMap.BRIDGE_HALF_LEN + 32, 0), Near = At - new Vector2(IslandMap.BRIDGE_HALF_LEN + 32, 0);
            Check(IslandMap.OnLand(Far, 12) && IslandMap.OnLand(Near, 12), "both ends of a bridge reach dry land");
            var Way = W.WaypointFast(Near, Far, 0.5f * 32, new[] { new Rect2(-(Vector2)GameSim.HALF, (Vector2)GameSim.HALF * 2) });
            Check(Way.DistanceTo(Near) > 1 && Mathf.Abs(Way.Y - At.Y) < IslandMap.BRIDGE_HALF_W, "enemies route straight over a bridge");
        }
        Check(!W.LmAt.Values.Any(P => IslandMap.InRiver(P, 6 * 32)), "no landmark stands in or by a river");
        Check(!W.LmProps.Any(P => IslandMap.InRiver(P.Pos)) && !W.HarvestSites.Any(S => IslandMap.InRiver(S.Pos, 8)), "no prop, tree or rock stands in a river");
        int Wet = 0;
        foreach (var (Path, Width) in W.Trails)
            foreach (var Pt in Path)
                if (IslandMap.InRiver(Pt) && !IslandMap.NearBridge(Pt)) { Wet++; GD.Print("wet path point ", Pt / 32, " width ", Width / 32, " of ", Path.Length); }
        Check(Wet == 0, "paths cross a river only on a bridge");
        // a body cannot swim across: walking straight at the river it stops at the bank
        using (var Walker = new GameSim())
        {
            Walker.AddPlayer(1, "Scout", 0); Walker.StartMatch();
            dynamic Wp = Walker.Players[1];
            var Path = IslandMap.Rivers[0];
            Vector2 Mid = Path[Path.Length / 3];
            Wp.Pos = Mid + new Vector2(IslandMap.RIVER_HALF + 3 * 32, 0);
            for (int I = 0; I < 120; I++) Wp.Pos = Walker.MoveActor(Wp.Pos, Wp.Pos + new Vector2(-6, 0), 0.3f * 32);
            Check(((Vector2)Wp.Pos).X > Mid.X + IslandMap.RIVER_HALF - 4, "players cannot walk through a river");
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
