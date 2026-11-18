using Godot;
using HoldTilDawn;

/// <summary>Runs the actual Main RPC protocol between two local Godot processes.</summary>
public partial class NetworkCheck : Main
{
    private bool server;
    private bool started;
    private bool inputSent;
    private double elapsed;

    public override void _Ready()
    {
        Multiplayer.PeerDisconnected += id => OnPeerLeft((int)id);
        Multiplayer.ConnectedToServer += OnConnected;
        Multiplayer.ConnectionFailed += () => Fail("Connection failed");
        Multiplayer.ServerDisconnected += () => { if (!started) Fail("Server disconnected"); };
        server = OS.GetCmdlineUserArgs().Contains("--network-server");
        var peer = new ENetMultiplayerPeer();
        var error = server ? peer.CreateServer(17777, 5) : peer.CreateClient("127.0.0.1", 17777);
        if (error != Error.Ok) { Fail($"Could not open test peer: {error}"); return; }
        Multiplayer.MultiplayerPeer = peer;
        if (server) { Sim = new GameSim(); return; }
        NameEdit.Text = "CSharpNetworkCheck";
        IpEdit.Text = "127.0.0.1";
        RoleOpt.AddItem("Scout");
        RoleOpt.AddItem("Healer");
        RoleOpt.AddItem("Engineer");
        RoleOpt.Selected = 2;
    }

    public override void _PhysicsProcess(double delta)
    {
        try { base._PhysicsProcess(delta); }
        catch (Exception error) { Fail(error.ToString()); }
    }

    public override void _Process(double delta)
    {
        elapsed += delta;
        if (elapsed > 25) { Fail("Network check timed out"); return; }
        if (server || !Gd.Has(Snap, "ph")) return;
        if (Snap.Ph == "lobby" && !started)
        {
            started = true;
            Act("start");
        }
        if (Snap.Ph != "day" || !Gd.Has(Snap.Ps, MyId)) return;
        if (!inputSent)
        {
            inputSent = true;
            Act("craft", 1); // Also exercises the Variant argument in CAction.
        }
        dynamic player = Snap.Ps[MyId];
        if (player[24] <= 0) return;
        if (player[1] != 2 || Gd.Size(Snap.Nodes) == 0) { Fail("Invalid snapshot payload"); return; }
        GD.Print("NETWORK ALL OK: join, action, snapshot and input acknowledgement");
        GetTree().Quit(0);
    }

    public override void _Draw() { }

    private void Fail(string message)
    {
        GD.PushError(message);
        GetTree().Quit(1);
    }
}
