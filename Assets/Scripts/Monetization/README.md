# Hybrid monetization services

`MonetizationRoot` is a persistent composition root. It has no rendering logic.
`PolyusUI` owns the single Canvas, pages and blocking dialogs; `PolyusPrototype`
owns the run and explicit phases. Services are wired automatically on Play.

## Economy and progression

- `EconomyManager` loads/saves `EconomyState` using the existing PlayerPrefs key
  `Polyus.Prototype.Economy.v1`; previous prototype balances remain compatible.
- Each drone awards 25 gold + 25 pass XP with a run/kill reward ID. Completing
  the campaign grants 150 extra gold, 5 crystals and 100 pass XP once per run.
- `IAPManager`: `polyus.crystals.100` (consumable) and `polyus.pass.prototype`
  (non-consumable permanent prototype pass). Explicit confirmation is required.
  Simulation is enabled in Editor/debug builds only (`UNITY_EDITOR || DEBUG`).
  An adapter must verify a matching product and stable transaction ID.
- `BattlePassManager`: five tiers at 100 XP each. Free rewards: tier × 50 gold;
  premium rewards: 20 crystals. Claims are independent and idempotent; buying
  premium later unlocks earned tiers. No subscription/season reset is implied.
- `LootboxManager`: 50 crystals buys one weapon or armor (50% type probability).
  Displayed rarity odds: Common 70%, Rare 25%, Epic 5%. Spending and adding the
  item are one state mutation and save. Inventory supports equipping one item
  per slot. The next run uses its rarity's damage or maximum-health bonus.

## Rewarded placements

`AdsManager.ShowRewarded(placement, reward, finished)` accepts one request at
once. Only Completed grants; Cancelled/Failed do not. `RewardedRequest` ensures
that a terminal result cannot subsequently change or grant a second reward.
The default simulator waits five realtime seconds. Cancellation or loss of focus
cancels it. Replace it with `SetProvider(IRewardedAdsProvider)`.

- `daily_chest`: 100 gold + random equipment once per UTC calendar day. The
  claim marker is written only after completion; retrying cannot duplicate it.
- `revive`: one optional revival at 50% maximum HP per run. Cancelled/failed
  ads do not consume it. A successful revive clears bullets, respawns safely
  and grants two seconds of protection without resetting earned progress.

Pages, pending purchases and advertising block physics, enemies and controls.
The UI never uses IMGUI or draws the shop over the HUD. State uses JSON locally;
this is not a secure economy or protection against device clock manipulation.

## Production adapters (not included)

Implement `IRewardedAdsProvider` for Unity Ads / LevelPlay and use the actual
SDK earned-reward event. Ad close alone is insufficient. Initialize/load ads,
handle failures and consent, and finish the Show enumerator after a terminal
SDK event. Provider callbacks must run on Unity's main thread.

Implement `IStoreProvider` with Unity IAP. Validate receipts on a trusted server
before returning Verified=true. Handle pending/failed/cancelled purchases,
restore non-consumable ownership, and acknowledge transactions only after a
durable server-side grant. The local ledger cannot provide production purchase
durability, refunds, cross-device synchronization or restoration. Real product
prices must come from the store, not hard-coded test labels.

Before accepting real money, move the authoritative economy, random loot,
daily time, claims and transaction deduplication to the backend. No real SDK,
store catalog or server is configured in this demo.

## Checks

Core rules run without Unity via `python Tools/validate_core.py` (.NET 8 SDK).
Unity tests are under Assets/Tests: EditMode checks economy, PlayMode checks
art loading, exclusive HUD/page states, physics pause and gravity reversal.
Unity tests are supplied but were not executed in the cloud.

Manual integration cases: complete/cancel daily ads; retry same-day reward;
confirm/cancel crystal purchase; open chests until insufficient funds; equip
gear and begin a fresh run; claim both pass tracks once; cancel then complete
revive; die again to confirm there is no second revival; background during an
ad; retry runs and restart Play to confirm balances and inventory persist.
