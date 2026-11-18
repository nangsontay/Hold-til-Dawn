using Godot;

/// <summary>Match rules tuning (day, dusk). Owner: Pham Minh Hieu. Edit config/gameplay/gameplay.tres in the Godot Inspector; restart the game after a change.</summary>
[GlobalClass]
public partial class GameplaySettings : Resource
{
    private static GameplaySettings _current;

    /// <summary>The shared settings file, loaded once (defaults when the file is missing).</summary>
    public static GameplaySettings Current => _current ??= (GD.Load<Resource>("res://config/gameplay/gameplay.tres") as GameplaySettings) ?? new GameplaySettings();

    [Export] public float DayMin { get; set; } = 180;     // FML-14: skip vote counts only after 180 s of day
    [Export] public float DuskSeconds { get; set; } = 15; // BAL-05: dusk warning
}
