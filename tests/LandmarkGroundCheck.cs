using Godot;
using System;

// Compare the fast, spatially filtered decoration queries against the existing terrain rules.
public partial class LandmarkGroundCheck : Node
{
    public override void _Ready()
    {
        try
        {
            var W = new WorldGeometry();
            CheckExpeditionSites(W);
            CheckCompoundPerimeters(W);
            CheckLookouts(W);
            var Flats = Array.Find(WorldGeometry.LandmarkSoils, S => S.Id == 3);
            if (WorldGeometry.HomeFarmRadii.X * WorldGeometry.HomeFarmRadii.Y >= 6.5f * 6.5f * 32 * 32)
                throw new InvalidOperationException("The home gathering footprint was not made more compact");
            foreach (var (Id, BaselineArea) in new[] { (6, 9f * 6.4f), (13, 12.4f * 7.6f) })
            {
                Rect2 Footprint = W.Compounds[Id];
                float Expected = Id==13 ? 1.5f*WorldGeometry.YARD_AREA_SCALE : 1.5f;
                if (MathF.Abs(Footprint.Area / (32 * 32) / BaselineArea - Expected) > .001f)
                    throw new InvalidOperationException($"Compound {Id} did not gain 50 percent floor area");
                foreach (Vector2 Corner in new[] { Footprint.Position, Footprint.End, new Vector2(Footprint.Position.X, Footprint.End.Y), new Vector2(Footprint.End.X, Footprint.Position.Y) })
                    if (!IslandMap.OnLand(Corner, 10) || IslandMap.InRiver(Corner, IslandMap.RIVER_BANK))
                        throw new InvalidOperationException($"Expanded compound {Id} extends into water");
            }
            foreach (var (Id, Entry) in W.CompoundEntrances)
                if (!IslandMap.OnLand(Entry, 10) || !W.ResolveFast(Entry, 10).IsEqualApprox(Entry))
                    throw new InvalidOperationException($"Compound {Id} entrance is obstructed");
            int Compared = 0, Segments = 0;
            foreach (var (_, Centre) in W.LmAt)
            {
                var Area = new Rect2(Centre - new Vector2(250, 180), new Vector2(500, 360));
                var Mask = new LandmarkGroundMask(W, Area);
                Segments += Mask.SegmentCount;
                foreach (float Margin in new[] { 4.0f, 8.0f })
                    for (float Y = Area.Position.Y; Y < Area.End.Y; Y += 16)
                        for (float X = Area.Position.X; X < Area.End.X; X += 16)
                        {
                            Vector2 P = new Vector2(X, Y);
                            bool Expected = W.TerrainAt(P, Margin) is 0 or 4 or 9;
                            if (Mask.CanPaint(P, Margin) != Expected) throw new InvalidOperationException($"Ground mask differs at {P}, margin {Margin}");
                            Compared++;
                        }
            }
            var Cave = Array.Find(WorldGeometry.LandmarkSoils, S => S.Id == 16);
            if (W.CaveCentre.X >= 0 || W.CaveCentre.Y <= 0 || MathF.Abs(Cave.Rx * Cave.Ry / (7.5f * 5.2f * 1.5f) - 3) > .001f)
                throw new InvalidOperationException("The tripled cave region must remain in the southwest");
            var CaveArea = new Rect2(W.CaveCentre - WorldGeometry.CaveRadii, WorldGeometry.CaveRadii * 2);
            var CaveMask = new LandmarkGroundMask(W, CaveArea);
            int CaveCells = 0;
            for (float Y = CaveArea.Position.Y; Y < CaveArea.End.Y; Y += 4)
                for (float X = CaveArea.Position.X; X < CaveArea.End.X; X += 4)
                {
                    Vector2 P = new(X, Y);
                    if (!W.InCaveRegion(P)) continue;
                    if (!IslandMap.OnLand(P) || IslandMap.InRiver(P, IslandMap.RIVER_BANK + 3)) continue;
                    if (WorldGeometry.SoilCoverage(Cave, P - W.SoilCentre(Cave)) != 1 ||
                        !CaveMask.CanPaint(P, 3, AllowBeach: true, PreserveTrails: false))
                        throw new InvalidOperationException($"Uncovered cave ground at {P}");
                    CaveCells++;
                }
            foreach (var (Face, Cap, _, _, _) in W.CaveWalls)
                foreach (Vector2 P in Face.Concat(Cap))
                    if (!W.InCaveRegion(P)) throw new InvalidOperationException($"Cave wall extends beyond its rock ground at {P / 32}");
            var MiningNodes = new HoldTilDawn.GArray();
            foreach (var Site in W.HarvestSites) MiningNodes.Add(Site.CreateNode());
            W.Rebuild(MiningNodes, new HoldTilDawn.GArray());
            Vector2 WeatherWalker = W.WeatherEntrance + new Vector2(0, 60);
            for (int I = 0; I < 90; I++) WeatherWalker = W.MoveBodyFast(WeatherWalker, WeatherWalker.MoveToward(W.WeatherCentre, 3), 10, Array.Empty<Rect2>());
            if (WeatherWalker.DistanceTo(W.WeatherCentre) > 3 || !W.IsWeatherInterior(WeatherWalker))
                throw new InvalidOperationException("The weather hut cannot be entered through its south doorway");
            for (int I = 0; I < 90; I++) WeatherWalker = W.MoveBodyFast(WeatherWalker, WeatherWalker.MoveToward(W.WeatherEntrance + new Vector2(0, 60), 3), 10, Array.Empty<Rect2>());
            if (W.IsWeatherInterior(WeatherWalker)) throw new InvalidOperationException("The weather hut cannot be exited");
            foreach (Vector2 Side in new[] { Vector2.Left, Vector2.Right, Vector2.Up, new Vector2(.7f, 1) })
            {
                Vector2 P = W.WeatherCentre + WorldGeometry.WeatherHalf * Side + Side * 40;
                for (int I = 0; I < 50; I++) P = W.MoveBodyFast(P, P.MoveToward(W.WeatherCentre, 3), 10, Array.Empty<Rect2>());
                if (W.IsWeatherInterior(P)) throw new InvalidOperationException("A weather hut wall can be walked through");
            }
            if (W.HarvestSites.Exists(S => W.WeatherRoom.HasPoint(S.Pos))) throw new InvalidOperationException("Harvestable resources inside the display-only weather hut");
            if (W.FishingRods.Count != 3 || W.FishingFish.Count < 4 || W.FishingLitter.Count < 8)
                throw new InvalidOperationException("The fishing pier lacks rods, jumping fish or litter");
            foreach (Vector2 Fish in W.FishingFish)
                if (IslandMap.OnLand(Fish, -8)) throw new InvalidOperationException("A jumping fish is on dry land or on the deck");
            GD.Print($"WEATHER / PIER ALL OK: walkable room, solid walls, {W.FishingRods.Count} rods, {W.FishingFish.Count} fish and {W.FishingLitter.Count} litter props");
            if (W.GardenCentre.Y > 11 * 32 || W.GardenCentre.Y < 9 * 32 || W.GardenCentre.X > -64 * 32 || W.GardenCentre.X < -68 * 32)
                throw new InvalidOperationException("The garden did not move north to the marked west-bank clearing");
            Vector2 GardenWalker = W.GardenEntrance + new Vector2(0, 45);
            Vector2 GardenGoal = W.GardenCentre - new Vector2(0, 4 * 32);
            for (int I = 0; I < 130; I++) GardenWalker = W.MoveBodyFast(GardenWalker, GardenWalker.MoveToward(GardenGoal, 3), 10, Array.Empty<Rect2>());
            if (GardenWalker.DistanceTo(GardenGoal) > 3) throw new InvalidOperationException("Garden gate or central aisle is blocked");
            foreach (var (Outside, Inside) in new[] {
                (W.GardenCentre + new Vector2(-WorldGeometry.GardenHalf.X - 40, 0), W.GardenCentre),
                (W.GardenCentre + new Vector2(WorldGeometry.GardenHalf.X + 40, 0), W.GardenCentre),
                (W.GardenCentre + new Vector2(0, -WorldGeometry.GardenHalf.Y - 40), W.GardenCentre),
                (W.GardenEntrance + new Vector2(100, 40), W.GardenCentre + new Vector2(100, 0)) })
            {
                Vector2 P = Outside;
                for (int I = 0; I < 40; I++) P = W.MoveBodyFast(P, P.MoveToward(Inside, 3), 10, Array.Empty<Rect2>());
                if (W.GardenBounds.Grow(-12).HasPoint(P)) throw new InvalidOperationException("A garden fence can be walked through");
            }
            foreach (var Mask in W.CaveExteriorMasks)
                if (Geometry2D.TriangulatePolygon(Mask).Length == 0) throw new InvalidOperationException("Cave exterior mask is not drawable");
            Vector2 Walker = W.CaveEntrance + new Vector2(2 * 32, 0);
            Vector2 Goal = W.CaveCentre - new Vector2(8.5f * 32, 0);
            for (int I = 0; I < 400 && Walker.DistanceTo(Goal) > 2; I++)
                Walker = W.MoveBodyFast(Walker, Walker.MoveToward(Goal, 3), 10, Array.Empty<Rect2>());
            if (Walker.DistanceTo(Goal) > 3) throw new InvalidOperationException($"The east-to-west cave entrance or mining aisle is blocked at {Walker / 32}, goal {Goal / 32}");
            GD.Print($"CAVE GROUND ALL OK: {CaveCells} opaque rock cells; east-to-west access is clear");
            if (!Cave.Bare || WorldGeometry.SoilCoverage(Cave, Vector2.Zero) != 1 ||
                WorldGeometry.SoilCoverage(Cave, new Vector2(0, 2 * 32)) != 1)
                throw new InvalidOperationException("Cave mouth and forecourt must have opaque rock ground");
            foreach (var Soil in WorldGeometry.LandmarkSoils)
            {
                Vector2 Centre = W.SoilCentre(Soil);
                var Area = new Rect2(Centre - new Vector2(Soil.Rx, Soil.Ry) * 32, new Vector2(Soil.Rx, Soil.Ry) * 64);
                var Mask = new LandmarkGroundMask(W, Area);
                for (float Y = Area.Position.Y; Y < Area.End.Y; Y += 16)
                    for (float X = Area.Position.X; X < Area.End.X; X += 16)
                    {
                        Vector2 P = new(X, Y);
                        if (Mask.CanPaint(P, 3, AllowBeach: true, PreserveTrails: false) && (W.TerrainAt(P, 3) is 1 or 6 or 8 || IslandMap.InRiver(P, IslandMap.RIVER_BANK + 3)))
                            throw new InvalidOperationException($"Landmark ground overwrites water, a bridge or floor at {P}");
                    }
            }
            foreach (HoldTilDawn.GArray Row in W.Scatter)
                if (W.BareLandmarkGround((Vector2)Row[0]))
                    throw new InvalidOperationException("Grass tufts on bare landmark ground");
            GD.Print($"LANDMARK GROUND ALL OK: {Compared} queries match existing terrain; average {Segments / W.LmAt.Count} nearby path segments per location");
            GetTree().Quit();
        }
        catch (Exception Error) { GD.PushError(Error.ToString()); GetTree().Quit(1); }
    }

    static void CheckExpeditionSites(WorldGeometry W)
    {
        foreach (int Id in new[] {5,7,11})
        {
            var Site = W.ExpeditionSites[Id];
            var Soil = Array.Find(WorldGeometry.LandmarkSoils,S=>S.Id==Id);
            if (!W.SoilCentre(Soil).IsEqualApprox(Site.Centre) || !W.LmAt[Id].IsEqualApprox(Site.Centre))
                throw new InvalidOperationException($"Site {Id} ground / scenery / marker centres differ");
            for(float Y=-Site.Half.Y;Y<=Site.Half.Y;Y+=16)
                for(float X=-Site.Half.X;X<=Site.Half.X;X+=16)
                {
                    Vector2 P=Site.Centre+new Vector2(X,Y);
                    if(!IslandMap.OnLand(P,10) || IslandMap.InRiver(P,IslandMap.RIVER_BANK+8))
                        throw new InvalidOperationException($"Site {Id} footprint extends into water at {P/32}");
                    if(WorldGeometry.SoilCoverage(Soil,P-Site.Centre)<.99f)
                        throw new InvalidOperationException($"Site {Id} ground does not cover its complete footprint at {P/32}");
                }
            if(Id==11) continue;
            Vector2 Sum=Vector2.Zero; int Count=0;
            foreach(var V in W.ExpeditionVehicles)
                if(V.Id==Id) { Sum+=V.Pos; Count++; }
            if(Count!=(Id==5?1:3) || !(Sum/Count).IsEqualApprox(Site.Centre))
                throw new InvalidOperationException($"Vehicle group {Id} is not centred on its ground");
        }
        foreach(var F in W.ExpeditionCliffs)
            if(Geometry2D.TriangulatePolygon(F.Face).Length==0 || Geometry2D.TriangulatePolygon(F.Cap).Length==0)
                throw new InvalidOperationException("An exposed Northwind rock ledge is not drawable");
        var Nodes=new HoldTilDawn.GArray();
        foreach(var S in W.HarvestSites) Nodes.Add(S.CreateNode());
        W.Rebuild(Nodes,new HoldTilDawn.GArray());
        foreach(int Id in new[]{5,7,11})
        {
            var Site=W.ExpeditionSites[Id];
            Vector2 From=Id==11 ? Site.Centre+new Vector2(0,Site.Half.Y+8) : Site.Centre+new Vector2(-Site.Half.X-5,Id==5?60:4);
            Vector2 Goal=Id==11 ? Site.Centre-new Vector2(0,Site.Half.Y+8) : Site.Centre+new Vector2(Site.Half.X+5,Id==5?60:4);
            Vector2 Walker=From;
            for(int I=0;I<500 && Walker.DistanceTo(Goal)>2;I++)
                Walker=W.MoveBodyFast(Walker,Walker.MoveToward(Goal,3),10,Array.Empty<Rect2>());
            if(Walker.DistanceTo(Goal)>3) throw new InvalidOperationException($"Site {Id} walking passage blocked at {Walker/32}");
        }
        GD.Print("EXPEDITION SITES ALL OK: shared centres, complete dry ground, centred vehicles, walkable passages and drawable rock terraces");
    }

    static void CheckCompoundPerimeters(WorldGeometry W)
    {
        foreach (int Id in new[]{6,13})
        {
            var Sections=W.CompoundBoundaries.Where(S=>S.Id==Id).ToArray();
            if(Sections.Select(S=>S.Side).Distinct().Count()!=4) throw new Exception($"Compound {Id} does not have all four perimeter sides");
            foreach(var S in Sections)
            {
                if(Id==6 && (Geometry2D.TriangulatePolygon(S.Face).Length==0 || Geometry2D.TriangulatePolygon(S.Cap).Length==0))
                    throw new Exception("A damaged ruin wall is not drawable");
                Vector2 Mid=(S.A+S.B)/2;
                Vector2 Out=S.Side switch {0=>Vector2.Up,1=>Vector2.Left,2=>Vector2.Right,_=>Vector2.Down};
                foreach(int Direction in new[]{-1,1})
                {
                    Vector2 P=Mid+Out*Direction*42,Goal=Mid-Out*Direction*42;
                    for(int I=0;I<60;I++) P=W.MoveBodyFast(P,P.MoveToward(Goal,3),10,Array.Empty<Rect2>());
                    if((P-Mid).Dot(Out)*Direction<14) throw new Exception($"Compound {Id} side {S.Side} can be crossed through its wall/fence");
                }
            }
            foreach(float Radius in new[]{10f,35f})
            {
                Vector2 Gate=W.CompoundGates[Id],Goal=W.LmAt[Id]+new Vector2(0,-35),P=Gate+new Vector2(0,48);
                for(int I=0;I<200 && P.DistanceTo(Goal)>2;I++) P=W.MoveBodyFast(P,P.MoveToward(Goal,3),Radius,Array.Empty<Rect2>());
                if(P.DistanceTo(Goal)>3) throw new Exception($"Compound {Id} gate / central aisle blocked for radius {Radius} at {P/32}");
                Goal=Gate+new Vector2(0,48);
                for(int I=0;I<200 && P.DistanceTo(Goal)>2;I++) P=W.MoveBodyFast(P,P.MoveToward(Goal,3),Radius,Array.Empty<Rect2>());
                if(P.DistanceTo(Goal)>3) throw new Exception($"Compound {Id} cannot be exited");
            }
            if(!W.Compounds[Id].GetCenter().IsEqualApprox(W.LmAt[Id])) throw new Exception("Compound floor and scenery centres differ");
        }
        GD.Print("COMPOUND PERIMETERS ALL OK: four solid sides each, open gates, player/boss entry and exit, drawable walls and shared centres");
    }

    static void CheckLookouts(WorldGeometry W)
    {
        foreach(int Id in new[]{12,17})
        {
            var Site=W.Lookouts[Id];
            var Soil=Array.Find(WorldGeometry.LandmarkSoils,S=>S.Id==Id);
            if(!W.LmAt[Id].IsEqualApprox(Site.Centre) || !W.SoilCentre(Soil).IsEqualApprox(Site.Deck.GetCenter()))
                throw new Exception($"Lookout {Id} ground, platform and marker have different centres");
            if(Geometry2D.TriangulatePolygon(Site.Outline).Length==0) throw new Exception("Lookout platform is not drawable");
            for(float Y=-Site.Radii.Y;Y<=Site.Radii.Y;Y+=8)
                for(float X=-Site.Radii.X;X<=Site.Radii.X;X+=8)
                {
                    Vector2 D=new(X,Y),P=Site.Centre+D;
                    if((D/Site.Radii).Length()>1) continue;
                    if(!IslandMap.OnLand(P,12) || WorldGeometry.SoilCoverage(Soil,D)<.999f)
                        throw new Exception($"Lookout {Id} has uncovered or wet ground at {P/32}");
                }
            foreach(var Placement in W.LookoutPlacements.Where(P=>P.Id==Id))
                foreach(Vector2 Side in new[]{Vector2.Zero,Vector2.Left,Vector2.Right,Vector2.Up,Vector2.Down})
                {
                    Vector2 P=Placement.Pos+Side*Placement.Radius;
                    if(!IslandMap.OnLand(P,10) || WorldGeometry.SoilCoverage(Soil,P-Site.Centre)<.999f)
                        throw new Exception($"Lookout {Id} fixture extends off its ground at {P/32}");
                }
            foreach(float Radius in new[]{10f,35f})
            {
                Vector2 P=W.CompoundEntrances[Id],Goal=Site.Centre;
                for(int I=0;I<180 && P.DistanceTo(Goal)>2;I++) P=W.MoveBodyFast(P,P.MoveToward(Goal,3),Radius,Array.Empty<Rect2>());
                if(P.DistanceTo(Goal)>3) throw new Exception($"Lookout {Id} entrance is blocked at {P/32}");
                Goal=W.CompoundEntrances[Id];
                for(int I=0;I<180 && P.DistanceTo(Goal)>2;I++) P=W.MoveBodyFast(P,P.MoveToward(Goal,3),Radius,Array.Empty<Rect2>());
                if(P.DistanceTo(Goal)>3) throw new Exception($"Lookout {Id} exit is blocked");
            }
        }
        if(W.Coral.Count<18 || W.Coral.Count!=W.ReefBeds.Count) throw new Exception("Coral Lookout lacks a visible reef");
        foreach(var Bed in W.ReefBeds)
        {
            if(Geometry2D.TriangulatePolygon(Bed).Length==0 || Bed.Any(P=>IslandMap.OnLand(P,-8)))
                throw new Exception("A reef patch extends onto dry land or cannot be drawn");
        }
        foreach(Vector2 P in W.CapeScarpFace.Concat(W.CapeScarpCap))
        {
            var Soil=Array.Find(WorldGeometry.LandmarkSoils,S=>S.Id==17);
            if(!IslandMap.OnLand(P,8) || WorldGeometry.SoilCoverage(Soil,P-W.LmAt[17])<.999f)
                throw new Exception("The cape scarp is not covered by slate ground");
        }
        if(Geometry2D.TriangulatePolygon(W.CapeScarpFace).Length==0 || Geometry2D.TriangulatePolygon(W.CapeScarpCap).Length==0)
            throw new Exception("Cape rock rim is not drawable");
        if(W.CapeGulls.Count<2 || W.CapeGulls.Any(P=>IslandMap.OnLand(P,-24))) throw new Exception("Cape seabirds are missing or over dry land");
        GD.Print($"LOOKOUTS ALL OK: shared centres, complete dry ground, covered fixtures, clear entrances, {W.Coral.Count} underwater reef patches and {W.CapeGulls.Count} seabirds");
    }
}
