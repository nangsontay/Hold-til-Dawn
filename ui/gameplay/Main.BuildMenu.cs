using Godot;
using Environment = Godot.Environment;
using FileAccess = Godot.FileAccess;
using System;
using System.Threading.Tasks;
using HoldTilDawn;

// Owner: Pham Minh Hieu. Engineer build menu (B, MEC-11): pick a wall or a tower, see a green or red placement
// preview at the cursor, left mouse places it, right mouse cancels, R turns a wall.
public partial class Main : Node2D
{
    public dynamic BuildOpen = false;
    public dynamic BuildKind = "";
    public dynamic BuildVert = false;

    // [kind, label, cost text, unlock tier]
    public static readonly GArray BUILD_OPTIONS = new GArray(
        new GArray("wood", "Wood wall  200 HP", "5 wood", 0),
        new GArray("stone", "Stone wall  400 HP", "5 stone", 1),
        new GArray("tower", "Tower  300 HP, lights 6 tiles", "10 wood + 5 titanium", 2));

    public void CloseBuild()
    {
        BuildOpen = false;
        BuildKind = "";
    }

    public void ToggleBuild()
    {
        dynamic Me = Gd.Get(Snap.Ps, MyId);
        if (((Me == null) || (Gd.At(Me, 1) != 2)))
        {
            this.Note("Only the Engineer builds");
            return;
        }
        if (Gd.Truth(BuildOpen))
        {
            this.CloseBuild();
            return;
        }
        BuildOpen = true;
        BuildKind = "";
        BagOpen = false;
        CraftOpen = false;
    }

    // A short local message in the hint line (no server round trip).
    public void Note(string S)
    {
        LocalNote = S;
        LocalNoteT = 2.5f;
    }

    public dynamic LocalNote = "";
    public dynamic LocalNoteT = 0.0f;

    public bool BuildKey(dynamic Code)
    {
        if (!Gd.Truth(BuildOpen))
        {
            return false;
        }
        int N = Gd.Int((Code - 49));
        if (((N >= 0) && (N < BUILD_OPTIONS.Count)))
        {
            BuildKind = Gd.At(Gd.At(BUILD_OPTIONS, N), 0);
            return true;
        }
        if (Gd.Equal(Code, 82))
        {
            BuildVert = !Gd.Truth(BuildVert);
            return true;
        }
        return false;
    }

    public Vector2 BuildSpot()
    {
        Vector2 Mouse = (Vector2)(Gd.Native(this, "get_global_mouse_position"));
        return ((BuildKind == "tower") ? Mouse : GameSim.SnapToTile((Mouse + Gd.MakeVector2(0, (LIFT * 0.5f)))));
    }

    // Why the selected structure cannot be placed at the cursor, or "" (the server checks again).
    public string BuildProblem(dynamic Me)
    {
        Vector2 Spot = this.BuildSpot();
        if ((Spot.DistanceTo(LocalPos) > (GameSim.BUILD_REACH * T)))
        {
            return "Too far away";
        }
        dynamic Walls = Gd.Map(Gd.Get(Snap, "walls", new GArray()), (Func<dynamic, dynamic>)((W) => new GDict { { "pos", Gd.At(W, 0) }, { "vert", Gd.At(W, 1) } }));
        int Tier = 0;
        GDict Cost = new GDict();
        if ((BuildKind == "tower"))
        {
            string Why = GameSim.TowerSpotProblem(Spot, Geometry);
            if ((Why != ""))
            {
                return Why;
            }
            if ((Gd.Size(Gd.Get(Snap, "tw", new GArray())) >= GameSim.MAX_TOWERS))
            {
                return Gd.Format("The team already has %d towers", GameSim.MAX_TOWERS);
            }
            if (Gd.Truth(Gd.Get(Snap, "free_tower", false)))
            {
                return "";
            }
            Tier = 2;
            Cost = new GDict { { "wood", 10 }, { "titanium", 5 } };
        }
        else
        {
            dynamic W = GameSim.WALL_KINDS[BuildKind];
            string Why = GameSim.WallSpotProblem(Spot, Gd.Truth(BuildVert), Geometry, Walls);
            if ((Why != ""))
            {
                return Why;
            }
            Tier = Gd.Int(Gd.At(W, 2));
            Cost = Gd.At(W, 1);
        }
        if ((Tier > Gd.Get(Snap, "unlock", 0)))
        {
            return ((Tier == 1) ? "Unlocks after night 1" : "Unlocks after the first boss");
        }
        foreach (dynamic Res in Gd.Iter(Cost))
        {
            if ((Main.Carried(Me, Res) < Gd.At(Cost, Res)))
            {
                return Gd.Format("Need %d %s", new GArray(Gd.At(Cost, Res), Res));
            }
        }
        return "";
    }

    // World space: the green or red ghost of the structure under the cursor.
    public void DrawBuildPreview()
    {
        dynamic Me = Gd.Get(Gd.Get(Snap, "ps", new GDict()), MyId);
        if ((!Gd.Truth(BuildOpen) || (BuildKind == "") || (Me == null)))
        {
            return;
        }
        Vector2 Spot = this.BuildSpot();
        bool Ok = (this.BuildProblem(Me) == "");
        Color Col = (Ok ? Gd.MakeColor(0.3f, 1.0f, 0.45f, 0.45f) : Gd.MakeColor(1.0f, 0.25f, 0.25f, 0.45f));
        if ((BuildKind == "tower"))
        {
            Gd.Native(this, "draw_circle", Spot, (0.7f * T), Col);
            Gd.Native(this, "draw_arc", Spot, GameSim.TOWER_RANGE, 0, Mathf.Tau, 64, Col, 1.5f);
            return;
        }
        Vector2 Half = (Gd.Truth(BuildVert) ? Gd.MakeVector2((0.5f * T), (1.5f * T)) : Gd.MakeVector2((1.5f * T), (0.5f * T)));
        Gd.Native(this, "draw_rect", Gd.MakeRect2((Spot - Half), (Half * 2)), Col);
    }

    public void DrawBuildMenu(dynamic C, dynamic Vs, dynamic Me)
    {
        if (!Gd.Truth(BuildOpen))
        {
            return;
        }
        Rect2 Box = Gd.MakeRect2((Vs.X - 330), 98, 320, 112);
        this.Panel(C, Box);
        this.Text(C, (Box.Position + Gd.MakeVector2(10, 16)), "BUILD", 10, GOLD, 0, (-1.0f), true);
        int Unlock = Gd.Int(Gd.Get(Snap, "unlock", 0));
        for (int I = 0; I < BUILD_OPTIONS.Count; I++)
        {
            dynamic O = Gd.At(BUILD_OPTIONS, I);
            bool Locked = (Gd.At(O, 3) > Unlock);
            string Cost = Gd.At(O, 2);
            if (((Gd.At(O, 0) == "tower") && Gd.Truth(Gd.Get(Snap, "free_tower", false))))
            {
                Locked = false;
                Cost = "free";
            }
            bool Picked = (Gd.At(O, 0) == BuildKind);
            Color Col = (Locked ? Gd.MakeColor(1, 1, 1, 0.35f) : (Picked ? GOLD : Colors.White));
            this.Text(C, (Box.Position + Gd.MakeVector2(10, (38 + (I * 18)))), Gd.Format("%d  %s - %s%s", new GArray((I + 1), Gd.At(O, 1), Cost, (Locked ? "  LOCKED" : ""))), 11, Col);
        }
        if ((BuildKind != ""))
        {
            string Why = this.BuildProblem(Me);
            this.Text(C, (Box.Position + Gd.MakeVector2(10, 100)), ((Why == "") ? "Left click to place" : Why), 11, ((Why == "") ? Gd.MakeColor(0.4f, 1.0f, 0.5f) : NEON_RED));
        }
    }

    // A built wall: [pos, vertical, hp, maxhp, kind] drawn as a raised block with an HP bar once damaged.
    public void DrawBuiltWall(dynamic W)
    {
        Vector2 Pos = (Vector2)(Gd.At(W, 0));
        Vector2 Half = (Gd.Truth(Gd.At(W, 1)) ? Gd.MakeVector2((0.5f * T), (1.5f * T)) : Gd.MakeVector2((1.5f * T), (0.5f * T)));
        // art/props/wall_<kind>_<h|v>.png from tools/PixelProps.cs
        this.DrawProp(Gd.Format("wall_%s_%s", new GArray(Gd.At(W, 4), (Gd.Truth(Gd.At(W, 1)) ? "v" : "h"))), Pos);
        if ((Gd.At(W, 2) < Gd.At(W, 3)))
        {
            this.Bar((Pos - Gd.MakeVector2(0, (Half.Y + 22))), (Half.X * 1.6f), (Gd.At(W, 2) / Gd.At(W, 3)), GOLD);
        }
    }

    public bool BuildMouse(dynamic Ev)
    {
        if ((!Gd.Truth(BuildOpen) || (BuildKind == "")))
        {
            return false;
        }
        if (((Ev.ButtonIndex == 2) && Gd.Truth(Ev.Pressed)))
        {
            BuildKind = "";
            return true;
        }
        if (((Ev.ButtonIndex == 1) && Gd.Truth(Ev.Pressed)))
        {
            this.Act("build", new GArray(BuildKind, this.BuildSpot(), Gd.Truth(BuildVert)));
        }
        return (Ev.ButtonIndex == 1);
    }
}
