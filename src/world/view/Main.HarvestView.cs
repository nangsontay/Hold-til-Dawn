using Godot;
using System;
using System.Collections.Generic;
using HoldTilDawn;

public partial class Main
{
    readonly List<GArray> HarvestColliderSources = new();
    GArray HarvestColliderNodes = new();
    readonly Dictionary<Vector2, int> HarvestTypes = new();

    GArray NodeColliders(GArray Records)
    {
        if (HarvestColliderSources.Count != Records.Count)
        {
            HarvestColliderSources.Clear();
            HarvestTypes.Clear();
            HarvestColliderNodes = new GArray();
        }
        for (int I = 0; I < Records.Count; I++)
        {
            var Record = (GArray)Records[I];
            if (I < HarvestColliderSources.Count && ReferenceEquals(HarvestColliderSources[I], Record)) continue;
            if (I < HarvestColliderSources.Count) HarvestTypes.Remove((Vector2)HarvestColliderSources[I][1]);
            HarvestTypes[(Vector2)Record[1]] = Gd.Int(Record[0]);
            if (I < HarvestColliderSources.Count)
            {
                HarvestColliderSources[I] = Record;
                var Node = (GDict)HarvestColliderNodes[I];
                Node["type"] = Record[0]; Node["pos"] = Record[1]; Node["amount"] = Record[2];
                Node["radius"] = Record.Count > 4 ? (object)Record[4] : null;
            }
            else { HarvestColliderSources.Add(Record); HarvestColliderNodes.Add(Harvestables.FromSnapshot(Record)); }
        }
        return HarvestColliderNodes;
    }

    public string GatherAnimation(Vector2 Target)
    {
        if (!HarvestTypes.TryGetValue(Target, out int Type)) return null;
        return Type switch { 0 => "gather_wood", 1 or 3 => "gather_stone", 2 => "gather_herb", _ => null };
    }
}
