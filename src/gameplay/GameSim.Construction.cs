using Godot;
using System;
using HoldTilDawn;

public partial class GameSim
{
    public const float TOWER_BUILD_TIME = 6f;
    public readonly GArray Construction = new();
    public void CancelAllConstruction()
    {
        Construction.Clear();
        foreach (GDict P in ((GDict)Players).Values()) P["tower_build_t"] = 0f;
    }
    bool ConstructionSlotTaken(int Slot)
    {
        foreach (GDict Job in Construction) if (Gd.Int(Job.Get("slot")) == Slot) return true;
        return false;
    }
    bool ReserveTower(dynamic P, int Slot, Vector2 At)
    {
        foreach (GDict Job in Construction)
            if (Gd.Int(Job.Get("slot")) == Slot || Gd.Int(Job.Get("owner")) == Gd.Int(P.Id)
                || ((Vector2)Job.Get("pos")).DistanceTo(At) < 2 * T) return false;
        Construction.Add(new GDict { { "slot", Slot }, { "owner", P.Id }, { "pos", At },
            { "origin", P.Pos }, { "taken", P.Taken }, { "left", TOWER_BUILD_TIME } });
        P.TowerBuildT = TOWER_BUILD_TIME;
        Note(P, "Preparing dirt foundation and tower: 6 seconds. Stand still.");
        return true;
    }
    public bool TickTowerConstruction(dynamic P, float Dt)
    {
        for (int I = Construction.Count - 1; I >= 0; I--)
        {
            var Job = (GDict)Construction[I];
            if (Gd.Int(Job.Get("owner")) != Gd.Int(P.Id)) continue;
            Vector2 At = (Vector2)Job.Get("pos");
            if (P.State != 0 || P.Role != 2 || Count(P, "hammer") <= 0 || Gd.Truth(P.Inside) || P.CraftIdx >= 0
                || Gd.Truth(P.Atk) || P.Taken > Job.Get("taken")
                || ((Vector2)P.Pos).DistanceTo((Vector2)Job.Get("origin")) > 4)
            {
                Construction.RemoveAt(I); P.TowerBuildT = 0f;
                Note(P, "Construction interrupted; materials kept"); return false;
            }
            P.Activity = "repair"; P.ActivityTarget = At;
            float Left = Math.Max(0, Gd.Float(Job.Get("left")) - Dt);
            Job["left"] = Left; P.TowerBuildT = Left;
            if (Left > 0) return true;
            Construction.RemoveAt(I);
            // People may have walked across the foundation, or a wall may have been placed during the channel.
            string Why = TowerSpotProblem(At, World);
            foreach (dynamic Q in Gd.Iter(Gd.Values(Players)))
                if (Q.Id != P.Id && ((Vector2)Q.Pos).DistanceTo(At) < 1.1f * T) Why = "Someone is standing there";
            if (Gd.At(Towers, Job.Get("slot")) != null) Why = "Tower slot is occupied";
            if (!Gd.Truth(FreeTower))
            {
                if (Unlock < 2) Why = "Unlocks after the first boss";
                foreach (string Id in (GDict)TOWER_COST)
                    if (Count(P, Id) < Gd.At(TOWER_COST, Id)) Why = "Need tower materials";
            }
            if (Why != "") { Note(P, Why + "; materials kept"); return true; }
            if (Gd.Truth(FreeTower)) FreeTower = false;
            else foreach (string Id in (GDict)TOWER_COST) Take(P, Id, Gd.At(TOWER_COST, Id));
            PlaceTower(Job.Get("slot"), At);
            World.Rebuild(Nodes, Towers, BuiltWalls);
            Note(P, "Tower ready on dirt foundation"); return true;
        }
        P.TowerBuildT = 0f; return false;
    }
    public GArray ConstructionSnapshot()
    {
        var Result = new GArray();
        foreach (GDict Job in Construction)
            Result.Add(new GArray(Job.Get("pos"), 1f - Gd.Float(Job.Get("left")) / TOWER_BUILD_TIME, Job.Get("owner")));
        return Result;
    }
}
