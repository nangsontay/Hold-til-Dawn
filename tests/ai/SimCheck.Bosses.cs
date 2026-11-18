using Godot;
using Environment = Godot.Environment;
using FileAccess = Godot.FileAccess;
using System;
using System.Threading.Tasks;
using HoldTilDawn;

// Owner: Vu Duc Viet Anh. Boss archetype and threat checks (AGT-05..11, table 4.2, FML-05, FML-09).
public partial class SimCheck : Node
{
    // Night 2 with one boss of the given kind 12 tiles in front of door A, no waves, nodes cleared.
    private dynamic BossMatch(int Kind)
    {
        dynamic S = this.NewMatch();
        Gd.Clear(S.Nodes);
        S.World.Rebuild(S.Nodes, S.Towers);
        S.K = 2;
        S.Phase = "night";
        S.TimeLeft = 720.0f;
        S.NextWave = float.PositiveInfinity;
        S.BossSpawned = true;
        S.Active = new GArray(0);
        S.BossPlan = new GArray(Kind, ((Kind + 1) % 7));
        S.SpawnBoss();
        dynamic B = S.Boss();
        B.Pos = (Gd.At(GameSim.DOOR_POS, 0) + ((Gd.At(GameSim.DOOR_DIR, 0) * 12) * 32));
        B.SlamCd = 99.0f;
        foreach (dynamic P in Gd.Iter(Gd.Values(S.Players)))
        {
            P.Pos = Gd.MakeVector2(0, (-15 * 32));
        }
        return S;
    }

    private void CheckBosses()
    {
        // FML-05: night 4 never repeats night 2, and every archetype can come first
        dynamic S = new GameSim();
        dynamic Seen = new GDict();
        bool Repeat = false;
        for (int I = 0; I < 300; I++)
        {
            S.Rng.Seed = (ulong)(I + 1);
            S.DrawBosses();
            Seen[Gd.At(S.BossPlan, 0)] = true;
            Repeat = (Repeat || (Gd.At(S.BossPlan, 0) == Gd.At(S.BossPlan, 1)));
        }
        this.Check(((Gd.Size(Seen) == 7) && !Repeat), "all seven archetypes are drawn; night 4 never repeats night 2 (T-FML05)");
        S.Rng.Seed = 5;
        S.DrawBosses();
        dynamic First = Gd.Duplicate(S.BossPlan);
        S.Rng.Seed = 5;
        S.DrawBosses();
        this.Check((S.BossPlan == First), "the same seed draws the same bosses");
        // FML-02 / table 4.2: HP for 4 players on Medium, night 2
        S = new GameSim();
        foreach (dynamic I in Gd.Iter(4))
        {
            S.AddPlayer((I + 1), "P", ((I == 0) ? 2 : 0));
        }
        S.Action(1, "start", 0);
        S.K = 2;
        S.Active = new GArray(0);
        dynamic Hp = new GArray();
        foreach (dynamic Kind in Gd.Iter(7))
        {
            S.BossPlan = new GArray(Kind, 0);
            Gd.Clear(S.Enemies);
            S.SpawnBoss();
            Gd.Append(Hp, Gd.Roundi(S.Boss().Maxhp));
        }
        this.Check((Hp == new GArray(3900, 4680, 3510, 3120, 4290, 3120, 2730)), ("boss HP per archetype at n = 4 matches table 4.2: " + Gd.Str(Hp)));
        // Juggernaut: line telegraph, dash, 35 damage and knockback, then a slam and a 2 s recovery
        S = this.BossMatch(GameSim.JUGGERNAUT);
        dynamic B = S.Boss();
        dynamic V = Gd.At(S.Players, 2);
        V.Pos = (B.Pos + Gd.MakeVector2(0, (6 * 32)));
        B.SigCd = 0.0f;
        this.Run(S, 0.1f);
        this.Check((Gd.Truth(Gd.Any(S.Tele, (Func<dynamic, dynamic>)((Tl) => (Tl.Kind == "line")))) && (B.Act == "charge_wind")), "Juggernaut shows a line telegraph before its charge");
        this.Check((V.Hp == V.Maxhp), "nothing is hit during the 1.0 s telegraph");
        this.Run(S, 2.0f);
        this.Check((V.Hp <= (V.Maxhp - 35)), Gd.Format("the charge hits the player on the line for 35 (hp %d)", V.Hp));
        this.Run(S, 1.5f);
        this.Check((B.Act == "recover"), "after the dash it slams and then recovers for 2 s");
        // Broodmother: 2 Grunts per summon, at most 4, gone with the boss, no drops
        S = this.BossMatch(GameSim.BROODMOTHER);
        B = S.Boss();
        this.Check((Gd.Absf((B.Spd - ((0.75f * 2.16f) * 32))) < 0.01f), "Broodmother moves at 0.75 x Grunt speed");
        B.Spd = 0.0f;
        B.SigCd = 0.0f;
        this.Run(S, 1.1f);
        Func<int> Summons = () => Gd.Size(Gd.Filter(S.Enemies, (Func<dynamic, dynamic>)((E) => Gd.Truth(Gd.Get(E, "summon", false)))));
        this.Check((Summons() == 2), "Broodmother summons 2 Grunts after a 1.0 s glow");
        B.SigCd = 0.0f;
        this.Run(S, 1.1f);
        B.SigCd = 0.0f;
        this.Run(S, 1.1f);
        this.Check((Summons() == 4), "never more than 4 summons alive");
        dynamic Killer = Gd.At(S.Players, 1);
        Killer.Pos = (B.Pos + Gd.MakeVector2(40, 0));
        Killer.Aim = Mathf.Pi;
        B.Hp = 1.0f;
        S.Hit(B, 10, Killer);
        Gd.Clear(S.Drops);
        S.Phase = "day";
        S.TickEnemies(0.016f);
        this.Check(((Summons() == 0) && Gd.Truth(Gd.IsEmpty(S.Drops))), "summons vanish with the Broodmother and drop nothing");
        // Bio-Cannon: a ring of 8 projectiles
        S = this.BossMatch(GameSim.BIO_CANNON);
        B = S.Boss();
        Gd.At(S.Players, 2).Pos = (B.Pos + Gd.MakeVector2(0, (6 * 32)));
        B.SigCd = 0.0f;
        this.Run(S, 1.05f);
        this.Check((Gd.Size(S.Proj) == 8), Gd.Format("Bio-Cannon fires a ring of 8 projectiles (%d)", Gd.Size(S.Proj)));
        // Burrower: cannot be hit underground, surfaces next to its target
        S = this.BossMatch(GameSim.BURROWER);
        B = S.Boss();
        V = Gd.At(S.Players, 2);
        V.Pos = (B.Pos + Gd.MakeVector2(0, (6 * 32)));
        B.SigCd = 0.0f;
        this.Run(S, 0.1f);
        float Hp0 = Gd.Float(B.Hp);
        S.Hit(B, 50, V);
        this.Check(((B.Act == "burrow") && (B.Hp == Hp0)), "a burrowed Burrower cannot be hit");
        this.Run(S, 4.0f);
        this.Check(((V.Hp <= (V.Maxhp - 35)) && (B.Pos.DistanceTo(V.Pos) >= (1.4f * 32))), "it surfaces at least 1.5 tiles away and hits a player who stays");
        // Gatebreaker: one 120-point door blow after a 1.0 s wind-up
        S = this.BossMatch(GameSim.GATEBREAKER);
        B = S.Boss();
        B.Pos = (Gd.At(GameSim.DOOR_POS, 0) + (Gd.At(GameSim.DOOR_DIR, 0) * (B.R + 6)));
        B.Cd = 0.0f;
        this.Run(S, 0.5f);
        this.Check((Gd.At(S.Doors, 0) == GameSim.DOOR_HP), "the door blow waits for its wind-up");
        this.Run(S, 0.7f);
        this.Check((Gd.Absf((Gd.At(S.Doors, 0) - (GameSim.DOOR_HP - 120.0f))) < 0.01f), Gd.Format("Gatebreaker deals 120 door damage in one blow (%.1f)", Gd.At(S.Doors, 0)));
        // Phantom: cloaks, a hit reveals it
        S = this.BossMatch(GameSim.PHANTOM);
        B = S.Boss();
        V = Gd.At(S.Players, 2);
        V.Pos = (B.Pos + Gd.MakeVector2(0, (7 * 32)));
        B.SigCd = 0.0f;
        this.Run(S, 0.1f);
        dynamic En = Gd.At(S.Snapshot().En, 0);
        this.Check(((B.Act == "cloak") && (Gd.At(En, 7) == "cloak")), "Phantom turns invisible");
        S.Hit(B, 10, V);
        this.Check(((B.Act == "") && (S.BossLook(B) == "shown")), "a hit reveals the Phantom");
        // Ironhide: 30% damage from the front, full from behind or while staggered
        S = this.BossMatch(GameSim.IRONHIDE);
        B = S.Boss();
        B.Face = 0.0f;
        V = Gd.At(S.Players, 2);
        Hp0 = Gd.Float(B.Hp);
        V.Pos = (B.Pos + Gd.MakeVector2(40, 0));
        S.Hit(B, 100, V);
        float Front = (Hp0 - Gd.Float(B.Hp));
        V.Pos = (B.Pos - Gd.MakeVector2(40, 0));
        Hp0 = Gd.Float(B.Hp);
        S.Hit(B, 100, V);
        float Back = (Hp0 - Gd.Float(B.Hp));
        this.Check(((Gd.Absf((Front - 30)) < 0.01f) && (Gd.Absf((Back - 100)) < 0.01f)), Gd.Format("Ironhide takes 30%% from the front, full from behind (%.0f / %.0f)", new GArray(Front, Back)));
        S.BossAct(B, "stagger", 3.0f);
        V.Pos = (B.Pos + Gd.MakeVector2(40, 0));
        Hp0 = Gd.Float(B.Hp);
        S.Hit(B, 100, V);
        this.Check((Gd.Absf(((Hp0 - Gd.Float(B.Hp)) - 100)) < 0.01f), "a staggered Ironhide takes full damage from the front");
        // FML-09: threat counts the last 5 s only; switching needs 20% more than the current target
        S = this.NewMatch();
        Gd.Clear(S.Nodes);
        S.World.Rebuild(S.Nodes, S.Towers);
        dynamic A = Gd.At(S.Players, 1);
        dynamic C = Gd.At(S.Players, 2);
        dynamic G = S.Spawn(GameSim.GRUNT, 0, 0.0f);
        G.Pos = (Gd.At(GameSim.DOOR_POS, 0) + ((Gd.At(GameSim.DOOR_DIR, 0) * 10) * 32));
        G.Spd = 0.0f;
        G.Hp = 1000.0f;
        A.Pos = (G.Pos + Gd.MakeVector2(0, 40));
        C.Pos = (G.Pos + Gd.MakeVector2(0, -40));
        S.Hit(G, 45, A);
        S.Hit(G, 30, C);
        this.Check((G.Target == 1), "45 vs 30 damage: the enemy keeps chasing the bigger threat");
        S.Hit(G, 25, C);
        this.Check((G.Target == 2), "55 > 1.2 x 45: it switches");
        S.Clock += 4.0f;
        S.Hit(G, 10, A);
        S.Clock += 2.0f;
        S.Retarget(G);
        this.Check(((S.Threat(G, 2) == 0.0f) && (S.Threat(G, 1) == 10.0f) && (G.Target == 1)), "hits older than 5 s no longer count");
    }
}
