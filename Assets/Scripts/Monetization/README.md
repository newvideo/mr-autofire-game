# Prototype hybrid monetization

## Architecture

`MonetizationRoot` creates persistent services and a disposable IMGUI menu.
The prototype attaches to it on Play. There is no manual Inspector wiring.

- `AdsManager`: one rewarded request at a time; only Completed grants the
  callback. Cancelled/failed ads grant nothing. The five-second simulator is
  replaced through `SetProvider(IRewardedAdsProvider)`.
- `EconomyManager` / `EconomyState`: gold, crystals, owned gear, pass XP,
  reward identifiers and processed transactions. Local JSON uses PlayerPrefs
  key `Polyus.Prototype.Economy.v1`. This is a prototype persistence mechanism,
  not protection from cheating or device clock manipulation.
- `IAPManager`: catalog IDs `polyus.crystals.100` (consumable) and
  `polyus.pass.prototype` (non-consumable prototype pass). Only verified,
  matching products and new transaction IDs can grant rewards. An explicit
  confirmation precedes the test purchase. Simulation is enabled only in the
  Editor / development builds; release builds require a store adapter.
- `BattlePassManager`: five tiers at 100 XP per tier. Free reward is tier × 50
  gold; premium reward is 20 crystals. Tracks are claimed independently once.
  Buying premium later unlocks already-earned tiers. This is one permanent
  prototype pass, not a seasonal subscription or auto-renewing product.
- `LootboxManager`: a chest costs 50 crystals and grants one weapon or armor
  with equal type probability. Rarity odds: Common 70%, Rare 25%, Epic 5%.
  The odds are displayed before purchase. Gear is stored, not equipped yet.

## Prototype hooks

- Each defeated drone awards 25 gold + 25 pass XP with a run/kill reward ID.
- At zero HP, an optional ad prompt offers exactly 50/100 HP once per run.
  Cancelling an ad does not consume the revive. A completed revive keeps XP,
  killed drones and rewards, clears projectiles and respawns at the start with
  two seconds of protection. A later death cannot offer a second revive.
- The main menu daily ad chest grants 100 gold and random gear once per UTC
  calendar date. The entitlement is recorded only after completion. Production
  daily time must come from a trusted backend.
- Menus, purchase confirmation, revive prompt and ads block gameplay and touch
  controls. Economy survives room retries; revive eligibility resets each run.

## Connecting production SDKs (not implemented)

No Unity IAP / Unity Ads / LevelPlay package or live store product is configured.
The project currently uses explicit adapters, not real purchases or ad revenue.

Implement `IRewardedAdsProvider`, including initialization, readiness/loading,
failure handling, consent flow and the SDK's earned-reward event. Its Show
enumerator must finish once a terminal SDK event arrives. Closing an ad is not
proof of a reward. Inject the adapter into AdsManager before showing placements.

Implement `IStoreProvider` using the selected Unity IAP API. Verify purchases
on a trusted server before returning `Verified = true`, retain stable store
transaction IDs, handle pending/cancelled purchases and restore owned pass
entitlements. Complete/acknowledge the store transaction only after the server
has durably recorded the grant. Move economy, RNG, claims and duplicate
transaction checks to that server before accepting real money. The local
PlayerPrefs ledger cannot provide production transaction durability, refunds,
cross-device sync or purchase restoration.

## Validation

Open **Window → General → Test Runner → EditMode → Run All** for
`Polyus.Tests.Editor`. These tests cover duplicate grants/transactions,
insufficient funds, atomic chest grants, invalid amounts/overflow, pass locks,
and rarity boundaries. Tests do not modify PlayerPrefs. They have not been
executed in the cloud because Unity Editor is unavailable there.

In Play mode verify these integration scenarios:

1. On a clean save, balance is zero. Cancel the daily ad before five seconds:
   no currency or item appears; finishing it grants one item and 100 gold.
2. Retry the daily chest and restart Play: the same UTC day's reward stays
   unavailable. Room restarts preserve balances and inventory.
3. Buy a TEST crystal pack: cancel confirmation to receive nothing; confirm
   to receive 100 crystals. Buy two gear chests: balance becomes zero and two
   items are added. A third chest is disabled.
4. Kill four drones: earn 100 gold and 100 XP. Claim free tier 1 once. The
   premium track remains locked until the TEST pass purchase, then tier 1
   premium grants 20 crystals once. Rebuying the pass is disabled.
5. Die: cancel a revive ad to remain dead with the offer still available.
   Complete it: HP becomes 50, existing progress remains. Die again: no offer.
   A new run offers one revive again.
6. While an ad or menu is open, movement/enemies stop. Backgrounding cancels
   the simulated ad without a reward. Repeat on an Android development build.
7. A non-development release build has no simulated purchases; it reports
   that a store SDK is not connected until a real provider is installed.
