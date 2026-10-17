using System;
using System.Collections.Generic;
using Polyus.Monetization;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Polyus
{
    public enum PolyusPhase { Menu, Playing, Paused, Upgrade, Defeated, Victory }

    // Orchestrates a complete three-room run. Rendering, UI, audio and economy are separate services.
    public sealed class PolyusPrototype : MonoBehaviour
    {
        private const int WorldMask = 1 << 8;
        private readonly List<Enemy> enemies = new List<Enemy>();
        private readonly List<Projectile> projectiles = new List<Projectile>();
        private readonly List<Mote> motes = new List<Mote>();
        private PolyusArt art;
        private PolyusAudio audioPlayer;
        private PolyusUI ui;
        private MonetizationRoot services;
        private Transform world, actors, effects, heroVisual;
        private Rigidbody2D body;
        private SpriteRenderer hero, background;
        private Camera gameCamera;
        private PhysicsMaterial2D frictionless;
        private float move, fireTimer, hurtTimer, flipTimer, runTime, shake, roomTime;
        private bool jumpQueued, flipQueued, externallyBlocked, reviveUsed, reviveDeclined, roomReady;
        private bool runStarted;
        private int gravitySign = 1, kills, runGold, maximumHp = 100, health = 100;
        private float damage = 18, fireInterval = .28f;
        private string runId;
        public PolyusPhase Phase { get; private set; } = PolyusPhase.Menu;
        public int RoomIndex { get; private set; }
        public int Health => health;
        public int MaximumHealth => maximumHp;
        public int RunGold => runGold;
        public float RunTime => runTime;
        public bool HasRun => runStarted;
        public bool IsEnded => Phase == PolyusPhase.Defeated || Phase == PolyusPhase.Victory;
        public bool CanRevive => PolyusRunRules.CanRevive(Phase == PolyusPhase.Defeated, reviveUsed, reviveDeclined);
        public bool IsBlocked => externallyBlocked || Phase != PolyusPhase.Playing;
        public PolyusArt Art => art;
        public PolyusAudio Audio => audioPlayer;

        private sealed class Enemy
        {
            public Transform Root;
            public SpriteRenderer Visual, Bar;
            public Vector2 Origin;
            public float Timer, Health, Maximum;
            public bool Boss;
        }
        private sealed class Projectile
        {
            public Transform Root;
            public Vector2 Velocity;
            public float Life = 3.5f, Damage;
            public bool Hostile;
        }
        private sealed class Mote
        {
            public Transform Root;
            public SpriteRenderer Sprite;
            public Vector2 Velocity;
            public float Life, Maximum;
            public bool Coin;
            public TextMesh Text;
        }

        private void Start()
        {
#if UNITY_ANDROID || UNITY_IOS
            Screen.orientation = ScreenOrientation.LandscapeLeft;
#endif
            art = new PolyusArt();
            audioPlayer = gameObject.AddComponent<PolyusAudio>();
            services = MonetizationRoot.GetOrCreate();
            services.Attach(this);
            frictionless = new PhysicsMaterial2D("Polyus frictionless") { friction = 0, bounciness = 0 };
            var cam = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cam.transform.SetParent(transform, false);
            cam.tag = "MainCamera";
            gameCamera = cam.GetComponent<Camera>();
            gameCamera.orthographic = true;
            gameCamera.clearFlags = CameraClearFlags.SolidColor;
            gameCamera.backgroundColor = PolyusArt.Ink;
            cam.transform.localPosition = new Vector3(0, 0, -10);
            if (art.Background != null) background = art.Draw(transform, "Orbital greenhouse", art.Background, Vector2.zero, new Vector2(24, 14), Color.white, -30);
            effects = new GameObject("Transient effects").transform;
            effects.SetParent(transform, false);
            ui = gameObject.AddComponent<PolyusUI>();
            ui.Initialize(this, services, art);
            FitCamera();
            ui.ShowHome();
        }

        private void FitCamera()
        {
            gameCamera.orthographicSize = Mathf.Max(6.2f, 10.7f / Mathf.Max(.1f, gameCamera.aspect));
            if (background != null)
            {
                float h = gameCamera.orthographicSize * 2;
                float w = h * gameCamera.aspect;
                float scale = Mathf.Max(w / background.sprite.bounds.size.x, h / background.sprite.bounds.size.y);
                background.transform.localScale = Vector3.one * (scale * 1.02f);
            }
        }

        public void BeginRun()
        {
            if (services.Ads.IsShowing) return;
            runStarted = true;
            runId = Guid.NewGuid().ToString("N");
            kills = runGold = 0;
            runTime = 0;
            reviveUsed = reviveDeclined = false;
            maximumHp = 100;
            damage = 18;
            fireInterval = .28f;
            var state = services.Economy.State;
            foreach (var item in state.inventory)
            {
                if (item.id == state.equippedArmorId) maximumHp += PolyusRunRules.ArmorBonus((int)item.rarity);
                if (item.id == state.equippedWeaponId) damage += PolyusRunRules.WeaponBonus((int)item.rarity);
            }
            health = maximumHp;
            RoomIndex = 0;
            Phase = PolyusPhase.Playing;
            BuildRoom();
            ui.ShowHud();
            ui.Toast("СЕКТОР 01 · ОРБИТАЛЬНЫЙ САД", 3);
        }

        public void SetExternalBlocked(bool blocked)
        {
            externallyBlocked = blocked;
            ApplySimulation();
        }
        private void ApplySimulation() { if (body != null) body.simulated = !IsBlocked; }
        public void RequestJump() => jumpQueued = true;
        public void RequestFlip() => flipQueued = true;
        public void RestartRun() => BeginRun();
        public void Pause()
        {
            if (Phase != PolyusPhase.Playing || externallyBlocked) return;
            Phase = PolyusPhase.Paused;
            move = 0; jumpQueued = flipQueued = false;
            ApplySimulation();
            ui.ShowPause();
        }
        public void Resume()
        {
            if (!runStarted || IsEnded) { BeginRun(); return; }
            Phase = PolyusPhase.Playing;
            jumpQueued = flipQueued = false;
            ApplySimulation();
            ui.ShowHud();
        }
        public void OpenHome()
        {
            if (services.Ads.IsShowing) return;
            if (Phase == PolyusPhase.Playing) Phase = PolyusPhase.Paused;
            ApplySimulation();
            ui.ShowHome();
        }
        public void DeclineRevive() { reviveDeclined = true; ui.ShowResult(false); }
        public bool TryRevive()
        {
            if (!CanRevive) return false;
            reviveUsed = true;
            health = PolyusRunRules.ReviveHealth(maximumHp);
            ResetHero();
            ClearProjectiles();
            hurtTimer = 2;
            Phase = PolyusPhase.Playing;
            ApplySimulation();
            ui.ShowHud();
            ui.Toast("ВТОРОЙ ШАНС · 50% ЗДОРОВЬЯ", 3);
            audioPlayer.Reward();
            return true;
        }

        private Transform Container(string label)
        {
            var root = new GameObject(label).transform;
            root.SetParent(transform, false);
            return root;
        }
        private void BuildRoom()
        {
            if (world != null) { world.gameObject.SetActive(false); Destroy(world.gameObject); }
            if (actors != null) { actors.gameObject.SetActive(false); Destroy(actors.gameObject); }
            ClearProjectiles();
            foreach (var mote in motes) Destroy(mote.Root.gameObject);
            motes.Clear(); enemies.Clear();
            world = Container("Sector geometry"); actors = Container("Combat actors");
            roomReady = false; roomTime = 0; fireTimer = 0; hurtTimer = 1;
            if (background != null) background.color = RoomIndex == 2 ? new Color(.72f, .66f, .85f)
                : RoomIndex == 1 ? new Color(.65f, .85f, .8f) : new Color(.8f, .9f, 1);
            Platform(new Vector2(0, -4.65f), new Vector2(21, .7f));
            Platform(new Vector2(0, 4.65f), new Vector2(21, .7f));
            Platform(new Vector2(-10.35f, 0), new Vector2(.45f, 9.5f));
            Platform(new Vector2(10.35f, 0), new Vector2(.45f, 9.5f));
            if (RoomIndex == 0)
            {
                Platform(new Vector2(-5.1f, -1.7f), new Vector2(4.1f, .38f));
                Platform(new Vector2(.2f, .3f), new Vector2(3.1f, .38f));
                Platform(new Vector2(5.4f, 2.25f), new Vector2(4, .38f));
            }
            else if (RoomIndex == 1)
            {
                Platform(new Vector2(-5.8f, 1.65f), new Vector2(3.3f, .38f));
                Platform(new Vector2(-1.2f, -1.55f), new Vector2(3.2f, .38f));
                Platform(new Vector2(4.5f, .6f), new Vector2(4, .38f));
            }
            else
            {
                Platform(new Vector2(-5, -1.5f), new Vector2(3.7f, .38f));
                Platform(new Vector2(4.5f, 1.6f), new Vector2(3.7f, .38f));
            }
            var player = new GameObject("Astronaut", typeof(Rigidbody2D), typeof(BoxCollider2D));
            player.transform.SetParent(actors, false);
            player.GetComponent<BoxCollider2D>().size = new Vector2(.62f, 1.08f);
            player.GetComponent<BoxCollider2D>().sharedMaterial = frictionless;
            body = player.GetComponent<Rigidbody2D>();
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            hero = art.Draw(player.transform, "Astronaut illustration", art.Hero, Vector2.zero, new Vector2(1.65f, 1.65f), Color.white, 10);
            heroVisual = hero.transform;
            ResetHero();
            if (RoomIndex == 0)
            {
                SpawnEnemy(new Vector2(-4.7f, 2.8f)); SpawnEnemy(new Vector2(2.5f, -2.8f));
                SpawnEnemy(new Vector2(6.3f, -.2f)); SpawnEnemy(new Vector2(8.2f, 3.5f));
            }
            else if (RoomIndex == 1)
            {
                SpawnEnemy(new Vector2(-7, 3.3f)); SpawnEnemy(new Vector2(-2.8f, .2f));
                SpawnEnemy(new Vector2(2.7f, 3.3f)); SpawnEnemy(new Vector2(5.3f, -2.5f)); SpawnEnemy(new Vector2(8.4f, 1.8f));
            }
            else SpawnEnemy(new Vector2(3, -1), true);
            ApplySimulation();
        }

        private void Platform(Vector2 point, Vector2 size)
        {
            var main = art.Draw(world, "Armored platform", art.White, point, size, new Color(.09f, .16f, .21f), 0);
            main.gameObject.layer = 8;
            main.gameObject.AddComponent<BoxCollider2D>();
            bool horizontal = size.x > size.y;
            if (!horizontal) return;
            art.Draw(world, "Beveled edge", art.White, point + Vector2.up * (size.y / 2 - .045f), new Vector2(size.x, .09f), new Color(.28f, .42f, .46f), 1);
            art.Draw(world, "Energy seam", art.White, point - Vector2.up * (size.y / 2 + .025f), new Vector2(size.x * .94f, .045f), PolyusArt.Cyan, 2);
            art.Draw(world, "Underglow", art.Glow, point - Vector2.up * .15f, new Vector2(size.x, .7f), new Color(.1f, .7f, .85f, .18f), -1);
            for (float x = -.5f * size.x + .35f; x < size.x / 2; x += .8f)
            {
                art.Draw(world, "Panel join", art.White, point + Vector2.right * x, new Vector2(.025f, size.y * .7f), new Color(.03f, .06f, .08f), 2);
                art.Draw(world, "Fastener", art.Disc, point + new Vector2(x + .1f, .02f), new Vector2(.045f, .045f), new Color(.5f, .58f, .56f), 3);
            }
        }
        private void ResetHero()
        {
            body.position = new Vector2(-7.8f, -3.65f);
            body.linearVelocity = Vector2.zero;
            gravitySign = 1; body.gravityScale = 3.2f;
            hero.flipX = hero.flipY = false;
            flipTimer = 0; move = 0; jumpQueued = flipQueued = false;
        }
        private void SpawnEnemy(Vector2 position, bool boss = false)
        {
            var root = new GameObject(boss ? "Sector guardian" : "Sentinel drone").transform;
            root.SetParent(actors, false); root.position = position;
            float size = boss ? 3.1f : 1.35f;
            art.Draw(root, "Energy aura", art.Glow, Vector2.zero, Vector2.one * (size * 1.5f), new Color(1, .28f, .05f, .24f), 3);
            var visual = art.Draw(root, boss ? "Guardian illustration" : "Drone illustration", boss ? art.Boss : art.Drone,
                Vector2.zero, Vector2.one * size, Color.white, 5);
            art.Draw(root, "Health rail", art.White, Vector2.up * (size * .42f), new Vector2(size * .7f, .065f), PolyusArt.Ink, 6);
            var bar = art.Draw(root, "Health", art.White, Vector2.up * (size * .42f), new Vector2(size * .7f, .065f), PolyusArt.Orange, 7);
            float hp = boss ? 600 : 48 + RoomIndex * 12;
            enemies.Add(new Enemy { Root = root, Visual = visual, Bar = bar, Origin = position,
                Timer = boss ? 2.5f : 1.4f + enemies.Count * .35f, Health = hp, Maximum = hp, Boss = boss });
        }

        private void Update()
        {
            if (art == null) return;
            FitCamera();
            shake = Mathf.MoveTowards(shake, 0, Time.unscaledDeltaTime * .8f);
            gameCamera.transform.localPosition = new Vector3(Mathf.Sin(Time.unscaledTime * 111) * shake, Mathf.Cos(Time.unscaledTime * 89) * shake, -10);
            if (body == null || IsBlocked) return;
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) { Pause(); return; }
            move = ui.Move;
            if (keyboard != null)
            {
                move += (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1 : 0)
                    - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1 : 0);
                jumpQueued |= keyboard.spaceKey.wasPressedThisFrame;
                flipQueued |= keyboard.gKey.wasPressedThisFrame;
            }
            jumpQueued |= ui.ConsumeJump(); flipQueued |= ui.ConsumeFlip();
            move = Mathf.Clamp(move, -1, 1);
            runTime += Time.deltaTime; roomTime += Time.deltaTime;
            hurtTimer -= Time.deltaTime; flipTimer -= Time.deltaTime; fireTimer -= Time.deltaTime;
            hero.color = hurtTimer > 0 && Mathf.Sin(Time.time * 35) > 0 ? new Color(.45f, 1, 1, .5f) : Color.white;
            heroVisual.localPosition = Vector3.up * (Mathf.Sin(roomTime * (Mathf.Abs(move) > .1f ? 17 : 3)) * .035f);
            heroVisual.localRotation = Quaternion.Euler(0, 0, -move * 4 * gravitySign);
            hero.flipY = gravitySign < 0;
            if (Mathf.Abs(move) > .1f) hero.flipX = move < 0;
            UpdateEnemies();
            if (Phase != PolyusPhase.Playing) return;
            UpdateProjectiles();
            if (Phase != PolyusPhase.Playing) return;
            UpdateMotes();
            if (enemies.Count == 0 && !roomReady)
            {
                roomReady = true;
                audioPlayer.Reward();
                if (RoomIndex == PolyusRunRules.RoomCount - 1) { Win(); return; }
                BuildPortal();
                ui.Toast("КОМНАТА ОЧИЩЕНА · ИДИ К ПОРТАЛУ СПРАВА", 4);
            }
            if (roomReady && body.position.x > 8.5f && body.position.y < -2.7f)
            {
                Phase = PolyusPhase.Upgrade; ApplySimulation(); ui.ShowUpgrades();
            }
            if (body.position.y < -7 || body.position.y > 7) { health = 0; Defeat(); }
            ui.RefreshHud(health, maximumHp, RoomIndex, enemies.Count, gravitySign, flipTimer, runGold);
        }
        private void FixedUpdate()
        {
            if (body == null || IsBlocked) return;
            if (flipQueued && flipTimer <= 0)
            {
                gravitySign *= -1; body.gravityScale = 3.2f * gravitySign;
                body.linearVelocity = new Vector2(body.linearVelocity.x, 0);
                flipTimer = .45f; audioPlayer.Flip(); shake = .045f;
                Burst(body.position, PolyusArt.Cyan, 18, 3.5f);
            }
            flipQueued = false;
            bool grounded = Physics2D.BoxCast(body.position, new Vector2(.5f, .08f), 0, Vector2.down * gravitySign, .59f, WorldMask);
            body.linearVelocity = new Vector2(move * 6.5f, jumpQueued && grounded ? 11 * gravitySign : body.linearVelocity.y);
            if (jumpQueued && grounded) Burst(body.position + Vector2.down * gravitySign * .5f, PolyusArt.Cyan, 5, 2);
            jumpQueued = false;
        }

        private void UpdateEnemies()
        {
            Enemy nearest = null;
            float best = 25;
            foreach (var enemy in enemies)
            {
                enemy.Root.position = enemy.Origin + new Vector2(Mathf.Sin(roomTime * .8f + enemy.Origin.y) * (enemy.Boss ? 1.7f : .35f), Mathf.Sin(roomTime * 1.4f + enemy.Origin.x) * (enemy.Boss ? .9f : .28f));
                enemy.Visual.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(roomTime * 2 + enemy.Origin.x) * 7);
                Vector2 delta = (Vector2)enemy.Root.position - body.position;
                bool visible = !Physics2D.Linecast(body.position, enemy.Root.position, WorldMask);
                if (visible && delta.magnitude < best) { nearest = enemy; best = delta.magnitude; }
                enemy.Timer -= Time.deltaTime;
                if (visible && enemy.Timer <= 0)
                {
                    Vector2 aim = -delta.normalized;
                    int count = enemy.Boss ? (enemy.Health < enemy.Maximum * .5f ? 5 : 3) : 1;
                    for (int i = 0; i < count; i++)
                    {
                        Vector2 direction = Quaternion.Euler(0, 0, (i - (count - 1) / 2f) * 14) * aim;
                        Fire(enemy.Root.position, direction * (enemy.Boss ? 5 : 4.4f), true, 20);
                    }
                    enemy.Timer = enemy.Boss ? .95f : 2.4f - RoomIndex * .2f;
                }
                if (delta.magnitude < (enemy.Boss ? 1.3f : .7f)) Hurt(20);
                if (Phase != PolyusPhase.Playing) return;
            }
            if (nearest != null && fireTimer <= 0)
            {
                Vector2 aim = ((Vector2)nearest.Root.position - body.position).normalized;
                hero.flipX = aim.x < 0;
                Fire(body.position + aim * .55f, aim * 17, false, damage);
                fireTimer = fireInterval;
                Burst(body.position + aim * .7f, PolyusArt.Cyan, 3, 1.1f);
                audioPlayer.Shot();
            }
            Enemy boss = enemies.Find(e => e.Boss);
            ui.SetBoss(boss != null, boss != null ? boss.Health / boss.Maximum : 0);
        }
        private void Fire(Vector2 position, Vector2 velocity, bool hostile, float amount)
        {
            var root = new GameObject(hostile ? "Plasma orb" : "Pulse shot").transform;
            root.SetParent(effects, false); root.position = position;
            Color color = hostile ? PolyusArt.Orange : PolyusArt.Cyan;
            art.Draw(root, "Glow", art.Glow, Vector2.zero, Vector2.one * .8f, new Color(color.r, color.g, color.b, .8f), 13);
            var core = art.Draw(root, "Core", hostile ? art.Disc : art.White, Vector2.zero, hostile ? Vector2.one * .2f : new Vector2(.36f, .09f), color, 14);
            core.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg);
            projectiles.Add(new Projectile { Root = root, Velocity = velocity, Hostile = hostile, Damage = amount });
        }
        private void UpdateProjectiles()
        {
            for (int i = projectiles.Count - 1; i >= 0; i--)
            {
                var shot = projectiles[i];
                Vector2 from = shot.Root.position, to = from + shot.Velocity * Time.deltaTime;
                var wall = Physics2D.Linecast(from, to, WorldMask);
                if (wall) to = wall.point;
                bool consumed = false;
                if (shot.Hostile)
                {
                    if (Distance(from, to, body.position) < .43f) { Hurt((int)shot.Damage); consumed = true; }
                }
                else
                {
                    Enemy hit = null; float closest = float.MaxValue;
                    foreach (var enemy in enemies)
                    {
                        if (Distance(from, to, enemy.Root.position) >= (enemy.Boss ? 1.15f : .48f)) continue;
                        float distance = Vector2.Distance(from, enemy.Root.position);
                        if (distance < closest) { hit = enemy; closest = distance; }
                    }
                    if (hit != null)
                    {
                        consumed = true; hit.Health -= shot.Damage;
                        Number(hit.Root.position, Mathf.RoundToInt(shot.Damage).ToString(), Color.white);
                        Burst(to, PolyusArt.Orange, 4, 2);
                        hit.Bar.transform.localScale = new Vector3((hit.Boss ? 3.1f : 1.35f) * .7f * Mathf.Clamp01(hit.Health / hit.Maximum), .065f, 1);
                        if (hit.Health <= 0) Kill(hit);
                    }
                }
                shot.Root.position = to; shot.Life -= Time.deltaTime;
                if (wall || consumed || shot.Life <= 0)
                {
                    if (wall) Burst(to, shot.Hostile ? PolyusArt.Orange : PolyusArt.Cyan, 3, 1);
                    Destroy(shot.Root.gameObject); projectiles.RemoveAt(i);
                }
                if (Phase != PolyusPhase.Playing) return;
            }
        }
        private static float Distance(Vector2 a, Vector2 b, Vector2 p) => (float)PolyusRunRules.SegmentDistance(a.x, a.y, b.x, b.y, p.x, p.y);
        private void Kill(Enemy enemy)
        {
            Vector2 point = enemy.Root.position;
            Burst(point, PolyusArt.Orange, enemy.Boss ? 45 : 18, enemy.Boss ? 7 : 4);
            for (int i = 0; i < 4; i++)
            {
                var coin = art.Draw(effects, "Gold pickup", art.Disc, point + UnityEngine.Random.insideUnitCircle * .4f, Vector2.one * .14f, new Color(1, .78f, .25f), 16);
                motes.Add(new Mote { Root = coin.transform, Sprite = coin, Coin = true, Life = 1.4f, Maximum = 1.4f });
            }
            audioPlayer.Explosion(); shake = enemy.Boss ? .2f : .09f;
            services.AwardDrone(runId, ++kills); runGold += 25;
            enemies.Remove(enemy); Destroy(enemy.Root.gameObject);
        }
        private void Hurt(int amount)
        {
            if (hurtTimer > 0 || IsBlocked) return;
            health = Mathf.Max(0, health - amount); hurtTimer = 1.1f;
            Burst(body.position, PolyusArt.Orange, 10, 3); Number(body.position, "−" + amount, PolyusArt.Orange);
            audioPlayer.Hit(); shake = .13f; ui.FlashDamage();
            if (health == 0) Defeat();
        }
        private void Defeat()
        {
            Phase = PolyusPhase.Defeated; ApplySimulation(); ui.ShowResult(CanRevive);
        }
        private void Win()
        {
            Phase = PolyusPhase.Victory; ApplySimulation();
            if (services.Economy.GrantOnce("clear:" + runId, 150, 5)) services.Pass.AwardCombatXp(100);
            runGold += 150;
            ui.ShowVictory();
        }
        private void BuildPortal()
        {
            art.Draw(world, "Exit light", art.Glow, new Vector2(9, -3.25f), new Vector2(2.5f, 3), new Color(.1f, 1, .8f, .9f), 4);
            art.Draw(world, "Exit core", art.White, new Vector2(9.2f, -3.25f), new Vector2(.12f, 1.7f), Color.white, 5);
            Number(new Vector2(8.9f, -2.1f), "ВЫХОД →", PolyusArt.Cyan, 12);
        }
        public void ChooseUpgrade(int choice)
        {
            if (Phase != PolyusPhase.Upgrade || externallyBlocked) return;
            if (choice == 0) damage *= 1.3f;
            else if (choice == 1) fireInterval *= .78f;
            else { maximumHp += 25; health = maximumHp; }
            RoomIndex++; Phase = PolyusPhase.Playing; BuildRoom(); ui.ShowHud(); audioPlayer.Reward();
            ui.Toast(RoomIndex == 2 ? "СЕКТОР 03 · СТРАЖ ПОЛЮСА" : "СЕКТОР 02 · ЗАРОСШИЙ РЕАКТОР", 3);
        }
        private void Burst(Vector2 point, Color color, int count, float speed)
        {
            for (int i = 0; i < count; i++)
            {
                var spark = art.Draw(effects, "Energy spark", art.Disc, point, Vector2.one * UnityEngine.Random.Range(.04f, .11f), color, 17);
                float life = UnityEngine.Random.Range(.2f, .55f);
                motes.Add(new Mote { Root = spark.transform, Sprite = spark, Velocity = UnityEngine.Random.insideUnitCircle * speed, Life = life, Maximum = life });
            }
        }
        private void Number(Vector2 point, string value, Color color, float lifetime = .8f)
        {
            var item = new GameObject("Combat text", typeof(TextMesh)); item.transform.SetParent(effects, false); item.transform.position = point;
            var text = item.GetComponent<TextMesh>(); text.text = value; text.font = art.Font; text.fontSize = 48; text.characterSize = .065f;
            text.anchor = TextAnchor.MiddleCenter; text.color = color;
            item.GetComponent<MeshRenderer>().sharedMaterial = art.Font.material;
            item.GetComponent<MeshRenderer>().sortingOrder = 20;
            motes.Add(new Mote { Root = item.transform, Text = text, Velocity = Vector2.up * .6f, Life = lifetime, Maximum = lifetime });
        }
        private void UpdateMotes()
        {
            for (int i = motes.Count - 1; i >= 0; i--)
            {
                var mote = motes[i]; mote.Life -= Time.deltaTime;
                if (mote.Coin) mote.Root.position = Vector3.Lerp(mote.Root.position, body.position, Time.deltaTime * 6);
                else mote.Root.position += (Vector3)(mote.Velocity * Time.deltaTime);
                float alpha = Mathf.Clamp01(mote.Life / mote.Maximum);
                if (mote.Sprite != null) { Color c = mote.Sprite.color; c.a = alpha; mote.Sprite.color = c; }
                if (mote.Text != null) { Color c = mote.Text.color; c.a = alpha; mote.Text.color = c; }
                if (mote.Life <= 0) { Destroy(mote.Root.gameObject); motes.RemoveAt(i); }
            }
        }
        private void ClearProjectiles() { foreach (var shot in projectiles) if (shot.Root != null) Destroy(shot.Root.gameObject); projectiles.Clear(); }
        private void OnApplicationFocus(bool focused) { if (!focused && Phase == PolyusPhase.Playing) Pause(); }
        private void OnDestroy() { art?.Dispose(); if (frictionless != null) Destroy(frictionless); }
    }
}
