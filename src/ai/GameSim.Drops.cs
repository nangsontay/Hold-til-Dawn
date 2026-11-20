using Godot;
using Environment = Godot.Environment;
using FileAccess = Godot.FileAccess;
using System;
using System.Threading.Tasks;
using HoldTilDawn;

// Owner: Vu Duc Viet Anh. Enemy drops (MEC-23, BAL-41, FML-15).
public partial class GameSim : RefCounted
{
    public static float DROP_MUL = AiSettings.Current.DropMultiplier;
    public static readonly dynamic DROP_TTL = 30.0f;
    public static readonly dynamic DROPS = new GArray(new GArray(0.1f, new GArray("wood", "stone", "herb"), 1, 2), new GArray(0.15f, new GArray("bandage"), 1, 1), new GArray(0.2f, new GArray("titanium"), 2, 2));

    public void Drop(dynamic E)
    {
        // summoned Grunts drop nothing (BAL-43)
        if (Gd.Truth(Gd.Get(E, "summon", false)))
        {
            return;
        }
        if ((E.Type == BOSS))
        {
            this.AddDrop(E.Pos, "titanium", 10);
            this.AddDrop((E.Pos + Gd.MakeVector2((0.6f * T), 0)), "bandage", 3);
            return;
        }
        dynamic D = Gd.At(DROPS, E.Type);
        if ((Rng.Randf() < Gd.Minf(1.0f, (Gd.At(D, 0) * DROP_MUL))))
        {
            this.AddDrop(E.Pos, Gd.At(Gd.At(D, 1), (Rng.Randi() % Gd.Size(Gd.At(D, 1)))), Rng.RandiRange(Gd.At(D, 2), Gd.At(D, 3)));
        }
    }

    public void AddDrop(dynamic Pos, dynamic Item, dynamic N)
    {
        Gd.Append(Drops, new GDict { { "pos", Pos }, { "item", Item }, { "n", N }, { "ttl", DROP_TTL } });
    }

    public void TickDrops(dynamic Dt)
    {
        foreach (dynamic D in Gd.Iter(Drops))
        {
            // items dropped by players never vanish and are picked up with E (GameSim.Inventory.cs)
            if (Gd.Truth(Gd.Get(D, "player", false)))
            {
                continue;
            }
            D.Ttl -= Dt;
            foreach (dynamic P in Gd.Iter(Gd.Values(Players)))
            {
                // MEC-23: walk over it with free inventory space, never from inside the base
                if (((D.Ttl > 0) && (P.State == 0) && !(Gd.Truth(GameSim.InsideBase(P.Pos))) && (P.Pos.DistanceTo(D.Pos) <= (0.8f * T)) && (this.Room(P, D.Item) > 0)))
                {
                    int Left = GameSim.AddTo(P.Inv, D.Item, Gd.Int(D.N));
                    this.Note(P, Gd.Format("+%d %s", new GArray((D.N - Left), GameSim.ItemName(D.Item))));
                    D.N = Left;
                    if ((Left == 0))
                    {
                        D.Ttl = 0.0f;
                    }
                }
            }
        }
        Drops = Gd.Filter(Drops, (Func<dynamic, dynamic>)((D) => (Gd.Truth(Gd.Get(D, "player", false)) || (D.Ttl > 0))));
    }
}
