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
    public static readonly dynamic ISLAND = (72.0f * T);
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
            if (((D >= Lo) && (D <= Hi) && (Gd.Length(P) < (ISLAND - (6 * T))) && !(Gd.Truth(GameSim.InDoorLane(P))) && !(Gd.Truth(this.NearPad(P)))))
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

    public static dynamic Walls(dynamic Open)
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

    public void GenNodes()
    {
        Gd.Clear(Nodes);
        foreach (dynamic Type in Gd.Iter(4))
        {
            foreach (dynamic I in Gd.Iter(((4 + (3 * N0)) * NODE_MUL)))
            {
                dynamic Lo = ((Type < 3) ? (dynamic)((3 * T)) : (SAFE_D + (3 * T)));
                dynamic Hi = ((Type < 3) ? (dynamic)((SAFE_D - T)) : ISLAND);
                dynamic Spot = this.RandZonePoint(Lo, Hi);
                foreach (dynamic Attempt in Gd.Iter(100))
                {
                    if (((World.Resolve(Spot, T).DistanceTo(Spot) < 0.01f) && Gd.Truth(Gd.All(Nodes, (Func<dynamic, dynamic>)((N) => (N.Pos.DistanceTo(Spot) > (2.5f * T)))))))
                    {
                        break;
                    }
                    Spot = this.RandZonePoint(Lo, Hi);
                }
                Gd.Append(Nodes, new GDict { { "type", Type }, { "pos", Spot }, { "amount", 5 } });
            }
        }
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
        dynamic SolidWalls = this.MovementWalls();
        return GameSim.Collide(World.MoveBody(From, To.LimitLength(ISLAND), Radius, SolidWalls), Radius, Gd.Map(Doors, (Func<dynamic, dynamic>)((D) => ((Phase != "night") || (D <= 0)))));
    }

    public dynamic Navigate(dynamic From, dynamic To, dynamic Radius, dynamic Outside = null)
    {
        Outside ??= true;
        dynamic Barriers = (Gd.Truth(Outside) ? (dynamic)(new GArray(Gd.MakeRect2((-HALF), (HALF * 2)))) : this.MovementWalls());
        return World.Waypoint(From, To, Radius, Barriers);
    }

    public dynamic MovementWalls()
    {
        dynamic Open = Gd.Map(Doors, (Func<dynamic, dynamic>)((D) => ((Phase != "night") || (D <= 0))));
        dynamic Key = Gd.Str(Open);
        if ((Key != WallKey))
        {
            WallKey = Key;
            WallCache = GameSim.Walls(Open);
        }
        return WallCache;
    }
}
