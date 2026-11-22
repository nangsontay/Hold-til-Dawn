using Godot;
using System.Collections.Generic;

public partial class WorldGeometry
{
    public readonly List<(Vector2 Foot, int Side)> FishingRods = new();
    public readonly List<Vector2> FishingFish = new();
    public readonly List<(Vector2 At, int Kind)> FishingLitter = new();
    public readonly List<Vector2[]> FishingLitterFaces = new();

    void BuildFishingDetails(Vector2 Land)
    {
        FishingRods.Clear(); FishingFish.Clear(); FishingLitter.Clear(); FishingLitterFaces.Clear();
        Rect2 Deck = IslandMap.Docks[0];
        // Feet sit on the deck edge, with lines hanging out over the sea.
        foreach (var (Fraction, Side) in new[] { (.18f, -1), (.47f, 1), (.76f, -1) })
            FishingRods.Add((new Vector2(Mathf.Lerp(Deck.Position.X, Deck.End.X, Fraction), Land.Y + Side * 20 * LANDMARK_SCALE).Round(), Side));
        for (int I = 0; I < 8; I++)
        {
            int Side = I % 2 == 0 ? -1 : 1;
            Vector2 P = new(Mathf.Lerp(Deck.Position.X - 34, Deck.End.X - 48, (I + .5f) / 8), Land.Y + Side * (66 + I % 3 * 24));
            if (!IslandMap.OnLand(P, -16) && !IslandMap.OnLand(P + new Vector2(28, 0), -12)
                && !IslandMap.OnLand(P - new Vector2(28, 0), -12) && !Deck.Grow(24).HasPoint(P)) FishingFish.Add(P.Round());
        }
        for (int I = 0; I < 12; I++)
        {
            Vector2 P = I < 5
                ? new Vector2(Mathf.Lerp(Deck.Position.X + 30, Deck.End.X - 26, I / 5f), Land.Y + (I % 2 == 0 ? -1 : 1) * 17)
                : Land + Tl(.8f + I % 3 * .7f, (I % 2 == 0 ? -1 : 1) * (1.0f + I % 3 * .6f));
            if (I < 5 || IslandMap.OnLand(P, 6))
            {
                P = P.Round(); FishingLitter.Add((P, I % 4));
                FishingLitterFaces.Add(new[] { P + new Vector2(-6, -3), P + new Vector2(3, -5), P + new Vector2(8, 1), P + new Vector2(-2, 4) });
            }
        }
    }
}
