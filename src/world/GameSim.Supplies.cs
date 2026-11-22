using Godot;
using System;
using System.Collections.Generic;
using HoldTilDawn;

public partial class GameSim
{
    public static readonly string[] SUPPLY_NAMES = { "Radio battery", "Antenna assembly", "Locator circuit" };
    public static readonly string[] COMPONENT_IDS = { "battery_cells", "antenna_assembly", "locator_circuit" };
    public static readonly string[] RESCUE_IDS = { "locator_radio" };
    public const int SUPPLY_COUNT = 3;
    public const float SUPPLY_PICK = 1.1f;
    public const float RADIO_BUILD_TIME = 3f;
    public const int EXPLORATION_CHEST_SLOTS = 8;
    public const float CHEST_UNLOCK_TIME = 2f;
    public const float CHEST_REACH = 44f;
    public static readonly string[] CHEST_SUPPORT = {
        "bandage", "revive", "light_armour", "stone_armour", "titanium_armour", "vine_trap",
        "poison_flask", "healing_flask", "haste_tonic", "gathering_kit", "repair_kit"
    };
    public sealed class ExplorationChest
    {
        public int Id, Landmark, Component = -1;
        public Vector2 Pos;
        public bool Opened;
        public float OpenedAt;
        public int? Unlocker;
        public float UnlockProgress;
        public readonly GArray Slots = EmptySlots(EXPLORATION_CHEST_SLOTS);
        public bool Empty => Slots.All(S => S == null);
    }
    public readonly List<ExplorationChest> ExplorationChests = new();
    public readonly int[] RescueComponents = new int[3];
    public bool RadioBuilt;
    public int? RadioBuilder;
    public float RadioBuildT;
    Vector2 RadioBuildPos;
    float RadioBuildDamage;
    readonly List<Rect2> ChestScenery = new();
    readonly List<Vector2> ChestResources = new();
    static Dictionary<string, Vector2> ChestPropAnchors;
    public static int RescueKind(string Id) => Array.IndexOf(RESCUE_IDS, Id);
    public int SuppliesGot()
    {
        int Count = 0; foreach (int N in RescueComponents) if (N > 0) Count++; return Count;
    }
    public bool SuppliesAllIn() => RadioBuilt;
    public int CraftHave(dynamic P, string Id)
    {
        int Kind = Array.IndexOf(COMPONENT_IDS, Id);
        return Kind >= 0 ? RescueComponents[Kind] : Count(P, Id);
    }
    public void CancelRadioBuild() { RadioBuilder = null; RadioBuildT = 0; }
    public void ClearSupplies()
    {
        ExplorationChests.Clear(); Array.Clear(RescueComponents); RadioBuilt = false;
        CancelRadioBuild();
        foreach (dynamic P in Gd.Iter(Gd.Values(Players))) { CancelChestUnlock(P); P.OpenChest = -1; P.ChestUseSeq = 0; }
    }
    public void PlaceSupplies()
    {
        ClearSupplies();
        CacheChestScenery();
        var Random = (RandomNumberGenerator)Gd.Unwrap((object)Rng);
        var Places = new List<IslandMap.Landmark>(IslandMap.Landmarks);
        Places.RemoveAll(L => L.Id <= 4);
        for (int I = Places.Count - 1; I > 0; I--)
        {
            int J = Random.RandiRange(0, I); (Places[I], Places[J]) = (Places[J], Places[I]);
        }
        var Support = (string[])CHEST_SUPPORT.Clone();
        for (int I = Support.Length - 1; I > 0; I--)
        {
            int J = Random.RandiRange(0, I); (Support[I], Support[J]) = (Support[J], Support[I]);
        }
        for (int I = 0; I < 20; I++)
        {
            var C = new ExplorationChest { Id = I, Landmark = I < Places.Count ? Places[I].Id : 0,
                Pos = I < Places.Count ? ChestSpot(Places[I], Random.RandfRange(0, Mathf.Tau)) : ScatteredChestSpot(Random) };
            AddTo(C.Slots, "wood", Random.RandiRange(4,10)); AddTo(C.Slots, "stone", Random.RandiRange(3,8));
            AddTo(C.Slots, "herb", Random.RandiRange(2,5)); AddTo(C.Slots, "titanium", Random.RandiRange(1,3));
            // Guaranteed variety, one support stack per chest at most. Total loot fits the eight real slots.
            if (I < Support.Length || Random.Randf() < .35f) AddTo(C.Slots, Support[I % Support.Length], 1);
            if (I < 3) { C.Component = I; AddTo(C.Slots, COMPONENT_IDS[I], 1); }
            ExplorationChests.Add(C);
        }
    }
    Vector2 ScatteredChestSpot(RandomNumberGenerator Random)
    {
        var W = (WorldGeometry)World;
        for (int Attempt = 0; Attempt < 20000; Attempt++)
        {
            Vector2 At = (IslandMap.Box.Position + new Vector2(Random.Randf(), Random.Randf()) * IslandMap.Box.Size).Round();
            bool Remote = true;
            foreach (var (Id, Centre) in W.LmAt)
                if (At.DistanceSquaredTo(Centre) < 520*520) { Remote = false; break; }
            if (Remote && ChestSpotFree(At, ChestScenery)) return At;
        }
        throw new InvalidOperationException("No accessible scattered chest spot");
    }
    Vector2 ChestSpot(IslandMap.Landmark L, float Angle)
    {
        var W = (WorldGeometry)World;
        Vector2 Centre = W.LmAt.GetValueOrDefault(L.Id, L.Pos);
        var Search = new Rect2(Centre - Vector2.One*432, Vector2.One*864);
        var Scenery = ChestScenery.FindAll(B => B.Intersects(Search));
        for (float Ring = 2; Ring <= 12; Ring += .75f)
            for (int I = 0; I < 24; I++)
            {
                Vector2 At = (Centre + Vector2.FromAngle(Angle + I * Mathf.Tau / 24) * Ring * 32).Round();
                if (ChestSpotFree(At, Scenery)) return At;
            }
        throw new InvalidOperationException($"No accessible chest spot at landmark {L.Id}");
    }
    bool ChestSpotFree(Vector2 At, List<Rect2> Scenery)
    {
        var Footprint = new Rect2(At - new Vector2(20,33), new Vector2(40,40)).Grow(8);
        foreach (var Bounds in Scenery) if (Bounds.Intersects(Footprint)) return false;
        foreach (Vector2 Resource in ChestResources) if (Resource.DistanceSquaredTo(At) < 56*56) return false;
        foreach (var Other in ExplorationChests) if (Other.Pos.DistanceSquaredTo(At) < 160*160) return false;
        var W = (WorldGeometry)World;
        if (W.WeatherRoom.Grow(48).HasPoint(At) || !IslandMap.OnLand(At,32) || W.TerrainAt(At) == 1
            || !W.ResolveFast(At,26).IsEqualApprox(At) || Gd.Truth(InsideBase(At)) || IslandMap.InBridgeApproach(At,32)) return false;
        return !W.InCaveRegion(At) || W.InCaveFloor(At);
    }
    // Physical trunks/vehicle bodies are smaller than their pictures. Reserve the complete artwork too.
    // Cache once at placement, with no texture loads, scene nodes or per-frame scenery scans.
    void CacheChestScenery()
    {
        ChestScenery.Clear();
        ChestResources.Clear();
        if (ChestPropAnchors == null)
        {
            ChestPropAnchors = new();
            var Index = Json.ParseString(Godot.FileAccess.GetFileAsString("res://art/landmarks/index.json")).AsGodotDictionary();
            foreach (var Key in Index.Keys)
            {
                var Meta = Index[Key].AsGodotArray();
                ChestPropAnchors[Key.AsString()] = new Vector2(Meta[0].AsSingle(), Meta[1].AsSingle());
            }
        }
        var W = (WorldGeometry)World;
        foreach (var (At, Sprite) in W.LmProps)
        {
            Vector2 Anchor = ChestPropAnchors.GetValueOrDefault(Sprite, new Vector2(32,48));
            ChestScenery.Add(new Rect2(At - Anchor, new Vector2(Anchor.X*2,Anchor.Y+12)));
        }
        foreach (var V in W.ExpeditionVehicles)
            ChestScenery.Add(new Rect2(V.Pos - new Vector2(112,120), new Vector2(224,150)));
        foreach (var Cliff in W.ExpeditionCliffs) AddBounds(Cliff.Cap);
        foreach (var B in W.CompoundBoundaries) AddBounds(B.Face);
        foreach (var O in W.LookoutObjects)
            ChestScenery.Add(new Rect2(O.Pos - (O.Model == 0 ? new Vector2(88,110) : new Vector2(38,106)),
                O.Model == 0 ? new Vector2(176,140) : new Vector2(76,125)));
        foreach (GDict Node in (GArray)Nodes)
        {
            int Type = Gd.Int(Node.Get("type")); Vector2 At = (Vector2)Node.Get("pos");
            ChestResources.Add(At);
            float Width = Type == 0 ? 42 : Type == 2 ? 18 : 28;
            float Height = Type == 0 ? 110 : Type == 2 ? 26 : 60;
            ChestScenery.Add(new Rect2(At - new Vector2(Width,Height), new Vector2(Width*2,Height+10)));
        }
    }
    void AddBounds(Vector2[] Points)
    {
        if (Points.Length == 0) return;
        Vector2 Lo = Points[0], Hi = Points[0];
        foreach (Vector2 P in Points) { Lo = Lo.Min(P); Hi = Hi.Max(P); }
        ChestScenery.Add(new Rect2(Lo,Hi-Lo));
    }
    public ExplorationChest ChestById(int Id) => Id >= 0 && Id < ExplorationChests.Count ? ExplorationChests[Id] : null;
    public bool InChestReach(dynamic P, ExplorationChest C) => C != null && P.State == 0
        && (string)Phase is "day" or "dusk" or "night" && !Gd.Truth(P.Inside)
        && ((Vector2)P.Pos).DistanceSquaredTo(C.Pos) <= CHEST_REACH*CHEST_REACH && Gd.Truth(LineClear(P.Pos,C.Pos));
    public bool CanAccessChest(dynamic P, ExplorationChest C) => C != null && C.Opened && InChestReach(P,C);
    public void OpenChestPanel(dynamic P, int Id)
    {
        var C = ChestById(Id);
        if (!CanAccessChest(P,C)) return;
        CancelChestUnlock(P); P.OpenChest = Id; P.ChestUseSeq = Gd.Int(Gd.Get(P,"chest_use_seq",0)) + 1;
        P.Inter = false; P.Activity = "loot"; P.ActivityTarget = C.Pos;
    }
    public void CancelChestUnlock(dynamic P)
    {
        var C = ChestById(Gd.Int(Gd.Get(P,"unlock_chest",-1)));
        if (C != null && C.Unlocker == Gd.Int(P.Id)) { C.Unlocker = null; C.UnlockProgress = 0; }
        P.UnlockChest = -1; P.UnlockT = 0f;
    }
    bool UnlockInterrupted(dynamic P, ExplorationChest C) => !InChestReach(P,C) || !Gd.Truth(P.Inter) || Gd.Truth(P.Atk)
        || P.CraftIdx >= 0 || Gd.Float(P.Channel) > 0 || Count(P,"chest_key") == 0
        || ((Vector2)P.Pos).DistanceSquaredTo((Vector2)P.UnlockPos) > 16 || Gd.Float(P.Taken) != Gd.Float(P.UnlockDamage);
    void TickChestAccess()
    {
        foreach (dynamic P in Gd.Iter(Gd.Values(Players)))
        {
            int Id = Gd.Int(Gd.Get(P,"unlock_chest",-1));
            if (Id >= 0 && UnlockInterrupted(P,ChestById(Id)))
            { CancelChestUnlock(P); P.Inter = false; }
            if (Gd.Int(Gd.Get(P,"open_chest",-1)) >= 0 && !CanAccessChest(P,ChestById(Gd.Int(P.OpenChest)))) P.OpenChest = -1;
        }
    }
    public bool TryLootChest(dynamic P, float Dt = .05f)
    {
        string CurrentPhase = Phase;
        if (P.State != 0 || CurrentPhase is not ("day" or "dusk" or "night")) return false;
        foreach (var C in ExplorationChests)
        {
            if (!InChestReach(P,C)) continue;
            if (C.Opened) { OpenChestPanel(P,C.Id); return true; }
            if (Count(P,"chest_key") == 0) { Note(P,"Locked - craft a chest key (2 wood + 2 stone)"); P.Inter = false; return true; }
            int PlayerId = Gd.Int(P.Id);
            if (C.Unlocker.HasValue && C.Unlocker != PlayerId) { Note(P,"A teammate is unlocking this chest"); return true; }
            if (Gd.Int(P.UnlockChest) != C.Id)
            {
                CancelChestUnlock(P); P.UnlockChest = C.Id; P.UnlockPos = P.Pos; P.UnlockDamage = P.Taken; C.Unlocker = PlayerId;
            }
            if (UnlockInterrupted(P,C)) { CancelChestUnlock(P); return true; }
            P.UnlockT += Math.Max(0,Dt); C.UnlockProgress = Math.Min(1,Gd.Float(P.UnlockT)/CHEST_UNLOCK_TIME);
            P.Activity = "loot"; P.ActivityTarget = C.Pos;
            if (Gd.Float(P.UnlockT) + .00001f >= CHEST_UNLOCK_TIME && Take(P,"chest_key",1))
            {
                C.Opened = true; C.OpenedAt = Gd.Float(Clock); CancelChestUnlock(P); OpenChestPanel(P,C.Id);
                Note(P,"Chest unlocked - drag only the items you want into your bag");
            }
            return true;
        }
        CancelChestUnlock(P);
        return false;
    }
    // Only physically reachable, unlocked contents are published for the player's storage panel.
    public GDict ChestContentsSnapshot()
    {
        var Views = new GDict();
        foreach (dynamic P in Gd.Iter(Gd.Values(Players)))
        {
            ExplorationChest C = ChestById(Gd.Int(Gd.Get(P,"open_chest",-1)));
            if (CanAccessChest(P,C)) Views[P.Id] = new GArray(C.Id,new GArray(C.Slots.Select(S => S is GArray A ? new GArray(A.ToArray()) : S).ToArray()));
        }
        return Views;
    }
    public bool TryBuildRadio(dynamic P, float Dt)
    {
        if (RadioBuilt || P.State != 0 || !AtBench(P)) return false;
        if ((string)Phase != "day")
        { Note(P, "Assemble the locator radio by day, before night 5"); return true; }
        if (SuppliesGot() < 3) { Note(P, "Find all 3 radio parts in landmark chests (" + SuppliesGot() + "/3)"); return true; }
        if (P.CraftIdx >= 0) { Note(P, "Finish crafting before assembling the radio"); return true; }
        int Id = Gd.Int(P.Id);
        if (RadioBuilder.HasValue && RadioBuilder != Id) { Note(P, "A teammate is assembling the radio"); return true; }
        if (!RadioBuilder.HasValue) { RadioBuilder = Id; RadioBuildPos = P.Pos; RadioBuildDamage = P.Taken; }
        if (((Vector2)P.Pos).DistanceSquaredTo(RadioBuildPos) > 16 || Gd.Float(P.Taken) != RadioBuildDamage)
        { CancelRadioBuild(); return true; }
        RadioBuildT += Dt; P.Activity = "craft"; P.ActivityTarget = WORKBENCH_POS;
        if (RadioBuildT >= RADIO_BUILD_TIME)
        {
            RadioBuilt = true; CancelRadioBuild();
            Say("Locator radio online. Rescue is confirmed for night 5 - defend a landing pad!");
        }
        return true;
    }
    // Public chest state only. Contents and component assignments stay hidden.
    public GArray SupplySnapshot()
    {
        var Rows = new GArray();
        foreach (var C in ExplorationChests) Rows.Add(new GArray(C.Id, C.Pos, C.Empty, C.Landmark, C.Opened,
            C.UnlockProgress, C.Opened ? Mathf.Clamp((Gd.Float(Clock)-C.OpenedAt)/.35f,0,1) : 0));
        return Rows;
    }
    public GArray RescueSnapshot() => new(new GArray(RescueComponents[0], RescueComponents[1], RescueComponents[2]),
        RadioBuilt, RadioBuildT / RADIO_BUILD_TIME);
}
