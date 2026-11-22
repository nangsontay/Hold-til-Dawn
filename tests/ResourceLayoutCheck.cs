using Godot;
using System;
using System.Linq;
using HoldTilDawn;

public partial class ResourceLayoutCheck : Node
{
    public override void _Ready()
    {
        try
        {
            using var S = new GameSim(); S.AddPlayer(1, "Engineer", 2); S.AddPlayer(2, "Scout", 0); S.StartMatch();
            var W = (WorldGeometry)S.World;
            for (int B = 0; B < IslandMap.Bridges.Count; B++)
            {
                Vector2 C = IslandMap.Bridges[B].At;
                foreach (float Radius in new[] { 9.6f, 16f, 35f })
                {
                    // Both directions, including the large boss body, can still cross the whole bridge.
                    foreach (int Sign in new[] { -1, 1 })
                    {
                        Vector2 Start = C + new Vector2(Sign * (IslandMap.BRIDGE_HALF_LEN + 36), 0), Goal = C - new Vector2(Sign * (IslandMap.BRIDGE_HALF_LEN + 36), 0);
                        Vector2 P = Start;
                        for (int I = 0; I < 140; I++) P = (Vector2)S.MoveActor(P, P.MoveToward(Goal, 3), Radius);
                        if (P.DistanceTo(Goal) > 4) throw new Exception($"Bridge {B} no longer supports radius {Radius}: {P / 32} -> {Goal / 32}");
                    }
                }
                foreach (float X in new[] { -IslandMap.BRIDGE_HALF_LEN + 24, 0, IslandMap.BRIDGE_HALF_LEN - 24 })
                    foreach (int Side in new[] { -1, 1 })
                    {
                        Vector2 P = C + new Vector2(X, 0);
                        for (int I = 0; I < 100; I++) P = W.MoveBodyFast(P, P + new Vector2(0, Side * 6), 9.6f, Array.Empty<Rect2>());
                        float Limit = IslandMap.BRIDGE_RAIL_OFFSET - IslandMap.BRIDGE_RAIL_RADIUS - 9.6f;
                        if (Side * (P.Y - C.Y) > Limit + 1.5f) throw new Exception($"Bridge {B} rail can be crossed at {X}");
                        // Entering sideways from the bank must also stop at the rail.
                        P = C + new Vector2(X, Side * (IslandMap.BRIDGE_RAIL_OFFSET + 36));
                        for (int I = 0; I < 50; I++) P = W.MoveBodyFast(P, P + new Vector2(0, -Side * 4), 9.6f, Array.Empty<Rect2>());
                        if (Side * (P.Y - C.Y) < IslandMap.BRIDGE_RAIL_OFFSET + 16)
                            throw new Exception($"Bridge {B} can be entered through a side rail");
                    }
            }
            GD.Print("BRIDGE RAILS ALL OK: all five bridges, both sides, dry abutments and boss crossings");
            var Nodes = ((GArray)S.Nodes).Cast<GDict>().ToArray();
            int[] Minimum = { 36, 32, 36 };
            for (int Type = 0; Type < 3; Type++)
            {
                Vector2 C = WorldGeometry.HomeFarmCentre(Type);
                var Here = Nodes.Where(N => Gd.Int(N["type"]) == Type && WorldGeometry.InHomeFarm((Vector2)N["pos"], Type)).ToArray();
                var Soil = Array.Find(WorldGeometry.LandmarkSoils, P => P.Id == Type + 2);
                if (Here.Length < Minimum[Type] || W.LmAt[Type + 2] != C || W.SoilCentre(Soil) != C) throw new Exception("Home farm and its soil have different centres or insufficient resources");
                if (Here.Count(N => !Gd.Truth(N.Get("ambient", true))) != 10 * GameSim.NODE_MUL) throw new Exception("Primary home resources were duplicated or lost");
                foreach (var N in Here)
                {
                    Vector2 P = (Vector2)N["pos"];
                    foreach (Vector2 Offset in new[] { Vector2.Zero, Vector2.Left, Vector2.Right, Vector2.Up, Vector2.Down })
                        if (WorldGeometry.SoilCoverage(Soil, P - C + Offset * (Harvestables.Radius(N) + 3)) < .999f)
                            throw new Exception($"Home {Type} resource has exposed grass around its footprint at {P / 32}");
                }
                Vector2 Walker = C - new Vector2(WorldGeometry.HomeFarmRadii.X + 24, 0), Goal = C + new Vector2(WorldGeometry.HomeFarmRadii.X + 24, 0);
                for (int I = 0; I < 150; I++) Walker = (Vector2)S.MoveActor(Walker, Walker.MoveToward(Goal, 3), 9.6f);
                if (Walker.DistanceTo(Goal) > 3) throw new Exception($"Home farm {Type} central aisle is blocked");
                int Outside = Nodes.Count(N => Gd.Int(N["type"]) == Type && !WorldGeometry.InHomeFarm((Vector2)N["pos"]));
                if (Outside != W.OutsideAfter[Type] || Math.Abs(W.OutsideAfter[Type] - W.OutsideBefore[Type] * .75f) > 3.5f)
                    throw new Exception($"Outer material {Type} did not decrease by 25 percent");
                GD.Print($"HOME FARM {Type}: {Here.Length} nodes, opaque ground, clear aisle; outer {W.OutsideBefore[Type]} -> {W.OutsideAfter[Type]}");
            }
            if (W.HarvestSites.Count(P => P.Type == 3 && W.InCaveFloor(P.Pos)) != 19) throw new Exception("The wood/stone/herb reduction changed titanium veins");
            if (W.GardenBeds.Any(B => W.HarvestSites.Count(P => P.Type == 2 && B.HasPoint(P.Pos)) != 6)) throw new Exception("The outer herb reduction did not preserve all six garden beds");
            var Titanium = Nodes.Where(N=>Gd.Int(N["type"])==3 && !Gd.Truth(N.Get("ambient",false))).ToArray();
            int[] Sites={5,6,7,13,14};
            for(int I=0;I<Titanium.Length;I++)
            {
                int Id=Sites[I%Sites.Length];
                if(Id is not (5 or 7)) continue;
                Vector2 P=(Vector2)Titanium[I]["pos"];
                var Soil=Array.Find(WorldGeometry.LandmarkSoils,S=>S.Id==Id);
                if(WorldGeometry.SoilCoverage(Soil,P-W.LmAt[Id])<.95f || W.InExpeditionAisle(P))
                    throw new Exception($"Titanium at site {Id} follows the old map centre or blocks a passage");
            }
            foreach(int Id in new[]{5,7,11})
            {
                var Site=W.ExpeditionSites[Id];
                Vector2 P=Id==11 ? Site.Centre+new Vector2(0,Site.Half.Y+8) : Site.Centre+new Vector2(-Site.Half.X-5,Id==5?60:4);
                Vector2 Goal=Id==11 ? Site.Centre-new Vector2(0,Site.Half.Y+8) : Site.Centre+new Vector2(Site.Half.X+5,Id==5?60:4);
                for(int I=0;I<500 && P.DistanceTo(Goal)>2;I++) P=(Vector2)S.MoveActor(P,P.MoveToward(Goal,3),9.6f);
                if(P.DistanceTo(Goal)>3) throw new Exception($"Resources block expedition passage {Id} at {P/32}");
            }
            foreach(int Id in new[]{6,13})
            {
                Vector2 Gate=W.CompoundGates[Id],Goal=W.LmAt[Id]+new Vector2(0,-35),P=Gate+new Vector2(0,48);
                for(int I=0;I<200 && P.DistanceTo(Goal)>2;I++) P=(Vector2)S.MoveActor(P,P.MoveToward(Goal,3),9.6f);
                if(P.DistanceTo(Goal)>3) throw new Exception($"Resources obstruct compound {Id} entrance at {P/32}");
                Goal=Gate+new Vector2(0,48);
                for(int I=0;I<200 && P.DistanceTo(Goal)>2;I++) P=(Vector2)S.MoveActor(P,P.MoveToward(Goal,3),9.6f);
                if(P.DistanceTo(Goal)>3) throw new Exception($"Resources obstruct compound {Id} exit");
            }
            GD.Print("COMPOUND RESOURCE ACCESS ALL OK: both enclosed compounds can be entered and exited with all resources present");
            foreach(int Id in new[]{12,17})
            {
                Vector2 P=W.CompoundEntrances[Id],Goal=W.LmAt[Id];
                for(int I=0;I<180 && P.DistanceTo(Goal)>2;I++) P=(Vector2)S.MoveActor(P,P.MoveToward(Goal,3),9.6f);
                if(P.DistanceTo(Goal)>3) throw new Exception($"Resources block lookout {Id} entrance");
                Goal=W.CompoundEntrances[Id];
                for(int I=0;I<180 && P.DistanceTo(Goal)>2;I++) P=(Vector2)S.MoveActor(P,P.MoveToward(Goal,3),9.6f);
                if(P.DistanceTo(Goal)>3) throw new Exception($"Resources block lookout {Id} exit");
            }
            GD.Print("LOOKOUT RESOURCE ACCESS ALL OK: both observation decks remain accessible with all resources present");
            GD.Print("EXPEDITION RESOURCE ACCESS ALL OK: titanium follows rebuilt scenes and every passage remains clear with all resources present");
            GD.Print("RESOURCE LAYOUT ALL OK"); GetTree().Quit();
        }
        catch (Exception E) { GD.PushError(E.ToString()); GetTree().Quit(1); }
    }
}
