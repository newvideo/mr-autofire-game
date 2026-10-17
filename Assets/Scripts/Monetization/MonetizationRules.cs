using System;

namespace Polyus.Monetization
{
    public enum RewardedAdResult { Completed, Cancelled, Failed }

    public static class MonetizationRules
    {
        public static ItemRarity RarityForRoll(int roll)
        {
            if (roll < 0 || roll >= 100) throw new ArgumentOutOfRangeException(nameof(roll));
            return roll < 70 ? ItemRarity.Common : roll < 95 ? ItemRarity.Rare : ItemRarity.Epic;
        }
        public static string PassRewardId(int tier, bool premium) => $"pass:prototype:v1:{tier}:{premium}";
        public static bool CanClaimPass(EconomyState state, int tier, bool premium) => tier >= 1 && tier <= 5
            && state.battlePassXp / 100 >= tier && (!premium || state.premiumPass)
            && !state.rewardIds.Contains(PassRewardId(tier, premium));
    }

    // A terminal result is irreversible: failure/cancellation can never become a late reward.
    public sealed class RewardedRequest
    {
        public bool IsResolved { get; private set; }
        public RewardedAdResult Outcome { get; private set; }
        public bool Resolve(RewardedAdResult result, Action grant)
        {
            if (IsResolved) return false;
            IsResolved = true;
            Outcome = result;
            if (result == RewardedAdResult.Completed) grant?.Invoke();
            return true;
        }
    }
}
