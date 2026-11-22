using Godot;
using System;
using HoldTilDawn;

// Expedition chests have no objective beams or extra lights. Their hidden loot is server-owned.
public partial class Main : Node2D
{
    static readonly Color SUPPLY_COL = new Color("FFD45C");
    // [id, position, empty, landmark, unlocked, lock progress, lid progress]; no hidden contents.
    GArray SupplyRows() => (Gd.Get(Snap, "sup", null) as GArray) ?? new GArray();
    void ExplorationItems(GArray Items, Rect2 View)
    {
        foreach (GArray C in SupplyRows())
        {
            Vector2 At = (Vector2)C[1];
            if (View.Grow(60).HasPoint(At) && !HiddenOutsideInterior(At)) Items.Add(new GArray(At.Y, 21, C));
        }
        var Rescue = Gd.Get(Snap, "rescue", null) as GArray;
        if (Rescue != null && Gd.Truth(Rescue[1]))
            Items.Add(new GArray(GameSim.WORKBENCH_POS.Y + 1, 22));
    }
    void DrawExplorationChest(GArray C)
    {
        Vector2 At = (Vector2)C[1]; bool Open = Gd.Truth(C[4]), Empty = Gd.Truth(C[2]);
        float Unlock = Gd.Float(C[5]), Lid = Open ? Mathf.SmoothStep(0,1,Gd.Float(C[6])) : 0;
        DrawSetTransform(At);
        DrawRect(new Rect2(-18,-3,36,9), new Color(Ep("232820"), .4f));
        DrawRect(new Rect2(-17,-19,34,24), Ep("222D2C"));
        DrawRect(new Rect2(-15,-17,30,20), Empty ? Ep("555B55") : Ep("6D754F"));
        DrawRect(new Rect2(-12,-16,4,19), Ep("AFAF91")); DrawRect(new Rect2(8,-16,4,19), Ep("AFAF91"));
        DrawRect(new Rect2(-15,-5,30,2), Ep("414D3C"));
        if (Open)
        {
            float Top = Mathf.Round(Mathf.Lerp(-24,-36,Lid));
            DrawRect(new Rect2(-17,Top,34,Mathf.Lerp(9,19,Lid)), Ep("222D2C"));
            DrawRect(new Rect2(-14,Top+3,28,Mathf.Lerp(3,13,Lid)), Ep("53624D"));
            DrawRect(new Rect2(-12,Top+4,24,Mathf.Lerp(1,9,Lid)), Ep("2D3831"));
            DrawRect(new Rect2(-14,-17,28,6), Ep("172721"));
            if (!Empty) { DrawRect(new Rect2(-10,-16,8,4), Ep("D2BD79")); DrawRect(new Rect2(3,-16,6,3),Ep("8BA7A0")); }
            // A broken latch and green corner remain visible after the opening animation finishes.
            DrawRect(new Rect2(7,-5,5,3),Ep("89C793"));
            DrawLine(new Vector2(-4,-12),new Vector2(-8,-7),Ep("D7B76E"),2);
        }
        else
        {
            DrawRect(new Rect2(-18,-24,36,9), Ep("303D32"));
            DrawRect(new Rect2(-16,-23,32,5), Ep("88916A"));
            DrawRect(new Rect2(-3,-20,6,10), Ep("D7B76E"));
            DrawRect(new Rect2(-1,-17,2,3), Ep("4D4936"));
            DrawArc(new Vector2(0,-22),4,Mathf.Pi,Mathf.Tau,8,Ep("E7D19B"),2);
            if (Unlock > 0)
            {
                DrawArc(new Vector2(0,-16),9,-Mathf.Pi/2,-Mathf.Pi/2+Mathf.Tau*Unlock,24,SUPPLY_COL,2);
                DrawRect(new Rect2(-25,-43,50,5),Ep("202826"));
                DrawRect(new Rect2(-25,-43,50*Unlock,5),SUPPLY_COL);
            }
        }
        DrawSetTransform(Vector2.Zero);
        if (((Vector2)LocalPos).DistanceSquaredTo(At) <= 56 * 56)
            Text(Gd.Wrap(this), At + new Vector2(-100,Unlock > 0 ? -51 : -45), Open ? (Empty ? "EMPTY - E: STORAGE" : "UNLOCKED - E: STORAGE")
                : Unlock > 0 ? $"UNLOCKING {Unlock*100:F0}% - HOLD E" : "LOCKED - KEY + HOLD E (2s)", 10,
                Open ? Colors.LightGreen : SUPPLY_COL, 1, 200, true);
    }
    public static void DrawSupplyIcon(CanvasItem C, int Kind, Vector2 At, float S, float A = 1)
    {
        C.DrawRect(new Rect2(At + new Vector2(-8,-6)*S, new Vector2(16,12)*S), new Color(.13f,.2f,.2f,A));
        C.DrawRect(new Rect2(At + new Vector2(-6,-4)*S, new Vector2(7,4)*S), new Color(.55f,.8f,.55f,A));
        C.DrawRect(new Rect2(At + new Vector2(5,-12)*S, new Vector2(1,8)*S), new Color(.8f,.82f,.72f,A));
        C.DrawCircle(At + new Vector2(4,2)*S, 2*S, new Color(.9f,.73f,.37f,A));
    }
    void DrawInstalledRadio()
    {
        Vector2 At = (Vector2)GameSim.WORKBENCH_POS - new Vector2(0,24);
        DrawSupplyIcon(this, 0, At, 2);
        DrawCircle(At + new Vector2(-8,-3), 2, Ep("8FFF9C"));
    }
    public static void DrawComponentIcon(CanvasItem C, int Kind, Vector2 At, float Scale)
    {
        void Box(float X, float Y, float W, float H, string Hex) =>
            C.DrawRect(new Rect2(At + new Vector2(X, Y) * Scale, new Vector2(W, H) * Scale), new Color(Hex));
        if (Kind == 0)
        {
            foreach (int X in new[] { -5, 1 })
            {
                Box(X - 1, -5, 6, 11, "171D22"); Box(X, -4, 4, 9, "AAB7B6");
                Box(X + 1, -6, 2, 2, "D8DFD5"); Box(X, 1, 4, 3, "507856");
            }
        }
        else if (Kind == 1)
        {
            Box(-5, 1, 10, 5, "3B4D51"); Box(-3, 2, 6, 3, "C3CCB2");
            Box(-1, -7, 2, 9, "B9C6C3"); Box(-6, -5, 12, 2, "9CADA8");
            Box(-4, -8, 8, 2, "D5DED0");
        }
        else
        {
            Box(-6, -5, 12, 10, "171D22"); Box(-5, -4, 10, 8, "386C4C");
            Box(-2, -2, 4, 4, "222D36"); Box(-4, -3, 2, 1, "C2A45D");
            Box(2, 1, 2, 1, "C2A45D");
            for (int X = -4; X <= 3; X += 2) Box(X, 4, 1, 2, "C2A45D");
        }
    }

    public void DrawSupplyRow(CanvasItem C, float Right, float Y)
    {
        var Rescue = Gd.Get(Snap, "rescue", null) as GArray;
        if (Rescue == null) return;
        bool Built = Gd.Truth(Rescue[1]);
        var Box = new Rect2(Right - 232, Y + 12, 232, 66);
        dynamic Cw = Gd.Wrap(C);
        C.DrawRect(Box, Ep("192027")); C.DrawRect(Box, SUPPLY_COL, false, 1);
        Text(Cw, Box.Position + new Vector2(6,13), Built ? "LOCATOR RADIO: ONLINE" : "LOCATOR RADIO: BEFORE NIGHT 5", 10, SUPPLY_COL);
        for (int I = 0; I < 3; I++)
        {
            bool Found = Gd.Int(Gd.At(Rescue[0], I)) > 0;
            var Slot = new Rect2(Box.Position + new Vector2(6 + I * 74,19), new Vector2(70,22));
            C.DrawRect(Slot, Found ? Ep("285637") : Ep("30363D"));
            DrawComponentIcon(C, I, Slot.Position + new Vector2(11,11), 1);
            Text(Cw, Slot.Position + new Vector2(24,15), Found ? "FOUND" : "?", 10, Found ? Colors.LightGreen : SUPPLY_COL);
        }
        float Progress = Gd.Float(Rescue[2]);
        Text(Cw, Box.Position + new Vector2(6,57), Built ? "Defend a landing pad on night 5" : Progress > 0 ? "ASSEMBLING " + (int)(Progress*100) + "%"
            : "Find 3 parts; hold E at the workbench", 10, Colors.White);
    }
}
