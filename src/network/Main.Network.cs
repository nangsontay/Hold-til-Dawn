using Godot;
using Environment = Godot.Environment;
using FileAccess = Godot.FileAccess;
using System;
using System.Threading.Tasks;
using HoldTilDawn;

// Owner: Mai Thien Ngon. ENet host/join, RPCs and the server tick. The network only forwards inputs, action strings
// and snapshots: it calls GameSim through AddPlayer / RemovePlayer / Input / Action / Tick / Snapshot and never applies rules.
public partial class Main : Node2D
{
    // Client + network glue: menu, lobby, input, drawing and hit feedback.
    // Server rules live in GameSim.cs (GameSim). Movement is predicted locally and validated by the server.
    // Look: dark top-down cartoon world, thick outlines, neon VFX with 2D glow, floating damage numbers.
    // Dedicated server: godot --headless --path . -- --server
    public static readonly dynamic PORT = 7777;

    // --- network ------------------------------------------------------------
    public void Host(dynamic Play)
    {
        dynamic Peer = Gd.Wrap(new ENetMultiplayerPeer());
        if ((Peer.CreateServer(PORT, 5) != 0))
        {
            Status.Text = Gd.Format("UDP port %d is busy", PORT);
            return;
        }
        Gd.Wrap(Multiplayer).MultiplayerPeer = Peer;
        Sim = new GameSim();
        if (Gd.Truth(Play))
        {
            MyId = 1;
            Sim.AddPlayer(1, this.GetPlayerName(), RoleOpt.Selected);
        }
        else
        {
            GD.Print(Gd.Format("Hold 'til Dawn dedicated server on UDP %d", PORT));
        }
    }

    public void Join()
    {
        dynamic Peer = Gd.Wrap(new ENetMultiplayerPeer());
        if ((Peer.CreateClient(Gd.StripEdges(IpEdit.Text), PORT) != 0))
        {
            Status.Text = "Bad address";
            return;
        }
        Gd.Wrap(Multiplayer).MultiplayerPeer = Peer;
        Status.Text = "Connecting...";
    }

    public void OnConnected()
    {
        MyId = Gd.Wrap(Multiplayer).GetUniqueId();
        RpcId(1, "CJoin", this.GetPlayerName(), RoleOpt.Selected);
    }

    public void OnPeerLeft(dynamic Id)
    {
        if (Gd.Truth(Sim))
        {
            Sim.RemovePlayer(Id);
        }
    }

    public void BackToMenu(dynamic Why)
    {
        Gd.Wrap(Multiplayer).MultiplayerPeer = null;
        Sim = null;
        Snap = new GDict {  };
        MyId = 0;
        Gd.Clear(PendingInputs);
        InputSequence = 0;
        Status.Text = Why;
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void CJoin(string Pname, int Role)
    {
        if (Gd.Truth(Sim))
        {
            Sim.AddPlayer(Gd.Wrap(Multiplayer).GetRemoteSenderId(), Pname, Role);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered)]
    public void CInput(Vector2 Pos, float Aim, bool Atk, bool Inter, bool Ent, int Tp, int Sequence)
    {
        if (Gd.Truth(Sim))
        {
            Sim.Input(Gd.Wrap(Multiplayer).GetRemoteSenderId(), Pos, Aim, Atk, Inter, Ent, Tp, Sequence);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void CAction(string Kind, Variant Arg)
    {
        dynamic ActionArg = Gd.FromVariant(Arg);
        if (Gd.Truth(Sim))
        {
            Sim.Action(Gd.Wrap(Multiplayer).GetRemoteSenderId(), Kind, ActionArg);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered)]
    public void SSnap(Godot.Collections.Dictionary D)
    {
        dynamic SnapshotData = Gd.FromVariant(D);
        this.OnSnap(Gd.FromVariant(D));
    }

    public void Act(dynamic Kind, dynamic Arg = null)
    {
        Arg ??= 0;
        if (Gd.Truth(Sim))
        {
            Sim.Action(MyId, Kind, Arg);
        }
        else if ((MyId != 0))
        {
            RpcId(1, "CAction", Kind, Arg);
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        dynamic Dt = (float)delta;
        if (Gd.Truth(Sim))
        {
            Sim.Tick(Dt);
            Frame += 1;
            if (((Frame % 3) == 0))
            {
                dynamic D = Sim.Snapshot();
                if (!(Gd.Truth(Gd.IsEmpty(Gd.Wrap(Multiplayer).GetPeers()))))
                {
                    Rpc("SSnap", Gd.ToVariant(D));
                }
                if ((MyId == 1))
                {
                    this.OnSnap(Gd.FromVariant(D));
                }
            }
        }
        if (((MyId != 0) && Gd.Truth(Gd.Has(Snap, "ps"))))
        {
            this.Move(Dt);
        }
    }
}
