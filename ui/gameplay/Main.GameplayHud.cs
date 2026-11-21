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
        foreach (GArray Job in (GArray)Gd.Get(Snap, "construction", new GArray()))
            if (Gd.Equal(Job[2], MyId)) Hint = $"Building tower: {Gd.Float(Job[1]) * 100:F0}% - stand still";
        if (((Hint == "") && (LocalNoteT > 0)))
        {
            Hint = LocalNote;
        }
        // a downed teammate, or a dead one (with a Revival kit in the bag)
        if (((Hint == "") && (Gd.At(Me, 5) == 0)))
        {
            foreach (dynamic Qid in Gd.Iter(Snap.Ps))
            {
                dynamic Q = Gd.At(Snap.Ps, Qid);
                if ((Qid == MyId) || (Gd.At(Q, 5) == 0) || (LocalPos.DistanceTo((Vector2)Gd.Get(ShownP, Qid, Gd.At(Q, 2))) > (1.5f * T)))
                {
                    continue;
                }
                if (Gd.At(Q, 5) == 1)
                {
                    Hint = "E: revive";
                }
                else
                {
                    Hint = ((Main.Carried(Me, "revive") > 0) ? "E: use Revival kit" : "Needs a Revival kit");
                }
                break;
            }
        }
        // standing at the base furniture
        if (((Hint == "") && (Gd.At(Me, 5) == 0)))
        {
            if (this.AtBenchLocal(Me))
            {
                Hint = "C: craft";
            }
            else if (this.AtStorageLocal(Me))
            {
                Hint = "Tab: storage";
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
                    Hint = (Gd.Format("Tower %d/%d", new GArray(Gd.At(Tw, 2), Tcap)) + ((Gd.At(Tw, 2) < Tcap) ? (dynamic)("  |  E: repair") : ""));
                    break;
                }
            }
            if (((Hint == "") && this.FreeTowerHere()))
            {
                Hint = "T: build free tower (6s)";
            }
        }
        if (((Hint == "") && Gd.Contains(new GArray("day", "dusk"), Ph) && (Gd.At(Me, 5) == 0)))
        {
            foreach (dynamic Nd in Gd.Iter(Snap.Nodes))
            {
                if (((Gd.At(Nd, 2) > 0) && (LocalPos.DistanceTo(Gd.At(Nd, 1)) <= (1.5f * T))))
                {
                    dynamic Pct = Gd.Int((Gd.At(Me, 21) * 100));
                    Hint = ((Gd.At(Me, 21) > 0) ? (dynamic)(Gd.Format("Gathering %s... %d%%", new GArray(Gd.At(GameSim.RES, Gd.At(Nd, 0)), Pct))) : Gd.Format("E: gather %s", Gd.At(GameSim.RES, Gd.At(Nd, 0))));
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
                    Hint = "";
                }
                else if ((Gd.Truth(Gd.At(Snap.Broken, I)) && Gd.Truth(Snap.InsideEn)))
                {
                    Hint = "Clear the enemies inside first";
                }
                else if ((Main.Carried(Me, "wood") <= 0))
                {
                    Hint = "Need wood";
                }
                else if (Gd.Truth(Gd.Static(typeof(Input), "is_physical_key_pressed", 69)))
                {
                    Hint = (Gd.Truth(Gd.At(Snap.Dpause, I)) ? (dynamic)("Interrupted!") : Gd.Format("Door %s %d/%d", new GArray(Dn, Dur, Cap)));
                }
                else
                {
                    Hint = Gd.Format("E: repair door %s", Dn);
                }
                if (((Ph == "night") && (Dur > 0)))
                {
                    Hint += ((Hint == "") ? "F: enter" : "  |  F: enter");
                }
                break;
            }
        }
        if (((Hint == "") && (Ph == "night") && (Gd.At(Me, 1) == 2) && (Gd.At(Me, 5) == 0)))
        {
            if (Gd.Truth(Gd.At(Me, 19)))
            {
                Hint = "F at a door: leave";
            }
            else
            {
                foreach (dynamic I in Gd.Iter(3))
                {
                    if (((Gd.At(Snap.Doors, I) > 0) && (LocalPos.DistanceTo(Gd.At(GameSim.DOOR_POS, I)) <= (2.0f * T))))
                    {
                        Hint = "F: enter";
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
            SelectCraftTier(CraftTier);
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
                var Choices = CraftChoices();
                if (N >= 0 && N < Choices.Count)
                {
                    CraftSel = Choices[N];
                    this.Act("craft", CraftSel);
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
        if (UiCapturesMouse()) return;
        dynamic Me = Gd.Get(Snap.Ps, MyId);
        dynamic Slot = Me == null ? null : Gd.At(Gd.At(Me, 7), Gd.Int(Gd.At(Me, 25)));
        bool Use = Slot != null && GameSim.IsTacticalUse(Gd.Str(Gd.At(Slot, 0)));
        this.Act(Use ? "use_item" : "flask", Gd.Native(this, "get_global_mouse_position"));
    }
}
