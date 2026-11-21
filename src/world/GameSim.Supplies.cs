using Godot;
using System;
using System.Collections.Generic;
using HoldTilDawn;

// Owner: Vu Duc Viet Anh. Each night three supplies the rescue needs lie out on the island, at 3 of the 17
// landmarks (never the base): one at a landmark near the base, two far out. Walking over one picks it up.
// The night ends early only once the night's enemies are all down AND the three supplies are in (CheckEnd),
// so the team has to leave the base at night.
public partial class GameSim
{
    // what the rescue needs: a battery for the beacon's radio, a flare to guide the helicopter, the lab's data
    public static readonly string[] SUPPLY_NAMES = { "Radio battery", "Signal flare", "Lab data drive" };
    public const int SUPPLY_COUNT = 3;
    // landmarks closer than this to the base count as near (Coconut Grove, Limestone Flats, Herb Meadow)
    public const float SUPPLY_NEAR_TILES = 45f;
    // how close you walk to pick one up
    public const float SUPPLY_PICK = 1.1f;
    // [kind, pos, landmark id, picked up]
    public readonly GArray Supplies = new();

    public int SuppliesGot()
    {
        int N = 0;
        foreach (GArray S in Supplies)
            if (Gd.Truth(S[3])) N++;
        return N;
    }

    public bool SuppliesAllIn() => Supplies.Count > 0 && SuppliesGot() >= Supplies.Count;

    public void PlaceSupplies()
    {
        Supplies.Clear();
        var Near = new List<IslandMap.Landmark>();
        var Far = new List<IslandMap.Landmark>();
        foreach (var L in IslandMap.Landmarks)
        {
            if (L.Id == 1) continue;
            (L.Pos.Length() < SUPPLY_NEAR_TILES * T ? Near : Far).Add(L);
        }
        var R = (RandomNumberGenerator)Gd.Unwrap((object)Rng);
        void Shuffle(List<IslandMap.Landmark> A)
        {
            for (int I = A.Count - 1; I > 0; I--)
            {
                int J = (int)(R.Randi() % (uint)(I + 1));
                (A[I], A[J]) = (A[J], A[I]);
            }
        }
        Shuffle(Near);
        Shuffle(Far);
        var Picked = new List<IslandMap.Landmark>();
        if (Near.Count > 0) Picked.Add(Near[0]);
        for (int I = 0; I < Far.Count && Picked.Count < SUPPLY_COUNT; I++) Picked.Add(Far[I]);
        // which supply lies where is random too
        var Kinds = new List<int> { 0, 1, 2 };
        for (int I = Kinds.Count - 1; I > 0; I--)
        {
            int J = (int)(R.Randi() % (uint)(I + 1));
            (Kinds[I], Kinds[J]) = (Kinds[J], Kinds[I]);
        }
        for (int I = 0; I < Picked.Count; I++)
            Supplies.Add(new GArray(Kinds[I], SupplySpot(Picked[I]), Picked[I].Id, false));
    }

    // in front of the landmark's structure, on open land
    Vector2 SupplySpot(IslandMap.Landmark L)
    {
        Vector2 At = L.Pos;
        if (World is WorldGeometry Wg && Wg.LmAt.TryGetValue(L.Id, out Vector2 Built)) At = Built;
        At = IslandMap.PullInland(At + new Vector2(0, 2.5f * T), T);
        return (Vector2)World.Resolve(At, 0.5f * T);
    }

    public void ClearSupplies() => Supplies.Clear();

    // night only: a standing player who walks over a supply picks it up for the team
    public void TickSupplies()
    {
        foreach (GArray S in Supplies)
        {
            if (Gd.Truth(S[3])) continue;
            Vector2 At = (Vector2)S[1];
            foreach (dynamic P in Gd.Iter(Gd.Values(Players)))
            {
                if (P.State != 0 || ((Vector2)P.Pos).DistanceTo(At) > SUPPLY_PICK * T) continue;
                S[3] = true;
                int Got = SuppliesGot();
                this.Say(Gd.Format("%s found (%d/%d)", new GArray(SUPPLY_NAMES[Gd.Int(S[0])], Got, Supplies.Count)));
                break;
            }
        }
    }

    public GArray SupplySnapshot()
    {
        var Out = new GArray();
        foreach (GArray S in Supplies) Out.Add(new GArray(S[0], S[1], S[3]));
        return Out;
    }
}
