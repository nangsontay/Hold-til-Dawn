using Godot;
using System;
using System.Collections.Generic;
using HoldTilDawn;

public partial class WorldGeometry
{
    readonly List<(Vector2 Pos, float Radius)> NextDynamicColliders = new(), PreviousDynamicColliders = new();
    object PreviousStaticColliders, PreviousBuiltColliders;
    int PreviousStaticCount = -1;

    GArray UpdatedColliders(GArray Nodes, GArray Towers, GArray Walls)
    {
        NextDynamicColliders.Clear();
        if (Walls != null)
            foreach (GDict Wall in Walls)
            {
                Vector2 P = (Vector2)Wall.Get("pos"), Along = Gd.Truth(Wall.Get("vert")) ? Vector2.Down : Vector2.Right;
                for (int I = -1; I <= 1; I++) NextDynamicColliders.Add((P + Along * I * 32, WALL_PIECE * 32));
            }
        foreach (GDict Node in Nodes)
        {
            float Radius = Harvestables.Radius(Node);
            if (Gd.Int(Node.Get("amount")) > 0 && Radius > 0) NextDynamicColliders.Add(((Vector2)Node.Get("pos"), Radius));
        }
        foreach (object Entry in Towers)
            if (Entry is GDict Tower) NextDynamicColliders.Add(((Vector2)Tower.Get("pos"), 0.7f * 32));
        bool Same = ReferenceEquals(PreviousStaticColliders, StaticSolids) && PreviousStaticCount == ((GArray)StaticSolids).Count
            && ReferenceEquals(PreviousBuiltColliders, Solids) && NextDynamicColliders.Count == PreviousDynamicColliders.Count;
        if (Same)
            for (int I = 0; I < NextDynamicColliders.Count; I++)
                if (NextDynamicColliders[I] != PreviousDynamicColliders[I]) { Same = false; break; }
        if (Same) return null;
        var Next = new GArray();
        foreach (object Static in (GArray)StaticSolids) Next.Add(Static);
        foreach (var (Pos, Radius) in NextDynamicColliders) Next.Add(new GArray(Pos, Radius, true));
        PreviousDynamicColliders.Clear();
        PreviousDynamicColliders.AddRange(NextDynamicColliders);
        PreviousStaticColliders = StaticSolids;
        PreviousStaticCount = ((GArray)StaticSolids).Count;
        PreviousBuiltColliders = Next;
        return Next;
    }
}
