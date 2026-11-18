using Godot;
using Environment = Godot.Environment;
using FileAccess = Godot.FileAccess;
using System;
using System.Threading.Tasks;
using HoldTilDawn;

// Owner: Pham Minh Hieu. Gameplay widgets and keys plugged into the HUD shell of Main.cs:
// inventory panel, context hints, workbench panel, gameplay key bindings.
public partial class Main : Node2D
{
    public void DrawInventoryPanel(dynamic C, dynamic Vs, dynamic Me)
    {
        if ((Me != null))
        {
            dynamic Inv = Gd.At(Me, 7);
            dynamic Rx = (Vs.X - 130);
            this.Panel(C, Gd.MakeRect2((Rx - 8), 6, 130, 84));
            dynamic I = 0;
            foreach (dynamic Res in Gd.Iter(GameSim.RES))
            {
                C.DrawCircle(Gd.MakeVector2((Rx + 6), (20 + (I * 18))), 6, INK);
                C.DrawCircle(Gd.MakeVector2((Rx + 6), (20 + (I * 18))), 5, Gd.At(RES_COL, I));
                this.Text(C, Gd.MakeVector2((Rx + 18), (25 + (I * 18))), Gd.Format("%s  x%d", new GArray(Res, Gd.At(Inv, Res))), 12, Colors.White);
                I += 1;
            }
        }
    }

    // Context hint above the hotbar: towers, gathering, door repair and night entry.
    public void DrawInteractionHint(dynamic C, dynamic Vs, dynamic Me, string Ph)
    {
        string Hint = Gd.At(Me, 16);
        dynamic GatheringHint = false;
        if (((Hint == "") && (Gd.At(Me, 1) == 2) && (Gd.At(Me, 5) == 0) && !(Gd.Truth(Gd.At(Me, 19)))))
        {
            dynamic Tws = Gd.Get(Snap, "tw", new GArray());
            foreach (dynamic Tw in Gd.Iter(Tws))
            {
                if ((LocalPos.DistanceTo(Gd.At(Tw, 1)) <= (1.5f * T)))
                {
                    float Tcap = Gd.Float((GameSim.TOWER_HP * (Gd.Truth(Gd.At(Tw, 3)) ? (dynamic)(0.3f) : 1.0f)));
                    Hint = (Gd.Format("Tower at door %s: %d / %d", new GArray(Gd.At(GameSim.DOOR_NAMES, Gd.At(Tw, 0)), Gd.At(Tw, 2), Tcap)) + ((Gd.At(Tw, 2) < Tcap) ? (dynamic)(Gd.Format("   |   hold E to repair (wood %d)", Gd.At(Me, 7).Wood)) : ""));
                    break;
                }
            }
            dynamic Slot = GameSim.TowerSlot(LocalPos);
            if (((Hint == "") && (Slot >= 0) && !(Gd.Truth(Gd.Any(Tws, (Func<dynamic, dynamic>)((Tw) => (Gd.At(Tw, 0) == Slot)))))))
            {
                if (Gd.Truth(Gd.Get(Snap, "free_tower", false)))
                {
                    Hint = Gd.Format("T: place the free tower at door %s", Gd.At(GameSim.DOOR_NAMES, Slot));
                }
                else if ((Snap.Unlock < 2))
                {
                    Hint = "More towers unlock after the first boss";
                }
                else
                {
                    Hint = Gd.Format("T: build a tower at door %s (10 wood + 5 metal)", Gd.At(GameSim.DOOR_NAMES, Slot));
                }
            }
        }
        if (((Hint == "") && Gd.Contains(new GArray("day", "dusk"), Ph) && (Gd.At(Me, 5) == 0)))
        {
            foreach (dynamic Nd in Gd.Iter(Snap.Nodes))
            {
                if (((Gd.At(Nd, 2) > 0) && (LocalPos.DistanceTo(Gd.At(Nd, 1)) <= (1.5f * T))))
                {
                    dynamic Pct = Gd.Int((Gd.At(Me, 21) * 100));
                    Hint = ((Gd.At(Me, 21) > 0) ? (dynamic)(Gd.Format("Gathering %s... %d%%", new GArray(Gd.At(GameSim.RES, Gd.At(Nd, 0)), Pct))) : Gd.Format("Hold E (stand still) to gather %s - %d left", new GArray(Gd.At(GameSim.RES, Gd.At(Nd, 0)), Gd.At(Nd, 2))));
                    GatheringHint = true;
                    break;
                }
            }
        }
        if (((Hint == "") && Gd.Contains(new GArray("day", "dusk", "night"), Ph) && (Gd.At(Me, 1) == 2) && (Gd.At(Me, 5) == 0) && !(Gd.Truth(Gd.At(Me, 19))) && !(Gd.Truth(GameSim.InsideBase(LocalPos)))))
        {
            foreach (dynamic I in Gd.Iter(3))
            {
                if ((LocalPos.DistanceTo(Gd.At(GameSim.DOOR_POS, I)) > (1.5f * T)))
                {
                    continue;
                }
                string Dn = Gd.At(GameSim.DOOR_NAMES, I);
                float Cap = Gd.Float((GameSim.DOOR_HP * (Gd.Truth(Gd.At(Snap.Broken, I)) ? (dynamic)(0.3f) : 1.0f)));
                float Dur = Gd.Float(Gd.At(Snap.Doors, I));
                if ((Dur >= Cap))
                {
                    Hint = Gd.Format("Door %s is at full durability (%d)", new GArray(Dn, Dur));
                }
                else if ((Gd.Truth(Gd.At(Snap.Broken, I)) && Gd.Truth(Snap.InsideEn)))
                {
                    Hint = Gd.Format("Door %s is broken - clear the enemies inside before repairing", Dn);
                }
                else if ((Gd.At(Me, 7).Wood <= 0))
                {
                    Hint = Gd.Format("Need wood to repair door %s (1 wood per 10 durability)", Dn);
                }
                else if (Gd.Truth(Gd.Static(typeof(Input), "is_physical_key_pressed", 69)))
                {
                    Hint = (Gd.Truth(Gd.At(Snap.Dpause, I)) ? (dynamic)(Gd.Format("Repair interrupted by a hit on door %s!", Dn)) : Gd.Format("Repairing door %s  %d / %d", new GArray(Dn, Dur, Cap)));
                }
                else
                {
                    Hint = Gd.Format("Hold E to repair door %s  %d / %d  (wood %d)", new GArray(Dn, Dur, Cap, Gd.At(Me, 7).Wood));
                }
                if (((Ph == "night") && (Dur > 0)))
                {
                    Hint += "   |   hold F to enter";
                }
                break;
            }
        }
        if (((Hint == "") && (Ph == "night") && (Gd.At(Me, 1) == 2) && (Gd.At(Me, 5) == 0)))
        {
            if (Gd.Truth(Gd.At(Me, 19)))
            {
                Hint = "Inside the base: C craft  -  hold F at a door to leave";
            }
            else
            {
                foreach (dynamic I in Gd.Iter(3))
                {
                    if (((Gd.At(Snap.Doors, I) > 0) && (LocalPos.DistanceTo(Gd.At(GameSim.DOOR_POS, I)) <= (2.0f * T))))
                    {
                        Hint = "Hold F (stand still) to enter the base";
                    }
                }
            }
        }
        if ((Hint != ""))
        {
            if (Gd.Truth(GatheringHint))
            {
                C.DrawString(FontUi, Gd.MakeVector2(0, (Vs.Y - 74)), Hint, 1, Vs.X, 13, Colors.Black);
            }
            else
            {
                this.Text(C, Gd.MakeVector2(0, (Vs.Y - 74)), Hint, 13, GOLD, 1, Vs.X);
            }
        }
    }

    public void DrawCraftPanel(dynamic C, dynamic Vs)
    {
        if (Gd.Truth(CraftOpen))
        {
            dynamic Cr = Gd.MakeRect2((Vs.X - 300), 190, 290, (22 + (Gd.Size(GameSim.RECIPES) * 18)));
            this.Panel(C, Cr);
            this.Text(C, (Cr.Position + Gd.MakeVector2(10, 16)), "WORKBENCH  (number to craft, C close)", 11, GOLD, 0, (-1.0f), true);
            foreach (dynamic I in Gd.Iter(Gd.Size(GameSim.RECIPES)))
            {
                dynamic R = Gd.At(GameSim.RECIPES, I);
                dynamic Cost = new GArray();
                foreach (dynamic Res in Gd.Iter(Gd.At(R, 1)))
                {
                    Gd.Append(Cost, Gd.Format("%d %s", new GArray(Gd.At(Gd.At(R, 1), Res), Res)));
                }
                bool Locked = Gd.Truth((Gd.At(R, 2) > Snap.Unlock));
                dynamic Line = Gd.Format("%d  %s - %s%s", new GArray((I + 1), Gd.At(R, 0), Gd.Join(", ", Gd.PackedStringArray(Cost)), (Gd.Truth(Gd.At(R, 5)) ? (dynamic)("  [Eng]") : "")));
                this.Text(C, (Cr.Position + Gd.MakeVector2(10, (34 + (I * 18)))), (Line + (Gd.Truth(Locked) ? (dynamic)("  LOCKED") : "")), 11, (Gd.Truth(Locked) ? (dynamic)(Gd.MakeColor(1, 1, 1, 0.35f)) : Colors.White));
            }
        }
    }

    // Gameplay keys (Esc, F3 and zoom stay in Main.cs). Each key sends one action string to GameSim.Action.
    public void GameplayKey(dynamic Code)
    {
        if (Gd.Equal(Code, 81))
        {
            this.Act("bandage");
        }
        else if (Gd.Equal(Code, 84))
        {
            this.Act("tower");
        }
        else if (Gd.Equal(Code, 86))
        {
            this.Act("vote");
        }
        else if (Gd.Equal(Code, 67))
        {
            CraftOpen = !(Gd.Truth(CraftOpen));
        }
        else if (Gd.Equal(Code, 4194340))
        {
            this.Act("skip");
        }
        else
        {
            int N = Gd.Int((Code - 49));
            if ((Gd.Truth(CraftOpen) && (N >= 0) && (N < Gd.Size(GameSim.RECIPES))))
            {
                this.Act("craft", N);
            }
        }
    }

    public void GameplayRightClick()
    {
        this.Act("flask", Gd.Native(this, "get_global_mouse_position"));
    }
}
