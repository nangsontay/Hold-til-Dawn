using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
using HoldTilDawn;

public partial class HarvestCheck : Node
{
    void Check(bool Ok, string Message)
    {
        if (!Ok) throw new InvalidOperationException(Message);
        GD.Print("ok   ", Message);
    }

    public override void _Ready()
    {
        try { Run(); GetTree().Quit(); }
        catch (Exception Error) { GD.PushError(Error.ToString()); GetTree().Quit(1); }
    }

    void Run()
    {
        var S = new GameSim();
        S.AddPlayer(1, "Engineer", 2);
        S.StartMatch();
        var W = (WorldGeometry)S.World;
        var VisibleResources = new HashSet<(Vector2, string)>();
        foreach (GDict N in (GArray)S.Nodes) VisibleResources.Add(((Vector2)N.Get("pos"), Gd.Str(N.Get("sprite", ""))));
        Check(W.HarvestSites.All(Site => VisibleResources.Contains((Site.Pos, Site.Sprite))), "every natural scenery resource is an actual gatherable node");
        Check(((GArray)W.Deco).All(D => Harvestables.TypeOf(Gd.Str(((GArray)D)[1])) < 0 && !Gd.Str(((GArray)D)[1]).StartsWith("dead_")), "no decorative trees, rocks, herbs or dead trees remain in world scenery");
        foreach (var Site in W.HarvestSites)
            for (int Stage = 0; Stage <= 5; Stage++)
            {
                string Name = Stage == 0 ? Site.Sprite : Harvestables.StageSprite(Site.Sprite, Stage);
                CheckAsset(Name);
            }
        GD.Print($"ok   full, damaged and depleted sprites exist for all {W.HarvestSites.Count} scenery resource sites");

        foreach (int Type in new[] { 0, 1, 2 })
        {
            var Site = W.HarvestSites.First(N => N.Type == Type && N.Radius <= 20 && IslandMap.OnGrass(N.Pos, 64));
            var N = Site.CreateNode();
            S.Nodes = new GArray(N);
            W.StaticSolids = new GArray();
            W.Rebuild(S.Nodes, S.Towers);
            int Revision = (int)W.Revision;
            var Before = (GArray)S.SnapshotNodes()[0];
            N["amount"] = 4;
            var Changed = (GArray)S.SnapshotNodes()[0];
            Check((string)Changed[3] == Site.Sprite && (float)Changed[4] == Site.Radius, "snapshot preserves the scenery sprite and footprint");
            Check(!ReferenceEquals(Before, Changed) && (int)Before[2] == 5 && (int)Changed[2] == 4, "resource snapshot changes without mutating its previous record");
            W.Rebuild(S.Nodes, S.Towers);
            Check((int)W.Revision == Revision, "partial harvesting does not rebuild unchanged collision/navigation");
            N["amount"] = 5;
            dynamic Player = S.Players[1];
            Player.Pos = Site.Pos + Vector2.Down * (Site.Radius + 11);
            Player.Inter = true;
            Player.GatherAcc = 0;
            Player.GatherPos = Player.Pos;
            int Had = (int)S.Count(Player, GameSim.RES[Type]);
            for (int I = 0; I < 16 * 60; I++) S.Tick(1.0f / 60);
            Player.Inter = false;
            Check((int)N["amount"] == 0 && (int)S.Count(Player, GameSim.RES[Type]) == Had + 5, "holding E fully harvests a scenery " + GameSim.RES[Type] + " resource");
            W.Rebuild(S.Nodes, S.Towers);
            if (Site.Radius > 0) Check(float.IsPositiveInfinity((float)W.FirstHit(Site.Pos, Site.Pos, 1)), "exhausted scenery resource no longer blocks movement");
            S.Dawn();
            W.Rebuild(S.Nodes, S.Towers);
            Check((int)N["amount"] == 5, "scenery resource regenerates at dawn");
        }
        GD.Print("HARVEST ALL OK");
    }

    static void CheckAsset(string Name)
    {
        if (!Godot.FileAccess.FileExists($"res://art/nature/{Name}.png") && !Godot.FileAccess.FileExists($"res://art/landmarks/{Name}.png"))
            throw new InvalidOperationException("Missing resource sprite: " + Name);
    }
}
