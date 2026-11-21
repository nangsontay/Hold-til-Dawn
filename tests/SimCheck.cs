using Godot;
using Environment = Godot.Environment;
using FileAccess = Godot.FileAccess;
using System;
using System.Threading.Tasks;
using HoldTilDawn;

public partial class SimCheck : Node
{
    // Headless check of the server rules: godot --headless --path . tests/SimCheck.tscn
    public dynamic Fails = 0;
    public void Check(dynamic Ok, dynamic What)
    {
        GD.Print(((Gd.Truth(Ok) ? (dynamic)("ok   ") : "FAIL ") + What));
        if (!(Gd.Truth(Ok)))
        {
            Fails += 1;
        }
    }

    public void Run(dynamic S, dynamic Seconds, dynamic UntilPhase = null)
    {
        UntilPhase ??= "";
        foreach (dynamic I in Gd.Iter(Gd.Int((Seconds * 60))))
        {
            S.Tick((1.0f / 60));
            if (((UntilPhase != "") && (S.Phase == UntilPhase)))
            {
                return;
            }
        }
    }

    public dynamic NewMatch()
    {
        dynamic S = new GameSim();
        S.AddPlayer(1, "Eng", 2);
        S.AddPlayer(2, "Scout", 0);
        S.Action(1, "start", 0);
        return S;
    }

    public override void _Ready()
    {
        try { RunChecks(); }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }

    private void RunChecks()
    {
        var arguments = OS.GetCmdlineUserArgs();
        if (!arguments.Contains("--mechanics-only")) CheckProgression();
        if (!arguments.Contains("--progression-only")) CheckMechanics();
        if (!arguments.Contains("--progression-only")) CheckBots();
        if (!arguments.Contains("--progression-only")) CheckItems();
        if (!arguments.Contains("--progression-only")) CheckBosses();
        GD.Print((Gd.Truth(Fails) ? (dynamic)(Gd.Format("FAILED %d", Fails)) : "ALL OK"));
        GetTree().Quit((Gd.Truth(Fails) ? (dynamic)(1) : 0));
    }


    private void CheckProgression()
    {
        dynamic S = this.NewMatch();
        this.Check(((S.Phase == "day") && (Gd.Size(S.Active) == 1)), "match starts on day 1 with one attacked door");
        this.Check((Gd.Size(Gd.Filter(S.Nodes, (Func<dynamic, dynamic>)((N) => (N.Type == 0) && !Gd.Truth(Gd.Get(N, "ambient", false))))) == ((4 + (3 * 2)) * GameSim.NODE_MUL)), "4 + 3n primary nodes per type, plus harvestable world scenery");
        dynamic Eng = Gd.At(S.Players, 1);
        dynamic Wood = Gd.At(Gd.Filter(S.Nodes, (Func<dynamic, dynamic>)((N) => (N.Type == 0))), 0);
        Eng.Pos = Wood.Pos;
        Eng.Inter = true;
        this.Run(S, 15);
        this.Check(((S.Count(Eng, "wood") == 5) && (Wood.Amount == 0)), "gathering at 0.35/s empties a 5-unit node in ~15 s");
        Eng.Inter = false;
        dynamic Stone = Gd.At(Gd.Filter(S.Nodes, (Func<dynamic, dynamic>)((N) => (N.Type == 1))), 0);
        Eng.Pos = Stone.Pos;
        Eng.Inter = true;
        foreach (dynamic I in Gd.Iter((60 * 6)))
        {
            Eng.Pos = (Stone.Pos + Gd.MakeVector2((20.0f * Gd.Sin((I * 0.2f))), 0));
            S.Tick((1.0f / 60));
        }
        this.Check(((S.Count(Eng, "stone") == 0) && (Stone.Amount == 5)), "moving while holding E gathers nothing (MEC-08)");
        Eng.Pos = Stone.Pos;
        this.Run(S, 1.5f);
        this.Check(((Eng.GatherAcc > 0.4f) && (Eng.GatherAcc < 0.6f) && (Gd.At(Gd.At(S.Snapshot().Ps, 1), 21) == Eng.GatherAcc)), "gather progress is in the snapshot");
        Eng.Inter = false;
        this.Run(S, 0.1f);
        this.Check((Eng.GatherAcc == 0.0f), "releasing E resets gather progress");
        Eng.Pos = (GameSim.WORKBENCH_POS + Gd.MakeVector2(0, 24));
        S.SetCount(Eng, "wood", 6);
        S.Action(1, "craft", 0);
        this.Run(S, 2.9f);
        this.Check((S.Count(Eng, "light_armour") == 0), "crafting takes 3 s (BAL-19)");
        this.Run(S, 0.2f);
        this.Check(((Eng.Armour == 1) && (S.Count(Eng, "wood") == 0)), "light armour crafted at workbench");
        S.Action(1, "craft", 4);
        this.Run(S, 3.1f);
        this.Check((S.Count(Eng, "stone_spear") == 0), "stone spear locked before night 1");
        S.TimeLeft = 0;
        this.Run(S, 0.1f);
        this.Check((S.Phase == "dusk"), "day -> dusk");
        this.Run(S, 16, "night");
        this.Check(((S.Phase == "night") && !(Gd.Truth(GameSim.InsideBase(Eng.Pos)))), "night starts, players moved outside");
        // An undefended base loses: isolate door/generator rules from the free automatic tower,
        // which can now hold the deliberately sparse first-night waves on its own.
        S.Towers = new GArray((object)null, (object)null, (object)null);
        foreach (dynamic P in Gd.Iter(Gd.Values(S.Players)))
        {
            P.Pos = Gd.MakeVector2(0, (40 * 32));
        }
        this.Run(S, 601, "result");
        this.Check(((S.Phase == "result") && Gd.Contains(S.Msg, "Generator")), ("idle team loses night 1: " + S.Msg));
        // Progression path (debug skip), with an unkillable base so the boss timer can be tested.
        S = this.NewMatch();
        S.BaseHp = 1000000000.0f;
        S.Action(1, "skip", 0);
        this.Run(S, 0.1f);
        this.Run(S, 16, "night");
        S.Action(1, "skip", 0);
        this.Run(S, 0.1f);
        this.Check(((S.K == 2) && (S.Phase == "day") && (S.Unlock == 1)), "night 1 cleared, stone tier unlocked");
        S.Action(1, "skip", 0);
        this.Run(S, 0.1f);
        this.Run(S, 16, "night");
        foreach (dynamic P in Gd.Iter(Gd.Values(S.Players)))
        {
            P.Pos = Gd.MakeVector2(0, (40 * 32));
        }
        this.Run(S, 305);
        dynamic Boss = S.Boss();
        this.Check((Boss != null), "boss spawned on night 2");
        if (Gd.Truth(Boss))
        {
            Boss.Hp = 1;
            // up above ground and not about to dig in (a burrowed Burrower cannot be hit)
            S.BossAct(Boss, "", 0.0f);
            Boss.SigCd = 99.0f;
            dynamic Sc = Gd.At(S.Players, 2);
            Sc.Pos = (Boss.Pos - Gd.MakeVector2(32, 0));
            Sc.Aim = 0.0f;
            Sc.Atk = true;
            this.Run(S, 0.2f);
            Sc.Atk = false;
            this.Check(((S.K == 3) && (S.Unlock == 2)), "killing the boss ends night 2 and unlocks titanium tier");
        }
    }

    private void CheckMechanics()
    {
        dynamic S = null;
        // Engineer night entry (MEC-15)
        S = this.NewMatch();
        S.Action(1, "skip", 0);
        this.Run(S, 0.1f);
        this.Run(S, 16, "night");
        // isolate door entry and repair from the faster night waves
        S.NextWave = float.PositiveInfinity;
        dynamic E1 = Gd.At(S.Players, 1);
        dynamic Sc2 = Gd.At(S.Players, 2);
        E1.Pos = (Gd.At(GameSim.DOOR_POS, 0) + (Gd.At(GameSim.DOOR_DIR, 0) * 32));
        E1.Ent = true;
        this.Run(S, 2.1f);
        E1.Ent = false;
        this.Check((Gd.Truth(E1.Inside) && Gd.Truth(GameSim.InsideBase(E1.Pos))), "Engineer holds F 2 s at a closed door and gets inside");
        // walks over to the workbench by the north wall
        E1.Pos = (GameSim.WORKBENCH_POS + Gd.MakeVector2(0, 24));
        S.SetCount(E1, "herb", 2);
        S.Action(1, "craft", 1);
        this.Run(S, 3.1f);
        this.Check((S.Count(E1, "bandage") == 1), "Engineer crafts at night inside");
        Sc2.Pos = (Gd.At(GameSim.DOOR_POS, 1) + (Gd.At(GameSim.DOOR_DIR, 1) * 32));
        Sc2.Ent = true;
        this.Run(S, 2.1f);
        this.Check((!(Gd.Truth(Sc2.Inside)) && !(Gd.Truth(GameSim.InsideBase(Sc2.Pos)))), "Scout cannot enter at night");
        // back to door A, just inside
        E1.Pos = (Gd.At(GameSim.DOOR_POS, 0) - ((Gd.At(GameSim.DOOR_DIR, 0) * 1.2f) * 32));
        E1.Ent = true;
        this.Run(S, 2.1f);
        E1.Ent = false;
        this.Check((!(Gd.Truth(E1.Inside)) && !(Gd.Truth(GameSim.InsideBase(E1.Pos)))), "Engineer leaves the same way");
        // Engineer repairs a door at night from outside (MEC-12, FML-08)
        E1.Pos = (Gd.At(GameSim.DOOR_POS, 0) + (Gd.At(GameSim.DOOR_DIR, 0) * 24));
        S.Doors[0] = 300.0f;
        S.DoorPause[0] = 0.0f;
        S.SetCount(E1, "wood", 0);
        E1.Inter = true;
        this.Run(S, 1.0f);
        this.Check((Gd.At(S.Doors, 0) == 300.0f), "no repair without wood");
        S.SetCount(E1, "wood", 10);
        this.Run(S, 2.0f);
        this.Check(((Gd.At(S.Doors, 0) > 328.0f) && (Gd.At(S.Doors, 0) < 332.0f) && (S.Count(E1, "wood") == 7)), Gd.Format("night repair: +15/s and 1 wood per 10 (%.1f, wood %d)", new GArray(Gd.At(S.Doors, 0), S.Count(E1, "wood"))));
        S.DoorPause[0] = 2.0f;
        float Before = Gd.Float(Gd.At(S.Doors, 0));
        this.Run(S, 1.0f);
        this.Check((Gd.At(S.Doors, 0) == Before), "a hit on the door pauses repair for 2 s");
        E1.Inter = false;
        this.Check((Gd.Truth(Gd.Has(S.Snapshot(), "dpause")) && Gd.Truth(Gd.Has(S.Snapshot(), "broken"))), "repair state is in the snapshot");
        // enemies from the far side reach their door around the walls
        dynamic Ep = Gd.MakeVector2(0, (30 * 32));
        Vector2 Goal = (Vector2)((Gd.At(GameSim.DOOR_POS, 0) + (Gd.At(GameSim.DOOR_DIR, 0) * 22)));
        foreach (dynamic I in Gd.Iter(2000))
        {
            Ep += ((GameSim.Route(Ep, Goal, 16.0f) - Ep).Normalized() * 3.0f);
            Ep = GameSim.PushOut(Ep, 16.0f);
            if ((Ep.DistanceTo(Goal) < 4))
            {
                break;
            }
        }
        this.Check((Ep.DistanceTo(Goal) < 4), "enemy spawned behind the base walks around to door A");
        // Daytime roamers (MEC-26, FML-16)
        // points straight south of the base: wall distance = y - HALF.y
        float Wall = Gd.Float(GameSim.HALF.Y);
        this.Check((GameSim.RoamChance(Gd.MakeVector2(0, (Wall + (19 * 32)))) == 0.0f), "no roamer chance inside 20 tiles");
        this.Check((Gd.Absf((GameSim.RoamChance(Gd.MakeVector2(0, (Wall + (40 * 32)))) - 0.15f)) < 0.001f), "roamer chance 15% at 40 tiles");
        this.Check((Gd.Absf((GameSim.RoamChance(Gd.MakeVector2(0, (Wall + (65 * 32)))) - 0.3f)) < 0.001f), "roamer chance capped at 30%");
        S = this.NewMatch();
        dynamic Far = Gd.At(S.Players, 2);
        dynamic Near = Gd.At(S.Players, 1);
        Far.Pos = Gd.MakeVector2(0, (Wall + (55 * 32)));
        Near.Pos = Gd.MakeVector2(0, (-(Wall + (10 * 32))));
        this.Run(S, 300);
        dynamic Roam = Gd.Filter(S.Enemies, (Func<dynamic, dynamic>)((E) => Gd.Has(E, "home")));
        this.Check(((Gd.Size(Roam) >= 2) && (Gd.Size(Roam) <= GameSim.ROAM_MAX)), Gd.Format("roamers met far from the base (%d alive)", Gd.Size(Roam)));
        this.Check(Gd.All(Roam, (Func<dynamic, dynamic>)((E) => (E.Pos.DistanceTo(Near.Pos) > (20 * 32)))), "no roamer near the player in the safe zone");
        this.Check(Gd.All(Roam, (Func<dynamic, dynamic>)((E) => ((E.State == 4) || (E.State == 1)))), "roamers wander or chase by day, never go to a door");
        this.Check((Far.State == 0), "a player standing still out of their sight is left alone");
        S.TimeLeft = 0;
        this.Run(S, 0.1f);
        this.Check(((S.Phase == "dusk") && Gd.Truth(Gd.All(Roam, (Func<dynamic, dynamic>)((E) => ((E.State == 0) && Gd.Contains(S.Active, E.Door) && !(Gd.Truth(Gd.Has(E, "home")))))))), "at dusk roamers head for an attacked door");
        // a roamer that spots a player chases and hurts them
        S = this.NewMatch();
        dynamic Vic = Gd.At(S.Players, 2);
        Vic.Pos = Gd.MakeVector2(0, (Wall + (50 * 32)));
        S.SpawnRoamers(Vic);
        dynamic R0 = Gd.Filter(S.Enemies, (Func<dynamic, dynamic>)((E) => Gd.Has(E, "home")));
        this.Check((Gd.Size(R0) == 2), "a roamer group is a pair");
        if ((Gd.Size(R0) > 0))
        {
            Gd.At(R0, 0).Pos = (Vic.Pos + Gd.MakeVector2((3 * 32), 0));
            this.Run(S, 3);
            this.Check((Vic.Hp < Vic.Maxhp), "roamer within 4 tiles chases and hits");
        }
        // team wiped by day is a loss
        foreach (dynamic P in Gd.Iter(Gd.Values(S.Players)))
        {
            P.State = 1;
            P.Bleed = 30.0f;
        }
        this.Run(S, 0.1f);
        this.Check(((S.Phase == "result") && Gd.Contains(S.Msg, "wiped")), "team wiped by day loses");
        // Drops (BAL-41, FML-15)
        S = this.NewMatch();
        foreach (dynamic I in Gd.Iter(10000))
        {
            S.Drop(new GDict { { "type", 0 }, { "pos", Vector2.Zero } });
        }
        // BAL-41: 10% x playtest multiplier
        float Want = Gd.Float((1000.0f * GameSim.DROP_MUL));
        this.Check((Gd.Absf((Gd.Size(S.Drops) - Want)) <= (0.1f * Want)), Gd.Format("Grunt drop rate %d%% +-10%% (%d / 10000)", new GArray((Want / 100), Gd.Size(S.Drops))));
        this.Check(Gd.All(S.Drops, (Func<dynamic, dynamic>)((D) => (Gd.Contains(new GArray("wood", "stone", "herb"), D.Item) && (D.N >= 1) && (D.N <= 2)))), "Grunt drops 1-2 wood/stone/herb");
        Gd.Clear(S.Drops);
        S.Drop(new GDict { { "type", GameSim.BOSS }, { "pos", Gd.MakeVector2(0, (30 * 32)) } });
        dynamic Pick = Gd.At(S.Players, 2);
        Pick.Pos = Gd.MakeVector2(0, (30 * 32));
        this.Run(S, 0.1f);
        this.Check((S.Count(Pick, "titanium") == 10), "boss drop collected by walking over it (+10 titanium)");
        Gd.Clear(S.Drops);
        S.AddDrop(Gd.MakeVector2(0, ((30 * 32) + 300)), "wood", 2);
        this.Run(S, 31);
        this.Check(Gd.IsEmpty(S.Drops), "uncollected drop vanishes after 30 s");
        S.AddDrop(Vector2.Zero, "wood", 2);
        Pick.Pos = Vector2.Zero;
        dynamic W0 = S.Count(Pick, "wood");
        this.Run(S, 0.5f);
        this.Check((S.Count(Pick, "wood") == W0), "drops cannot be collected from inside the base");
        // Walls: only doors let players in.
        Func<dynamic, dynamic, dynamic, dynamic> Walk = (Start, Step, Open) =>
        {
            dynamic Pos = Start;
            foreach (dynamic I in Gd.Iter(200))
            {
                Pos = GameSim.Collide((Pos + Step), 12.8f, Open);
            }
            return Pos;
        };
        this.Check((Walk(Gd.MakeVector2(0, (GameSim.HALF.Y + 40)), Gd.MakeVector2(0, (-2)), new GArray(true, true, true)).Y > GameSim.HALF.Y), "bottom wall blocks (no door)");
        this.Check((Walk(Gd.MakeVector2(0, ((-GameSim.HALF.Y) - 40)), Gd.MakeVector2(0, 2), new GArray(true, true, true)).Y > 0), "open door B (north) lets you in");
        this.Check((Walk(Gd.MakeVector2(0, ((-GameSim.HALF.Y) - 40)), Gd.MakeVector2(0, 2), new GArray(true, false, true)).Y < (-GameSim.HALF.Y)), "closed door B blocks");
        this.Check((Walk(Gd.MakeVector2(((-GameSim.HALF.X) - 40), 0), Gd.MakeVector2(2, 0), new GArray(true, true, true)).X > 0), "open door A (west) lets you in");
        this.Check((Walk(Gd.MakeVector2((GameSim.HALF.X + 40), 0), Gd.MakeVector2((-2), 0), new GArray(true, true, false)).X > GameSim.HALF.X), "closed door C (east) blocks");
        this.Check((Walk(Gd.MakeVector2(((-GameSim.HALF.X) - 40), 60), Gd.MakeVector2(2, 0), new GArray(true, true, true)).X < (-GameSim.HALF.X)), "west wall beside door A blocks");
        // Geometry from the GDD (BAL-21, BAL-34, section 4.3)
        this.Check(((GameSim.HALF == (Gd.MakeVector2(11, 7) * 16)) && (Gd.At(GameSim.DOOR_POS, 0).X < 0) && (Gd.At(GameSim.DOOR_POS, 1).Y < 0) && (Gd.At(GameSim.DOOR_POS, 2).X > 0)), "11 x 7 base, doors A west, B north, C east");
        S = this.NewMatch();
        S.Rng.Seed = 42;
        // random resource nodes would nudge spawns out of the band; scenery stays (it is fixed)
        dynamic KeptNodes = S.Nodes;
        S.Nodes = new GArray();
        S.World.Rebuild(S.Nodes, S.Towers);
        S.K = 3;
        S.Active = new GArray(0, 1, 2);
        S.Night();
        S.NextWave = float.PositiveInfinity;
        S.SpawnWave();
        for (int SpawnStep = 0; SpawnStep < 100; SpawnStep++)
        {
            S.Elapsed = SpawnStep * 0.5f;
            S.TickWaves();
        }
        Func<dynamic, dynamic> InBand = (E) =>
        {
            Vector2 Rel = (Vector2)((E.Pos - Gd.At(GameSim.DOOR_POS, E.Door)));
            Vector2 Dd = (Vector2)(Gd.At(GameSim.DOOR_DIR, E.Door));
            dynamic Fwd = (Rel.Dot(Dd) / 32.0f);
            return ((Fwd >= 24.99f) && (Fwd <= 35.01f) && (Gd.Absf(Rel.Dot(Gd.MakeVector2((-Dd.Y), Dd.X))) <= (5.01f * 32)));
        };
        dynamic South = (Func<dynamic, dynamic>)((E) => ((Gd.Absf(E.Pos.X) < GameSim.HALF.X) && (E.Pos.Y > GameSim.HALF.Y)));
        this.Check(!(Gd.Truth(Gd.IsEmpty(S.Enemies))), "night wave spawns enemies");
        if (Gd.Truth(GameSim.SPAWN_ALL_SIDES))
        {
            this.Check(Gd.All(S.Enemies, (Func<dynamic, dynamic>)((E) => (IslandMap.OnLand((Vector2)E.Pos) && (GameSim.WallDist(E.Pos) >= (8 * GameSim.T))))), "wave spawns stay on the island and outside the base");
            this.Check((Gd.Truth(Gd.Any(S.Enemies, (Func<dynamic, dynamic>)((E) => (E.Pos.X < (-GameSim.HALF.X))))) && Gd.Truth(Gd.Any(S.Enemies, (Func<dynamic, dynamic>)((E) => (E.Pos.X > GameSim.HALF.X)))) && Gd.Truth(Gd.Any(S.Enemies, (Func<dynamic, dynamic>)((E) => (E.Pos.Y < (-GameSim.HALF.Y))))) && Gd.Truth(Gd.Any(S.Enemies, (Func<dynamic, dynamic>)((E) => (E.Pos.Y > GameSim.HALF.Y))))), "night wave comes from west, east, north and south");
            this.Check((Gd.Truth(Gd.Any(S.Enemies, (Func<dynamic, dynamic>)((E) => (GameSim.WallDist(E.Pos) < (20 * GameSim.T))))) && Gd.Truth(Gd.Any(S.Enemies, (Func<dynamic, dynamic>)((E) => (GameSim.WallDist(E.Pos) > (45 * GameSim.T)))))), "night wave spreads from near the base to the outer island");
            this.Check(Gd.All(S.Enemies, (Func<dynamic, dynamic>)((E) => (E.Door == S.NearestActiveDoor(E.Pos)))), "each enemy heads for its nearest attacked door");
        }
        else
        {
            this.Check(Gd.All(S.Enemies, InBand), "night spawns in a 10 x 10 tile band 25-35 tiles in front of each active door");
            this.Check(!(Gd.Truth(Gd.Any(S.Enemies, South))), "no night spawn south of the base");
        }
        S.Nodes = KeptNodes;
        // island map (src/world/IslandMap.cs): most wood, stone and herbs round the three clusters in the safe zone,
        // a few at outer landmarks; all titanium past it; every node on land, off the door corridors
        this.Check(Gd.All(S.Nodes, (Func<dynamic, dynamic>)((N) => (!(Gd.Truth(GameSim.InDoorLane(N.Pos))) && IslandMap.OnLand((Vector2)N.Pos) && ((N.Type < 3) || (GameSim.WallDist(N.Pos) > (20 * 32)))))), "resource nodes on land, titanium beyond the safe zone, door corridors clear");
        int Basic = 0, InSafe = 0;
        foreach (dynamic N in Gd.Iter(S.Nodes))
        {
            if (N.Type < 3 && !Gd.Truth(Gd.Get(N, "ambient", false)))
            {
                Basic += 1;
                InSafe += ((GameSim.WallDist(N.Pos) <= (20 * 32)) ? 1 : 0);
            }
        }
        this.Check((InSafe >= (0.7f * Basic)), "most primary resource clusters remain in the safe zone");
        // the landmarks (src/world/WorldLandmarks.cs): all 17 built, the pier walkable out over the sea, and
        // titanium still at every salvage site
        WorldGeometry Wld = (WorldGeometry)S.World;
        this.Check((Wld.LmAt.Count == 17), ("every landmark but the outpost is built (" + Wld.LmAt.Count + ")"));
        Vector2 PierEnd = Wld.LmAt[10] + new Vector2(-200, 0);
        this.Check(((IslandMap.Depth(PierEnd) < 0) && IslandMap.OnLand(PierEnd, 10) && (IslandMap.Keep(Wld.LmAt[10], PierEnd, 10) == PierEnd)), "the Old Fishing Pier can be walked out over the sea");
        bool EverySite = true;
        foreach (int Site in new[] { 5, 6, 7, 13, 14 })
        {
            Vector2 Sp = IslandMap.ById(Site).Pos;
            EverySite &= Gd.Truth(Gd.Any(S.Nodes, (Func<dynamic, dynamic>)((N) => ((N.Type == 3) && (((Vector2)N.Pos).DistanceTo(Sp) < (9 * 32 * IslandMap.SCALE))))));
        }
        this.Check(EverySite, "titanium nodes at all five salvage sites, round their structures");
        S.K = 2;
        S.Active = new GArray(1);
        // a Juggernaut: the Broodmother is slower on purpose (BAL-39)
        S.BossPlan = new GArray(0, 1);
        S.SpawnBoss();
        dynamic B = S.Boss();
        this.Check(((B != null) && (B.Pos.DistanceTo((Gd.At(GameSim.DOOR_POS, 1) + ((Gd.At(GameSim.DOOR_DIR, 1) * 60) * 32))) < 1.0f)), "boss starts 60 tiles in front of its door");
        this.Check((Gd.Absf((B.Spd - (2.16f * GameSim.T))) < 0.01f), "night-2 boss moves at a night-2 Grunt's speed, 2.16 tiles/s (BAL-39)");
        dynamic Snap = S.Snapshot();
        this.Check((Gd.Truth(Gd.Has(Snap, "ps")) && (Gd.Size(Snap.Ps) == 2)), "snapshot has both players");
        // Movement and wave timing (BAL-04, BAL-29, FML-01).
        S = this.NewMatch();
        dynamic Slow = S.Spawn(GameSim.GRUNT, 0, 20.0f, false);
        Vector2 Origin = (Vector2)(Slow.Pos);
        S.TickEnemies(1.0f);
        this.Check((Gd.Absf((Slow.Pos.DistanceTo(Origin) - 64.0f)) < 0.01f), "night-1 Grunt travels 2 tiles per second");
        Gd.Clear(S.Enemies);
        S.Night();
        S.Elapsed = 59.99f;
        S.TickWaves();
        this.Check(((S.Wave == 0) && Gd.Truth(Gd.IsEmpty(S.Enemies))), "no first-night wave before 60 seconds");
        S.Elapsed = 60.0f;
        S.TickWaves();
        this.Check(((S.Wave == 1) && !(Gd.Truth(Gd.IsEmpty(S.Enemies)))), "first-night wave begins with one enemy at 60 seconds");
        S.Elapsed = 209.99f;
        S.TickWaves();
        this.Check((S.Wave == 1), "first-night waves wait 150 seconds");
        S.Elapsed = 210.0f;
        S.TickWaves();
        this.Check((S.Wave == 2), "second first-night wave arrives at 210 seconds");
        S.K = 5;
        Gd.Clear(S.Enemies);
        S.Night();
        S.Elapsed = 30.0f;
        S.TickWaves();
        S.Elapsed = 74.99f;
        S.TickWaves();
        this.Check((S.Wave == 1), "final-night waves wait 45 seconds");
        S.Elapsed = 75.0f;
        S.TickWaves();
        this.Check((S.Wave == 2), "final-night second wave arrives at 75 seconds");
        // Towers (AGT-12, MEC-11, MEC-28, BAL-28)
        S = this.NewMatch();
        // Isolate tower rules from randomly placed line-of-sight blockers.
        Gd.Clear(S.Nodes);
        S.World.Rebuild(S.Nodes, S.Towers);
        dynamic Te = Gd.At(S.Players, 1);
        dynamic Ts = Gd.At(S.Players, 2);
        Te.Pos = (Gd.At(GameSim.DOOR_POS, 0) + ((Gd.At(GameSim.DOOR_DIR, 0) * 1.5f) * 32));
        S.Action(1, "tower", 0);
        this.Check(((Gd.At(S.Towers, 0) == null) && Gd.Truth(S.FreeTower)), "no tower closer than 3 tiles to a door");
        Ts.Pos = (Gd.At(GameSim.DOOR_POS, 0) + ((Gd.At(GameSim.DOOR_DIR, 0) * 5) * 32));
        S.Action(2, "tower", 0);
        this.Check((Gd.At(S.Towers, 0) == null), "only an Engineer builds towers");
        Te.Pos = (Gd.At(GameSim.DOOR_POS, 0) + ((Gd.At(GameSim.DOOR_DIR, 0) * 5) * 32));
        S.Action(1, "tower", 0);
        dynamic Tw = Gd.At(S.Towers, 0);
        this.Check(((Tw != null) && (Tw.Hp == 300.0f) && !(Gd.Truth(S.FreeTower)) && (S.Count(Te, "wood") == 0)), "the free tower is placed 3-8 tiles in front of door A at no cost");
        Te.Pos = (Gd.At(GameSim.DOOR_POS, 2) + ((Gd.At(GameSim.DOOR_DIR, 2) * 5) * 32));
        S.SetCount(Te, "wood", 20);
        S.SetCount(Te, "titanium", 10);
        S.Action(1, "tower", 0);
        this.Check(((Gd.At(S.Towers, 2) == null) && (S.Count(Te, "wood") == 20)), "a second tower is locked until the first boss is killed");
        S.Unlock = 2;
        S.Action(1, "tower", 0);
        this.Check(((Gd.At(S.Towers, 2) != null) && (S.Count(Te, "wood") == 10) && (S.Count(Te, "titanium") == 5)), "an unlocked tower costs 10 wood + 5 titanium");
        Te.Pos = (Gd.At(GameSim.DOOR_POS, 2) + ((Gd.At(GameSim.DOOR_DIR, 2) * 6) * 32));
        S.Action(1, "tower", 0);
        this.Check((S.Count(Te, "wood") == 10), "one tower per door");
        dynamic Tg = S.Spawn(GameSim.GRUNT, 0, 0.0f);
        Tg.Pos = (Tw.Pos + Gd.MakeVector2(0, (4 * 32)));
        Tg.State = 4;
        // A stationary target keeps the DPS/range check deterministic.
        Tg.Spd = 0.0f;
        Tg.Home = Tg.Pos;
        Tg.Wander = Tg.Pos;
        Te.Pos = Vector2.Zero;
        Ts.Pos = Vector2.Zero;
        this.Run(S, 3.0f);
        this.Check(((Tg.Hp < (Tg.Maxhp - 20)) && (Tg.Hp > (Tg.Maxhp - 28))), Gd.Format("a tower shoots the nearest enemy within 6 tiles at 8 DPS (%.1f)", (Tg.Maxhp - Tg.Hp)));
        this.Run(S, 6.0f);
        this.Check(!(Gd.Truth(Gd.Has(S.Enemies, Tg))), "a tower kills on its own");
        dynamic FarE = S.Spawn(GameSim.GRUNT, 0, 0.0f);
        FarE.Pos = (Tw.Pos + Gd.MakeVector2(0, (7 * 32)));
        FarE.State = 4;
        FarE.Spd = 0.0f;
        FarE.Home = FarE.Pos;
        FarE.Wander = FarE.Pos;
        float Hp0 = Gd.Float(FarE.Hp);
        this.Run(S, 1.0f);
        this.Check((FarE.Hp == Hp0), "nothing beyond 6 tiles is shot");
        Gd.Clear(S.Enemies);
        S.Phase = "night";
        // tower behaviour is checked without unrelated wave attackers
        S.NextWave = float.PositiveInfinity;
        S.Towers[2] = null;
        dynamic G2 = S.Spawn(GameSim.GRUNT, 0, 0.0f);
        G2.Pos = (Tw.Pos + ((Gd.At(GameSim.DOOR_DIR, 0) * 10) * 32));
        G2.Hp = 1000.0f;
        // the enemy must cross the light and reach the tower before hitting it
        this.Run(S, 8.0f);
        this.Check(((G2.State == 5) && (Tw.Hp < 300.0f) && (Gd.At(S.Doors, 0) == GameSim.DOOR_HP)), "at night an enemy entering the light attacks the tower before its door");
        G2.Cd = 99.0f;
        Te.Pos = (Tw.Pos + Gd.MakeVector2(0, 32));
        S.SetCount(Te, "wood", 5);
        Te.Inter = true;
        float Thp = Gd.Float(Tw.Hp);
        // a hit blocks repair for 2 s (FML-08)
        this.Run(S, 3.0f);
        this.Check((Tw.Hp > Thp), "the Engineer repairs a tower like a door");
        Te.Inter = false;
        G2.Cd = 0.0f;
        Tw.Hp = 5.0f;
        this.Run(S, 0.5f);
        this.Check(((Tw.Hp == 0.0f) && Gd.Truth(Tw.Wrecked) && Gd.Truth(Gd.At(Gd.At(S.Snapshot().Tw, 0), 3))), "a tower at 0 HP is destroyed");
        this.Run(S, 0.2f);
        this.Check((G2.State != 5), "enemies locked on a fallen tower walk on to their door");
        Te.Inter = true;
        this.Run(S, 1.0f);
        this.Check((Tw.Hp == 0.0f), "a destroyed tower cannot be repaired while an enemy is in its light");
        Gd.Clear(S.Enemies);
        S.SetCount(Te, "wood", 20);
        this.Run(S, 10.0f);
        this.Check((Gd.Absf((Tw.Hp - 90.0f)) < 0.01f), Gd.Format("a destroyed tower is repaired only to 30%% (%.1f)", Tw.Hp));
        Te.Inter = false;
        dynamic G3 = S.Spawn(GameSim.SPITTER, 0, 0.0f);
        G3.Pos = (Tw.Pos + Gd.MakeVector2(0, (2 * 32)));
        this.Run(S, 0.2f);
        this.Check((G3.State != 5), "Spitters never attack towers");
        S = this.NewMatch();
        S.Action(1, "vote", 0);
        S.Action(2, "vote", 0);
        this.Run(S, 179.0f, "dusk");
        this.Check((S.Phase == "day"), "T-FML14: a unanimous vote does not skip the day before 180 s");
        this.Run(S, 1.1f, "dusk");
        this.Check((S.Phase == "dusk"), "the vote skips the day once 180 s have passed");
        this.Check((Gd.Absf((S.TimeLeft - 15.0f)) < 0.05f), "dusk warning lasts 15 s (BAL-05)");
        int D0 = Gd.Int(Gd.At(S.Active, 0));
        this.Check(((Gd.At(S.Towers, D0) != null) && (Gd.At(S.Towers, D0).Pos.DistanceTo((Gd.At(GameSim.DOOR_POS, D0) + ((Gd.At(GameSim.DOOR_DIR, D0) * 5) * 32))) < 1.0f) && !(Gd.Truth(S.FreeTower))), "an unplaced free tower appears in front of night 1's door at dusk");
    }
}
