using Godot;
using Environment = Godot.Environment;
using FileAccess = Godot.FileAccess;
using System;
using System.Threading.Tasks;
using HoldTilDawn;

// Owner: Pham Minh Hieu. Gameplay widgets and keys plugged into the HUD shell of Main.cs:
// context hints and gameplay key bindings.
public partial class Main : Node2D
{
    // Context hint above the hotbar: towers, gathering, door repair and night entry.
    public void DrawInteractionHint(dynamic C, dynamic Vs, dynamic Me, string Ph)
    {
        string Hint = Gd.At(Me, 16);
        if (((Hint == "") && (LocalNoteT > 0)))
        {
            Hint = LocalNote;
        }
        // standing at the base furniture
        if (((Hint == "") && (Gd.At(Me, 5) == 0)))
        {
            if (this.AtBenchLocal(Me))
            {
                Hint = "Workbench: C to craft";
            }
            else if (this.AtStorageLocal(Me))
            {
                Hint = "Storage chest: Tab to open the shared storage";
            }
        }
        dynamic GatheringHint = false;
        if (((Hint == "") && (Gd.At(Me, 1) == 2) && (Gd.At(Me, 5) == 0) && !(Gd.Truth(Gd.At(Me, 19)))))
        {
            dynamic Tws = Gd.Get(Snap, "tw", new GArray());
            foreach (dynamic Tw in Gd.Iter(Tws))
            {
                if ((LocalPos.DistanceTo(Gd.At(Tw, 1)) <= (1.5f * T)))
                {
                    float Tcap = Gd.Float((GameSim.TOWER_HP * (Gd.Truth(Gd.At(Tw, 3)) ? (dynamic)(0.3f) : 1.0f)));
                    Hint = (Gd.Format("Tower at door %s: %d / %d", new GArray(Gd.At(GameSim.DOOR_NAMES, Gd.At(Tw, 0)), Gd.At(Tw, 2), Tcap)) + ((Gd.At(Tw, 2) < Tcap) ? (dynamic)(Gd.Format("   |   hold E to repair (wood %d)", Main.Carried(Me, "wood"))) : ""));
                    break;
                }
            }
            dynamic Slot = GameSim.TowerSlot(LocalPos);
            if (((Hint == "") && (Slot >= 0) && !(Gd.Truth(Gd.Any(Tws, (Func<dynamic, dynamic>)((Tw) => (Gd.At(Tw, 0) == Slot)))))))
            {
                if (Gd.Truth(Gd.Get(Snap, "free_tower", false)))
                {
                    Hint = Gd.Format("B then 3: place the free tower at door %s (or T here)", Gd.At(GameSim.DOOR_NAMES, Slot));
                }
                else if ((Snap.Unlock < 2))
                {
                    Hint = "More towers unlock after the first boss";
                }
                else
                {
                    Hint = Gd.Format("B then 3: build a tower at door %s (10 wood + 5 metal)", Gd.At(GameSim.DOOR_NAMES, Slot));
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
                else if ((Main.Carried(Me, "wood") <= 0))
                {
                    Hint = Gd.Format("Need wood to repair door %s (1 wood per 10 durability)", Dn);
                }
                else if (Gd.Truth(Gd.Static(typeof(Input), "is_physical_key_pressed", 69)))
                {
                    Hint = (Gd.Truth(Gd.At(Snap.Dpause, I)) ? (dynamic)(Gd.Format("Repair interrupted by a hit on door %s!", Dn)) : Gd.Format("Repairing door %s  %d / %d", new GArray(Dn, Dur, Cap)));
                }
                else
                {
                    Hint = Gd.Format("Hold E to repair door %s  %d / %d  (wood %d)", new GArray(Dn, Dur, Cap, Main.Carried(Me, "wood")));
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
                Hint = "Inside the base: workbench (C) and storage chest (Tab) by the north wall  -  hold F at a door to leave";
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

    // Gameplay keys (F3 and zoom stay in Main.cs). Each key sends one action string to GameSim.Action.
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
        else if (Gd.Equal(Code, 71))
        {
            this.Act("give");
        }
        else if (Gd.Equal(Code, 77))
        {
            this.ToggleMap();
        }
        else if (Gd.Equal(Code, 67))
        {
            // the workbench tab opens only at the workbench (and closes by itself away from it, KeepPanelsInReach)
            dynamic Me = Gd.Get(Snap.Ps, MyId);
            if (!Gd.Truth(CraftOpen) && !this.AtBenchLocal(Me))
            {
                return;
            }
            CraftOpen = !(Gd.Truth(CraftOpen));
            BagOpen = false;
        }
        else if (Gd.Equal(Code, 4194306))
        {
            // the bag opens anywhere; opened at the chest it is the chest, and closes when you walk away from it
            BagOpen = !(Gd.Truth(BagOpen));
            BagAtChest = (Gd.Truth(BagOpen) && this.AtStorageLocal(Gd.Get(Snap.Ps, MyId)));
            DragFrom = null;
            CraftOpen = false;
        }
        else if (Gd.Equal(Code, 4194340))
        {
            this.Act("skip");
        }
        else
        {
            int N = Gd.Int((Code - 49));
            if ((Code == 48))
            {
                N = 9;
            }
            if (Gd.Truth(CraftOpen))
            {
                // a number key shows that recipe and crafts it at once (ui/gameplay/Main.CraftPanel.cs)
                if (((N >= 0) && (N < Gd.Size(GameSim.RECIPES))))
                {
                    CraftSel = N;
                    this.Act("craft", N);
                }
            }
            else if (((N >= 0) && (N < GameSim.HOTBAR)))
            {
                this.Act("select", N);
            }
        }
    }

    // Mouse wheel picks the hotbar slot (MEC-09).
    public void HotbarWheel(int Step)
    {
        dynamic Me = Gd.Get(Snap.Ps, MyId);
        if ((Me != null))
        {
            this.Act("select", (((Gd.Int(Gd.At(Me, 25)) + Step) + GameSim.HOTBAR) % GameSim.HOTBAR));
        }
    }

    public void GameplayRightClick()
    {
        this.Act("flask", Gd.Native(this, "get_global_mouse_position"));
    }
}
