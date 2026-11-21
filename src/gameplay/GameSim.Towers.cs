using Godot;
using Environment = Godot.Environment;
using FileAccess = Godot.FileAccess;
using System;
using System.Threading.Tasks;
using HoldTilDawn;

// Owner: Pham Minh Hieu. Defence towers: placing, shooting, light (AGT-12, MEC-11, MEC-28, BAL-28).
// Team change: a tower goes anywhere outside the base except on a landing pad (was 3-8 tiles in front of a door).
public partial class GameSim : RefCounted
{
    public static readonly dynamic TOWER_HP = 300.0f;
    public static readonly dynamic TOWER_DPS = 8.0f;
    public static readonly dynamic TOWER_RANGE = (6.0f * T);
    public static readonly dynamic TOWER_LIGHT = (6.0f * T);
    public static readonly dynamic TOWER_COST = new GDict { { "wood", 10 }, { "titanium", 5 } };
    // the team has this many tower slots (a wrecked tower keeps its slot and can be repaired)
    public const int MAX_TOWERS = 3;
    // the tower's footprint (its collider, WorldGeometry.DynamicColliders)
    public const float TOWER_R = 0.7f;

    // --- towers (AGT-12, MEC-11, MEC-28) -------------------------------------
    // A tower goes anywhere outside the base on open land, except on a helicopter landing pad (FML-13) and
    // right in front of a door. Returns "" when the spot is fine, else what is wrong.
    public static string TowerSpotProblem(Vector2 Pos, dynamic World)
    {
        if (Gd.Truth(GameSim.InsideBase(Pos, (TOWER_R * T))))
        {
            return "Towers go outside the base";
        }
        foreach (dynamic I in Gd.Iter(3))
        {
            if ((Pos.DistanceTo((Vector2)GameSim.PadPos(I)) < ((float)PAD_R + (TOWER_R * T))))
            {
                return "Keep the landing pads clear";
            }
            if ((Pos.DistanceTo((Vector2)Gd.At(DOOR_POS, I)) < (WALL_DOOR_GAP * T)))
            {
                return "Too close to a door (3 tiles)";
            }
        }
        if (!IslandMap.OnLand(Pos, (TOWER_R * T)))
        {
            return "Too close to the water";
        }
        if (World is WorldGeometry Ground && Ground.TerrainAt(Pos, TOWER_R * T) == 1) return "Needs dry land for the dirt foundation";
        if (((World != null) && ((Vector2)World.Resolve(Pos, (TOWER_R * T)) != Pos)))
        {
            return "Something is in the way";
        }
        return "";
    }

    // The first free tower slot, or -1 when all MAX_TOWERS are taken.
    public int FreeTowerSlot()
    {
        for (int I = 0; I < MAX_TOWERS; I++)
        {
            if ((Gd.At(Towers, I) == null) && !ConstructionSlotTaken(I))
            {
                return I;
            }
        }
        return (-1);
    }

    // Where the unplaced free tower appears at dusk: in front of the door, beside its landing pad.
    public Vector2 FreeTowerSpot(int Door)
    {
        Vector2 Dp = (Vector2)Gd.At(DOOR_POS, Door);
        Vector2 Dd = (Vector2)Gd.At(DOOR_DIR, Door);
        Vector2 Side = new Vector2((-Dd.Y), Dd.X);
        float Out = (float)PAD_DIST;
        float Off = ((float)PAD_R + ((TOWER_R + 1.0f) * T));
        foreach (float Grow in new[] { 0.0f, 1.0f, 2.0f, 3.0f, 4.0f })
        {
            foreach (float Sgn in new[] { 1.0f, -1.0f })
            {
                foreach (float Along in new[] { 0.0f, -1.5f, 1.5f })
                {
                    Vector2 P = ((Dp + (Dd * (Out + (Along * T)))) + (Side * (Sgn * (Off + (Grow * T)))));
                    if ((GameSim.TowerSpotProblem(P, this.World) == ""))
                    {
                        return P;
                    }
                }
            }
        }
        // nothing free beside the pad: just past its far edge
        return ((Dp + (Dd * (Out + (float)PAD_R + ((TOWER_R + 1.0f) * T)))));
    }

    public void BuildTower(dynamic P, dynamic At = null)
    {
        Vector2 Spot = (Vector2)((At ?? P.Pos));
        if (!Spot.IsFinite() || !Gd.Contains(new GArray("day", "dusk", "night"), Phase)) return;
        if (P.CraftIdx >= 0 || P.Channel > 0 || Gd.Get(P, "repair_kit_t", 0f) > 0) { Note(P, "Finish your current action first"); return; }
        if (((P.Role != 2) || (P.State != 0) || Gd.Truth(P.Inside)))
        {
            this.Note(P, "Engineer only");
            return;
        }
        if ((this.Count(P, "hammer") <= 0))
        {
            this.Note(P, "Needs the hammer");
            return;
        }
        if ((Spot.DistanceTo(P.Pos) > (BUILD_REACH * T)))
        {
            this.Note(P, "Too far away to build there");
            return;
        }
        // T places it at the Engineer's feet (they stand there already); from the build menu the spot must be free of people too
        string Why = GameSim.TowerSpotProblem(Spot, ((At != null) ? this.World : null));
        if (((Why == "") && (At != null) && Gd.Truth(Gd.Any(Gd.Values(Players), (Func<dynamic, dynamic>)((Q) => (Q.Pos.DistanceTo(Spot) < (1.1f * T)))))))
        {
            Why = "Someone is standing there";
        }
        if (((Why == "") && Gd.Truth(Gd.Any(Towers, (Func<dynamic, dynamic>)((Tw) => ((Tw != null) && (((Vector2)Tw.Pos).DistanceTo(Spot) < ((2.0f * TOWER_R) * T))))))))
        {
            Why = "Something is in the way";
        }
        if ((Why != ""))
        {
            this.Note(P, Why);
            return;
        }
        int I = this.FreeTowerSlot();
        if ((I < 0))
        {
            this.Note(P, Gd.Format("The team already has %d towers", MAX_TOWERS));
            return;
        }
        if (!Gd.Truth(FreeTower))
        {
            if (Unlock < 2) { Note(P, "Unlocks after the first boss"); return; }
            foreach (string Res in (GDict)TOWER_COST)
                if (Count(P, Res) < Gd.At(TOWER_COST, Res)) { Note(P, "A tower costs 10 wood + 5 titanium"); return; }
        }
        if (!ReserveTower(P, I, Spot)) Note(P, "Tower foundation is already reserved");
    }

    public void PlaceTower(dynamic Slot, dynamic Pos)
    {
        Towers[Slot] = new GDict { { "slot", Slot }, { "pos", Pos }, { "hp", TOWER_HP }, { "wrecked", false }, { "pause", 0.0f }, { "target", null } };
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
