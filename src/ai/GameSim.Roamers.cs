using Godot;
using Environment = Godot.Environment;
using FileAccess = Godot.FileAccess;
using System;
using System.Threading.Tasks;
using HoldTilDawn;

// Owner: Vu Duc Viet Anh. Daytime roamers (MEC-26, FML-16, BAL-44).
public partial class GameSim : RefCounted
{
    public static float ROAM_MUL = AiSettings.Current.RoamerMultiplier;
    public static readonly dynamic ROAM_CHECK = 15.0f;
    public static readonly dynamic ROAM_D0 = 20.0f;
    public static readonly dynamic ROAM_D1 = 60.0f;
    public static readonly dynamic ROAM_PMAX = 0.3f;
    public static readonly dynamic ROAM_MAX = 8;
    public static readonly dynamic ROAM_SIGHT = (4.0f * T);
    public static readonly dynamic VIEW_HALF = (Gd.MakeVector2(15.0f, 8.5f) * T);

    public void TickRoamers(dynamic Dt)
    {
        foreach (dynamic P in Gd.Iter(Gd.Values(Players)))
        {
            P.RoamT -= Dt;
            if ((P.RoamT > 0))
            {
                continue;
            }
            P.RoamT = ROAM_CHECK;
            if (((P.State != 0) || Gd.Truth(GameSim.InsideBase(P.Pos)) || (this.RoamerCount() >= ROAM_MAX) || (Gd.Size(Enemies) > (MAX_ENEMIES - 2))))
            {
                continue;
            }
            if ((Rng.Randf() < (GameSim.RoamChance(P.Pos) * ROAM_MUL)))
            {
                this.SpawnRoamers(P);
            }
        }
    }

    public static dynamic RoamChance(dynamic Pos)
    {
        // tiles from the nearest base wall (FML-16)
        dynamic D = (GameSim.WallDist(Pos) / T);
        return (ROAM_PMAX * Gd.Clampf(((D - ROAM_D0) / (ROAM_D1 - ROAM_D0)), 0.0f, 1.0f));
    }

    public void SpawnRoamers(dynamic P)
    {
        // Just outside this player's camera view, on the side away from the base.
        Vector2 Away = (Vector2)(P.Pos.Normalized());
        dynamic Side = Gd.MakeVector2((-Away.Y), Away.X);
        Vector2 At = (Vector2)(((P.Pos + (Away * (VIEW_HALF.X + (2 * T)))) + ((Side * Rng.RandfRange((-4.0f), 4.0f)) * T)));
        if (!IslandMap.OnLand(At, (2 * T)))
        {
            At = (P.Pos + ((Side * (VIEW_HALF.X + (2 * T))) * ((Rng.Randf() < 0.5f) ? (dynamic)(1.0f) : (-1.0f))));
        }
        At = IslandMap.PullInland(At, (2 * T));
        foreach (dynamic Q in Gd.Iter(Gd.Values(Players)))
        {
            if (((Gd.Absf((Q.Pos.X - At.X)) < (VIEW_HALF.X + T)) && (Gd.Absf((Q.Pos.Y - At.Y)) < (VIEW_HALF.Y + T))))
            {
                return;
            }
        }
        foreach (dynamic I in Gd.Iter(2))
        {
            int Ty = Gd.Int((((K >= 4) && (I == 1) && (Rng.Randf() < 0.5f)) ? (dynamic)(SPITTER) : GRUNT));
            // stats of night k on day k
            dynamic E = this.Spawn(Ty, this.NearestActiveDoor(At), 0.0f);
            E.Pos = (At + Gd.MakeVector2(((I * 1.3f) * T), 0));
            E.Home = At;
            E.Wander = E.Pos;
            E.State = 4;
        }
    }

    public dynamic RoamerCount()
    {
        return Gd.Size(Gd.Filter(Enemies, (Func<dynamic, dynamic>)((E) => Gd.Has(E, "home"))));
    }

    public void Wander(dynamic E, dynamic Dt)
    {
        if ((E.Pos.DistanceTo(E.Wander) < 4.0f))
        {
            E.Wander = (E.Home + ((Vector2.FromAngle((Rng.Randf() * Mathf.Tau)) * Rng.RandfRange(0.0f, 3.0f)) * T));
        }
        E.Pos += ((this.EnemyWaypoint(E, E.Wander) - E.Pos).Normalized() * Gd.Minf(((E.Spd * 0.4f) * Dt), E.Pos.DistanceTo(E.Wander)));
        E.Pos = GameSim.PushOut(E.Pos, E.R);
    }
}
