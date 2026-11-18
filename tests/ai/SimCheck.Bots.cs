using Godot;
using Environment = Godot.Environment;
using FileAccess = Godot.FileAccess;
using System;
using System.Threading.Tasks;
using HoldTilDawn;

// Owner: Vu Duc Viet Anh. Bot teammate checks (Bots.cs; bot takeover cards A19-A21), run by tests/SimCheck.tscn.
public partial class SimCheck : Node
{
    private void CheckBots()
    {
        dynamic S = new GameSim();
        S.AddPlayer(1, "Me", 2);
        S.Action(1, "add_bot", 0);
        S.Action(1, "add_bot", 0);
        dynamic Bots = Gd.Filter(Gd.Values(S.Players), (Func<dynamic, dynamic>)((P) => P.Bot));
        this.Check(((Gd.Size(Bots) == 2) && (Gd.Map(Bots, (Func<dynamic, dynamic>)((P) => P.Role)) == new GArray(1, 0)) && (S.Leader() == 1)), "two bots fill the missing roles (Healer, Scout); the person stays leader");
        S.Action(1, "remove_bot", 0);
        S.Action(1, "add_bot", 0);
        this.Check((Gd.Size(S.Players) == 3), "bots can be removed and added in the lobby");
        S.Action(1, "start", 0);
        this.Run(S, 60);
        this.Check((Gd.Size(Gd.Filter(Gd.Values(S.Players), (Func<dynamic, dynamic>)((P) => (Gd.Truth(P.Bot) && (((P.Inv.Wood + P.Inv.Stone) + P.Inv.Herb) > 0))))) == 2), "bots gather resources by day");
        this.Check((S.Snapshot().Need == 1), "only people count for the day-skip vote");
        S.Action(1, "vote", 0);
        this.Run(S, 400, "night");
        // check idle guard positions before introducing enemies explicitly below
        S.NextWave = float.PositiveInfinity;
        this.Run(S, 8);
        int Dn = Gd.Int(Gd.At(S.Active, 0));
        dynamic AtPost = Gd.Filter(Gd.Values(S.Players), (Func<dynamic, dynamic>)((P) => (Gd.Truth(P.Bot) && !(Gd.Truth(GameSim.InsideBase(P.Pos))) && (P.Pos.DistanceTo(Gd.At(GameSim.DOOR_POS, Dn)) < (6 * 32)))));
        this.Check((Gd.Size(AtPost) == 2), "at night the bots hold the attacked door");
        dynamic Hb = Gd.At(Gd.Filter(Gd.Values(S.Players), (Func<dynamic, dynamic>)((P) => (Gd.Truth(P.Bot) && (P.Role == 1)))), 0);
        dynamic Sb = Gd.At(Gd.Filter(Gd.Values(S.Players), (Func<dynamic, dynamic>)((P) => (Gd.Truth(P.Bot) && (P.Role == 0)))), 0);
        Gd.Clear(S.Enemies);
        dynamic Gb = S.Spawn(GameSim.GRUNT, Dn, 0.0f);
        Gb.Pos = (Sb.Pos + ((Gd.At(GameSim.DOOR_DIR, Dn) * 2) * 32));
        float Ghp = Gd.Float(Gb.Hp);
        this.Run(S, 2.0f);
        this.Check(((Gb.Hp < Ghp) || !(Gd.Truth(Gd.Has(S.Enemies, Gb)))), "a bot fights an enemy near its post");
        Gd.Clear(S.Enemies);
        dynamic Me = Gd.At(S.Players, 1);
        Me.Pos = (Hb.Pos + Gd.MakeVector2(32, 0));
        Me.Hp = 20.0f;
        this.Run(S, 1.5f);
        this.Check(((Me.Hp >= 60.0f) && (Hb.Flasks < 3)), "the Healer bot throws a flask at a hurt teammate");
        Me.State = 1;
        Me.Bleed = 60.0f;
        Me.Pos = (Hb.Pos + ((Gd.At(GameSim.DOOR_DIR, Dn) * 5) * 32));
        this.Run(S, 14.0f);
        this.Check((Me.State == 0), "a bot walks over and revives a downed teammate");
        S.RemovePlayer(1);
        this.Check((Gd.Truth(Gd.IsEmpty(S.Players)) && (S.Phase == "lobby")), "when the last person leaves the bots go too");
    }
}
