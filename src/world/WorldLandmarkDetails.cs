using Godot;
using System;
using HoldTilDawn;

// Owner: Vu Duc Viet Anh. Atlas landmarks get a silhouette, supporting props and a distinct ground treatment.
// These are fixed world decorations, not loot or usable machines. Added solid props stay off the paths.
public partial class WorldGeometry
{
    void DressLandmarks()
    {
        // 01: the outpost's exterior service corner; leave all three defence approaches open.
        DressAt(Tl(-7.6f, 5.8f), "lm_generator_0", 12);
        DressAt(Tl(-9.1f, 5.5f), "lm_cable_0", 9);
        DressAt(Tl(7.9f, 5.5f), "lm_barricade_0");
        // 02: a recognisable clearing, with the fallen palm at the centre of a coconut harvest.
        Dress(2, -2.2f, 0.8f, "lm_coconuts_0");
        Dress(2, 2.4f, 1.1f, "lm_coconuts_0");
        Dress(2, -2.8f, -1.4f, "lm_fern_0");
        Dress(2, 3.1f, -1.1f, "lm_fern_0");
        Dress(2, 0.6f, 2.9f, "lm_signpost_0", 4);
        for (int I = 0; I < 12; I++)
        {
            Vector2 P = LmAt[2] + Vector2.FromAngle(I * 2.39996f) * (5.4f + I % 3 * 1.5f) * TL;
            if (IslandMap.OnGrass(P, 18)) DressAt(P, "lm_palm_" + I % 4, 6);
        }
        // 03: a large spiral ammonite makes this more specific than a scattering of rocks.
        Dress(3, 0.8f, 2.3f, "lm_fossil_0");
        Dress(3, -2.7f, 1.4f, "lm_cairn_0", 7);
        // 04: bands of purple, cream and pink flowers make the resource meadow read from a distance.
        FlowerBorder(4, 6.0f);
        Dress(4, -2.0f, 2.0f, "lm_signpost_0", 4);
        // 05: a stripped utility vehicle, its detached panels and a failed recovery attempt.
        Dress(5, -2.3f, 1.1f, "lm_scrap_0");
        Dress(5, 1.7f, 1.5f, "lm_cable_0", 9);
        Dress(5, -1.0f, 2.1f, "lm_rope_0");
        // 06: the ruined radio depot, no longer an anonymous concrete footprint.
        Dress(6, 2.5f, -0.4f, "lm_radio_0");
        Dress(6, -2.4f, 0.2f, "lm_generator_0", 12);
        Dress(6, 2.4f, 2.1f, "lm_barricade_0");
        Dress(6, -4.8f, 3.2f, "lm_scrap_0");
        Dress(6, 4.8f, 1.4f, "lm_fern_0");
        // 07: unloaded cargo and a deserted checkpoint beside the three trucks.
        Dress(7, -3.6f, 1.7f, "lm_barricade_0");
        Dress(7, 2.7f, 1.8f, "lm_scrap_0");
        Dress(7, 3.4f, -1.8f, "lm_cable_0", 9);
        Dress(7, -2.5f, -2.1f, "lm_expedition_0");
        // 08: dense fern fans grow on the raised banks, never in the water or across a path.
        for (int I = 0; I < 15; I++)
        {
            Vector2 P = SwampCentre + Vector2.FromAngle(I * 2.39996f) * (0.35f + I * 0.035f) * SwampR;
            if (!NearPath(P, 0.8f * TL)) DressAt(P, "lm_fern_0");
        }
        // 09: a tall satellite dish, cracked solar panel and orange windsock form a station compound.
        Dress(9, -3.7f, -0.8f, "lm_radar_0", 12);
        Dress(9, 2.7f, 1.5f, "lm_solar_0", 12);
        Dress(9, -1.5f, -3.0f, "lm_windsock_0", 4);
        Dress(9, -2.2f, 2.0f, "lm_cable_0", 9);
        // 10: fishing gear on shore; the pier deck remains fully walkable.
        Dress(10, 2.7f, 3.1f, "lm_boat_0");
        Dress(10, 1.0f, -2.7f, "lm_buoys_0", 7);
        Dress(10, 1.2f, 2.6f, "lm_rope_0");
        Dress(10, 2.2f, -2.2f, "lm_lifering_0");
        // 11: paired cairns bracket the climb. The centre of the steps is kept open.
        var (_, Along) = OnPath(LmAt[11]);
        Vector2 Side = Along.Orthogonal();
        DressAt(LmAt[11] + Side * 2.0f * TL + Along * 3.7f * TL, "lm_cairn_0", 7);
        DressAt(LmAt[11] - Side * 2.0f * TL - Along * 3.7f * TL, "lm_cairn_0", 7);
        Dress(11, 3.0f, 1.0f, "lm_signpost_0", 4);
        // 12: the coastal observation shelter has a telescope and marine safety equipment.
        Dress(12, -3.0f, -0.2f, "lm_scope_0", 6);
        Dress(12, 2.4f, 1.1f, "lm_lifering_0");
        Dress(12, -2.2f, -1.8f, "lm_buoys_0", 7);
        // 13: a rusting loading crane provides the yard with a strong skyline.
        Vector2 Crane = LmAt[13] + Tl(-5.0f, 2.7f);
        if (DressAt(Crane, "lm_crane_0", 18))
        {
            Solid(Crane + new Vector2(-13, -10), 9);
            Solid(Crane + new Vector2(16, -10), 9);
        }
        Dress(13, -2.3f, 0.4f, "lm_cable_0", 9);
        Dress(13, 4.4f, 2.8f, "lm_scrap_0");
        Dress(13, 5.2f, -0.3f, "lm_generator_0", 12);
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
        // 16: an old survey cache beside the cave opening, not blocking the entrance.
        Dress(16, -3.7f, 1.6f, "lm_expedition_0");
        Dress(16, 3.6f, 1.3f, "lm_cairn_0", 7);
        Dress(16, -4.4f, -0.3f, "lm_fern_0");
        // 17: the cape is an old survey viewpoint, with a telescope and a wind marker.
        Dress(17, 0.8f, 0.2f, "lm_scope_0", 6);
        Dress(17, -2.8f, -0.3f, "lm_windsock_0", 4);
        Dress(17, 2.6f, -1.4f, "lm_cairn_0", 7);
        // 18: a vine-covered entrance, water collection and abandoned pots frame the four beds.
        Vector2 Arch = LmAt[18] + Tl(0.0f, 3.4f);
        if (DressAt(Arch, "lm_arch_0"))
        {
            Solid(Arch + new Vector2(-46, 0), 7);
            Solid(Arch + new Vector2(46, 0), 7);
        }
        Dress(18, 3.4f, -2.2f, "lm_rainbarrel_0", 10);
        Dress(18, -3.7f, 2.1f, "lm_pots_0");
        FlowerBorder(18, 4.8f);
    }

    void Dress(int Id, float X, float Y, string Sprite, float Radius = 0)
    {
        if (LmAt.TryGetValue(Id, out Vector2 C)) DressAt(C + Tl(X, Y), Sprite, Radius);
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
        for (int I = 0; I < 24; I++)
        {
            float A = I * 2.39996f;
            Vector2 P = C + Vector2.FromAngle(A) * (Radius * (0.55f + I % 5 * 0.1f)) * TL;
            if (!NearPath(P, 0.8f * TL) && IslandMap.OnGrass(P, 20)) DressAt(P, "lm_flowers_" + I % 3);
        }
    }
}
