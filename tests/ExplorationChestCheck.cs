using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
using HoldTilDawn;

public partial class ExplorationChestCheck : Node
{
    int Checks;
    void Check(bool Pass, string Label) { if (!Pass) throw new Exception(Label); Checks++; GD.Print("ok   " + Label); }
    public override void _Ready()
    {
        try { Run(); GD.Print($"EXPLORATION CHESTS ALL OK: {Checks} checks"); GetTree().Quit(); }
        catch (Exception E) { GD.PushError(E.ToString()); GetTree().Quit(1); }
    }
    void Run()
    {
        using var S = new GameSim(); S.AddPlayer(1,"Scout",0); S.AddPlayer(2,"Engineer",2);
        S.AddPlayer(-1,"Bot helper",1); S.StartMatch();
        var W = (WorldGeometry)S.World; var Seen = new HashSet<int>(); var Amounts = new HashSet<int>();
        Check(W.LmAt.Keys.Count(Id => Id != 1) == 17, "all 17 expedition and farm landmarks remain built outside the outpost");
        var ResourcePositions = ((GArray)S.Nodes).Cast<GDict>().Select(N => (Vector2)N.Get("pos")).ToArray();
        for (int Seed = 0; Seed < 64; Seed++)
        {
            S.Rng.Seed = (ulong)Seed; S.PlaceSupplies();
            if (S.ExplorationChests.Count != 20 || S.ExplorationChests.Count(C => C.Landmark == 0) != 6
                || S.ExplorationChests.Where(C => C.Landmark != 0).GroupBy(C => C.Landmark).Any(G => G.Count() != 1 || G.Key <= 4))
                throw new Exception("Expected one chest at each of 14 remote landmarks and six scattered chests");
            var Parts = S.ExplorationChests.Where(C => C.Component >= 0).ToList();
            if (Parts.Count != 3 || Parts.Select(C => C.Component).Distinct().Count() != 3
                || Parts.Select(C => C.Landmark).Distinct().Count() != 3 || Parts.Any(C => C.Landmark <= 4))
                throw new Exception("Radio parts must belong to three different landmark chests, never scattered chests");
            foreach (var C in S.ExplorationChests)
            {
                if (!IslandMap.OnLand(C.Pos,32) || !W.ResolveFast(C.Pos,26).IsEqualApprox(C.Pos)
                    || W.WeatherRoom.Grow(48).HasPoint(C.Pos) || IslandMap.InBridgeApproach(C.Pos,32))
                    throw new Exception("Chest obstructed, wet, inside the weather display or on a bridge approach");
                if (ResourcePositions.Any(P => P.DistanceSquaredTo(C.Pos) < 44*44)) throw new Exception("Chest overlaps a harvestable resource");
                if (C.Slots.Count != 8 || C.Slots.Any(A => A is GArray Item && Gd.Int(Item[1]) > GameSim.StackOf(Item[0]))) throw new Exception("Eight bounded storage slots required");
                foreach (string Id in new[] { "wood", "stone", "herb", "titanium" })
                    if (GameSim.CountIn(C.Slots,Id) < 1) throw new Exception("Every chest needs all four resources");
                if (C.Landmark == 0 && W.LmAt.Values.Any(At => At.DistanceSquaredTo(C.Pos) < 520*520)) throw new Exception("Scattered chest too near a landmark or home farm");
                if (C.Component >= 0 && GameSim.CountIn(C.Slots,GameSim.COMPONENT_IDS[C.Component]) != 1) throw new Exception("Part missing from physical storage");
                Amounts.Add(GameSim.CountIn(C.Slots,"wood"));
            }
            if (!GameSim.CHEST_SUPPORT.All(Id => S.ExplorationChests.Any(C => GameSim.CountIn(C.Slots,Id)>0)))
                throw new Exception("Every loot-only support item must have a source");
            foreach (var C in Parts) Seen.Add(C.Landmark);
            foreach (GArray Row in S.SupplySnapshot())
                if (Row.Count != 7 || Gd.Int(Row[0]) != S.ExplorationChests[Gd.Int(Row[0])].Id || Row.Any(A => A is GArray)) throw new Exception("Public chest snapshot leaks loot");
            if (S.ChestContentsSnapshot().Count != 0) throw new Exception("Locked contents leaked into the UI");
        }
        Check(Seen.Count == 14 && Amounts.Count > 4, "64 seeds: 20 accessible eight-slot chests, one per landmark, six scattered, all part sites vary");
        Check(S.ExplorationChests.All(C => !C.Opened), "walking near a chest never unlocks or collects it");
        dynamic P = S.Players[1], Q = S.Players[2];
        var PartChest = S.ExplorationChests.First(C => C.Component >= 0); int Part = PartChest.Component;
        P.Pos = PartChest.Pos + new Vector2(100,0); P.Inter = true;
        Check(!S.TryLootChest(P,3), "holding E cannot unlock a distant chest");
        P.Pos = PartChest.Pos; P.Inv = GameSim.EmptySlots(8); P.Inter = true;
        S.TryLootChest(P,3); Check(!PartChest.Opened && S.ChestContentsSnapshot().Count == 0, "a key is mandatory and locked contents stay hidden");
        S.SetCount(P,"chest_key",2); P.Inter = true; S.TryLootChest(P,1);
        Check(!PartChest.Opened && P.UnlockT == 1 && S.Count(P,"chest_key") == 2, "first second only progresses the lock without consuming a key");
        Q.Pos = PartChest.Pos; Q.Inter = true; S.SetCount(Q,"chest_key",1); S.TryLootChest(Q,2);
        Check(!PartChest.Opened && P.UnlockT == 1 && S.Count(Q,"chest_key") == 1, "another teammate cannot accelerate or pay for the same lock");
        Q.Pos = GameSim.WORKBENCH_POS; Q.Inter = false;
        P.Inter = false; S.TickPlayers(.01f);
        Check(P.UnlockT == 0 && PartChest.Unlocker == null && S.Count(P,"chest_key") == 2, "release E cancels lock progress and keeps the key");
        P.Inter = true; S.TryLootChest(P,1); P.Pos += new Vector2(8,0); S.TickPlayers(.01f);
        Check(P.UnlockT == 0 && !PartChest.Opened, "moving interrupts unlocking");
        P.Pos = PartChest.Pos; P.Inter = true; S.TryLootChest(P,1); P.Invuln = 0; S.Hurt(P,1);
        Check(P.UnlockT == 0 && S.Count(P,"chest_key") == 2, "damage cancels unlocking without spending keys");
        P.Inter = true; S.TryLootChest(P,1); P.Atk = true; S.TickPlayers(.01f); P.Atk = false;
        Check(P.UnlockT == 0 && !PartChest.Opened, "attacking interrupts unlocking");
        P.Inter = true; S.Phase = "night"; S.TryLootChest(P,1.99f);
        Check(!PartChest.Opened && S.Count(P,"chest_key") == 2, "lock requires the full two seconds, including at night");
        S.TryLootChest(P,.01f);
        Check(PartChest.Opened && S.Count(P,"chest_key") == 1 && P.OpenChest == PartChest.Id, "two seconds consumes exactly one key and opens the selection panel");
        Check(S.SuppliesGot() == 0 && S.Count(P,"wood") == 0 && GameSim.CountIn(PartChest.Slots,"wood") > 0, "unlocking takes no resources or radio parts automatically");
        var View = (GArray)S.ChestContentsSnapshot().Get(1);
        Check(View != null && ((GArray)View[1]).Count == 8, "reachable unlocked chest publishes exactly eight slots");
        ((GArray)View[1])[0] = null;
        Check(PartChest.Slots[0] != null, "UI snapshot cannot mutate the chest inventory");
        int Wood = GameSim.CountIn(PartChest.Slots,"wood");
        S.MoveSlot(P,new GArray("chest:"+PartChest.Id,0,"inv",4));
        Check(S.Count(P,"wood") == Wood && GameSim.CountIn(PartChest.Slots,"wood") == 0 && S.Count(P,"stone") == 0, "dragging takes only the selected stack");
        S.MoveSlot(P,new GArray("inv",4,"chest:"+PartChest.Id,0));
        Check(S.Count(P,"wood") == 0 && GameSim.CountIn(PartChest.Slots,"wood") == Wood, "items can be stored back in a chest");
        P.Inv = new GArray(Enumerable.Range(0,8).Select(_ => (object)new GArray("wood",20)).ToArray());
        int PartSlot = Enumerable.Range(0,8).First(I => PartChest.Slots[I] is GArray A && Gd.Str(A[0]) == GameSim.COMPONENT_IDS[Part]);
        S.MoveSlot(P,new GArray("chest:"+PartChest.Id,PartSlot,"inv",0));
        Check(S.RescueComponents[Part] == 1 && PartChest.Component == -1 && S.Count(P,"wood") == 160, "explicitly taking a radio part shares it even with a full bag without replacing items");
        S.MoveSlot(P,new GArray("chest:"+PartChest.Id,PartSlot,"inv",0));
        Check(S.RescueComponents[Part] == 1 && GameSim.CountIn(PartChest.Slots,"stone") > 0, "repeat drags cannot duplicate parts or take unrelated loot");
        P.Inv = GameSim.EmptySlots(8); P.Inter = true; S.TryLootChest(P,10);
        Check(PartChest.Opened && S.Count(P,"chest_key") == 0, "unlocked chests reopen instantly without another key");
        P.Pos += new Vector2(100,0); S.MoveSlot(P,new GArray("chest:"+PartChest.Id,0,"inv",0));
        Check(S.Count(P,"wood") == 0 && S.ChestContentsSnapshot().Count == 0, "walking away hides contents and denies remote transfers");
        var Other = S.ExplorationChests.First(C => C.Component >= 0); P.State = 1; P.Pos = Other.Pos;
        Check(!S.TryLootChest(P,3) && !Other.Opened, "downed players cannot unlock chests"); P.State = 0; S.Phase = "day";
        S.MoveSlot(P,new GArray("chest:"+Other.Id,0,"inv",0));
        Check(S.Count(P,"wood") == 0, "a crafted transfer request cannot bypass the lock");
        S.MoveSlot(P,new GArray("nonsense",0,"inv",1));
        Check(S.Count(P,"wood") == 0, "unknown container names cannot fall back to the backpack");
        foreach (var C in S.ExplorationChests.Where(C => C.Component >= 0).ToList())
        {
            P.Pos = C.Pos; S.SetCount(P,"chest_key",1); P.Inter = true; S.TryLootChest(P,2);
            int Slot = Enumerable.Range(0,8).First(I => C.Slots[I] is GArray A && Array.IndexOf(GameSim.COMPONENT_IDS,Gd.Str(A[0])) >= 0);
            S.MoveSlot(P,new GArray("chest:"+C.Id,Slot,"inv",0));
        }
        Check(S.SuppliesGot() == 3 && !S.RadioBuilt, "three selected radio parts still require workbench assembly");
        S.Dawn(); Check(S.SuppliesGot() == 3 && PartChest.Opened && GameSim.CountIn(PartChest.Slots,"wood") == Wood, "dawn preserves unlocked storage, remaining loot and team parts");
        S.ResetPlayer(P); Check(S.SuppliesGot() == 3, "parts survive player death and reset");
        dynamic Helper = S.Players[-1]; Helper.Pos = GameSim.WORKBENCH_POS + new Vector2(-12,24); Helper.Inter = true;
        Q.Pos = GameSim.WORKBENCH_POS + new Vector2(0,24);
        S.TryBuildRadio(Helper,1); S.TryBuildRadio(Q,1);
        Check(S.RadioBuilder == -1 && S.RadioBuildT == 1, "negative bot IDs own the channel and a second teammate cannot speed it up");
        S.SetCount(Helper,"wood",1); S.SetCount(Q,"wood",1);
        Check(!S.CanCraft(Helper,0,false) && S.CanCraft(Q,0,false), "only the radio builder is prevented from crafting another item at the same time");
        Helper.Inter = false; S.TickPlayers(.01f);
        Check(S.RadioBuilder == null && S.RadioBuildT == 0, "a bot releasing E cancels the shared assembly channel");
        Q.Pos = GameSim.WORKBENCH_POS + new Vector2(0,24); S.Phase = "night"; Q.Inside = true;
        S.TryBuildRadio(Q,10); Check(!S.RadioBuilt && S.RadioBuildT == 0, "radio cannot be assembled at night");
        S.Phase = "day"; Q.Inside = false; Q.Inter = true;
        S.Interact(Q,1); Q.Inter = false; S.TickPlayers(.01f);
        Check(S.RadioBuildT == 0 && S.SuppliesGot() == 3, "releasing E cancels assembly without losing parts");
        Q.Inter = true; S.Interact(Q,1); Q.Pos += new Vector2(8,0); S.TickPlayers(.01f);
        Check(S.RadioBuildT == 0, "moving cancels radio assembly");
        Q.Pos = GameSim.WORKBENCH_POS + new Vector2(0,24); S.Interact(Q,1); Q.Invuln = 0; S.Hurt(Q,2);
        Check(S.RadioBuildT == 0 && !S.RadioBuilt, "damage interrupts assembly");
        S.K = 5; S.Phase = "day"; Q.Inter = true;
        S.Interact(Q,2.9f); Check(!S.RadioBuilt, "day 5 is allowed but assembly takes its full three seconds");
        S.Interact(Q,.2f); Check(S.RadioBuilt && S.RadioBuildT == 0, "one assembled radio uses all three team parts");
        S.Interact(Q,10); Check(S.RadioBuilt && S.SuppliesGot() == 3, "assembled radio cannot be duplicated or lose parts");
        S.Night(); Check(S.Phase == "night", "an online radio allows the final night to begin");
        S.Enemies = new GArray(); S.TimeLeft = 0; S.CheckEnd(.05f);
        Check(S.Phase == "result" && Gd.Truth(S.Result[0]), "radio plus a clear pad leads to rescue");
        S.StartMatch(); S.K = 5; S.Night();
        Check(S.Phase == "result" && !Gd.Truth(S.Result[0]), "night 5 enforces the radio deadline");
        S.StartMatch(); Check(!S.RadioBuilt && S.SuppliesGot() == 0 && S.ExplorationChests.All(C => !C.Opened), "new match resets radio and rerolls sealed chests");
        string[] Allowed = { "light_armour", "stone_armour", "titanium_armour", "bandage", "revive", "base", "healing_flask", "repair_kit", "gathering_kit", "crafting_kit", "chest_key" };
        Check(Allowed.All(Id => ((GArray)GameSim.RECIPES).Cast<GArray>().Any(R => Gd.Str(R[3]) == Id)), "armour, medicine, maintenance and workshop kits and keys are craftable");
        Check(new[]{"haste_tonic","poison_flask","vine_trap"}.All(Id => !((GArray)GameSim.RECIPES).Cast<GArray>().Any(R => Gd.Str(R[3]) == Id)), "speed, poison and traps remain loot only");
        Q.Pos = GameSim.WORKBENCH_POS + new Vector2(0,24); Q.Inv = GameSim.EmptySlots(8); S.Unlock = 2;
        S.SetCount(Q,"wood",6); S.SetCount(Q,"titanium",2); S.FinishCraft(Q,2);
        Check(S.Count(Q,"bow") == 1 && S.Count(Q,"arrow") == 0, "crafting a bow gives no free arrows");
        S.SetCount(Q,"wood",1); S.FinishCraft(Q,0);
        Check(S.Count(Q,"arrow") == 5, "arrows are crafted separately and load into the empty bow");
        S.SetCount(Q,"crafting_kit",1);
        Check(Mathf.IsEqualApprox(S.PlayerCraftDuration(Q,0),2.4f), "carried workshop kit reduces crafting time twenty percent");
        S.SetCount(Q,"crafting_kit",2);
        Check(Mathf.IsEqualApprox(S.PlayerCraftDuration(Q,0),2.4f), "workshop kit speed does not stack");
        S.BaseHp = 500; S.SetCount(Q,"stone",5); S.FinishCraft(Q,10);
        Check(S.BaseHp == 600 && S.Count(Q,"stone") == 0, "restored generator repair really restores HP and spends stone");
    }
}
