using Godot;
using Environment = Godot.Environment;
using FileAccess = Godot.FileAccess;
using System;
using System.Threading.Tasks;
using HoldTilDawn;

public partial class GeometryCheck : Node
{
    public dynamic Fails = 0;
    public void Check(dynamic Ok, dynamic Message)
    {
        GD.Print(((Gd.Truth(Ok) ? (dynamic)("ok   ") : "FAIL ") + Message));
        if (!(Gd.Truth(Ok)))
        {
            Fails += 1;
        }
    }

    public dynamic Fixture()
    {
        dynamic S = new GameSim();
        S.AddPlayer(1, "Engineer", 2);
        S.StartMatch();
        Gd.Clear(S.Nodes);
        S.Towers = new GArray((object)null, (object)null, (object)null);
        S.World.StaticSolids = new GArray();
        S.World.Rebuild(new GArray(), new GArray());
        return S;
    }

    public override void _Ready()
    {
        try { RunChecks(); }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }

    private void RunChecks()
    {
        dynamic First = new WorldGeometry();
        dynamic Second = new WorldGeometry();
        this.Check(((First.Deco == Second.Deco) && (First.Solids == Second.Solids)), "server and renderer build identical scenery/colliders");
        this.Check(Gd.Any(First.Deco, (Func<dynamic, dynamic>)((D) => (Gd.At(D, 0) == Gd.MakeVector2(960, 320)))), "Godot map scene supplies hand-placed landmarks");
        this.Check(Gd.Any(First.Solids, (Func<dynamic, dynamic>)((D) => ((Gd.At(D, 0) == Gd.MakeVector2(960, 320)) && (Gd.At(D, 1) == 18.0f)))), "Inspector collider radius is used by simulation");
        this.Check((Gd.Absf((WorldGeometry.CircleHit(Vector2.Zero, Gd.MakeVector2(100, 0), Gd.MakeVector2(50, 0), 10) - 0.4f)) < 0.0001f), "swept collision catches a target between frame endpoints");
        this.Check((WorldGeometry.CircleHit(Vector2.Zero, Gd.MakeVector2(100, 0), Gd.MakeVector2(150, 0), 10) == float.PositiveInfinity), "segment does not hit objects beyond its endpoint");
        this.Check((WorldGeometry.CircleHit(Vector2.Zero, Vector2.Zero, Vector2.Zero, 10) == 0), "zero-length segment inside a collider is safe");
        dynamic S = this.Fixture();
        S.World.StaticSolids = new GArray(new GArray(Gd.MakeVector2(500, 0), 24.0f, true));
        S.World.Rebuild(new GArray(), new GArray());
        Vector2 Moved = (Vector2)(S.MoveActor(Gd.MakeVector2(400, 0), Gd.MakeVector2(600, 0), 10));
        this.Check(((Moved.X <= 466.01f) && (Moved.DistanceTo(Gd.MakeVector2(500, 0)) >= 33.99f)), "large movement cannot tunnel through a rock");
        dynamic Pos = Gd.MakeVector2(400, 0);
        dynamic Clear = true;
        foreach (dynamic I in Gd.Iter(160))
        {
            Vector2 Way = (Vector2)(S.Navigate(Pos, Gd.MakeVector2(650, 0), 10, false));
            Pos = S.MoveActor(Pos, (Pos + ((Way - Pos).Normalized() * Gd.Minf(4, Pos.DistanceTo(Way)))), 10);
            Clear = (Gd.Truth(Clear) && (Pos.DistanceTo(Gd.MakeVector2(500, 0)) >= 33.9f));
        }
        this.Check((Gd.Truth(Clear) && (Pos.DistanceTo(Gd.MakeVector2(650, 0)) < 8)), "navigation reaches the goal around a rock without entering it");
        dynamic P = Gd.At(S.Players, 1);
        P.Pos = Gd.MakeVector2(400, 0);
        P.MoveCredit = 30.0f;
        S.Input(1, Gd.MakeVector2(1500, 0), 0, false, false, false, P.Tp);
        this.Check((P.Pos.DistanceTo(Gd.MakeVector2(400, 0)) <= 30.01f), "server limits an arbitrary client position jump");
        Vector2 Before = (Vector2)(P.Pos);
        S.Input(1, Gd.MakeVector2(float.NaN, 0), float.NaN, false, false, false, P.Tp);
        this.Check(((P.Pos == Before) && Gd.Truth(Gd.IsFinite(P.Aim))), "invalid network coordinates/aim do not poison the simulation");
        S = this.Fixture();
        P = Gd.At(S.Players, 1);
        P.Pos = Gd.MakeVector2(650, 0);
        P.Hp = 100;
        P.Invuln = 0;
        S.World.StaticSolids = new GArray(new GArray(Gd.MakeVector2(500, 0), 24.0f, true));
        S.World.Rebuild(new GArray(), new GArray());
        S.Proj = new GArray(new GDict { { "pos", Gd.MakeVector2(400, 0) }, { "vel", Gd.MakeVector2(400, 0) }, { "dmg", 10.0f }, { "ttl", 1.0f } });
        S.TickProj(0.8f);
        this.Check(((P.Hp == 100) && Gd.Truth(Gd.IsEmpty(S.Proj))), "rock absorbs a projectile before it reaches the player");
        S.World.StaticSolids = new GArray();
        S.World.Rebuild(new GArray(), new GArray());
        S.Proj = new GArray(new GDict { { "pos", Gd.MakeVector2(400, 0) }, { "vel", Gd.MakeVector2(400, 0) }, { "dmg", 10.0f }, { "ttl", 1.0f } });
        S.TickProj(0.8f);
        this.Check(((P.Hp == 90) && Gd.Truth(Gd.IsEmpty(S.Proj))), "projectile hits a player even when both endpoints are outside the hitbox");
        S.PlaceTower(0, Gd.MakeVector2((-400), 0));
        dynamic Target = S.Spawn(GameSim.GRUNT, 0, 0, false);
        Target.Pos = Gd.MakeVector2((-550), 0);
        S.World.StaticSolids = new GArray(new GArray(Gd.MakeVector2((-475), 0), 24.0f, true));
        S.World.Rebuild(new GArray(), S.Towers);
        S.TickTowers(0.5f);
        this.Check((Target.Hp == Target.Maxhp), "tower does not shoot through a rock");
        S.World.StaticSolids = new GArray();
        S.World.Rebuild(new GArray(), S.Towers);
        S.TickTowers(0.5f);
        this.Check((Gd.Absf(((Target.Hp - Target.Maxhp) + 4.0f)) < 0.01f), "tower shoots normally when its line of sight clears");
        Gd.Clear(S.Enemies);
        S.Towers = new GArray((object)null, (object)null, (object)null);
        S.World.Rebuild(new GArray(), new GArray());
        S.Phase = "night";
        P.Pos = Gd.MakeVector2(150, 80);
        P.Aim = 0;
        dynamic Enemy = S.Spawn(GameSim.GRUNT, 2, 0, false);
        Enemy.Pos = Gd.MakeVector2(210, 80);
        S.Attack(P);
        this.Check((Enemy.Hp == Enemy.Maxhp), "melee cannot damage an enemy through a closed base wall");
        P.Pos = Gd.MakeVector2(215, 80);
        S.Attack(P);
        this.Check((Enemy.Hp < Enemy.Maxhp), "unobstructed melee still deals damage");
        S = this.Fixture();
        P = Gd.At(S.Players, 1);
        P.Pos = Gd.MakeVector2(400, 0);
        P.Inter = true;
        S.Nodes = new GArray(new GDict { { "type", 0 }, { "pos", Gd.MakeVector2(432, 0) }, { "amount", 5 } });
        S.Tick(0.1f);
        this.Check(((P.Activity == "gather") && (Gd.At(Gd.At(S.Snapshot().Ps, 1), 22) == "gather")), "gather animation state is authoritative and included in snapshots");
        P.Inter = false;
        S.Tick(0.1f);
        this.Check((P.Activity == "idle"), "releasing interaction stops the activity animation");
        S = this.Fixture();
        S.World.StaticSolids = new GArray(new GArray(Gd.MakeVector2(500, 0), 24.0f, true));
        S.World.Rebuild(new GArray(), new GArray());
        P = Gd.At(S.Players, 1);
        P.Pos = Gd.MakeVector2(650, 0);
        Enemy = S.Spawn(GameSim.GRUNT, 2, 0, false);
        Enemy.Pos = Gd.MakeVector2(400, 0);
        Enemy.State = 1;
        Enemy.Target = 1;
        Enemy.Anchor = Enemy.Pos;
        Clear = true;
        foreach (dynamic I in Gd.Iter(90))
        {
            S.TickEnemies(0.05f);
            Clear = (Gd.Truth(Clear) && (Enemy.Pos.DistanceTo(Gd.MakeVector2(500, 0)) >= 39.9f));
        }
        this.Check((Gd.Truth(Clear) && (Enemy.Pos.X > 560)), "enemy follows a route around scenery");
        dynamic Bot = Gd.At(S.Players, 1);
        Bot.Pos = Gd.MakeVector2(400, 0);
        Bot.NavT = 0.0f;
        Clear = true;
        foreach (dynamic I in Gd.Iter(100))
        {
            Bot.NavT -= 0.05f;
            Bots.Move(S, Bot, Gd.MakeVector2(650, 0), 0.05f);
            Clear = (Gd.Truth(Clear) && (Bot.Pos.DistanceTo(Gd.MakeVector2(500, 0)) >= 33.5f));
        }
        this.Check((Gd.Truth(Clear) && (Bot.Pos.DistanceTo(Gd.MakeVector2(650, 0)) < 12)), "bot walks around the same obstacle as the player and enemy");
        GD.Print(((Fails == 0) ? (dynamic)("GEOMETRY ALL OK") : Gd.Format("GEOMETRY FAILED %d", Fails)));
        GetTree().Quit(Fails);
    }

}
