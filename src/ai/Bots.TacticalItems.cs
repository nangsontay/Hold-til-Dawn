using Godot;
using HoldTilDawn;

public partial class Bots
{
    // Called on the bot's existing decision tick; item cooldowns and limits are enforced by GameSim.
    public static bool UseTacticalItems(dynamic Sim, dynamic P, dynamic Foe, float FoeDistance)
    {
        if (Gd.Float(Gd.Get(P, "repair_kit_t", 0f)) > 0) return true;
        if (Gd.Float(Gd.Get(P, "tactical_think_at", 0f)) > Gd.Float(Sim.Clock)) return false;
        P.TacticalThinkAt = Gd.Float(Sim.Clock) + 0.5f;
        dynamic Hurt = Nearest(Sim, P, (System.Func<dynamic, dynamic>)(Q => Q.State == 0 && Q.Hp < Q.Maxhp * 0.65f), 4f * T);
        if (Hurt == null && P.Hp < P.Maxhp * 0.65f) Hurt = P;
        if (Hurt != null && Sim.UseTacticalItem(P, "healing_flask", (Vector2)Hurt.Pos)) return false;
        if (Foe != null)
        {
            if (FoeDistance < 5f * T && Sim.UseTacticalItem(P, "poison_flask", (Vector2)Foe.Pos)) return false;
            if (FoeDistance < 2.5f * T && Foe.Type != GameSim.BOSS && Gd.Float(Gd.Get(P, "next_trap_at", 0f)) <= Gd.Float(Sim.Clock) && Sim.UseTacticalItem(P, "vine_trap", (Vector2)P.Pos))
                P.NextTrapAt = Gd.Float(Sim.Clock) + 8f;
            if (P.Hp < P.Maxhp * 0.4f) Sim.UseTacticalItem(P, "haste_tonic", (Vector2)P.Pos);
        }
        else if (P.Role == 2 && Sim.Count(P, "repair_kit") > 0)
        {
            for (int I = 0; I < 3; I++)
                if (Gd.At(Sim.Doors, I) < GameSim.DOOR_HP - 80 && ((Vector2)P.Pos).DistanceTo((Vector2)Gd.At(GameSim.DOOR_POS, I)) <= 3f * T &&
                    Sim.UseTacticalItem(P, "repair_kit", (Vector2)Gd.At(GameSim.DOOR_POS, I))) return true;
        }
        return false;
    }
}
