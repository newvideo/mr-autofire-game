using System;
using Polyus.Monetization;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Polyus
{
    // One Canvas and one input system. Pages and modals are opaque, mutually exclusive layers.
    public sealed class PolyusUI : MonoBehaviour
    {
        private PolyusPrototype game;
        private MonetizationRoot services;
        private PolyusArt art;
        private RectTransform safe, hud, page, modal;
        private Image healthFill, bossFill, damageFlash;
        private Text healthText, roomText, goldText, gravityText, wallet, toast;
        private RectTransform bossPanel;
        private PolyusControl left, right, jump, flip;
        private string currentPage;
        private float toastUntil, adStarted;
        private Text adCountdown;
        private int inventoryPage;
        private bool advertising, pendingPurchase;
        private readonly Color panel = new Color(.035f, .075f, .12f, .98f);
        private readonly Color muted = new Color(.58f, .72f, .78f);
        public float Move => (right != null && right.Held ? 1 : 0) - (left != null && left.Held ? 1 : 0);
        public bool ConsumeJump() => jump != null && jump.ConsumePress();
        public bool ConsumeFlip() => flip != null && flip.ConsumePress();

        public void Initialize(PolyusPrototype prototype, MonetizationRoot hub, PolyusArt visuals)
        {
            game = prototype; services = hub; art = visuals;
            var canvasObject = new GameObject("POLYUS Interface", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.GetComponent<Canvas>().sortingOrder = 100;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            safe = Stretch(canvasObject.transform, "Device safe area");
            hud = Stretch(safe, "Gameplay HUD");
            page = Stretch(safe, "Full screen pages");
            modal = Stretch(safe, "Modal dialogs");
            var events = new GameObject("UI EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            events.transform.SetParent(transform, false);
            events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            BuildHud();
            page.gameObject.SetActive(false); modal.gameObject.SetActive(false);
            UpdateSafeArea();
        }
        private RectTransform Stretch(Transform parent, string name)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }
        private RectTransform Box(Transform parent, string name, Vector2 position, Vector2 size, Vector2? anchor = null)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = anchor ?? Vector2.one * .5f;
            rect.sizeDelta = size; rect.anchoredPosition = position;
            return rect;
        }
        private Image Image(Transform parent, string name, Vector2 position, Vector2 size, Color color, Sprite sprite = null, Vector2? anchor = null)
        {
            var image = Box(parent, name, position, size, anchor).gameObject.AddComponent<Image>();
            image.sprite = sprite ?? art.White; image.color = color; image.raycastTarget = false;
            if (sprite != null && sprite != art.White) image.preserveAspect = true;
            return image;
        }
        private Image Cover(Transform parent, Color color, Sprite sprite = null)
        {
            var image = Stretch(parent, "Backdrop").gameObject.AddComponent<Image>();
            image.color = color; image.sprite = sprite ?? art.White; image.raycastTarget = true;
            if (sprite != null)
            {
                var fit = image.gameObject.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fit.aspectRatio = sprite.rect.width / sprite.rect.height;
            }
            return image;
        }
        private Text Text(Transform parent, string value, Vector2 position, Vector2 size, int fontSize = 26,
            Color? color = null, TextAnchor align = TextAnchor.MiddleCenter, Vector2? anchor = null)
        {
            var text = Box(parent, value, position, size, anchor).gameObject.AddComponent<Text>();
            text.font = art.Font; text.text = value; text.fontSize = fontSize;
            text.color = color ?? Color.white; text.alignment = align;
            text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }
        private Button Button(Transform parent, string caption, Vector2 position, Vector2 size, UnityAction action,
            bool primary = false, bool enabled = true, Vector2? anchor = null)
        {
            var background = Image(parent, caption, position, size, primary ? PolyusArt.Orange : new Color(.09f, .18f, .25f), null, anchor);
            background.raycastTarget = true;
            var button = background.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            var colors = button.colors; colors.highlightedColor = new Color(.8f, 1, 1); colors.pressedColor = new Color(.5f, .75f, .85f);
            colors.disabledColor = new Color(.3f, .37f, .4f, .65f); button.colors = colors;
            button.interactable = enabled;
            button.onClick.AddListener(() => { game.Audio.Click(); action?.Invoke(); });
            Text(background.transform, caption, Vector2.zero, size - new Vector2(20, 4), size.y > 75 ? 32 : 24,
                primary ? new Color(.055f, .08f, .1f) : Color.white);
            return button;
        }
        private void Accent(Transform parent, Vector2 position, Vector2 size, Color color) => Image(parent, "Accent", position, size, color);
        private Image Bar(Transform parent, Vector2 position, Vector2 size, Color color)
        {
            var rail = Image(parent, "Progress rail", position, size, new Color(.02f, .04f, .065f));
            var fill = Image(rail.transform, "Progress fill", Vector2.zero, size, color);
            fill.type = UnityEngine.UI.Image.Type.Filled; fill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            fill.fillOrigin = 0;
            return fill;
        }
        private void Clear(RectTransform root)
        {
            for (int i = root.childCount - 1; i >= 0; i--)
            { var child = root.GetChild(i).gameObject; child.SetActive(false); Destroy(child); }
        }
        private void ResetControls() { left.Clear(); right.Clear(); jump.Clear(); flip.Clear(); }
        private void BuildHud()
        {
            Image(hud, "Vitals plate", new Vector2(225, -64), new Vector2(390, 98), panel, null, new Vector2(0, 1));
            Image(hud, "Hero portrait", new Vector2(80, -64), new Vector2(90, 90), Color.white, art.Hero, new Vector2(0, 1));
            healthText = Text(hud, "100 / 100", new Vector2(263, -45), new Vector2(250, 34), 26, null, TextAnchor.MiddleLeft, new Vector2(0, 1));
            healthFill = Bar(hud, Vector2.zero, new Vector2(260, 16), PolyusArt.Cyan);
            healthFill.transform.parent.GetComponent<RectTransform>().anchorMin = healthFill.transform.parent.GetComponent<RectTransform>().anchorMax = new Vector2(0, 1);
            healthFill.transform.parent.GetComponent<RectTransform>().anchoredPosition = new Vector2(263, -82);
            roomText = Text(hud, "СЕКТОР 01 / 03", new Vector2(0, -45), new Vector2(430, 40), 30, null, TextAnchor.MiddleCenter, new Vector2(.5f, 1));
            goldText = Text(hud, "+0 ЗОЛОТА", new Vector2(-250, -48), new Vector2(260, 45), 27, PolyusArt.Orange, TextAnchor.MiddleRight, new Vector2(1, 1));
            Button(hud, "II", new Vector2(-75, -58), new Vector2(90, 75), () => game.Pause(), false, true, new Vector2(1, 1));
            bossPanel = Box(hud, "Boss rail", new Vector2(0, -118), new Vector2(630, 70), new Vector2(.5f, 1));
            Text(bossPanel, "СТРАЖ ПОЛЮСА", new Vector2(0, 15), new Vector2(600, 30), 22, PolyusArt.Orange);
            bossFill = Bar(bossPanel, new Vector2(0, -15), new Vector2(600, 13), PolyusArt.Orange);
            bossPanel.gameObject.SetActive(false);
            left = Hold("‹", new Vector2(90, 102), new Vector2(0, 0));
            right = Hold("›", new Vector2(235, 102), new Vector2(0, 0));
            flip = Hold("ПОЛЮС", new Vector2(-240, 102), new Vector2(1, 0));
            jump = Hold("ПРЫЖОК", new Vector2(-90, 102), new Vector2(1, 0));
            gravityText = Text(hud, "ГРАВИТАЦИЯ ↓", new Vector2(0, 83), new Vector2(560, 45), 22, PolyusArt.Cyan, TextAnchor.MiddleCenter, new Vector2(.5f, 0));
            Text(hud, "A / D — движение    ПРОБЕЛ — прыжок    G — полюс", new Vector2(0, 37), new Vector2(850, 32), 19, muted, TextAnchor.MiddleCenter, new Vector2(.5f, 0));
            damageFlash = Cover(hud, new Color(1, .1f, .05f, 0)); damageFlash.raycastTarget = false;
            toast = Text(hud, "", new Vector2(0, -190), new Vector2(1250, 55), 27, Color.white, TextAnchor.MiddleCenter, new Vector2(.5f, 1));
        }
        private PolyusControl Hold(string caption, Vector2 position, Vector2 anchor)
        {
            var bg = Image(hud, caption, position, new Vector2(125, 125), new Color(.055f, .16f, .22f, .92f), art.Disc, anchor);
            bg.raycastTarget = true;
            Image(bg.transform, "Ring", Vector2.zero, new Vector2(108, 108), new Color(.15f, .48f, .58f, .7f), art.Disc);
            Image(bg.transform, "Inner", Vector2.zero, new Vector2(101, 101), new Color(.025f, .09f, .14f, 1), art.Disc);
            Text(bg.transform, caption, Vector2.zero, new Vector2(115, 70), caption.Length > 2 ? 21 : 64, PolyusArt.Cyan);
            return bg.gameObject.AddComponent<PolyusControl>();
        }
        private void UpdateSafeArea()
        {
            var area = Screen.safeArea;
            safe.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
            safe.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
            safe.offsetMin = safe.offsetMax = Vector2.zero;
        }
        private void Update()
        {
            if (game == null) return;
            UpdateSafeArea();
            if (wallet != null) wallet.text = $"ЗОЛОТО  {services.Economy.State.gold}     КРИСТАЛЛЫ  {services.Economy.State.crystals}";
            if (toast != null && Time.unscaledTime > toastUntil) toast.text = "";
            if (damageFlash != null)
            { var color = damageFlash.color; color.a = Mathf.MoveTowards(color.a, 0, Time.unscaledDeltaTime); damageFlash.color = color; }
            if (adCountdown != null) adCountdown.text = services.Ads.IsSimulation
                ? "Демонстрационный ролик · осталось " + Mathf.Max(0, Mathf.CeilToInt(5 - (Time.realtimeSinceStartup - adStarted))) + " сек."
                : "Реклама воспроизводится…";
            game.SetExternalBlocked(advertising || services.Ads.IsShowing || pendingPurchase || services.IAP.IsBusy);
        }
        public void RefreshHud(int hp, int maximum, int room, int enemies, int sign, float cooldown, int gold)
        {
            healthText.text = hp + " / " + maximum; healthFill.fillAmount = hp / (float)maximum;
            healthFill.color = hp < maximum * .3f ? PolyusArt.Orange : PolyusArt.Cyan;
            roomText.text = $"СЕКТОР {room + 1:00} / 03  ·  {enemies} ЦЕЛЕЙ";
            gravityText.text = (sign == 1 ? "ГРАВИТАЦИЯ ↓" : "ГРАВИТАЦИЯ ↑") + (cooldown > 0 ? "  ·  ПЕРЕЗАРЯДКА" : "");
            goldText.text = "+" + gold + " ЗОЛОТА";
        }
        public void SetBoss(bool visible, float value) { bossPanel.gameObject.SetActive(visible); bossFill.fillAmount = value; }
        public void FlashDamage() => damageFlash.color = new Color(1, .1f, .03f, .3f);
        public void Toast(string value, float seconds = 3) { toast.text = value; toastUntil = Time.unscaledTime + seconds; }
        public void ShowHud()
        {
            currentPage = "hud"; page.gameObject.SetActive(false); modal.gameObject.SetActive(false); hud.gameObject.SetActive(true);
            ResetControls(); RefreshHud(game.Health, game.MaximumHealth, game.RoomIndex, PolyusRunRules.EnemyCount(game.RoomIndex), 1, 0, game.RunGold);
        }
        private void Page(string id, string title = null)
        {
            currentPage = id; ResetControls(); hud.gameObject.SetActive(false); page.gameObject.SetActive(true); Clear(page);
            if (art.Background != null) Cover(page, Color.white, art.Background);
            Cover(page, new Color(.015f, .035f, .065f, id == "home" ? .68f : .94f));
            if (title != null)
            {
                Text(page, title, new Vector2(0, 365), new Vector2(850, 80), 46);
                Button(page, "← НАЗАД", new Vector2(-650, 365), new Vector2(215, 60), ShowHome);
            }
            wallet = Text(page, "", new Vector2(430, 420), new Vector2(680, 40), 23, PolyusArt.Orange, TextAnchor.MiddleRight);
        }
        public void ShowHome()
        {
            Page("home");
            Text(page, "ОРБИТАЛЬНАЯ СТАНЦИЯ · СЕКТОР 01", new Vector2(-415, 260), new Vector2(650, 45), 24, PolyusArt.Cyan, TextAnchor.MiddleLeft);
            Text(page, "ПОЛЮС", new Vector2(-415, 165), new Vector2(670, 150), 104, null, TextAnchor.MiddleLeft);
            Accent(page, new Vector2(-640, 65), new Vector2(200, 5), PolyusArt.Orange);
            Text(page, "Меняй гравитацию.\nПобеди стража станции.", new Vector2(-415, 3), new Vector2(650, 100), 30, muted, TextAnchor.MiddleLeft);
            Image(page, "Hero aura", new Vector2(390, 0), new Vector2(670, 670), new Color(.15f, .75f, 1, .4f), art.Glow);
            Image(page, "Hero key art", new Vector2(390, 12), new Vector2(560, 580), Color.white, art.Hero);
            Text(page, "3 КОМНАТЫ  /  2 УСИЛЕНИЯ  /  1 БОСС", new Vector2(400, -290), new Vector2(650, 55), 25, PolyusArt.Cyan);
            bool resume = game.HasRun && !game.IsEnded;
            Button(page, resume ? "ПРОДОЛЖИТЬ ЗАБЕГ  →" : "НАЧАТЬ ЗАБЕГ  →", new Vector2(-430, -115), new Vector2(620, 96), () => { if (resume) game.Resume(); else game.BeginRun(); }, true);
            Button(page, "МАГАЗИН", new Vector2(-640, -225), new Vector2(200, 68), ShowShop);
            Button(page, "ГЕРОЙ", new Vector2(-430, -225), new Vector2(200, 68), ShowInventory);
            Button(page, "ПРОПУСК", new Vector2(-220, -225), new Vector2(200, 68), ShowPass);
            Button(page, services.Lootboxes.CanOpenDaily ? "ЕЖЕДНЕВНЫЙ СУНДУК · ЗА РЕКЛАМУ" : "СУНДУК ПОЛУЧЕН · ДО ЗАВТРА", new Vector2(-430, -320), new Vector2(620, 70), DailyChest, false, services.Lootboxes.CanOpenDaily);
            Text(page, "ДЕМО 0.2  ·  ТЕСТОВЫЕ ПОКУПКИ БЕЗ СПИСАНИЙ", new Vector2(-250, -420), new Vector2(1000, 35), 20, muted);
            Button(page, game.Audio.Muted ? "ЗВУК: ВЫКЛ" : "ЗВУК: ВКЛ", new Vector2(635, -390), new Vector2(250, 55), () => { game.Audio.ToggleMute(); ShowHome(); });
        }
        private RectTransform Card(string title, Vector2 position, Color accent)
        {
            var card = Image(page, title, position, new Vector2(430, 500), panel).rectTransform;
            Accent(card, new Vector2(0, 248), new Vector2(430, 4), accent);
            Text(card, title, new Vector2(0, 190), new Vector2(400, 65), 29);
            return card;
        }
        private void ChestIcon(Transform parent, Vector2 position, Color color)
        {
            Image(parent, "Chest aura", position, new Vector2(250, 200), new Color(color.r, color.g, color.b, .5f), art.Glow);
            Image(parent, "Chest shell", position, new Vector2(175, 110), new Color(.17f, .26f, .32f));
            Image(parent, "Chest lid", position + Vector2.up * 37, new Vector2(180, 35), color);
            Accent(parent, position + Vector2.up * 3, new Vector2(180, 6), color);
            Image(parent, "Chest latch", position, new Vector2(30, 45), color);
        }
        private void ShowShop()
        {
            Page("shop", "СНАБЖЕНИЕ СТАНЦИИ");
            var crystals = Card("КРИСТАЛЛЫ", new Vector2(-470, 5), PolyusArt.Cyan);
            Image(crystals, "Crystal aura", new Vector2(0, 60), new Vector2(240, 220), PolyusArt.Cyan, art.Glow);
            Text(crystals, "100", new Vector2(0, 75), new Vector2(300, 130), 88, PolyusArt.Cyan);
            Text(crystals, "Для сундуков с экипировкой.\nСейчас — тестовая покупка.", new Vector2(0, -45), new Vector2(390, 95), 25, muted);
            Button(crystals, "100 КРИСТАЛЛОВ · ТЕСТ", new Vector2(0, -170), new Vector2(380, 72), () => ConfirmPurchase(IAPManager.CrystalPack));
            var gear = Card("АРСЕНАЛ", new Vector2(0, 5), PolyusArt.Orange);
            ChestIcon(gear, new Vector2(0, 70), PolyusArt.Orange);
            Text(gear, "Оружие или броня\nОбычное 70% · Редкое 25%\nЭпическое 5%", new Vector2(0, -55), new Vector2(390, 110), 24, muted);
            Button(gear, "ОТКРЫТЬ · 50 КРИСТАЛЛОВ", new Vector2(0, -170), new Vector2(380, 72), BuyChest, true, services.Economy.State.crystals >= 50);
            var daily = Card("ЕЖЕДНЕВНЫЙ ГРУЗ", new Vector2(470, 5), new Color(.65f, .4f, 1));
            ChestIcon(daily, new Vector2(0, 70), new Color(.65f, .4f, 1));
            Text(daily, "100 золота + снаряжение\nЗа полный просмотр ролика.\nОдин раз в день (UTC).", new Vector2(0, -55), new Vector2(390, 110), 24, muted);
            Button(daily, services.Lootboxes.CanOpenDaily ? "СМОТРЕТЬ И ПОЛУЧИТЬ" : "УЖЕ ПОЛУЧЕНО", new Vector2(0, -170), new Vector2(380, 72), DailyChest, false, services.Lootboxes.CanOpenDaily);
            Text(page, "Покупки и реклама демонстрационные. Реальные деньги не списываются.", new Vector2(0, -335), new Vector2(1350, 60), 23, muted);
        }
        private void ShowPass()
        {
            Page("pass", "ПРОПУСК ПЕРВОПРОХОДЦА");
            int xp = services.Economy.State.battlePassXp;
            Text(page, $"ОПЫТ {xp} / 500   ·   25 ОПЫТА ЗА ДРОНА   ·   100 ЗА ПОБЕДУ", new Vector2(0, 265), new Vector2(1350, 50), 24, PolyusArt.Cyan);
            var fill = Bar(page, new Vector2(0, 220), new Vector2(1300, 12), PolyusArt.Cyan); fill.fillAmount = xp / 500f;
            for (int i = 1; i <= 5; i++)
            {
                int tier = i; float y = 145 - (i - 1) * 93;
                Image(page, "Tier", new Vector2(0, y), new Vector2(1330, 80), panel);
                Text(page, $"{tier:00}  /  {tier * 100} XP", new Vector2(-490, y), new Vector2(310, 60), 27, PolyusArt.Cyan);
                bool free = services.Pass.IsClaimed(tier, false), premium = services.Pass.IsClaimed(tier, true);
                Button(page, free ? "ПОЛУЧЕНО" : $"{tier * 50} ЗОЛОТА · БЕСПЛАТНО", new Vector2(-70, y), new Vector2(480, 62), () => { services.Pass.Claim(tier, false); game.Audio.Reward(); ShowPass(); }, false, services.Pass.CanClaim(tier, false));
                Button(page, premium ? "ПОЛУЧЕНО" : "20 КРИСТАЛЛОВ · ПРЕМИУМ", new Vector2(440, y), new Vector2(480, 62), () => { services.Pass.Claim(tier, true); game.Audio.Reward(); ShowPass(); }, false, services.Pass.CanClaim(tier, true));
            }
            Button(page, services.Economy.State.premiumPass ? "ПРЕМИУМ АКТИВЕН" : "ОТКРЫТЬ ПРЕМИУМ · ТЕСТ", new Vector2(0, -360), new Vector2(650, 75), () => ConfirmPurchase(IAPManager.PremiumPass), true, !services.Economy.State.premiumPass);
        }
        private static string RarityName(ItemRarity rarity) => rarity == ItemRarity.Common ? "ОБЫЧНОЕ" : rarity == ItemRarity.Rare ? "РЕДКОЕ" : "ЭПИЧЕСКОЕ";
        private static Color RarityColor(ItemRarity rarity) => rarity == ItemRarity.Common ? new Color(.6f, .75f, .8f) : rarity == ItemRarity.Rare ? PolyusArt.Cyan : new Color(.75f, .4f, 1);
        private void ShowInventory()
        {
            Page("inventory", "СНАРЯЖЕНИЕ ГЕРОЯ");
            var state = services.Economy.State;
            Text(page, "Выбранные предметы усиливают следующий забег.", new Vector2(0, 290), new Vector2(1350, 55), 25, muted);
            if (state.inventory.Count == 0)
            {
                Image(page, "Astronaut", new Vector2(-320, -30), new Vector2(420, 440), Color.white, art.Hero);
                Text(page, "АРСЕНАЛ ПОКА ПУСТ\n\nОткрой ежедневный сундук\nили загляни в магазин.", new Vector2(260, 30), new Vector2(650, 260), 31, muted);
                Button(page, "К СУНДУКАМ  →", new Vector2(260, -180), new Vector2(500, 75), ShowShop, true);
                return;
            }
            int pages = Mathf.Max(1, (state.inventory.Count + 5) / 6);
            inventoryPage = Mathf.Clamp(inventoryPage, 0, pages - 1);
            for (int slot = 0; slot < 6; slot++)
            {
                int index = state.inventory.Count - 1 - (inventoryPage * 6 + slot);
                if (index < 0) break;
                var item = state.inventory[index];
                float x = -470 + slot % 3 * 470, y = 120 - slot / 3 * 270;
                var card = Image(page, "Gear", new Vector2(x, y), new Vector2(430, 245), panel).rectTransform;
                var color = RarityColor(item.rarity);
                Accent(card, new Vector2(0, 120), new Vector2(430, 4), color);
                Text(card, RarityName(item.rarity), new Vector2(0, 80), new Vector2(400, 40), 22, color);
                string name = item.kind == EquipmentKind.Weapon ? "ИМПУЛЬСНАЯ ВИНТОВКА" : "ГРАВИТАЦИОННАЯ БРОНЯ";
                string bonus = item.kind == EquipmentKind.Weapon ? "+" + PolyusRunRules.WeaponBonus((int)item.rarity) + " УРОНА" : "+" + PolyusRunRules.ArmorBonus((int)item.rarity) + " ЗДОРОВЬЯ";
                Text(card, name, new Vector2(0, 25), new Vector2(410, 55), 25);
                Text(card, bonus, new Vector2(0, -25), new Vector2(410, 40), 24, PolyusArt.Cyan);
                bool equipped = item.id == state.equippedArmorId || item.id == state.equippedWeaponId;
                Button(card, equipped ? "ЭКИПИРОВАНО" : "ЭКИПИРОВАТЬ", new Vector2(0, -82), new Vector2(380, 55), () =>
                {
                    if (item.kind == EquipmentKind.Weapon) state.equippedWeaponId = item.id; else state.equippedArmorId = item.id;
                    services.Economy.Save(); ShowInventory();
                }, false, !equipped);
            }
            Button(page, "←", new Vector2(-210, -365), new Vector2(120, 60), () => { inventoryPage--; ShowInventory(); }, false, inventoryPage > 0);
            Text(page, $"{inventoryPage + 1} / {pages}", new Vector2(0, -365), new Vector2(220, 50), 26);
            Button(page, "→", new Vector2(210, -365), new Vector2(120, 60), () => { inventoryPage++; ShowInventory(); }, false, inventoryPage < pages - 1);
        }
        public void ShowPause()
        {
            Page("pause", "ПАУЗА");
            Text(page, "Станция подождёт.", new Vector2(0, 140), new Vector2(1000, 65), 32, muted);
            Button(page, "ПРОДОЛЖИТЬ", new Vector2(0, 10), new Vector2(650, 95), () => game.Resume(), true);
            Button(page, "В ГЛАВНОЕ МЕНЮ", new Vector2(0, -115), new Vector2(650, 75), () => game.OpenHome());
            Button(page, "НАЧАТЬ ЗАНОВО", new Vector2(0, -220), new Vector2(650, 70), () => game.BeginRun());
        }
        public void ShowUpgrades()
        {
            Page("upgrade", "ВЫБЕРИ УСИЛЕНИЕ");
            Text(page, "Один модуль перед следующим сектором.", new Vector2(0, 275), new Vector2(1300, 55), 27, muted);
            string[] titles = { "ПРОРЫВ", "СКОРОСТРЕЛЬНОСТЬ", "РЕЗЕРВ" };
            string[] marks = { "+30%", "+28%", "+25" };
            string[] descriptions = { "Больше урона\nот каждого импульса.", "Стреляй чаще.\nПлотнее огонь — быстрее победа.", "+25 к максимуму здоровья.\nПолностью восстанавливает HP." };
            for (int i = 0; i < 3; i++)
            {
                int choice = i;
                var card = Card(titles[i], new Vector2(-470 + i * 470, -10), i == 2 ? PolyusArt.Cyan : PolyusArt.Orange);
                Text(card, marks[i], new Vector2(0, 70), new Vector2(400, 120), 75, i == 2 ? PolyusArt.Cyan : PolyusArt.Orange);
                Text(card, descriptions[i], new Vector2(0, -40), new Vector2(390, 110), 27, muted);
                Button(card, "УСТАНОВИТЬ", new Vector2(0, -170), new Vector2(380, 75), () => game.ChooseUpgrade(choice), true);
            }
        }
        public void ShowResult(bool offerRevive)
        {
            Page("defeat", "СВЯЗЬ ПОТЕРЯНА");
            Text(page, $"СЕКТОР {game.RoomIndex + 1} / 3   ·   +{game.RunGold} ЗОЛОТА СОХРАНЕНО", new Vector2(0, 220), new Vector2(1300, 65), 28, PolyusArt.Orange);
            Image(page, "Hero", new Vector2(-420, -20), new Vector2(350, 380), new Color(.55f, .65f, .7f), art.Hero);
            if (offerRevive)
            {
                Text(page, "ЕЩЁ ОДИН ШАНС\n\nВозродись с 50% здоровья\nза полный просмотр рекламы.\nОдин раз за забег.", new Vector2(250, 60), new Vector2(750, 260), 29);
                Button(page, "СМОТРЕТЬ И ВЕРНУТЬСЯ", new Vector2(250, -145), new Vector2(700, 85), Revive, true);
                Button(page, "ЗАВЕРШИТЬ ЗАБЕГ", new Vector2(250, -255), new Vector2(700, 65), () => game.DeclineRevive());
            }
            else
            {
                Text(page, "Заработанное золото и опыт\nостаются с тобой.", new Vector2(250, 65), new Vector2(750, 180), 32, muted);
                Button(page, "НОВЫЙ ЗАБЕГ", new Vector2(250, -100), new Vector2(700, 90), () => game.BeginRun(), true);
                Button(page, "СНАРЯЖЕНИЕ", new Vector2(250, -220), new Vector2(700, 70), ShowInventory);
            }
        }
        public void ShowVictory()
        {
            Page("victory", "СТАНЦИЯ ОСВОБОЖДЕНА");
            Image(page, "Hero", new Vector2(-410, -20), new Vector2(420, 460), Color.white, art.Hero);
            Text(page, "СТРАЖ ПОВЕРЖЕН", new Vector2(260, 170), new Vector2(800, 70), 45, PolyusArt.Cyan);
            int seconds = Mathf.FloorToInt(game.RunTime);
            Text(page, $"3 / 3 СЕКТОРА\n{seconds / 60:00}:{seconds % 60:00} В ПОЛЁТЕ\n\n+{game.RunGold} ЗОЛОТА ЗА ЗАБЕГ\n+5 КРИСТАЛЛОВ ЗА ПОБЕДУ", new Vector2(260, -5), new Vector2(750, 260), 30, muted);
            Button(page, "СНОВА В БОЙ", new Vector2(260, -210), new Vector2(650, 90), () => game.BeginRun(), true);
            Button(page, "ПОЛУЧИТЬ НАГРАДЫ ПРОПУСКА", new Vector2(260, -320), new Vector2(650, 65), ShowPass);
        }
        private void Dialog(string title, string description)
        {
            modal.gameObject.SetActive(true); Clear(modal);
            Cover(modal, new Color(.005f, .015f, .03f, .94f));
            Image(modal, "Dialog plate", Vector2.zero, new Vector2(980, 570), panel);
            Accent(modal, new Vector2(0, 285), new Vector2(980, 4), PolyusArt.Cyan);
            Text(modal, title, new Vector2(0, 180), new Vector2(900, 100), 40);
            Text(modal, description, new Vector2(0, 30), new Vector2(870, 160), 27, muted);
            ResetControls();
        }
        private void CloseDialog()
        {
            pendingPurchase = false; modal.gameObject.SetActive(false);
            game.SetExternalBlocked(advertising || services.IAP.IsBusy);
        }
        private void Info(string title, string message)
        {
            pendingPurchase = true; Dialog(title, message);
            Button(modal, "ПОНЯТНО", new Vector2(0, -180), new Vector2(620, 80), CloseDialog, true);
            game.SetExternalBlocked(true);
        }
        private void ConfirmPurchase(string product)
        {
            pendingPurchase = true;
            Dialog(services.IAP.IsSimulation ? "ТЕСТОВАЯ ПОКУПКА" : "ПОКУПКА В МАГАЗИНЕ", services.IAP.IsSimulation
                ? "Это симуляция. Реальные деньги не списываются.\n" + (product == IAPManager.CrystalPack ? "Получить 100 кристаллов?" : "Открыть премиум-награды пропуска?")
                : "Платёжный SDK ещё не подключён.\nПокупки в этой сборке недоступны.");
            Button(modal, "ПОДТВЕРДИТЬ", new Vector2(-210, -180), new Vector2(380, 80), () =>
            {
                services.IAP.Buy(product, (success, reason) =>
                {
                    CloseDialog(); RefreshCurrentPage();
                    if (success) { game.Audio.Reward(); Info("ПОЛУЧЕНО", product == IAPManager.CrystalPack ? "+100 кристаллов" : "Премиум-пропуск активирован."); }
                    else Info("ПОКУПКА НЕ ЗАВЕРШЕНА", "Награда не выдана.\nПроверь подключение магазина или повтори позже.");
                });
            }, true, services.IAP.IsSimulation);
            Button(modal, "ОТМЕНА", new Vector2(210, -180), new Vector2(380, 80), CloseDialog);
            game.SetExternalBlocked(true);
        }
        private void RefreshCurrentPage()
        {
            if (currentPage == "shop") ShowShop(); else if (currentPage == "pass") ShowPass();
            else if (currentPage == "inventory") ShowInventory(); else if (currentPage == "home") ShowHome();
        }
        private void BuyChest()
        {
            if (!services.Lootboxes.TryBuy(out var item)) return;
            ShowShop(); game.Audio.Reward();
            Info("ГРУЗ ДОСТАВЛЕН", RarityName(item.rarity) + " · " + (item.kind == EquipmentKind.Weapon ? "ОРУЖИЕ" : "БРОНЯ") + "\nПредмет доступен в разделе «Герой».");
        }
        private void DailyChest()
        {
            if (!services.Lootboxes.CanOpenDaily) return;
            ShowAd("daily_chest", () =>
            {
                if (!services.Lootboxes.TryOpenDaily(out var item)) return;
                RefreshCurrentPage(); game.Audio.Reward();
                Info("ЕЖЕДНЕВНЫЙ ГРУЗ", "+100 золота\n" + RarityName(item.rarity) + " · " + (item.kind == EquipmentKind.Weapon ? "ОРУЖИЕ" : "БРОНЯ"));
            });
        }
        private void Revive()
        {
            if (!game.CanRevive) return;
            ShowAd("revive", () => game.TryRevive());
        }
        private void ShowAd(string placement, Action reward)
        {
            if (advertising) return;
            advertising = true; adStarted = Time.realtimeSinceStartup;
            Dialog("ВОЗНАГРАЖДАЕМАЯ РЕКЛАМА", "Награда выдаётся только после полного просмотра.\nСейчас используется демонстрационный ролик.");
            adCountdown = Text(modal, "", new Vector2(0, -85), new Vector2(900, 60), 26, PolyusArt.Cyan);
            Button(modal, "ЗАКРЫТЬ БЕЗ НАГРАДЫ", new Vector2(0, -200), new Vector2(650, 65), () => services.Ads.Cancel());
            game.SetExternalBlocked(true);
            bool started = services.Ads.ShowRewarded(placement, () =>
            {
                advertising = false; modal.gameObject.SetActive(false); reward();
            }, outcome =>
            {
                advertising = false; adCountdown = null;
                if (outcome != RewardedAdResult.Completed)
                { modal.gameObject.SetActive(false); Info("ПРОСМОТР ПРЕРВАН", "Награда не выдана.\nТы можешь попробовать снова."); }
                game.SetExternalBlocked(pendingPurchase);
            });
            if (!started) { advertising = false; Info("РЕКЛАМА НЕДОСТУПНА", "Попробуй позже. Награда и попытка возрождения не потрачены."); }
        }
    }
}
