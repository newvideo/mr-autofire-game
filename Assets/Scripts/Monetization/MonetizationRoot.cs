using UnityEngine;

namespace Polyus.Monetization
{
    // One persistent composition root. SDK adapters can be injected here later.
    public sealed class MonetizationRoot : MonoBehaviour
    {
        public static MonetizationRoot Instance { get; private set; }
        public EconomyManager Economy { get; private set; }
        public AdsManager Ads { get; private set; }
        public IAPManager IAP { get; private set; }
        public BattlePassManager Pass { get; private set; }
        public LootboxManager Lootboxes { get; private set; }
        public PolyusPrototype Prototype { get; private set; }
        public static MonetizationRoot GetOrCreate()
        {
            if (Instance == null) new GameObject("Monetization").AddComponent<MonetizationRoot>();
            return Instance;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Economy = gameObject.AddComponent<EconomyManager>();
            Ads = gameObject.AddComponent<AdsManager>();
            IAP = gameObject.AddComponent<IAPManager>();
            Pass = gameObject.AddComponent<BattlePassManager>();
            Lootboxes = gameObject.AddComponent<LootboxManager>();
            IAP.Initialize(Economy);
            Pass.Initialize(Economy);
            Lootboxes.Initialize(Economy);
        }

        public void Attach(PolyusPrototype prototype) => Prototype = prototype;

        public void AwardDrone(string runId, int droneNumber)
        {
            // Stable reward IDs also prevent duplicate kill notifications granting twice.
            if (Economy.GrantOnce($"combat:{runId}:{droneNumber}", 25)) Pass.AwardCombatXp(25);
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }
    }
}
