using Godot;
using Environment = Godot.Environment;
using FileAccess = Godot.FileAccess;
using System;
using System.Threading.Tasks;
using HoldTilDawn;

// Owner: Pham Minh Hieu. Engineer construction from the build menu (B): wood and stone walls (ITM-07, ITM-11,
// MEC-11, BAL-27). A wall is 3 tiles long and 1 tile thick; enemies whose way it blocks attack it.
public partial class GameSim : RefCounted
{
    // kind -> [name, cost, unlock tier, HP]
    public static readonly GDict WALL_KINDS = new GDict {
        { "wood", new GArray("Wood wall", new GDict { { "wood", 5 } }, 0, 200.0f) },
        { "stone", new GArray("Stone wall", new GDict { { "stone", 5 } }, 1, 400.0f) },
    };
    public const float BUILD_REACH = 5.0f;  // tiles from the Engineer
    public const float WALL_DOOR_GAP = 3.0f;  // BAL-27: no wall within 3 tiles of a door
    public const int MAX_WALLS = 30;

    public dynamic BuiltWalls = new GArray();

    // Why a structure cannot go there, or "" when it can (shared by the server and the client preview).
    public static string WallSpotProblem(dynamic Pos, bool Vert, dynamic World, dynamic Walls)
    {
        foreach (dynamic Piece in Gd.Iter(WorldGeometry.WallPieces(Pos, Vert)))
        {
            if (Gd.Truth(GameSim.InsideBase(Piece, (0.6f * T))))
            {
                return "Walls go outside the base";
            }
            foreach (dynamic I in Gd.Iter(3))
            {
                if ((Piece.DistanceTo(Gd.At(DOOR_POS, I)) < (WALL_DOOR_GAP * T)))
                {
                    return "Too close to a door (3 tiles)";
                }
            }
            if (!IslandMap.OnGrass((Vector2)Piece, T))
            {
                return "Too close to the shore";
            }
            if ((World.Resolve(Piece, (0.5f * T)) != Piece))
            {
                return "Something is in the way";
            }
        }
        return "";
    }

    public static Vector2 SnapToTile(Vector2 Pos)
    {
        return ((Pos / T).Round() * T);
    }

    // Action "build": [kind, position, vertical]; kind is "wood", "stone" or "tower".
    public void Build(dynamic P, dynamic Arg)
    {
        if ((!Gd.IsType(Arg, typeof(GArray)) || (Gd.Size(Arg) < 3) || !Gd.IsType(Gd.At(Arg, 1), typeof(Vector2))))
        {
            return;
        }
        string Kind = Gd.Str(Gd.At(Arg, 0));
        Vector2 Pos = (Vector2)(Gd.At(Arg, 1));
        if ((Kind == "tower"))
        {
            this.BuildTower(P, Pos);
            return;
        }
        if (!Gd.Truth(WALL_KINDS.Has(Kind)))
        {
            return;
        }
        dynamic W = WALL_KINDS[Kind];
        if (((P.Role != 2) || (P.State != 0) || Gd.Truth(P.Inside)))
        {
            this.Note(P, "Engineer only, outside the base");
            return;
        }
        if ((this.Count(P, "hammer") <= 0))
        {
            this.Note(P, "Needs the hammer");
            return;
        }
        if ((Gd.At(W, 2) > Unlock))
        {
            this.Note(P, Gd.Format("%s unlocks after night 1", Gd.At(W, 0)));
            return;
        }
        Pos = GameSim.SnapToTile(Pos);
        if ((Pos.DistanceTo(P.Pos) > (BUILD_REACH * T)))
        {
            this.Note(P, "Too far away to build there");
            return;
        }
        if ((Gd.Size(BuiltWalls) >= MAX_WALLS))
        {
            this.Note(P, Gd.Format("At most %d walls", MAX_WALLS));
            return;
        }
        bool Vert = Gd.Truth(Gd.At(Arg, 2));
        string Why = GameSim.WallSpotProblem(Pos, Vert, World, BuiltWalls);
        if ((Why == "") && this.PlayerOnWall(Pos, Vert))
        {
            Why = "Someone is standing there";
        }
        if ((Why != ""))
        {
            this.Note(P, Why);
            return;
        }
        foreach (dynamic Res in Gd.Iter(Gd.At(W, 1)))
        {
            if ((this.Count(P, Res) < Gd.At(Gd.At(W, 1), Res)))
            {
                this.Note(P, Gd.Format("Need more %s", Res));
                return;
            }
        }
        foreach (dynamic Res in Gd.Iter(Gd.At(W, 1)))
        {
            this.Take(P, Res, Gd.At(Gd.At(W, 1), Res));
        }
        // MEC-11: the structure appears at once with full HP
        Gd.Append(BuiltWalls, new GDict { { "id", NextId }, { "kind", Kind }, { "pos", Pos }, { "vert", Vert }, { "hp", Gd.At(W, 3) }, { "maxhp", Gd.At(W, 3) } });
        NextId += 1;
        World.Rebuild(Nodes, Towers, BuiltWalls);
        this.Note(P, ("Built a " + Gd.At(W, 0)));
    }

    public bool PlayerOnWall(Vector2 Pos, bool Vert)
    {
        foreach (dynamic Piece in Gd.Iter(WorldGeometry.WallPieces(Pos, Vert)))
        {
            foreach (dynamic Q in Gd.Iter(Gd.Values(Players)))
            {
                if ((Q.Pos.DistanceTo(Piece) < (0.9f * T)))
                {
                    return true;
                }
            }
        }
        return false;
    }

    // The wall an enemy is pressed against, if any.
    public dynamic WallTouching(dynamic Pos, dynamic R)
    {
        foreach (dynamic W in Gd.Iter(BuiltWalls))
        {
            foreach (dynamic Piece in Gd.Iter(WorldGeometry.WallPieces(W.Pos, W.Vert)))
            {
                if ((Pos.DistanceTo(Piece) <= ((R + (WorldGeometry.WALL_PIECE * T)) + 4.0f)))
                {
                    return W;
                }
            }
        }
        return null;
    }

    public void DamageWall(dynamic E, dynamic W, dynamic Dmg)
    {
        W.Hp -= (Dmg * ((E.Type == WRECKER) ? (dynamic)(2.0f) : 1.0f));
        if ((W.Hp <= 0))
        {
            BuiltWalls = Gd.Filter(BuiltWalls, (Func<dynamic, dynamic>)((X) => (X.Id != W.Id)));
            this.Say(Gd.Format("A %s was destroyed", Gd.At(WALL_KINDS[W.Kind], 0).ToLower()));
        }
    }
}
