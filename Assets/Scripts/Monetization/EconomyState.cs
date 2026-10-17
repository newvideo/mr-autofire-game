using System;
using System.Collections.Generic;

namespace Polyus.Monetization
{
    public enum EquipmentKind { Weapon, Armor }
    public enum ItemRarity { Common, Rare, Epic }

    [Serializable]
    public sealed class EquipmentItem
    {
        public string id;
        public EquipmentKind kind;
        public ItemRarity rarity;
    }

    // Local prototype data only. A production economy must be authoritative on a server.
    [Serializable]
    public sealed class EconomyState
    {
        public int gold;
        public int crystals;
        public int battlePassXp;
        public bool premiumPass;
        public List<EquipmentItem> inventory = new List<EquipmentItem>();
        public List<string> rewardIds = new List<string>();
        public List<string> transactionIds = new List<string>();

        public bool GrantOnce(string rewardId, int goldAmount, int crystalAmount)
        {
            if (string.IsNullOrEmpty(rewardId) || goldAmount < 0 || crystalAmount < 0 || rewardIds.Contains(rewardId)) return false;
            if (goldAmount > int.MaxValue - gold || crystalAmount > int.MaxValue - crystals) return false;
            gold += goldAmount;
            crystals += crystalAmount;
            rewardIds.Add(rewardId);
            return true;
        }

        public bool PurchaseChest(int price, EquipmentItem item)
        {
            if (price <= 0 || crystals < price || item == null || string.IsNullOrEmpty(item.id)) return false;
            if (inventory.Exists(existing => existing.id == item.id)) return false;
            // Spend and grant are one state mutation and one subsequent save.
            crystals -= price;
            inventory.Add(item);
            return true;
        }

        public bool ApplyPurchase(string transactionId, int crystalAmount, bool unlockPremium)
        {
            if (string.IsNullOrEmpty(transactionId) || crystalAmount < 0 || transactionIds.Contains(transactionId)) return false;
            if (crystalAmount > int.MaxValue - crystals) return false;
            crystals += crystalAmount;
            premiumPass |= unlockPremium;
            transactionIds.Add(transactionId);
            return true;
        }
    }
}
