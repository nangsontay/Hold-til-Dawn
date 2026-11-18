using Godot;
using Environment = Godot.Environment;
using FileAccess = Godot.FileAccess;
using System;
using System.Threading.Tasks;
using HoldTilDawn;

// Owner: Mai Thien Ngon. Pause overlay (Esc, section 6.3) and the result screen (section 2.5).
// A solo match pauses while the overlay is open; a network match will keep running (FTR-04).
public partial class Main : Node2D
{
    public dynamic Paused = false;

    public static readonly GArray CONTROLS = new GArray(
        "WASD move   mouse aim   LMB attack (hold)",
        "E (hold) gather, repair, revive, pick up",
        "1-4 / wheel hotbar   Tab inventory   G give / drop",
        "Q bandage   RMB flask (Healer)   C workbench",
        "B build (Engineer)   F (hold) enter base at night",
        "V skip-day vote   Ctrl + wheel zoom");

    public Rect2 PauseButton(dynamic Vs, int I)
    {
        return Gd.MakeRect2(((Vs.X / 2) - 110), (((Vs.Y / 2) + 40) + (I * 46)), 220, 36);
    }

    public void DrawPause(dynamic C, dynamic Vs)
    {
        if (!Gd.Truth(Paused))
        {
            return;
        }
        C.DrawRect(Gd.MakeRect2(0, 0, Vs.X, Vs.Y), Gd.MakeColor(0, 0, 0, 0.55f));
        Rect2 Box = Gd.MakeRect2(((Vs.X / 2) - 220), ((Vs.Y / 2) - 170), 440, 320);
        this.Panel(C, Box);
        this.Text(C, Gd.MakeVector2(Box.Position.X, (Box.Position.Y + 34)), "PAUSED", 26, GOLD, 1, Box.Size.X, true);
        for (int I = 0; I < CONTROLS.Count; I++)
        {
            this.Text(C, Gd.MakeVector2(Box.Position.X, ((Box.Position.Y + 64) + (I * 18))), Gd.At(CONTROLS, I), 11, Gd.MakeColor(1, 1, 1, 0.75f), 1, Box.Size.X);
        }
        Vector2 Mouse = (Vector2)(Hud.GetLocalMousePosition());
        dynamic Labels = new GArray("RESUME  (Esc)", "LEAVE MATCH");
        for (int I = 0; I < 2; I++)
        {
            Rect2 B = this.PauseButton(Vs, I);
            C.DrawRect(B, (B.HasPoint(Mouse) ? (dynamic)(Gd.MakeColor(0.95f, 0.71f, 0.27f, 0.35f)) : Gd.MakeColor(1, 1, 1, 0.08f)));
            C.DrawRect(B, GOLD, false, 1.5f);
            this.Text(C, Gd.MakeVector2(B.Position.X, (B.Position.Y + 24)), Gd.At(Labels, I), 14, Colors.White, 1, B.Size.X, true);
        }
    }

    public bool PauseMouse(dynamic Ev)
    {
        if ((!Gd.Truth(Paused) || (Ev.ButtonIndex != 1)))
        {
            return false;
        }
        if (Gd.Truth(Ev.Pressed))
        {
            Vector2 Mouse = (Vector2)(Hud.GetLocalMousePosition());
            if (this.PauseButton(Hud.Size, 0).HasPoint(Mouse))
            {
                Paused = false;
            }
            else if (this.PauseButton(Hud.Size, 1).HasPoint(Mouse))
            {
                Paused = false;
                this.BackToMenu("Left the match");
            }
        }
        return true;
    }

    // Esc closes an open panel first, then toggles the pause overlay.
    public void EscapeKey()
    {
        if ((Gd.Truth(BagOpen) || Gd.Truth(CraftOpen) || Gd.Truth(BuildOpen)))
        {
            BagOpen = false;
            CraftOpen = false;
            this.CloseBuild();
            DragFrom = null;
            return;
        }
        if ((Gd.Get(Snap, "ph", "") == "result"))
        {
            return;
        }
        Paused = !(Gd.Truth(Paused));
    }

    // --- result screen ------------------------------------------------------------
    public Rect2 ResultButton(dynamic Vs)
    {
        return Gd.MakeRect2(((Vs.X / 2) - 110), ((Vs.Y / 2) + 150), 220, 38);
    }

    public void DrawResult(dynamic C, dynamic Vs)
    {
        dynamic R = Gd.Get(Snap, "result", null);
        if (((Gd.Get(Snap, "ph", "") != "result") || (R == null)))
        {
            return;
        }
        bool Win = Gd.Truth(Gd.At(R, 0));
        C.DrawRect(Gd.MakeRect2(0, 0, Vs.X, Vs.Y), Gd.MakeColor(0, 0, 0, 0.6f));
        Rect2 Box = Gd.MakeRect2(((Vs.X / 2) - 300), ((Vs.Y / 2) - 200), 600, 410);
        this.Panel(C, Box);
        this.Text(C, Gd.MakeVector2(Box.Position.X, (Box.Position.Y + 44)), (Win ? "MISSION COMPLETE" : "BEACON LOST"), 32, (Win ? GOLD : NEON_RED), 1, Box.Size.X, true);
        this.Text(C, Gd.MakeVector2(Box.Position.X, (Box.Position.Y + 72)), Gd.At(R, 1), 14, Colors.White, 1, Box.Size.X);
        this.Text(C, Gd.MakeVector2(Box.Position.X, (Box.Position.Y + 92)), Gd.Format("Night reached: %d / 5", Gd.At(R, 2)), 12, Gd.MakeColor(1, 1, 1, 0.7f), 1, Box.Size.X);
        dynamic Cols = new GArray(new GArray("PLAYER", 0.0f), new GArray("ROLE", 190.0f), new GArray("KILLS", 290.0f), new GArray("REVIVES", 360.0f), new GArray("DAMAGE", 440.0f), new GArray("TAKEN", 515.0f));
        float Y = (Box.Position.Y + 128);
        foreach (dynamic Col in Gd.Iter(Cols))
        {
            this.Text(C, Gd.MakeVector2(((Box.Position.X + 24) + Gd.At(Col, 1)), Y), Gd.At(Col, 0), 10, GOLD, 0, (-1.0f), true);
        }
        foreach (dynamic Id in Gd.Iter(Snap.Ps))
        {
            dynamic P = Gd.At(Snap.Ps, Id);
            Y += 22;
            dynamic Vals = new GArray(Gd.At(P, 0), Gd.At(GameSim.ROLES, Gd.At(P, 1)), Gd.Str(Gd.At(P, 17)), Gd.Str(Gd.At(P, 28)), Gd.Str(Gd.Int(Gd.At(P, 18))), Gd.Str(Gd.Int(Gd.At(P, 29))));
            for (int I = 0; I < 6; I++)
            {
                this.Text(C, Gd.MakeVector2(((Box.Position.X + 24) + Gd.At(Gd.At(Cols, I), 1)), Y), Gd.At(Vals, I), 12, ((I == 1) ? (dynamic)(Gd.At(ROLE_COL, Gd.At(P, 1))) : Colors.White));
            }
        }
        Rect2 B = this.ResultButton(Vs);
        bool Leader = (Gd.Get(Snap, "leader", 0) == MyId);
        Vector2 Mouse = (Vector2)(Hud.GetLocalMousePosition());
        C.DrawRect(B, (B.HasPoint(Mouse) ? (dynamic)(Gd.MakeColor(0.95f, 0.71f, 0.27f, 0.35f)) : Gd.MakeColor(1, 1, 1, 0.08f)));
        C.DrawRect(B, GOLD, false, 1.5f);
        this.Text(C, Gd.MakeVector2(B.Position.X, (B.Position.Y + 25)), "BACK TO LOBBY", 14, Colors.White, 1, B.Size.X, true);
    }

    public bool ResultMouse(dynamic Ev)
    {
        if (((Gd.Get(Snap, "ph", "") != "result") || (Ev.ButtonIndex != 1)))
        {
            return false;
        }
        if ((Gd.Truth(Ev.Pressed) && this.ResultButton(Hud.Size).HasPoint((Vector2)(Hud.GetLocalMousePosition()))))
        {
            this.Act("lobby");
        }
        return true;
    }
}
