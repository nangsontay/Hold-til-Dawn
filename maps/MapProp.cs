using Godot;
using FileAccess = Godot.FileAccess;

/// <summary>Position is the ground anchor of the nature sprite.</summary>
[Tool]
public partial class MapProp : Sprite2D
{
    [Export] public float SolidRadius { get; set; } = 20;
    [Export] public bool BlocksAttacks { get; set; } = true;

    public override void _Ready()
    {
        TextureChanged += AlignFeet;
        AlignFeet();
    }

    private void AlignFeet()
    {
        if (Texture == null) return;
        var index = Json.ParseString(FileAccess.GetFileAsString("res://art/nature/index.json")).AsGodotDictionary();
        var key = Texture.ResourcePath.GetFile().GetBaseName();
        if (!index.ContainsKey(key)) return;
        var anchor = index[key].AsGodotArray();
        Centered = false;
        Offset = -new Vector2(anchor[0].AsSingle(), anchor[1].AsSingle());
    }
}
