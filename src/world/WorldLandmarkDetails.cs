using Godot;
using System;
using HoldTilDawn;

// Owner: Vu Duc Viet Anh. Atlas landmarks get a silhouette, supporting props and a distinct ground treatment.
// These are fixed world decorations, not loot or usable machines. Added solid props stay off the paths.
public partial class WorldGeometry
{
    // One palette shared by the world renderer, minimap and ground-cover placement.
    // Concrete yards, the swamp and the wooden cape deck already have dedicated surfaces.
    public readonly record struct LandmarkSoil
    {
        public int Id { get; }
        public float Rx { get; }
        public float Ry { get; }
        public string Hex { get; }
        public bool Bare { get; }
        public LandmarkSoil(int Id, float Rx, float Ry, string Hex, bool Bare)
        { this.Id = Id; this.Rx = Rx * LANDMARK_SCALE; this.Ry = Ry * LANDMARK_SCALE; this.Hex = Hex; this.Bare = Bare; }
        public void Deconstruct(out int Id, out float Rx, out float Ry, out string Hex, out bool Bare)
        { Id = this.Id; Rx = this.Rx; Ry = this.Ry; Hex = this.Hex; Bare = this.Bare; }
    }
    public static readonly LandmarkSoil[] LandmarkSoils = {
        new(1, 12f, 8f, "83734F", true),     // outpost service clearing
        new(2, 6.8f / LANDMARK_SCALE, 5.7f / LANDMARK_SCALE, "A39761", true),
        new(3, 6.8f / LANDMARK_SCALE, 5.7f / LANDMARK_SCALE, "B7B098", true),
        new(4, 6.8f / LANDMARK_SCALE, 5.7f / LANDMARK_SCALE, "77965B", false),
        new(5, 8.3f / LANDMARK_SCALE, 5.9f / LANDMARK_SCALE, "91836C", true), // recovery clearing
        new(6, 6.8f, 4.8f, "92917D", true),  // broken foundation apron
        new(7, 11.0f / LANDMARK_SCALE, 6.9f / LANDMARK_SCALE, "847254", true), // stranded convoy
        new(9, 6.7f, 5.0f, "8E8A7A", true),  // station gravel apron
        new(10, 4.0f, 3.8f, "BBA77E", true), // fishing beach
        new(11, 7.5f / LANDMARK_SCALE, 8.2f / LANDMARK_SCALE, "9A9881", true), // wind-eroded ascent
        new(12, 7.1f / LANDMARK_SCALE, 5.5f / LANDMARK_SCALE, "CABCA3", true), // pale coral-shell clearing
        new(13, 9.1f * MathF.Sqrt(YARD_AREA_SCALE), 5.8f * MathF.Sqrt(YARD_AREA_SCALE), "828172", true), // expanded yard gravel shoulder
        new(14, 9.0f, 4.5f, "BBA77E", true), // shipwreck's salvage beach
        new(15, 6.5f, 4.8f, "596047", true), // damp pond banks
        new(16, 7.5f * MathF.Sqrt(CAVE_AREA_SCALE), 5.2f * MathF.Sqrt(CAVE_AREA_SCALE), "65645D", true),
        new(17, 7.0f / LANDMARK_SCALE, 6.4f / LANDMARK_SCALE, "829B98", true), // storm-worn slate at the cape
        new(18, 8.0f, 6.3f, "796347", true), // fenced garden and its gate apron
    };

    public Vector2 SoilCentre(LandmarkSoil Soil) => Soil.Id == 16 ? CaveCentre : LmAt[Soil.Id];

    // Continuous core with a broken, feathered outer edge; never a faint green-tinted wash.
    public static float SoilCoverage(LandmarkSoil Soil, Vector2 Offset)
    {
        // Every point of the cave region is opaque rock, including its rim and eastern approach.
        if (Soil.Id == 16) return CaveGroundContains(Offset) ? 1 : 0;
        if (Soil.Id is 12 or 17)
        {
            float Distance=(Offset/LookoutRadii(Soil.Id)).Length();
            return Math.Clamp((1.18f-Distance)/.18f,0,1);
        }
        if (Soil.Id is 5 or 7 or 11)
        {
            // The complete designed footprint shares its centre with the map marker and the scene.
            // A rounded rectangle covers the outer vehicles and ledges, unlike the former small ellipse.
            Vector2 Outside = (Offset.Abs() - ExpeditionHalf(Soil.Id)).Max(Vector2.Zero);
            return Math.Clamp(1 - Outside.Length() / TL, 0, 1);
        }
        if (Soil.Id is 2 or 3 or 4)
        {
            float FarmDistance = (Offset / HomeFarmRadii).Length();
            // The complete gathering footprint is opaque, with the feathering entirely outside it.
            return Math.Clamp((1.12f - FarmDistance) / .12f, 0, 1);
        }
        if (Soil.Id == 18)
        {
            Vector2 GardenEdge = Offset.Abs() / TL - GardenHalf / TL;
            return Math.Clamp(1 - (MathF.Max(GardenEdge.X, GardenEdge.Y) - .25f) / 1.25f, 0, 1);
        }
        if (MathF.Abs(Offset.X) >= Soil.Rx * TL || MathF.Abs(Offset.Y) >= Soil.Ry * TL) return 0;
        float D = new Vector2(Offset.X / (Soil.Rx * TL), Offset.Y / (Soil.Ry * TL)).Length();
        float Angle = MathF.Atan2(Offset.Y, Offset.X);
        float Edge = .94f + .035f * MathF.Sin(Angle * 5 + Soil.Id) + .025f * MathF.Sin(Angle * 9 - Soil.Id);
        return Math.Clamp((Edge - D) / .18f, 0, 1) * (Soil.Bare ? 1 : .38f);
    }

    public bool BareLandmarkGround(Vector2 P)
    {
        foreach (var Soil in LandmarkSoils)
            if (Soil.Bare && LmAt.ContainsKey(Soil.Id) && SoilCoverage(Soil, P - SoilCentre(Soil)) > .5f) return true;
        return false;
    }

    void DressLandmarks()
    {
        // 01: the outpost's exterior service corner; leave all three defence approaches open.
        DressAt(Tl(-7.6f, 5.8f), "lm_generator_0", 12);
        DressAt(Tl(-9.1f, 5.5f), "lm_cable_0", 9);
        DressAt(Tl(7.9f, 5.5f), "lm_barricade_0");
        // 02..04: the compact home farms own their scenery and all their trees, stones and herbs are harvestable.
        // 05 and 07 supply their own centred recovery / convoy scenes.
        // 06: the expanded ruined compound supplies its own equipment and collapse debris.
        // 08: dense fern fans grow on the raised banks, never in the water or across a path.
        for (int I = 0; I < 15; I++)
        {
            Vector2 P = SwampCentre + Vector2.FromAngle(I * 2.39996f) * (0.35f + I * 0.035f) * SwampR;
            if (!NearPath(P, 0.8f * TL)) DressAt(P, "lm_fern_0");
        }
        // 09: the walkable weather hut owns its equipment and a clear southern doorway.
        // 10: fishing gear on shore; the pier deck remains fully walkable.
        Dress(10, 2.7f, 3.1f, "lm_boat_0");
        Dress(10, 1.0f, -2.7f, "lm_buoys_0", 7);
        Dress(10, 1.2f, 2.6f, "lm_rope_0");
        Dress(10, 2.2f, -2.2f, "lm_lifering_0");
        // 11 owns the rocky terraces, stairs and wind markers.
        // 12 supplies its marine lookout, reef board and diving equipment.
        // 13: the expanded yard supplies its own crane, forklift and loading bays.
        // 14: an anchor, tender and ring identify the wreck's salvage beach.
        Dress(14, -5.6f, 3.1f, "lm_anchor_0");
        Dress(14, 2.7f, 4.0f, "lm_boat_0");
        Dress(14, -1.1f, 3.5f, "lm_lifering_0");
        Dress(14, 4.5f, 3.0f, "lm_rope_0");
        Dress(14, -3.6f, 2.8f, "lm_scrap_0");
        // 15: a wet garden of ferns and pale flowers around the actual pond, with animated dragonflies.
        if (Ponds.Count > 0)
            for (int I = 0; I < 14; I++)
            {
                Vector2 P = Ponds[0].Centre + Vector2.FromAngle(I * Mathf.Tau / 14) * Ponds[0].Radius * new Vector2(1.95f, 1.55f);
                if (!NearPath(P, 0.6f * TL)) DressAt(P, I % 3 == 0 ? "lm_flowers_1" : "lm_fern_0");
            }
        // 16: the cave has a dedicated scree apron and survey cache.
        // The eastern cave entrance and resource fields are dressed by WorldCave.cs.
        // 17 supplies its compass terrace, beacon and signal equipment.
        // 18 supplies its own enclosed garden, gate, equipment and harvestable herb beds.
    }

    void Dress(int Id, float X, float Y, string Sprite, float Radius = 0)
    {
        if (LmAt.TryGetValue(Id, out Vector2 C)) DressAt(C + Tl(X, Y) * LANDMARK_SCALE, Sprite, Radius);
    }

    bool DressAt(Vector2 P, string Sprite, float Radius = 0)
    {
        // Reject rather than relocate: do not move set dressing into a doorway, a pool or a resource route.
        if (!IslandMap.OnLand(P, Math.Max(8, Radius)) || NearPad(P) || Gd.Truth(Lane(P))) return false;
        if (TerrainAt(P, Math.Max(6, Radius)) is not (0 or 4 or 6 or 7 or 9)) return false;
        if (Radius > 0)
        {
            if (NearPath(P, Radius + 12)) return false;
            foreach (var (C, R) in LmSolids) if (P.DistanceTo(C) < Radius + R + 12) return false;
            // Avoid overlapping the centres of the Atlas's existing props.
            foreach (var (C, _) in LmProps) if (P.DistanceTo(C) < Radius + 20) return false;
        }
        Prop(P.Round(), Sprite);
        Clear(P, Math.Max(14, Radius + 14));
        return true;
    }

    void FlowerBorder(int Id, float Radius)
    {
        if (!LmAt.TryGetValue(Id, out Vector2 C)) return;
        Radius *= LANDMARK_SCALE;
        for (int I = 0; I < 24; I++)
        {
            float A = I * 2.39996f;
            Vector2 P = C + Vector2.FromAngle(A) * (Radius * (0.55f + I % 5 * 0.1f)) * TL;
            if (!NearPath(P, 0.8f * TL) && IslandMap.OnGrass(P, 20)) DressAt(P, "lm_flowers_" + I % 3);
        }
    }
}
