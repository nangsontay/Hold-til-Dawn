using Godot;
using Environment = Godot.Environment;
using FileAccess = Godot.FileAccess;
using System;
using System.Threading.Tasks;
using HoldTilDawn;

public partial class WorldGeometry : RefCounted
{
    // Shared world data. No rendering or SceneTree is needed on the dedicated server.
    public static readonly dynamic T = 32.0f;
    public static readonly dynamic ISLAND = (72.0f * T);
    public static readonly dynamic HALF = (Gd.MakeVector2(5.5f, 3.5f) * T);
    public static readonly dynamic SAFE_D = (20.0f * T);
    public static readonly dynamic CELL = 128.0f;
    public static readonly dynamic COLD = Gd.MakeColor(0.8f, 0.88f, 0.92f);
    public static readonly dynamic LAYOUT = Gd.Load("res://maps/island.tscn");
    public dynamic Deco = new GArray();
    public dynamic Scatter = new GArray();
    public dynamic Solids = new GArray();
    public dynamic StaticSolids = new GArray();
    public dynamic Buckets = new GDict {  };
    public dynamic Grids = new GDict {  };
    public dynamic Paths = new GDict {  };
    public dynamic Revision = 0;
    public WorldGeometry()
    {
        this.Generate();
    }

    public static dynamic WallDist(dynamic P)
    {
        return Gd.Length(Gd.MakeVector2(Gd.Maxf((Gd.Absf(P.X) - HALF.X), 0), Gd.Maxf((Gd.Absf(P.Y) - HALF.Y), 0)));
    }

    public static dynamic Lane(dynamic P)
    {
        return (((Gd.Absf(P.Y) < (2.5f * T)) && (Gd.Absf(P.X) > HALF.X)) || ((Gd.Absf(P.X) < (2.5f * T)) && (P.Y < (-HALF.Y))));
    }

    public void Generate()
    {
        Gd.Clear(Deco);
        Gd.Clear(Scatter);
        dynamic Random = Gd.Wrap(new RandomNumberGenerator());
        Random.Seed = 7;
        dynamic Pines = new GArray("Pine_1", "Pine_2", "Pine_3", "Pine_4", "Pine_5");
        dynamic Dead = new GArray("DeadTree_1", "DeadTree_2", "DeadTree_3", "DeadTree_4", "DeadTree_5");
        dynamic Rocks = new GArray("Rock_Medium_1", "Rock_Medium_2", "Rock_Medium_3");
        dynamic Variants = Gd.FromVariant(Json.ParseString(Gd.Static(typeof(FileAccess), "get_file_as_string", "res://art/nature/index.json")));
        dynamic Names = new GDict {  };
        foreach (dynamic N in Gd.Iter(Variants))
        {
            dynamic Base = Gd.Rsplit(N, "_", true, 1);
            Names[Gd.At(Base, 0)] = Gd.Maxi(Gd.Get(Names, Gd.At(Base, 0), 0), (Gd.ToInt(Gd.At(Base, 1)) + 1));
        }
        dynamic Variant = (Func<dynamic, dynamic, dynamic>)((N, K) => Gd.Format("%s_%d", new GArray(N, (Gd.Absi(K) % Gd.Maxi(1, Gd.Get(Names, N, 1))))));
        dynamic Outer = (Func<dynamic, dynamic>)((P) => ((WorldGeometry.WallDist(P) > (SAFE_D + (2 * T))) && !(Gd.Truth(WorldGeometry.Lane(P))) && (Gd.Length(P) < (ISLAND - (2 * T)))));
        dynamic Ring = (Func<dynamic>)(() => (Vector2.FromAngle((Random.Randf() * Mathf.Tau)) * Random.RandfRange((HALF.X + SAFE_D), (ISLAND - (4 * T)))));
        foreach (dynamic C in Gd.Iter(44))
        {
            Vector2 Centre = (Vector2)(Ring());
            foreach (dynamic K in Gd.Iter(Random.RandiRange(4, 10)))
            {
                dynamic P = (Centre + (Vector2.FromAngle((Random.Randf() * Mathf.Tau)) * Random.RandfRange(0, (5.5f * T))));
                if (Gd.Truth(Outer(P)))
                {
                    dynamic Pool = ((Random.Randf() < 0.2f) ? (dynamic)(Dead) : Pines);
                    Gd.Append(Deco, new GArray(P, Variant(Gd.At(Pool, (Random.Randi() % Gd.Size(Pool))), Random.Randi()), Random.RandfRange(0.8f, 1.2f), COLD));
                }
            }
        }
        foreach (dynamic C in Gd.Iter(24))
        {
            Vector2 Centre = (Vector2)(Ring());
            foreach (dynamic K in Gd.Iter(Random.RandiRange(3, 6)))
            {
                dynamic P = (Centre + (Vector2.FromAngle((Random.Randf() * Mathf.Tau)) * Random.RandfRange(0, (2.2f * T))));
                if (Gd.Truth(Outer(P)))
                {
                    Gd.Append(Deco, new GArray(P, Variant(Gd.At(Rocks, (Random.Randi() % Gd.Size(Rocks))), Random.Randi()), Random.RandfRange(0.8f, 1.6f), COLD));
                }
            }
        }
        foreach (dynamic I in Gd.Iter(40))
        {
            Vector2 P = (Vector2)(Ring());
            if (Gd.Truth(Outer(P)))
            {
                Gd.Append(Deco, new GArray(P, Variant(Gd.At(Dead, (Random.Randi() % Gd.Size(Dead))), Random.Randi()), Random.RandfRange(0.8f, 1.1f), COLD));
            }
        }
        foreach (dynamic I in Gd.Iter(400))
        {
            dynamic P = ((Vector2.FromAngle((Random.Randf() * Mathf.Tau)) * ISLAND) * Gd.Sqrt(Random.Randf()));
            dynamic Wd = WorldGeometry.WallDist(P);
            if (((Wd > (3 * T)) && (Wd < SAFE_D) && !(Gd.Truth(WorldGeometry.Lane(P))) && (Random.Randf() < 0.12f)))
            {
                Gd.Append(Deco, new GArray(P, "Bush_Common_0", Random.RandfRange(0.6f, 0.9f), COLD));
            }
        }
        dynamic Flat = new GArray("Grass_Wispy_Short", "Grass_Wispy_Tall", "Grass_Common_Short", "Grass_Wispy_Short", "Grass_Wispy_Tall", "Pebble_Round_1", "Pebble_Round_3", "Pebble_Square_2", "Pebble_Square_4", "Pebble_Round_5");
        while ((Gd.Size(Scatter) < 1400))
        {
            dynamic P = ((Vector2.FromAngle((Random.Randf() * Mathf.Tau)) * ISLAND) * Gd.Sqrt(Random.Randf()));
            if (((Gd.Absf(P.X) > (HALF.X + (2.6f * T))) || (Gd.Absf(P.Y) > (HALF.Y + (2.6f * T)))))
            {
                Gd.Append(Scatter, new GArray(P, Variant(Gd.At(Flat, (Random.Randi() % Gd.Size(Flat))), Random.Randi()), Random.RandfRange(0.8f, 1.2f)));
            }
        }
        // Hand-placed objects are editable with Godot's 2D editor.
        dynamic Layout = LAYOUT.Instantiate();
        Gd.Clear(StaticSolids);
        foreach (dynamic D in Gd.Iter(Deco))
        {
            if (!(Gd.Truth(Gd.BeginsWith(Gd.At(D, 1), "Bush"))))
            {
                Gd.Append(StaticSolids, new GArray(Gd.At(D, 0), (((Gd.Truth(Gd.BeginsWith(Gd.At(D, 1), "Rock")) ? (dynamic)(0.75f) : 0.3f) * T) * Gd.At(D, 2)), true));
            }
        }
        foreach (dynamic Child in Gd.Iter(Layout.GetChildren()))
        {
            if ((Gd.IsType(Child, typeof(Sprite2D)) && (Child.Texture != null)))
            {
                Gd.Append(Deco, new GArray(Child.Position, Gd.GetBasename(Gd.GetFile(Child.Texture.ResourcePath)), Child.Scale.X, Child.Modulate));
                if ((Child.SolidRadius > 0))
                {
                    Gd.Append(StaticSolids, new GArray(Child.Position, (Child.SolidRadius * Gd.Absf(Child.Scale.X)), Child.BlocksAttacks));
                }
            }
        }
        Layout.Free();
        Gd.Append(StaticSolids, new GArray(Gd.MakeVector2(0, ((0.3f * T) - 6)), 22.0f, true));
        foreach (dynamic X in Gd.Iter(new GArray((-10.0f), 0.0f, 10.0f)))
        {
            Gd.Append(StaticSolids, new GArray(Gd.MakeVector2(X, ((1.6f * T) - 4)), 7.0f, true));
        }
        Gd.SortCustom(Scatter, (Func<dynamic, dynamic, dynamic>)((A, B) => (Gd.At(A, 0).Y < Gd.At(B, 0).Y)));
        this.Rebuild(new GArray(), new GArray());
    }

    public void Rebuild(dynamic Nodes, dynamic Towers)
    {
        dynamic Next = Gd.Duplicate(StaticSolids);
        foreach (dynamic Nd in Gd.Iter(Nodes))
        {
            float R = Gd.Float((Gd.At(new GArray(0.25f, 0.62f, 0.0f, 0.6f), Nd.Type) * T));
            if (((Nd.Amount > 0) && (R > 0)))
            {
                Gd.Append(Next, new GArray(Nd.Pos, R, true));
            }
        }
        foreach (dynamic Tw in Gd.Iter(Towers))
        {
            if ((Tw != null))
            {
                Gd.Append(Next, new GArray(Tw.Pos, (0.7f * T), true));
            }
        }
        if ((Next == Solids))
        {
            return;
        }
        Solids = Next;
        Gd.Clear(Buckets);
        Gd.Clear(Grids);
        Gd.Clear(Paths);
        Revision += 1;
        foreach (dynamic I in Gd.Iter(Gd.Size(Solids)))
        {
            dynamic S = Gd.At(Solids, I);
            dynamic Lo = Gd.MakeVector2I(((Gd.At(S, 0) - (Vector2.One * Gd.At(S, 1))) / CELL));
            dynamic Hi = Gd.MakeVector2I(((Gd.At(S, 0) + (Vector2.One * Gd.At(S, 1))) / CELL));
            // floor is essential for negative world coordinates.
            Lo = Gd.MakeVector2I(((Gd.At(S, 0) - (Vector2.One * Gd.At(S, 1))) / CELL).Floor());
            Hi = Gd.MakeVector2I(((Gd.At(S, 0) + (Vector2.One * Gd.At(S, 1))) / CELL).Floor());
            foreach (dynamic Y in Gd.Iter(Gd.Range(Lo.Y, (Hi.Y + 1))))
            {
                foreach (dynamic X in Gd.Iter(Gd.Range(Lo.X, (Hi.X + 1))))
                {
                    dynamic Key = Gd.MakeVector2I(X, Y);
                    if (!(Gd.Truth(Gd.Has(Buckets, Key))))
                    {
                        Buckets[Key] = new GArray();
                    }
                    Gd.Append(Gd.At(Buckets, Key), I);
                }
            }
        }
    }

    public dynamic Candidates(dynamic A, dynamic B, dynamic Radius)
    {
        dynamic Lo = Gd.MakeVector2I(((A.Min(B) - (Vector2.One * Radius)) / CELL).Floor());
        dynamic Hi = Gd.MakeVector2I(((A.Max(B) + (Vector2.One * Radius)) / CELL).Floor());
        dynamic Found = new GDict {  };
        foreach (dynamic Y in Gd.Iter(Gd.Range(Lo.Y, (Hi.Y + 1))))
        {
            foreach (dynamic X in Gd.Iter(Gd.Range(Lo.X, (Hi.X + 1))))
            {
                foreach (dynamic I in Gd.Iter(Gd.Get(Buckets, Gd.MakeVector2I(X, Y), new GArray())))
                {
                    Found[I] = true;
                }
            }
        }
        return Gd.Keys(Found);
    }

    public dynamic Resolve(dynamic Pos, dynamic Radius)
    {
        dynamic Nearby = this.Candidates(Pos, Pos, (Radius + 32.0f));
        foreach (dynamic PassIndex in Gd.Iter(3))
        {
            foreach (dynamic I in Gd.Iter(Nearby))
            {
                dynamic S = Gd.At(Solids, I);
                Vector2 D = (Vector2)((Pos - Gd.At(S, 0)));
                float Need = Gd.Float((Radius + Gd.At(S, 1)));
                if ((D.LengthSquared() < (Need * Need)))
                {
                    Pos = (Gd.At(S, 0) + (((D.LengthSquared() > 0.0001f) ? (dynamic)(D.Normalized()) : Vector2.Down) * Need));
                }
            }
        }
        return Pos;
    }

    public dynamic MoveBody(dynamic From, dynamic To, dynamic Radius, dynamic Walls)
    {
        dynamic Count = Gd.Maxi(1, Gd.Ceili((From.DistanceTo(To) / 4.0f)));
        dynamic Step = ((To - From) / Count);
        dynamic Pos = From;
        foreach (dynamic I in Gd.Iter(Count))
        {
            dynamic Next = this.Resolve((Pos + Step), Radius);
            foreach (dynamic W in Gd.Iter(Walls))
            {
                dynamic Q = Next.Clamp(W.Position, W.End);
                dynamic D = (Next - Q);
                if (((D.LengthSquared() < (Radius * Radius)) && (D.LengthSquared() > 1e-06f)))
                {
                    Next = (Q + (D.Normalized() * Radius));
                }
            }
            Pos = Next;
        }
        return Pos;
    }

    public static dynamic CircleHit(dynamic A, dynamic B, dynamic Centre, dynamic Radius)
    {
        // Fraction of a finite segment at first contact; INF means no contact.
        dynamic Start = (A - Centre);
        if ((Start.LengthSquared() <= (Radius * Radius)))
        {
            return 0.0f;
        }
        dynamic Step = (B - A);
        dynamic Length2 = Step.LengthSquared();
        if ((Length2 < 1e-06f))
        {
            return float.PositiveInfinity;
        }
        dynamic Dot = Start.Dot(Step);
        dynamic Disc = ((Dot * Dot) - (Length2 * (Start.LengthSquared() - (Radius * Radius))));
        if ((Disc < 0))
        {
            return float.PositiveInfinity;
        }
        dynamic Hit = (((-Dot) - Gd.Sqrt(Disc)) / Length2);
        return (((Hit >= 0.0f) && (Hit <= 1.0f)) ? (dynamic)(Hit) : float.PositiveInfinity);
    }

    public dynamic FirstHit(dynamic A, dynamic B, dynamic Radius = null, dynamic Attacks = null, dynamic Ignore = null)
    {
        Radius ??= 0.0f;
        Attacks ??= false;
        Ignore ??= Vector2.Inf;
        dynamic Best = float.PositiveInfinity;
        foreach (dynamic I in Gd.Iter(this.Candidates(A, B, Radius)))
        {
            dynamic S = Gd.At(Solids, I);
            if (((Gd.At(S, 1) <= 0) || Gd.Truth(Gd.At(S, 0).IsEqualApprox(Ignore)) || (Gd.Truth(Attacks) && !(Gd.Truth(Gd.At(S, 2))))))
            {
                continue;
            }
            Best = Gd.Minf(Best, WorldGeometry.CircleHit(A, B, Gd.At(S, 0), (Gd.At(S, 1) + Radius)));
        }
        return Best;
    }

    public dynamic Waypoint(dynamic From, dynamic To, dynamic Radius, dynamic Walls)
    {
        // A resource/tower is a valid destination: walk up to its surface, not its centre.
        if (((this.FirstHit(From, To, Radius, false, To) == float.PositiveInfinity) && !(Gd.Truth(Gd.Any(Walls, (Func<dynamic, dynamic>)((W) => (WorldGeometry.SegmentBox(From, To, W.Grow(Radius)) < float.PositiveInfinity)))))))
        {
            return To;
        }
        dynamic Key = Gd.Format("%d:%s", new GArray(Gd.Roundi((Radius * 10)), Gd.Str(Walls)));
        dynamic Grid = Gd.Get(Grids, Key);
        if ((Grid == null))
        {
            Grid = Gd.Wrap(new AStarGrid2D());
            Grid.Region = Gd.MakeRect2I((-73), (-73), 147, 147);
            Grid.CellSize = (Vector2.One * T);
            Grid.DiagonalMode = Gd.EnumValue(typeof(AStarGrid2D), "DIAGONAL_MODE_ONLY_IF_NO_OBSTACLES", 3);
            Grid.Update();
            foreach (dynamic S in Gd.Iter(Solids))
            {
                float Reach = Gd.Float(((Gd.At(S, 1) + Radius) + 1.0f));
                dynamic Lo = Gd.MakeVector2I(((Gd.At(S, 0) - (Vector2.One * Reach)) / T).Floor());
                dynamic Hi = Gd.MakeVector2I(((Gd.At(S, 0) + (Vector2.One * Reach)) / T).Ceil());
                foreach (dynamic Y in Gd.Iter(Gd.Range(Lo.Y, (Hi.Y + 1))))
                {
                    foreach (dynamic X in Gd.Iter(Gd.Range(Lo.X, (Hi.X + 1))))
                    {
                        dynamic Cell = Gd.MakeVector2I(X, Y);
                        if ((Gd.Truth(Grid.IsInBoundsv(Cell)) && ((Gd.MakeVector2(Cell) * T).DistanceTo(Gd.At(S, 0)) < Reach)))
                        {
                            Grid.SetPointSolid(Cell);
                        }
                    }
                }
            }
            foreach (dynamic W in Gd.Iter(Walls))
            {
                dynamic Box = W.Grow((Radius + 1));
                dynamic Lo = Gd.MakeVector2I((Box.Position / T).Floor());
                dynamic Hi = Gd.MakeVector2I((Box.End / T).Ceil());
                foreach (dynamic Y in Gd.Iter(Gd.Range(Lo.Y, (Hi.Y + 1))))
                {
                    foreach (dynamic X in Gd.Iter(Gd.Range(Lo.X, (Hi.X + 1))))
                    {
                        dynamic Cell = Gd.MakeVector2I(X, Y);
                        if ((Gd.Truth(Grid.IsInBoundsv(Cell)) && Gd.Truth(Box.HasPoint((Gd.MakeVector2(Cell) * T)))))
                        {
                            Grid.SetPointSolid(Cell);
                        }
                    }
                }
            }
            Grids[Key] = Grid;
        }
        dynamic Start = this.OpenCell(Grid, From);
        dynamic End = this.OpenCell(Grid, To);
        dynamic PathKey = new GArray(Key, Start, End);
        dynamic Path = Gd.Get(Paths, PathKey, new GArray());
        if (Gd.Truth(Gd.IsEmpty(Path)))
        {
            Path = Grid.GetIdPath(Start, End, true);
            if ((Gd.Size(Paths) > 1024))
            {
                Gd.Clear(Paths);
            }
            Paths[PathKey] = Path;
        }
        foreach (dynamic J in Gd.Iter(Gd.Range(Gd.Mini((Gd.Size(Path) - 1), 5), (-1), (-1))))
        {
            dynamic P = (Gd.MakeVector2(Gd.At(Path, J)) * T);
            if ((From.DistanceTo(P) < 2))
            {
                continue;
            }
            if (((this.FirstHit(From, P, Radius) == float.PositiveInfinity) && !(Gd.Truth(Gd.Any(Walls, (Func<dynamic, dynamic>)((W) => (WorldGeometry.SegmentBox(From, P, W.Grow(Radius)) < float.PositiveInfinity)))))))
            {
                return P;
            }
        }
        return From;
    }

    public dynamic OpenCell(dynamic Grid, dynamic Pos)
    {
        dynamic Cell = Gd.MakeVector2I((Pos / T).Round());
        foreach (dynamic Ring in Gd.Iter(7))
        {
            dynamic Best = Cell;
            dynamic Distance = float.PositiveInfinity;
            foreach (dynamic Y in Gd.Iter(Gd.Range((-Ring), (Ring + 1))))
            {
                foreach (dynamic X in Gd.Iter(Gd.Range((-Ring), (Ring + 1))))
                {
                    dynamic C = (Cell + Gd.MakeVector2I(X, Y));
                    if ((Gd.Truth(Grid.IsInBoundsv(C)) && !(Gd.Truth(Grid.IsPointSolid(C)))))
                    {
                        dynamic D = (Gd.MakeVector2(C) * T).DistanceSquaredTo(Pos);
                        if ((D < Distance))
                        {
                            Distance = D;
                            Best = C;
                        }
                    }
                }
            }
            if ((Distance < float.PositiveInfinity))
            {
                return Best;
            }
        }
        return Cell;
    }

    public static dynamic SegmentBox(dynamic A, dynamic B, dynamic Box)
    {
        dynamic Enter = 0.0f;
        dynamic Leave = 1.0f;
        dynamic Step = (B - A);
        foreach (dynamic Axis in Gd.Iter(2))
        {
            if ((Gd.Absf(Gd.At(Step, Axis)) < 1e-05f))
            {
                if (((Gd.At(A, Axis) < Gd.At(Box.Position, Axis)) || (Gd.At(A, Axis) > Gd.At(Box.End, Axis))))
                {
                    return float.PositiveInfinity;
                }
            }
            else
            {
                float T0 = Gd.Float(((Gd.At(Box.Position, Axis) - Gd.At(A, Axis)) / Gd.At(Step, Axis)));
                float T1 = Gd.Float(((Gd.At(Box.End, Axis) - Gd.At(A, Axis)) / Gd.At(Step, Axis)));
                Enter = Gd.Maxf(Enter, Gd.Minf(T0, T1));
                Leave = Gd.Minf(Leave, Gd.Maxf(T0, T1));
                if ((Enter > Leave))
                {
                    return float.PositiveInfinity;
                }
            }
        }
        return Enter;
    }

}
