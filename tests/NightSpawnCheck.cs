using Godot;
using System;
using System.Collections.Generic;
using HoldTilDawn;

public partial class NightSpawnCheck : Node
{
    int Checks;
    void Check(bool Pass, string What)
    {
        if (!Pass) throw new Exception(What);
        Checks++; GD.Print("ok   " + What);
    }

    public override void _Ready()
    {
        try { Run(); GetTree().Quit(); }
        catch (Exception Error) { GD.PushError(Error.ToString()); GetTree().Quit(1); }
    }

    void Prepare(GameSim Sim, int Night)
    {
        Sim.K = Night; Sim.Enemies = new GArray(); Sim.Active = new GArray(0, 1, 2);
        Sim.Rng.Seed = 42; Sim.Night(); Sim.BossSpawned = true;
    }

    List<(float At, int Type, int Door)> Arrivals(GameSim Sim, int Night, bool Kill)
    {
        Prepare(Sim, Night);
        var Events = new List<(float, int, int)>();
        int AlivePeak = 0;
        for (int Step = 0; Step < 2400; Step++)
        {
            float Now = Step * 0.25f;
            Sim.Elapsed = Now;
            Sim.TimeLeft = 1000f; // exercise the director independently of combat/end conditions
            int Before = Gd.Int(Sim.NextId);
            Sim.TickWaves();
            int Born = Gd.Int(Sim.NextId) - Before;
            if (Born > 1) throw new Exception("A tick created multiple regular enemies");
            if (Born != 0)
            {
                dynamic E = Gd.At(Sim.Enemies, Gd.Size(Sim.Enemies) - 1);
                Events.Add((Now, Gd.Int(E.Type), Gd.Int(E.Door)));
            }
            AlivePeak = Math.Max(AlivePeak, Gd.Size(Sim.Enemies));
            if (Kill) Gd.Clear(Sim.Enemies);
        }
        Check(AlivePeak <= Sim.NightEnemyLimit, $"night {Night}: alive limit {Sim.NightEnemyLimit} respected");
        for (int I = 1; I < Events.Count; I++)
            if (Events[I].Item1 - Events[I - 1].Item1 < Sim.NightSpawnSpacing - 0.001f)
                throw new Exception("Spawn spacing was bypassed");
        Check(Events.Count > 0 && Events[0].Item1 >= Sim.NightFirstWaveDelay, $"night {Night}: warm-up and per-enemy spacing respected");
        return Events;
    }

    void Run()
    {
        using var Sim = new GameSim();
        for (int I = 1; I <= 3; I++) Sim.AddPlayer(I, "Test", I - 1);
        Sim.StartMatch();
        int Previous = 0;
        List<(float At, int Type, int Door)> FirstNight = null;
        for (int Night = 1; Night <= 5; Night++)
        {
            var Events = Arrivals(Sim, Night, true);
            int Quota = Sim.NightQuota, Spawned = Sim.NightSpawned;
            GD.Print($"PACING night={Night} regular_births/600s={Events.Count} first={Sim.NightFirstWaveDelay}s wave_every={Sim.NightWaveInterval}s spacing={Sim.NightSpawnSpacing}s cap={Sim.NightEnemyLimit}");
            Check(Events.Count > Previous, $"night {Night}: more enemies than the previous night");
            Previous = Events.Count;
            if (Night == 1)
            {
                FirstNight = Events;
                Check(Events.Count <= 30 && Events.TrueForAll(E => E.Type == GameSim.GRUNT), "first night stays light and only uses Grunts");
                // while the first enemies are still alive the next wave keeps its schedule
                var Held = Arrivals(Sim, 1, false);
                Check(Held[0].At == 20 && Held[1].At == 22 && Held[2].At == 24 && Held[3].At >= 78,
                    "first night: three quick enemies, then most of a minute of rest while they live (three-player normal mode)");
                // an empty field brings the next wave forward instead
                Check(Events[3].At < 24 + 5 + Sim.NightSpawnSpacing + 0.3f, "first night: a cleared field brings the next wave in 5 s");
            }
            Check(Events.TrueForAll(E => Gd.Int(Gd.At(Gd.At(GameSim.ENEMY, E.Type), 4)) <= Night), "later enemy types remain locked to their night");
            int Boss = (Night == 2 || Night == 4) ? 1 : 0;
            Check(Quota > Boss && Events.Count == Quota - Boss && Spawned == Events.Count,
                $"night {Night}: exactly the night's quota of {Quota - Boss} regular enemies comes{(Boss > 0 ? " (plus the boss)" : "")}");
            Arrivals(Sim, Night, false);
        }
        var Replay = Arrivals(Sim, 1, true);
        Check(FirstNight.Count == Replay.Count && FirstNight.TrueForAll(E => Replay.Contains(E)), "same seed produces the same paced arrivals");

        Prepare(Sim, 1); Sim.Elapsed = 500f; Sim.TickWaves();
        Check(Gd.Int(Sim.Wave) == 1 && Gd.Size(Sim.Enemies) == 1 && Gd.Float(Sim.NextWave) > 500,
            "a large clock jump never catches up missed waves in a burst");
        Sim.TickWaves();
        Check(Gd.Size(Sim.Enemies) == 1, "repeated calls at the same time do not spawn twice");
        Sim.Dawn(); Sim.Phase = "night"; Sim.NextWave = float.PositiveInfinity; Sim.Elapsed = 510f;
        Sim.TickWaves(); Check(Gd.Size(Sim.Enemies) == 1, "dawn discards unfinished arrivals");
        Prepare(Sim, 1); Sim.Elapsed = 60f; Sim.TickWaves(); Sim.End(false, "Test");
        Sim.Phase = "night"; Sim.TimeLeft = 100; Sim.NextWave = float.PositiveInfinity; Sim.Elapsed = 70f;
        Sim.TickWaves(); Check(Gd.Size(Sim.Enemies) == 0, "result screen discards unfinished arrivals");
        Prepare(Sim, 1); Sim.Elapsed = 60f; Sim.TickWaves(); Sim.ToLobby();
        Sim.Phase = "night"; Sim.TimeLeft = 100; Sim.NextWave = float.PositiveInfinity; Sim.Elapsed = 70f;
        Sim.TickWaves(); Check(Gd.Size(Sim.Enemies) == 0, "lobby discards unfinished arrivals");
        Prepare(Sim, 1); Sim.Elapsed = 60f; Sim.TickWaves(); Sim.TimeLeft = 0; Sim.Elapsed = 66f;
        Sim.TickWaves(); Check(Gd.Size(Sim.Enemies) == 1, "no final burst at the night deadline");

        // The night's quota: the HUD count, and dawn once every enemy of it is dead.
        Prepare(Sim, 1);
        Sim.Phase = "night"; Sim.BaseHp = 1000;
        Sim.NightSpawned = Sim.NightQuota - 1;
        dynamic Last = Sim.Spawn(GameSim.GRUNT, 0, 30f); Last.Quota = true; Sim.NightSpawned++;
        Sim.CheckEnd(0.1f);
        Check(Sim.Phase == "night" && Sim.NightKilled() == Sim.NightQuota - 1, "the night goes on while one of its enemies lives");
        Last.Hp = 0f; Sim.Enemies = Gd.Filter(Sim.Enemies, (Func<dynamic, dynamic>)((E) => (E.Hp > 0)));
        Check(Sim.NightKilled() == Sim.NightQuota && Sim.NightAllDown(), "the bar is full once the last one dies");
        Sim.CheckEnd(0.1f);
        Check(Sim.Phase == "day" && Gd.Int(Sim.K) == 2, "clearing the enemy quota ends the night without fetch quests");
        Prepare(Sim, 2); Sim.BossSpawned = false; Sim.Phase = "night"; Sim.BaseHp = 1000;
        Check(Sim.NightQuota >= 2, "the boss counts toward nights 2 and 4");
        Sim.NightSpawned = Sim.NightQuota - 1; Sim.Elapsed = 100f; Sim.TickWaves(); Sim.Elapsed = 111f; Sim.TickWaves();
        Check(Gd.Truth(Sim.BossSpawned) && Sim.NightSpawned == Sim.NightQuota, "once the regular enemies are cleared the boss comes early");

        // All enemies ask for a route after a collider revision. The bounded rotating update must serve all.
        Prepare(Sim, 5); Sim.NextWave = float.PositiveInfinity;
        for (int I = 0; I < 16; I++) Sim.Spawn(GameSim.GRUNT, I % 3, 30f);
        var Began = new Dictionary<int, Vector2>();
        foreach (dynamic E in Gd.Iter(Sim.Enemies)) Began[Gd.Int(E.Id)] = E.Pos;
        for (int I = 0; I < 24; I++)
        {
            Sim.TickEnemies(0.05f);
            Check(Sim.EnemyNavigationUpdatesThisTick <= 2, "at most two enemy navigation recalculations per tick");
        }
        foreach (dynamic E in Gd.Iter(Sim.Enemies))
            Check(Gd.Has(E, "nav_way") && ((Vector2)E.Pos).DistanceTo(Began[Gd.Int(E.Id)]) > 1,
                "rotating navigation budget serves each enemy without starvation");

        // Compare the new typed route against the established implementation for the same geometry.
        var World = (WorldGeometry)Sim.World;
        World.StaticSolids = new GArray(new GArray(new Vector2(500, 0), 24f, true));
        Sim.Nodes = new GArray(); Sim.Towers = new GArray(); World.Rebuild(Sim.Nodes, Sim.Towers);
        var Walls = new[] { new Rect2(-(Vector2)GameSim.HALF, (Vector2)GameSim.HALF * 2) };
        var LegacyWalls = new GArray(Walls[0]);
        foreach (float Radius in new[] { 16f, 20f, 35.2f })
            foreach (float Offset in new[] { -24f, 0f, 24f })
            {
                var From = new Vector2(400, Offset); var To = new Vector2(650, 0);
                Vector2 Expected = World.Waypoint(From, To, Radius, LegacyWalls);
                Check(World.WaypointFast(From, To, Radius, Walls).IsEqualApprox(Expected), "typed navigation matches the original route around a rock");
            }
        int Builds = World.NavigationGridBuilds, Searches = World.NavigationPathSearches;
        for (int I = 0; I < 10; I++) World.WaypointFast(new Vector2(400, 0), new Vector2(650, 0), 16f, Walls);
        Check(World.NavigationGridBuilds == Builds && World.NavigationPathSearches == Searches, "unchanged routes reuse native grid and path caches");
        World.StaticSolids = new GArray(); World.Rebuild(Sim.Nodes, Sim.Towers);
        Check(World.WaypointFast(new Vector2(400, 0), new Vector2(650, 0), 16f, Walls) == new Vector2(650, 0),
            "removing a blocker invalidates the old navigation route");
        GD.Print($"NIGHT SPAWN ALL OK: {Checks} checks");
    }
}
