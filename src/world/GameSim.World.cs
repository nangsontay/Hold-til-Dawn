using Godot;
using Environment = Godot.Environment;
using FileAccess = Godot.FileAccess;
using System;
using System.Threading.Tasks;
using HoldTilDawn;

// Owner: Vu Duc Viet Anh. Island and base geometry, collision, navigation and resource nodes (shared by every peer).
public partial class GameSim : RefCounted
{
    public const float T = 32.0f;  // pixels per tile; const so every partial file can use it in its static fields
    public static readonly dynamic HALF = (Gd.MakeVector2(5.5f, 3.5f) * T);
    public static readonly dynamic SAFE_D = (20.0f * T);
    // how far the island reaches from the outpost; its real outline (and the sea) is src/world/IslandMap.cs
    public static readonly dynamic ISLAND = (126.0f * T);
    public static readonly dynamic DOOR_DIR = new GArray(Gd.MakeVector2((-1), 0), Gd.MakeVector2(0, (-1)), Gd.MakeVector2(1, 0));
    public static readonly dynamic DOOR_POS = new GArray(Gd.MakeVector2((-HALF.X), 0), Gd.MakeVector2(0, (-HALF.Y)), Gd.MakeVector2(HALF.X, 0));
    public static readonly dynamic DOOR_NAMES = new GArray("A", "B", "C");
    public static readonly dynamic PAD_DIST = (5.5f * T);
    public static readonly dynamic PAD_R = (4.0f * T);
    public static int NODE_MUL = WorldSettings.Current.NodeMultiplier;
    // The base furniture, both against the north wall and out of the door lanes: the workbench (crafting, MEC-10)
    // west of door B, the shared storage chest (BAL-18) east of it. The ground point (front edge, middle).
    public static readonly Vector2 WORKBENCH_POS = (new Vector2(-3.2f, -2.0f) * T);
    public static readonly Vector2 STORAGE_POS = (new Vector2(3.3f, -2.0f) * T);
    // how close you have to stand to use them
    public const float BENCH_REACH = (2.2f * T);
    public const float STORAGE_REACH = (1.8f * T);

    public static bool NearBench(Vector2 Pos)
    {
        return (Pos.DistanceTo(WORKBENCH_POS) <= BENCH_REACH);
    }

    public static bool NearStorage(Vector2 Pos)
    {
        return (Pos.DistanceTo(STORAGE_POS) <= STORAGE_REACH);
    }

    // Colliders of the base furniture as [centre, radius]: a row of circles along the bench, two under the chest.
    // They reach back to the wall so nobody slips behind.
    public static GArray FurnitureSolids()
    {
        var Out = new GArray();
        foreach (float X in new[] { -19.0f, -6.0f, 6.0f, 19.0f })
        {
            Out.Add(new GArray((WORKBENCH_POS + new Vector2(X, -10.0f)), 12.0f));
        }
        foreach (float X in new[] { -7.0f, 7.0f })
        {
            Out.Add(new GArray((STORAGE_POS + new Vector2(X, -7.0f)), 10.0f));
        }
        return Out;
    }

    // --- helpers ------------------------------------------------------------
    public static dynamic WallDist(dynamic Pos)
    {
        // Distance to the nearest base wall (0 inside); every zone distance in the GDD uses this.
        return Gd.Length(Gd.MakeVector2(Gd.Maxf((Gd.Absf(Pos.X) - HALF.X), 0.0f), Gd.Maxf((Gd.Absf(Pos.Y) - HALF.Y), 0.0f)));
    }

    public static dynamic InDoorLane(dynamic Pos, dynamic HalfWidth = null)
    {
        HalfWidth ??= (1.5f * T);
        // The 3-tile corridor in front of each door that stays clear (section 4.3).
        foreach (dynamic I in Gd.Iter(3))
        {
            Vector2 Rel = (Vector2)((Pos - Gd.At(DOOR_POS, I)));
            Vector2 Dd = (Vector2)(Gd.At(DOOR_DIR, I));
            if (((Rel.Dot(Dd) > 0) && (Gd.Absf(Rel.Dot(Gd.MakeVector2((-Dd.Y), Dd.X))) < HalfWidth)))
            {
                return true;
            }
        }
        return false;
    }

    public dynamic RandZonePoint(dynamic Lo, dynamic Hi)
    {
        // Random point lo..hi from the nearest base wall, on the island, off the door lanes and pads.
        foreach (dynamic I in Gd.Iter(400))
        {
            dynamic P = Gd.MakeVector2(Rng.RandfRange((-ISLAND), ISLAND), Rng.RandfRange((-ISLAND), ISLAND));
            dynamic D = GameSim.WallDist(P);
            if (((D >= Lo) && (D <= Hi) && IslandMap.OnGrass((Vector2)P, (2 * T)) && !(Gd.Truth(GameSim.InDoorLane(P))) && !(Gd.Truth(this.NearPad(P)))))
            {
                return P;
            }
        }
        return Gd.MakeVector2(0, (HALF.Y + Lo));
    }

    public static dynamic InsideBase(dynamic Pos, dynamic Margin = null)
    {
        Margin ??= 0.0f;
        return ((Gd.Absf(Pos.X) < (HALF.X + Margin)) && (Gd.Absf(Pos.Y) < (HALF.Y + Margin)));
    }

    // The wall pieces for each of the 8 ways the three doors can be open or shut, built once: bodies are pushed
    // out of them several times a tick. Callers only read the arrays.
    static readonly GArray[] WallSets = new GArray[8];
    static readonly Rect2[][] WallRectSets = new Rect2[8][];

    // bit I set: door I is open
    static int OpenMask(dynamic Open)
    {
        int M = 0;
        for (int I = 0; I < 3; I++)
            if (Gd.Truth(Gd.At(Open, I))) M |= 1 << I;
        return M;
    }

    // doors open to bodies moving now: all by day, the broken ones at night
    int DoorMask()
    {
        bool Day = (Phase != "night");
        int M = 0;
        for (int I = 0; I < 3; I++)
            if (Day || Gd.Float(Gd.At(Doors, I)) <= 0) M |= 1 << I;
        return M;
    }

    public static Rect2[] WallRects(int Mask)
    {
        if (WallRectSets[Mask] == null)
        {
            GArray Set = (GArray)Walls(new GArray((Mask & 1) != 0, (Mask & 2) != 0, (Mask & 4) != 0));
            var Rects = new Rect2[Set.Count];
            for (int I = 0; I < Rects.Length; I++) Rects[I] = (Rect2)Gd.At(Set, I);
            WallRectSets[Mask] = Rects;
        }
        return WallRectSets[Mask];
    }

    public static dynamic Walls(dynamic Open)
    {
        int Mask = OpenMask(Open);
        return WallSets[Mask] ??= (GArray)BuildWalls(Open);
    }

    static dynamic BuildWalls(dynamic Open)
    {
        // Solid wall pieces; an open door (day, or broken) leaves a gap.
        dynamic H = HALF;
        dynamic Th = (WALL_IN + WALL_OUT);
        dynamic Top = Gd.MakeRect2(((-H.X) - WALL_OUT), ((-H.Y) - WALL_OUT), ((2 * H.X) + Th), Th);
        dynamic Bottom = Gd.MakeRect2(((-H.X) - WALL_OUT), (H.Y - WALL_IN), ((2 * H.X) + Th), Th);
        dynamic Left = Gd.MakeRect2(((-H.X) - WALL_OUT), ((-H.Y) - WALL_OUT), Th, ((2 * H.Y) + Th));
        dynamic Right = Gd.MakeRect2((H.X - WALL_IN), ((-H.Y) - WALL_OUT), Th, ((2 * H.Y) + Th));
        return (((new GArray(Bottom) + GameSim.Split(Top, Gd.At(Open, 1), false)) + GameSim.Split(Left, Gd.At(Open, 0), true)) + GameSim.Split(Right, Gd.At(Open, 2), true));
    }

    public static dynamic Split(dynamic R, dynamic Open, dynamic Vertical)
    {
        if (!(Gd.Truth(Open)))
        {
            return new GArray(R);
        }
        if (Gd.Truth(Vertical))
        {
            return new GArray(Gd.MakeRect2(R.Position.X, R.Position.Y, R.Size.X, ((-DOOR_GAP) - R.Position.Y)), Gd.MakeRect2(R.Position.X, DOOR_GAP, R.Size.X, (R.End.Y - DOOR_GAP)));
        }
        return new GArray(Gd.MakeRect2(R.Position.X, R.Position.Y, ((-DOOR_GAP) - R.Position.X), R.Size.Y), Gd.MakeRect2(DOOR_GAP, R.Position.Y, (R.End.X - DOOR_GAP), R.Size.Y));
    }

    public static dynamic Collide(dynamic Pos, dynamic Radius, dynamic Open)
    {
        if (Pos is Vector2 At)
        {
            return CollideFast(At, Gd.Float(Radius), OpenMask(Open));
        }
        // Pushes a circle out of every wall piece (players only enter through doors).
        foreach (dynamic W in Gd.Iter(GameSim.Walls(Open)))
        {
            dynamic Q = Pos.Clamp(W.Position, W.End);
            dynamic D = (Pos - Q);
            dynamic L = Gd.Length(D);
            if ((L >= Radius))
            {
                continue;
            }
            if ((L > 0.001f))
            {
                Pos = (Q + ((D / L) * Radius));
            }
            else
            {
                dynamic C = W.GetCenter();
                dynamic E = ((W.Size / 2) + Gd.MakeVector2(Radius, Radius));
                dynamic O = (Pos - C);
                if (((E.X - Gd.Absf(O.X)) < (E.Y - Gd.Absf(O.Y))))
                {
                    Pos.X = (C.X + (((O.X >= 0) ? (dynamic)(1.0f) : (-1.0f)) * E.X));
                }
                else
                {
                    Pos.Y = (C.Y + (((O.Y >= 0) ? (dynamic)(1.0f) : (-1.0f)) * E.Y));
                }
            }
        }
        return Pos;
    }

    // Collide(), typed
    public static Vector2 CollideFast(Vector2 Pos, float Radius, int Mask)
    {
        foreach (Rect2 W in WallRects(Mask))
        {
            Vector2 Q = Pos.Clamp(W.Position, W.End);
            Vector2 D = Pos - Q;
            float L = D.Length();
            if (L >= Radius)
            {
                continue;
            }
            if (L > 0.001f)
            {
                Pos = Q + (D / L) * Radius;
            }
            else
            {
                Vector2 C = W.GetCenter();
                Vector2 E = (W.Size / 2) + new Vector2(Radius, Radius);
                Vector2 O = Pos - C;
                if ((E.X - Mathf.Abs(O.X)) < (E.Y - Mathf.Abs(O.Y)))
                {
                    Pos.X = C.X + ((O.X >= 0) ? 1.0f : -1.0f) * E.X;
                }
                else
                {
                    Pos.Y = C.Y + ((O.Y >= 0) ? 1.0f : -1.0f) * E.Y;
                }
            }
        }
        return Pos;
    }

    public static dynamic Route(dynamic From, dynamic To, dynamic R)
    {
        // Next waypoint around the base: the goal if the way is clear, else the best visible corner.
        // ponytail: corner hopping around one rectangle; NavigationAgent2D once the map has obstacles.
        if (!(Gd.Truth(GameSim.Blocked(From, To, R))))
        {
            return To;
        }
        dynamic C = (HALF + Gd.MakeVector2((R + T), (R + T)));
        dynamic Corners = new GArray(Gd.MakeVector2(C.X, C.Y), Gd.MakeVector2((-C.X), C.Y), Gd.MakeVector2(C.X, (-C.Y)), Gd.MakeVector2((-C.X), (-C.Y)));
        dynamic Best = To;
        dynamic BestD = float.PositiveInfinity;
        foreach (dynamic Corner in Gd.Iter(Corners))
        {
            if (((From.DistanceTo(Corner) < 4.0f) || Gd.Truth(GameSim.Blocked(From, Corner, R))))
            {
                continue;
            }
            dynamic D = (From.DistanceTo(Corner) + GameSim.Rest(Corner, To, R, Corners));
            if ((D < BestD))
            {
                BestD = D;
                Best = Corner;
            }
        }
        return Best;
    }

    public static dynamic Rest(dynamic Corner, dynamic To, dynamic R, dynamic Corners)
    {
        // Distance left from a corner to the goal, with at most one more corner (enough for a rectangle).
        if (!(Gd.Truth(GameSim.Blocked(Corner, To, R))))
        {
            return Corner.DistanceTo(To);
        }
        dynamic D = float.PositiveInfinity;
        foreach (dynamic C2 in Gd.Iter(Corners))
        {
            if (((C2 != Corner) && !(Gd.Truth(GameSim.Blocked(Corner, C2, R))) && !(Gd.Truth(GameSim.Blocked(C2, To, R)))))
            {
                D = Gd.Minf(D, (Corner.DistanceTo(C2) + C2.DistanceTo(To)));
            }
        }
        return D;
    }

    public static dynamic Blocked(dynamic A, dynamic B, dynamic R)
    {
        dynamic H = (HALF + Gd.MakeVector2(R, R));
        dynamic Pts = new GArray(Gd.MakeVector2((-H.X), (-H.Y)), Gd.MakeVector2(H.X, (-H.Y)), Gd.MakeVector2(H.X, H.Y), Gd.MakeVector2((-H.X), H.Y));
        foreach (dynamic I in Gd.Iter(4))
        {
            if ((Gd.Static(typeof(Geometry2D), "segment_intersects_segment", A, B, Gd.At(Pts, I), Gd.At(Pts, ((I + 1) % 4))) != null))
            {
                return true;
            }
        }
        return false;
    }

    public static dynamic PushOut(dynamic Pos, dynamic Margin)
    {
        // Moves a point inside the (grown) base rectangle to its nearest edge.
        if (!(Gd.Truth(GameSim.InsideBase(Pos, Margin))))
        {
            return Pos;
        }
        dynamic Dx = ((HALF.X + Margin) - Gd.Absf(Pos.X));
        dynamic Dy = ((HALF.Y + Margin) - Gd.Absf(Pos.Y));
        if ((Dx < Dy))
        {
            Pos.X = ((HALF.X + Margin) * ((Pos.X >= 0) ? (dynamic)(1.0f) : (-1.0f)));
        }
        else
        {
            Pos.Y = ((HALF.Y + Margin) * ((Pos.Y >= 0) ? (dynamic)(1.0f) : (-1.0f)));
        }
        return Pos;
    }

    public static dynamic PadPos(dynamic I)
    {
        return (Gd.At(DOOR_POS, I) + (Gd.At(DOOR_DIR, I) * PAD_DIST));
    }

    // Primary wood, stone and herbs share the actual compact home-farm sites and their soil footprints.
    // Titanium remains spread over five salvage landmarks, with additional fixed veins in Southwest Cave.
    static readonly int[] TITANIUM_SITES = { 5, 6, 7, 13, 14 };

    public void GenNodes()
    {
        Gd.Clear(Nodes);
        var Geometry = (WorldGeometry)World;
        int PrimaryCount = Gd.Int((4 + 3 * N0) * NODE_MUL);
        Geometry.EnsureHomeFarmResources(PrimaryCount);
        var Ambient = new GArray();
        var HomeCount = new int[3];
        foreach (var Site in Geometry.HarvestSites)
        {
            var Node = Site.CreateNode();
            if (Site.Type < 3 && WorldGeometry.InHomeFarm(Site.Pos, Site.Type) && HomeCount[Site.Type] < PrimaryCount)
            {
                Node["ambient"] = false; HomeCount[Site.Type]++; ((GArray)Nodes).Add(Node);
            }
            else Ambient.Add(Node);
        }
        var Occupied = new GArray(); Gd.AppendArray(Occupied, Nodes); Gd.AppendArray(Occupied, Ambient);
        World.Rebuild(Occupied, Towers);
        for (int I = 0; I < PrimaryCount; I++)
        {
            int Site = TITANIUM_SITES[I % TITANIUM_SITES.Length];
            Vector2 Centre = Geometry.LmAt.GetValueOrDefault(Site, IslandMap.ById(Site).Pos);
            float Spread = 6 * T;
            dynamic Spot = null;
            for (int Attempt = 0; Attempt < 300; Attempt++)
            {
                Vector2 P = (Centre + ((Vector2.FromAngle((Rng.Randf() * Mathf.Tau)) * Spread) * Mathf.Sqrt(Rng.Randf())));
                if (Geometry.InExpeditionAisle(P)) continue;
                if (Geometry.InCompoundPassage(P)) continue;
                if (Geometry.InLookoutPassage(P)) continue;
                if (IslandMap.InBridgeApproach(P, T)) continue;
                if (Site is 5 or 7 && WorldGeometry.SoilCoverage(Array.Find(WorldGeometry.LandmarkSoils, S => S.Id == Site), P - Centre) < .95f) continue;
                // The cave supplies fixed ore veins and exterior scree, with clear walkways between them.
                if (((WorldGeometry)World).InCaveRegion(P, T) || ((WorldGeometry)World).GardenBounds.Grow(T).HasPoint(P)
                    || ((WorldGeometry)World).WeatherRoom.Grow(T).HasPoint(P)) continue;
                float Wd = Gd.Float(GameSim.WallDist(P));
                if (!IslandMap.OnGrass(P, (1.5f * T)) || (Wd < (3 * T)) || Gd.Truth(GameSim.InDoorLane(P)) || Gd.Truth(this.NearPad(P)))
                {
                    continue;
                }
                if (Wd < (float)SAFE_D + 3 * T)
                {
                    continue;
                }
                if ((World.TerrainAt(P, (0.5f * T)) is 1 or 5) || (((Vector2)World.Resolve(P, T)).DistanceTo(P) > 0.01f) || Gd.Truth(Gd.Any(Nodes, (Func<dynamic, dynamic>)((N) => (N.Pos.DistanceTo(P) <= (2.5f * T))))))
                {
                    continue;
                }
                Spot = P;
                break;
            }
            // a crowded site: anywhere in the right zone
            Spot ??= this.RandZonePoint(SAFE_D + 3 * T, ISLAND);
            Gd.Append(Nodes, new GDict { { "type", 3 }, { "pos", Spot }, { "amount", 5 } });
        }
        // The visible forest, rocks and herbs use the same gathering rules as the original resource clusters.
        Gd.AppendArray(Nodes, Ambient);
        World.Rebuild(Nodes, Towers);
    }

    public dynamic ObstacleHit(dynamic A, dynamic B, dynamic Radius = null, dynamic Ignore = null)
    {
        Radius ??= 0.0f;
        Ignore ??= Vector2.Inf;
        float Hit = Gd.Float(World.FirstHit(A, B, Radius, true, Ignore));
        foreach (dynamic W in Gd.Iter(this.MovementWalls()))
        {
            Hit = Gd.Minf(Hit, WorldGeometry.SegmentBox(A, B, W.Grow(Radius)));
        }
        return Hit;
    }

    public dynamic LineClear(dynamic A, dynamic B, dynamic Ignore = null)
    {
        Ignore ??= Vector2.Inf;
        return (this.ObstacleHit(A, B, 0.0f, Ignore) == float.PositiveInfinity);
    }

    public dynamic MoveActor(dynamic From, dynamic To, dynamic Radius)
    {
        // typed all the way (src/world/WorldGeometry.FastMove.cs): every body moves through here every tick
        int Mask = this.DoorMask();
        Vector2 F = (Vector2)From;
        float R = Gd.Float(Radius);
        // the sea stops everyone: slide along the coast (src/world/IslandMap.cs)
        Vector2 Moved = IslandMap.Keep(F, ((WorldGeometry)World).MoveBodyFast(F, IslandMap.Keep(F, (Vector2)To, R), R, GameSim.WallRects(Mask)), R);
        return GameSim.CollideFast(Moved, R, Mask);
    }

    static readonly Rect2[] OutsideNavigationWalls = { new Rect2(-(Vector2)HALF, (Vector2)HALF * 2) };

    public dynamic Navigate(dynamic From, dynamic To, dynamic Radius, dynamic Outside = null)
    {
        Outside ??= true;
        Rect2[] Barriers = Gd.Truth(Outside) ? OutsideNavigationWalls : WallRects(DoorMask());
        return ((WorldGeometry)World).WaypointFast((Vector2)From, (Vector2)To, Gd.Float(Radius), Barriers);
    }

    public dynamic MovementWalls()
    {
        int Mask = this.DoorMask();
        return WallSets[Mask] ?? GameSim.Walls(new GArray((Mask & 1) != 0, (Mask & 2) != 0, (Mask & 4) != 0));
    }
}
