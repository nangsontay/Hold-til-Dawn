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
    // how far the island reaches from the outpost, the sea beyond it is not walkable (src/world/IslandMap.cs)
    public static readonly dynamic ISLAND = (126.0f * T);
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
    // a built wall is three solid circles one tile apart (GameSim.Walls.cs)
    public const float WALL_PIECE = 0.55f;
    public WorldGeometry()
    {
        this.Generate();
    }

    public static dynamic WallDist(dynamic P)
    {
        return Gd.Length(Gd.MakeVector2(Gd.Maxf((Gd.Absf(P.X) - HALF.X), 0), Gd.Maxf((Gd.Absf(P.Y) - HALF.Y), 0)));
    }

    static bool NearPad(Vector2 P)
    {
        foreach (dynamic I in Gd.Iter(3))
        {
            if (P.DistanceTo((Vector2)GameSim.PadPos(I)) < ((float)GameSim.PAD_R + T))
            {
                return true;
            }
        }
        return false;
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
        // paths, ponds, the swamp and the cave spires first, so the scenery grows around them (WorldTerrain.cs)
        this.BuildTerrain();
        // the landmarks' structures, so the scenery keeps clear of them (WorldLandmarks.cs)
        this.BuildLandmarks();
        // sprite names without the variant number (art/nature/<name>_<k>.png, tools/PixelNature.cs)
        dynamic Pines = new GArray("pine");
        dynamic Dead = new GArray("dead");
        dynamic Rocks = new GArray("rock");
        dynamic Variants = Gd.FromVariant(Json.ParseString(Gd.Static(typeof(FileAccess), "get_file_as_string", "res://art/nature/index.json")));
        // and the landmarks' sprites (art/landmarks, tools/PixelLandmarks.cs)
        dynamic LmVariants = Gd.FromVariant(Json.ParseString(FileAccess.GetFileAsString("res://art/landmarks/index.json")));
        foreach (dynamic N in Gd.Iter(LmVariants))
        {
            Variants[N] = Gd.At(LmVariants, N);
        }
        dynamic Names = new GDict {  };
        foreach (dynamic N in Gd.Iter(Variants))
        {
            dynamic Base = Gd.Rsplit(N, "_", true, 1);
            Names[Gd.At(Base, 0)] = Gd.Maxi(Gd.Get(Names, Gd.At(Base, 0), 0), (Gd.ToInt(Gd.At(Base, 1)) + 1));
        }
        dynamic Variant = (Func<dynamic, dynamic, dynamic>)((N, K) => Gd.Format("%s_%d", new GArray(N, (Gd.Absi(K) % Gd.Maxi(1, Gd.Get(Names, N, 1))))));
        // scenery stays on the grass, off the door roads and paths, out of the outpost's surroundings
        Func<Vector2, bool> Free = (P) => (IslandMap.OnGrass(P, (0.6f * T)) && (WorldGeometry.WallDist(P) > (4 * T)) && !(Gd.Truth(WorldGeometry.Lane(P))) && !this.TerrainBlocked(P, (0.6f * T)) && !NearPad(P));
        Func<Vector2, bool> Outer = (P) => (Free(P) && (WorldGeometry.WallDist(P) > (SAFE_D + (2 * T))));
        Rect2 Box = IslandMap.Box;
        Func<Vector2> Anywhere = () => new Vector2(Mathf.Lerp(Box.Position.X, Box.End.X, Random.Randf()), Mathf.Lerp(Box.Position.Y, Box.End.Y, Random.Randf()));
        // a tree for every tree on the design map, most with one to three more beside it (the island is larger than the map)
        foreach (Vector2 Dot in IslandMap.Trees)
        {
            int Count = 1 + ((Random.Randf() < 0.8f) ? 1 : 0) + ((Random.Randf() < 0.5f) ? 1 : 0) + ((Random.Randf() < 0.25f) ? 1 : 0);
            foreach (dynamic K in Gd.Iter(Count))
            {
                Vector2 P = ((K == 0) ? Dot : (Dot + (Vector2.FromAngle((Random.Randf() * Mathf.Tau)) * Random.RandfRange((0.8f * T), (2.6f * T)))));
                if (Free(P))
                {
                    dynamic Pool = ((Random.Randf() < 0.15f) ? (dynamic)(Dead) : Pines);
                    // coconut palms in the Coconut Grove
                    if (this.InGrove(P))
                    {
                        Pool = new GArray("lm_palm");
                    }
                    Gd.Append(Deco, new GArray(P, Variant(Gd.At(Pool, (Random.Randi() % Gd.Size(Pool))), Random.Randi()), 1.0f, COLD));
                }
            }
        }
        // and the woods filled in more thinly
        foreach (Vector2[] Wood in IslandMap.Shade)
        {
            var Wb = new Rect2(Wood[0], Vector2.Zero);
            foreach (Vector2 Q in Wood) Wb = Wb.Expand(Q);
            int Many = (int)((Wb.Area / (T * T)) / 70.0f);
            foreach (dynamic K in Gd.Iter(Many))
            {
                var P = new Vector2(Mathf.Lerp(Wb.Position.X, Wb.End.X, Random.Randf()), Mathf.Lerp(Wb.Position.Y, Wb.End.Y, Random.Randf()));
                if (IslandMap.InPolygon(Wood, P) && Free(P))
                {
                    Gd.Append(Deco, new GArray(P, Variant((this.InGrove(P) ? "lm_palm" : Gd.At(Pines, (Random.Randi() % Gd.Size(Pines)))), Random.Randi()), 1.0f, COLD));
                }
            }
        }
        // rock outcrops and lone dead trees out past the safe zone
        foreach (dynamic C in Gd.Iter(54))
        {
            Vector2 Centre = Anywhere();
            if (!Outer(Centre))
            {
                continue;
            }
            foreach (dynamic K in Gd.Iter(Random.RandiRange(3, 6)))
            {
                Vector2 P = (Centre + (Vector2.FromAngle((Random.Randf() * Mathf.Tau)) * Random.RandfRange(0, (2.2f * T))));
                if (Outer(P))
                {
                    Gd.Append(Deco, new GArray(P, Variant(Gd.At(Rocks, (Random.Randi() % Gd.Size(Rocks))), Random.Randi()), 1.0f, COLD));
                }
            }
        }
        foreach (dynamic I in Gd.Iter(90))
        {
            Vector2 P = Anywhere();
            if (Outer(P))
            {
                Gd.Append(Deco, new GArray(P, Variant(Gd.At(Dead, (Random.Randi() % Gd.Size(Dead))), Random.Randi()), 1.0f, COLD));
            }
        }
        // low shrubs in the safe zone
        foreach (dynamic I in Gd.Iter(1350))
        {
            Vector2 P = Anywhere();
            dynamic Wd = WorldGeometry.WallDist(P);
            if (((Wd > (3 * T)) && (Wd < SAFE_D) && (Random.Randf() < 0.12f) && Free(P)))
            {
                Gd.Append(Deco, new GArray(P, Variant("shrub", Random.Randi()), 1.0f, COLD));
            }
        }
        // grass tufts and pebbles on the grass, pebbles on the beach
        dynamic Flat = new GArray("grass", "grass", "grass", "grass_dry", "grass", "grass_dry", "pebble", "grass", "pebble", "grass");
        foreach (dynamic Tries in Gd.Iter(45000))
        {
            if (Gd.Size(Scatter) >= 3800)
            {
                break;
            }
            Vector2 P = Anywhere();
            if (((Gd.Absf(P.X) <= (HALF.X + (2.6f * T))) && (Gd.Absf(P.Y) <= (HALF.Y + (2.6f * T)))))
            {
                continue;
            }
            int Kind = this.TerrainAt(P, 4.0f);
            if (Kind is 0 or 4 or 9)
            {
                Gd.Append(Scatter, new GArray(P, Variant(Gd.At(Flat, (Random.Randi() % Gd.Size(Flat))), Random.Randi()), 1.0f));
            }
            else if ((Kind == 7) && IslandMap.OnLand(P, T) && (Random.Randf() < 0.25f))
            {
                Gd.Append(Scatter, new GArray(P, Variant("pebble", Random.Randi()), 1.0f));
            }
        }
        // rock spires round the Southwest Cave, and reeds round the pond and the swamp pools
        foreach (Vector2 Cp in this.Crags)
        {
            Gd.Append(Deco, new GArray(Cp, Variant("crag", Random.Randi()), 1.0f, COLD));
        }
        foreach (var Pd in this.Ponds)
        {
            for (int I = 0; I < Pd.Edge.Length; I += 2)
            {
                Vector2 Bank = (Pd.Edge[I] + ((Pd.Edge[I] - Pd.Centre).Normalized() * 6.0f));
                if (((Random.Randf() < 0.6f) && IslandMap.OnGrass(Bank) && !this.NearPath(Bank, 0.3f * T)))
                {
                    Gd.Append(Deco, new GArray(Bank, Variant("reed", Random.Randi()), 1.0f, COLD));
                }
            }
        }
        foreach (var (Lp, Ls) in this.LmProps)
        {
            Gd.Append(Deco, new GArray(Lp, Ls, 1.0f, COLD));
        }
        // Hand-placed objects are editable with Godot's 2D editor.
        dynamic Layout = LAYOUT.Instantiate();
        Gd.Clear(StaticSolids);
        // index.json gives each sprite's solid radius in pixels: a trunk, a boulder's footprint, 0 for shrubs
        foreach (dynamic D in Gd.Iter(Deco))
        {
            float Radius = Gd.Float(Gd.At(Gd.Get(Variants, Gd.At(D, 1), new GArray(0, 0, 0)), 2));
            if ((Radius > 0))
            {
                Gd.Append(StaticSolids, new GArray(Gd.At(D, 0), Radius, true));
            }
        }
        this.AddTerrainSolids(StaticSolids);
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
        // the workbench and the storage chest (GameSim.World.cs)
        foreach (dynamic S in Gd.Iter(GameSim.FurnitureSolids()))
        {
            Gd.Append(StaticSolids, new GArray(Gd.At(S, 0), Gd.At(S, 1), true));
        }
        Gd.SortCustom(Scatter, (Func<dynamic, dynamic, dynamic>)((A, B) => (Gd.At(A, 0).Y < Gd.At(B, 0).Y)));
        this.Rebuild(new GArray(), new GArray());
    }

    public static GArray WallPieces(dynamic Pos, dynamic Vert)
    {
        Vector2 Along = (Gd.Truth(Vert) ? Vector2.Down : Vector2.Right);
        return new GArray((Pos - (Along * T)), Pos, (Pos + (Along * T)));
    }

    public void Rebuild(dynamic Nodes, dynamic Towers, dynamic Walls = null)
    {
        dynamic Next = Gd.Duplicate(StaticSolids);
        foreach (dynamic W in Gd.Iter((Walls ?? new GArray())))
        {
            foreach (dynamic Piece in Gd.Iter(WorldGeometry.WallPieces(W.Pos, W.Vert)))
            {
                Gd.Append(Next, new GArray(Piece, (WALL_PIECE * T), true));
            }
        }
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
            Grid.Region = Gd.MakeRect2I((-128), (-128), 257, 257);
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
            // the sea
            for (int Cy = -128; Cy < 129; Cy++)
            {
                for (int Cx = -128; Cx < 129; Cx++)
                {
                    if (!IslandMap.OnLand(new Vector2(Cx, Cy) * T, (float)Radius))
                    {
                        Grid.SetPointSolid(new Vector2I(Cx, Cy));
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
        // Nothing ahead is in plain sight, e.g. the body is pressed into a tree or squeezed between two solids:
        // head for the next cell anyway and let the movement slide along the solid. Returning From here left a
        // bot (or an enemy) standing still for good.
        foreach (dynamic J in Gd.Iter(Gd.Range(Gd.Mini(1, (Gd.Size(Path) - 1)), (-1), (-1))))
        {
            dynamic P = (Gd.MakeVector2(Gd.At(Path, J)) * T);
            if ((From.DistanceTo(P) >= 2))
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
