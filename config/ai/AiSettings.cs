using Godot;

/// <summary>Enemy, wave, roamer and drop tuning. Owner: Vu Duc Viet Anh. Edit config/ai/ai.tres in the Godot Inspector; restart the game after a change.</summary>
[GlobalClass]
public partial class AiSettings : Resource
{
    private static AiSettings _current;

    /// <summary>The shared settings file, loaded once (defaults when the file is missing).</summary>
    public static AiSettings Current => _current ??= (GD.Load<Resource>("res://config/ai/ai.tres") as AiSettings) ?? new AiSettings();

    [Export] public float EnemySpeed { get; set; } = 1;         // multiplier on BAL-29..32 speeds (GDD: 1)
    [Export] public float FirstWave { get; set; } = 30;         // BAL-04: first wave 30 s into the night
    [Export] public float WaveInterval { get; set; } = 90;      // BAL-04: every 90 s on nights 1-4
    [Export] public float FinalWaveInterval { get; set; } = 45; // BAL-04: every 45 s on night 5
    [Export] public float SpawnMultiplier { get; set; } = 1;    // multiplier on the FML-04 budget (GDD: 1)
    [Export] public float RoamerMultiplier { get; set; } = 1;   // multiplier on the FML-16 roamer chance (GDD: 1)
    [Export] public float DropMultiplier { get; set; } = 1;     // multiplier on BAL-41 drop chances (GDD: 1)
}
