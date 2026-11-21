using Godot;
using System;
using HoldTilDawn;

public partial class TacticalCraftCheck : Node
{
    int Checks;
    void Check(bool Pass, string Label) { if (!Pass) throw new Exception(Label); Checks++; GD.Print("ok   " + Label); }
    GameSim Sim;
    dynamic P, Ally;
    readonly Vector2 Field = new(0, 640);

    void Reset()
    {
        Sim.Phase = "day"; Sim.Clock = 10f; Sim.ResetTacticalItems();
        Sim.Enemies = new GArray(); Sim.Arrows = new GArray(); Sim.Proj = new GArray(); Sim.Drops = new GArray(); Sim.BuiltWalls = new GArray();
        Sim.Towers = new GArray(null, null, null); Sim.Doors = new GArray(500f, 500f, 500f);
        Sim.Broken = new GArray(false, false, false); Sim.DoorPause = new GArray(0f, 0f, 0f);
        Sim.World.Rebuild(new GArray(), Sim.Towers, Sim.BuiltWalls);
        Sim.ResetPlayer(P); Sim.ResetPlayer(Ally);
        P.Pos = Field; Ally.Pos = Field + new Vector2(0, 24); P.Inv = GameSim.EmptySlots(12); P.Role = 2;
    }
    void Give(string Item, int Count = 5) => Sim.SetCount(P, Item, Count);
    void Advance(float Seconds) { Sim.Clock += Seconds; Sim.TickTacticalItems(); }
    dynamic Enemy(Vector2 Pos, int Type = 0)
    {
        dynamic E = Sim.Spawn(Type, 0, 0f); E.Pos = Pos; E.Hp = 200f; E.Maxhp = 200f; E.Spd = 0f; return E;
    }
    dynamic BossAt(Vector2 Pos)
    {
        Sim.K = 2; Sim.SpawnBoss();
        dynamic E = Gd.At(Sim.Enemies, Gd.Size(Sim.Enemies) - 1); E.Pos = Pos; E.Hp = 2000f; return E;
    }
    public override void _Ready()
    {
        try { Run(); GD.Print("TACTICAL CRAFT ALL OK ", Checks); GetTree().Quit(); }
        catch (Exception E) { GD.PushError(E.ToString()); GetTree().Quit(1); }
    }
    void Run()
    {
        GD.Print("TACTICAL CRAFT CHECK starting");
        Sim = new GameSim();
        GD.Print("TACTICAL CRAFT CHECK simulation created");
        Sim.AddPlayer(1, "Engineer", 2); Sim.AddPlayer(2, "Ally", 1); Sim.Action(1, "start", 0);
        P = Gd.At(Sim.Players, 1); Ally = Gd.At(Sim.Players, 2);
        Check(Gd.Size(GameSim.RECIPES) == 22 && !GameSim.ITEMS.Has("wood_shield"), "22 recipes including rescue, no shield");
        for (int I = 10; I < 19; I++)
        {
            Reset(); P.Pos = GameSim.WORKBENCH_POS + new Vector2(0, 24);
            dynamic R = Gd.At(GameSim.RECIPES, I); string Id = Gd.Str(Gd.At(R, 3));
            int Tier = Gd.Int(Gd.At(R, 2)); Sim.Unlock = Tier;
            foreach (dynamic Res in Gd.Iter(Gd.At(R, 1))) Sim.SetCount(P, Res, Gd.At(Gd.At(R, 1), Res));
            Check(Sim.CanCraft(P, I, false), Id + " affordable at correct tier");
            if (Tier > 0) { Sim.Unlock = Tier - 1; Check(!Sim.CanCraft(P, I, false), Id + " locked before tier"); Sim.Unlock = Tier; }
            Sim.Craft(P, I); Sim.TickCraft(P, 2f); Check(Sim.Count(P, Id) == 0, Id + " requires full craft time");
            Sim.TickCraft(P, 1.1f); Check(Sim.Count(P, Id) == Gd.Int(Gd.At(R, 4)), Id + " craft output");
            foreach (dynamic Res in Gd.Iter(Gd.At(R, 1))) Check(Sim.Count(P, Res) == 0, Id + " spends " + Res);
            Check(GameSim.TacticalDescription(Id).Length > 0, Id + " has usage instructions");
            Check(ResourceLoader.Exists("res://art/items/" + Id + ".png"), Id + " has imported icon");
        }
        Reset(); Give("haste_tonic", 2);
        float NormalSpeed = Sim.PlayerSpeed(P);
        Check(Sim.UseTacticalItem(P, "haste_tonic", Field), "drink tonic");
        Check(MathF.Abs(Sim.PlayerSpeed(P) / NormalSpeed - 1.2f) < 0.001f, "tonic increases authoritative speed 20%");
        Advance(1f); Check(!Sim.UseTacticalItem(P, "haste_tonic", Field) && Sim.Count(P, "haste_tonic") == 1, "tonic cannot stack or waste a dose");
        Advance(7f); Check(Sim.PlayerSpeed(P) == NormalSpeed, "tonic expires after 8 seconds");
        Reset(); Give("vine_trap");
        Check(!Sim.UseTacticalItem(P, "vine_trap", new Vector2(float.NaN, 0)), "reject non-finite aim without spending");
        for (int I = 0; I < 3; I++) { Check(Sim.UseTacticalItem(P, "vine_trap", Field), "place trap " + I); Advance(0.6f); }
        Check(!Sim.UseTacticalItem(P, "vine_trap", Field) && Sim.VineTraps.Count == 3, "per-player trap cap");
        dynamic Grunt = Enemy(Field);
        Advance(0.3f); Check(Gd.Float(Gd.Get(Grunt, "stun_until", 0f)) > Gd.Float(Sim.Clock), "trap roots normal enemy");
        Sim.TickEnemies(0.1f); Check((Vector2)Grunt.Pos == Field, "rooted enemy cannot move");
        Advance(121f); Check(Sim.VineTraps.Count == 0, "traps expire");
        Reset(); Give("vine_trap"); Sim.UseTacticalItem(P, "vine_trap", Field); dynamic Boss = BossAt(Field);
        Advance(0.3f); Check(Sim.VineTraps.Count == 1 && Gd.Float(Gd.Get(Boss, "stun_until", 0f)) == 0, "boss ignores vine trap");

        Reset(); Give("poison_flask", 3); Grunt = Enemy(Field + new Vector2(24, 0));
        Check(Sim.UseTacticalItem(P, "poison_flask", Field), "throw poison");
        Advance(0.5f); Check(Gd.Float(Grunt.Hp) == 196f, "poison pulse deals 4 damage");
        Advance(0.2f); Check(Sim.UseTacticalItem(P, "poison_flask", Field), "throw overlapping staggered poison");
        Advance(0.3f); Advance(0.2f); Check(Gd.Float(Grunt.Hp) == 192f, "staggered poison zones do not stack");
        float Before = Gd.Float(Grunt.Hp); Advance(20f); Check(Gd.Float(Grunt.Hp) == Before && Sim.TacticalAreas.Count == 0, "expired areas do not catch up damage");
        Reset(); Give("poison_flask"); Grunt = Enemy(Field + new Vector2(24, 0)); Grunt.Hp = 4f;
        Sim.UseTacticalItem(P, "poison_flask", Field); Advance(0.5f);
        Check(Gd.Size(Sim.Enemies) == 0 && P.Kills == 1 && P.Dmg == 4f, "poison kills use normal damage / kill accounting");
        Sim.Hit(Grunt, 4f, P); Check(P.Kills == 1, "dead targets cannot award duplicate kills");

        Reset(); Give("healing_flask"); P.Hp = 40f; Ally.Hp = 30f;
        Sim.UseTacticalItem(P, "healing_flask", Field); Advance(0.5f);
        Check(P.Hp == 44f && Ally.Hp == 34f, "healing zone heals caster and ally");
        Ally.State = 1; Advance(0.5f); Check(Ally.State == 1 && Ally.Hp == 34f, "healing does not revive downed players");
        P.Hp = P.Maxhp - 1f; Advance(0.5f); Check(P.Hp == P.Maxhp, "healing clamps max HP");
        Reset(); Give("poison_flask", 20);
        for (int I = 0; I < GameSim.MAX_TACTICAL_AREAS; I++) Sim.TacticalAreas.Add(new GDict { { "pos", Field }, { "owner", 1 }, { "kind", "heal" }, { "until", 100f }, { "next", 100f } });
        Check(!Sim.UseTacticalItem(P, "poison_flask", Field) && Sim.Count(P, "poison_flask") == 20, "area cap preserves unused consumable");

        Reset(); P.Weapon = "long_spear"; P.Aim = 0f;
        dynamic A = Enemy(Field + new Vector2(36, 0)), B = Enemy(Field + new Vector2(56, 0)), C = Enemy(Field + new Vector2(78, 0));
        dynamic Side = Enemy(Field + new Vector2(50, 42)); Sim.Attack(P);
        Check(A.Hp == 178f && B.Hp == 178f && C.Hp == 200f && Side.Hp == 200f, "long spear hits nearest two in a narrow line");
        Reset(); P.Weapon = "stun_maul"; P.Aim = 0f; Grunt = Enemy(Field + new Vector2(24, 0)); Sim.Attack(P);
        Check(Grunt.Hp == 170f && Gd.Get(Grunt, "stun_until", 0f) > Sim.Clock && P.AtkCd > 1.5f, "maul damages / stuns with slow attack cadence");
        Boss = BossAt(Field + new Vector2(25, 0)); Sim.Attack(P);
        Check(Gd.Get(Boss, "slow_until", 0f) > Sim.Clock && Gd.Get(Boss, "stun_until", 0f) == 0f, "maul slows boss instead of stunning");

        Reset(); Give("explosive_arrow", 3);
        Check(!Sim.UseTacticalItem(P, "explosive_arrow", Field + Vector2.Right * 200) && Sim.Count(P, "explosive_arrow") == 3, "explosive arrows require a bow");
        Give("starter_bow", 1); Grunt = Enemy(Field + new Vector2(70, 0)); B = Enemy(Field + new Vector2(76, 20));
        Check(Sim.UseTacticalItem(P, "explosive_arrow", Field + Vector2.Right * 200) && Sim.Count(P, "explosive_arrow") == 2, "Scout starter bow consumes explosive ammo");
        Sim.TickArrows(0.2f); Check(Grunt.Hp == 168f && B.Hp == 168f && P.Hp == P.Maxhp, "explosive arrow damages nearby foes once, no friendly fire");
        Check(Gd.Size(Sim.Arrows) == 0, "explosive projectile consumed on impact");
        Reset(); Give("explosive_arrow", 1); Give("bow", 1); Sim.UseTacticalItem(P, "explosive_arrow", Field + Vector2.Right * 500); Sim.TickArrows(1f);
        Check(Gd.Size(Sim.Arrows) == 0 && Sim.TacticalAreas.Count == 1, "explosive arrow detonates at maximum range");

        Reset(); Give("repair_kit", 2);
        Vector2 Door = (Vector2)Gd.At(GameSim.DOOR_POS, 0), Direction = (Vector2)Gd.At(GameSim.DOOR_DIR, 0);
        P.Pos = Door + Direction * 50; Sim.Doors[0] = 300f;
        Check(Sim.UseTacticalItem(P, "repair_kit", Door), "begin field door repair");
        Sim.TickFieldRepair(P, 2f); Check(Gd.At(Sim.Doors, 0) == 300f && Sim.Count(P, "repair_kit") == 2, "repair reserves kit until completion");
        Sim.TickFieldRepair(P, 1.1f); Check(Gd.At(Sim.Doors, 0) == 400f && Sim.Count(P, "repair_kit") == 1, "repair adds 100 HP / consumes one kit");
        Sim.UseTacticalItem(P, "repair_kit", Door); P.Pos += new Vector2(0, 6); Sim.TickFieldRepair(P, 1f);
        Check(Sim.Count(P, "repair_kit") == 1 && P.RepairKitT == 0f, "moving cancels field repair without spending");
        P.Pos = Door + Direction * 50; Sim.UseTacticalItem(P, "repair_kit", Door); Sim.Hurt(P, 5f);
        Check(P.RepairKitT == 0f && Sim.Count(P, "repair_kit") == 1, "damage cancels field repair");
        P.Role = 0; Check(!Sim.UseTacticalItem(P, "repair_kit", Door), "non-Engineer cannot use repair kit");
        P.Role = 2; Sim.Phase = "night"; Sim.UseTacticalItem(P, "repair_kit", Door); Sim.TickFieldRepair(P, 3.1f);
        Check(Gd.At(Sim.Doors, 0) == 500f, "field repair works on closed doors at night");

        Reset(); Give("repair_kit", 3);
        dynamic Wall = new GDict { { "pos", Field + new Vector2(60, 0) }, { "hp", 30f }, { "maxhp", 200f }, { "kind", "wood" }, { "vert", false } };
        Sim.BuiltWalls.Add(Wall);
        Check(Sim.UseTacticalItem(P, "repair_kit", (Vector2)Wall.Pos), "begin wall field repair");
        Sim.TickFieldRepair(P, 3.1f); Check(Wall.Hp == 130f && Sim.Count(P, "repair_kit") == 2, "field kit repairs built walls");
        Sim.UseTacticalItem(P, "repair_kit", (Vector2)Wall.Pos); Sim.BuiltWalls.Clear(); Sim.TickFieldRepair(P, 3.1f);
        Check(Sim.Count(P, "repair_kit") == 2, "destroyed wall does not consume reserved kit");
        Sim.PlaceTower(0, Field + new Vector2(60, 0)); dynamic Tower = Gd.At(Sim.Towers, 0); Tower.Hp = 100f;
        Check(Sim.UseTacticalItem(P, "repair_kit", (Vector2)Tower.Pos), "begin tower field repair");
        Sim.TickFieldRepair(P, 3.1f); Check(Tower.Hp == 200f && Sim.Count(P, "repair_kit") == 1, "field kit repairs towers");
        Tower.Wrecked = true; Tower.Hp = 85f;
        Sim.UseTacticalItem(P, "repair_kit", (Vector2)Tower.Pos); Sim.TickFieldRepair(P, 3.1f);
        Check(Tower.Hp == 90f, "wrecked tower repair stays within 30 percent cap");

        Reset(); Give("haste_tonic", 1); P.Inv[0] = new GArray("haste_tonic", 1); P.Sel = 0;
        Sim.Action(1, "use_item", Field);
        Check(Sim.Count(P, "haste_tonic") == 0 && Sim.PlayerSpeed(P) > Gd.Float(GameSim.SPEED), "hotbar use action consumes selected item");

        Reset(); Give("gathering_kit", 2);
        dynamic Node = Gd.At(Sim.Nodes, 0); Node.Amount = 5; P.Pos = Node.Pos;
        Sim.Interact(P, 1f); Check(MathF.Abs(Gd.Float(P.GatherAcc) - 0.4375f) < 0.001f, "gathering kit reduces farm duration 20%, does not stack");
        Sim.SetCount(P, "gathering_kit", 0); P.GatherAcc = 0f; Sim.Interact(P, 1f);
        Check(MathF.Abs(Gd.Float(P.GatherAcc) - 0.35f) < 0.001f, "dropping gathering kit restores normal rate");
        Reset(); Give("haste_tonic"); Sim.UseTacticalItem(P, "haste_tonic", Field); Sim.VineTraps.Add(new GDict()); Sim.Dawn();
        Check(Sim.VineTraps.Count == 0 && Sim.TacticalAreas.Count == 0 && Sim.PlayerSpeed(P) == Gd.Float(GameSim.SPEED), "dawn clears temporary tactical effects");
        dynamic Snapshot = Sim.Snapshot(); Check(Gd.Has(Snapshot, "tactical") && Gd.Size(Gd.At(Snapshot.Ps, 1)) == 32, "snapshot includes tactical state and speed / repair HUD fields");
    }
}
