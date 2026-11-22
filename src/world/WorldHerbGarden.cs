using Godot;
using System;
using System.Collections.Generic;

public partial class WorldGeometry
{
    public static readonly Vector2 GardenHalf = new(8 * TL, 6 * TL);
    public Vector2 GardenCentre { get; private set; }
    public Rect2 GardenBounds => new(GardenCentre - GardenHalf, GardenHalf * 2);
    public Vector2 GardenEntrance => GardenCentre + new Vector2(0, GardenHalf.Y);
    public readonly List<Rect2> GardenBeds = new();
    public readonly List<(Vector2 A, Vector2 B)> GardenFence = new();

    void BuildAbandonedGarden()
    {
        GardenCentre = At(18).Round(); LmAt[18] = GardenCentre;
        GardenBeds.Clear(); GardenFence.Clear();
        foreach (float Y in new[] { -3.8f, -.3f, 3.2f })
            foreach (int Side in new[] { -1, 1 })
                GardenBeds.Add(new Rect2(GardenCentre + Tl(Side * 4.3f - 2.5f, Y - 1.1f), Tl(5, 2.2f)));
        Vector2 NW = GardenBounds.Position, SE = GardenBounds.End;
        void Fence(Vector2 A, Vector2 B)
        {
            CompoundLine(A, B, 8);
            int N = Math.Max(1, (int)MathF.Ceiling(A.DistanceTo(B) / (1.6f * TL)));
            for (int I = 0; I < N; I++) GardenFence.Add((A.Lerp(B, I / (float)N).Round(), A.Lerp(B, (I + 1f) / N).Round()));
        }
        Fence(NW, new(SE.X, NW.Y)); Fence(NW, new(NW.X, SE.Y)); Fence(new(SE.X, NW.Y), SE);
        Fence(new(NW.X, SE.Y), GardenEntrance - Tl(1.5f, 0));
        Fence(GardenEntrance + Tl(1.5f, 0), SE);
        Prop(GardenEntrance, "lm_arch_0");
        Solid(GardenEntrance + new Vector2(-46, 0), 8); Solid(GardenEntrance + new Vector2(46, 0), 8);
        Prop(GardenCentre + Tl(-6.7f, -5.0f), "lm_waterpump_0");
        Prop(GardenCentre + Tl(6.7f, -5.0f), "lm_rainbarrel_0");
        Prop(GardenCentre + Tl(6.5f, 5.0f), "lm_pots_0");
        Prop(GardenCentre + Tl(-6.5f, 5.0f), "lm_can_0");
        Clear(GardenCentre, GardenHalf.Length() + TL);
        CompoundEntrances[18] = GardenEntrance + Tl(0, 1.2f);
    }

    void AddGardenHerbs()
    {
        int I = 0;
        foreach (var Bed in GardenBeds)
            foreach (float Y in new[] { -.43f, .43f })
                foreach (float X in new[] { -1.7f, -.57f, .57f, 1.7f })
                {
                    Vector2 P = (Bed.GetCenter() + Tl(X, Y)).Round();
                    HarvestSites.Add(new(P, "node_herb_" + I++ % 2, 2, 0));
                }
    }
}
