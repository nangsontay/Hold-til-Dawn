using Godot;
using System;
using HoldTilDawn;

public partial class GameSim
{
    // A wave is a bounded budget, not a queue of allocated enemies. Only one body is made when due.
    float PendingWaveBudget, NextEnemySpawnAt, WaveSpawnEndsAt;
    int WaveSpawnOrdinal;
    static readonly float[] NIGHT_BUDGET = { 18, 30, 45, 60, 80 };
    static readonly float[] SPAWN_GAP = { 6, 4, 3, 2, 1.5f };
    static readonly int[] NIGHT_ALIVE_CAP = { 8, 12, 18, 24, 32 };
    int NightIndex => Math.Clamp(Gd.Int((object)K) - 1, 0, 4);
    public float NightFirstWaveDelay => Math.Max(0, NightIndex switch {
        0 => AiSettings.Current.EarlyNightFirstWave,
        1 => (AiSettings.Current.EarlyNightFirstWave + FIRST_WAVE_DELAY) * 0.5f,
        _ => FIRST_WAVE_DELAY });
    public float NightWaveInterval => Math.Max(10, NightIndex switch {
        0 => AiSettings.Current.EarlyNightWaveInterval,
        1 => (AiSettings.Current.EarlyNightWaveInterval + WAVE_INTERVAL) * 0.5f,
        2 => WAVE_INTERVAL, 3 => WAVE_INTERVAL * (5f / 6f), _ => FINAL_WAVE_INTERVAL });
    public float NightSpawnSpacing => Math.Max(0.25f, SPAWN_GAP[NightIndex] * AiSettings.Current.SpawnSpacing / 6f);
    public int NightEnemyLimit => Math.Clamp(Math.Min(NIGHT_ALIVE_CAP[NightIndex], AiSettings.Current.MaxNightEnemies), 1, Gd.Int((object)MAX_ENEMIES));

    public void ResetNightSpawns()
    {
        PendingWaveBudget = 0; NextEnemySpawnAt = 0; WaveSpawnEndsAt = 0; WaveSpawnOrdinal = 0;
        Carry = 0f;
    }

    void PlanNightWave()
    {
        // Old unfinished waves are discarded. Neither a full field nor a hitch accumulates future bursts.
        float Crew = 1f + 0.35f * Math.Max(0, Gd.Size((object)Players) - 1);
        float Growth = NightIndex == 0 ? 1 : 1 + 0.08f * Math.Clamp(Gd.Int((object)Wave) - 1, 0, 3);
        float BossScale = 1;
        foreach (GDict Enemy in (GArray)Enemies)
            if (Gd.Int(Enemy.Get("type")) == BOSS) { BossScale = 1f / 3f; break; }
        var ModeStats = (GArray)((GArray)MODE_MUL)[Gd.Int((object)Mode)];
        float ModeScale = Gd.Float((object)ModeStats[0]);
        PendingWaveBudget = Math.Clamp(NIGHT_BUDGET[NightIndex] * Crew * Growth * BossScale * ModeScale * SPAWN_MUL
            + Gd.Float((object)Carry), 0, NightEnemyLimit * 20f);
        Carry = 0f;
        NextEnemySpawnAt = Gd.Float((object)Elapsed);
        WaveSpawnEndsAt = NextEnemySpawnAt + NightWaveInterval * 0.7f;
        WaveSpawnOrdinal = 0;
    }

    void TickPendingNightSpawn()
    {
        float Now = Gd.Float((object)Elapsed);
        if (PendingWaveBudget < 10) return;
        if (Now >= WaveSpawnEndsAt) { PendingWaveBudget = 0; return; }
        if (Now < NextEnemySpawnAt) return;
        // Delay instead of a catch-up loop, including when the alive cap has been reached.
        NextEnemySpawnAt = Now + NightSpawnSpacing;
        if (Gd.Size((object)Enemies) >= NightEnemyLimit || Gd.Size((object)Active) == 0) return;
        Span<int> Eligible = stackalloc int[3];
        int Count = 0;
        for (int Type = 0; Type < 3; Type++)
        {
            var Stats = (GArray)((GArray)ENEMY)[Type];
            if (Gd.Int((object)Stats[4]) <= Gd.Int((object)K)
                && Gd.Float((object)Stats[3]) <= PendingWaveBudget) Eligible[Count++] = Type;
        }
        if (Count == 0) { PendingWaveBudget = 0; return; }
        var Random = (RandomNumberGenerator)Gd.Unwrap((object)Rng);
        int Kind = Eligible[(int)(Random.Randi() % (uint)Count)];
        int Door = Gd.Int((object)((GArray)Active)[WaveSpawnOrdinal++ % ((GArray)Active).Count]);
        float Distance = Gd.Truth((object)SPAWN_ALL_SIDES)
            ? Random.RandfRange(Gd.Float((object)WAVE_MIN_DISTANCE), (Gd.Float((object)ISLAND) - ((Vector2)HALF).Length()) / T - 2)
            : Random.RandfRange(25f, 35f);
        GDict Enemy = SpawnEnemy(Kind, Door, Distance, Gd.Truth((object)SPAWN_ALL_SIDES));
        if (Gd.Truth((object)SPAWN_ALL_SIDES)) Enemy["door"] = NearestActiveDoor(Enemy.Get("pos"));
        var ChosenStats = (GArray)((GArray)ENEMY)[Kind];
        PendingWaveBudget -= Gd.Float((object)ChosenStats[3]);
        if (PendingWaveBudget < 10)
        {
            Carry = PendingWaveBudget;
            PendingWaveBudget = 0;
        }
    }
}
