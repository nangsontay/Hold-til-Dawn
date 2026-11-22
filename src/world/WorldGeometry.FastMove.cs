using Godot;
using System;
using System.Collections.Generic;
using HoldTilDawn;

// Owner: Vu Duc Viet Anh. Typed copies of the colliders (Solids and Buckets) for the moves made every tick: each
// enemy, bot and player body is slid along the scenery and the base walls up to twice a tick, so at night with
// dozens of enemies the dynamic versions (WorldGeometry.Resolve / MoveBody) kept a laptop's CPU flat out. Same
// sums in the same order, so every body ends up exactly where the dynamic versions put it.
public partial class WorldGeometry
{
    (Vector2 C, float R)[] SolidT = Array.Empty<(Vector2, float)>();
    readonly Dictionary<Vector2I, int[]> BucketT = new();
    readonly List<int> NearT = new();
    int[] SeenT = Array.Empty<int>();
    int StampT;

    // after Rebuild has filled Solids and Buckets
    void MirrorSolids()
    {
        int N = Gd.Size(Solids);
        SolidT = new (Vector2, float)[N];
        for (int I = 0; I < N; I++)
        {
            dynamic S = Gd.At(Solids, I);
            SolidT[I] = ((Vector2)Gd.At(S, 0), Gd.Float(Gd.At(S, 1)));
        }
        SeenT = new int[N];
        StampT = 0;
        BucketT.Clear();
        foreach (dynamic Key in Gd.Iter(Gd.Keys(Buckets)))
        {
            dynamic List = Gd.At(Buckets, Key);
            var Ids = new int[Gd.Size(List)];
            for (int I = 0; I < Ids.Length; I++) Ids[I] = Gd.Int(Gd.At(List, I));
            BucketT[(Vector2I)Key] = Ids;
        }
    }

    // Candidates(): the solids in the cells round the segment, each once, in the order they are first met
    List<int> CandidatesFast(Vector2 A, Vector2 B, float Radius)
    {
        NearT.Clear();
        if (++StampT == int.MaxValue)
        {
            Array.Clear(SeenT);
            StampT = 1;
        }
        float Cell = Gd.Float(CELL);
        var Lo = (Vector2I)((A.Min(B) - Vector2.One * Radius) / Cell).Floor();
        var Hi = (Vector2I)((A.Max(B) + Vector2.One * Radius) / Cell).Floor();
        for (int Y = Lo.Y; Y < Hi.Y + 1; Y++)
            for (int X = Lo.X; X < Hi.X + 1; X++)
            {
                if (!BucketT.TryGetValue(new Vector2I(X, Y), out var Ids)) continue;
                foreach (int I in Ids)
                {
                    if (SeenT[I] == StampT) continue;
                    SeenT[I] = StampT;
                    NearT.Add(I);
                }
            }
        return NearT;
    }

    // Resolve(), typed
    public Vector2 ResolveFast(Vector2 Pos, float Radius)
    {
        var Nearby = CandidatesFast(Pos, Pos, Radius + 32.0f);
        for (int Pass = 0; Pass < 3; Pass++)
            foreach (int I in Nearby)
            {
                var (C, R) = SolidT[I];
                Vector2 D = Pos - C;
                float Need = Radius + R;
                if (D.LengthSquared() < Need * Need)
                    Pos = C + ((D.LengthSquared() > 0.0001f) ? D.Normalized() : Vector2.Down) * Need;
            }
        return Pos;
    }

    // MoveBody(), typed: 4-pixel steps, each pushed out of the scenery and then of the wall pieces
    public Vector2 MoveBodyFast(Vector2 From, Vector2 To, float Radius, Rect2[] Walls)
    {
        int Count = Math.Max(1, Mathf.CeilToInt(From.DistanceTo(To) / 4.0f));
        Vector2 Step = (To - From) / Count;
        Vector2 Pos = From;
        for (int I = 0; I < Count; I++)
        {
            Vector2 Next = ResolveFast(Pos + Step, Radius);
            foreach (Rect2 W in Walls)
            {
                Vector2 Q = Next.Clamp(W.Position, W.End);
                Vector2 D = Next - Q;
                if (D.LengthSquared() < Radius * Radius && D.LengthSquared() > 1e-06f)
                    Next = Q + D.Normalized() * Radius;
            }
            Pos = Next;
        }
        return Pos;
    }
}
