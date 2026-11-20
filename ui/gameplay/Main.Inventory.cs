using Godot;
using Environment = Godot.Environment;
using FileAccess = Godot.FileAccess;
using System;
using System.Threading.Tasks;
using HoldTilDawn;

// Owner: Pham Minh Hieu. Hotbar, inventory and storage panels with drag and drop (MEC-09, HUD element 8),
// and the item art: icons from art/items (tools/PixelItems.cs), projectiles from art/fx.
public partial class Main : Node2D
{
    public const float SLOT = 44.0f;
    public const float SLOT_GAP = 4.0f;

    public const string ITEM_ART = "res://art/items/";
    public const string FX_ART = "res://art/fx/";
    public GDict ItemTex = new GDict();
    public GDict FxTex = new GDict();

    public void LoadItemArt()
    {
        foreach (dynamic Id in Gd.Iter(GameSim.ITEMS))
        {
            ItemTex[Id] = Gd.Load(((ITEM_ART + Id) + ".png"));
        }
        // the crafts that make no item have an icon too (workbench panel), under their recipe id
        ItemTex["revive"] = Gd.Load((ITEM_ART + "revival_kit.png"));
        ItemTex["base"] = Gd.Load((ITEM_ART + "base_repair.png"));
        foreach (dynamic N in Gd.Iter(new GArray("arrow", "spit", "orb")))
        {
            FxTex[N] = Gd.Load(((FX_ART + N) + ".png"));
        }
    }

    // An item icon centred on a point, at a whole-number scale so the pixels stay square.
    public void DrawItemIcon(dynamic Canvas, dynamic Id, Vector2 Centre, float Scale)
    {
        dynamic Tex = ItemTex.Get(Id);
        if ((Tex == null))
        {
            return;
        }
        Vector2 Size = (Tex.GetSize() * Scale);
        Gd.Native(Canvas, "draw_texture_rect", this.Pixel(Tex), Gd.MakeRect2((Centre - (Size / 2)).Round(), Size), false);
    }

    // A projectile sprite in the world, turned to its flight direction.
    public void DrawFx(dynamic N, Vector2 Pos, float Angle)
    {
        dynamic Tex = FxTex.Get(N);
        if ((Tex == null))
        {
            return;
        }
        Gd.Native(this, "draw_set_transform", Pos, Angle, Vector2.One);
        Gd.Native(this, "draw_texture", Tex, ((-Tex.GetSize()) / 2).Round());
        Gd.Native(this, "draw_set_transform", Vector2.Zero, 0.0f, Vector2.One);
    }

    public dynamic BagOpen = false;
    // [box, index] of the slot being dragged, or null
    public dynamic DragFrom = null;

    public static int Carried(dynamic Me, string Id)
    {
        return ((Me == null) ? 0 : GameSim.CountIn(Gd.At(Me, 7), Id));
    }

    // Crafting is at the workbench, storage at the chest, both by the base's north wall: inside the base by day, or
    // as the Engineer inside at night (the server checks the same, GameSim.AtBench / AtStorage).
    public bool InBaseLocal(dynamic Me)
    {
        string Ph = Gd.Get(Snap, "ph", "");
        return ((Me != null) && (((Ph == "day") || (Ph == "dusk")) ? Gd.Truth(GameSim.InsideBase(LocalPos)) : ((Ph == "night") && Gd.Truth(Gd.At(Me, 19)))));
    }

    public bool AtBenchLocal(dynamic Me)
    {
        return (this.InBaseLocal(Me) && GameSim.NearBench((Vector2)LocalPos));
    }

    public bool AtStorageLocal(dynamic Me)
    {
        return (this.InBaseLocal(Me) && GameSim.NearStorage((Vector2)LocalPos));
    }

    // Panels close by themselves out of reach: the workbench tab, and the bag when it was opened as the chest. The tab
    // keeps half a tile of slack past the reach, so a step back while crafting does not flick it shut.
    public bool BagAtChest = false;
    public const float PANEL_SLACK = (0.5f * GameSim.T);

    public void KeepPanelsInReach(dynamic Me)
    {
        if ((Me == null))
        {
            return;
        }
        bool InBase = this.InBaseLocal(Me);
        if (Gd.Truth(CraftOpen) && !(InBase && (((Vector2)LocalPos).DistanceTo(GameSim.WORKBENCH_POS) <= (GameSim.BENCH_REACH + PANEL_SLACK))))
        {
            CraftOpen = false;
        }
        // the chest has no slack: past its reach its slots go (the server refuses them there too)
        if (Gd.Truth(BagOpen) && BagAtChest && !this.AtStorageLocal(Me))
        {
            BagOpen = false;
            BagAtChest = false;
            DragFrom = null;
        }
        // an item picked up from the chest is let go once the chest is out of reach
        if (((DragFrom != null) && Gd.Equal(Gd.At(DragFrom, 0), "sto") && !this.AtStorageLocal(Me)))
        {
            DragFrom = null;
        }
    }

    public bool UiCapturesMouse()
    {
        return (Gd.Truth(BagOpen) || Gd.Truth(CraftOpen) || Gd.Truth(BuildOpen) || Gd.Truth(Paused));
    }

    public void DrawSlot(dynamic C, Rect2 R, dynamic S, bool Selected, string Key)
    {
        C.DrawRect(R, Gd.MakeColor(0.05f, 0.06f, 0.09f, 0.85f));
        C.DrawRect(R, (Selected ? (dynamic)(GOLD) : Gd.MakeColor(1, 1, 1, 0.18f)), false, (Selected ? 2.0f : 1.0f));
        if ((Key != ""))
        {
            this.Text(C, (R.Position + Gd.MakeVector2(3, 10)), Key, 9, Gd.MakeColor(1, 1, 1, 0.5f));
        }
        if ((S == null))
        {
            return;
        }
        this.DrawItemIcon(C, Gd.At(S, 0), R.GetCenter(), 2.0f);
        if ((Gd.At(S, 1) > 1))
        {
            this.Text(C, Gd.MakeVector2(R.Position.X, (R.End.Y - 3)), Gd.Str(Gd.At(S, 1)), 11, Colors.White, 2, (R.Size.X - 3), true);
        }
        if (GameSim.IsBow(S))
        {
            // the arrows loaded in the bow: a small arrow and the count, red when it is empty
            int N = GameSim.Loaded(S);
            this.DrawItemIcon(C, "arrow", (R.End - Gd.MakeVector2(30, 8)), 1.0f);
            this.Text(C, Gd.MakeVector2(R.Position.X, (R.End.Y - 3)), Gd.Str(N), 11, ((N > 0) ? GOLD : NEON_RED), 2, (R.Size.X - 3), true);
        }
    }

    // HUD element 8: the first four inventory slots, keys 1-4 or the mouse wheel.
    public void DrawHotbar(dynamic C, dynamic Vs, dynamic Me)
    {
        float W = ((GameSim.HOTBAR * SLOT) + ((GameSim.HOTBAR - 1) * SLOT_GAP));
        float X0 = ((Vs.X - W) / 2);
        float Y0 = (Vs.Y - SLOT - 10);
        for (int I = 0; I < GameSim.HOTBAR; I++)
        {
            this.DrawSlot(C, Gd.MakeRect2((X0 + (I * (SLOT + SLOT_GAP))), Y0, SLOT, SLOT), Gd.At(Gd.At(Me, 7), I), (I == Gd.Int(Gd.At(Me, 25))), Gd.Str((I + 1)));
        }
        // bandage and craft channels above the hotbar
        if ((Gd.At(Me, 26) >= 0))
        {
            dynamic R = Gd.At(GameSim.RECIPES, Gd.Int(Gd.At(Me, 26)));
            this.Text(C, Gd.MakeVector2(0, (Y0 - 30)), Gd.Format("Crafting %s...", Gd.At(R, 0)), 11, GOLD, 1, Vs.X);
            C.DrawRect(Gd.MakeRect2(((Vs.X / 2) - 60), (Y0 - 24), 120, 5), INK);
            C.DrawRect(Gd.MakeRect2(((Vs.X / 2) - 60), (Y0 - 24), (120 * Gd.Clampf(Gd.At(Me, 27), 0, 1)), 5), GOLD);
        }
    }

    // [box, index, rect] for every slot of the open bag panel: inventory (4 x 3) and, at the chest, storage (8 x 5).
    public GArray BagSlots(dynamic Vs, dynamic Me)
    {
        var Out = new GArray();
        bool Sto = this.AtStorageLocal(Me);
        float InvW = ((4 * SLOT) + (3 * SLOT_GAP));
        float StoW = (Sto ? ((8 * SLOT) + (7 * SLOT_GAP)) : 0.0f);
        float Gap = (Sto ? 28.0f : 0.0f);
        float X0 = ((Vs.X - ((InvW + Gap) + StoW)) / 2);
        float Y0 = ((Vs.Y / 2) - 110);
        for (int I = 0; I < GameSim.INV_SLOTS; I++)
        {
            // the hotbar row sits apart at the top
            float Y = ((Y0 + ((I / 4) * (SLOT + SLOT_GAP))) + ((I >= 4) ? 8.0f : 0.0f));
            Out.Add(new GArray("inv", I, Gd.MakeRect2((X0 + ((I % 4) * (SLOT + SLOT_GAP))), Y, SLOT, SLOT)));
        }
        if (Sto)
        {
            float Sx = ((X0 + InvW) + Gap);
            for (int I = 0; I < GameSim.STORAGE_SLOTS; I++)
            {
                Out.Add(new GArray("sto", I, Gd.MakeRect2((Sx + ((I % 8) * (SLOT + SLOT_GAP))), (Y0 + ((I / 8) * (SLOT + SLOT_GAP))), SLOT, SLOT)));
            }
        }
        return Out;
    }

    public void DrawBag(dynamic C, dynamic Vs, dynamic Me)
    {
        if (!Gd.Truth(BagOpen))
        {
            return;
        }
        GArray Slots = this.BagSlots(Vs, Me);
        Rect2 All = (Rect2)(Gd.At(Gd.At(Slots, 0), 2));
        foreach (dynamic S in Slots)
        {
            All = All.Merge(Gd.At(S, 2));
        }
        // wide enough for the two help lines under the slots
        Rect2 Frame = All.Grow(14).GrowSide(Side.Top, 16).GrowSide(Side.Bottom, 30);
        float Extra = Math.Max(0.0f, ((330.0f - Frame.Size.X) / 2));
        Frame = Frame.GrowIndividual(Extra, 0, Extra, 0);
        this.Panel(C, Frame);
        this.Text(C, (All.Position + Gd.MakeVector2(0, (-14))), "YOUR BAG", 11, GOLD, 0, (-1.0f), true);
        this.Text(C, Gd.MakeVector2(Frame.Position.X, (All.End.Y + 28)), "Drag: move, store, arrows into a bow   G give   Tab close", 10, Gd.MakeColor(1, 1, 1, 0.6f), 1, Frame.Size.X);
        if ((Gd.Size(Slots) > GameSim.INV_SLOTS))
        {
            Rect2 First = (Rect2)(Gd.At(Gd.At(Slots, GameSim.INV_SLOTS), 2));
            this.Text(C, (First.Position + Gd.MakeVector2(0, (-14))), "BASE STORAGE (shared)", 11, GOLD, 0, (-1.0f), true);
        }
        else
        {
            this.Text(C, Gd.MakeVector2(Frame.Position.X, (All.End.Y + 14)), "Shared storage: the chest in the base", 10, Gd.MakeColor(1, 1, 1, 0.6f), 1, Frame.Size.X);
        }
        dynamic Sto = Gd.Get(Snap, "sto", new GArray());
        Vector2 Mouse = (Vector2)(Hud.GetLocalMousePosition());
        foreach (dynamic S in Slots)
        {
            bool IsInv = Gd.Equal(Gd.At(S, 0), "inv");
            int I = Gd.Int(Gd.At(S, 1));
            dynamic Item = (IsInv ? Gd.At(Gd.At(Me, 7), I) : ((I < Gd.Size(Sto)) ? Gd.At(Sto, I) : null));
            bool Dragged = ((DragFrom != null) && Gd.Equal(Gd.At(DragFrom, 0), Gd.At(S, 0)) && (Gd.At(DragFrom, 1) == I));
            this.DrawSlot(C, Gd.At(S, 2), (Dragged ? null : Item), (IsInv && (I == Gd.Int(Gd.At(Me, 25)))), ((IsInv && (I < GameSim.HOTBAR)) ? Gd.Str((I + 1)) : ""));
        }
        if ((DragFrom != null))
        {
            dynamic Box = (Gd.Equal(Gd.At(DragFrom, 0), "inv") ? Gd.At(Me, 7) : Sto);
            int I = Gd.Int(Gd.At(DragFrom, 1));
            if ((I < Gd.Size(Box)))
            {
                this.DrawSlot(C, Gd.MakeRect2((Mouse - (Vector2.One * (SLOT / 2))), (Vector2.One * SLOT)), Gd.At(Box, I), true, "");
            }
        }
    }

    public dynamic SlotAt(dynamic Vs, dynamic Me, Vector2 At)
    {
        foreach (dynamic S in this.BagSlots(Vs, Me))
        {
            if (((Rect2)(Gd.At(S, 2))).HasPoint(At))
            {
                return S;
            }
        }
        return null;
    }

    // Left mouse in the bag panel: press picks a slot up, release drops it on another slot.
    public bool InventoryMouse(dynamic Ev)
    {
        if ((!Gd.Truth(BagOpen) || (Ev.ButtonIndex != 1)))
        {
            return false;
        }
        dynamic Me = Gd.Get(Snap.Ps, MyId);
        if ((Me == null))
        {
            return false;
        }
        dynamic Vs = Hud.Size;
        dynamic Hit = this.SlotAt(Vs, Me, (Vector2)(Hud.GetLocalMousePosition()));
        if (Gd.Truth(Ev.Pressed))
        {
            DragFrom = ((Hit != null) ? (dynamic)(new GArray(Gd.At(Hit, 0), Gd.At(Hit, 1))) : null);
            return true;
        }
        if (((DragFrom != null) && (Hit != null)))
        {
            this.Act("move_item", new GArray(Gd.At(DragFrom, 0), Gd.At(DragFrom, 1), Gd.At(Hit, 0), Gd.At(Hit, 1)));
        }
        DragFrom = null;
        return true;
    }
}
