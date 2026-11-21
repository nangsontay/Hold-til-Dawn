using Godot;
using System;
using System.Collections.Generic;
using HoldTilDawn;

// Owner: Vu Duc Viet Anh. Where the paths meet the rivers (src/world/IslandRivers.cs). A path that crosses on a
// bridge runs on over it. A path the water cuts anywhere else stops at the bank and turns along it to the nearest
// bridge on that side, so no path just ends at the water; one too far from any bridge ends at the bank.
public partial class WorldGeometry
{
    // the longest walk along a bank to a bridge, in tiles
    const float BANK_WALK = 45.0f;

    void CutPathsAtRivers()
    {
        if (IslandMap.Rivers.Count == 0) return;
        var Out = new List<(Vector2[] Path, float Width)>();
        var Extra = new List<(Vector2[] Path, float Width)>();
        for (int K = 0; K < Trails.Count; K++)
        {
            var (Path, Width) = Trails[K];
            var Pieces = CutPath(Path, Width);
            // the first piece keeps the path's place in the list (the door roads are the first three)
            Out.Add(Pieces.Count > 0 ? (Pieces[0], Width) : (Path, Width));
            for (int I = 1; I < Pieces.Count; I++) Extra.Add((Pieces[I], Width));
        }
        Trails.Clear();
        Trails.AddRange(Out);
        Trails.AddRange(Extra);
    }

    bool Wet(Vector2 P, float Width)
    {
        return IslandMap.InRiver(P, IslandMap.RIVER_BANK + Width / 2) && !IslandMap.NearBridge(P);
    }

    List<Vector2[]> CutPath(Vector2[] Path, float Width)
    {
        var Pieces = new List<Vector2[]>();
        int I = 0;
        while (I < Path.Length)
        {
            while (I < Path.Length && Wet(Path[I], Width)) I++;
            int Start = I;
            while (I < Path.Length && !Wet(Path[I], Width)) I++;
            if (I - Start < 2) continue;
            var Piece = new List<Vector2>(Path[Start..I]);
            // cut by water at either end: turn along the bank to a bridge
            if (I < Path.Length) Piece.AddRange(BankWalk(Path[I - 1], Width));
            if (Start > 0)
            {
                var Before = BankWalk(Path[Start], Width);
                Before.Reverse();
                Piece.InsertRange(0, Before);
            }
            Pieces.Add(Piece.ToArray());
        }
        if (Pieces.Count == 0) Pieces.Add(Path);
        return Pieces;
    }

    // From a path's last dry point by a river, along that bank to the nearest bridge on the river (empty when
    // there is none near enough, or the bank runs into the sea first).
    List<Vector2> BankWalk(Vector2 From, float Width)
    {
        var Walk = new List<Vector2>();
        int Best = -1, At = 0;
        float Bd = float.MaxValue;
        for (int R = 0; R < IslandMap.Rivers.Count; R++)
        {
            var P = IslandMap.Rivers[R];
            for (int J = 0; J < P.Length; J++)
            {
                float D = P[J].DistanceSquaredTo(From);
                if (D < Bd) { Bd = D; Best = R; At = J; }
            }
        }
        if (Best < 0) return Walk;
        var River = IslandMap.Rivers[Best];
        int Target = -1;
        float Steps = float.MaxValue;
        foreach (var (R, Bridge) in IslandMap.Bridges)
        {
            if (R != Best) continue;
            int Bi = 0;
            float Bbd = float.MaxValue;
            for (int J = 0; J < River.Length; J++)
            {
                float D = River[J].DistanceSquaredTo(Bridge);
                if (D < Bbd) { Bbd = D; Bi = J; }
            }
            if (Math.Abs(Bi - At) < Steps) { Steps = Math.Abs(Bi - At); Target = Bi; }
        }
        // the river's points are half a tile apart
        if (Target < 0 || Steps * 0.5f > BANK_WALK) return Walk;
        Vector2 Normal(int J)
        {
            Vector2 Along = (River[Math.Min(J + 1, River.Length - 1)] - River[Math.Max(J - 1, 0)]).Normalized();
            return new Vector2(-Along.Y, Along.X);
        }
        float Side = MathF.Sign((From - River[At]).Dot(Normal(At)));
        if (Side == 0) Side = 1;
        float Off = IslandMap.RIVER_HALF + IslandMap.RIVER_BANK + Width / 2 + 0.3f * TL;
        int Dir = Math.Sign(Target - At);
        // every other point (a tile apart), stopping where the bridge's deck begins
        for (int J = At; J != Target; J += Dir)
        {
            if ((J - At) % 2 != 0) continue;
            Vector2 P = River[J] + Normal(J) * Side * Off;
            if (IslandMap.Depth(P) < TL) return new List<Vector2>();
            if (IslandMap.NearBridge(P, 0.5f * TL)) break;
            Walk.Add(P);
        }
        // onto the end of the deck
        Vector2 Deck = River[Target] + Normal(Target) * Side * (IslandMap.BRIDGE_HALF_LEN - 0.5f * TL);
        Walk.Add(Deck);
        return Walk;
    }
}
