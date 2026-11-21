using Godot;
using System;
using System.Collections.Generic;
using HoldTilDawn;

// Rescue components belong to the team and persist through dawn; backpacks cannot lose them.
public partial class GameSim
{
    public static readonly string[] SUPPLY_NAMES = { "Battery cells", "Flare powder", "Encrypted circuit" };
    public static readonly string[] RESCUE_IDS = { "rescue_battery", "rescue_flare", "rescue_data" };
    public static readonly string[] COMPONENT_IDS = { "battery_cells", "flare_powder", "encrypted_circuit" };
    public const int SUPPLY_COUNT = 3;
    public const float SUPPLY_PICK = 1.1f;
    public const float SUPPLY_NEAR_TILES = 45f;
    // [kind, accessible position, landmark id, collected]
    public readonly GArray Supplies = new();
    public readonly int[] RescueComponents = new int[3];
    public readonly bool[] RescueCrafted = new bool[3];
    public static int RescueKind(string Id) => Array.IndexOf(RESCUE_IDS, Id);
    public int SuppliesGot() { int N = 0; foreach (GArray S in Supplies) if (Gd.Truth(S[3])) N++; return N; }
    public bool SuppliesAllIn() => Array.TrueForAll(RescueCrafted, V => V);
    public int CraftHave(dynamic P, string Id)
    {
        int Kind = Array.IndexOf(COMPONENT_IDS, Id);
        return Kind >= 0 ? RescueComponents[Kind] : Gd.Int(Count(P, Id));
    }
    public void ClearSupplies()
    {
        Supplies.Clear(); Array.Clear(RescueComponents); Array.Clear(RescueCrafted);
    }
    public void PlaceSupplies()
    {
        ClearSupplies();
        var Places = new List<IslandMap.Landmark>(IslandMap.Landmarks);
        Places.RemoveAll(L => L.Id == 1);
        var Random = (RandomNumberGenerator)Gd.Unwrap((object)Rng);
        for (int I = Places.Count - 1; I > 0; I--)
        {
            int J = (int)(Random.Randi() % (uint)(I + 1));
            (Places[I], Places[J]) = (Places[J], Places[I]);
        }
        for (int I = 0; I < SUPPLY_COUNT; I++)
            Supplies.Add(new GArray(I, SupplySpot(Places[I]), Places[I].Id, false));
    }
    Vector2 SupplySpot(IslandMap.Landmark L)
    {
        Vector2 Centre = World is WorldGeometry W && W.LmAt.TryGetValue(L.Id, out Vector2 Built) ? Built : L.Pos;
        var Geometry = (WorldGeometry)World;
        for (float Ring = 3; Ring <= 12; Ring += 1)
            for (int I = 0; I < 16; I++)
            {
                Vector2 At = (Centre + Vector2.FromAngle(Mathf.Pi / 2 + I * Mathf.Tau / 16) * Ring * T).Round();
                if (IslandMap.OnLand(At, T) && Geometry.TerrainAt(At) != 1
                    && Geometry.ResolveFast(At, 0.6f * T).IsEqualApprox(At)
                    && !Gd.Truth(InsideBase(At))) return At;
            }
        throw new InvalidOperationException($"No accessible rescue component at landmark {L.Id}");
    }
    public void TickSupplies()
    {
        if ((string)Phase != "day") return;
        foreach (GArray S in Supplies)
        {
            if (Gd.Truth(S[3])) continue;
            foreach (dynamic P in Gd.Iter(Gd.Values(Players)))
            {
                if (P.State != 0 || ((Vector2)P.Pos).DistanceSquaredTo((Vector2)S[1]) > SUPPLY_PICK * T * SUPPLY_PICK * T) continue;
                S[3] = true; int Kind = Gd.Int(S[0]); RescueComponents[Kind]++;
                Say(SUPPLY_NAMES[Kind] + " secured. Craft rescue equipment at the workbench by day.");
                break;
            }
        }
    }
    public GArray SupplySnapshot()
    {
        var Result = new GArray();
        if ((string)Phase == "day") foreach (GArray S in Supplies) Result.Add(new GArray(S[0], S[1], S[3], S[2]));
        return Result;
    }
    public GArray RescueSnapshot() => new(new GArray(RescueComponents[0], RescueComponents[1], RescueComponents[2]),
        new GArray(RescueCrafted[0], RescueCrafted[1], RescueCrafted[2]));
}
