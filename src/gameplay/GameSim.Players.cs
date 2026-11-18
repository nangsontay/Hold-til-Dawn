using Godot;
using Environment = Godot.Environment;
using FileAccess = Godot.FileAccess;
using System;
using System.Threading.Tasks;
using HoldTilDawn;

// Owner: Pham Minh Hieu. Player actions: movement credit, combat, gather/repair/revive, healing (MEC-01..13).
public partial class GameSim : RefCounted
{
    public void Craft(dynamic P, dynamic Idx)
    {
        if (((Idx < 0) || (Idx >= Gd.Size(RECIPES))))
        {
            return;
        }
        dynamic R = Gd.At(RECIPES, Idx);
        // ponytail: crafting is instant and day-only; GDD's 3 s craft time and Engineer night entry come later.
        bool BenchOk = Gd.Truth(((Gd.Contains(new GArray("day", "dusk"), Phase) && Gd.Truth(GameSim.InsideBase(P.Pos))) || ((Phase == "night") && Gd.Truth(P.Inside))));
        if (((P.State != 0) || !(Gd.Truth(BenchOk))))
        {
            this.Note(P, "Craft at the workbench inside the base (by day; Engineer at night)");
            return;
        }
        if ((Gd.At(R, 2) > Unlock))
        {
            this.Note(P, Gd.Format("%s is locked", Gd.At(R, 0)));
            return;
        }
        if ((Gd.Truth(Gd.At(R, 5)) && (P.Role != 2)))
        {
            this.Note(P, "Only an Engineer can do that");
            return;
        }
        foreach (dynamic Res in Gd.Iter(Gd.At(R, 1)))
        {
            if ((Gd.At(P.Inv, Res) < Gd.At(Gd.At(R, 1), Res)))
            {
                this.Note(P, Gd.Format("Not enough %s for %s", new GArray(Res, Gd.At(R, 0))));
                return;
            }
        }
        dynamic Dead = Gd.Filter(Gd.Values(Players), (Func<dynamic, dynamic>)((Q) => (Q.State == 2)));
        if (((Gd.At(R, 3) == "revive") && Gd.Truth(Gd.IsEmpty(Dead))))
        {
            this.Note(P, "No dead teammate to revive");
            return;
        }
        foreach (dynamic Res in Gd.Iter(Gd.At(R, 1)))
        {
            P.Inv[Res] -= Gd.At(Gd.At(R, 1), Res);
        }
        if (Gd.Equal(Gd.At(R, 3), "armour"))
        {
            P.Armour = Gd.Maxi(P.Armour, Gd.At(R, 4));
        }
        else if (Gd.Equal(Gd.At(R, 3), "weapon"))
        {
            P.Weapon = Gd.Maxi(P.Weapon, Gd.At(R, 4));
        }
        else if (Gd.Equal(Gd.At(R, 3), "bandage"))
        {
            P.Inv.Bandage += 1;
        }
        else if (Gd.Equal(Gd.At(R, 3), "base"))
        {
            BaseHp = Gd.Minf(BASE_HP, (BaseHp + Gd.At(R, 4)));
        }
        else if (Gd.Equal(Gd.At(R, 3), "revive"))
        {
            dynamic Q = Gd.At(Dead, 0);
            Q.State = 0;
            Q.Hp = (Q.Maxhp * 0.5f);
            this.Teleport(Q, (Gd.At(DOOR_POS, 0) + (Gd.At(DOOR_DIR, 0) * T)));
            this.Say(Gd.Format("%s is back (Revival kit)", Q.Name));
        }
        this.Note(P, ("Crafted " + Gd.At(R, 0)));
    }

    // --- players ------------------------------------------------------------
    public void ResetPlayer(dynamic P)
    {
        P.Hp = Gd.At(ROLE_HP, P.Role);
        P.Maxhp = P.Hp;
        // 0 standing, 1 downed, 2 dead
        P.State = 0;
        P.Bleed = 0.0f;
        P.Inv = new GDict { { "wood", 0 }, { "stone", 0 }, { "herb", 0 }, { "metal", 0 }, { "bandage", 0 } };
        P.Weapon = 0;
        P.Armour = 0;
        P.Flasks = ((P.Role == 1) ? (dynamic)(3) : 0);
        P.Invuln = 0.0f;
        P.AtkCd = 0.0f;
        P.Swing = 0.0f;
        P.Channel = 0.0f;
        P.ReviveP = 0.0f;
        P.GatherAcc = 0.0f;
        P.RepairAcc = 0.0f;
        P.Aim = 0.0f;
        P.Atk = false;
        P.Inter = false;
        P.Kills = 0;
        P.Dmg = 0.0f;
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
        this.Teleport(P, (Gd.MakeVector2(Rng.RandfRange((-2), 2), Rng.RandfRange((-2), 2)) * T));
    }

    public void TickPlayers(dynamic Dt)
    {
        foreach (dynamic P in Gd.Iter(Gd.Values(Players)))
        {
            P.Activity = "idle";
            P.MoveCredit = Gd.Minf(((SPEED * ((P.Role == 0) ? (dynamic)(1.2f) : 1.0f)) * 0.2f), (P.MoveCredit + ((SPEED * ((P.Role == 0) ? (dynamic)(1.2f) : 1.0f)) * Dt)));
            P.Invuln -= Dt;
            P.AtkCd -= Dt;
            P.Swing -= Dt;
            P.NoteT -= Dt;
            if ((P.State == 1))
            {
                P.Bleed -= Dt;
                if ((P.Bleed <= 0))
                {
                    P.State = 2;
                    this.Say(Gd.Format("%s died - craft a Revival kit at the workbench", P.Name));
                }
            }
            if ((P.State != 0))
            {
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
                    if (((P.Channel <= 0) && (P.Inv.Bandage > 0)))
                    {
                        P.Inv.Bandage -= 1;
                        P.Hp = Gd.Minf(P.Maxhp, (P.Hp + (30.0f * ((P.Role == 1) ? (dynamic)(1.5f) : 1.0f))));
                    }
                }
            }
            this.TickEntry(P, Dt);
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
            this.Note(P, "Inside: C to craft, hold F at a door to leave");
        }
        else
        {
            this.Teleport(P, (Gd.At(DOOR_POS, Door) + (Gd.At(DOOR_DIR, Door) * T)));
            P.Invuln = 0.5f;
        }
    }

    public void Attack(dynamic P)
    {
        P.AtkCd = (1.0f / (1.5f * ((P.Role == 0) ? (dynamic)(1.25f) : 1.0f)));
        P.Swing = 0.25f;
        dynamic Dir = Vector2.FromAngle(P.Aim);
        foreach (dynamic E in Gd.Iter(Enemies))
        {
            Vector2 D = (Vector2)((E.Pos - P.Pos));
            if (((Gd.Length(D) <= ((1.5f * T) + E.R)) && ((Gd.Length(D) < (0.5f * T)) || (Dir.Dot(D.Normalized()) >= Gd.Cos((Mathf.Pi / 4)))) && Gd.Truth(this.LineClear(P.Pos, E.Pos))))
            {
                this.Hit(E, Gd.At(WEAPON_DMG, P.Weapon), P);
            }
        }
        Enemies = Gd.Filter(Enemies, (Func<dynamic, dynamic>)((E) => (E.Hp > 0)));
    }

    public void Hit(dynamic E, dynamic Dmg, dynamic P)
    {
        E.Hp -= Dmg;
        E.Nodmg = 0.0f;
        P.Dmg += Dmg;
        if ((((E.Type == GRUNT) || (E.Type == BOSS)) && ((E.State <= 1) || (E.State == 4))))
        {
            // ponytail: threat is cumulative until the leash breaks, not a sliding 5 s window (FML-09).
            E.Threat[P.Id] = (Gd.Get(E.Threat, P.Id, 0.0f) + Dmg);
            if (((E.State == 0) || (E.State == 4)))
            {
                E.State = 1;
                E.Anchor = E.Pos;
                E.Target = P.Id;
            }
            else if ((Gd.At(E.Threat, P.Id) > (1.2f * Gd.Get(E.Threat, E.Target, 0.0f))))
            {
                E.Target = P.Id;
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
                    this.Say(Gd.Format("%s revived %s", new GArray(P.Name, Q.Name)));
                }
                return;
            }
        }
        P.ReviveP = 0.0f;
        if (((P.Role == 2) && !(Gd.Truth(GameSim.InsideBase(P.Pos)))))
        {
            foreach (dynamic I in Gd.Iter(3))
            {
                Vector2 Dp = (Vector2)(Gd.At(DOOR_POS, I));
                if ((P.Pos.DistanceTo(Dp) <= (1.5f * T)))
                {
                    float Cap = Gd.Float((Gd.Truth(Gd.At(Broken, I)) ? (dynamic)((DOOR_HP * 0.3f)) : DOOR_HP));
                    if (((Gd.Truth(Gd.At(Broken, I)) && Gd.Truth(this.EnemyInside())) || (Gd.At(DoorPause, I) > 0) || (Gd.At(Doors, I) >= Cap) || (P.Inv.Wood <= 0)))
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
                        P.Inv.Wood -= 1;
                    }
                    return;
                }
            }
            foreach (dynamic Tw in Gd.Iter(Towers))
            {
                if (((Tw != null) && (P.Pos.DistanceTo(Tw.Pos) <= (1.5f * T))))
                {
                    float Tcap = Gd.Float((Gd.Truth(Tw.Wrecked) ? (dynamic)((TOWER_HP * 0.3f)) : TOWER_HP));
                    if (((Gd.Truth(Tw.Wrecked) && Gd.Truth(this.EnemyNear(Tw.Pos, TOWER_LIGHT))) || (Tw.Pause > 0) || (Tw.Hp >= Tcap) || (P.Inv.Wood <= 0)))
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
                        P.Inv.Wood -= 1;
                    }
                    return;
                }
            }
        }
        foreach (dynamic Nd in Gd.Iter(Nodes))
        {
            if (((Nd.Amount > 0) && (Nd.Pos.DistanceTo(P.Pos) <= (1.5f * T)) && Gd.Truth(this.LineClear(P.Pos, Nd.Pos, Nd.Pos))))
            {
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
                P.GatherAcc += (0.35f * Dt);
                if ((P.GatherAcc >= 1.0f))
                {
                    P.GatherAcc = 0.0f;
                    Nd.Amount -= 1;
                    P.Inv[Gd.At(RES, Nd.Type)] += 1;
                }
                return;
            }
        }
        // nothing in reach
        P.GatherAcc = 0.0f;
    }

    public void Hurt(dynamic P, dynamic Dmg)
    {
        // ponytail: an Engineer inside is untouchable; let breached enemies hit them if playtests ask.
        if (((P.State != 0) || (P.Invuln > 0) || Gd.Truth(P.Inside)))
        {
            return;
        }
        P.Entry = 0.0f;
        P.Hp -= Gd.Maxf(1.0f, Gd.Roundf((Dmg * (1.0f - Gd.At(ARMOUR, P.Armour)))));
        P.Invuln = 1.0f;
        P.Channel = 0.0f;
        P.ReviveP = 0.0f;
        P.GatherAcc = 0.0f;
        if ((P.Hp <= 0))
        {
            P.Hp = 0.0f;
            P.State = 1;
            P.Bleed = 30.0f;
            this.Say(Gd.Format("%s is DOWN - hold E next to them to revive", P.Name));
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
