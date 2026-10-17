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
        private bool menu = true;
        private string pendingProduct, message = "Prototype economy. No real money is charged.";

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

        public void Attach(PolyusPrototype prototype)
        {
            Prototype = prototype;
            ApplyBlock();
        }

        private void ApplyBlock()
        {
            if (Prototype != null)
                Prototype.SetExternalBlocked(menu || Ads.IsShowing || pendingProduct != null || IAP.IsBusy || Prototype.CanRevive);
        }

        private void Update() => ApplyBlock();

        public void AwardDrone(string runId, int droneNumber)
        {
            // Stable reward IDs also prevent duplicate kill notifications granting twice.
            if (Economy.GrantOnce($"combat:{runId}:{droneNumber}", 25)) Pass.AwardCombatXp(25);
        }

        private void RewardDaily()
        {
            if (!Lootboxes.CanOpenDaily) { message = "Daily chest already claimed (UTC day)."; return; }
            if (!Ads.ShowRewarded("daily_chest", () =>
            {
                message = Lootboxes.TryOpenDaily(out var item)
                    ? $"Daily reward: 100 gold + {item.rarity} {item.kind}" : "Already claimed today.";
            }, AdFinished)) message = "Ad unavailable. Try again later.";
            ApplyBlock();
        }

        private void RewardRevive()
        {
            var run = Prototype;
            if (run == null || !run.CanRevive) return;
            if (!Ads.ShowRewarded("revive", () =>
            {
                if (run != null && run == Prototype) run.TryRevive();
            }, AdFinished)) message = "Ad unavailable. Revive was not consumed.";
            ApplyBlock();
        }

        private void AdFinished(RewardedAdResult outcome)
        {
            if (outcome != RewardedAdResult.Completed) message = "Ad did not complete. No reward granted.";
            ApplyBlock();
        }

        private void OnGUI()
        {
            if (Prototype == null) return;
            Matrix4x4 previous = GUI.matrix;
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 1280 * scale) / 2, (Screen.height - 720 * scale) / 2), Quaternion.identity, Vector3.one * scale);
            var oldSkin = GUI.skin;
            // IMGUI is intentionally a disposable prototype view, independent of SDKs.
            if (Ads.IsShowing)
            {
                GUI.Box(new Rect(320, 210, 640, 260), "");
                GUI.Label(new Rect(355, 245, 570, 80), Ads.IsSimulation
                    ? "SIMULATED REWARDED AD\nWait 5 seconds to finish. Closing early grants nothing."
                    : "Rewarded ad in progress...");
                if (GUI.Button(new Rect(490, 370, 300, 55), "Cancel ad (no reward)")) Ads.Cancel();
            }
            else if (pendingProduct != null)
            {
                GUI.Box(new Rect(320, 210, 640, 260), "Confirm purchase");
                GUI.Label(new Rect(355, 255, 570, 60), IAP.IsSimulation
                    ? "TEST PURCHASE ONLY — no money charged.\nProduct: " + pendingProduct
                    : "Continue to store checkout: " + pendingProduct);
                if (GUI.Button(new Rect(355, 355, 260, 55), "Confirm"))
                {
                    string id = pendingProduct;
                    pendingProduct = null;
                    IAP.Buy(id, (success, reason) => message = reason);
                }
                if (GUI.Button(new Rect(645, 355, 260, 55), "Cancel")) pendingProduct = null;
            }
            else if (menu) DrawMenu();
            else
            {
                if (GUI.Button(new Rect(1030, 90, 190, 42), "Menu / Shop")) { menu = true; ApplyBlock(); }
                if (Prototype.CanRevive)
                {
                    GUI.Box(new Rect(330, 205, 620, 270), "Defeated — optional revive");
                    GUI.Label(new Rect(370, 255, 540, 55), "Watch a rewarded ad to revive with 50% HP.\nOnce per run. Declining keeps your earned gold / XP.");
                    if (GUI.Button(new Rect(370, 335, 540, 50), "Watch ad and revive")) RewardRevive();
                    if (GUI.Button(new Rect(370, 400, 540, 45), "No thanks")) Prototype.DeclineRevive();
                }
            }
            GUI.skin = oldSkin;
            GUI.matrix = previous;
        }

        private void DrawMenu()
        {
            var state = Economy.State;
            GUI.Box(new Rect(260, 30, 760, 660), "POLYUS — prototype menu / shop");
            GUI.Label(new Rect(290, 65, 690, 30), $"Gold: {state.gold}    Crystals: {state.crystals}    Gear: {state.inventory.Count}    Pass XP: {state.battlePassXp}");
            if (GUI.Button(new Rect(290, 105, 335, 45), Prototype.IsEnded ? "Start new run" : "Play / Resume"))
            {
                menu = false;
                if (Prototype.IsEnded) Prototype.RestartRun();
                ApplyBlock();
            }
            GUI.enabled = Lootboxes.CanOpenDaily;
            if (GUI.Button(new Rect(645, 105, 335, 45), "Daily chest — rewarded ad")) RewardDaily();
            GUI.enabled = !IAP.IsBusy;
            string prefix = IAP.IsSimulation ? "TEST: " : "Store: ";
            if (GUI.Button(new Rect(290, 165, 335, 45), prefix + "100 crystals")) pendingProduct = IAPManager.CrystalPack;
            GUI.enabled = !IAP.IsBusy && !state.premiumPass;
            if (GUI.Button(new Rect(645, 165, 335, 45), prefix + "Premium pass")) pendingProduct = IAPManager.PremiumPass;
            GUI.enabled = state.crystals >= LootboxManager.ChestPrice;
            if (GUI.Button(new Rect(290, 225, 690, 45), "Gear chest — 50 crystals | Common 70% / Rare 25% / Epic 5%"))
            {
                if (Lootboxes.TryBuy(out var item)) message = $"Found: {item.rarity} {item.kind}";
            }
            GUI.enabled = true;
            GUI.Label(new Rect(290, 282, 690, 30), "BATTLE PASS — each tier needs 100 XP. Each drone gives 25 XP + 25 gold.");
            for (int tier = 1; tier <= BattlePassManager.TierCount; tier++)
            {
                float y = 320 + (tier - 1) * 43;
                GUI.Label(new Rect(290, y, 110, 35), $"Tier {tier} ({tier * 100} XP)");
                GUI.enabled = Pass.CanClaim(tier, false);
                if (GUI.Button(new Rect(415, y, 265, 35), Pass.IsClaimed(tier, false) ? "Free: claimed" : $"Free: {tier * 50} gold")) Pass.Claim(tier, false);
                GUI.enabled = Pass.CanClaim(tier, true);
                if (GUI.Button(new Rect(695, y, 285, 35), Pass.IsClaimed(tier, true) ? "Premium: claimed" : "Premium: 20 crystals")) Pass.Claim(tier, true);
            }
            GUI.enabled = true;
            GUI.Label(new Rect(290, 550, 690, 45), message);
            GUI.Label(new Rect(290, 600, 690, 65), "Local prototype save. Gear is collectible; stats are not equipped yet.\nDaily reset uses UTC. Store / ad SDKs are not connected.\nRelease builds disable simulated IAP; configure a verified store adapter.");
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }
    }
}
