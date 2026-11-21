using Godot;
using Environment = Godot.Environment;
using FileAccess = Godot.FileAccess;
using System;
using System.Threading.Tasks;
using HoldTilDawn;

// Owner: Pham Minh Hieu. Player actions: movement credit, combat, gather/repair/revive, healing (MEC-01..13).
public partial class GameSim : RefCounted
{
    // Crafting takes CRAFT_TIME at the workbench (BAL-19); base repair runs at 20 HP/s (BAL-20).
    public const float CRAFT_TIME = 3.0f;
    // seconds of holding E over a dead teammate to use a Revival kit on them
    public const float REVIVE_KIT_TIME = 3.0f;

    // In the base (by day, or as the Engineer who went in at night) and standing at the furniture.
    public bool InBaseToWork(dynamic P)
    {
        return Gd.Truth(((Gd.Contains(new GArray("day", "dusk"), Phase) && Gd.Truth(GameSim.InsideBase(P.Pos))) || ((Phase == "night") && Gd.Truth(P.Inside))));
    }

    public bool AtBench(dynamic P)
    {
        return (this.InBaseToWork(P) && GameSim.NearBench((Vector2)P.Pos));
    }

    // Checks every rule of MEC-10; Note() tells the player why not.
    public bool CanCraft(dynamic P, int Idx, bool Tell = true)
    {
        if (((Idx < 0) || (Idx >= Gd.Size(RECIPES))))
        {
            return false;
        }
        dynamic R = Gd.At(RECIPES, Idx);
        string Why = "";
        if (((P.State != 0) || !this.AtBench(P)))
        {
            Why = "Go to the workbench";
        }
        else if ((RescueKind(Gd.Str(Gd.At(R, 3))) < 0) && (Gd.At(R, 2) > Unlock))
        {
            Why = Gd.Format("%s is locked", Gd.At(R, 0));
        }
        else if ((Gd.Truth(Gd.At(R, 5)) && (P.Role != 2)))
        {
            Why = "Engineer only";
        }
        else if (((Gd.At(R, 3) == "base") && (Phase == "night")))
        {
            Why = "By day only";
        }
        else if (((Gd.At(R, 3) == "base") && (BaseHp >= BASE_HP)))
        {
            Why = "Generator full";
        }
        else if (RescueKind(Gd.Str(Gd.At(R, 3))) >= 0 && ((string)Phase != "day" || RescueCrafted[RescueKind(Gd.Str(Gd.At(R, 3)))]))
        {
            Why = (string)Phase != "day" ? "Rescue equipment: craft by day only" : "Already installed for the team";
        }
        else if ((GameSim.KindOf(Gd.At(R, 3)) == "armour") && (Gd.Find(ARMOUR_ITEMS, Gd.At(R, 3)) <= Gd.Int(P.Armour)))
        {
            Why = "Already worn";
        }
        else
        {
            foreach (dynamic Res in Gd.Iter(Gd.At(R, 1)))
            {
                if ((this.CraftHave(P, Gd.Str(Res)) < Gd.At(Gd.At(R, 1), Res)))
                {
                    Why = Gd.Format("Need more %s", GameSim.ItemName(Res));
                    break;
                }
            }
        }
        if (((Why != "") && Tell))
        {
            this.Note(P, Why);
        }
        return (Why == "");
    }

    public dynamic CraftDuration(int Idx)
    {
        dynamic R = Gd.At(RECIPES, Idx);
        return ((Gd.At(R, 3) == "base") ? (dynamic)((Gd.At(R, 4) / 20.0f)) : CRAFT_TIME);
    }

    // Starts the craft channel; the cost is paid when it completes (TickCraft).
    public void Craft(dynamic P, dynamic Idx)
    {
        if (((P.CraftIdx >= 0) || !this.CanCraft(P, Gd.Int(Idx))))
        {
            return;
        }
        P.CraftIdx = Gd.Int(Idx);
        P.CraftT = 0.0f;
    }

    public void TickCraft(dynamic P, dynamic Dt)
    {
        if ((P.CraftIdx < 0))
        {
            return;
        }
        if (((P.State != 0) || !this.AtBench(P)))
        {
            P.CraftIdx = (-1);
            return;
        }
        P.Activity = "craft";
        P.CraftT += Dt;
        if ((P.CraftT < this.CraftDuration(P.CraftIdx)))
        {
            return;
        }
        int Idx = Gd.Int(P.CraftIdx);
        P.CraftIdx = (-1);
        P.CraftT = 0.0f;
        if (this.CanCraft(P, Idx))
        {
            this.FinishCraft(P, Idx);
        }
    }

    public void FinishCraft(dynamic P, int Idx)
    {
        dynamic R = Gd.At(RECIPES, Idx);
        foreach (dynamic Res in Gd.Iter(Gd.At(R, 1)))
        {
            int Component = Array.IndexOf(COMPONENT_IDS, Gd.Str(Res));
            if (Component >= 0) RescueComponents[Component] -= Gd.Int(Gd.At(Gd.At(R, 1), Res));
            else this.Take(P, Res, Gd.At(Gd.At(R, 1), Res));
        }
        int Rescue = RescueKind(Gd.Str(Gd.At(R, 3)));
        if (Rescue >= 0)
        {
            RescueCrafted[Rescue] = true;
            Say(Gd.Str(Gd.At(R, 0)) + " installed for rescue" + (SuppliesAllIn() ? ": all equipment ready!" : ""));
        }
        else if (Gd.Equal(Gd.At(R, 3), "base"))
        {
            BaseHp = Gd.Minf(BASE_HP, (BaseHp + Gd.At(R, 4)));
        }
        else
        {
            this.AddItem(P, Gd.At(R, 3), Gd.At(R, 4));
            if (Gd.Equal(Gd.At(R, 3), "bow"))
            {
                this.AddItem(P, "arrow", 5);
            }
        }
        this.Note(P, ("Crafted " + Gd.At(R, 0)));
    }

    // --- players ------------------------------------------------------------
    public void ResetPlayer(dynamic P)
    {
        P.Hp = Gd.At(ROLE_HP, P.Role);
        P.Maxhp = P.Hp;
        P.HasteUntil = 0f; P.ItemCd = 0f; P.RepairKitT = 0f;
        P.AreaHealAt = 0f; P.TacticalThinkAt = 0f; P.NextTrapAt = 0f;
        // 0 standing, 1 downed, 2 dead
        P.State = 0;
        P.Bleed = 0.0f;
        this.GiveStartingKit(P);
        P.Weapon = this.WeaponOf(P);
        P.Armour = 0;
        P.CraftIdx = (-1);
        P.CraftT = 0.0f;
        P.Flasks = ((P.Role == 1) ? (dynamic)(3) : 0);
        P.Invuln = 0.0f;
        P.AtkCd = 0.0f;
        P.Swing = 0.0f;
        P.Channel = 0.0f;
        P.RepairKitT = 0f;
        P.TowerBuildT = 0f;
        P.ReviveP = 0.0f;
        P.GatherAcc = 0.0f;
        P.RepairAcc = 0.0f;
        P.Aim = 0.0f;
        P.Atk = false;
        P.Inter = false;
        // result screen stats (section 2.5): kills, damage dealt, revives, damage taken
        P.Kills = 0;
        P.Dmg = 0.0f;
        P.Revives = 0;
        P.Taken = 0.0f;
        // Engineer inside the base at night (MEC-15)
        P.Inside = false;
        P.Entry = 0.0f;
        P.EntryPos = Vector2.Zero;
        P.Ent = false;
        P.GatherPos = Vector2.Zero;
        P.RoamT = ROAM_CHECK;
        P.Note = "";
        P.NoteT = 0.0f;
        P.BotCd = 0.0f;
        P.Activity = "idle";
        P.ActivityTarget = Vector2.Zero;
        P.MoveCredit = (SPEED * 0.1f);
        P.InputSequence = 0;
        // index of the resource node a bot is gathering
        P.BotNode = (-1);
        // spawn beside the lighthouse, left or right (behind it the sprite hides you), in front of the workbench
        // and the storage chest by the north wall
        float Side = ((Rng.Randf() < 0.5f) ? (-1.0f) : 1.0f);
        this.Teleport(P, World.Resolve((Gd.MakeVector2((Side * Rng.RandfRange(2.5f, 4.0f)), Rng.RandfRange((-0.6f), 1.5f)) * T), (0.3f * T)));
    }

    public void TickPlayers(dynamic Dt)
    {
        foreach (dynamic P in Gd.Iter(Gd.Values(Players)))
        {
            P.Activity = "idle";
            P.MoveCredit = Gd.Minf((PlayerSpeed(P) * 0.2f), (P.MoveCredit + (PlayerSpeed(P) * Dt)));
            P.Invuln -= Dt;
            P.AtkCd -= Dt;
            P.Swing -= Dt;
            P.NoteT -= Dt;
            P.Weapon = this.WeaponOf(P);
            this.WearArmour(P);
            if ((P.State == 1))
            {
                P.Bleed -= Dt;
                if ((P.Bleed <= 0))
                {
                    P.State = 2;
                    this.Say(Gd.Format("%s died", P.Name));
                }
            }
            if ((P.State != 0))
            {
                TickTowerConstruction(P, 0);
                continue;
            }
            if ((P.Channel > 0))
            {
                P.Activity = "heal";
                if (Gd.Truth(P.Atk))
                {
                    // attacking cancels the bandage and keeps it
                    P.Channel = 0.0f;
                }
                else
                {
                    P.Channel -= Dt;
                    if (((P.Channel <= 0) && this.Take(P, "bandage", 1)))
                    {
                        P.Hp = Gd.Minf(P.Maxhp, (P.Hp + (30.0f * ((P.Role == 1) ? (dynamic)(1.5f) : 1.0f))));
                    }
                }
            }
            if (TickTowerConstruction(P, Gd.Float(Dt))) continue;
            if (TickFieldRepair(P, Gd.Float(Dt))) continue;
            this.TickEntry(P, Dt);
            this.TickCraft(P, Dt);
            if (Gd.Truth(P.Inside))
            {
                P.ReviveP = 0.0f;
                continue;
            }
            if ((Gd.Truth(P.Atk) && (P.AtkCd <= 0)))
            {
                this.Attack(P);
            }
            if (Gd.Truth(P.Inter))
            {
                this.Interact(P, Dt);
            }
            else
            {
                P.ReviveP = 0.0f;
                P.GatherAcc = 0.0f;
            }
        }
    }

    public void TickEntry(dynamic P, dynamic Dt)
    {
        dynamic Door = (-1);
        if ((Gd.Truth(P.Ent) && (P.Role == 2) && (Phase == "night")))
        {
            foreach (dynamic I in Gd.Iter(3))
            {
                if (((Gd.At(Doors, I) > 0) && (P.Pos.DistanceTo(Gd.At(DOOR_POS, I)) <= (2.0f * T))))
                {
                    Door = I;
                }
            }
        }
        if (((Door < 0) || ((P.Entry > 0) && (P.Pos.DistanceTo(P.EntryPos) > 4.0f))))
        {
            P.Entry = 0.0f;
            return;
        }
        if ((P.Entry == 0.0f))
        {
            P.EntryPos = P.Pos;
        }
        P.Entry += Dt;
        if ((P.Entry < 2.0f))
        {
            return;
        }
        P.Entry = 0.0f;
        P.Inside = !(Gd.Truth(P.Inside));
        if (Gd.Truth(P.Inside))
        {
            this.Teleport(P, (Gd.At(DOOR_POS, Door) - ((Gd.At(DOOR_DIR, Door) * 1.2f) * T)));
            this.Note(P, "Inside the base");
        }
        else
        {
            this.Teleport(P, (Gd.At(DOOR_POS, Door) + (Gd.At(DOOR_DIR, Door) * T)));
            P.Invuln = 0.5f;
        }
    }

    public const float ARROW_SPEED = 16.0f;  // tiles/s

    // MEC-02 / MEC-03: a melee swing hits every enemy in the 90-degree arc once; a bow fires one arrow.
    public void Attack(dynamic P)
    {
        string Selected = SelectedItem(P);
        if (IsTacticalUse(Selected))
        {
            return;
        }
        string W = P.Weapon;
        if (W == "long_spear") { AttackLongSpear(P); return; }
        dynamic St = WEAPON_STATS[W];
        if ((Gd.Truth(Gd.At(St, 4)) && !this.Take(P, "arrow", 1)))
        {
            this.Note(P, "No arrows");
            P.AtkCd = 0.5f;
            return;
        }
        // BAL-07: the Scout attacks 1.25x faster
        P.AtkCd = (1.0f / (Gd.At(St, 1) * ((P.Role == 0) ? (dynamic)(1.25f) : 1.0f)));
        P.Swing = 0.25f;
        dynamic Dir = Vector2.FromAngle(P.Aim);
        if (Gd.Truth(Gd.At(St, 3)))
        {
            Gd.Append(Arrows, new GDict { { "pos", (P.Pos + (Dir * (0.4f * T))) }, { "vel", ((Dir * ARROW_SPEED) * T) }, { "dmg", Gd.At(St, 0) }, { "left", ((Gd.At(St, 2) * T) - (0.4f * T)) }, { "owner", P.Id } });
            return;
        }
        float Reach = Gd.Float((Gd.At(St, 2) * T));
        foreach (dynamic E in Gd.Iter(Enemies))
        {
            Vector2 D = (Vector2)((E.Pos - P.Pos));
            if (((Gd.Length(D) <= (Reach + E.R)) && ((Gd.Length(D) < (0.5f * T)) || (Dir.Dot(D.Normalized()) >= Gd.Cos((Mathf.Pi / 4)))) && Gd.Truth(this.LineClear(P.Pos, E.Pos))))
            {
                this.Hit(E, Gd.At(St, 0), P);
                if (W == "stun_maul")
                {
                    if (E.Type == BOSS) E.SlowUntil = Gd.Float(Clock) + 1f;
                    else E.StunUntil = Gd.Float(Clock) + 0.8f;
                }
            }
        }
        Enemies = Gd.Filter(Enemies, (Func<dynamic, dynamic>)((E) => (E.Hp > 0)));
    }

    // Arrows fly toward the cursor, hit the first enemy on their way and stop at walls or at their range.
    public void TickArrows(dynamic Dt)
    {
        foreach (dynamic A in Gd.Iter(Arrows))
        {
            Vector2 Start = (Vector2)(A.Pos);
            float Step = Gd.Minf((Gd.Length(A.Vel) * Dt), A.Left);
            Vector2 End = (Start + (A.Vel.Normalized() * Step));
            dynamic First = this.ObstacleHit(Start, End, 2.0f);
            dynamic Victim = null;
            foreach (dynamic E in Gd.Iter(Enemies))
            {
                if (!this.Hittable(E))
                {
                    continue;
                }
                dynamic H = WorldGeometry.CircleHit(Start, End, E.Pos, (E.R + 2.0f));
                if ((H < First))
                {
                    First = H;
                    Victim = E;
                }
            }
            A.Left -= Step;
            A.Pos = ((First < float.PositiveInfinity) ? (dynamic)(Start.Lerp(End, First)) : End);
            if (First == float.PositiveInfinity && A.Left <= 0 && Gd.Truth(Gd.Get(A, "explosive", false))) ExplodeArrow(A);
            if ((First < float.PositiveInfinity))
            {
                A.Left = 0.0f;
                dynamic Owner = Gd.Get(Players, A.Owner);
                if (Gd.Truth(Gd.Get(A, "explosive", false))) ExplodeArrow(A);
                else if (((Victim != null) && (Owner != null)))
                {
                    this.Hit(Victim, A.Dmg, Owner);
                }
            }
        }
        Arrows = Gd.Filter(Arrows, (Func<dynamic, dynamic>)((A) => (A.Left > 0)));
        Enemies = Gd.Filter(Enemies, (Func<dynamic, dynamic>)((E) => (E.Hp > 0)));
    }

    // FML-09: threat is the damage a player dealt to this enemy in the last 5 s.
    public float Threat(dynamic E, dynamic Pid)
    {
        float Sum = 0.0f;
        foreach (dynamic H in Gd.Iter(E.Hits))
        {
            if ((Gd.Equal(Gd.At(H, 1), Pid) && ((Clock - Gd.At(H, 0)) <= 5.0f)))
            {
                Sum += Gd.Float(Gd.At(H, 2));
            }
        }
        return Sum;
    }

    // Chase the top threat; switch only when a newcomer's threat beats the current target's by 20%.
    public void Retarget(dynamic E)
    {
        E.Hits = Gd.Filter(E.Hits, (Func<dynamic, dynamic>)((H) => ((Clock - Gd.At(H, 0)) <= 5.0f)));
        float Cur = this.Threat(E, E.Target);
        foreach (dynamic H in Gd.Iter(E.Hits))
        {
            dynamic Pid = Gd.At(H, 1);
            dynamic Q = Gd.Get(Players, Pid);
            if (((Q != null) && (Q.State == 0) && (this.Threat(E, Pid) > (1.2f * Cur))))
            {
                E.Target = Pid;
                Cur = this.Threat(E, Pid);
            }
        }
    }

    public void Hit(dynamic E, dynamic Dmg, dynamic P)
    {
        if (E.Hp <= 0 || !this.Hittable(E))
        {
            return;
        }
        Dmg = this.BossDamageTaken(E, Dmg, P.Pos);
        E.Hp -= Dmg;
        E.Nodmg = 0.0f;
        P.Dmg += Dmg;
        this.BossOnHit(E);
        // Grunts and bosses retaliate unless locked on a door or tower; Wreckers never do (AGT-01..03).
        if ((((E.Type == GRUNT) || (E.Type == BOSS)) && ((E.State <= 1) || (E.State == 4))))
        {
            Gd.Append(E.Hits, new GArray(Clock, P.Id, Dmg));
            if (((E.State == 0) || (E.State == 4)))
            {
                // BAL-35: a boss ignores new threat for 10 s after its leash broke
                if ((Gd.Get(E, "calm", 0.0f) <= 0))
                {
                    E.State = 1;
                    E.Anchor = E.Pos;
                    E.Target = P.Id;
                }
            }
            else
            {
                this.Retarget(E);
            }
        }
        if ((E.Hp <= 0))
        {
            this.Drop(E);
            P.Kills += 1;
            if ((E.Type == BOSS))
            {
                BossDead = true;
            }
        }
    }

    public void Interact(dynamic P, dynamic Dt)
    {
        foreach (dynamic Q in Gd.Iter(Gd.Values(Players)))
        {
            if (((Q.State == 1) && (Q.Id != P.Id) && (Q.Pos.DistanceTo(P.Pos) <= (1.5f * T)) && Gd.Truth(this.LineClear(P.Pos, Q.Pos))))
            {
                P.Activity = "revive";
                P.ActivityTarget = Q.Pos;
                P.ReviveP += (Dt / ((P.Role == 1) ? (dynamic)(10.0f) : 20.0f));
                if ((P.ReviveP >= 1.0f))
                {
                    P.ReviveP = 0.0f;
                    Q.State = 0;
                    Q.Hp = (0.3f * Q.Maxhp);
                    Q.Invuln = 1.0f;
                    P.Revives += 1;
                    this.Say(Gd.Format("%s revived %s", new GArray(P.Name, Q.Name)));
                }
                return;
            }
        }
        // a Revival kit is carried to a dead teammate's body and used there: hold E for REVIVE_KIT_TIME
        if (this.Count(P, "revive") > 0)
        {
            foreach (dynamic Q in Gd.Iter(Gd.Values(Players)))
            {
                if (((Q.State == 2) && (Q.Id != P.Id) && (Q.Pos.DistanceTo(P.Pos) <= (1.5f * T)) && Gd.Truth(this.LineClear(P.Pos, Q.Pos))))
                {
                    P.Activity = "revive";
                    P.ActivityTarget = Q.Pos;
                    P.ReviveP += (Dt / REVIVE_KIT_TIME);
                    if ((P.ReviveP >= 1.0f) && this.Take(P, "revive", 1))
                    {
                        // MEC-07: back where they fell, with 50% HP
                        P.ReviveP = 0.0f;
                        Q.State = 0;
                        Q.Hp = (Q.Maxhp * 0.5f);
                        Q.Invuln = 1.0f;
                        P.Revives += 1;
                        this.Say(Gd.Format("%s is back", Q.Name));
                    }
                    return;
                }
            }
        }
        P.ReviveP = 0.0f;
        if (this.PickUp(P))
        {
            P.Inter = false;
            return;
        }
        // repairs need the Engineer's hammer (ITM-04) and wood (BAL-23)
        if (((P.Role == 2) && !(Gd.Truth(GameSim.InsideBase(P.Pos))) && (this.Count(P, "hammer") > 0)))
        {
            foreach (dynamic I in Gd.Iter(3))
            {
                Vector2 Dp = (Vector2)(Gd.At(DOOR_POS, I));
                if ((P.Pos.DistanceTo(Dp) <= (1.5f * T)))
                {
                    float Cap = Gd.Float((Gd.Truth(Gd.At(Broken, I)) ? (dynamic)((DOOR_HP * 0.3f)) : DOOR_HP));
                    if (((Gd.Truth(Gd.At(Broken, I)) && Gd.Truth(this.EnemyInside())) || (Gd.At(DoorPause, I) > 0) || (Gd.At(Doors, I) >= Cap) || (this.Count(P, "wood") <= 0)))
                    {
                        return;
                    }
                    P.Activity = "repair";
                    P.ActivityTarget = Dp;
                    dynamic Amt = (15.0f * Dt);
                    Doors[I] = Gd.Minf(Cap, (Gd.At(Doors, I) + Amt));
                    P.RepairAcc += Amt;
                    if ((P.RepairAcc >= 10.0f))
                    {
                        P.RepairAcc -= 10.0f;
                        this.Take(P, "wood", 1);
                    }
                    return;
                }
            }
            foreach (dynamic Tw in Gd.Iter(Towers))
            {
                if (((Tw != null) && (P.Pos.DistanceTo(Tw.Pos) <= (1.5f * T))))
                {
                    float Tcap = Gd.Float((Gd.Truth(Tw.Wrecked) ? (dynamic)((TOWER_HP * 0.3f)) : TOWER_HP));
                    if (((Gd.Truth(Tw.Wrecked) && Gd.Truth(this.EnemyNear(Tw.Pos, TOWER_LIGHT))) || (Tw.Pause > 0) || (Tw.Hp >= Tcap) || (this.Count(P, "wood") <= 0)))
                    {
                        return;
                    }
                    P.Activity = "repair";
                    P.ActivityTarget = Tw.Pos;
                    dynamic Tamt = (15.0f * Dt);
                    Tw.Hp = Gd.Minf(Tcap, (Tw.Hp + Tamt));
                    P.RepairAcc += Tamt;
                    if ((P.RepairAcc >= 10.0f))
                    {
                        P.RepairAcc -= 10.0f;
                        this.Take(P, "wood", 1);
                    }
                    return;
                }
            }
        }
        foreach (dynamic Nd in Gd.Iter(Nodes))
        {
            if (((Nd.Amount > 0) && (Nd.Pos.DistanceTo(P.Pos) <= (1.5f * T)) && Gd.Truth(this.LineClear(P.Pos, Nd.Pos, Nd.Pos))))
            {
                if ((this.Room(P, Gd.At(RES, Nd.Type)) <= 0))
                {
                    this.Note(P, "Bag full");
                    P.GatherAcc = 0.0f;
                    return;
                }
                P.Activity = "gather";
                P.ActivityTarget = Nd.Pos;
                if (((P.GatherAcc > 0) && (P.Pos.DistanceTo(P.GatherPos) > 4.0f)))
                {
                    P.GatherAcc = 0.0f;
                }
                if ((P.GatherAcc == 0.0f))
                {
                    P.GatherPos = P.Pos;
                }
                P.GatherAcc += (0.35f * Dt * (Count(P, "gathering_kit") > 0 ? 1.25f : 1f));
                if ((P.GatherAcc >= 1.0f))
                {
                    P.GatherAcc = 0.0f;
                    Nd.Amount -= 1;
                    GameSim.AddTo(P.Inv, Gd.At(RES, Nd.Type), 1);
                }
                return;
            }
        }
        // nothing in reach
        P.GatherAcc = 0.0f;
    }

    public void Hurt(dynamic P, dynamic Dmg)
    {
        // An Engineer inside the base can be hit too, but only enemies that broke in can reach them.
        if (((P.State != 0) || (P.Invuln > 0)))
        {
            return;
        }
        P.Entry = 0.0f;
        // FML-06: armour reduces the hit, never below 1 damage; BAL-13: 1 s invulnerability
        float Taken = Gd.Maxf(1.0f, Gd.Roundf((Dmg * (1.0f - Gd.Minf(0.6f, Gd.At(ARMOUR, P.Armour))))));
        P.Hp -= Taken;
        P.Taken += Taken;
        P.Invuln = 1.0f;
        P.Channel = 0.0f;
        P.RepairKitT = 0f;
        P.TowerBuildT = 0f;
        P.ReviveP = 0.0f;
        P.GatherAcc = 0.0f;
        P.CraftIdx = (-1);
        if ((P.Hp <= 0))
        {
            P.Hp = 0.0f;
            P.State = 1;
            P.Bleed = 30.0f;
            this.Say(Gd.Format("%s is down", P.Name));
        }
    }

    public void Flask(dynamic P, dynamic AimPoint)
    {
        if (((P.Role != 1) || (P.Flasks <= 0) || (P.State != 0)))
        {
            return;
        }
        dynamic Target = P;
        foreach (dynamic Q in Gd.Iter(Gd.Values(Players)))
        {
            if (((Q.State == 0) && (Q.Id != P.Id) && (Q.Pos.DistanceTo(P.Pos) <= (3 * T)) && (Q.Pos.DistanceTo(AimPoint) < T)))
            {
                Target = Q;
            }
        }
        Target.Hp = Gd.Minf(Target.Maxhp, (Target.Hp + 40.0f));
        P.Flasks -= 1;
    }

    public void Say(dynamic S)
    {
        Msg = S;
        MsgT = 6.0f;
    }

    public void Note(dynamic P, dynamic S)
    {
        P.Note = S;
        P.NoteT = 3.0f;
    }
}
