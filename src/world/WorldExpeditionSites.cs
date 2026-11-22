using Godot;
using System.Collections.Generic;

public partial class WorldGeometry
{
    public readonly record struct ExpeditionSite(int Id, Vector2 Centre, Vector2 Half);
    // Native pixel-art vehicles share their physical footprint and foot anchor with the renderer.
    public readonly record struct ExpeditionVehicle(int Id, Vector2 Pos, int Model, float Tilt);
    public readonly record struct ExpeditionCliff(Vector2 A, Vector2 B, Vector2[] Face, Vector2[] Cap);
    public readonly Dictionary<int, ExpeditionSite> ExpeditionSites = new();
    public readonly List<ExpeditionVehicle> ExpeditionVehicles = new();
    public readonly List<ExpeditionCliff> ExpeditionCliffs = new();
    public static Vector2 ExpeditionHalf(int Id) => Id switch {
        5 => new(220,144), 7 => new(306,183), 11 => new(204,222), _ => Vector2.Zero
    };

    Vector2 ExpeditionCentre(int Id, Vector2 Half)
    {
        Vector2 C = CompoundSpot(Id, Half);
        ExpeditionSites[Id] = new(Id, C, Half); LmAt[Id] = C;
        // Keep random vegetation out of the designed ground and the walking corridors.
        Clear(C, Half.Length() + 12);
        CompoundEntrances[Id] = C + new Vector2(0, Half.Y - 12);
        return C;
    }

    void ExpeditionProp(Vector2 P, string Sprite) => Prop(P.Round(), Sprite);

    void ExpeditionCar(int Id, Vector2 P, int Model, float Tilt = 0)
    {
        ExpeditionVehicles.Add(new(Id, P, Model, Tilt));
        float Half = Model == 0 ? 70 : 82;
        // Preserve a path around the trucks. The body, rather than an oversized circular sprite, is solid.
        CompoundLine(P + new Vector2(-Half + 15, -15), P + new Vector2(Half - 15, -15), 22);
    }

    void BuildBaysideScene()
    {
        Vector2 C = ExpeditionCentre(5, ExpeditionHalf(5));
        ExpeditionCar(5, C, 0, -.12f);
        // A snapped roadside barrier, a winch and recovered cargo describe a failed rescue attempt.
        ExpeditionProp(C + new Vector2(-150, -74), "lm_barricade_0");
        ExpeditionProp(C + new Vector2(-79, -110), "lm_barricade_0");
        ExpeditionProp(C + new Vector2(157, 10), "lm_cable_0");
        Solid(C + new Vector2(157, 3), 14);
        ExpeditionProp(C + new Vector2(128, -73), "lm_crate_0");
        ExpeditionProp(C + new Vector2(178, -57), "lm_drum_1");
        ExpeditionProp(C + new Vector2(-134, 80), "lm_scrap_0");
        ExpeditionProp(C + new Vector2(-63, 111), "lm_rope_0");
        ExpeditionProp(C + new Vector2(141, 103), "lm_warning_0");
    }

    void BuildConvoyScene()
    {
        Vector2 C = ExpeditionCentre(7, ExpeditionHalf(7));
        // Symmetric foot anchors centre the whole convoy, rather than using the truck in one side lane.
        ExpeditionCar(7, C + new Vector2(-178, -38), 1, -.04f);
        ExpeditionCar(7, C + new Vector2(0, 76), 2, .035f);
        ExpeditionCar(7, C + new Vector2(178, -38), 3, -.07f);
        ExpeditionProp(C + new Vector2(-241, 117), "lm_barricade_0");
        ExpeditionProp(C + new Vector2(230, 123), "lm_barricade_0");
        ExpeditionProp(C + new Vector2(-135, -133), "lm_cargo_stack_0");
        ExpeditionProp(C + new Vector2(-49, -118), "lm_pallet_0");
        ExpeditionProp(C + new Vector2(42, -130), "lm_crate_0");
        ExpeditionProp(C + new Vector2(111, -117), "lm_drum_0");
        ExpeditionProp(C + new Vector2(5, -154), "lm_radio_0");
        ExpeditionProp(C + new Vector2(183, 88), "lm_scrap_0");
        ExpeditionProp(C + new Vector2(-133, 122), "lm_expedition_0");
        ExpeditionProp(C + new Vector2(249, -127), "lm_warning_0");
    }

    void BuildNorthwindScene()
    {
        Vector2 C = ExpeditionCentre(11, ExpeditionHalf(11));
        // Three exposed rock benches flank a generous central ascent; cached polygons are reused every frame.
        for (int I = 0; I < 3; I++)
            foreach (int Side in new[] { -1, 1 })
            {
                float Y = -132 + I * 108;
                Vector2 A = C + new Vector2(Side * 73, Y + 13);
                Vector2 B = C + new Vector2(Side * (177 - I * 10), Y - 3);
                Vector2 Lift = new(0, -28 - I * 4);
                Vector2 F1 = A.Lerp(B,.23f) + new Vector2(0,4), F2 = A.Lerp(B,.61f) - new Vector2(0,2);
                Vector2 T0 = A+Lift, T1 = F1+Lift-new Vector2(0,4), T2 = F2+Lift+new Vector2(0,2), T3 = B+Lift;
                Vector2[] Face = { A,F1,F2,B,T3,T2,T1,T0 };
                Vector2[] Cap = { T0,T1,T2,T3,T3+new Vector2(-Side*12,-24),T2+new Vector2(Side*2,-35),T1+new Vector2(Side*6,-29),T0+new Vector2(Side*15,-25) };
                ExpeditionCliffs.Add(new(A, B, Face, Cap)); CompoundLine(A, B, 12);
            }
        ExpeditionProp(C + new Vector2(-143, -187), "lm_windsock_0");
        ExpeditionProp(C + new Vector2(133, -183), "lm_cairn_0");
        Solid(C + new Vector2(133, -183), 12);
        ExpeditionProp(C + new Vector2(-135, 194), "lm_signpost_0");
        ExpeditionProp(C + new Vector2(134, 190), "lm_cairn_0");
        Solid(C + new Vector2(134, 190), 12);
        ExpeditionProp(C + new Vector2(168, -43), "lm_expedition_0");
    }

    public bool InExpeditionAisle(Vector2 P)
    {
        foreach (var Site in ExpeditionSites.Values)
        {
            Vector2 D = P - Site.Centre;
            if (Site.Id == 11 && System.MathF.Abs(D.X) < 63 && System.MathF.Abs(D.Y) < Site.Half.Y + 32) return true;
            if (Site.Id is 5 or 7 && System.MathF.Abs(D.Y - (Site.Id == 5 ? 60 : 4)) < 28 && System.MathF.Abs(D.X) < Site.Half.X + 20) return true;
        }
        return false;
    }
}
