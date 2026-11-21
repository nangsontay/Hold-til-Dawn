using Godot;
using HoldTilDawn;

public partial class Bots
{
    static bool FetchRescueByDay(dynamic Sim, dynamic P, dynamic Dt)
    {
        if (P.CraftIdx >= 0) return true;
        for (int I = 19; I < 22; I++)
        {
            // Team components are shared. Any bot with the common materials can finish an objective.
            var Recipe = (GArray)GameSim.RECIPES[I];
            bool Affordable = !Sim.RescueCrafted[I - 19];
            foreach (string Id in (GDict)Recipe[1])
                if (Sim.CraftHave(P, Id) < Gd.At(Recipe[1], Id)) Affordable = false;
            if (!Affordable) continue;
            if (!Sim.AtBench(P)) Move(Sim, P, GameSim.WORKBENCH_POS + new Vector2(0, 24), Dt);
            else Sim.Craft(P, I);
            return true;
        }
        return P.Role == 0 && FetchSupply(Sim, P, Dt);
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
