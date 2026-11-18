using Godot;

/// <summary>Island and resource tuning. Owner: Vu Duc Viet Anh. Edit config/world/world.tres in the Godot Inspector; restart the game after a change.</summary>
[GlobalClass]
public partial class WorldSettings : Resource
{
    private static WorldSettings _current;

    /// <summary>The shared settings file, loaded once (defaults when the file is missing).</summary>
    public static WorldSettings Current => _current ??= (GD.Load<Resource>("res://config/world/world.tres") as WorldSettings) ?? new WorldSettings();

    [Export] public int NodeMultiplier { get; set; } = 1; // multiplier on BAL-17 resource nodes per type (GDD: 1)
}
