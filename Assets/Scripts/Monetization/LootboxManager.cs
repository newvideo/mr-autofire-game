using System;
using UnityEngine;

namespace Polyus.Monetization
{
    public sealed class LootboxManager : MonoBehaviour
    {
        public const int ChestPrice = 50;
        private EconomyManager economy;
        public void Initialize(EconomyManager wallet) => economy = wallet;
        private static string DailyId => "daily-chest:" + DateTime.UtcNow.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        public bool CanOpenDaily => !economy.State.rewardIds.Contains(DailyId);

        public bool TryOpenDaily(out EquipmentItem item)
        {
            item = null;
            if (!economy.State.GrantOnce(DailyId, 100, 0)) return false;
            item = RollItem();
            economy.State.inventory.Add(item);
            economy.Save();
            return true;
        }

        private static EquipmentItem RollItem() => new EquipmentItem
        {
            id = Guid.NewGuid().ToString("N"),
            kind = UnityEngine.Random.Range(0, 2) == 0 ? EquipmentKind.Weapon : EquipmentKind.Armor,
            rarity = RarityForRoll(UnityEngine.Random.Range(0, 100))
        };

        // Displayed odds: Common 70%, Rare 25%, Epic 5%.
        public static ItemRarity RarityForRoll(int roll)
        {
            if (roll < 0 || roll >= 100) throw new ArgumentOutOfRangeException(nameof(roll));
            return roll < 70 ? ItemRarity.Common : roll < 95 ? ItemRarity.Rare : ItemRarity.Epic;
        }

        public bool TryBuy(out EquipmentItem item)
        {
            item = null;
            if (economy.State.crystals < ChestPrice) return false;
            var rolled = RollItem();
            if (!economy.State.PurchaseChest(ChestPrice, rolled)) return false;
            economy.Save();
            item = rolled;
            return true;
        }
    }
}
