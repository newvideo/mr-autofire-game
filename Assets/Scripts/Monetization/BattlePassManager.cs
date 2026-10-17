using UnityEngine;

namespace Polyus.Monetization
{
    public sealed class BattlePassManager : MonoBehaviour
    {
        public const int TierCount = 5;
        public const int XpPerTier = 100;
        private EconomyManager economy;
        public int UnlockedTiers => Mathf.Min(TierCount, economy.State.battlePassXp / XpPerTier);
        public void Initialize(EconomyManager wallet) => economy = wallet;

        public void AwardCombatXp(int amount)
        {
            if (amount <= 0) return;
            economy.State.battlePassXp = (int)System.Math.Min(TierCount * XpPerTier,
                (long)economy.State.battlePassXp + amount);
            economy.Save();
        }

        private static string RewardId(int tier, bool premium) => MonetizationRules.PassRewardId(tier, premium);
        public bool IsClaimed(int tier, bool premium) => economy.State.rewardIds.Contains(RewardId(tier, premium));
        public static bool CanClaimReward(EconomyState state, int tier, bool premium) => MonetizationRules.CanClaimPass(state, tier, premium);
        public bool CanClaim(int tier, bool premium) => CanClaimReward(economy.State, tier, premium);

        public bool Claim(int tier, bool premium)
        {
            if (!CanClaim(tier, premium)) return false;
            // Free track: gold. Premium track: crystals. Earlier tiers remain claimable after purchase.
            return economy.GrantOnce(RewardId(tier, premium), premium ? 0 : tier * 50, premium ? 20 : 0);
        }
    }
}
