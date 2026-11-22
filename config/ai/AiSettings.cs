using Godot;

/// <summary>Enemy, wave, roamer and drop tuning. Owner: Vu Duc Viet Anh. Edit config/ai/ai.tres in the Godot Inspector; restart the game after a change.</summary>
[GlobalClass]
public partial class AiSettings : Resource
{
    private static AiSettings _current;

    /// <summary>The shared settings file, loaded once (defaults when the file is missing).</summary>
    public static AiSettings Current => _current ??= (GD.Load<Resource>("res://config/ai/ai.tres") as AiSettings) ?? new AiSettings();

    [Export] public float EnemySpeed { get; set; } = 1;         // multiplier on BAL-29..32 speeds (GDD: 1)
    [Export] public float FirstWave { get; set; } = 15;         // first arrival on nights 3-5; nights 1-2 ease in later
    [Export] public float WaveInterval { get; set; } = 45;      // night-3 interval; early nights slower, night 4 slightly faster
    [Export] public float FinalWaveInterval { get; set; } = 30; // night 5
    [Export] public float SpawnMultiplier { get; set; } = 1;    // multiplier on the FML-04 budget (GDD: 1)
    [Export] public float RoamerMultiplier { get; set; } = 1;   // multiplier on the FML-16 roamer chance (GDD: 1)
    [Export] public float DropMultiplier { get; set; } = 1;     // multiplier on BAL-41 drop chances (GDD: 1)
    [Export] public float EarlyNightFirstWave { get; set; } = 20;
    [Export] public float EarlyNightWaveInterval { get; set; } = 60;
    [Export] public float SpawnSpacing { get; set; } = 2;       // night 1; decreases to 0.5 s on night 5
    [Export] public int MaxNightEnemies { get; set; } = 40;
}
