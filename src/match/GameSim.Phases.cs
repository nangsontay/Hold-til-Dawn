using Godot;
using Environment = Godot.Environment;
using FileAccess = Godot.FileAccess;
using System;
using System.Threading.Tasks;
using HoldTilDawn;

// Owner: Pham Minh Hieu. Day / dusk / night cycle, win and loss, extraction (MEC-14..22, FML-13).
public partial class GameSim : RefCounted
{
    // --- phases -------------------------------------------------------------
    public void Dawn()
    {
        Phase = "day";
        TimeLeft = DAY_MAX;
        Gd.Clear(Votes);
        Broken = new GArray(false, false, false);
        foreach (dynamic Tw in Gd.Iter(Towers))
        {
            if ((Tw != null))
            {
                Tw.Wrecked = false;
            }
        }
        foreach (dynamic Nd in Gd.Iter(Nodes))
        {
            Nd.Amount = 5;
        }
        foreach (dynamic P in Gd.Iter(Gd.Values(Players)))
        {
            P.Inside = false;
            P.Entry = 0.0f;
            if ((P.Role == 1))
            {
                P.Flasks = 3;
            }
        }
        dynamic Idx = new GArray(0, 1, 2);
        Gd.Shuffle(Idx);
        Active = Gd.Slice(Idx, 0, Gd.Mini(3, K));
        Gd.Sort(Active);
        dynamic Names = Gd.PackedStringArray(Gd.Map(Active, (Func<dynamic, dynamic>)((I) => Gd.At(DOOR_NAMES, I))));
        dynamic S = Gd.Format("Radio: night %d/5 - %s %s will be attacked", new GArray(K, ((Gd.Size(Names) == 1) ? (dynamic)("door") : "doors"), Gd.Join(", ", Names)));
        if (((K == 2) || (K == 4)))
        {
            // FML-05: tonight's boss is announced at dawn so the team can prepare
            S += Gd.Format(", and specimen %s is coming", GameSim.BossTitle(this.BossKindFor(K)).ToUpper());
        }
        if ((K == 5))
        {
            S = "Radio: We can see your light. Keep one landing pad clear.";
        }
        if ((Unlock > ShownUnlock))
        {
            ShownUnlock = Unlock;
            S += "  |  New recipes unlocked (C)";
        }
        this.Say(S);
    }

    public void Dusk()
    {
        Phase = "dusk";
        TimeLeft = DUSK;
        foreach (dynamic E in Gd.Iter(Enemies))
        {
            if (Gd.Truth(Gd.Has(E, "home")))
            {
                Gd.Erase(E, "home");
                E.State = 0;
                Gd.Clear(E.Hits);
                E.Door = this.NearestActiveDoor(E.Pos);
            }
        }
        if ((Gd.Truth(FreeTower) && (K == 1)))
        {
            FreeTower = false;
            int D = Gd.Int(Gd.At(Active, 0));
            this.PlaceTower(D, (Gd.At(DOOR_POS, D) + ((Gd.At(DOOR_DIR, D) * 5.0f) * T)));
        }
        this.Say(Gd.Format("Dusk: night falls in %d s - everyone out of the base", DUSK));
    }

    public void Night()
    {
        Phase = "night";
        TimeLeft = Gd.At(NIGHT_LEN, (K - 1));
        Elapsed = 0.0f;
        Wave = 0;
        NextWave = FIRST_WAVE_DELAY;
        Carry = 0.0f;
        BossSpawned = false;
        Ext = (-1.0f);
        PadClear = new GArray(0.0f, 0.0f, 0.0f);
        foreach (dynamic P in Gd.Iter(Gd.Values(Players)))
        {
            P.Inside = false;
            if (Gd.Truth(GameSim.InsideBase(P.Pos, (0.5f * T))))
            {
                dynamic D = this.NearestDoor(P.Pos);
                this.Teleport(P, (Gd.At(DOOR_POS, D) + (Gd.At(DOOR_DIR, D) * T)));
            }
        }
        this.Say(Gd.Format("Night %d - hold the doors!", K));
    }

    public void NightCleared()
    {
        if ((K == 1))
        {
            Unlock = Gd.Maxi(Unlock, 1);
        }
        Gd.Clear(Enemies);
        Gd.Clear(Proj);
        Gd.Clear(Tele);
        K += 1;
        this.Dawn();
    }

    public void End(dynamic Win, dynamic Why)
    {
        // section 2.5: the result screen stays until the players go back to the lobby
        Phase = "result";
        TimeLeft = 0.0f;
        Result = new GArray(Win, Why, K);
        Msg = (((Gd.Truth(Win) ? (dynamic)("MISSION COMPLETE - ") : "BEACON LOST - ") + Why) + Gd.Format(" (night %d)", K));
        Gd.Clear(Enemies);
        Gd.Clear(Proj);
        Gd.Clear(Tele);
    }

    public void ToLobby()
    {
        Phase = "lobby";
        Msg = "";
        Result = null;
        Gd.Clear(Drops);
        Gd.Clear(Enemies);
        Gd.Clear(Proj);
        Gd.Clear(Tele);
        foreach (dynamic P in Gd.Iter(Gd.Values(Players)))
        {
            this.ResetPlayer(P);
        }
    }

    public void CheckEnd(dynamic Dt)
    {
        if ((BaseHp <= 0))
        {
            this.End(false, ((LastDoor >= 0) ? (dynamic)(Gd.Format("Generator destroyed at door %s", Gd.At(DOOR_NAMES, LastDoor))) : "Generator destroyed"));
            return;
        }
        if (Gd.Truth(this.TeamWiped()))
        {
            this.End(false, "Team wiped");
            return;
        }
        if (Gd.Truth(BossDead))
        {
            BossDead = false;
            BossesKilled += 1;
            Unlock = Gd.Maxi(Unlock, 2);
            this.NightCleared();
            return;
        }
        if ((TimeLeft > 0))
        {
            return;
        }
        if (((K == 2) || (K == 4)))
        {
            this.End(false, "Specimen still alive at dawn");
            return;
        }
        if ((K < 5))
        {
            this.NightCleared();
            return;
        }
        // night 5 extraction (FML-13)
        dynamic Clear = new GArray();
        foreach (dynamic I in Gd.Iter(3))
        {
            Gd.Append(Clear, this.PadIsClear(I));
        }
        if ((Ext < 0))
        {
            if (Gd.Contains(Clear, true))
            {
                this.End(true, Gd.Format("Helicopter landed at pad %s", Gd.At(DOOR_NAMES, Gd.Find(Clear, true))));
            }
            else if ((BaseHp > 500))
            {
                Ext = 60.0f;
                this.Say("Extension: keep one landing pad clear for 10 s!");
            }
            else
            {
                this.End(false, "No landing pad was clear");
            }
            return;
        }
        Ext -= Dt;
        foreach (dynamic I in Gd.Iter(3))
        {
            PadClear[I] = (Gd.Truth(Gd.At(Clear, I)) ? (dynamic)((Gd.At(PadClear, I) + Dt)) : 0.0f);
            if ((Gd.At(PadClear, I) >= 10.0f))
            {
                this.End(true, Gd.Format("Helicopter landed at pad %s", Gd.At(DOOR_NAMES, I)));
                return;
            }
        }
        if ((Ext <= 0))
        {
            this.End(false, "Extension ended without a clear pad");
        }
    }

    public dynamic TeamWiped()
    {
        return (!(Gd.Truth(Gd.IsEmpty(Players))) && Gd.Truth(Gd.IsEmpty(Gd.Filter(Gd.Values(Players), (Func<dynamic, dynamic>)((P) => (P.State == 0))))));
    }

    public dynamic NearPad(dynamic P)
    {
        foreach (dynamic I in Gd.Iter(3))
        {
            if ((P.DistanceTo(GameSim.PadPos(I)) < (PAD_R + T)))
            {
                return true;
            }
        }
        return false;
    }

    public dynamic PadIsClear(dynamic I)
    {
        dynamic Pad = GameSim.PadPos(I);
        foreach (dynamic E in Gd.Iter(Enemies))
        {
            if ((E.Pos.DistanceTo(Pad) < PAD_R))
            {
                return false;
            }
        }
        return true;
    }

    // MEC-17 / BAL-25: once no door is broken, players inside (other than the Engineer who entered) get a
    // 5 s warning and are then moved to the nearest door.
    public dynamic BreachEndT = (-1.0f);

    public void TickBreachEnd(dynamic Dt)
    {
        dynamic Caught = Gd.Filter(Gd.Values(Players), (Func<dynamic, dynamic>)((P) => ((P.State != 2) && !Gd.Truth(P.Inside) && Gd.Truth(GameSim.InsideBase(P.Pos)))));
        if ((Gd.Truth(this.AnyBreach()) || Gd.Truth(Gd.IsEmpty(Caught))))
        {
            BreachEndT = (-1.0f);
            return;
        }
        if ((BreachEndT < 0))
        {
            BreachEndT = 5.0f;
            this.Say("The doors hold again - everyone out in 5 s!");
        }
        BreachEndT -= Dt;
        if ((BreachEndT > 0))
        {
            return;
        }
        BreachEndT = (-1.0f);
        foreach (dynamic P in Gd.Iter(Caught))
        {
            dynamic D = this.NearestDoor(P.Pos);
            this.Teleport(P, (Gd.At(DOOR_POS, D) + (Gd.At(DOOR_DIR, D) * T)));
        }
    }

    public dynamic AnyBreach()
    {
        return Gd.Any(Doors, (Func<dynamic, dynamic>)((D) => (D <= 0)));
    }

    public dynamic EnemyInside()
    {
        foreach (dynamic E in Gd.Iter(Enemies))
        {
            if (((E.State == 3) && Gd.Truth(GameSim.InsideBase(E.Pos))))
            {
                return true;
            }
        }
        return false;
    }

    public dynamic NearestDoor(dynamic Pos)
    {
        dynamic Best = 0;
        foreach (dynamic I in Gd.Iter(3))
        {
            if ((Pos.Dot(Gd.At(DOOR_DIR, I)) > Pos.Dot(Gd.At(DOOR_DIR, Best))))
            {
                Best = I;
            }
        }
        return Best;
    }

    public void Teleport(dynamic P, dynamic Pos)
    {
        P.Pos = World.Resolve(Pos, (0.3f * T));
        P.NavT = 0.0f;
        P.Tp += 1;
    }
}
