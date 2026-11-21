using Godot;

/// <summary>Camera and display tuning (client only). Owner: Mai Thien Ngon. Edit config/display/display.tres in the Godot Inspector; restart the game after a change.</summary>
[GlobalClass]
public partial class DisplaySettings : Resource
{
    private static DisplaySettings _current;

    /// <summary>The shared settings file, loaded once (defaults when the file is missing).</summary>
    public static DisplaySettings Current => _current ??= (GD.Load<Resource>("res://config/display/display.tres") as DisplaySettings) ?? new DisplaySettings();

    [Export] public Vector2 CameraDeadZone { get; set; } = new(30, 18);
    [Export] public float CameraFollowRate { get; set; } = 8;
    /// <summary>Most frames drawn per second (0 = one per screen refresh). Without a cap a 120-165 Hz laptop screen keeps the CPU and GPU flat out all match long.</summary>
    [Export] public int MaxFps { get; set; } = 60;
}
