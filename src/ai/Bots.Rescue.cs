using Godot;
using HoldTilDawn;

public partial class Bots
{
    static bool FetchRescueByDay(dynamic Sim, dynamic P, dynamic Dt)
    {
        if (P.CraftIdx >= 0) return true;
        if (!Sim.RadioBuilt && Sim.SuppliesGot() == 3)
        {
            var Match = (GameSim)Sim;
            if (Match.RadioBuilder.HasValue && Match.RadioBuilder.Value != Gd.Int(P.Id)) return false;
            if (!Sim.AtBench(P)) Move(Sim, P, GameSim.WORKBENCH_POS + new Vector2(0, 24), Dt);
            else P.Inter = true;
            return true;
        }
        if (Sim.RadioBuilt || P.Role != 0) return false;
        var S = (GameSim)Sim;
        var Open = S.ChestById(Gd.Int(P.OpenChest));
        if (S.CanAccessChest(P,Open))
            for (int I=0; I<Open.Slots.Count; I++)
            {
                dynamic Item = Open.Slots[I];
                if (Item == null || System.Array.IndexOf(GameSim.COMPONENT_IDS,Gd.Str(Item[0])) < 0) continue;
                S.MoveSlot(P,new GArray("chest:"+Open.Id,I,"inv",0)); return true;
            }
        if (S.Count(P,"chest_key") == 0)
        {
            if (S.Count(P,"wood") >= 2 && S.Count(P,"stone") >= 2)
            {
                if (!S.AtBench(P)) Move(S,P,GameSim.WORKBENCH_POS+new Vector2(0,24),Dt);
                else S.Craft(P,17);
                return true;
            }
            return false;
        }
        return FetchSupply(Sim, P, Dt);
    }
}

public partial class GameSim
{
    float BotStepAccumulator;
    void TickBots(float Dt)
    {
        BotStepAccumulator += Dt;
        if (BotStepAccumulator < .05f - .000001f) return;
        Bots.Tick(this, BotStepAccumulator);
        BotStepAccumulator = 0;
    }
}
