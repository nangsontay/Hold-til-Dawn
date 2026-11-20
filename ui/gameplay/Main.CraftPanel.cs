using Godot;
using System;
using System.Collections.Generic;
using HoldTilDawn;

// Owner: Vu Duc Viet Anh. The workbench tab (MEC-10, BAL-19, recipe table in GDD section 3.2), opened with C.
// It takes the middle of the screen like the bag: on the left every recipe as a card with its icon, grouped by
// tier; on the right the picked recipe in detail (what it does, its numbers) and how it is made, drawn as
// ingredients + ingredients -> result, the steps to follow and a Craft button. A number key still crafts at once.
// The server decides (GameSim.CanCraft); the reasons shown here only mirror its rules.
public partial class Main : Node2D
{
    // the recipe shown on the right
    public int CraftSel;

    static readonly Dictionary<string, string> RES_FROM = new()
    {
        ["wood"] = "trees",
        ["stone"] = "rocks",
        ["herb"] = "herb bushes",
        ["metal"] = "scrap piles",
    };
    static readonly string[] TIER_TITLES = { "BASIC", "STONE TIER  -  after night 1", "METAL TIER  -  after the first boss" };
    const float CARD_H = 92.0f;
    const float CARD_GAP = 8.0f;
    const float BOX = 52.0f;
    // the title line
    const float HEAD_H = 48.0f;

    // --- layout --------------------------------------------------------------
    public Rect2 CraftPanelRect(Vector2 Vs)
    {
        // nearly the whole screen (the HUD is often only 540 px high, scaled up)
        float W = Mathf.Min((Vs.X - 40.0f), 940.0f);
        float H = Mathf.Min((Vs.Y - 24.0f), 540.0f);
        return new Rect2(Mathf.Round(((Vs.X - W) / 2)), Mathf.Round(((Vs.Y - H) / 2)), W, H);
    }

    float CraftLeftW(Rect2 P)
    {
        return Mathf.Floor((P.Size.X * 0.54f));
    }

    public Rect2 CraftCard(Vector2 Vs, int I)
    {
        Rect2 P = this.CraftPanelRect(Vs);
        float Lw = this.CraftLeftW(P);
        float Cw = Mathf.Floor(((Lw - (3 * CARD_GAP)) / 4));
        dynamic R = Gd.At(GameSim.RECIPES, I);
        int Tier = Gd.Int(Gd.At(R, 2));
        int K = 0;
        for (int J = 0; J < I; J++)
        {
            if (Gd.Int(Gd.At(Gd.At(GameSim.RECIPES, J), 2)) == Tier)
            {
                K++;
            }
        }
        float Y = (this.CraftRowTop(P, Tier) + 18);
        return new Rect2((P.Position.X + 20 + (K * (Cw + CARD_GAP))), Y, Cw, CARD_H);
    }

    // top of a tier's row: its title, then its cards 18 px lower
    float CraftRowTop(Rect2 P, int Tier)
    {
        return ((P.Position.Y + HEAD_H) + (Tier * (CARD_H + 28)));
    }

    Rect2 CraftDetail(Rect2 P)
    {
        float X = (P.Position.X + 20 + this.CraftLeftW(P) + 24);
        return new Rect2(X, (P.Position.Y + HEAD_H + 4), ((P.End.X - 20) - X), ((P.Size.Y - HEAD_H) - 4 - 16));
    }

    public Rect2 CraftButton(Vector2 Vs)
    {
        Rect2 D = this.CraftDetail(this.CraftPanelRect(Vs));
        return new Rect2(D.Position.X, (D.End.Y - 38), D.Size.X, 38);
    }

    // --- what the recipe is ----------------------------------------------------
    // 8 -> "8", 1.5 -> "1.5"
    static string Num(dynamic V)
    {
        float F = Gd.Float(V);
        return ((F == Mathf.Floor(F)) ? ((int)F).ToString() : F.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture));
    }

    static string KeyOf(int I)
    {
        return Gd.Str(((I + 1) % 10));
    }

    float CraftSeconds(dynamic R)
    {
        return ((Gd.At(R, 3) == "base") ? (Gd.Float(Gd.At(R, 4)) / 20.0f) : GameSim.CRAFT_TIME);
    }

    string CraftTag(string Id)
    {
        switch (Id)
        {
            case "revive":
                return "TEAM";
            case "base":
                return "BASE  -  ENGINEER ONLY";
            case "bandage":
                return "HEALING";
            case "arrow":
                return "AMMO";
        }
        string Kind = GameSim.KindOf(Id);
        if (Kind == "weapon")
        {
            return (Gd.Truth(Gd.At(GameSim.WEAPON_STATS[Id], 3)) ? "WEAPON  -  RANGED" : "WEAPON  -  MELEE");
        }
        return Kind.ToUpper();
    }

    // a few short lines on what it does
    List<string> CraftDesc(string Id)
    {
        var Out = new List<string>();
        if (Gd.Truth(GameSim.WEAPON_STATS.Has(Id)))
        {
            dynamic S = GameSim.WEAPON_STATS[Id];
            bool Ranged = Gd.Truth(Gd.At(S, 3));
            Out.Add((Id == "stone_spear") ? "A long thrust that keeps them at reach." : ((Id == "metal_blade") ? "A wide, heavy slash. Best melee weapon." : ((Id == "bow") ? "Shoots toward the cursor. Holds its arrows (up to 30)." : "Shoots toward the cursor; needs no arrows.")));
            Out.Add(("Damage " + Num(Gd.At(S, 0)) + "   -   " + Num(Gd.At(S, 1)) + (Ranged ? " shot" : " hit") + ((Gd.Float(Gd.At(S, 1)) == 1.0f) ? "" : "s") + "/s"));
            Out.Add(((Ranged ? "Range " : "Reach ") + Num(Gd.At(S, 2)) + " tiles"));
            return Out;
        }
        int Tier = 0;
        for (int K = 1; K < Gd.Size(GameSim.ARMOUR_ITEMS); K++)
        {
            if (Gd.Str(Gd.At(GameSim.ARMOUR_ITEMS, K)) == Id)
            {
                Tier = K;
            }
        }
        if (Tier > 0)
        {
            Out.Add(Gd.Format("Takes %d%% off every hit you take.", Gd.Int(Mathf.Round((Gd.Float(Gd.At(GameSim.ARMOUR, Tier)) * 100.0f)))));
            Out.Add("Worn by itself when it is your best.");
            return Out;
        }
        switch (Id)
        {
            case "arrow":
                Out.Add("Go straight into your bow (up to 30),");
                Out.Add("so they need no slot. 5 per craft.");
                break;
            case "bandage":
                Out.Add("Q: heals 30 HP after a short wait");
                Out.Add("(Healer 45). Attacking stops it.");
                break;
            case "revive":
                Out.Add("Brings a dead teammate back with 50% HP");
                Out.Add("at the door nearest to where they fell.");
                Out.Add("Used the moment it is made.");
                break;
            case "base":
                Out.Add("+100 HP for the generator (max 1000).");
                Out.Add("By day only.");
                break;
        }
        return Out;
    }

    // why this recipe cannot be crafted right now ("" = it can), the same rules as GameSim.CanCraft
    string CraftBlock(dynamic Me, int I)
    {
        dynamic R = Gd.At(GameSim.RECIPES, I);
        string Id = Gd.Str(Gd.At(R, 3));
        int Tier = Gd.Int(Gd.At(R, 2));
        if (Tier > Gd.Int(Gd.Get(Snap, "unlock", 0)))
        {
            return ((Tier == 1) ? "Unlocks after night 1" : "Unlocks after the first boss");
        }
        if (Gd.Truth(Gd.At(R, 5)) && (Gd.At(Me, 1) != 2))
        {
            return "Only an Engineer can do this";
        }
        if (Id == "revive")
        {
            bool Dead = false;
            foreach (dynamic Pid in Gd.Iter(Snap.Ps))
            {
                Dead = (Dead || (Gd.At(Gd.At(Snap.Ps, Pid), 5) == 2));
            }
            if (!Dead)
            {
                return "No dead teammate to revive";
            }
        }
        if (Id == "base")
        {
            if (Gd.Get(Snap, "ph", "") == "night")
            {
                return "The generator can only be repaired by day";
            }
            if (Gd.Float(Gd.Get(Snap, "base", 0.0f)) >= Gd.Float(GameSim.BASE_HP))
            {
                return "The generator is at full HP";
            }
        }
        foreach (dynamic Res in Gd.Iter(Gd.At(R, 1)))
        {
            int Need = Gd.Int(Gd.At(Gd.At(R, 1), Res));
            int Have = Main.Carried(Me, Res);
            if (Have < Need)
            {
                return Gd.Format("Need %d more %s", new GArray((Need - Have), Res));
            }
        }
        if (!this.AtBenchLocal(Me))
        {
            return "Walk to the workbench to craft";
        }
        int Busy = Gd.Int(Gd.At(Me, 26));
        if ((Busy >= 0) && (Busy != I))
        {
            return Gd.Format("Already making %s", Gd.At(Gd.At(GameSim.RECIPES, Busy), 0));
        }
        return "";
    }

    // --- drawing ---------------------------------------------------------------
    public void DrawCraftPanel(dynamic C, dynamic Vs)
    {
        if (!Gd.Truth(CraftOpen))
        {
            return;
        }
        dynamic Me = Gd.Get(Snap.Ps, MyId);
        if (Me == null)
        {
            return;
        }
        Vector2 V = (Vector2)Vs;
        CraftSel = Mathf.Clamp(CraftSel, 0, (Gd.Size(GameSim.RECIPES) - 1));
        Vector2 Mouse = (Vector2)(Hud.GetLocalMousePosition());
        // see-through like the bag panel, so the scene around the bench stays visible while crafting
        Rect2 P = this.CraftPanelRect(V);
        C.DrawRect(P, new Color(0.06f, 0.07f, 0.1f, 0.72f));
        C.DrawRect(P, (GOLD * new Color(1, 1, 1, 0.7f)), false, 1.5f);
        C.DrawLine(new Vector2((P.Position.X + 16), (P.Position.Y + HEAD_H - 8)), new Vector2((P.End.X - 16), (P.Position.Y + HEAD_H - 8)), new Color(1, 1, 1, 0.1f), 1.0f);
        this.Text(C, (P.Position + new Vector2(20, 28)), "WORKBENCH", 18, GOLD, 0, -1.0f, true);
        bool At = this.AtBenchLocal(Me);
        this.Text(C, (P.Position + new Vector2(150, 27)), (At ? "You are at the workbench" : "Walk to the workbench (north wall, west of door B) to craft"), 11, (At ? new Color("7FD38A") : new Color(1, 0.6f, 0.55f)));
        this.Text(C, new Vector2((P.End.X - 420), (P.Position.Y + 27)), "click a recipe  -  number key crafts at once  -  C close", 10, new Color(1, 1, 1, 0.5f), 2, 400.0f);
        // --- recipe cards, by tier
        float Lw = this.CraftLeftW(P);
        for (int T = 0; T < 3; T++)
        {
            float Hy = (this.CraftRowTop(P, T) + 12);
            bool Open = (T <= Gd.Int(Gd.Get(Snap, "unlock", 0)));
            this.Text(C, new Vector2((P.Position.X + 20), Hy), TIER_TITLES[T], 10, (Open ? GOLD : new Color(1, 1, 1, 0.4f)), 0, -1.0f, true);
        }
        int Busy = Gd.Int(Gd.At(Me, 26));
        for (int I = 0; I < Gd.Size(GameSim.RECIPES); I++)
        {
            this.DrawCraftCard(C, Me, I, this.CraftCard(V, I), Mouse, Busy);
        }
        // what you carry and what is in the chest
        float By = (P.End.Y - 20);
        this.Text(C, new Vector2((P.Position.X + 20), (By + 4)), "Bag", 10, new Color(1, 1, 1, 0.6f));
        dynamic Sto = Gd.Get(Snap, "sto", new GArray());
        float Bx = (P.Position.X + 50);
        foreach (dynamic Res in Gd.Iter(GameSim.RES))
        {
            this.DrawItemIcon(C, Res, new Vector2((Bx + 10), By), 1.0f);
            this.Text(C, new Vector2((Bx + 22), (By + 5)), Gd.Format("%d", Main.Carried(Me, Res)), 11, Colors.White);
            int InChest = GameSim.CountIn(Sto, Res);
            this.Text(C, new Vector2((Bx + 44), (By + 5)), Gd.Format("(chest %d)", InChest), 9, new Color(1, 1, 1, 0.45f));
            Bx += 108;
        }
        C.DrawLine(new Vector2((P.Position.X + 20 + Lw + 12), (P.Position.Y + HEAD_H)), new Vector2((P.Position.X + 20 + Lw + 12), (P.End.Y - 14)), new Color(1, 1, 1, 0.12f), 1.0f);
        this.DrawCraftDetail(C, Me, V, this.CraftDetail(P), Mouse, Busy);
    }

    void DrawCraftCard(dynamic C, dynamic Me, int I, Rect2 Card, Vector2 Mouse, int Busy)
    {
        dynamic R = Gd.At(GameSim.RECIPES, I);
        string Id = Gd.Str(Gd.At(R, 3));
        bool Locked = (Gd.Int(Gd.At(R, 2)) > Gd.Int(Gd.Get(Snap, "unlock", 0)));
        string Why = this.CraftBlock(Me, I);
        bool Ready = (Why == "");
        bool Hover = Card.HasPoint(Mouse);
        bool Sel = (I == CraftSel);
        C.DrawRect(Card, (Hover ? new Color(0.14f, 0.16f, 0.21f, 0.85f) : new Color(0.05f, 0.06f, 0.09f, 0.6f)));
        C.DrawRect(Card, (Sel ? (dynamic)(GOLD) : (Ready ? new Color(0.5f, 0.85f, 0.55f, 0.55f) : new Color(1, 1, 1, 0.15f))), false, (Sel ? 2.0f : 1.0f));
        this.Text(C, (Card.Position + new Vector2(5, 13)), KeyOf(I), 10, new Color(1, 1, 1, 0.55f), 0, -1.0f, true);
        if (Gd.Truth(Gd.At(R, 5)))
        {
            this.Text(C, (Card.Position + new Vector2(0, 13)), "ENG", 9, new Color("F28C38"), 2, (Card.Size.X - 5), true);
        }
        var Icon = new Vector2(Card.GetCenter().X, (Card.Position.Y + 31));
        C.DrawCircle(Icon, 22.0f, new Color(1, 1, 1, (Locked ? 0.02f : 0.05f)));
        this.DrawItemIcon(C, Id, Icon, 2.0f);
        int N = Gd.Int(Gd.At(R, 4));
        if ((N > 1) && (Id != "base"))
        {
            this.Text(C, (Icon + new Vector2(14, 19)), ("x" + N), 10, Colors.White, 0, -1.0f, true);
        }
        this.Text(C, new Vector2(Card.Position.X, (Card.Position.Y + 64)), Gd.Str(Gd.At(R, 0)), 11, (Locked ? new Color(1, 1, 1, 0.4f) : Colors.White), 1, Card.Size.X);
        // the cost as small icons, red where you carry too little
        var Cost = Gd.At(R, 1);
        int Parts = Gd.Size(Cost);
        float Each = 38.0f;
        float X = (Card.GetCenter().X - ((Parts * Each) / 2));
        foreach (dynamic Res in Gd.Iter(Cost))
        {
            int Need = Gd.Int(Gd.At(Cost, Res));
            bool Enough = (Main.Carried(Me, Res) >= Need);
            this.DrawItemIcon(C, Res, new Vector2((X + 10), (Card.Position.Y + 79)), 1.0f);
            this.Text(C, new Vector2((X + 20), (Card.Position.Y + 84)), Gd.Str(Need), 10, (Enough ? Colors.White : new Color(1, 0.55f, 0.5f)));
            X += Each;
        }
        if (Locked)
        {
            C.DrawRect(Card, new Color(0, 0, 0, 0.45f));
            this.Text(C, new Vector2(Card.Position.X, (Card.Position.Y + 36)), "LOCKED", 11, new Color(1, 1, 1, 0.7f), 1, Card.Size.X, true);
        }
        if (Busy == I)
        {
            float F = Mathf.Clamp(Gd.Float(Gd.At(Me, 27)), 0.0f, 1.0f);
            C.DrawRect(new Rect2(Card.Position.X, (Card.End.Y - 4), (Card.Size.X * F), 4), GOLD);
        }
    }

    void DrawCraftDetail(dynamic C, dynamic Me, Vector2 V, Rect2 D, Vector2 Mouse, int Busy)
    {
        int I = CraftSel;
        dynamic R = Gd.At(GameSim.RECIPES, I);
        string Id = Gd.Str(Gd.At(R, 3));
        int N = Gd.Int(Gd.At(R, 4));
        // big icon, name, kind and what it does
        var Frame = new Rect2(D.Position, new Vector2(96, 96));
        C.DrawRect(Frame, new Color(0.05f, 0.06f, 0.09f, 0.7f));
        C.DrawRect(Frame, GOLD, false, 1.5f);
        this.DrawItemIcon(C, Id, Frame.GetCenter(), 4.0f);
        float Tx = (D.Position.X + 110);
        this.Text(C, new Vector2(Tx, (D.Position.Y + 20)), Gd.Str(Gd.At(R, 0)), 17, Colors.White, 0, -1.0f, true);
        this.Text(C, new Vector2(Tx, (D.Position.Y + 38)), this.CraftTag(Id), 10, GOLD, 0, -1.0f, true);
        float Ly = (D.Position.Y + 58);
        foreach (string Line in this.CraftDesc(Id))
        {
            this.Text(C, new Vector2(Tx, Ly), Line, 11, new Color(1, 1, 1, 0.85f));
            Ly += 16;
        }
        // how it is made: ingredients + ingredients -> result
        float Hy = (D.Position.Y + 128);
        this.Text(C, new Vector2(D.Position.X, Hy), "HOW TO CRAFT", 11, GOLD, 0, -1.0f, true);
        float X = D.Position.X;
        float Y = (Hy + 12);
        var Cost = Gd.At(R, 1);
        bool First = true;
        foreach (dynamic Res in Gd.Iter(Cost))
        {
            if (!First)
            {
                this.Text(C, new Vector2(X, (Y + (BOX / 2) + 8)), "+", 22, Colors.White, 1, 22.0f, true);
                X += 22;
            }
            First = false;
            int Need = Gd.Int(Gd.At(Cost, Res));
            int Have = Main.Carried(Me, Res);
            this.CraftBox(C, new Rect2(X, Y, BOX, BOX), Res, ("x" + Need), (Have >= Need) ? new Color(1, 1, 1, 0.25f) : new Color(1, 0.5f, 0.45f, 0.7f));
            this.Text(C, new Vector2(X, (Y + BOX + 13)), Gd.Format("have %d", Have), 10, ((Have >= Need) ? new Color("7FD38A") : new Color(1, 0.6f, 0.55f)), 1, BOX);
            X += BOX;
        }
        // the arrow, with the hammer and the time it takes
        float Ax = (X + 10);
        float Aw = 58.0f;
        float My = (Y + (BOX / 2));
        var Arrow = new Color(1, 1, 1, 0.8f);
        C.DrawLine(new Vector2(Ax, My), new Vector2(((Ax + Aw) - 8), My), Arrow, 3.0f);
        C.DrawColoredPolygon(new Vector2[] { new Vector2((Ax + Aw), My), new Vector2(((Ax + Aw) - 10), (My - 7)), new Vector2(((Ax + Aw) - 10), (My + 7)) }, Arrow);
        this.DrawItemIcon(C, "hammer", new Vector2((Ax + (Aw / 2) - 2), (My - 16)), 1.0f);
        this.Text(C, new Vector2(Ax, (My + 20)), (Num(this.CraftSeconds(R)) + " s"), 10, new Color(1, 1, 1, 0.7f), 1, Aw);
        X = (Ax + Aw + 10);
        // the result (the bow comes with 5 arrows)
        string Count = (((N > 1) && (Id != "base")) ? ("x" + N) : ((Id == "base") ? "+100" : ""));
        this.CraftBox(C, new Rect2(X, Y, BOX, BOX), Id, Count, GOLD);
        if (Id == "bow")
        {
            this.Text(C, new Vector2((X + BOX), (Y + (BOX / 2) + 8)), "+", 22, Colors.White, 1, 22.0f, true);
            this.CraftBox(C, new Rect2((X + BOX + 22), Y, BOX, BOX), "arrow", "x5", GOLD);
        }
        // the steps
        float Sy = (Y + BOX + 40);
        var Steps = new List<string>();
        var Gather = new List<string>();
        foreach (dynamic Res in Gd.Iter(Cost))
        {
            string Name = Gd.Str(Res);
            string From = RES_FROM.GetValueOrDefault(Name, "");
            Gather.Add((Gd.Str(Gd.At(Cost, Res)) + " " + Name + " (" + From + ")"));
        }
        Steps.Add(("Gather " + string.Join(" and ", Gather) + "."));
        Steps.Add("Carry them: crafting uses your bag, not the chest.");
        Steps.Add(("Stand at the workbench, press Craft or key " + KeyOf(I) + "."));
        Steps.Add(("Hold on " + Num(this.CraftSeconds(R)) + " s: a hit or walking away stops it."));
        for (int K = 0; K < Steps.Count; K++)
        {
            this.Text(C, new Vector2(D.Position.X, Sy), Gd.Format("%d.", (K + 1)), 11, GOLD, 0, -1.0f, true);
            this.Text(C, new Vector2((D.Position.X + 16), Sy), Steps[K], 11, new Color(1, 1, 1, 0.85f));
            Sy += 18;
        }
        // the button, or why not
        Rect2 B = this.CraftButton(V);
        string Why = this.CraftBlock(Me, I);
        if (Busy == I)
        {
            float F = Mathf.Clamp(Gd.Float(Gd.At(Me, 27)), 0.0f, 1.0f);
            C.DrawRect(B, new Color(0.05f, 0.06f, 0.09f, 0.95f));
            C.DrawRect(new Rect2(B.Position, new Vector2((B.Size.X * F), B.Size.Y)), (GOLD * new Color(1, 1, 1, 0.75f)));
            C.DrawRect(B, GOLD, false, 1.5f);
            this.Text(C, new Vector2(B.Position.X, (B.Position.Y + 25)), Gd.Format("MAKING...  %d%%", Gd.Int((F * 100))), 13, Colors.White, 1, B.Size.X, true);
            return;
        }
        bool Can = (Why == "");
        bool Hover = (Can && B.HasPoint(Mouse));
        C.DrawRect(B, (Can ? (Hover ? GOLD.Lightened(0.15f) : GOLD) : new Color(0.2f, 0.21f, 0.25f, 0.9f)));
        C.DrawRect(B, (Can ? INK : new Color(1, 1, 1, 0.15f)), false, 1.0f);
        this.Text(C, new Vector2(B.Position.X, (B.Position.Y + 25)), (Can ? Gd.Format("CRAFT  [%s]", KeyOf(I)) : Why.ToUpper()), 13, (Can ? INK : new Color(1, 1, 1, 0.55f)), 1, B.Size.X, true);
    }

    // one picture box of the recipe: an icon at 2x with its count in the corner
    void CraftBox(dynamic C, Rect2 R, string Id, string Count, Color Edge)
    {
        C.DrawRect(R, new Color(0.05f, 0.06f, 0.09f, 0.7f));
        C.DrawRect(R, Edge, false, 1.5f);
        this.DrawItemIcon(C, Id, R.GetCenter(), 2.0f);
        if (Count != "")
        {
            this.Text(C, new Vector2(R.Position.X, (R.End.Y - 4)), Count, 11, Colors.White, 2, (R.Size.X - 4), true);
        }
    }

    // --- mouse -----------------------------------------------------------------
    public bool CraftMouse(dynamic Ev)
    {
        if ((!Gd.Truth(CraftOpen) || (Ev.ButtonIndex != 1)))
        {
            return false;
        }
        if (Gd.Truth(Ev.Pressed))
        {
            Vector2 Vs = (Vector2)Hud.Size;
            Vector2 Mouse = (Vector2)(Hud.GetLocalMousePosition());
            for (int I = 0; I < Gd.Size(GameSim.RECIPES); I++)
            {
                if (this.CraftCard(Vs, I).HasPoint(Mouse))
                {
                    CraftSel = I;
                }
            }
            dynamic Me = Gd.Get(Snap.Ps, MyId);
            if ((Me != null) && this.CraftButton(Vs).HasPoint(Mouse) && (this.CraftBlock(Me, CraftSel) == ""))
            {
                this.Act("craft", CraftSel);
            }
        }
        return true;
    }
}
