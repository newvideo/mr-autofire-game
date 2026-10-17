using NUnit.Framework;
using Polyus.Monetization;

namespace Polyus.Tests
{
    public sealed class MonetizationTests
    {
        [Test]
        public void RewardCannotBeGrantedTwice()
        {
            var state = new EconomyState();
            Assert.That(state.GrantOnce("daily:today", 100, 5), Is.True);
            Assert.That(state.GrantOnce("daily:today", 100, 5), Is.False);
            Assert.That(state.gold, Is.EqualTo(100));
            Assert.That(state.crystals, Is.EqualTo(5));
        }

        [Test]
        public void InsufficientFundsDoNotGrantGearOrSpend()
        {
            var state = new EconomyState { crystals = 49 };
            Assert.That(state.PurchaseChest(50, new EquipmentItem { id = "gear" }), Is.False);
            Assert.That(state.crystals, Is.EqualTo(49));
            Assert.That(state.inventory, Is.Empty);
        }

        [Test]
        public void ChestSpendsExactlyOnceAndGrantsOneItem()
        {
            var state = new EconomyState { crystals = 100 };
            var item = new EquipmentItem { id = "gear", rarity = ItemRarity.Epic, kind = EquipmentKind.Weapon };
            Assert.That(state.PurchaseChest(50, item), Is.True);
            Assert.That(state.PurchaseChest(50, item), Is.False);
            Assert.That(state.crystals, Is.EqualTo(50));
            Assert.That(state.inventory.Count, Is.EqualTo(1));
        }

        [Test]
        public void DuplicateStoreTransactionDoesNotGrantAgain()
        {
            var state = new EconomyState();
            Assert.That(state.ApplyPurchase("store:123", 100, false), Is.True);
            Assert.That(state.ApplyPurchase("store:123", 100, false), Is.False);
            Assert.That(state.crystals, Is.EqualTo(100));
        }

        [Test]
        public void EmptyTransactionAndNegativeRewardsAreRejected()
        {
            var state = new EconomyState();
            Assert.That(state.ApplyPurchase("", 100, false), Is.False);
            Assert.That(state.GrantOnce("bad", -1, 0), Is.False);
            Assert.That(state.PurchaseChest(-50, new EquipmentItem { id = "bad" }), Is.False);
            Assert.That(state.gold, Is.Zero);
            Assert.That(state.crystals, Is.Zero);
        }

        [Test]
        public void CurrencyOverflowDoesNotPartiallyApplyReward()
        {
            var state = new EconomyState { crystals = int.MaxValue };
            Assert.That(state.GrantOnce("overflow", 10, 1), Is.False);
            Assert.That(state.ApplyPurchase("overflow-tx", 100, true), Is.False);
            Assert.That(state.gold, Is.Zero);
            Assert.That(state.premiumPass, Is.False);
            Assert.That(state.rewardIds, Is.Empty);
            Assert.That(state.transactionIds, Is.Empty);
        }

        [Test]
        public void PassRequiresXpAndEntitlementAndRejectsDuplicateClaims()
        {
            var state = new EconomyState { battlePassXp = 99 };
            Assert.That(BattlePassManager.CanClaimReward(state, 1, false), Is.False);
            state.battlePassXp = 100;
            Assert.That(BattlePassManager.CanClaimReward(state, 1, false), Is.True);
            Assert.That(BattlePassManager.CanClaimReward(state, 1, true), Is.False);
            state.ApplyPurchase("pass", 0, true);
            Assert.That(BattlePassManager.CanClaimReward(state, 1, true), Is.True);
            state.GrantOnce("pass:prototype:v1:1:True", 0, 20);
            Assert.That(BattlePassManager.CanClaimReward(state, 1, true), Is.False);
            Assert.That(BattlePassManager.CanClaimReward(state, 0, false), Is.False);
            Assert.That(BattlePassManager.CanClaimReward(state, 6, false), Is.False);
        }

        [TestCase(0, ItemRarity.Common)]
        [TestCase(69, ItemRarity.Common)]
        [TestCase(70, ItemRarity.Rare)]
        [TestCase(94, ItemRarity.Rare)]
        [TestCase(95, ItemRarity.Epic)]
        [TestCase(99, ItemRarity.Epic)]
        public void RarityBoundariesMatchPublishedOdds(int roll, ItemRarity expected)
            => Assert.That(LootboxManager.RarityForRoll(roll), Is.EqualTo(expected));

        [Test]
        public void InvalidRandomRollIsRejected()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => LootboxManager.RarityForRoll(-1));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => LootboxManager.RarityForRoll(100));
        }
    }
}
