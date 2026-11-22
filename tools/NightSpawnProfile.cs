using Godot;
using System;
using System.Diagnostics;
using HoldTilDawn;

// Reproducible CPU sample, independent of drawing/GPU load. Run with -- --night=1 or --night=5.
public partial class NightSpawnProfile : Node
{
    public override void _Ready()
    {
        try
        {
            int Night = 1;
            bool EveryFrame = Array.IndexOf(OS.GetCmdlineUserArgs(), "--every-frame") >= 0;
            int Hz = 60, Steps = 1800;
            foreach (string Arg in OS.GetCmdlineUserArgs()) if (Arg.StartsWith("--night=")) Night = int.Parse(Arg[8..]);
            using var Sim = new GameSim();
            for (int I = 1; I <= 3; I++) Sim.AddPlayer(I, "Profile", I - 1);
            Sim.Rng.Seed = 42;
            Sim.StartMatch(); Sim.Rng.Seed = 42; Sim.EnemyBodyInterval = EveryFrame ? 0f : .05f; Sim.K = Night; Sim.RadioBuilt = true; Sim.Active = new GArray(0, 1, 2); Sim.Night();
            Sim.BossSpawned = true;
            foreach (dynamic P in Gd.Iter(Gd.Values(Sim.Players))) P.Pos = new Vector2(0, 2000);
            double SpawnMs = 0, AiMs = 0, Total = 0;
            long Bytes = GC.GetAllocatedBytesForCurrentThread();
            int Born = 0, MaxBirths = 0;
            // Warm the dynamic call sites without creating an enemy or navigation grid.
            Sim.TickEnemySimulation(1f / Hz); Sim.TickWaves();
            for (int I = 0; I < Steps; I++)
            {
                Sim.Elapsed = 60f + I / (float)Hz;
                long BeforeId = Gd.Int(Sim.NextId);
                long T = Stopwatch.GetTimestamp();
                Sim.TickWaves();
                double Spawn = Stopwatch.GetElapsedTime(T).TotalMilliseconds;
                T = Stopwatch.GetTimestamp();
                Sim.TickEnemySimulation(1f / Hz);
                double Ai = Stopwatch.GetElapsedTime(T).TotalMilliseconds;
                SpawnMs = Math.Max(SpawnMs, Spawn); AiMs = Math.Max(AiMs, Ai); Total += Spawn + Ai;
                int Births = Gd.Int(Sim.NextId) - (int)BeforeId;
                Born += Births; MaxBirths = Math.Max(MaxBirths, Births);
            }
            GD.Print($"NIGHT PROFILE night={Night} ai={(EveryFrame ? "60Hz" : "20Hz")} body_steps={Sim.EnemyBodySteps} births={Born} max_births/tick={MaxBirths} worst_spawn_ms={SpawnMs:F3} worst_ai_ms={AiMs:F3} avg_spawn_ai_ms={Total/Steps:F3} managed_mb={(GC.GetAllocatedBytesForCurrentThread()-Bytes)/1048576.0:F2}");
            GetTree().Quit();
        }
        catch (Exception E) { GD.PushError(E.ToString()); GetTree().Quit(1); }
    }
}
