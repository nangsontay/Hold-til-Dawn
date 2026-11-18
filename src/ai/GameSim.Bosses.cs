using Godot;
using Environment = Godot.Environment;
using FileAccess = Godot.FileAccess;
using System;
using System.Threading.Tasks;
using HoldTilDawn;

// Owner: Vu Duc Viet Anh. The seven NOX boss archetypes (AGT-05..11, table 4.2, BAL-36..39, BAL-43, FML-05).
// Every boss walks to its door like a Grunt, slams players near it (BAL-38) and adds one signature move.
// A boss is "busy" while it winds up, dashes, burrows or recovers: then TickBoss moves it instead of TickEnemies.
public partial class GameSim : RefCounted
{
    public const int JUGGERNAUT = 0;
    public const int BROODMOTHER = 1;
    public const int BIO_CANNON = 2;
    public const int BURROWER = 3;
    public const int GATEBREAKER = 4;
    public const int PHANTOM = 5;
    public const int IRONHIDE = 6;

    // [code, name, HP at night 2 for 4 players on Medium, speed x Grunt] (table 4.2, BAL-39)
    public static readonly GArray BOSS_KINDS = new GArray(
        new GArray("NOX-01", "Juggernaut", 3900.0f, 1.0f),
        new GArray("NOX-02", "Broodmother", 4680.0f, 0.75f),
        new GArray("NOX-03", "Bio-Cannon", 3510.0f, 1.0f),
        new GArray("NOX-04", "Burrower", 3120.0f, 1.0f),
        new GArray("NOX-05", "Gatebreaker", 4290.0f, 1.0f),
        new GArray("NOX-06", "Phantom", 3120.0f, 1.0f),
        new GArray("NOX-07", "Ironhide", 2730.0f, 1.0f));

    public const float TELEGRAPH = 1.0f;     // every damaging boss move is shown 1.0 s before it hits (BAL-43)
    public const float SIGNATURE_HIT = 35.0f;
    public const int MAX_SUMMONS = 4;

    // The bosses of nights 2 and 4, drawn when the match starts and announced at dawn of that day (FML-05).
    public dynamic BossPlan = new GArray(0, 1);

    public void DrawBosses()
    {
        int First = Gd.Int((Rng.Randi() % 7));
        int Second = Gd.Int((Rng.Randi() % 6));
        if ((Second >= First))
        {
            Second += 1;
        }
        BossPlan = new GArray(First, Second);
    }

    public int BossKindFor(dynamic Night)
    {
        return Gd.Int(Gd.At(BossPlan, ((Night >= 4) ? 1 : 0)));
    }

    public static string BossTitle(dynamic Kind)
    {
        dynamic B = Gd.At(BOSS_KINDS, Gd.Int(Kind));
        return Gd.Format("%s %s", new GArray(Gd.At(B, 0), Gd.At(B, 1)));
    }

    // Night 4: damage x1.21 and cooldowns x0.9, telegraphs unchanged (section 4.1).
    public float BossDamage(float Base)
    {
        return ((Base * ((K >= 4) ? 1.21f : 1.0f)) * Gd.Float(Gd.At(Gd.At(MODE_MUL, Mode), 2)));
    }

    public float BossCooldown(float Base)
    {
        return (Base * ((K >= 4) ? 0.9f : 1.0f));
    }

    public void SetupBoss(dynamic E, int Kind)
    {
        dynamic B = Gd.At(BOSS_KINDS, Kind);
        E.Type = BOSS;
        E.Kind = Kind;
        // FML-02: HP scales with the team, x1.5625 on night 4, and with the mode
        E.Hp = ((((Gd.At(B, 2) * (1 + (0.8f * (Gd.Size(Players) - 1)))) / 3.4f) * ((K == 4) ? (dynamic)(1.5625f) : 1.0f)) * Gd.At(Gd.At(MODE_MUL, Mode), 1));
        E.Maxhp = E.Hp;
        E.Spd = (E.Spd * Gd.At(B, 3));
        // BAL-37: 60 door damage per 4 s (the Gatebreaker deals it as one 120-point blow every 8 s)
        E.Dmg = this.BossDamage(60.0f);
        E.Slam = this.BossDamage(40.0f);
        E.SlamCd = 3.0f;
        E.SigCd = 4.0f;
        E.Act = "";
        E.ActT = 0.0f;
        E.Face = (Gd.At(DOOR_POS, E.Door) - E.Pos).Angle();
        E.R = (0.9f * T);
        E.DashHit = new GArray();
    }

    // --- damage rules -------------------------------------------------------
    public bool Hittable(dynamic E)
    {
        // a burrowed boss cannot be hit (Burrower, table 4.2)
        return !((E.Type == BOSS) && Gd.Contains(new GArray("burrow", "surface"), Gd.Get(E, "act", "")));
    }

    public dynamic BossDamageTaken(dynamic E, dynamic Dmg, dynamic From)
    {
        if (((E.Type != BOSS) || (Gd.Int(E.Kind) != IRONHIDE) || (Gd.Get(E, "act", "") == "stagger")))
        {
            return Dmg;
        }
        // Ironhide: hits from its front 120 degrees deal 30% damage
        float Off = Mathf.Abs(Mathf.AngleDifference(Gd.Float(E.Face), (From - E.Pos).Angle()));
        return ((Off <= Mathf.DegToRad(60.0f)) ? (dynamic)((Dmg * 0.3f)) : Dmg);
    }

    public void BossOnHit(dynamic E)
    {
        // a hit reveals the Phantom
        if (((E.Type == BOSS) && (Gd.Int(E.Kind) == PHANTOM) && (Gd.Get(E, "act", "") == "cloak")))
        {
            this.BossAct(E, "", 0.0f);
            E.Shown = 3.0f;
        }
    }

    public void BossAct(dynamic E, string Act, float Time)
    {
        E.Act = Act;
        E.ActT = Time;
    }

    // Whom the boss's signature move aims at: the player it retaliates against, else the nearest one it sees.
    public dynamic BossTarget(dynamic E)
    {
        dynamic P = ((E.State == 1) ? (dynamic)(Gd.Get(Players, E.Target)) : null);
        if (((P != null) && (P.State == 0)))
        {
            return P;
        }
        return this.NearestPlayer(E.Pos, (8.0f * T));
    }

    public void AddTelegraph(string Kind, Vector2 Pos, float R, float Dmg, dynamic Extra = null)
    {
        dynamic Tl = new GDict { { "kind", Kind }, { "pos", Pos }, { "r", R }, { "t", TELEGRAPH }, { "dur", TELEGRAPH }, { "dmg", Dmg }, { "end", Pos }, { "ang", 0.0f }, { "spread", Mathf.Tau } };
        if ((Extra != null))
        {
            foreach (dynamic Key in Gd.Iter(Extra))
            {
                Tl[Key] = Gd.At(Extra, Key);
            }
        }
        Gd.Append(Tele, Tl);
    }

    // BAL-38: a 40-damage slam of radius 3 every 6 s when a player is close; the boss stands still while it lands.
    public void BossSlam(dynamic E, dynamic Dt)
    {
        E.SlamCd -= Dt;
        if (((E.SlamCd > 0) || (E.Act != "")))
        {
            return;
        }
        foreach (dynamic P in Gd.Iter(Gd.Values(Players)))
        {
            if (((P.State == 0) && (P.Pos.DistanceTo(E.Pos) < (3 * T))))
            {
                this.StartSlam(E);
                return;
            }
        }
    }

    public void StartSlam(dynamic E)
    {
        this.AddTelegraph("circle", E.Pos, (3 * T), Gd.Float(E.Slam));
        E.SlamCd = this.BossCooldown(6.0f);
        this.BossAct(E, "slam", TELEGRAPH);
    }

    // Runs the boss's current move; returns true while the boss is busy (it then skips walking and door hits).
    public bool TickBoss(dynamic E, dynamic Dt)
    {
        E.SigCd -= Dt;
        E.Shown = (Gd.Get(E, "shown", 0.0f) - Dt);
        int Kind = Gd.Int(E.Kind);
        string Act = E.Act;
        if ((Act == "cloak"))
        {
            return this.TickCloak(E, Dt);
        }
        if ((Act != ""))
        {
            E.ActT -= Dt;
            if ((Act == "charge"))
            {
                return this.TickCharge(E, Dt);
            }
            if ((Act == "burrow"))
            {
                return this.TickBurrow(E, Dt);
            }
            if ((E.ActT > 0))
            {
                return true;
            }
            this.FinishAct(E, Act);
            return (E.Act != "");
        }
        if ((E.SigCd > 0))
        {
            return false;
        }
        dynamic P = this.BossTarget(E);
        float D = Gd.Float(((P != null) ? (dynamic)(P.Pos.DistanceTo(E.Pos)) : float.PositiveInfinity));
        if (((Kind == JUGGERNAUT) && (D >= (3 * T)) && (D <= (10 * T))))
        {
            // charge: 1.0 s line telegraph 1.5 tiles wide, then a dash of up to 10 tiles at 9 tiles/s
            Vector2 Dir = (P.Pos - E.Pos).Normalized();
            E.Dash = Dir;
            E.DashLeft = (10.0f * T);
            E.Face = Dir.Angle();
            E.DashHit = new GArray();
            this.AddTelegraph("line", E.Pos, (0.75f * T), 0.0f, new GDict { { "end", (E.Pos + (Dir * (10.0f * T))) } });
            this.BossAct(E, "charge_wind", TELEGRAPH);
            E.SigCd = this.BossCooldown(8.0f);
            return true;
        }
        if ((Kind == BROODMOTHER))
        {
            this.BossAct(E, "glow", TELEGRAPH);
            E.SigCd = this.BossCooldown(12.0f);
            return true;
        }
        if (((Kind == BIO_CANNON) && (P != null)))
        {
            this.BossAct(E, "glow", TELEGRAPH);
            E.SigCd = this.BossCooldown(5.0f);
            return true;
        }
        if (((Kind == BURROWER) && (P != null)))
        {
            E.Burrow = P.Id;
            this.BossAct(E, "burrow", 3.0f);
            E.SigCd = this.BossCooldown(10.0f);
            return true;
        }
        if ((Kind == PHANTOM))
        {
            E.Burrow = ((P != null) ? (dynamic)(P.Id) : 0);
            this.BossAct(E, "cloak", 4.0f);
            E.SigCd = this.BossCooldown(12.0f);
            return false;
        }
        return false;
    }

    public void FinishAct(dynamic E, string Act)
    {
        int Kind = Gd.Int(E.Kind);
        this.BossAct(E, "", 0.0f);
        if ((Act == "slam"))
        {
            // Juggernaut: 2 s recovery after the slam; Ironhide: 3 s stagger with full damage from every side
            if (((Kind == JUGGERNAUT) && Gd.Truth(Gd.Get(E, "dashed", false))))
            {
                E.Dashed = false;
                this.BossAct(E, "recover", 2.0f);
            }
            else if ((Kind == IRONHIDE))
            {
                this.BossAct(E, "stagger", 3.0f);
            }
        }
        else if ((Act == "charge_wind"))
        {
            this.BossAct(E, "charge", 10.0f);
        }
        else if (((Act == "glow") && (Kind == BROODMOTHER)))
        {
            this.Summon(E);
        }
        else if (((Act == "glow") && (Kind == BIO_CANNON)))
        {
            // a ring of 8 projectiles with 45-degree gaps: 12 damage, 4 tiles/s, range 8 tiles
            for (int I = 0; I < 8; I++)
            {
                Vector2 Dir = Vector2.FromAngle(((I * Mathf.Tau) / 8.0f));
                Gd.Append(Proj, new GDict { { "pos", (E.Pos + (Dir * E.R)) }, { "vel", ((Dir * 4.0f) * T) }, { "dmg", this.BossDamage(12.0f) }, { "ttl", 2.0f }, { "kind", "orb" } });
            }
        }
        else if ((Act == "surface"))
        {
            E.Pos = E.Surface;
            this.BossAct(E, "climb", 1.5f);
        }
        else if ((Act == "ambush"))
        {
            E.Shown = 3.0f;
        }
        else if ((Act == "blow"))
        {
            dynamic Tw = ((E.State == 5) ? (dynamic)(Gd.At(Towers, E.Tw)) : null);
            if (((Tw != null) && (Tw.Hp > 0)))
            {
                this.DamageTower(E, Tw, this.BossDamage(120.0f));
            }
            else if ((E.State != 5))
            {
                this.DamageDoor(E, this.BossDamage(120.0f));
            }
        }
    }

    public bool TickCharge(dynamic E, dynamic Dt)
    {
        Vector2 Old = (Vector2)(E.Pos);
        float Step = Gd.Minf(((9.0f * T) * Dt), E.DashLeft);
        Vector2 Want = (Old + (E.Dash * Step));
        E.Pos = GameSim.PushOut(this.MoveActor(Old, Want, E.R), E.R);
        E.DashLeft -= Step;
        // 35 damage and knockback to anyone on the line, once per dash
        foreach (dynamic P in Gd.Iter(Gd.Values(Players)))
        {
            if (((P.State == 0) && !Gd.Contains(E.DashHit, P.Id) && (P.Pos.DistanceTo(E.Pos) <= (E.R + (0.5f * T)))))
            {
                Gd.Append(E.DashHit, P.Id);
                this.Hurt(P, this.BossDamage(SIGNATURE_HIT));
                this.Knockback(P, E.Pos, (2.0f * T));
            }
        }
        bool Blocked = (E.Pos.DistanceTo(Want) > 2.0f);
        if (((E.DashLeft <= 0) || Blocked))
        {
            E.Dashed = true;
            this.StartSlam(E);
        }
        return true;
    }

    public bool TickBurrow(dynamic E, dynamic Dt)
    {
        dynamic P = Gd.Get(Players, E.Burrow);
        bool Lost = ((P == null) || (P.State != 0));
        if (!Lost)
        {
            // underground it closes in fast and cannot be hit or hit doors
            Vector2 Old = (Vector2)(E.Pos);
            E.Pos = GameSim.PushOut(this.MoveActor(Old, (Old + ((P.Pos - Old).LimitLength((((E.Spd * 2.0f) * Dt))))), E.R), E.R);
        }
        if ((((E.ActT > 0) && !Lost) && (E.Pos.DistanceTo(P.Pos) > (2.5f * T))))
        {
            return true;
        }
        // surfaces at least 1.5 tiles from its target behind a 1.0 s dirt telegraph of radius 2 tiles
        Vector2 Spot = (Vector2)(E.Pos);
        if (!Lost)
        {
            Vector2 Away = ((E.Pos.DistanceTo(P.Pos) > 1.0f) ? (dynamic)((E.Pos - P.Pos).Normalized()) : Vector2.Right);
            Spot = GameSim.PushOut(World.Resolve((P.Pos + (Away * (1.5f * T))), E.R), E.R);
        }
        E.Surface = Spot;
        this.AddTelegraph("circle", Spot, (2.0f * T), this.BossDamage(SIGNATURE_HIT));
        this.BossAct(E, "surface", TELEGRAPH);
        return true;
    }

    public bool TickCloak(dynamic E, dynamic Dt)
    {
        E.ActT -= Dt;
        dynamic P = Gd.Get(Players, E.Burrow);
        if (((P == null) || (P.State != 0)))
        {
            P = this.BossTarget(E);
        }
        if ((P == null))
        {
            if ((E.ActT <= 0))
            {
                this.BossAct(E, "", 0.0f);
            }
            // invisible but still walking to its door; it does not hit doors while cloaked
            return false;
        }
        Vector2 Old = (Vector2)(E.Pos);
        if ((Old.DistanceTo(P.Pos) > (1.5f * T)))
        {
            E.Pos = this.MoveActor(Old, (Old + (((this.EnemyWaypoint(E, P.Pos) - Old).Normalized() * E.Spd) * Dt)), E.R);
            E.Pos = GameSim.PushOut(E.Pos, E.R);
        }
        if (((Old.DistanceTo(P.Pos) <= (2.0f * T)) || (E.ActT <= 0)))
        {
            // the ambush telegraph reveals it: a 1.0 s arc, then 35 damage in a 1.5-tile arc
            E.Face = (P.Pos - E.Pos).Angle();
            this.AddTelegraph("arc", E.Pos, (E.R + (1.5f * T)), this.BossDamage(SIGNATURE_HIT), new GDict { { "ang", E.Face }, { "spread", Mathf.DegToRad(120.0f) } });
            this.BossAct(E, "ambush", TELEGRAPH);
        }
        return true;
    }

    // Broodmother: 2 Grunts of the current night appear beside it (at most 4 alive, no drops, vanish with it).
    public void Summon(dynamic E)
    {
        dynamic Mine = Gd.Filter(Enemies, (Func<dynamic, dynamic>)((X) => Gd.Equal(Gd.Get(X, "owner", (-1)), E.Id)));
        int Room = Math.Min((MAX_SUMMONS - Gd.Size(Mine)), (MAX_ENEMIES - Gd.Size(Enemies)));
        for (int I = 0; I < Math.Min(2, Room); I++)
        {
            dynamic G = this.Spawn(GRUNT, E.Door, 0.0f, false);
            Vector2 Side = Vector2.FromAngle((E.Face + ((I == 0) ? (Mathf.Pi / 2) : (-Mathf.Pi / 2))));
            G.Pos = GameSim.PushOut(World.Resolve((E.Pos + (Side * (E.R + (0.7f * T)))), G.R), G.R);
            G.Owner = E.Id;
            G.Summon = true;
        }
    }

    public void Knockback(dynamic P, Vector2 From, float Dist)
    {
        Vector2 Dir = ((P.Pos.DistanceTo(From) > 1.0f) ? (dynamic)((P.Pos - From).Normalized()) : Vector2.Right);
        Vector2 To = this.MoveActor(P.Pos, (P.Pos + (Dir * Dist)), (0.3f * T));
        if (!Gd.Truth(P.Inside))
        {
            To = GameSim.PushOut(To, (0.4f * T));
        }
        this.Teleport(P, To);
    }

    // Whether this boss hits doors and towers right now (not while burrowed or cloaked).
    public bool BossHitsDoors(dynamic E)
    {
        return !Gd.Contains(new GArray("burrow", "surface", "cloak"), Gd.Get(E, "act", ""));
    }

    // The Gatebreaker's door blow: a 1.0 s wind-up, then 120 damage in one hit, every 8 s.
    public bool StartBlow(dynamic E)
    {
        if (((E.Type != BOSS) || (Gd.Int(E.Kind) != GATEBREAKER)))
        {
            return false;
        }
        this.AddTelegraph("circle", (E.Pos + (Vector2.FromAngle(Gd.Float(E.Face)) * E.R)), (1.0f * T), 0.0f);
        this.BossAct(E, "blow", TELEGRAPH);
        E.Cd = this.BossCooldown(8.0f);
        return true;
    }

    // Boss state for the client: "" or the current move (cloak, burrow, glow, stagger, ...).
    public string BossLook(dynamic E)
    {
        if ((E.Type != BOSS))
        {
            return "";
        }
        string Act = E.Act;
        if (((Act == "") && (Gd.Get(E, "shown", 0.0f) > 0)))
        {
            return "shown";
        }
        return Act;
    }
}
