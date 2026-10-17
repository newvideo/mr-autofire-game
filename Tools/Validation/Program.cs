using System;
using Polyus;
using Polyus.Monetization;

static class Program
{
    static int passed;
    static void Test(string name, Action action)
    {
        try { action(); passed++; Console.WriteLine("PASS " + name); }
        catch (Exception e) { Console.Error.WriteLine("FAIL " + name + ": " + e.Message); Environment.Exit(1); }
    }
    static void Require(bool valid) { if (!valid) throw new Exception("Assertion failed"); }
    static void Main()
    {
        Test("Rewards and daily claims cannot duplicate", () =>
        {
            var s = new EconomyState();
            Require(s.GrantOnce("daily:2026-10-17", 100, 0));
            Require(!s.GrantOnce("daily:2026-10-17", 100, 0));
            Require(s.gold == 100);
            Require(s.GrantOnce("daily:2026-10-18", 100, 0) && s.gold == 200);
        });
        Test("Chest spending and gear grants are atomic", () =>
        {
            var s = new EconomyState { crystals = 49 };
            var item = new EquipmentItem { id = "unique" };
            Require(!s.PurchaseChest(50, item) && s.crystals == 49 && s.inventory.Count == 0);
            s.crystals = 100;
            Require(s.PurchaseChest(50, item) && s.crystals == 50 && s.inventory.Count == 1);
            Require(!s.PurchaseChest(50, item) && s.crystals == 50);
        });
        Test("Store transaction replay cannot duplicate crystals", () =>
        {
            var s = new EconomyState();
            Require(s.ApplyPurchase("store:1", 100, false));
            Require(!s.ApplyPurchase("store:1", 100, false));
            Require(s.crystals == 100 && s.transactionIds.Count == 1);
        });
        Test("Invalid amounts and overflow do not partly apply grants", () =>
        {
            var s = new EconomyState { crystals = int.MaxValue };
            Require(!s.GrantOnce("invalid", -5, 0));
            Require(!s.GrantOnce("overflow", 10, 1) && s.gold == 0);
            Require(!s.ApplyPurchase("overflow", 1, true) && !s.premiumPass);
            Require(!s.ApplyPurchase("", 0, true));
            Require(!s.PurchaseChest(-50, new EquipmentItem { id = "bad" }));
        });
        Test("Published chest odds are exactly 70/25/5", () =>
        {
            int[] counts = new int[3];
            for (int i = 0; i < 100; i++) counts[(int)MonetizationRules.RarityForRoll(i)]++;
            Require(counts[0] == 70 && counts[1] == 25 && counts[2] == 5);
            try { MonetizationRules.RarityForRoll(100); throw new Exception("Invalid roll accepted"); }
            catch (ArgumentOutOfRangeException) { }
        });
        Test("Pass requires XP, ownership, and unclaimed reward ID", () =>
        {
            var s = new EconomyState { battlePassXp = 99 };
            Require(!MonetizationRules.CanClaimPass(s, 1, false));
            s.battlePassXp = 500;
            Require(MonetizationRules.CanClaimPass(s, 5, false));
            Require(!MonetizationRules.CanClaimPass(s, 1, true));
            s.ApplyPurchase("pass", 0, true);
            Require(MonetizationRules.CanClaimPass(s, 1, true));
            s.GrantOnce(MonetizationRules.PassRewardId(1, true), 0, 20);
            Require(!MonetizationRules.CanClaimPass(s, 1, true));
            Require(!MonetizationRules.CanClaimPass(s, 0, false) && !MonetizationRules.CanClaimPass(s, 6, false));
        });
        Test("Only completed ads reward, and only once", () =>
        {
            int rewards = 0;
            var cancelled = new RewardedRequest();
            Require(cancelled.Resolve(RewardedAdResult.Cancelled, () => rewards++));
            Require(!cancelled.Resolve(RewardedAdResult.Completed, () => rewards++));
            var failed = new RewardedRequest(); failed.Resolve(RewardedAdResult.Failed, () => rewards++);
            var success = new RewardedRequest();
            Require(success.Resolve(RewardedAdResult.Completed, () => rewards++));
            Require(!success.Resolve(RewardedAdResult.Completed, () => rewards++));
            Require(rewards == 1);
        });
        Test("Revive requires death and an unused opportunity", () =>
        {
            Require(PolyusRunRules.CanRevive(true, false, false));
            Require(!PolyusRunRules.CanRevive(false, false, false));
            Require(!PolyusRunRules.CanRevive(true, true, false));
            Require(!PolyusRunRules.CanRevive(true, false, true));
            Require(PolyusRunRules.ReviveHealth(100) == 50 && PolyusRunRules.ReviveHealth(140) == 70);
        });
        Test("Campaign contains three populated sectors and a final boss", () =>
        {
            Require(PolyusRunRules.RoomCount == 3);
            Require(PolyusRunRules.EnemyCount(0) == 4 && PolyusRunRules.EnemyCount(1) == 5 && PolyusRunRules.EnemyCount(2) == 1);
        });
        Test("Swept projectile collision handles fast shots and zero distance", () =>
        {
            Require(PolyusRunRules.SegmentDistance(0, 0, 10, 0, 5, .2) < .21);
            Require(PolyusRunRules.SegmentDistance(0, 0, 10, 0, 12, 0) == 2);
            Require(PolyusRunRules.SegmentDistance(0, 0, 0, 0, 3, 4) == 5);
        });
        Test("Equipment rarity increases the actual combat bonuses", () =>
        {
            Require(PolyusRunRules.ArmorBonus(0) < PolyusRunRules.ArmorBonus(1));
            Require(PolyusRunRules.ArmorBonus(1) < PolyusRunRules.ArmorBonus(2));
            Require(PolyusRunRules.WeaponBonus(0) < PolyusRunRules.WeaponBonus(2));
        });
        Console.WriteLine($"{passed} tests passed. Unity rendering and device behavior require Unity Editor.");
    }
}
