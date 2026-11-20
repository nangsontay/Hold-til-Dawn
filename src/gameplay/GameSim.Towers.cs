using Godot;
using Environment = Godot.Environment;
using FileAccess = Godot.FileAccess;
using System;
using System.Threading.Tasks;
using HoldTilDawn;

// Owner: Pham Minh Hieu. Defence towers: placing, shooting, light (AGT-12, MEC-11, MEC-28, BAL-28).
public partial class GameSim : RefCounted
{
    public static readonly dynamic TOWER_HP = 300.0f;
    public static readonly dynamic TOWER_DPS = 8.0f;
    public static readonly dynamic TOWER_RANGE = (6.0f * T);
    public static readonly dynamic TOWER_LIGHT = (6.0f * T);
    public static readonly dynamic TOWER_BAND = (Gd.MakeVector2(3.0f, 8.0f) * T);
    public static readonly dynamic TOWER_SIDE = (3.0f * T);
    public static readonly dynamic TOWER_COST = new GDict { { "wood", 10 }, { "titanium", 5 } };

    // --- towers (AGT-12, MEC-11, MEC-28) -------------------------------------
    public static dynamic TowerSlot(dynamic Pos)
    {
        // The door whose tower band (3-8 tiles in front of it, 3 to either side) holds pos, else -1.
        foreach (dynamic I in Gd.Iter(3))
        {
            Vector2 Rel = (Vector2)((Pos - Gd.At(DOOR_POS, I)));
            Vector2 Dd = (Vector2)(Gd.At(DOOR_DIR, I));
            dynamic Along = Rel.Dot(Dd);
            if (((Along >= TOWER_BAND.X) && (Along <= TOWER_BAND.Y) && (Gd.Absf(Rel.Dot(Gd.MakeVector2((-Dd.Y), Dd.X))) <= TOWER_SIDE)))
            {
                return I;
            }
        }
        return (-1);
    }

    public void BuildTower(dynamic P, dynamic At = null)
    {
        Vector2 Spot = (Vector2)((At ?? P.Pos));
        if (((P.Role != 2) || (P.State != 0) || Gd.Truth(P.Inside)))
        {
            this.Note(P, "Only an Engineer can build towers");
            return;
        }
        if ((this.Count(P, "hammer") <= 0))
        {
            this.Note(P, "Building needs the Engineer's hammer");
            return;
        }
        if ((Spot.DistanceTo(P.Pos) > (BUILD_REACH * T)))
        {
            this.Note(P, "Too far away to build there");
            return;
        }
        // placed from the build menu at the cursor: the spot must be free (T places it at the Engineer's feet)
        if (((At != null) && (Gd.Truth((this.World.Resolve(Spot, (0.7f * T)) != Spot)) || Gd.Truth(Gd.Any(Gd.Values(Players), (Func<dynamic, dynamic>)((Q) => (Q.Pos.DistanceTo(Spot) < (1.1f * T))))))))
        {
            this.Note(P, "Something is in the way");
            return;
        }
        dynamic I = GameSim.TowerSlot(Spot);
        if ((I < 0))
        {
            this.Note(P, "Stand 3-8 tiles in front of a door to build a tower");
            return;
        }
        if ((Gd.At(Towers, I) != null))
        {
            this.Note(P, Gd.Format("Door %s already has a tower", Gd.At(DOOR_NAMES, I)));
            return;
        }
        if (Gd.Truth(FreeTower))
        {
            FreeTower = false;
        }
        else
        {
            if ((Unlock < 2))
            {
                this.Note(P, "More towers unlock after the first boss");
                return;
            }
            foreach (dynamic Res in Gd.Iter(TOWER_COST))
            {
                if ((this.Count(P, Res) < Gd.At(TOWER_COST, Res)))
                {
                    this.Note(P, "A tower costs 10 wood + 5 titanium");
                    return;
                }
            }
            foreach (dynamic Res in Gd.Iter(TOWER_COST))
            {
                this.Take(P, Res, Gd.At(TOWER_COST, Res));
            }
        }
        this.PlaceTower(I, Spot);
        this.Note(P, Gd.Format("Tower built at door %s", Gd.At(DOOR_NAMES, I)));
    }

    public void PlaceTower(dynamic Door, dynamic Pos)
    {
        Towers[Door] = new GDict { { "door", Door }, { "pos", Pos }, { "hp", TOWER_HP }, { "wrecked", false }, { "pause", 0.0f }, { "target", null } };
    }

    public void TickTowers(dynamic Dt)
    {
        // A standing tower shoots the nearest enemy within 6 tiles at 8 DPS, day or night.
        foreach (dynamic Tw in Gd.Iter(Towers))
        {
            if ((Tw == null))
            {
                continue;
            }
            Tw.Pause -= Dt;
            Tw.Target = null;
            if ((Tw.Hp <= 0))
            {
                continue;
            }
            dynamic Best = null;
            dynamic Bd = TOWER_RANGE;
            foreach (dynamic E in Gd.Iter(Enemies))
            {
                float D = Gd.Float(E.Pos.DistanceTo(Tw.Pos));
                if (((E.Hp > 0) && (D <= Bd) && Gd.Truth(this.LineClear(Tw.Pos, E.Pos, Tw.Pos))))
                {
                    Bd = D;
                    Best = E;
                }
            }
            if ((Best != null))
            {
                Tw.Target = Best.Pos;
                Best.Hp -= (TOWER_DPS * Dt);
                Best.Nodmg = 0.0f;
                if ((Best.Hp <= 0))
                {
                    this.Drop(Best);
                    if ((Best.Type == BOSS))
                    {
                        BossDead = true;
                    }
                }
            }
        }
        Enemies = Gd.Filter(Enemies, (Func<dynamic, dynamic>)((E) => (E.Hp > 0)));
    }

    public dynamic TowerInLight(dynamic Pos)
    {
        foreach (dynamic Tw in Gd.Iter(Towers))
        {
            if (((Tw != null) && (Tw.Hp > 0) && (Pos.DistanceTo(Tw.Pos) <= TOWER_LIGHT)))
            {
                return Tw;
            }
        }
        return null;
    }
}
