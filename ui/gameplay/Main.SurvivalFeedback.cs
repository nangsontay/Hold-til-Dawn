using Godot;
using System;
using HoldTilDawn;

public partial class Main
{
    public static float LowHealthStrength(float Hp, float MaxHp) => Mathf.Clamp((0.6f - Hp / Math.Max(1, MaxHp)) / 0.6f, 0, 1);
    void DrawLowHealth(CanvasItem C, Vector2 Size)
    {
        var Me = Gd.Get(Gd.Get(Snap, "ps", new GDict()), MyId) as GArray;
        if (Me == null || Gd.Int(Me[5]) == 2 || Gd.Get(Snap, "ph", "") == "result") return;
        float Strength = LowHealthStrength(Gd.Float(Me[3]), Gd.Float(Me[4]));
        if (Gd.Int(Me[5]) == 1) Strength = 1;
        if (Strength <= 0) return;
        // Six nested bands, constant cost. Steady warning with a slow heartbeat only below 25% HP.
        float Beat = Gd.Float(Me[3]) < Gd.Float(Me[4]) * .25f ? .88f + .12f * Mathf.Sin((float)Clock * 5) : 1;
        for (int I = 5; I >= 0; I--)
        {
            float Inset = I * (2 + 4 * Strength);
            var Box = new Rect2(Vector2.One * Inset, Size - Vector2.One * Inset * 2);
            C.DrawRect(Box, new Color(.83f, .035f, .025f, Strength * Beat * (0.12f + .07f * (5 - I))), false, 3 + Strength * 3);
        }
    }
    int CraftHaveLocal(dynamic Me, string Id)
    {
        int Kind = Array.IndexOf(GameSim.COMPONENT_IDS, Id);
        var Rescue = Gd.Get(Snap, "rescue", null) as GArray;
        return Kind >= 0 ? (Rescue == null ? 0 : Gd.Int(Gd.At(Rescue[0], Kind))) : Carried(Me, Id);
    }
    void DrawConstructionGround()
    {
        foreach (GArray Tw in (GArray)Gd.Get(Snap, "tw", new GArray())) DrawDirtFoundation((Vector2)Tw[1], 1);
        foreach (GArray Job in (GArray)Gd.Get(Snap, "construction", new GArray()))
        {
            Vector2 At = (Vector2)Job[0]; float Progress = Gd.Float(Job[1]);
            DrawDirtFoundation(At, Progress);
            DrawRect(new Rect2(At + new Vector2(-24, -38), new Vector2(48, 5)), new Color("202526"));
            DrawRect(new Rect2(At + new Vector2(-24, -38), new Vector2(48 * Progress, 5)), new Color("E6B257"));
            DrawArc(At, 22, 0, Mathf.Tau * Progress, 24, new Color("E6B257"), 2);
        }
    }
    void DrawRescueChecklist(dynamic C, Rect2 Panel)
    {
        Vector2 At = Panel.Position + new Vector2(20, 260);
        Text(C, At, "DAYTIME EXPEDITION", 12, GOLD, 0, -1, true);
        Text(C, At + new Vector2(0, 20), "M: gold markers show the missing components.", 11, Colors.White);
        Text(C, At + new Vector2(0, 36), "Collected components are shared by the team.", 11, Colors.White);
        var Rescue = Gd.Get(Snap, "rescue", null) as GArray;
        if (Rescue == null) return;
        for (int I = 0; I < 3; I++)
        {
            bool Done = Gd.Truth(Gd.At(Rescue[1], I)), Found = Gd.Int(Gd.At(Rescue[0], I)) > 0;
            string Where = "Explore a marked landmark by day";
            foreach (GArray S in SupplyRows())
                if (Gd.Int(S[0]) == I && S.Count > 3) Where = IslandMap.ById(Gd.Int(S[3]))?.Name ?? Where;
            Text(C, At + new Vector2(0, 65 + I * 38), GameSim.SUPPLY_NAMES[I] + ": " + (Done ? "INSTALLED" : Found ? "READY TO CRAFT" : Where), 11,
                Done ? Colors.LightGreen : GOLD);
        }
    }
    void DrawDirtFoundation(Vector2 At, float Progress)
    {
        DrawSetTransform(At, 0, new Vector2(1, .65f));
        DrawCircle(Vector2.Zero, 33, new Color("594731"));
        DrawCircle(Vector2.Zero, 29, new Color("927448"));
        for (int I = 0; I < 12; I++)
        {
            Vector2 Q = Vector2.FromAngle(I * 2.4f) * (10 + I % 4 * 4);
            DrawRect(new Rect2(Q.Round(), new Vector2(4, 2)), new Color(I % 2 == 0 ? "AF8E58" : "755638"));
        }
        DrawSetTransform(Vector2.Zero);
    }
}
