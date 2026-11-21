using Godot;
using System;
using System.Collections.Generic;
using HoldTilDawn;

// Owner: Vu Duc Viet Anh. The landmarks built on the island, after the Landmark Atlas (art/landmarks/ref/, sprites
// from tools/PixelLandmarks.cs): palms and fallen trunks in the Coconut Grove (02), limestone shelves (03),
// broad-leaf herbs along the meadow trail (04), the utility vehicle slid off the road (05), the ruined depot (06),
// the stranded convoy beside its track (07), mangroves in the swamp (08), the weather station (09), the old pier
// out over the sea with fish traps on the beach (10), stone steps between rocky shoulders (11), the lookout shelter
// over a coral reef (12), the container yard (13), the cargo ship on the beach (14), red ginger round the pond (15),
// the limestone bluff with the cave mouth (16), the concrete platform at the end of the south cape (17) and the
// old herb garden (18). The outpost (01) is the base.
// Everything is placed from the island's shape, the same on every run and every peer. Props stand in Deco with
// the other scenery (depth sorted, solid where index.json gives a radius, plus the circles here for the big ones);
// floors, the pier, the platform, the steps and the coral lie flat on the ground layer
// (src/world/view/Main.LandmarkView.cs). The pier is walkable: IslandMap.Docks.
public partial class WorldGeometry
{
    // props: [world position, sprite name]
    public readonly List<(Vector2 Pos, string Sprite)> LmProps = new();
    // flat sprites on the ground layer
    public readonly List<(Vector2 Pos, string Sprite)> LmGround = new();
    // built ground where nothing grows: the depot's and the yard's concrete (drawn), the platform (its sprite)
    public readonly List<(Rect2 R, bool Drawn)> Floors = new();
    // stone steps across the Northwind Slope trail: [middle, across]
    public readonly List<(Vector2 At, Vector2 Across)> Steps = new();
    public readonly List<Vector2> Coral = new();
    readonly List<(Vector2 C, float R)> LmSolids = new();
    // where each landmark's main structure stands (for --goto=lm:N)
    public readonly Dictionary<int, Vector2> LmAt = new();
    // no trees, rocks or grass inside these
    readonly List<(Vector2 C, float R)> LmClear = new();

    const float S_ = IslandMap.SCALE;

    void BuildLandmarks()
    {
        LmProps.Clear();
        LmGround.Clear();
        Floors.Clear();
        Steps.Clear();
        Coral.Clear();
        LmSolids.Clear();
        LmClear.Clear();
        IslandMap.Docks.Clear();
        LmAt.Clear();
        var R = new Random(31);
        Grove(R);
        LimestoneFlats(R);
        HerbMeadow(R);
        BaysideWreck();
        RuinedBase(R);
        Convoy();
        Mangroves(R);
        WeatherStation(R);
        Pier();
        Northwind(R);
        CoralLookout(R);
        ContainerYard();
        Shipwreck(R);
        PondBanks(R);
        Cave();
        SouthCape(R);
        HerbGarden(R);
        DressLandmarks();
        BuildCrossings();
        DressExploration();
    }

    static Vector2 Tl(float X, float Y) => new Vector2(X, Y) * TL;

    void Prop(Vector2 P, string Sprite) => LmProps.Add((P, Sprite));

    void Solid(Vector2 C, float Radius) => LmSolids.Add((C, Radius));

    void Clear(Vector2 C, float Radius) => LmClear.Add((C, Radius));

    bool InClear(Vector2 P, float Margin)
    {
        foreach (var (C, Rr) in LmClear)
            if (P.DistanceTo(C) < Rr + Margin) return true;
        return false;
    }

    // room for a structure of this radius: on the grass, off the paths, the door lanes and the pads, clear of the
    // other landmarks' structures
    bool Room(Vector2 P, float Radius, float Grass = 0.5f)
    {
        return IslandMap.OnGrass(P, Radius * Grass) && !NearPath(P, Radius) && !InClear(P, Radius)
            && !Gd.Truth(Lane(P)) && !NearPad(P) && (float)WallDist(P) > (float)SAFE_D * 0.5f;
    }

    // the nearest spot to Want with room, searching out in rings of half a tile
    Vector2 Spot(Vector2 Want, float Radius, float Grass = 0.5f, float Reach = 14)
    {
        if (Room(Want, Radius, Grass)) return Want;
        for (float Ring = 0.5f; Ring <= Reach; Ring += 0.5f)
        {
            int N = Math.Max(8, (int)(Ring * 8));
            for (int I = 0; I < N; I++)
            {
                Vector2 P = Want + Vector2.FromAngle(I * Mathf.Tau / N) * Ring * TL;
                if (Room(P, Radius, Grass)) return P;
            }
        }
        return Want;
    }

    // the nearest point on any route or trail, and which way it runs there
    (Vector2 At, Vector2 Along) OnPath(Vector2 P)
    {
        float Best = float.PositiveInfinity;
        Vector2 At = P, Along = Vector2.Right;
        for (int K = 3; K < Trails.Count; K++)
        {
            var Path = Trails[K].Path;
            for (int I = 0; I < Path.Length - 1; I++)
            {
                Vector2 A = Path[I], B = Path[I + 1];
                Vector2 Ab = B - A;
                float T = Math.Clamp((P - A).Dot(Ab) / Math.Max(Ab.LengthSquared(), 0.001f), 0, 1);
                Vector2 Q = A + Ab * T;
                float D = P.DistanceTo(Q);
                if (D < Best)
                {
                    Best = D;
                    At = Q;
                    Along = Ab.Normalized();
                }
            }
        }
        return (At, Along);
    }

    // a short footpath from a landmark's marker out to its structure
    void Footpath(Vector2 From, Vector2 To)
    {
        // not where a path already runs close by
        if (From.DistanceTo(To) < 2 * TL || NearPath(To, 1.5f * TL)) return;
        var Pts = new List<Vector2>();
        int N = (int)(From.DistanceTo(To) / (0.5f * TL));
        var Side = (To - From).Normalized().Orthogonal();
        for (int I = 0; I <= N; I++)
        {
            float T = (float)I / N;
            Pts.Add(From.Lerp(To, T) + Side * MathF.Sin(T * MathF.PI) * 0.8f * TL);
        }
        Trails.Add((Pts.ToArray(), TRAIL_W));
    }

    Vector2 At(int Id) => IslandMap.ById(Id)?.Pos ?? Vector2.Zero;

    // --- 02 Coconut Grove: palms round the grove, a fallen trunk and dry branches by the trail
    public bool InGrove(Vector2 P) => P.DistanceTo(At(2)) < 13 * TL;

    void Grove(Random R)
    {
        Vector2 G = At(2);
        Vector2 Log = Spot(G + Tl(1.5f, 2), 1.4f * TL);
        Prop(Log, "lm_log_0");
        LmAt[2] = Log;
        Clear(Log, 1.6f * TL);
        for (int I = 0; I < 3; I++)
        {
            Vector2 P = Spot(Log + Vector2.FromAngle(I * 2.1f + 0.4f) * 2.2f * TL, 0.5f * TL);
            Prop(P, "lm_sticks_0");
            Clear(P, 0.5f * TL);
        }
        for (int I = 0; I < 12; I++)
        {
            Vector2 P = G + Vector2.FromAngle((float)R.NextDouble() * Mathf.Tau) * (2.5f + (float)R.NextDouble() * 8.5f) * TL;
            if (!Room(P, 0.8f * TL)) continue;
            Prop(P, "lm_palm_" + (I % 4));
            Clear(P, 0.7f * TL);
        }
    }

    // --- 03 Limestone Flats: low pale shelves breaking through the grass
    void LimestoneFlats(Random R)
    {
        Vector2 L = At(3);
        for (int I = 0; I < 5; I++)
        {
            Vector2 P = Spot(L + Vector2.FromAngle(I * 1.26f + 0.3f) * (3.0f + (I % 2) * 3.0f) * TL, 2.2f * TL, 0.5f, 7);
            Prop(P, "lm_shelf_" + (I % 3));
            if (I == 0) LmAt[3] = P;
            Clear(P, 2.4f * TL);
        }
    }

    // --- 04 Herb Meadow: broad-leaf herbs along the trail
    void HerbMeadow(Random R)
    {
        Vector2 H = At(4);
        int Placed = 0;
        for (int Tries = 0; Tries < 300 && Placed < 12; Tries++)
        {
            Vector2 P = H + Vector2.FromAngle((float)R.NextDouble() * Mathf.Tau) * (float)R.NextDouble() * 8 * TL;
            var (On, _) = OnPath(P);
            float D = P.DistanceTo(On);
            if (D < 1.7f * TL || D > 3.2f * TL || !IslandMap.OnGrass(P, TL) || InClear(P, 0.6f * TL) || NearPad(P)) continue;
            Prop(P, "lm_broadleaf_" + (Placed % 2));
            if (Placed == 0) LmAt[4] = P;
            Clear(P, 0.6f * TL);
            Placed++;
        }
    }

    // --- 05 Bayside Wreck: the vehicle slid off the road, beside it
    void BaysideWreck()
    {
        var (On, Along) = OnPath(At(5));
        Vector2 Off = Along.Orthogonal();
        if (Off.Y < 0) Off = -Off;
        Vector2 P = Spot(On + Off * 3.0f * TL, 1.6f * TL, 0.3f, 6);
        Prop(P, "lm_jeep_0");
        LmAt[5] = P;
        Solid(P + Tl(-0.7f, -0.3f), 0.75f * TL);
        Solid(P + Tl(0.7f, -0.4f), 0.75f * TL);
        Clear(P, 2.0f * TL);
    }

    // --- 06 Ruined Base: a concrete floor, broken walls behind and to the left, shelves and crates, the mast
    void RuinedBase(Random R)
    {
        Vector2 C = Spot(At(6), 4.5f * TL, 0.6f, 10).Round();
        Floors.Add((new Rect2(C - Tl(4.5f, 3.4f), Tl(9, 6.4f)), true));
        LmAt[6] = C;
        Prop(C + Tl(-2.1f, -2.8f), "lm_ruin_h_0");
        Prop(C + Tl(2.1f, -2.8f), "lm_ruin_h_1");
        for (float X = -4.0f; X <= 3.4f; X += 0.8f)
            if (X < 3.0f || X > 3.4f) Solid(C + Tl(X, -3.1f), 0.5f * TL);
        Prop(C + Tl(-4.3f, 2.2f), "lm_ruin_v_0");
        for (float Y = -1.0f; Y <= 2.0f; Y += 0.8f) Solid(C + Tl(-4.3f, Y), 0.5f * TL);
        Prop(C + Tl(-2.4f, -2.2f), "lm_rack_0");
        Solid(C + Tl(-2.4f, -2.5f), 0.7f * TL);
        Prop(C + Tl(0.8f, 0.4f), "lm_crate_0");
        Prop(C + Tl(1.6f, 1.2f), "lm_crate_0");
        Prop(C + Tl(-1.2f, 1.8f), "lm_crate_0");
        Prop(C + Tl(5.1f, -3.4f), "lm_mast_0");
        Clear(C, 5.6f * TL);
    }

    // --- 07 Stranded Convoy: three trucks pulled off along the forest track
    void Convoy()
    {
        var (On, Along) = OnPath(At(7));
        Vector2 Off = Along.Orthogonal();
        for (int I = 0; I < 3; I++)
        {
            Vector2 Want = On + Along * (I - 1) * 5.0f * TL + Off * ((I % 2 == 0) ? 3.0f : -3.0f) * TL;
            Vector2 P = Spot(Want, 2.4f * TL, 0.3f, 6);
            Prop(P, "lm_truck_" + (I == 1 ? 1 : 0));
            if (I == 1) LmAt[7] = P;
            foreach (float X in new[] { -1.4f, 0.0f, 1.4f }) Solid(P + Tl(X, -0.5f), 0.85f * TL);
            Clear(P, 2.6f * TL);
        }
    }

    // --- 08 Mangrove Swamp: mangroves on their stilt roots round the pools
    void Mangroves(Random R)
    {
        int Placed = 0;
        for (int Tries = 0; Tries < 400 && Placed < 12; Tries++)
        {
            Vector2 P = SwampCentre + Vector2.FromAngle((float)R.NextDouble() * Mathf.Tau) * MathF.Sqrt((float)R.NextDouble()) * SwampR;
            if (!IslandMap.OnGrass(P, 0.6f * TL) || TerrainAt(P, 0.8f * TL) is 1 or 5 || InClear(P, 1.4f * TL)) continue;
            Prop(P, "lm_mangrove_" + (Placed % 3));
            if (Placed == 0) LmAt[8] = P;
            Clear(P, 1.0f * TL);
            Placed++;
        }
    }

    // --- 09 Weather Station: the hut on the ridge, rocks round it
    void WeatherStation(Random R)
    {
        Vector2 P = Spot(At(9), 2.2f * TL, 0.5f, 8);
        Prop(P, "lm_station_0");
        LmAt[9] = P;
        Solid(P + Tl(-0.6f, -0.5f), 0.7f * TL);
        Solid(P + Tl(0.6f, -0.5f), 0.7f * TL);
        Clear(P, 2.6f * TL);
        Footpath(At(9), P + Tl(-0.5f, 0.6f));
    }

    // --- 10 Old Fishing Pier: out to sea from the west shore, fish traps on the beach
    void Pier()
    {
        Vector2 M = At(10);
        // walk west to the waterline
        Vector2 Shore = M;
        while (IslandMap.OnLand(Shore) && Shore.X > M.X - 40 * TL) Shore.X -= 0.25f * TL;
        Vector2 Land = (Shore + Tl(2.2f, 0)).Round();
        LmGround.Add((Land, "lm_pier_0"));
        LmAt[10] = Land;
        // the deck: 224 px out from the land end, 48 px across (tools/PixelLandmarks.cs)
        IslandMap.Docks.Add(new Rect2(Land + new Vector2(-224, -24), new Vector2(226, 48)));
        Prop(Land + Tl(1.0f, 1.6f), "lm_trap_0");
        Prop(Land + Tl(1.9f, -1.5f), "lm_trap_1");
        Prop(Land + Tl(2.6f, 1.2f), "lm_trap_0");
        Clear(Land + Tl(1.5f, 0), 2.5f * TL);
        Footpath(M, Land + Tl(0.6f, 0));
    }

    // --- 11 Northwind Slope: stone steps up the trail between two rocky shoulders
    void Northwind(Random R)
    {
        var (On, Along) = OnPath(At(11));
        Vector2 Off = Along.Orthogonal();
        for (float D = -3.5f; D <= 3.5f; D += 0.8f) Steps.Add((On + Along * D * TL, Off));
        LmAt[11] = On;
        for (int I = 0; I < 3; I++)
            foreach (int Side in new[] { -1, 1 })
            {
                Vector2 P = On + Along * (I - 1) * 2.4f * TL + Off * Side * (2.6f + (I % 2) * 0.8f) * TL;
                if (!IslandMap.OnGrass(P, 0.6f * TL)) continue;
                Prop(P, (I == 1) ? "crag_" + ((I + Side + 3) % 3) : "rock_" + ((I * 2 + Side + 4) % 4));
                Clear(P, 1.2f * TL);
            }
    }

    // --- 12 Coral Lookout: the shelter on the headland, the reef in the shallows below
    void CoralLookout(Random R)
    {
        Vector2 M = At(12);
        Vector2 Best = M;
        float BestD = float.PositiveInfinity;
        for (int Y = -14; Y <= 14; Y++)
            for (int X = -14; X <= 14; X++)
            {
                Vector2 P = M + Tl(X, Y);
                if (P.DistanceTo(M) > 14 * TL || !Room(P, 1.8f * TL, 0.5f)) continue;
                float D = IslandMap.Depth(P);
                if (D < BestD)
                {
                    BestD = D;
                    Best = P;
                }
            }
        Prop(Best, "lm_lookout_0");
        LmAt[12] = Best;
        Solid(Best + Tl(-1.0f, -0.4f), 0.45f * TL);
        Solid(Best + Tl(1.0f, -0.4f), 0.45f * TL);
        Clear(Best, 2.2f * TL);
        Footpath(M, Best + Tl(-1.4f, 0.4f));
        for (int I = 0; I < 400 && Coral.Count < 46; I++)
        {
            Vector2 P = Best + Vector2.FromAngle((float)R.NextDouble() * Mathf.Tau) * (float)R.NextDouble() * 13 * TL;
            float D = IslandMap.Depth(P);
            if (D < -1.0f * TL && D > -7.5f * TL) Coral.Add(P);
        }
    }

    // --- 13 Container Yard: a concrete yard, containers along the back, one open, drums and pallets
    void ContainerYard()
    {
        Vector2 C = Spot(At(13), 5.0f * TL, 0.6f, 10).Round();
        Floors.Add((new Rect2(C - Tl(6.2f, 4.0f), Tl(12.4f, 7.6f)), true));
        LmAt[13] = C;
        Prop(C + Tl(-3.2f, -2.4f), "lm_container_1");
        Prop(C + Tl(0.5f, -2.4f), "lm_container_end_0");
        Prop(C + Tl(3.9f, -2.4f), "lm_container_0");
        for (float X = -5.2f; X <= 6.0f; X += 0.9f)
            if (X < -0.8f || X > 1.8f) Solid(C + Tl(X, -2.8f), 0.6f * TL);
        Solid(C + Tl(-0.2f, -2.9f), 0.6f * TL);
        Solid(C + Tl(1.1f, -2.9f), 0.6f * TL);
        Prop(C + Tl(-0.6f, 1.2f), "lm_drum_0");
        Prop(C + Tl(0.1f, 1.6f), "lm_drum_1");
        Prop(C + Tl(4.9f, 1.4f), "lm_drum_0");
        Prop(C + Tl(-3.1f, 1.9f), "lm_pallet_0");
        Prop(C + Tl(2.2f, 2.4f), "lm_pallet_0");
        Clear(C, 6.8f * TL);
    }

    // --- 14 Beached Shipwreck: on the sand, the bow in the shallows, machinery washed up round it
    void Shipwreck(Random R)
    {
        Vector2 M = At(14);
        Vector2 Best = M;
        float BestScore = float.NegativeInfinity;
        for (int Y = -18; Y <= 18; Y++)
            for (int X = -18; X <= 18; X++)
            {
                Vector2 P = M + Tl(X, Y);
                if (P.DistanceTo(M) > 18 * TL) continue;
                // the keel along the sand: on the beach or just in the water, not up on the grass
                int Good = 0;
                for (int K = -6; K <= 6; K++)
                {
                    Vector2 Q = P + Tl(K * 1.1f, 0);
                    float D = IslandMap.Depth(Q);
                    if (D > -3.5f * TL && IslandMap.GrassIn(Q) < 0.5f * TL) Good++;
                    if (D > 0.5f * TL && IslandMap.GrassIn(Q) < 0) Good++;
                }
                float Score = Good - P.DistanceTo(M) / (6 * TL);
                if (Score > BestScore)
                {
                    BestScore = Score;
                    Best = P;
                }
            }
        Best = Best.Round();
        Prop(Best, "lm_ship_0");
        LmAt[14] = Best;
        for (float X = -6.4f; X <= 6.6f; X += 1.2f)
        {
            Vector2 Q = Best + Tl(X, -0.6f);
            if (IslandMap.OnLand(Q, -TL)) Solid(Q, 1.1f * TL);
        }
        Clear(Best + Tl(0, -1.5f), 7.5f * TL);
        foreach (var (Dx, Dy, Name) in new[] { (-4.5f, 1.8f, "lm_gear_0"), (1.5f, 2.0f, "lm_drum_1"), (2.4f, 2.6f, "lm_drum_1"), (5.2f, 1.6f, "lm_gear_0"), (-1.8f, 2.4f, "lm_crate_0") })
        {
            Vector2 P = Best + Tl(Dx, Dy);
            if (IslandMap.OnLand(P, 0.5f * TL)) Prop(P, Name);
        }
    }

    // --- 15 Rainwater Pond: red ginger and broad leaves on the damp banks
    void PondBanks(Random R)
    {
        if (Ponds.Count == 0) return;
        var Pd = Ponds[0];
        LmAt[15] = Pd.Centre + new Vector2(0, Pd.Radius * 1.4f);
        for (int I = 0; I < 9; I++)
        {
            Vector2 P = Pd.Centre + Vector2.FromAngle(I * Mathf.Tau / 9 + 0.3f) * Pd.Radius * new Vector2(1.75f, 1.45f);
            if (!IslandMap.OnGrass(P, 0.5f * TL) || NearPath(P, 0.3f * TL) || InClear(P, 0.4f * TL)) continue;
            Prop(P, (I % 3 == 0) ? "lm_broadleaf_1" : "lm_ginger_0");
            Clear(P, 0.5f * TL);
        }
    }

    // --- 16 Southwest Cave: the bluff over the mouth (the spires of WorldTerrain flank it)
    void Cave()
    {
        var Cv = IslandMap.ById(16);
        if (Cv == null) return;
        Vector2 Mouth = (Cv.Pos + Tl(1.5f, -2.5f) * S_).Round();
        Crags.RemoveAll(P => MathF.Abs(P.X - Mouth.X) < 4.4f * TL && P.Y > Mouth.Y - 5.6f * TL && P.Y < Mouth.Y + 1.6f * TL);
        Prop(Mouth, "lm_bluff_0");
        LmAt[16] = Mouth;
        foreach (var (X, Y, Rr) in new[] { (-2.7f, 0.0f, 1.0f), (2.7f, 0.0f, 1.0f), (0.0f, -1.0f, 0.8f), (-1.6f, -1.7f, 1.1f), (1.6f, -1.7f, 1.1f), (0.0f, -2.1f, 1.1f), (-3.3f, -1.8f, 1.1f), (3.3f, -1.8f, 1.1f) })
            Solid(Mouth + Tl(X, Y), Rr * TL);
        Clear(Mouth + Tl(0, -1.2f), 4.2f * TL);
    }

    // --- 17 South Cape Lookout: the platform at the tip of the cape
    void SouthCape(Random R)
    {
        Vector2 M = At(17);
        Vector2 Best = M;
        float Far = float.NegativeInfinity;
        for (int Y = -18; Y <= 18; Y++)
            for (int X = -18; X <= 18; X++)
            {
                Vector2 P = M + Tl(X, Y);
                if (P.DistanceTo(M) > 18 * TL || !IslandMap.OnLand(P, 2.6f * TL)) continue;
                if (P.Length() > Far)
                {
                    Far = P.Length();
                    Best = P;
                }
            }
        Best = Best.Round();
        LmGround.Add((Best, "lm_platform_0"));
        LmAt[17] = Best;
        Floors.Add((new Rect2(Best - Tl(1.9f, 1.3f), Tl(3.8f, 2.6f)), false));
        Clear(Best, 2.6f * TL);
        foreach (int Side in new[] { -1, 1 })
        {
            Vector2 P = Best + Tl(Side * 3.4f, 0.6f);
            if (IslandMap.OnLand(P, 0.5f * TL)) Prop(P, "rock_" + (Side + 1));
        }
        Footpath(M, Best + (M - Best).Normalized() * 1.2f * TL);
    }

    // --- 18 Abandoned Herb Garden: raised beds in a broken fence, a watering can left behind
    void HerbGarden(Random R)
    {
        Vector2 C = Spot(At(18), 3.4f * TL, 0.6f, 10).Round();
        LmAt[18] = C;
        foreach (var (X, Y, K) in new[] { (-1.5f, -0.9f, 0), (1.5f, -0.9f, 1), (-1.5f, 1.1f, 1), (1.5f, 1.1f, 0) })
        {
            Prop(C + Tl(X, Y), "lm_bed_" + K);
            Solid(C + Tl(X - 0.6f, Y - 0.5f), 0.55f * TL);
            Solid(C + Tl(X + 0.6f, Y - 0.5f), 0.55f * TL);
        }
        Prop(C + Tl(-1.7f, -2.6f), "lm_fence_0");
        Prop(C + Tl(1.9f, -2.5f), "lm_fence_0");
        Prop(C + Tl(0.1f, 0.2f), "lm_can_0");
        Prop(C + Tl(-3.4f, 0.4f), "lm_broadleaf_1");
        Prop(C + Tl(3.5f, -0.2f), "lm_broadleaf_0");
        Clear(C, 3.8f * TL);
    }
}
