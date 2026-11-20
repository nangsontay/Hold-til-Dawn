using Godot;
using Environment = Godot.Environment;
using FileAccess = Godot.FileAccess;
using System;
using System.Threading.Tasks;
using HoldTilDawn;

// Owner: Pham Minh Hieu. Server-only match rules (GDD chapters 2-3): the public API used by the network
// (AddPlayer, RemovePlayer, Input, Action, Tick, Snapshot), core constants, match state and crafting.
// GameSim is split into partial files by owner; each file starts with an Owner comment. Clients never run this; they get Snapshot().
public partial class GameSim : RefCounted
{
    public static float DAY_MIN = GameplaySettings.Current.DayMin;
    public static readonly dynamic DAY_MAX = 360.0f;
    public static float DUSK = GameplaySettings.Current.DuskSeconds;
    public static readonly dynamic NIGHT_LEN = new GArray(600.0f, 720.0f, 600.0f, 720.0f, 600.0f);
    public static readonly dynamic BASE_HP = 1000.0f;
    public static readonly dynamic DOOR_HP = 500.0f;
    public static readonly dynamic ROLES = new GArray("Scout", "Healer", "Engineer");
    public static readonly dynamic ROLE_HP = new GArray(90.0f, 100.0f, 110.0f);
    public static readonly dynamic SPEED = (5.0f * T);
    public static readonly dynamic ARMOUR = new GArray(0.0f, 0.15f, 0.3f, 0.45f);
    public static readonly dynamic MODES = new GArray("Easy", "Medium", "Hard");
    public static readonly dynamic MODE_MUL = new GArray(new GArray(0.75f, 0.85f, 0.8f), new GArray(1.0f, 1.0f, 1.0f), new GArray(1.25f, 1.15f, 1.2f));
    public static readonly dynamic RES = new GArray("wood", "stone", "herb", "metal");
    // [name, cost, unlock tier, what it makes, amount, Engineer only]; "what" is an item id, "base" (repair) or
    // "revive" (Revival kit used at the workbench). Recipe table in GDD section 3.2; walls and towers are built (B).
    public static readonly dynamic RECIPES = new GArray(
        new GArray("Light armour", new GDict { { "wood", 6 } }, 0, "light_armour", 1, false),
        new GArray("Bandage", new GDict { { "herb", 2 } }, 0, "bandage", 1, false),
        new GArray("Revival kit", new GDict { { "herb", 4 }, { "metal", 4 } }, 0, "revive", 0, false),
        new GArray("Base repair +100", new GDict { { "stone", 5 } }, 0, "base", 100, true),
        new GArray("Stone spear", new GDict { { "wood", 8 }, { "stone", 12 } }, 1, "stone_spear", 1, false),
        new GArray("Stone armour", new GDict { { "wood", 8 }, { "stone", 16 } }, 1, "stone_armour", 1, false),
        new GArray("Bow + 5 arrows", new GDict { { "wood", 6 }, { "metal", 2 } }, 1, "bow", 1, false),
        new GArray("5 arrows", new GDict { { "wood", 1 } }, 1, "arrow", 5, false),
        new GArray("Metal blade", new GDict { { "wood", 10 }, { "metal", 20 } }, 2, "metal_blade", 1, false),
        new GArray("Metal armour", new GDict { { "metal", 24 } }, 2, "metal_armour", 1, false));
    public dynamic World = new WorldGeometry();
    public dynamic WallCache = new GArray();
    public dynamic WallKey = "";
    public dynamic Phase = "lobby";
    public dynamic TimeLeft = 0.0f;
    public dynamic Elapsed = 0.0f;
    public dynamic K = 1;
    public dynamic Mode = 1;
    public dynamic BaseHp = BASE_HP;
    public dynamic Doors = new GArray(DOOR_HP, DOOR_HP, DOOR_HP);
    public dynamic Broken = new GArray(false, false, false);
    public dynamic DoorPause = new GArray(0.0f, 0.0f, 0.0f);
    public dynamic Active = new GArray();
    public dynamic Players = new GDict {  };
    public dynamic Order = new GArray();
    public dynamic Enemies = new GArray();
    public dynamic Proj = new GArray();
    // players' arrows in flight (MEC-03)
    public dynamic Arrows = new GArray();
    // seconds since the match started; FML-09 threat windows are measured with it
    public dynamic Clock = 0.0f;
    public dynamic Tele = new GArray();
    public dynamic Nodes = new GArray();
    public dynamic Drops = new GArray();
    public dynamic Votes = new GDict {  };
    public dynamic Unlock = 0;
    public dynamic ShownUnlock = 0;
    public dynamic BossesKilled = 0;
    public dynamic BossDead = false;
    public dynamic Wave = 0;
    public dynamic NextWave = 0.0f;
    public dynamic Carry = 0.0f;
    public dynamic BossSpawned = false;
    public dynamic Towers = new GArray((object)null, (object)null, (object)null);
    public dynamic FreeTower = true;
    public dynamic Ext = (-1.0f);
    public dynamic PadClear = new GArray(0.0f, 0.0f, 0.0f);
    public dynamic Msg = "";
    // door where the generator was last hit, for the loss cause on the result screen
    public dynamic LastDoor = (-1);
    // [win, cause, night reached] while the result screen shows
    public dynamic Result = null;
    public dynamic MsgT = 0.0f;
    public dynamic NextId = 0;
    public dynamic N0 = 1;
    public dynamic Rng = Gd.Wrap(new RandomNumberGenerator());

    public dynamic Leader()
    {
        return ((Gd.Size(Order) > 0) ? (dynamic)(Gd.At(Order, 0)) : 0);
    }

    public void AddPlayer(dynamic Id, dynamic Pname, dynamic Role)
    {
        // ponytail: no joining mid-match (GDD: rejoin is out of scope).
        if (((Phase != "lobby") || (Gd.Size(Players) >= 5) || Gd.Truth(Gd.Has(Players, Id))))
        {
            return;
        }
        Players[Id] = new GDict { { "id", Id }, { "name", Gd.Substr(Pname, 0, 16) }, { "role", Gd.Clampi(Role, 0, 2) }, { "pos", Vector2.Zero }, { "tp", 0 }, { "bot", false } };
        Gd.Append(Order, Id);
        this.ResetPlayer(Gd.At(Players, Id));
    }

    public void RemovePlayer(dynamic Id)
    {
        Gd.Erase(Players, Id);
        Gd.Erase(Order, Id);
        Gd.Erase(Votes, Id);
        if ((this.Humans() == 0))
        {
            foreach (dynamic B in Gd.Iter(Gd.Keys(Players)))
            {
                Gd.Erase(Players, B);
                Gd.Erase(Order, B);
            }
            if ((Phase != "lobby"))
            {
                this.ToLobby();
            }
        }
    }

    public dynamic Humans()
    {
        return Gd.Size(Gd.Filter(Gd.Values(Players), (Func<dynamic, dynamic>)((P) => !(Gd.Truth(P.Bot)))));
    }

    public void Input(dynamic Id, dynamic Pos, dynamic Aim, dynamic Atk, dynamic Inter, dynamic Ent, dynamic Tp, dynamic Sequence = null)
    {
        Sequence ??= 0;
        dynamic P = Gd.Get(Players, Id);
        if (((P == null) || !(Gd.Truth(Pos.IsFinite())) || !(Gd.Truth(Gd.IsFinite(Aim)))))
        {
            return;
        }
        if (((Sequence > 0) && (Sequence <= P.InputSequence)))
        {
            return;
        }
        P.InputSequence = Sequence;
        P.Aim = Aim;
        P.Atk = Atk;
        P.Inter = Inter;
        P.Ent = Ent;
        if (((Tp == P.Tp) && (P.State == 0)))
        {
            Vector2 Travel = (Vector2)((Pos - P.Pos).LimitLength(P.MoveCredit));
            P.MoveCredit = Gd.Maxf(0.0f, (P.MoveCredit - Gd.Length(Travel)));
            P.Pos = this.MoveActor(P.Pos, (P.Pos + Travel), (0.3f * T));
            foreach (dynamic E in Gd.Iter(Enemies))
            {
                Vector2 Delta = (Vector2)((P.Pos - E.Pos));
                float Need = Gd.Float(((0.3f * T) + (E.R * 0.6f)));
                if ((Delta.LengthSquared() < (Need * Need)))
                {
                    P.Pos = (E.Pos + (((Delta.LengthSquared() > 0.001f) ? (dynamic)(Delta.Normalized()) : Vector2.Down) * Need));
                }
            }
            P.Pos = this.MoveActor(P.Pos, P.Pos, (0.3f * T));
            // at night only the Engineer (MEC-15) or a breach (MEC-16) lets anyone in; players caught inside
            // when a breach ends are moved out by TickBreachEnd after a warning (MEC-17)
            if (((Phase == "night") && !(Gd.Truth(P.Inside)) && !(Gd.Truth(this.AnyBreach())) && !Gd.Truth(GameSim.InsideBase(P.Pos))))
            {
                P.Pos = GameSim.PushOut(P.Pos, (0.4f * T));
            }
        }
    }

    public void Action(dynamic Id, dynamic Kind, dynamic Arg)
    {
        dynamic P = Gd.Get(Players, Id);
        if ((P == null))
        {
            return;
        }
        if (Gd.Equal(Kind, "role"))
        {
            if ((Phase == "lobby"))
            {
                P.Role = Gd.Clampi(Gd.Int(Arg), 0, 2);
                this.ResetPlayer(P);
            }
        }
        else if (Gd.Equal(Kind, "mode"))
        {
            if (((Phase == "lobby") && (Id == this.Leader())))
            {
                Mode = Gd.Clampi(Gd.Int(Arg), 0, 2);
            }
        }
        else if (Gd.Equal(Kind, "start"))
        {
            if (((Phase == "lobby") && (Id == this.Leader())))
            {
                dynamic Err = this.StartMatch();
                if ((Err != ""))
                {
                    this.Say(Err);
                }
            }
        }
        else if (Gd.Equal(Kind, "add_bot"))
        {
            if ((Id == this.Leader()))
            {
                Bots.Add(this);
            }
        }
        else if (Gd.Equal(Kind, "remove_bot"))
        {
            if ((Id == this.Leader()))
            {
                Bots.Remove(this);
            }
        }
        else if (Gd.Equal(Kind, "craft"))
        {
            this.Craft(P, Gd.Int(Arg));
        }
        else if (Gd.Equal(Kind, "tower"))
        {
            this.BuildTower(P);
        }
        else if (Gd.Equal(Kind, "build"))
        {
            this.Build(P, Arg);
        }
        else if (Gd.Equal(Kind, "lobby"))
        {
            // result screen: Back to lobby (section 2.5)
            if ((Phase == "result"))
            {
                this.ToLobby();
            }
        }
        else if (Gd.Equal(Kind, "bandage"))
        {
            if (((P.State == 0) && (this.Count(P, "bandage") > 0) && (P.Hp < P.Maxhp) && (P.Channel <= 0)))
            {
                P.Channel = 2.0f;
            }
        }
        else if (Gd.Equal(Kind, "select"))
        {
            P.Sel = Gd.Clampi(Gd.Int(Arg), 0, (HOTBAR - 1));
        }
        else if (Gd.Equal(Kind, "move_item"))
        {
            this.MoveSlot(P, Arg);
        }
        else if (Gd.Equal(Kind, "give"))
        {
            this.GiveOrDrop(P);
        }
        else if (Gd.Equal(Kind, "cancel_craft"))
        {
            P.CraftIdx = (-1);
        }
        else if (Gd.Equal(Kind, "flask"))
        {
            if (Gd.IsType(Arg, typeof(Vector2)))
            {
                this.Flask(P, Arg);
            }
        }
        else if (Gd.Equal(Kind, "vote"))
        {
            if ((Phase == "day"))
            {
                if (Gd.Truth(Gd.Has(Votes, Id)))
                {
                    Gd.Erase(Votes, Id);
                }
                else
                {
                    Votes[Id] = true;
                }
            }
        }
        else if (Gd.Equal(Kind, "skip"))
        {
            if (((Id == 1) && Gd.Truth(Gd.Static(typeof(OS), "is_debug_build")) && Gd.Contains(new GArray("day", "dusk", "night"), Phase)))
            {
                Gd.Clear(Enemies);
                TimeLeft = 0.0f;
            }
        }
    }

    public dynamic StartMatch()
    {
        dynamic Eng = false;
        foreach (dynamic P in Gd.Iter(Gd.Values(Players)))
        {
            if ((P.Role == 2))
            {
                Eng = true;
            }
        }
        if (!(Gd.Truth(Eng)))
        {
            return "Need at least one Engineer";
        }
        // ponytail: GDD wants 3-5 players, all Ready (BAL-01, card H08); 1+ allowed until the Ready lobby exists.
        Rng.Randomize();
        N0 = Gd.Size(Players);
        K = 1;
        BaseHp = BASE_HP;
        Doors = new GArray(DOOR_HP, DOOR_HP, DOOR_HP);
        Unlock = 0;
        Towers = new GArray((object)null, (object)null, (object)null);
        FreeTower = true;
        ShownUnlock = 0;
        BossesKilled = 0;
        Gd.Clear(Enemies);
        Gd.Clear(Proj);
        Gd.Clear(Arrows);
        Gd.Clear(Tele);
        Gd.Clear(Drops);
        Storage = GameSim.EmptySlots(STORAGE_SLOTS);
        BuiltWalls = new GArray();
        Clock = 0.0f;
        LastDoor = (-1);
        this.DrawBosses();
        this.GenNodes();
        foreach (dynamic P in Gd.Iter(Gd.Values(Players)))
        {
            this.ResetPlayer(P);
        }
        this.Dawn();
        return "";
    }

    public void Tick(dynamic Dt)
    {
        if ((Phase == "lobby"))
        {
            return;
        }
        TimeLeft -= Dt;
        MsgT -= Dt;
        Clock += Dt;
        if ((Phase == "result"))
        {
            return;
        }
        foreach (dynamic I in Gd.Iter(3))
        {
            DoorPause[I] -= Dt;
        }
        World.Rebuild(Nodes, Towers, BuiltWalls);
        Bots.Tick(this, Dt);
        this.TickPlayers(Dt);
        if ((Phase == "night"))
        {
            Elapsed += Dt;
            this.TickWaves();
        }
        else if ((Phase == "day"))
        {
            this.TickRoamers(Dt);
        }
        this.TickEnemies(Dt);
        this.TickTowers(Dt);
        this.TickProj(Dt);
        this.TickArrows(Dt);
        this.TickTele(Dt);
        this.TickDrops(Dt);
        if ((Phase == "night"))
        {
            this.TickBreachEnd(Dt);
            this.CheckEnd(Dt);
            return;
        }
        if (Gd.Truth(this.TeamWiped()))
        {
            this.End(false, "Team wiped");
        }
        else if ((Phase == "day"))
        {
            if (((TimeLeft <= 0) || (((DAY_MAX - TimeLeft) >= DAY_MIN) && (Gd.Size(Votes) >= Gd.Ceili((0.6f * this.Humans()))))))
            {
                this.Dusk();
            }
        }
        else if (((Phase == "dusk") && (TimeLeft <= 0)))
        {
            this.Night();
        }
    }

    // Player array layout: 0 name, 1 role, 2 pos, 3 hp, 4 maxhp, 5 state, 6 bleed, 7 inventory slots, 8 weapon id,
    // 9 armour tier, 10 flasks, 11 swinging, 12 aim, 13 bandage channel, 14 revive progress, 15 teleport count,
    // 16 note, 17 kills, 18 damage dealt, 19 inside, 20 entry, 21 gather, 22 activity, 23 activity target,
    // 24 input ack, 25 selected hotbar slot, 26 craft recipe (-1 none), 27 craft progress 0..1, 28 revives, 29 damage taken.
    public dynamic Snapshot()
    {
        dynamic Ps = new GDict {  };
        foreach (dynamic Id in Gd.Iter(Players))
        {
            dynamic P = Gd.At(Players, Id);
            Ps[Id] = new GArray(P.Name, P.Role, P.Pos, P.Hp, P.Maxhp, P.State, P.Bleed, P.Inv, P.Weapon, P.Armour, P.Flasks, (P.Swing > 0), P.Aim, P.Channel, P.ReviveP, P.Tp, ((P.NoteT > 0) ? (dynamic)(P.Note) : ""), P.Kills, P.Dmg, P.Inside, P.Entry, P.GatherAcc, P.Activity, P.ActivityTarget, P.InputSequence, P.Sel, P.CraftIdx, ((P.CraftIdx >= 0) ? (dynamic)((P.CraftT / this.CraftDuration(P.CraftIdx))) : 0.0f), P.Revives, P.Taken);
        }
        return new GDict { { "ph", Phase }, { "t", TimeLeft }, { "k", K }, { "base", BaseHp }, { "doors", Doors }, { "active", Active }, { "ps", Ps }, { "en", Gd.Map(Enemies, (Func<dynamic, dynamic>)((E) => new GArray(E.Id, E.Type, E.Pos, E.Hp, E.Maxhp, E.R, Gd.Get(E, "kind", (-1)), this.BossLook(E), Gd.Get(E, "face", 0.0f), (Gd.Get(E, "swing", 0.0f) > 0)))) }, { "proj", Gd.Map(Proj, (Func<dynamic, dynamic>)((B) => new GArray(B.Pos, Gd.Get(B, "kind", "spit")))) }, { "tele", Gd.Map(Tele, (Func<dynamic, dynamic>)((Tl) => new GArray(Tl.Pos, Tl.R, (Tl.TimeLeft / Tl.Dur), Tl.Kind, Tl.End, Tl.Ang, Tl.Spread))) }, { "nodes", Gd.Map(Nodes, (Func<dynamic, dynamic>)((Nd) => new GArray(Nd.Type, Nd.Pos, Nd.Amount))) }, { "drops", Gd.Map(Drops, (Func<dynamic, dynamic>)((D) => new GArray(D.Pos, D.Item, D.N, D.Ttl))) }, { "msg", (((MsgT > 0) || (Phase == "result")) ? (dynamic)(Msg) : "") }, { "unlock", Unlock }, { "votes", Gd.Size(Votes) }, { "need", Gd.Ceili((0.6f * this.Humans())) }, { "leader", this.Leader() }, { "mode", Mode }, { "ext", Ext }, { "pads", PadClear }, { "broken", Broken }, { "dpause", Gd.Map(DoorPause, (Func<dynamic, dynamic>)((V) => (V > 0))) }, { "inside_en", this.EnemyInside() }, { "tw", Gd.Map(Gd.Filter(Towers, (Func<dynamic, dynamic>)((Tw) => (Tw != null))), (Func<dynamic, dynamic>)((Tw) => new GArray(Tw.Door, Tw.Pos, Tw.Hp, Tw.Wrecked, (Tw.Target != null), ((Tw.Target != null) ? (dynamic)(Tw.Target) : Vector2.Zero)))) }, { "free_tower", FreeTower }, { "boss_next", (((K == 2) || (K == 4)) ? (dynamic)(this.BossKindFor(K)) : (-1)) }, { "sto", Storage }, { "walls", Gd.Map(BuiltWalls, (Func<dynamic, dynamic>)((W) => new GArray(W.Pos, W.Vert, W.Hp, W.Maxhp, W.Kind))) }, { "result", Result }, { "arrows", Gd.Map(Arrows, (Func<dynamic, dynamic>)((A) => new GArray(A.Pos, A.Vel.Angle()))) } };
    }

    public static readonly dynamic WALL_IN = 12.0f;
    public static readonly dynamic WALL_OUT = 6.0f;
    public static readonly dynamic DOOR_GAP = (1.2f * T);
}
