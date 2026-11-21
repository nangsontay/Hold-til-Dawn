using Godot;
using System;
using System.Collections.Generic;
using HoldTilDawn;

public partial class GameSim
{
    // Small bounded lists, checked at 4 Hz (traps) / 2 Hz (areas), without physics nodes or navigation changes.
    public readonly GArray TacticalAreas = new();
    public readonly GArray VineTraps = new();
    float NextTrapCheck;
    readonly HashSet<int> Poisoned = new(), Healed = new();
    public const int MAX_TACTICAL_AREAS = 16;
    public const int MAX_VINE_TRAPS = 12;

    public static bool IsTacticalUse(string Id) => Id is "vine_trap" or "poison_flask" or "healing_flask" or "haste_tonic" or "repair_kit" or "explosive_arrow";

    public static string[] TacticalDescription(string Id) => Id switch
    {
        "long_spear" => new[] { "22 damage, reach 2.6, hits two" },
        "stun_maul" => new[] { "30 damage, stuns 0.8 s" },
        "vine_trap" => new[] { "Roots a foe for 2.5 s" },
        "poison_flask" => new[] { "8 damage/s for 5 s" },
        "healing_flask" => new[] { "Heals 8 HP/s for 5 s" },
        "haste_tonic" => new[] { "+20% speed for 8 s" },
        "gathering_kit" => new[] { "Gather 20% faster" },
        "explosive_arrow" => new[] { "32 blast damage" },
        "repair_kit" => new[] { "+100 HP to a door, wall or tower" },
        _ => Array.Empty<string>()
    };

    public float PlayerSpeed(dynamic P) => Gd.Float(SPEED) * (Gd.Int(P.Role) == 0 ? 1.2f : 1f) *
        (Gd.Float(Gd.Get(P, "haste_until", 0f)) > Gd.Float(Clock) ? 1.2f : 1f);

    public void ResetTacticalItems()
    {
        TacticalAreas.Clear(); VineTraps.Clear(); NextTrapCheck = 0;
        foreach (dynamic P in Gd.Iter(Gd.Values(Players))) { P.RepairKitT = 0f; P.HasteUntil = 0f; }
    }

    public string SelectedItem(dynamic P)
    {
        dynamic S = Gd.At(P.Inv, Gd.Int(P.Sel));
        return S == null ? "" : Gd.Str(Gd.At(S, 0));
    }

    public void UseSelectedItem(dynamic P, Vector2 AimPoint) => UseTacticalItem(P, SelectedItem(P), AimPoint);

    public bool UseTacticalItem(dynamic P, string Id, Vector2 AimPoint)
    {
        if (!IsTacticalUse(Id) || !AimPoint.IsFinite() || !Gd.Contains(new GArray("day", "dusk", "night"), Phase) || Gd.Int(P.State) != 0 || Gd.Truth(P.Inside) ||
            Gd.Int(P.CraftIdx) >= 0 || Gd.Float(P.Channel) > 0 || Count(P, Id) <= 0 ||
            Gd.Float(Gd.Get(P, "item_cd", 0f)) > Gd.Float(Clock) || Gd.Float(Gd.Get(P, "repair_kit_t", 0f)) > 0) return false;
        Vector2 Pos = (Vector2)P.Pos;
        Vector2 At = Pos + (AimPoint - Pos).LimitLength((Id == "vine_trap" ? 2f : 5f) * T);
        float Now = Gd.Float(Clock);
        if (Id == "haste_tonic")
        {
            if (Gd.Float(Gd.Get(P, "haste_until", 0f)) > Now) { Note(P, "Already active"); return false; }
            P.HasteUntil = Now + 8f;
        }
        else if (Id == "repair_kit") return BeginFieldRepair(P, AimPoint);
        else if (Id == "explosive_arrow")
        {
            if (Count(P, "bow") + Count(P, "starter_bow") == 0) { Note(P, "Needs a bow"); return false; }
            if (Gd.Float(P.AtkCd) > 0) return false;
            Vector2 Dir = (AimPoint - Pos).Normalized();
            if (Dir == Vector2.Zero) Dir = Vector2.FromAngle(Gd.Float(P.Aim));
            Arrows.Add(new GDict { { "pos", Pos + Dir * 0.4f * T }, { "vel", Dir * ARROW_SPEED * T },
                { "dmg", 32f }, { "left", 7.6f * T }, { "owner", P.Id }, { "explosive", true } });
            P.Aim = Dir.Angle(); P.Swing = 0.25f; P.AtkCd = 1f;
        }
        else
        {
            if (!Gd.Truth(LineClear(Pos, At)) || !IslandMap.OnLand(At, 8f) ||
                (Id == "vine_trap" && ((Vector2)World.ResolveFast(At, 8f)).DistanceTo(At) > 1f))
            { Note(P, "Blocked"); return false; }
            if (Id == "vine_trap")
            {
                int Own = 0;
                foreach (dynamic Trap in Gd.Iter(VineTraps)) if (Gd.Equal(Trap.Owner, P.Id)) Own++;
                if (Own >= 3 || VineTraps.Count >= MAX_VINE_TRAPS) { Note(P, "Trap limit"); return false; }
                VineTraps.Add(new GDict { { "pos", At }, { "owner", P.Id }, { "until", Now + 120f } });
            }
            else
            {
                if (TacticalAreas.Count >= MAX_TACTICAL_AREAS) { Note(P, "Wait a moment"); return false; }
                TacticalAreas.Add(new GDict { { "pos", At }, { "owner", P.Id }, { "kind", Id == "poison_flask" ? "poison" : "heal" },
                    { "until", Now + 5f }, { "next", Now + 0.5f } });
            }
        }
        Take(P, Id, 1); P.ItemCd = Now + 0.6f;
        // the throw / drink plays as a short swing of the character
        if (Id != "explosive_arrow") P.Swing = 0.25f;
        Note(P, "Used " + ItemName(Id));
        return true;
    }

    public void TickTacticalItems()
    {
        float Now = Gd.Float(Clock);
        if (Now >= NextTrapCheck)
        {
            NextTrapCheck = Now + 0.25f;
            for (int I = VineTraps.Count - 1; I >= 0; I--)
            {
                dynamic Trap = Gd.At(VineTraps, I);
                if (Gd.Float(Trap.Until) <= Now) { VineTraps.RemoveAt(I); continue; }
                foreach (dynamic E in Gd.Iter(Enemies))
                {
                    if (E.Hp <= 0 || Gd.Int(E.Type) == BOSS || !Hittable(E) || ((Vector2)E.Pos).DistanceTo((Vector2)Trap.Pos) > Gd.Float(E.R) + 16f) continue;
                    E.StunUntil = Math.Max(Gd.Float(Gd.Get(E, "stun_until", 0f)), Now + 2.5f);
                    VineTraps.RemoveAt(I); break;
                }
            }
        }
        // Each actor receives a given area effect once per half-second, even where areas overlap.
        if (TacticalAreas.Count == 0) return;
        Poisoned.Clear(); Healed.Clear();
        for (int I = TacticalAreas.Count - 1; I >= 0; I--)
        {
            dynamic Area = Gd.At(TacticalAreas, I);
            float Until = Gd.Float(Area.Until), Next = Gd.Float(Area.Next);
            if (Now >= Next && Next <= Until && Now <= Until + 0.01f)
            {
                Area.Next = Now + 0.5f;
                dynamic Owner = Gd.Get(Players, Area.Owner);
                if (Gd.Str(Area.Kind) == "poison" && Owner != null)
                {
                    foreach (dynamic E in Gd.Iter(Enemies))
                        if (E.Hp > 0 && Hittable(E) && Gd.Float(Gd.Get(E, "area_poison_at", 0f)) <= Now && ((Vector2)E.Pos).DistanceTo((Vector2)Area.Pos) <= 1.8f * T + Gd.Float(E.R) &&
                            Gd.Truth(LineClear(Area.Pos, E.Pos)) && Poisoned.Add(Gd.Int(E.Id))) { E.AreaPoisonAt = Now + 0.5f; Hit(E, 4f, Owner); }
                }
                else if (Gd.Str(Area.Kind) == "heal")
                {
                    foreach (dynamic Q in Gd.Iter(Gd.Values(Players)))
                        if (Gd.Int(Q.State) == 0 && Gd.Float(Gd.Get(Q, "area_heal_at", 0f)) <= Now && ((Vector2)Q.Pos).DistanceTo((Vector2)Area.Pos) <= 1.8f * T &&
                            Gd.Truth(LineClear(Area.Pos, Q.Pos)) && Healed.Add(Gd.Int(Q.Id))) { Q.AreaHealAt = Now + 0.5f; Q.Hp = Math.Min(Gd.Float(Q.Maxhp), Gd.Float(Q.Hp) + 4f); }
                }
            }
            if (Now >= Until) TacticalAreas.RemoveAt(I);
        }
        // Hit() owns drops, boss progression, threat and player statistics.
        Enemies = Gd.Filter(Enemies, (Func<dynamic, dynamic>)(E => E.Hp > 0));
    }

    public void ExplodeArrow(dynamic Arrow)
    {
        Vector2 At = (Vector2)Arrow.Pos;
        dynamic Owner = Gd.Get(Players, Arrow.Owner);
        if (Owner != null)
            foreach (dynamic E in Gd.Iter(Enemies))
                if (E.Hp > 0 && Hittable(E) && ((Vector2)E.Pos).DistanceTo(At) <= 1.5f * T + Gd.Float(E.R) && Gd.Truth(LineClear(At, E.Pos))) Hit(E, 32f, Owner);
        if (TacticalAreas.Count < MAX_TACTICAL_AREAS)
            TacticalAreas.Add(new GDict { { "pos", At }, { "owner", Arrow.Owner }, { "kind", "blast" }, { "until", Gd.Float(Clock) + 0.3f }, { "next", float.PositiveInfinity } });
    }

    public GArray TacticalSnapshot()
    {
        var Out = new GArray();
        // [kind, pos, seconds left, owner]
        foreach (dynamic A in Gd.Iter(TacticalAreas)) Out.Add(new GArray(A.Kind, A.Pos, Math.Max(0f, Gd.Float(A.Until) - Gd.Float(Clock)), A.Owner));
        foreach (dynamic A in Gd.Iter(VineTraps)) Out.Add(new GArray("trap", A.Pos, Math.Max(0f, Gd.Float(A.Until) - Gd.Float(Clock)), A.Owner));
        return Out;
    }

    public void AttackLongSpear(dynamic P)
    {
        Vector2 Pos = (Vector2)P.Pos, Dir = Vector2.FromAngle(Gd.Float(P.Aim));
        var Hits = new List<(float Distance, object Enemy)>();
        foreach (dynamic E in Gd.Iter(Enemies))
        {
            Vector2 Delta = (Vector2)E.Pos - Pos;
            float Along = Delta.Dot(Dir), Side = MathF.Abs(Delta.Cross(Dir));
            if (Hittable(E) && Along >= 0 && Along <= 2.6f * T + Gd.Float(E.R) && Side <= 10f + Gd.Float(E.R) && Gd.Truth(LineClear(Pos, E.Pos)))
                Hits.Add((Along, (object)E));
        }
        Hits.Sort((A, B) => A.Distance.CompareTo(B.Distance));
        for (int I = 0; I < Math.Min(2, Hits.Count); I++) Hit((dynamic)Hits[I].Enemy, 22f, P);
        P.AtkCd = Gd.Int(P.Role) == 0 ? 0.8f : 1f; P.Swing = 0.25f;
        Enemies = Gd.Filter(Enemies, (Func<dynamic, dynamic>)(E => E.Hp > 0));
    }

    bool BeginFieldRepair(dynamic P, Vector2 Aim)
    {
        if (Gd.Int(P.Role) != 2 || Gd.Truth(InsideBase(P.Pos))) { Note(P, "Use outside the base"); return false; }
        dynamic Target = null; string Kind = ""; int Door = -1; float Best = T;
        for (int I = 0; I < 3; I++)
        {
            Vector2 At = (Vector2)Gd.At(DOOR_POS, I);
            float D = At.DistanceTo(Aim);
            if (D < Best && FieldDoorCap(I) > Gd.Float(Gd.At(Doors, I))) { Best = D; Door = I; Kind = "door"; }
        }
        foreach (dynamic W in Gd.Iter(BuiltWalls))
            if (((Vector2)W.Pos).DistanceTo(Aim) < Best && Gd.Float(W.Hp) < Gd.Float(W.Maxhp)) { Target = W; Best = ((Vector2)W.Pos).DistanceTo(Aim); Kind = "wall"; }
        foreach (dynamic W in Gd.Iter(Towers))
            if (W != null && ((Vector2)W.Pos).DistanceTo(Aim) < Best && FieldTowerCap(W) > Gd.Float(W.Hp)) { Target = W; Best = ((Vector2)W.Pos).DistanceTo(Aim); Kind = "tower"; }
        Vector2 Spot = Kind == "door" ? (Vector2)Gd.At(DOOR_POS, Door) : Target == null ? Vector2.Inf : (Vector2)Target.Pos;
        Vector2 Approach = Spot.IsFinite() ? Spot.MoveToward((Vector2)P.Pos, T) : Spot;
        if (Kind == "" || Spot.DistanceTo((Vector2)P.Pos) > 3f * T || !Gd.Truth(LineClear(P.Pos, Approach)))
        { Note(P, "Nothing to repair here"); return false; }
        P.RepairKitKind = Kind; P.RepairKitDoor = Door; P.RepairKitTarget = Target;
        P.RepairKitT = 3f; P.RepairKitPos = P.Pos; P.RepairKitSpot = Spot; P.Atk = false;
        Note(P, "Repairing..."); return true;
    }

    float FieldDoorCap(int I) => Gd.Float(Gd.At(DoorPause, I)) > 0 || (Gd.Truth(Gd.At(Broken, I)) && Gd.Truth(EnemyInside())) ? 0f :
        Gd.Float(DOOR_HP) * (Gd.Truth(Gd.At(Broken, I)) ? 0.3f : 1f);
    float FieldTowerCap(dynamic W) => Gd.Float(W.Pause) > 0 || (Gd.Truth(W.Wrecked) && Gd.Truth(EnemyNear(W.Pos, TOWER_LIGHT))) ? 0f :
        Gd.Float(TOWER_HP) * (Gd.Truth(W.Wrecked) ? 0.3f : 1f);

    public bool TickFieldRepair(dynamic P, float Dt)
    {
        float Left = Gd.Float(Gd.Get(P, "repair_kit_t", 0f));
        if (Left <= 0) return false;
        if (Gd.Int(P.State) != 0 || Gd.Truth(P.Atk) || Gd.Truth(P.Inside) || ((Vector2)P.Pos).DistanceTo((Vector2)P.RepairKitPos) > 4f || Count(P, "repair_kit") == 0)
        { P.RepairKitT = 0f; Note(P, "Repair cancelled"); return false; }
        P.Activity = "repair"; P.ActivityTarget = P.RepairKitSpot;
        P.RepairKitT = Math.Max(0f, Left - Dt);
        if (Gd.Float(P.RepairKitT) > 0) return true;
        string Kind = Gd.Str(P.RepairKitKind); dynamic Target = P.RepairKitTarget;
        float Before = 0f, After = 0f;
        if (Kind == "door")
        {
            int I = Gd.Int(P.RepairKitDoor); Before = Gd.Float(Gd.At(Doors, I)); After = Math.Min(FieldDoorCap(I), Before + 100f);
            if (After > Before) Doors[I] = After;
        }
        else if (Target != null && ((Kind == "wall" && Gd.Contains(BuiltWalls, Target)) || (Kind == "tower" && Gd.Contains(Towers, Target))))
        {
            Before = Gd.Float(Target.Hp); After = Math.Min(Kind == "wall" ? Gd.Float(Target.Maxhp) : FieldTowerCap(Target), Before + 100f);
            if (After > Before) Target.Hp = After;
        }
        if (After > Before) { Take(P, "repair_kit", 1); Note(P, "+" + MathF.Round(After - Before) + " HP"); }
        else Note(P, "Nothing to repair");
        return true;
    }
}
