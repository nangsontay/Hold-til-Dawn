using Godot;
using Environment = Godot.Environment;
using FileAccess = Godot.FileAccess;
using System;
using System.Threading.Tasks;
using HoldTilDawn;

// Owner: Vu Duc Viet Anh. Wave director, enemy state machine, boss, projectiles (MEC-19/20, FML-01..05, BAL-29..39).
// AI decides targets and movement; damage to players goes through Hurt() in src/gameplay.
public partial class GameSim : RefCounted
{
    public static readonly dynamic MAX_ENEMIES = 60;
    public static float SPAWN_MUL = AiSettings.Current.SpawnMultiplier;
    public static readonly dynamic SPAWN_ALL_SIDES = false;  // GDD 4.3 / BAL-34: waves spawn 25-35 tiles out in a band in front of each active door
    public static float ENEMY_SPEED_MUL = AiSettings.Current.EnemySpeed;
    public static float FIRST_WAVE_DELAY = AiSettings.Current.FirstWave;
    public static float WAVE_INTERVAL = AiSettings.Current.WaveInterval;
    public static float FINAL_WAVE_INTERVAL = AiSettings.Current.FinalWaveInterval;
    public static readonly dynamic WAVE_MIN_DISTANCE = 8.0f;
    public const int GRUNT = 0;
    public const int SPITTER = 1;
    public const int WRECKER = 2;
    public const int BOSS = 3;
    public static readonly dynamic ENEMY = new GArray(new GArray(60.0f, 10.0f, 2.0f, 10, 1), new GArray(40.0f, 8.0f, 2.0f, 15, 3), new GArray(100.0f, 10.0f, 1.6f, 20, 4));  // hp, dmg, speed tiles/s, cost, first night (BAL-29..31)

    // --- enemies ------------------------------------------------------------
    public void TickWaves()
    {
        if ((Elapsed >= NextWave))
        {
            Wave += 1;
            NextWave += ((K == 5) ? (dynamic)(FINAL_WAVE_INTERVAL) : WAVE_INTERVAL);
            this.SpawnWave();
        }
        if ((((K == 2) || (K == 4)) && !(Gd.Truth(BossSpawned)) && (Elapsed >= 300.0f)))
        {
            BossSpawned = true;
            this.SpawnBoss();
        }
    }

    public void SpawnWave()
    {
        dynamic N = Gd.Size(Players);
        dynamic Wf = ((K == 5) ? (dynamic)(1.0f) : (1.0f + (0.15f * Gd.Mini((Wave - 1), 5))));
        float Budget = Gd.Float(((((((((40.0f * (1 + (0.5f * (N - 1)))) * Gd.Pow(1.2f, (K - 1))) * Wf) * ((K == 5) ? (dynamic)(1.5f) : 1.0f)) * ((this.Boss() != null) ? (dynamic)((1.0f / 3.0f)) : 1.0f)) * Gd.At(Gd.At(MODE_MUL, Mode), 0)) * SPAWN_MUL) + Carry));
        dynamic Types = Gd.Filter(new GArray(0, 1, 2), (Func<dynamic, dynamic>)((Ty) => (Gd.At(Gd.At(ENEMY, Ty), 4) <= K)));
        dynamic I = 0;
        while ((Gd.Size(Enemies) < MAX_ENEMIES))
        {
            dynamic Can = Gd.Filter(Types, (Func<dynamic, dynamic>)((Ty) => (Gd.At(Gd.At(ENEMY, Ty), 3) <= Budget)));
            if (Gd.Truth(Gd.IsEmpty(Can)))
            {
                break;
            }
            int Ty = Gd.Int(Gd.At(Can, (Rng.Randi() % Gd.Size(Can))));
            Budget -= Gd.At(Gd.At(ENEMY, Ty), 3);
            dynamic Dist = (Gd.Truth(SPAWN_ALL_SIDES) ? (dynamic)(Rng.RandfRange(WAVE_MIN_DISTANCE, (((ISLAND - Gd.Length(HALF)) / T) - 2.0f))) : Rng.RandfRange(25.0f, 35.0f));
            dynamic E = this.Spawn(Ty, Gd.At(Active, (I % Gd.Size(Active))), Dist);
            if (Gd.Truth(SPAWN_ALL_SIDES))
            {
                E.Door = this.NearestActiveDoor(E.Pos);
            }
            I += 1;
        }
        Carry = ((Budget < 10) ? (dynamic)(Budget) : 0.0f);
    }

    public dynamic Spawn(dynamic Ty, dynamic Door, dynamic DistTiles, dynamic AnySide = null)
    {
        AnySide ??= SPAWN_ALL_SIDES;
        dynamic S = Gd.At(ENEMY, Ty);
        dynamic G = Gd.Pow(1.1f, (K - 1));
        dynamic E = new GDict { { "id", NextId }, { "type", Ty }, { "pos", this.SpawnPos(Door, DistTiles, AnySide) }, { "hp", ((Gd.At(S, 0) * G) * Gd.At(Gd.At(MODE_MUL, Mode), 1)) }, { "dmg", ((Gd.At(S, 1) * G) * Gd.At(Gd.At(MODE_MUL, Mode), 2)) }, { "spd", ((Gd.Minf(3.0f, (Gd.At(S, 2) * Gd.Pow(1.08f, (K - 1)))) * T) * ENEMY_SPEED_MUL) }, { "door", Door }, { "state", 0 }, { "cd", 0.0f }, { "hits", new GArray() }, { "target", 0 }, { "anchor", Vector2.Zero }, { "nodmg", 0.0f }, { "r", (((Ty == WRECKER) ? (dynamic)(0.625f) : 0.5f) * T) } };
        E.Pos = World.Resolve(E.Pos, E.R);
        E.Maxhp = E.Hp;
        NextId += 1;
        Gd.Append(Enemies, E);
        return E;
    }

    public void SpawnBoss()
    {
        int Door = Gd.Int(Gd.At(Active, (Rng.Randi() % Gd.Size(Active))));
        dynamic E = this.Spawn(GRUNT, Door, 60.0f, false);
        // BAL-34: 60 tiles in front of its door; BAL-39: a Grunt's speed of this night
        E.Pos = (Gd.At(DOOR_POS, Door) + ((Gd.At(DOOR_DIR, Door) * 60.0f) * T)).LimitLength((ISLAND - (2 * T)));
        this.SetupBoss(E, this.BossKindFor(K));
        this.Say(Gd.Format("%s is heading for door %s!", new GArray(GameSim.BossTitle(E.Kind).ToUpper(), Gd.At(DOOR_NAMES, Door))));
    }

    public dynamic Boss()
    {
        foreach (dynamic E in Gd.Iter(Enemies))
        {
            if ((E.Type == BOSS))
            {
                return E;
            }
        }
        return null;
    }

    public void TickEnemies(dynamic Dt)
    {
        // a copy: a Broodmother's summons join the list during the loop
        foreach (dynamic E in Gd.Iter(Gd.Duplicate(Enemies)))
        {
            Vector2 OldPos = (Vector2)(E.Pos);
            E.NavT = (Gd.Get(E, "nav_t", 0.0f) - Dt);
            E.Cd -= Dt;
            E.Nodmg += Dt;
            if (((E.Type == BOSS) && this.TickBoss(E, Dt)))
            {
                continue;
            }
            // a Broodmother's summons vanish with it (BAL-43)
            if ((Gd.Truth(Gd.Get(E, "summon", false)) && !Gd.Truth(Gd.Any(Enemies, (Func<dynamic, dynamic>)((X) => (Gd.Equal(X.Id, E.Owner) && (X.Hp > 0)))))))
            {
                E.Hp = 0.0f;
                continue;
            }
            if ((E.Type == SPITTER))
            {
                this.TickSpitter(E, Dt);
                E.Pos = this.MoveActor(OldPos, E.Pos, E.R);
                continue;
            }
            if ((E.State == 4))
            {
                dynamic Seen = this.NearestPlayer(E.Pos, ROAM_SIGHT);
                if ((Seen == null))
                {
                    this.Wander(E, Dt);
                    E.Pos = this.MoveActor(OldPos, E.Pos, E.R);
                    continue;
                }
                E.State = 1;
                E.Target = Seen.Id;
                E.Anchor = E.Pos;
                E.Nodmg = 0.0f;
            }
            dynamic TargetP = null;
            E.Calm = (Gd.Get(E, "calm", 0.0f) - Dt);
            if ((E.State == 1))
            {
                // FML-09: re-evaluate the top threat every 0.25 s
                E.RetargetT = (Gd.Get(E, "retarget_t", 0.0f) - Dt);
                if ((E.RetargetT <= 0))
                {
                    E.RetargetT = 0.25f;
                    this.Retarget(E);
                }
                TargetP = Gd.Get(Players, E.Target);
                if (((TargetP == null) || (TargetP.State != 0) || (Gd.Truth(TargetP.Inside) && !(Gd.Truth(this.AnyBreach()))) || (E.Nodmg > 5.0f) || (E.Pos.DistanceTo(E.Anchor) > (12 * T))))
                {
                    // roamers go back to wandering by day; a boss ignores new threat for 10 s (BAL-35)
                    E.State = (Gd.Truth(Gd.Has(E, "home")) ? (dynamic)(4) : 0);
                    Gd.Clear(E.Hits);
                    if ((E.Type == BOSS))
                    {
                        E.Calm = 10.0f;
                    }
                    TargetP = null;
                    if ((E.State == 4))
                    {
                        continue;
                    }
                }
            }
            if (((E.State == 0) && (Phase == "night")))
            {
                dynamic Lit = this.TowerInLight(E.Pos);
                if ((Lit != null))
                {
                    E.State = 5;
                    E.Tw = Lit.Door;
                }
            }
            if ((E.State == 5))
            {
                dynamic Tw = Gd.At(Towers, E.Tw);
                if (((Tw == null) || (Tw.Hp <= 0)))
                {
                    E.State = 0;
                }
                else
                {
                    if ((E.Pos.DistanceTo(Tw.Pos) > ((E.R + (0.7f * T)) + 2.0f)))
                    {
                        E.Pos += ((this.EnemyWaypoint(E, Tw.Pos) - E.Pos).Normalized() * Gd.Minf((E.Spd * Dt), E.Pos.DistanceTo(Tw.Pos)));
                    }
                    else if ((E.Cd <= 0))
                    {
                        this.TowerAttack(E, Tw);
                    }
                    if ((E.Type == BOSS))
                    {
                        this.BossSlam(E, Dt);
                    }
                    E.Pos = GameSim.PushOut(this.MoveActor(OldPos, E.Pos, E.R), E.R);
                    continue;
                }
            }
            if ((((E.State == 0) || (E.State == 2)) && (Gd.At(Doors, E.Door) <= 0)))
            {
                E.State = 3;
            }
            Vector2 DoorPos = (Vector2)(Gd.At(DOOR_POS, E.Door));
            // anywhere along the door gap
            bool NearDoor = Gd.Truth((E.Pos.DistanceTo(DoorPos) <= (E.R + T)));
            Vector2 Goal = (Vector2)((DoorPos + (Gd.At(DOOR_DIR, E.Door) * (E.R + (0.2f * T)))));
            dynamic Arrived = NearDoor;
            dynamic Outside = true;
            if ((E.State == 1))
            {
                Goal = TargetP.Pos;
                Arrived = (E.Pos.DistanceTo(Goal) <= (E.R + (0.4f * T)));
            }
            else if (((E.State == 3) && (Gd.Truth(GameSim.InsideBase(E.Pos, (E.R + 2))) || Gd.Truth(NearDoor))))
            {
                // through the broken door to the generator
                Goal = Vector2.Zero;
                Arrived = (Gd.Length(E.Pos) <= (2.5f * T));
                Outside = false;
            }
            if (!(Gd.Truth(Arrived)))
            {
                dynamic Way = this.EnemyWaypoint(E, Goal, Outside);
                E.Pos += ((Way - E.Pos).Normalized() * Gd.Minf((E.Spd * Dt), E.Pos.DistanceTo(Goal)));
            }
            else if ((E.Cd <= 0))
            {
                this.EnemyAttack(E, TargetP);
            }
            if ((E.Type == BOSS))
            {
                this.BossSlam(E, Dt);
                if ((E.Pos.DistanceTo(OldPos) > 0.5f))
                {
                    E.Face = (E.Pos - OldPos).Angle();
                }
            }
            E.Pos = this.MoveActor(OldPos, E.Pos, E.R);
            if (Gd.Truth(Outside))
            {
                E.Pos = GameSim.PushOut(E.Pos, E.R);
            }
            this.TickBlocked(E, OldPos, Arrived, Dt);
        }
        Enemies = Gd.Filter(Enemies, (Func<dynamic, dynamic>)((E) => (E.Hp > 0)));
        this.Separate();
    }

    public void EnemyAttack(dynamic E, dynamic P)
    {
        bool Boss = Gd.Truth((E.Type == BOSS));
        if ((E.State == 1))
        {
            // a boss hurts players only with its slam and signature move
            if (!(Gd.Truth(Boss)))
            {
                if (Gd.Truth(this.LineClear(E.Pos, P.Pos)))
                {
                    this.Hurt(P, E.Dmg);
                }
            }
            E.Cd = 2.0f;
            return;
        }
        if ((Boss && (!this.BossHitsDoors(E) || this.StartBlow(E))))
        {
            return;
        }
        this.DamageDoor(E, E.Dmg);
        E.Cd = (Gd.Truth(Boss) ? (dynamic)(4.0f) : 2.0f);
    }

    // FML-07: a hit on an intact door also hurts the generator, more as the door degrades; through a broken
    // door the damage goes to the generator in full. Wreckers deal double damage to doors and towers.
    public void DamageDoor(dynamic E, dynamic Dmg)
    {
        float D = Gd.Float((Dmg * ((E.Type == WRECKER) ? (dynamic)(2.0f) : 1.0f)));
        if ((E.State == 3))
        {
            BaseHp -= D;
        }
        else
        {
            float X = Gd.Float((1.0f - (Gd.At(Doors, E.Door) / DOOR_HP)));
            Doors[E.Door] = Gd.Maxf(0.0f, (Gd.At(Doors, E.Door) - D));
            BaseHp -= ((0.3f * D) * (1.0f + X));
            DoorPause[E.Door] = 2.0f;
            E.State = 2;
            if (((Gd.At(Doors, E.Door) <= 0) && !(Gd.Truth(Gd.At(Broken, E.Door)))))
            {
                Broken[E.Door] = true;
                this.Say(Gd.Format("Door %s broken - BREACH!", Gd.At(DOOR_NAMES, E.Door)));
            }
        }
        // the result screen names the door where the generator took its last hit
        LastDoor = E.Door;
        BaseHp = Gd.Maxf(0.0f, BaseHp);
    }

    // MEC-11: an enemy that cannot get past a built wall for a second attacks it (as it would a door).
    public void TickBlocked(dynamic E, Vector2 OldPos, dynamic Arrived, dynamic Dt)
    {
        bool Stuck = (!Gd.Truth(Arrived) && (E.Pos.DistanceTo(OldPos) < Gd.Maxf(((E.Spd * Dt) * 0.3f), 0.2f)));
        E.StuckT = (Stuck ? (dynamic)((Gd.Get(E, "stuck_t", 0.0f) + Dt)) : 0.0f);
        if (((E.StuckT < 1.0f) || (E.Cd > 0)))
        {
            return;
        }
        dynamic W = this.WallTouching(E.Pos, E.R);
        if ((W != null))
        {
            this.DamageWall(E, W, E.Dmg);
            E.Cd = ((E.Type == BOSS) ? (dynamic)(4.0f) : 2.0f);
        }
    }

    public void TowerAttack(dynamic E, dynamic Tw)
    {
        if (((E.Type == BOSS) && (!this.BossHitsDoors(E) || this.StartBlow(E))))
        {
            return;
        }
        this.DamageTower(E, Tw, E.Dmg);
        E.Cd = ((E.Type == BOSS) ? (dynamic)(4.0f) : 2.0f);
    }

    public void DamageTower(dynamic E, dynamic Tw, dynamic Dmg)
    {
        Tw.Hp = Gd.Maxf(0.0f, (Tw.Hp - (Dmg * ((E.Type == WRECKER) ? (dynamic)(2.0f) : 1.0f))));
        // FML-08: a hit blocks repair for 2 s
        Tw.Pause = 2.0f;
        if (((Tw.Hp <= 0) && !(Gd.Truth(Tw.Wrecked))))
        {
            Tw.Wrecked = true;
            this.Say(Gd.Format("Tower at door %s destroyed - its light is out", Gd.At(DOOR_NAMES, Tw.Door)));
        }
    }

    public dynamic EnemyNear(dynamic Pos, dynamic Radius)
    {
        foreach (dynamic E in Gd.Iter(Enemies))
        {
            if ((E.Pos.DistanceTo(Pos) <= Radius))
            {
                return true;
            }
        }
        return false;
    }

    public void TickSpitter(dynamic E, dynamic Dt)
    {
        dynamic Best = this.NearestPlayer(E.Pos, ((E.State == 4) ? (dynamic)(ROAM_SIGHT) : (8.0f * T)));
        float Bd = Gd.Float(((Best != null) ? (dynamic)(Best.Pos.DistanceTo(E.Pos)) : float.PositiveInfinity));
        if (((Best == null) && (E.State == 4)))
        {
            this.Wander(E, Dt);
        }
        else if ((Best == null))
        {
            Vector2 Front = (Vector2)((Gd.At(DOOR_POS, E.Door) + ((Gd.At(DOOR_DIR, E.Door) * 4) * T)));
            if ((E.Pos.DistanceTo(Front) > (0.5f * T)))
            {
                E.Pos += (((this.EnemyWaypoint(E, Front) - E.Pos).Normalized() * E.Spd) * Dt);
                E.Pos = GameSim.PushOut(E.Pos, E.R);
            }
        }
        else if ((Bd > (5.5f * T)))
        {
            E.Pos += (((this.EnemyWaypoint(E, Best.Pos) - E.Pos).Normalized() * E.Spd) * Dt);
            E.Pos = GameSim.PushOut(E.Pos, E.R);
        }
        else if (((E.Cd <= 0) && Gd.Truth(this.LineClear(E.Pos, Best.Pos))))
        {
            E.Cd = 2.0f;
            Gd.Append(Proj, new GDict { { "pos", E.Pos }, { "vel", (((Best.Pos - E.Pos).Normalized() * 8) * T) }, { "dmg", E.Dmg }, { "ttl", 1.0f } });
        }
    }

    public dynamic NearestPlayer(dynamic Pos, dynamic Radius)
    {
        dynamic Best = null;
        dynamic Bd = Radius;
        foreach (dynamic P in Gd.Iter(Gd.Values(Players)))
        {
            // an Engineer inside the base can only be reached through a broken door
            if (((P.State == 0) && (!(Gd.Truth(P.Inside)) || Gd.Truth(this.AnyBreach()))))
            {
                float D = Gd.Float(P.Pos.DistanceTo(Pos));
                if (((D <= Bd) && Gd.Truth(this.LineClear(Pos, P.Pos))))
                {
                    Bd = D;
                    Best = P;
                }
            }
        }
        return Best;
    }

    public dynamic NearestActiveDoor(dynamic Pos)
    {
        int Best = Gd.Int(Gd.At(Active, 0));
        foreach (dynamic I in Gd.Iter(Active))
        {
            if ((Pos.DistanceTo(Gd.At(DOOR_POS, I)) < Pos.DistanceTo(Gd.At(DOOR_POS, Best))))
            {
                Best = I;
            }
        }
        return Best;
    }

    public void TickProj(dynamic Dt)
    {
        foreach (dynamic B in Gd.Iter(Proj))
        {
            Vector2 Start = (Vector2)(B.Pos);
            Vector2 End = (Vector2)((Start + (B.Vel * Gd.Minf(Dt, Gd.Maxf(0.0f, B.Ttl)))));
            dynamic First = this.ObstacleHit(Start, End, 3.5f);
            dynamic Victim = null;
            foreach (dynamic P in Gd.Iter(Gd.Values(Players)))
            {
                if ((P.State != 0))
                {
                    continue;
                }
                dynamic Hit = WorldGeometry.CircleHit(Start, End, P.Pos, ((0.5f * T) + 3.5f));
                if ((Hit < First))
                {
                    First = Hit;
                    Victim = P;
                }
            }
            B.Pos = ((First < float.PositiveInfinity) ? (dynamic)(Start.Lerp(End, First)) : End);
            B.Ttl -= Dt;
            if ((First < float.PositiveInfinity))
            {
                if ((Victim != null))
                {
                    this.Hurt(Victim, B.Dmg);
                }
                B.Ttl = 0.0f;
            }
        }
        Proj = Gd.Filter(Proj, (Func<dynamic, dynamic>)((B) => (B.Ttl > 0)));
    }

    public void TickTele(dynamic Dt)
    {
        foreach (dynamic Tl in Gd.Iter(Tele))
        {
            Tl.TimeLeft -= Dt;
            if (((Tl.TimeLeft <= 0) && (Tl.Dmg > 0)))
            {
                foreach (dynamic P in Gd.Iter(Gd.Values(Players)))
                {
                    Vector2 D = (Vector2)((P.Pos - Tl.Pos));
                    bool In = (Gd.Length(D) <= Tl.R);
                    if ((In && (Tl.Kind == "arc")))
                    {
                        In = (Mathf.Abs(Mathf.AngleDifference(Gd.Float(Tl.Ang), D.Angle())) <= (Tl.Spread / 2));
                    }
                    if (In)
                    {
                        this.Hurt(P, Tl.Dmg);
                    }
                }
            }
        }
        Tele = Gd.Filter(Tele, (Func<dynamic, dynamic>)((Tl) => (Tl.TimeLeft > 0)));
    }

    public void Separate()
    {
        // Broad phase: circles cover every overlapping cell, including negative coordinates.
        dynamic Cells = new GDict {  };
        dynamic Pairs = new GDict {  };
        foreach (dynamic I in Gd.Iter(Gd.Size(Enemies)))
        {
            dynamic E = Gd.At(Enemies, I);
            dynamic Lo = Gd.MakeVector2I(((E.Pos - (Vector2.One * E.R)) / 64.0f).Floor());
            dynamic Hi = Gd.MakeVector2I(((E.Pos + (Vector2.One * E.R)) / 64.0f).Floor());
            foreach (dynamic Y in Gd.Iter(Gd.Range(Lo.Y, (Hi.Y + 1))))
            {
                foreach (dynamic X in Gd.Iter(Gd.Range(Lo.X, (Hi.X + 1))))
                {
                    dynamic Cell = Gd.MakeVector2I(X, Y);
                    foreach (dynamic J in Gd.Iter(Gd.Get(Cells, Cell, new GArray())))
                    {
                        Pairs[Gd.MakeVector2I(J, I)] = true;
                    }
                    if (!(Gd.Truth(Gd.Has(Cells, Cell))))
                    {
                        Cells[Cell] = new GArray();
                    }
                    Gd.Append(Gd.At(Cells, Cell), I);
                }
            }
        }
        foreach (dynamic Pair in Gd.Iter(Pairs))
        {
            dynamic A = Gd.At(Enemies, Pair.X);
            dynamic B = Gd.At(Enemies, Pair.Y);
            Vector2 D = (Vector2)((B.Pos - A.Pos));
            float Need = Gd.Float((A.R + B.R));
            if ((D.LengthSquared() >= (Need * Need)))
            {
                continue;
            }
            dynamic Length = Gd.Length(D);
            dynamic Normal = ((Length > 0.001f) ? (dynamic)((D / Length)) : Vector2.Right);
            dynamic Push = ((Normal * (Need - Length)) * 0.5f);
            A.Pos -= Push;
            B.Pos += Push;
        }
        // Resolve scenery once per body after all pair corrections, not once per pair.
        foreach (dynamic E in Gd.Iter(Enemies))
        {
            E.Pos = this.MoveActor(E.Pos, E.Pos, E.R);
        }
    }

    public dynamic SpawnPos(dynamic Door, dynamic DistTiles, dynamic AnySide)
    {
        if (!(Gd.Truth(AnySide)))
        {
            Vector2 Dd = (Vector2)(Gd.At(DOOR_DIR, Door));
            return ((Gd.At(DOOR_POS, Door) + ((Dd * DistTiles) * T)) + ((Gd.MakeVector2((-Dd.Y), Dd.X) * Rng.RandfRange((-22.0f), 22.0f)) * T));
        }
        dynamic Dir = Vector2.FromAngle((Rng.Randf() * Mathf.Tau));
        dynamic Pos = (Dir * ((DistTiles * T) + Gd.Length(HALF)));
        while ((GameSim.WallDist(Pos) < (DistTiles * T)))
        {
            Pos += (Dir * T);
        }
        return Pos;
    }

    public dynamic EnemyWaypoint(dynamic E, dynamic Goal, dynamic Outside = null)
    {
        Outside ??= true;
        if (((Gd.Get(E, "nav_t", 0.0f) <= 0) || !(Gd.Truth(Gd.Has(E, "nav_way"))) || (E.Pos.DistanceTo(E.NavWay) < 4.0f) || (Gd.Get(E, "nav_revision", (-1)) != World.Revision) || (Gd.Get(E, "nav_goal", Vector2.Inf).DistanceTo(Goal) > T)))
        {
            E.NavWay = this.Navigate(E.Pos, Goal, E.R, Outside);
            E.NavGoal = Goal;
            E.NavRevision = World.Revision;
            E.NavT = 0.4f;
        }
        return E.NavWay;
    }
}
