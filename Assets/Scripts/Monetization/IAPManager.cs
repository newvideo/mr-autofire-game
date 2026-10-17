using System;
using UnityEngine;

namespace Polyus.Monetization
{
    public sealed class StorePurchase
    {
        public string ProductId;
        public string TransactionId;
        public bool Verified;
    }

    // A production adapter must validate receipts on a trusted backend, handle pending
    // purchases, acknowledge only durable grants, and restore the non-consumable pass.
    public interface IStoreProvider
    {
        bool IsSimulation { get; }
        void Buy(string productId, Action<StorePurchase> completed, Action<string> failed);
    }

    public sealed class SimulatedStoreProvider : IStoreProvider
    {
        public bool IsSimulation => true;
        public void Buy(string productId, Action<StorePurchase> completed, Action<string> failed)
        {
            completed(new StorePurchase { ProductId = productId, TransactionId = "test:" + Guid.NewGuid(), Verified = true });
        }
    }

    public sealed class IAPManager : MonoBehaviour
    {
        public const string CrystalPack = "polyus.crystals.100";
        public const string PremiumPass = "polyus.pass.prototype";
        public bool IsBusy { get; private set; }
        public bool IsSimulation => provider != null && provider.IsSimulation;
        private IStoreProvider provider;
        private EconomyManager economy;

        public void Initialize(EconomyManager wallet)
        {
            economy = wallet;
            // A release build cannot accidentally expose simulated purchases.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            provider = new SimulatedStoreProvider();
#endif
        }

        public void SetProvider(IStoreProvider adapter)
        {
            if (IsBusy) throw new InvalidOperationException("A purchase is already pending.");
            provider = adapter ?? throw new ArgumentNullException(nameof(adapter));
        }

        public bool Buy(string productId, Action<bool, string> finished)
        {
            if (IsBusy || economy == null) return false;
            if (productId != CrystalPack && productId != PremiumPass) { finished?.Invoke(false, "Unknown product."); return false; }
            if (provider == null) { finished?.Invoke(false, "Store SDK is not connected."); return false; }
            if (productId == PremiumPass && economy.State.premiumPass) { finished?.Invoke(false, "Pass already owned."); return false; }
            IsBusy = true;
            bool settled = false;
            Action<string> fail = reason =>
            {
                if (settled) return;
                settled = true;
                IsBusy = false;
                finished?.Invoke(false, reason);
            };
            try { provider.Buy(productId, receipt =>
            {
                if (settled) return;
                settled = true;
                IsBusy = false;
                if (receipt == null || !receipt.Verified || receipt.ProductId != productId)
                { finished?.Invoke(false, "Purchase verification failed."); return; }
                bool granted = economy.State.ApplyPurchase(receipt.TransactionId,
                    productId == CrystalPack ? 100 : 0, productId == PremiumPass);
                if (granted) economy.Save();
                finished?.Invoke(granted, granted ? "Purchase granted." : "Transaction already handled or invalid.");
            }, fail); }
            catch (Exception error)
            {
                Debug.LogException(error);
                fail("Store adapter failed.");
            }
            return true;
        }
    }
}
