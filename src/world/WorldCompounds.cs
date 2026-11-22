using Godot;
using System;
using System.Collections.Generic;

public partial class WorldGeometry
{
    // Another 50% of the current yard area, in addition to the earlier island-wide enlargement.
    public const float YARD_AREA_SCALE = 1.5f;
    public static readonly Vector2 RuinHalf = new Vector2(4.5f, 3.2f) * TL * LANDMARK_SCALE;
    public static readonly Vector2 YardHalf = new Vector2(6.2f, 3.8f) * TL * LANDMARK_SCALE * MathF.Sqrt(YARD_AREA_SCALE);
    public readonly record struct CompoundBoundary(int Id, int Side, Vector2 A, Vector2 B, float Height,
        Vector2[] Face, Vector2[] Cap, Vector2[] Wire);
    public readonly List<CompoundBoundary> CompoundBoundaries = new();
    public readonly Dictionary<int, Vector2> CompoundGates = new();

    void BuildCompoundPerimeter(int Id, Rect2 Floor, float GateWidth)
    {
        Rect2 Edge = Floor.Grow(-8);
        Vector2 NW = Edge.Position, SE = Edge.End, Gate = new(Floor.GetCenter().X, SE.Y);
        CompoundGates[Id] = Gate;
        CompoundEntrances[Id] = Gate + new Vector2(0, 28);
        void Side(Vector2 A, Vector2 B, int SideId)
        {
            CompoundLine(A, B, 8);
            int Count = Math.Max(1, (int)MathF.Ceiling(A.DistanceTo(B) / 54));
            for (int I = 0; I < Count; I++)
            {
                Vector2 P = A.Lerp(B, I / (float)Count), Q = A.Lerp(B, (I + 1f) / Count);
                float Height = Id == 13 ? 48 : SideId == 0 ? 63 + I % 3 * 4 : SideId == 3 ? 40 + I % 3 * 5 : 52 + I % 2 * 7;
                Vector2 Lift = new(0, -Height), Across = (Q - P).Normalized().Orthogonal() * 5;
                Vector2[] Face;
                if (SideId is 1 or 2)
                    Face = new[] { P + new Vector2(2,0), Q + new Vector2(2,0), Q + Lift + new Vector2(9,0), P + Lift + new Vector2(9,0) };
                else
                    Face = new[] { P, Q, Q + Lift, P.Lerp(Q,.71f) + Lift + new Vector2(0,5),
                        P.Lerp(Q,.47f) + Lift - new Vector2(0,3), P.Lerp(Q,.22f) + Lift + new Vector2(0,3), P + Lift };
                Vector2[] Cap = { P + Lift - Across, Q + Lift - Across, Q + Lift + Across, P + Lift + Across };
                if (Id == 6 && SideId is 0 or 3)
                {
                    Cap = new Vector2[10];
                    for (int K = 0; K < 5; K++) { Cap[K] = Face[6-K]; Cap[9-K] = Face[6-K] - new Vector2(0,6); }
                }
                var Wire = new List<Vector2>();
                if (Id == 13)
                {
                    Vector2 Along = (Q - P).Normalized(); float Length = P.DistanceTo(Q);
                    // Cache clipped diamond mesh once rather than rebuilding wire geometry while drawing.
                    for (float Start = -Height; Start <= Length; Start += 12)
                    {
                        float Lo = Math.Max(0,-Start), Hi = Math.Min(Height,Length-Start);
                        if (Hi > Lo) { Wire.Add(P + Along * (Start+Lo) + Vector2.Up*Lo); Wire.Add(P + Along * (Start+Hi) + Vector2.Up*Hi); }
                    }
                    for (float Start = 0; Start <= Length+Height; Start += 12)
                    {
                        float Lo = Math.Max(0,Start-Length), Hi = Math.Min(Height,Start);
                        if (Hi > Lo) { Wire.Add(P + Along * (Start-Lo) + Vector2.Up*Lo); Wire.Add(P + Along * (Start-Hi) + Vector2.Up*Hi); }
                    }
                }
                CompoundBoundaries.Add(new(Id, SideId, P, Q, Height, Face, Cap, Wire.ToArray()));
            }
        }
        Side(NW, new(SE.X,NW.Y), 0);
        Side(NW, new(NW.X,SE.Y), 1);
        Side(new(SE.X,NW.Y), SE, 2);
        Side(new(NW.X,SE.Y), Gate - new Vector2(GateWidth/2,0), 3);
        Side(Gate + new Vector2(GateWidth/2,0), SE, 3);
    }

    void BuildRuinedCompound()
    {
        Vector2 C = CompoundSpot(6, RuinHalf);
        var Floor = new Rect2(C - RuinHalf, RuinHalf * 2);
        Floors.Add((Floor, true)); Compounds[6] = Floor; LmAt[6] = C;
        BuildCompoundPerimeter(6, Floor, 100);
        // The whole roof has fallen in; four standing, damaged walls describe the original base.
        Prop(C + new Vector2(-94,14), "lm_roof_fallen_0");
        CompoundLine(C + new Vector2(-130,7), C + new Vector2(-67,18), 15);
        Prop(C + new Vector2(103,48), "lm_roof_fallen_0");
        CompoundLine(C + new Vector2(75,40), C + new Vector2(135,51), 14);
        Prop(C + new Vector2(-107,-70), "lm_rack_0"); Solid(C + new Vector2(-107,-75), 15);
        Prop(C + new Vector2(81,-69), "lm_console_0");
        Prop(C + new Vector2(135,-60), "lm_mast_0");
        Prop(C + new Vector2(77,-13), "lm_radio_0");
        Prop(C + new Vector2(-133,80), "lm_generator_0"); Solid(C + new Vector2(-133,75), 15);
        Prop(C + new Vector2(-77,88), "lm_cable_0");
        Prop(C + new Vector2(136,87), "lm_crate_0");
        Prop(C + new Vector2(104,-88), "lm_crate_0");
        Prop(C + new Vector2(-49,-81), "lm_expedition_0");
        Prop(C + new Vector2(132,-4), "lm_drum_1");
        Prop(C + new Vector2(-129,-31), "lm_rubble_0");
        Prop(C + new Vector2(100,22), "lm_rubble_1");
        Prop(C + new Vector2(-62,54), "lm_rubble_1");
        Prop(C + new Vector2(65,88), "lm_rubble_0");
        Prop(C + new Vector2(-81,-99), "lm_scrap_0");
        Prop(C + new Vector2(-133,113), "lm_warning_0");
        Clear(C, RuinHalf.Length() + TL);
    }

    void BuildContainerCompound()
    {
        Vector2 C = CompoundSpot(13, YardHalf);
        var Floor = new Rect2(C - YardHalf, YardHalf * 2);
        Floors.Add((Floor, true)); Compounds[13] = Floor; LmAt[13] = C;
        BuildCompoundPerimeter(13, Floor, 144);
        // Rear bays, another long container at the front and a genuinely open end-on container.
        foreach (var (P, Sprite) in new[] {
            (C + new Vector2(-142,-105), "lm_container_1"),
            (C + new Vector2(142,-105), "lm_container_0"),
            (C + new Vector2(-157,108), "lm_container_0") })
        {
            Prop(P, Sprite);
            CompoundLine(P + new Vector2(-75,-8), P + new Vector2(75,-8), 18);
        }
        Vector2 Open = C + new Vector2(224,73);
        Prop(Open, "lm_container_end_0");
        CompoundLine(Open + Tl(-1,-2.6f), Open + Tl(-1,-.25f), 10);
        CompoundLine(Open + Tl(1,-2.6f), Open + Tl(1,-.25f), 10);
        CompoundLine(Open + Tl(-1,-2.6f), Open + Tl(1,-2.6f), 10);
        Prop(C + new Vector2(-110,12), "lm_forklift_0");
        CompoundLine(C + new Vector2(-133,5), C + new Vector2(-91,5), 15);
        Prop(C + new Vector2(-258,25), "lm_crane_0"); Solid(C + new Vector2(-258,25), 18);
        Prop(C + new Vector2(-89,-42), "lm_pallet_0");
        Prop(C + new Vector2(-81,-37), "lm_cargo_stack_0");
        Prop(C + new Vector2(91,48), "lm_cargo_stack_0");
        Prop(C + new Vector2(146,134), "lm_pallet_0");
        Prop(C + new Vector2(-256,143), "lm_cable_0");
        Prop(C + new Vector2(-52,139), "lm_expedition_0");
        Prop(C + new Vector2(261,143), "lm_console_0");
        Prop(C + new Vector2(93,163), "lm_warning_0");
        Prop(C + new Vector2(-95,161), "lm_warning_0");
        for (int I = 0; I < 6; I++)
            Prop(C + new Vector2(84 + I % 3 * 26, 106 + I / 3 * 28), "lm_drum_" + I % 2);
        Prop(C + new Vector2(-254,-37), "lm_cart_0");
        Prop(C + new Vector2(254,-151), "lm_crate_0");
        Prop(C + new Vector2(-250,-153), "lm_crate_0");
        Clear(C, YardHalf.Length() + TL);
    }

    public bool InCompoundPassage(Vector2 P)
    {
        foreach (var (Id, Floor) in Compounds)
            if (Id is 6 or 13 && Floor.Grow(48).HasPoint(P) && MathF.Abs(P.X-Floor.GetCenter().X) < 35) return true;
        return false;
    }

    void BuildCaveCompound() => BuildWalkableCave();
}
