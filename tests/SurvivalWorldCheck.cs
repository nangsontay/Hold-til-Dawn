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
        Check(W.HarvestSites.Count > 650 && W.OutsideAfter.Sum() < W.OutsideBefore.Sum(), $"concentrated home farms and sparser outer world: {W.HarvestSites.Count} sites");
        Check(IslandMap.Rivers.Count == 2 && IslandMap.Bridges.Count == 5 && W.Ramps.Count >= 3, $"two rivers with {IslandMap.Bridges.Count} bridges and {W.Ramps.Count} slopes");
        for (int R = 0; R < 2; R++)
        {
            var Path = IslandMap.Rivers[R];
            Check(IslandMap.Bridges.Count(B => B.River == R) == (R == 0 ? 3 : 2), $"river {R} has its exploration bridges");
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
        Check(!W.LmAt.Any(L => IslandMap.InRiver(L.Key == 16 ? W.CaveCentre : L.Value, 6 * 32)), "no landmark structure stands in or by a river");
        Check(!IslandMap.InRiver(W.CaveEntrance, IslandMap.RIVER_BANK + 10), "the eastern cave mouth stays on dry land beside the bridge approach");
        var WithoutBridges = ReachableLand(false);
        var WithBridges = ReachableLand(true);
        foreach (int Id in new[] { 5, 9 })
        {
            Vector2I Cell = (Vector2I)(W.LmAt[Id] / 32).Round();
            Check(!WithoutBridges.Contains(Cell) && WithBridges.Contains(Cell), IslandMap.ById(Id).Name + " is across the river and reachable by bridge");
        }
        Check(WithBridges.Contains((Vector2I)(W.SwampCentre / 32).Round()) && !IslandMap.InRiver(W.SwampCentre, W.SwampR), "relocated swamp leaves the expedition crossing clear");
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
        Check(S.ExplorationChests.Count == 20 && S.ExplorationChests.Count(C => C.Landmark > 4) == 14, "20 chests, 14 at expedition landmarks and six scattered");
        dynamic P = Gd.At(S.Players, 1), Q = Gd.At(S.Players, 2);
        foreach (var C in S.ExplorationChests.Where(C => C.Component >= 0).ToList())
        {
            P.Pos = C.Pos; P.Inter = true; S.SetCount(P,"chest_key",1); S.Interact(P,2f);
            int Slot = Enumerable.Range(0,8).First(I => C.Slots[I] is GArray A && Array.IndexOf(GameSim.COMPONENT_IDS,Gd.Str(A[0])) >= 0);
            S.MoveSlot(P,new GArray("chest:"+C.Id,Slot,"inv",0));
        }
        Check(S.SuppliesGot() == 3 && !S.SuppliesAllIn(), "finding parts requires assembling the radio");
        S.Dawn(); Check(S.SuppliesGot() == 3, "radio parts persist through dawn");
        P.Pos = GameSim.WORKBENCH_POS + new Vector2(0,24);
        S.TryBuildRadio(P, 3.1f);
        Check(S.RadioBuilt, "radio is assembled at the workbench");
        S.K = 5; S.Night(); S.TimeLeft = 0f; S.Enemies = new GArray(); S.CheckEnd(.05f);
        Check(S.Phase == "result" && Gd.Truth(Gd.At(S.Result,0)), "radio and clear pad allow night 5 rescue");
        S.StartMatch(); S.K = 5; S.Night();
        Check(S.Phase == "result" && !Gd.Truth(Gd.At(S.Result,0)), "missing radio at the start of night 5 prevents rescue");
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

    // Flood-fill land with and without decks: moving a map dot alone must not satisfy the crossing requirement.
    static HashSet<Vector2I> ReachableLand(bool Bridges)
    {
        var Seen = new HashSet<Vector2I> { Vector2I.Zero };
        var Queue = new Queue<Vector2I>(); Queue.Enqueue(Vector2I.Zero);
        Vector2I[] Dirs = { Vector2I.Left, Vector2I.Right, Vector2I.Up, Vector2I.Down };
        while (Queue.Count > 0)
        {
            Vector2I At = Queue.Dequeue();
            foreach (var Dir in Dirs)
            {
                Vector2I Next = At + Dir; Vector2 P = (Vector2)Next * 32;
                if (Seen.Contains(Next) || !IslandMap.OnLand(P, 10) || (!Bridges && IslandMap.InRiver(P, 10))) continue;
                Seen.Add(Next); Queue.Enqueue(Next);
            }
        }
        return Seen;
    }
}
