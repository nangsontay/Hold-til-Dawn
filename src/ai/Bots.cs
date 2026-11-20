using Godot;
using Environment = Godot.Environment;
using FileAccess = Godot.FileAccess;
using System;
using System.Threading.Tasks;
using HoldTilDawn;

public partial class Bots : RefCounted
{
    // Bot teammates for demos and solo playtests (not in the GDD). They run on the server inside GameSim and
    // follow the same rules as people: a bot only sets what a player's input sets (where it stands, aim,
    // attack, hold E) or calls the same actions (bandage, flask, craft). No planning, just priorities:
    //   dodge a boss slam > revive a downed teammate > heal (Healer) > fight > repair a door (Engineer)
    //   > by day gather, then craft at the workbench near dusk; at dusk and night hold the attacked door.
    // Bots share obstacle collision and navigation with the authoritative simulation.
    public static readonly dynamic T = GameSim.T;
    public static readonly dynamic GATHER = new GArray(0, 2, 1);
    public static readonly dynamic HOME_AT = 40.0f;
    public static readonly dynamic SIGHT_DAY = (6.0f * T);
    public static readonly dynamic LEASH = (8.0f * T);
    public static readonly dynamic FLASK_AT = 0.55f;
    public static readonly dynamic BANDAGE_AT = 0.4f;
    public static readonly dynamic BODY = (0.4f * T);
    // where bots stand to craft: in front of the workbench (GameSim.WORKBENCH_POS, -3.2, -2.0), within its reach
    public static readonly dynamic BENCH_SPOTS = new GArray(Gd.MakeVector2((-3.2f), (-0.8f)), Gd.MakeVector2((-2.1f), (-0.9f)), Gd.MakeVector2((-4.3f), (-0.9f)), Gd.MakeVector2((-2.4f), (-0.3f)));
    public static void Add(dynamic Sim)
    {
        // A bot takes the role the team lacks most: an Engineer first if there is none, then Healer, Scout.
        if (((Sim.Phase != "lobby") || (Gd.Size(Sim.Players) >= 5)))
        {
            return;
        }
        dynamic Count = new GArray(0, 0, 0);
        foreach (dynamic P in Gd.Iter(Gd.Values(Sim.Players)))
        {
            Count[P.Role] += 1;
        }
        dynamic Role = 2;
        if ((Gd.At(Count, 2) > 0))
        {
            Role = ((Gd.At(Count, 1) <= Gd.At(Count, 0)) ? (dynamic)(1) : 0);
        }
        dynamic Id = (-1);
        while (Gd.Truth(Gd.Has(Sim.Players, Id)))
        {
            Id -= 1;
        }
        string Pname = ("Bot " + Gd.At(GameSim.ROLES, Role));
        foreach (dynamic P in Gd.Iter(Gd.Values(Sim.Players)))
        {
            if ((P.Name == Pname))
            {
                Pname += Gd.Format(" %d", (-Id));
                break;
            }
        }
        Sim.AddPlayer(Id, Pname, Role);
        if (Gd.Truth(Gd.Has(Sim.Players, Id)))
        {
            Gd.At(Sim.Players, Id).Bot = true;
        }
    }

    public static void Remove(dynamic Sim)
    {
        if ((Sim.Phase != "lobby"))
        {
            return;
        }
        foreach (dynamic I in Gd.Iter(Gd.Range((Gd.Size(Sim.Order) - 1), (-1), (-1))))
        {
            int Id = Gd.Int(Gd.At(Sim.Order, I));
            if (Gd.Truth(Gd.At(Sim.Players, Id).Bot))
            {
                Sim.RemovePlayer(Id);
                return;
            }
        }
    }

    public static void Tick(dynamic Sim, dynamic Dt)
    {
        bool Playing = Gd.Truth(Gd.Contains(new GArray("day", "dusk", "night"), Sim.Phase));
        dynamic Slot = 0;
        foreach (dynamic P in Gd.Iter(Gd.Values(Sim.Players)))
        {
            if (!(Gd.Truth(P.Bot)))
            {
                continue;
            }
            P.Atk = false;
            P.Inter = false;
            P.Ent = false;
            P.NavT = (Gd.Get(P, "nav_t", 0.0f) - Dt);
            P.BotCd -= Dt;
            if ((Gd.Truth(Playing) && (P.State == 0)))
            {
                Bots.Think(Sim, P, Slot, Dt);
            }
            Slot += 1;
        }
    }

    public static void Think(dynamic Sim, dynamic P, dynamic Slot, dynamic Dt)
    {
        Bots.Equip(Sim, P);
        dynamic Post = Bots.Post(Sim, P, Slot);
        foreach (dynamic Tl in Gd.Iter(Sim.Tele))
        {
            if ((P.Pos.DistanceTo(Tl.Pos) < (Tl.R + (0.5f * T))))
            {
                Bots.Move(Sim, P, (Tl.Pos + ((P.Pos - Tl.Pos).Normalized() * (Tl.R + T))), Dt);
                return;
            }
        }
        dynamic Foe = Bots.Foe(Sim, P, Post);
        float FoeD = Gd.Float(((Foe == null) ? (dynamic)(float.PositiveInfinity) : P.Pos.DistanceTo(Foe.Pos)));
        dynamic Down = Bots.Nearest(Sim, P, (Func<dynamic, dynamic>)((Q) => (Q.State == 1)), (25.0f * T));
        if (((Down != null) && (FoeD > (2.5f * T))))
        {
            if ((P.Pos.DistanceTo(Down.Pos) > (1.0f * T)))
            {
                Bots.Move(Sim, P, Down.Pos, Dt);
            }
            else
            {
                P.Inter = true;
            }
            return;
        }
        if (((P.Role == 1) && (P.Flasks > 0) && (P.BotCd <= 0)))
        {
            dynamic Hurt = Bots.Nearest(Sim, P, (Func<dynamic, dynamic>)((Q) => ((Q.State == 0) && (Q.Hp < (FLASK_AT * Q.Maxhp)))), (3.0f * T));
            if (((Hurt != null) || (P.Hp < (FLASK_AT * P.Maxhp))))
            {
                Sim.Flask(P, ((Hurt != null) ? (dynamic)(Hurt.Pos) : P.Pos));
                P.BotCd = 1.0f;
            }
        }
        if (((P.Hp < (BANDAGE_AT * P.Maxhp)) && (Sim.Count(P, "bandage") > 0) && (P.Channel <= 0) && (FoeD > (3.0f * T))))
        {
            Sim.Action(P.Id, "bandage", 0);
        }
        if (((P.Channel > 0) && (FoeD > (1.5f * T))))
        {
            return;
        }
        if ((Foe != null))
        {
            float Reach = Gd.Float(((1.25f * T) + Foe.R));
            if ((FoeD > Reach))
            {
                Bots.Move(Sim, P, Foe.Pos, Dt);
            }
            P.Aim = (Foe.Pos - P.Pos).Angle();
            P.Atk = (P.Pos.DistanceTo(Foe.Pos) <= ((1.5f * T) + Foe.R));
            return;
        }
        if (((P.Role == 1) && (Sim.Phase != "day")))
        {
            dynamic Far = Bots.Nearest(Sim, P, (Func<dynamic, dynamic>)((Q) => ((Q.State == 0) && (Q.Hp < (FLASK_AT * Q.Maxhp)))), (10.0f * T));
            if (((Far != null) && (P.Flasks > 0)))
            {
                Bots.Move(Sim, P, Far.Pos, Dt);
                return;
            }
        }
        if (((P.Role == 2) && (Sim.Count(P, "wood") > 0) && Gd.Truth(Bots.Repair(Sim, P, Dt))))
        {
            return;
        }
        if (((Sim.Phase == "day") && (Sim.TimeLeft > HOME_AT)))
        {
            Bots.Gather(Sim, P, Dt);
        }
        else if ((Sim.Phase == "day"))
        {
            Vector2 Spot = (Vector2)((Gd.At(BENCH_SPOTS, (Slot % Gd.Size(BENCH_SPOTS))) * T));
            Bots.Move(Sim, P, Spot, Dt);
            if (Gd.Truth(GameSim.InsideBase(P.Pos)))
            {
                Bots.Craft(Sim, P);
            }
        }
        else
        {
            if ((P.Pos.DistanceTo(Post) > (0.3f * T)))
            {
                Bots.Move(Sim, P, Post, Dt);
            }
            else
            {
                P.Aim = (Post - Vector2.Zero).Angle();
            }
        }
    }

    public static dynamic Post(dynamic Sim, dynamic P, dynamic Slot)
    {
        // Bots share out the attacked doors and stand side by side in front of them; the Engineer stays closer.
        dynamic Act = (!(Gd.Truth(Gd.IsEmpty(Sim.Active))) ? (dynamic)(Sim.Active) : new GArray(0));
        int D = Gd.Int(Gd.At(Act, (Slot % Gd.Size(Act))));
        Vector2 Dir = (Vector2)(Gd.At(GameSim.DOOR_DIR, D));
        dynamic Out = (((P.Role == 2) ? (dynamic)(2.0f) : 3.0f) * T);
        if ((Sim.K == 5))
        {
            // night 5: between the door and its landing pad
            Out = (4.0f * T);
        }
        dynamic Side = ((Gd.Float((((Slot / Gd.Size(Act)) % 3) - 1)) * 1.5f) * T);
        return ((Gd.At(GameSim.DOOR_POS, D) + (Dir * Out)) + (Dir.Orthogonal() * Side));
    }

    public static dynamic Foe(dynamic Sim, dynamic P, dynamic Post)
    {
        dynamic Best = null;
        dynamic BestD = float.PositiveInfinity;
        bool Day = Gd.Truth((Sim.Phase == "day"));
        foreach (dynamic E in Gd.Iter(Sim.Enemies))
        {
            float D = Gd.Float(P.Pos.DistanceTo(E.Pos));
            bool Ok = Gd.Truth(false);
            if (Gd.Truth(Day))
            {
                Ok = ((D <= SIGHT_DAY) || (Gd.Get(E, "target", 0) == P.Id));
            }
            else
            {
                Ok = ((D <= (2.5f * T)) || (E.Pos.DistanceTo(Post) <= LEASH) || Gd.Truth(GameSim.InsideBase(E.Pos)));
            }
            if ((Gd.Truth(Ok) && (D < BestD) && Gd.Truth(Sim.LineClear(P.Pos, E.Pos))))
            {
                BestD = D;
                Best = E;
            }
        }
        return Best;
    }

    public static dynamic Nearest(dynamic Sim, dynamic P, dynamic Pred, dynamic Radius)
    {
        dynamic Best = null;
        dynamic BestD = Radius;
        foreach (dynamic Q in Gd.Iter(Gd.Values(Sim.Players)))
        {
            if (((Q.Id != P.Id) && Gd.Truth(Pred(Q))))
            {
                float D = Gd.Float(P.Pos.DistanceTo(Q.Pos));
                if ((D <= BestD))
                {
                    BestD = D;
                    Best = Q;
                }
            }
        }
        return Best;
    }

    public static dynamic Repair(dynamic Sim, dynamic P, dynamic Dt)
    {
        // The nearest door below full (30% cap once broken) that can be repaired now; stands just outside it.
        dynamic Best = (-1);
        foreach (dynamic I in Gd.Iter(3))
        {
            float Cap = Gd.Float((GameSim.DOOR_HP * (Gd.Truth(Gd.At(Sim.Broken, I)) ? (dynamic)(0.3f) : 1.0f)));
            if (((Gd.At(Sim.Doors, I) >= (Cap - 1.0f)) || (Gd.At(Sim.DoorPause, I) > 0) || (Gd.Truth(Gd.At(Sim.Broken, I)) && Gd.Truth(Sim.EnemyInside()))))
            {
                continue;
            }
            if (((Best < 0) || (P.Pos.DistanceTo(Gd.At(GameSim.DOOR_POS, I)) < P.Pos.DistanceTo(Gd.At(GameSim.DOOR_POS, Best)))))
            {
                Best = I;
            }
        }
        if ((Best < 0))
        {
            return false;
        }
        Vector2 Spot = (Vector2)((Gd.At(GameSim.DOOR_POS, Best) + ((Gd.At(GameSim.DOOR_DIR, Best) * 1.0f) * T)));
        if ((P.Pos.DistanceTo(Spot) > (0.3f * T)))
        {
            Bots.Move(Sim, P, Spot, Dt);
        }
        else
        {
            P.Inter = true;
        }
        return true;
    }

    public static void Gather(dynamic Sim, dynamic P, dynamic Dt)
    {
        int I = Gd.Int(P.BotNode);
        if (((I < 0) || (I >= Gd.Size(Sim.Nodes)) || (Gd.At(Sim.Nodes, I).Amount <= 0)))
        {
            I = Bots.PickNode(Sim, P);
            P.BotNode = I;
        }
        if ((I < 0))
        {
            return;
        }
        dynamic Nd = Gd.At(Sim.Nodes, I);
        if ((P.Pos.DistanceTo(Nd.Pos) > (1.0f * T)))
        {
            Bots.Move(Sim, P, Nd.Pos, Dt);
        }
        else
        {
            P.Aim = (Nd.Pos - P.Pos).Angle();
            // hold E standing still (MEC-08)
            P.Inter = true;
        }
    }

    public static dynamic PickNode(dynamic Sim, dynamic P)
    {
        // Nearest safe-zone node (no titanium), its favourite resource counted as 12 tiles closer; one bot per node.
        dynamic Taken = new GArray();
        foreach (dynamic Q in Gd.Iter(Gd.Values(Sim.Players)))
        {
            if ((Gd.Truth(Q.Bot) && (Q.Id != P.Id)))
            {
                Gd.Append(Taken, Q.BotNode);
            }
        }
        dynamic Best = (-1);
        dynamic BestS = float.PositiveInfinity;
        foreach (dynamic I in Gd.Iter(Gd.Size(Sim.Nodes)))
        {
            dynamic Nd = Gd.At(Sim.Nodes, I);
            if (((Nd.Type == 3) || (Nd.Amount <= 0) || Gd.Contains(Taken, I)))
            {
                continue;
            }
            float S = Gd.Float((P.Pos.DistanceTo(Nd.Pos) - ((Nd.Type == Gd.At(GATHER, P.Role)) ? (dynamic)((12.0f * T)) : 0.0f)));
            if ((S < BestS))
            {
                BestS = S;
                Best = I;
            }
        }
        return Best;
    }

    public static void Craft(dynamic Sim, dynamic P)
    {
        // One useful recipe it can afford: best weapon and armour first, then kits. Crafting takes 3 s.
        if (((P.BotCd > 0) || (P.CraftIdx >= 0)))
        {
            return;
        }
        P.BotCd = 0.5f;
        bool Dead = Gd.Truth(Gd.Any(Gd.Values(Sim.Players), (Func<dynamic, dynamic>)((Q) => (Q.State == 2))));
        int HasWeapon = ((Sim.Count(P, "titanium_blade") > 0) ? 2 : ((Sim.Count(P, "stone_spear") > 0) ? 1 : 0));
        foreach (dynamic Idx in Gd.Iter(new GArray(8, 9, 4, 5, 0, 2, 1, 3)))
        {
            dynamic R = Gd.At(GameSim.RECIPES, Idx);
            string Makes = Gd.At(R, 3);
            bool Useful = Makes switch
            {
                "stone_spear" => (HasWeapon < 1),
                "titanium_blade" => (HasWeapon < 2),
                "light_armour" => (Sim.ArmourTier(P) < 1),
                "stone_armour" => (Sim.ArmourTier(P) < 2),
                "titanium_armour" => (Sim.ArmourTier(P) < 3),
                "bandage" => (Sim.Count(P, "bandage") < 2),
                "revive" => Dead,
                "base" => (Sim.BaseHp <= (GameSim.BASE_HP - 100)),
                _ => false,
            };
            if ((Useful && Sim.CanCraft(P, Gd.Int(Idx), false)))
            {
                Sim.Craft(P, Idx);
                return;
            }
        }
        Bots.Equip(Sim, P);
    }

    // Holds the strongest melee weapon it carries (blade, then spear), else an empty hand: bare hands hit
    // harder than the hammer or the starter bow up close.
    public static void Equip(dynamic Sim, dynamic P)
    {
        foreach (dynamic W in Gd.Iter(new GArray("titanium_blade", "stone_spear")))
        {
            for (int I = 0; I < Gd.Size(P.Inv); I++)
            {
                dynamic S = Gd.At(P.Inv, I);
                if (((S != null) && Gd.Equal(Gd.At(S, 0), W)))
                {
                    int Hand = I;
                    if ((I >= GameSim.HOTBAR))
                    {
                        Hand = (GameSim.HOTBAR - 1);
                        Sim.MoveSlot(P, new GArray("inv", I, "inv", Hand));
                    }
                    P.Sel = Hand;
                    return;
                }
            }
        }
        foreach (dynamic I in Gd.Iter(GameSim.HOTBAR))
        {
            dynamic S = Gd.At(P.Inv, I);
            if (((S == null) || (GameSim.KindOf(Gd.At(S, 0)) != "weapon")))
            {
                P.Sel = I;
                return;
            }
        }
    }

    public static void Move(dynamic Sim, dynamic P, dynamic Goal, dynamic Dt)
    {
        // Walks like a player: same speed, same walls, and out of the base at night unless a door is broken.
        dynamic BaseWay = Bots.Waypoint(Sim, P.Pos, Goal);
        if (((Gd.Get(P, "nav_t", 0.0f) <= 0) || !(Gd.Truth(Gd.Has(P, "nav_way"))) || (P.Pos.DistanceTo(P.NavWay) < 4) || (Gd.Get(P, "nav_goal", Vector2.Inf).DistanceTo(BaseWay) > T) || (Gd.Get(P, "nav_revision", (-1)) != Sim.World.Revision)))
        {
            P.NavWay = Sim.Navigate(P.Pos, BaseWay, (0.3f * T), false);
            P.NavGoal = BaseWay;
            P.NavRevision = Sim.World.Revision;
            P.NavT = 0.4f;
        }
        Vector2 Way = (Vector2)(P.NavWay);
        Vector2 Step = (Vector2)((Way - P.Pos));
        if ((Gd.Length(Step) < 1.0f))
        {
            return;
        }
        float Spd = Gd.Float((GameSim.SPEED * ((P.Role == 0) ? (dynamic)(1.2f) : 1.0f)));
        P.Pos = Sim.MoveActor(P.Pos, (P.Pos + (Step.Normalized() * Gd.Minf((Spd * Dt), Gd.Length(Step)))), (0.3f * T));
        P.Aim = Step.Angle();
        bool Night = Gd.Truth((Sim.Phase == "night"));
        P.Pos = GameSim.Collide(P.Pos, BODY, Gd.Map(Sim.Doors, (Func<dynamic, dynamic>)((D) => (!(Gd.Truth(Night)) || (D <= 0)))));
        if ((Gd.Truth(Night) && !(Gd.Truth(P.Inside)) && !(Gd.Truth(Gd.Any(Sim.Doors, (Func<dynamic, dynamic>)((D) => (D <= 0)))))))
        {
            P.Pos = GameSim.PushOut(P.Pos, BODY);
        }
    }

    public static dynamic Waypoint(dynamic Sim, dynamic From, dynamic To)
    {
        // Around the base outside, through a door to get in or out.
        dynamic AIn = GameSim.InsideBase(From);
        dynamic BIn = GameSim.InsideBase(To);
        if ((Gd.Truth(AIn) && Gd.Truth(BIn)))
        {
            return To;
        }
        bool Night = Gd.Truth((Sim.Phase == "night"));
        dynamic Best = (-1);
        dynamic BestD = float.PositiveInfinity;
        foreach (dynamic I in Gd.Iter(3))
        {
            if ((Gd.Truth(Night) && (Gd.At(Sim.Doors, I) > 0)))
            {
                continue;
            }
            float D = Gd.Float((From.DistanceTo(Gd.At(GameSim.DOOR_POS, I)) + Gd.At(GameSim.DOOR_POS, I).DistanceTo(To)));
            if ((D < BestD))
            {
                BestD = D;
                Best = I;
            }
        }
        dynamic Gap = ((Best >= 0) && (From.DistanceTo(Gd.At(GameSim.DOOR_POS, Best)) < (1.0f * T)));
        if (!(Gd.Truth(AIn)))
        {
            if ((Gd.Truth(BIn) && (Best >= 0)))
            {
                Vector2 Outer = (Vector2)((Gd.At(GameSim.DOOR_POS, Best) + (Gd.At(GameSim.DOOR_DIR, Best) * T)));
                if ((Gd.Truth(Gap) || (From.DistanceTo(Outer) < (0.3f * T))))
                {
                    return (Gd.At(GameSim.DOOR_POS, Best) - (Gd.At(GameSim.DOOR_DIR, Best) * T));
                }
                if (!(Gd.Truth(GameSim.InsideBase(From, (BODY + 2)))))
                {
                    return GameSim.Route(From, Outer, BODY);
                }
            }
            if (Gd.Truth(GameSim.InsideBase(From, (BODY + 2))))
            {
                return GameSim.PushOut(From, (BODY + 4));
            }
            return GameSim.Route(From, To, BODY);
        }
        if ((Best < 0))
        {
            return From;
        }
        Vector2 Inner = (Vector2)((Gd.At(GameSim.DOOR_POS, Best) - (Gd.At(GameSim.DOOR_DIR, Best) * T)));
        if ((Gd.Truth(Gap) || (From.DistanceTo(Inner) < (0.3f * T))))
        {
            return (Gd.At(GameSim.DOOR_POS, Best) + (Gd.At(GameSim.DOOR_DIR, Best) * T));
        }
        return Inner;
    }

}
