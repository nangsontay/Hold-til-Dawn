using Godot;
using Environment = Godot.Environment;
using FileAccess = Godot.FileAccess;
using System;
using System.Threading.Tasks;
using HoldTilDawn;

// Owner: Pham Minh Hieu. Items, the 12-slot inventory with its 4-slot hotbar, shared base storage,
// giving, dropping and picking up (MEC-09, BAL-18, ITM-01..13).
public partial class GameSim : RefCounted
{
    public const int INV_SLOTS = 12;
    public const int HOTBAR = 4;
    public const int STORAGE_SLOTS = 40;
    public const int STACK = 20;

    // id -> [name, stack size, kind]; kind is res, use, ammo, weapon, tool or armour.
    public static readonly GDict ITEMS = new GDict {
        { "wood", new GArray("Wood", STACK, "res") },
        { "stone", new GArray("Stone", STACK, "res") },
        { "herb", new GArray("Herb", STACK, "res") },
        { "metal", new GArray("Metal", STACK, "res") },
        { "bandage", new GArray("Bandage", STACK, "use") },
        { "arrow", new GArray("Arrow", STACK, "ammo") },

        { "stone_spear", new GArray("Stone spear", 1, "weapon") },
        { "metal_blade", new GArray("Metal blade", 1, "weapon") },
        { "starter_bow", new GArray("Starter bow", 1, "weapon") },
        { "bow", new GArray("Bow", 1, "weapon") },
        { "hammer", new GArray("Hammer", 1, "weapon") },
        { "light_armour", new GArray("Light armour", 1, "armour") },
        { "stone_armour", new GArray("Stone armour", 1, "armour") },
        { "metal_armour", new GArray("Metal armour", 1, "armour") },
    };

    // id -> [damage, attacks per second, reach or range in tiles, ranged, uses arrows] (BAL-08..10, BAL-42)
    public static readonly GDict WEAPON_STATS = new GDict {
        // bare hands (no item): the GDD's basic weapon numbers (BAL-08), so the balance of FML-03 is unchanged
        { "fists", new GArray(15.0f, 1.5f, 1.5f, false, false) },
        { "stone_spear", new GArray(20.0f, 1.5f, 1.5f, false, false) },
        { "metal_blade", new GArray(26.0f, 1.5f, 1.5f, false, false) },
        { "hammer", new GArray(12.0f, 1.2f, 1.5f, false, false) },
        { "starter_bow", new GArray(8.0f, 0.75f, 7.0f, true, false) },
        { "bow", new GArray(12.0f, 1.0f, 8.0f, true, true) },
    };

    // armour items by tier; ARMOUR holds the matching values (BAL-11)
    public static readonly GArray ARMOUR_ITEMS = new GArray("", "light_armour", "stone_armour", "metal_armour");

    // Team storage inside the base (BAL-18).
    public dynamic Storage = GameSim.EmptySlots(STORAGE_SLOTS);

    public static GArray EmptySlots(int N)
    {
        var Slots = new GArray();
        for (int I = 0; I < N; I++)
        {
            Slots.Add(null);
        }
        return Slots;
    }

    public static string ItemName(dynamic Id)
    {
        return (Gd.Truth(ITEMS.Has(Id)) ? (string)Gd.At(ITEMS[Id], 0) : Gd.Str(Id));
    }

    public static int StackOf(dynamic Id)
    {
        return (Gd.Truth(ITEMS.Has(Id)) ? Gd.Int(Gd.At(ITEMS[Id], 1)) : 1);
    }

    public static string KindOf(dynamic Id)
    {
        return (Gd.Truth(ITEMS.Has(Id)) ? (string)Gd.At(ITEMS[Id], 2) : "");
    }

    // --- slot arrays (a player's inventory or the storage) -------------------
    public static int CountIn(dynamic Slots, dynamic Id)
    {
        int N = 0;
        foreach (dynamic S in Gd.Iter(Slots))
        {
            if (((S != null) && Gd.Equal(Gd.At(S, 0), Id)))
            {
                N += Gd.Int(Gd.At(S, 1));
            }
        }
        return N;
    }

    public static int RoomIn(dynamic Slots, dynamic Id)
    {
        int Room = 0;
        int Stack = GameSim.StackOf(Id);
        foreach (dynamic S in Gd.Iter(Slots))
        {
            if ((S == null))
            {
                Room += Stack;
            }
            else if (Gd.Equal(Gd.At(S, 0), Id))
            {
                Room += (Stack - Gd.Int(Gd.At(S, 1)));
            }
        }
        return Room;
    }

    // Adds up to N; returns what did not fit. Tops up existing stacks first, then empty slots.
    public static int AddTo(dynamic Slots, dynamic Id, int N)
    {
        int Stack = GameSim.StackOf(Id);
        for (int I = 0; ((I < Gd.Size(Slots)) && (N > 0)); I++)
        {
            dynamic S = Gd.At(Slots, I);
            if (((S != null) && Gd.Equal(Gd.At(S, 0), Id) && (Gd.At(S, 1) < Stack)))
            {
                int Put = Math.Min(N, (Stack - Gd.Int(Gd.At(S, 1))));
                Slots[I] = new GArray(Id, (Gd.Int(Gd.At(S, 1)) + Put));
                N -= Put;
            }
        }
        for (int I = 0; ((I < Gd.Size(Slots)) && (N > 0)); I++)
        {
            if ((Gd.At(Slots, I) == null))
            {
                int Put = Math.Min(N, Stack);
                Slots[I] = new GArray(Id, Put);
                N -= Put;
            }
        }
        return N;
    }

    // Removes N if there are that many (from the last slots first); otherwise removes nothing.
    public static bool TakeFrom(dynamic Slots, dynamic Id, int N)
    {
        if ((GameSim.CountIn(Slots, Id) < N))
        {
            return false;
        }
        for (int I = (Gd.Size(Slots) - 1); ((I >= 0) && (N > 0)); I--)
        {
            dynamic S = Gd.At(Slots, I);
            if (((S != null) && Gd.Equal(Gd.At(S, 0), Id)))
            {
                int Got = Math.Min(N, Gd.Int(Gd.At(S, 1)));
                N -= Got;
                Slots[I] = (((Gd.Int(Gd.At(S, 1)) - Got) > 0) ? new GArray(Id, (Gd.Int(Gd.At(S, 1)) - Got)) : null);
            }
        }
        return true;
    }

    // --- a player's inventory --------------------------------------------------
    public int Count(dynamic P, dynamic Id)
    {
        return GameSim.CountIn(P.Inv, Id);
    }

    public int Room(dynamic P, dynamic Id)
    {
        return GameSim.RoomIn(P.Inv, Id);
    }

    public bool Take(dynamic P, dynamic Id, dynamic N)
    {
        return GameSim.TakeFrom(P.Inv, Id, Gd.Int(N));
    }

    // Puts items in the inventory; whatever does not fit is dropped at the player's feet.
    public void AddItem(dynamic P, dynamic Id, dynamic N)
    {
        int Left = GameSim.AddTo(P.Inv, Id, Gd.Int(N));
        if ((Left > 0))
        {
            this.PlayerDrop(P.Pos, Id, Left);
            this.Note(P, "Inventory full - dropped at your feet");
        }
    }

    // Sets how many of an item a player carries (tests and debug).
    public void SetCount(dynamic P, dynamic Id, dynamic N)
    {
        GameSim.TakeFrom(P.Inv, Id, this.Count(P, Id));
        GameSim.AddTo(P.Inv, Id, Gd.Int(N));
    }

    public void GiveStartingKit(dynamic P)
    {
        // Team change to BAL-42: no basic weapon item; everyone fights bare-handed when the selected slot holds
        // no weapon. Scout: starter bow; Engineer: hammer; Healer: empty hands (the flasks are not an item).
        P.Inv = GameSim.EmptySlots(INV_SLOTS);
        if ((P.Role == 0))
        {
            P.Inv[0] = new GArray("starter_bow", 1);
        }
        else if ((P.Role == 2))
        {
            P.Inv[0] = new GArray("hammer", 1);
        }
        P.Sel = 0;
    }

    // The weapon in hand: the item in the selected hotbar slot if it is a weapon, otherwise bare hands.
    public string WeaponOf(dynamic P)
    {
        dynamic S = Gd.At(P.Inv, Gd.Int(P.Sel));
        if (((S != null) && (GameSim.KindOf(Gd.At(S, 0)) == "weapon")))
        {
            return Gd.At(S, 0);
        }
        return "fists";
    }

    public static string WeaponName(dynamic Id)
    {
        return (Gd.Equal(Id, "fists") ? "Bare hands" : GameSim.ItemName(Id));
    }

    // Armour is worn from the inventory: the best piece carried counts (BAL-11).
    public int ArmourTier(dynamic P)
    {
        for (int Tier = 3; Tier > 0; Tier--)
        {
            if ((this.Count(P, Gd.At(ARMOUR_ITEMS, Tier)) > 0))
            {
                return Tier;
            }
        }
        return 0;
    }

    // Storage is reachable at the workbench: inside the base by day, or as the Engineer inside at night.
    public bool AtStorage(dynamic P)
    {
        return this.AtBench(P);
    }

    // Drag and drop between the inventory ("inv") and storage ("sto"): swaps, or merges equal stacks.
    public void MoveSlot(dynamic P, dynamic Arg)
    {
        if (((P.State != 0) || !Gd.IsType(Arg, typeof(GArray)) || (Gd.Size(Arg) < 4)))
        {
            return;
        }
        bool FromSto = Gd.Equal(Gd.At(Arg, 0), "sto");
        bool ToSto = Gd.Equal(Gd.At(Arg, 2), "sto");
        dynamic From = (FromSto ? Storage : P.Inv);
        dynamic To = (ToSto ? Storage : P.Inv);
        if (((FromSto || ToSto) && !this.AtStorage(P)))
        {
            this.Note(P, "Storage is at the workbench inside the base");
            return;
        }
        int A = Gd.Int(Gd.At(Arg, 1));
        int B = Gd.Int(Gd.At(Arg, 3));
        if (((A < 0) || (A >= Gd.Size(From)) || (B < 0) || (B >= Gd.Size(To)) || ((FromSto == ToSto) && (A == B))))
        {
            return;
        }
        dynamic Sa = Gd.At(From, A);
        dynamic Sb = Gd.At(To, B);
        if ((Sa == null))
        {
            return;
        }
        if (((Sb != null) && Gd.Equal(Gd.At(Sa, 0), Gd.At(Sb, 0))))
        {
            int Stack = GameSim.StackOf(Gd.At(Sa, 0));
            int Put = Math.Min(Gd.Int(Gd.At(Sa, 1)), (Stack - Gd.Int(Gd.At(Sb, 1))));
            To[B] = new GArray(Gd.At(Sb, 0), (Gd.Int(Gd.At(Sb, 1)) + Put));
            From[A] = (((Gd.Int(Gd.At(Sa, 1)) - Put) > 0) ? new GArray(Gd.At(Sa, 0), (Gd.Int(Gd.At(Sa, 1)) - Put)) : null);
            return;
        }
        From[A] = Sb;
        To[B] = Sa;
    }

    // G: hand the selected stack to the nearest teammate in reach (1.5 tiles), otherwise drop it (MEC-09).
    public void GiveOrDrop(dynamic P)
    {
        int Sel = Gd.Int(P.Sel);
        dynamic S = Gd.At(P.Inv, Sel);
        if (((S == null) || (P.State != 0)))
        {
            return;
        }
        dynamic Best = null;
        float BestD = (1.5f * T);
        foreach (dynamic Q in Gd.Iter(Gd.Values(Players)))
        {
            if (((Q.Id != P.Id) && (Q.State == 0) && (Q.Pos.DistanceTo(P.Pos) <= BestD) && (GameSim.RoomIn(Q.Inv, Gd.At(S, 0)) > 0)))
            {
                BestD = Q.Pos.DistanceTo(P.Pos);
                Best = Q;
            }
        }
        P.Inv[Sel] = null;
        if ((Best != null))
        {
            int Left = GameSim.AddTo(Best.Inv, Gd.At(S, 0), Gd.Int(Gd.At(S, 1)));
            if ((Left > 0))
            {
                P.Inv[Sel] = new GArray(Gd.At(S, 0), Left);
            }
            this.Note(P, Gd.Format("Gave %s to %s", new GArray(GameSim.ItemName(Gd.At(S, 0)), Best.Name)));
            this.Note(Best, Gd.Format("%s gave you %s", new GArray(P.Name, GameSim.ItemName(Gd.At(S, 0)))));
            return;
        }
        this.PlayerDrop(P.Pos, Gd.At(S, 0), Gd.At(S, 1));
    }

    // Items dropped by players stay until someone picks them up with E (MEC-09).
    public void PlayerDrop(dynamic Pos, dynamic Id, dynamic N)
    {
        Gd.Append(Drops, new GDict { { "pos", Pos }, { "item", Id }, { "n", Gd.Int(N) }, { "ttl", (-1.0f) }, { "player", true } });
    }

    // E next to a dropped item: picks up as much as fits. Returns true when there was something to pick up.
    public bool PickUp(dynamic P)
    {
        foreach (dynamic D in Gd.Iter(Drops))
        {
            if ((Gd.Truth(Gd.Get(D, "player", false)) && (D.Pos.DistanceTo(P.Pos) <= (1.2f * T))))
            {
                int Left = GameSim.AddTo(P.Inv, D.Item, Gd.Int(D.N));
                if ((Left == Gd.Int(D.N)))
                {
                    this.Note(P, "Inventory full");
                    return true;
                }
                D.N = Left;
                Drops = Gd.Filter(Drops, (Func<dynamic, dynamic>)((X) => (X.N > 0)));
                return true;
            }
        }
        return false;
    }
}
