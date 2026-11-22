using Godot;
using System;

// Dedicated harvesting cells, composed from each character's own body, clothes and skin palette.
public static partial class PixelPoses
{
    public static readonly string[] GATHER_KINDS = { "wood", "stone", "herb" };
    public const int GATHER_FRAMES = 6;
    public const float GATHER_FPS = 7.0f;

    public static Image[] Gather(string Kind, Image Body, string View, Rig R, int Legs, int Feet, int Cell, float ShadowRx)
    {
        float A = Facing(View);
        bool Behind = View.StartsWith("up");
        var Out = new Image[GATHER_FRAMES];
        for (int I = 0; I < Out.Length; I++)
        {
            var B = (Image)Body.Duplicate();
            B.FillRect(R.EraseMain, Clear);
            B.FillRect(R.EraseOff, Clear);
            if (R.Patch.Size != Vector2I.Zero) B.FillRect(R.Patch, R.PatchCol);
            int Drop = Kind == "herb" ? new[] { 1, 3, 5, 5, 3, 1 }[I] : 0;
            int LeanBy = new[] { 0, -1, 0, 2, 1, 0 }[I];
            var D = new Vector2I(Mathf.RoundToInt(Mathf.Cos(A) * LeanBy), Math.Max(0, Mathf.RoundToInt(Mathf.Sin(A) * LeanBy * K)));
            var Im = Embed(Lean(Kneel(B, Legs, Drop), Legs + Drop, D), Cell);
            int O = (Cell - Body.GetWidth()) / 2;
            var Shift = new Vector2(O + D.X, O + D.Y + Drop);
            var C = new Ctx { A = A, R = R, Sm = R.Main + Shift, So = R.Off + Shift, L = Blank(Cell, Cell), Fx = Blank(Cell, Cell) };
            if (Kind == "herb") GatherHerb(C, I);
            else GatherTool(C, I, Kind == "wood");
            Image Done = Blank(Cell, Cell);
            if (Behind) { Over(Done, C.L, true); Over(Done, Im, false); }
            else { Over(Done, Im, false); Over(Done, C.L, true); }
            Done = Finish(Done, Feet + O + 1, ShadowRx);
            if (Behind) Under(Done, C.Fx); else Over(Done, C.Fx, false);
            Out[I] = Done;
        }
        return Out;
    }

    static void GatherTool(Ctx C, int I, bool Axe)
    {
        // Raise, wind up, swing, contact, follow through, return to a relaxed grip.
        var Hands = new[] { new Vector3(1, -1, -1), new Vector3(-1, -1, 4), new Vector3(1, -1, 5),
            new Vector3(6, -1, -1), new Vector3(5, -1, -2), new Vector3(2, -1, -1) };
        float[] Pitch = Axe ? new[] { 35f, 100, 80, -12, -30, 20 } : new[] { 30f, 110, 90, -50, -60, 10 };
        var H = C.Hand(Hands[I]);
        var Wd = C.W(Dir(Pitch[I], 0));
        var Pp = Across(Wd);
        var Head = H + Wd * 8;
        Seg(C.L, H - Wd * 3, Head, 1, Wood);
        Seg(C.L, H - Wd * 2, H + Wd * 5, 1, WoodLit);
        if (Axe)
        {
            // Wedge blade on one side of the wooden haft, with a bright cutting edge.
            Block(C.L, Head, Pp, -1, 1, 1.2f, Iron, IronLit);
            for (int J = 1; J <= 4; J++)
                Seg(C.L, Head + Pp * J - Wd * (1 + J * 0.35f), Head + Pp * J + Wd * (1 + J * 0.35f), 1, J == 4 ? Steel : SteelDark);
        }
        else
        {
            // Slim curved pick: pointed ends read differently from the broad axe blade.
            Seg(C.L, Head - Pp * 3, Head + Pp * 3, 2, IronLit);
            Seg(C.L, Head - Pp * 3, Head - Pp * 5 + Wd * 2, 1, Steel);
            Seg(C.L, Head + Pp * 3, Head + Pp * 5 + Wd * 2, 1, Steel);
        }
        C.ArmTo(H - Wd * 2.5f, true);
        C.ArmTo(H, false);
        if (I == 3)
        {
            var Hit = Axe ? Head + Pp * 4 : Head + Pp * 5 + Wd * 2;
            if (!Axe) C.Star(Hit, 2);
            Color Chip = Axe ? WoodLit : StoneLit;
            Set(C.Fx, Hit + new Vector2(3, -3), Chip);
            Set(C.Fx, Hit + new Vector2(-3, -1), Chip);
        }
    }

    static void GatherHerb(Ctx C, int I)
    {
        var Hands = new[] { new Vector3(2, -1, -1), new Vector3(4, -1, -2), new Vector3(6, -1, -3),
            new Vector3(6, -1, -3), new Vector3(4, -1, 0), new Vector3(2, -1, 0) };
        var H = C.Hand(Hands[I]);
        C.ArmTo(C.Hand(new Vector3(3, -2.5f, -2), true), true);
        C.ArmTo(H, false);
        if (I >= 3 && I <= 4)
        {
            var Leaf = new Color("64A65B");
            Seg(C.L, H, H + new Vector2(1, -4), 1, new Color("356746"));
            Seg(C.L, H + new Vector2(1, -2), H + new Vector2(-2, -4), 2, Leaf);
            Seg(C.L, H + new Vector2(1, -3), H + new Vector2(3, -5), 2, Leaf);
        }
    }
}
