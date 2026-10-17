using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Polyus.Monetization;

namespace Polyus
{
    public sealed class PolyusPrototype : MonoBehaviour
    {
        private const int WorldLayer = 8;
        private const float Speed = 6f;
        private readonly List<Drone> drones = new List<Drone>();
        private readonly List<Shot> shots = new List<Shot>();
        private readonly Color cyan = new Color(.15f, .9f, 1f);
        private readonly Color orange = new Color(1f, .48f, .12f);
        private Sprite square;
        private Texture2D texture;
        private PhysicsMaterial2D playerMaterial;
        private Font font;
        private Rigidbody2D body;
        private SpriteRenderer hero;
        private PolyusControl left, right, jump, gravity;
        private RectTransform safeArea;
        private CanvasGroup controls;
        private Text status, result;
        private float direction, fireTimer, hurtTimer, gravityTimer;
        private bool jumpQueued, flipQueued, ended, paused, externalBlocked, reviveUsed, reviveDeclined;
        public const int MaxHealth = 100;
        private int health = MaxHealth, defeated;
        private int gravitySign = 1;
        private readonly string runId = System.Guid.NewGuid().ToString("N");
        private MonetizationRoot monetization;
        public bool IsEnded => ended;
        public bool CanRevive => ended && health <= 0 && !reviveUsed && !reviveDeclined;

        public void SetExternalBlocked(bool blocked)
        {
            externalBlocked = blocked;
            if (controls != null) { controls.interactable = !blocked; controls.blocksRaycasts = !blocked; }
            if (body != null) body.simulated = !blocked && !ended && !paused;
            if (blocked)
            {
                direction = 0;
                jumpQueued = flipQueued = false;
                if (left != null) { left.Clear(); right.Clear(); jump.Clear(); gravity.Clear(); }
            }
        }

        public bool TryRevive()
        {
            if (!CanRevive) return false;
            reviveUsed = true;
            health = MaxHealth / 2;
            ended = false;
            paused = false;
            hurtTimer = 2;
            body.position = new Vector2(-7, -3.5f);
            body.linearVelocity = Vector2.zero;
            gravitySign = 1;
            body.gravityScale = 3;
            hero.flipY = false;
            foreach (var shot in shots) Destroy(shot.Transform.gameObject);
            shots.Clear();
            result.text = "";
            body.simulated = !externalBlocked;
            return true;
        }

        public void DeclineRevive() => reviveDeclined = true;
        public void RestartRun() => Restart();

        private sealed class Drone
        {
            public Transform Transform;
            public Vector2 Origin;
            public float Timer;
            public int Health = 3;
        }

        private sealed class Shot
        {
            public Transform Transform;
            public Vector2 Velocity;
            public bool Hostile;
            public float Lifetime = 4;
        }

        private void Start()
        {
            texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            square = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(.5f, .5f), 1);
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            BuildRoom();
            BuildUI();
            monetization = MonetizationRoot.GetOrCreate();
            monetization.Attach(this);
        }

        private GameObject Block(string label, Vector2 position, Vector2 size, Color color, bool solid = false)
        {
            var item = new GameObject(label);
            item.transform.SetParent(transform);
            item.transform.position = position;
            item.transform.localScale = size;
            var renderer = item.AddComponent<SpriteRenderer>();
            renderer.sprite = square;
            renderer.color = color;
            if (solid)
            {
                item.layer = WorldLayer;
                item.AddComponent<BoxCollider2D>();
            }
            return item;
        }

        private void Platform(Vector2 position, Vector2 size)
        {
            Block("Platform", position, size, new Color(.12f, .24f, .3f), true);
            Block("Platform light", position + Vector2.up * (size.y / 2 - .035f), new Vector2(size.x, .07f), cyan);
            Block("Ceiling light", position - Vector2.up * (size.y / 2 - .035f), new Vector2(size.x, .07f), cyan);
        }

        private void BuildRoom()
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.transform.SetParent(transform);
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0, 0, -10);
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 6;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.025f, .055f, .1f);
            // Fit the entire room even on a narrower Game view or phone screen.
            camera.orthographicSize = Mathf.Max(6f, 10.5f / camera.aspect);
            for (int i = -9; i <= 9; i++)
                Block("Background rib", new Vector2(i, 0), new Vector2(.025f, 9), new Color(.035f, .09f, .14f));
            Platform(new Vector2(0, -4.8f), new Vector2(20, .6f));
            Platform(new Vector2(0, 4.8f), new Vector2(20, .6f));
            Platform(new Vector2(-10, 0), new Vector2(.5f, 10));
            Platform(new Vector2(10, 0), new Vector2(.5f, 10));
            Platform(new Vector2(-5, -1.8f), new Vector2(4, .4f));
            Platform(new Vector2(0, .5f), new Vector2(3, .4f));
            Platform(new Vector2(5, 2.4f), new Vector2(4, .4f));

            var player = Block("Hero", new Vector2(-7, -3.5f), new Vector2(.65f, .9f), cyan);
            hero = player.GetComponent<SpriteRenderer>();
            hero.sortingOrder = 5;
            var collider = player.AddComponent<BoxCollider2D>();
            body = player.AddComponent<Rigidbody2D>();
            body.freezeRotation = true;
            body.gravityScale = 3;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            playerMaterial = new PhysicsMaterial2D("Prototype frictionless") { friction = 0, bounciness = 0 };
            collider.sharedMaterial = playerMaterial;
            var visor = Block("Visor", Vector2.zero, new Vector2(.45f, .22f), new Color(.025f, .08f, .15f));
            visor.transform.SetParent(player.transform, false);
            visor.transform.localPosition = new Vector3(.08f, .15f, -.1f);
            foreach (Vector2 point in new[] { new Vector2(-5, 2.7f), new Vector2(2, -2.5f), new Vector2(6.5f, -.3f), new Vector2(8, 3.7f) })
            {
                var drone = Block("Drone", point, new Vector2(.7f, .7f), orange);
                drone.transform.rotation = Quaternion.Euler(0, 0, 45);
                drones.Add(new Drone { Transform = drone.transform, Origin = point, Timer = 1.5f + drones.Count * .45f });
            }
        }

        private void BuildUI()
        {
            var canvasObject = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            controls = canvasObject.AddComponent<CanvasGroup>();
            canvasObject.transform.SetParent(transform);
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = .5f;
            safeArea = new GameObject("Safe area", typeof(RectTransform)).GetComponent<RectTransform>();
            safeArea.SetParent(canvasObject.transform, false);
            var events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            events.transform.SetParent(transform);
            events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            status = Label("Status", "", new Vector2(0, 1), new Vector2(330, -35), new Vector2(640, 55), 24);
            Label("Help", "A/D or arrows: move   SPACE: jump   G: gravity   R: restart", new Vector2(.5f, 1), new Vector2(0, -78), new Vector2(950, 35), 18);
            left = Control("<", new Vector2(0, 0), new Vector2(65, 65));
            right = Control(">", new Vector2(0, 0), new Vector2(180, 65));
            gravity = Control("FLIP", new Vector2(1, 0), new Vector2(-190, 65));
            jump = Control("JUMP", new Vector2(1, 0), new Vector2(-65, 65));
            var pause = Control("II", new Vector2(1, 1), new Vector2(-65, -35));
            pause.gameObject.AddComponent<Button>().onClick.AddListener(TogglePause);
            result = Label("Result", "", new Vector2(.5f, .5f), Vector2.zero, new Vector2(850, 150), 30);
            var restart = Control("RETRY", new Vector2(.5f, 0), new Vector2(0, 65));
            restart.gameObject.AddComponent<Button>().onClick.AddListener(Restart);
            UpdateSafeArea();
        }

        private RectTransform Rect(string label, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(label, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(safeArea, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }

        private Text Label(string name, string text, Vector2 anchor, Vector2 position, Vector2 size, int fontSize)
        {
            var label = Rect(name, anchor, position, size).gameObject.AddComponent<Text>();
            label.font = font;
            label.text = text;
            label.fontSize = fontSize;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.raycastTarget = false;
            return label;
        }

        private PolyusControl Control(string text, Vector2 anchor, Vector2 position)
        {
            var rect = Rect(text, anchor, position, new Vector2(100, 85));
            rect.gameObject.AddComponent<Image>().color = new Color(.08f, .23f, .31f, .92f);
            var caption = new GameObject("Caption", typeof(RectTransform)).AddComponent<Text>();
            caption.rectTransform.SetParent(rect, false);
            caption.rectTransform.anchorMin = Vector2.zero;
            caption.rectTransform.anchorMax = Vector2.one;
            caption.rectTransform.sizeDelta = Vector2.zero;
            caption.font = font;
            caption.fontSize = 24;
            caption.alignment = TextAnchor.MiddleCenter;
            caption.text = text;
            caption.raycastTarget = false;
            return rect.gameObject.AddComponent<PolyusControl>();
        }

        private void UpdateSafeArea()
        {
            var area = Screen.safeArea;
            safeArea.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
            safeArea.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
            safeArea.offsetMin = safeArea.offsetMax = Vector2.zero;
        }

        private void Update()
        {
            if (body == null) return;
            UpdateSafeArea();
            status.text = $"HP {health}/{MaxHealth}     DRONES {defeated}/4     GRAVITY {(gravitySign == 1 ? "DOWN" : "UP")}";
            if (externalBlocked) return;
            var keys = Keyboard.current;
            if (keys != null && keys.rKey.wasPressedThisFrame) { Restart(); return; }
            if (keys != null && keys.escapeKey.wasPressedThisFrame) TogglePause();
            if (ended || paused) return;
            direction = (right.Held ? 1 : 0) - (left.Held ? 1 : 0);
            if (keys != null)
            {
                direction += (keys.dKey.isPressed || keys.rightArrowKey.isPressed ? 1 : 0)
                    - (keys.aKey.isPressed || keys.leftArrowKey.isPressed ? 1 : 0);
                jumpQueued |= keys.spaceKey.wasPressedThisFrame;
                flipQueued |= keys.gKey.wasPressedThisFrame;
            }
            direction = Mathf.Clamp(direction, -1, 1);
            jumpQueued |= jump.ConsumePress();
            flipQueued |= gravity.ConsumePress();
            hurtTimer -= Time.deltaTime;
            gravityTimer -= Time.deltaTime;
            hero.color = hurtTimer > 0 && Mathf.Sin(Time.time * 35) > 0 ? Color.white : cyan;
            fireTimer -= Time.deltaTime;
            Drone closest = null;
            float best = 12f;
            foreach (var drone in drones)
            {
                drone.Transform.position = drone.Origin + Vector2.up * (Mathf.Sin(Time.time * 1.8f + drone.Origin.x) * .25f);
                Vector2 delta = (Vector2)drone.Transform.position - body.position;
                float distance = delta.magnitude;
                bool visible = !Physics2D.Linecast(body.position, drone.Transform.position, 1 << WorldLayer);
                if (distance < best && visible) { closest = drone; best = distance; }
                drone.Timer -= Time.deltaTime;
                if (drone.Timer <= 0 && visible)
                {
                    Fire(drone.Transform.position, delta.normalized * -4.5f, true);
                    drone.Timer = 2.1f;
                }
                if (distance < .7f) Hurt();
            }
            if (closest != null && fireTimer <= 0)
            {
                Fire(body.position, ((Vector2)closest.Transform.position - body.position).normalized * 15, false);
                fireTimer = .3f;
            }
            AdvanceShots();
            if (body.position.y < -8 || body.position.y > 8) { health = 0; Finish(false); }
        }

        private void FixedUpdate()
        {
            if (body == null || ended || paused || externalBlocked) return;
            if (flipQueued && gravityTimer <= 0)
            {
                gravitySign *= -1;
                body.gravityScale = 3 * gravitySign;
                body.linearVelocity = new Vector2(body.linearVelocity.x, 0);
                hero.flipY = gravitySign < 0;
                gravityTimer = .35f;
            }
            flipQueued = false;
            bool grounded = Physics2D.BoxCast(body.position, new Vector2(.5f, .08f), 0,
                Vector2.down * gravitySign, .49f, 1 << WorldLayer);
            body.linearVelocity = new Vector2(direction * Speed,
                jumpQueued && grounded ? 10 * gravitySign : body.linearVelocity.y);
            jumpQueued = false;
        }

        private void Fire(Vector2 origin, Vector2 velocity, bool hostile)
        {
            var item = Block(hostile ? "Enemy shot" : "Hero shot", origin, new Vector2(.18f, .18f), hostile ? orange : cyan);
            item.GetComponent<SpriteRenderer>().sortingOrder = 6;
            shots.Add(new Shot { Transform = item.transform, Velocity = velocity, Hostile = hostile });
        }

        private void AdvanceShots()
        {
            for (int i = shots.Count - 1; i >= 0; i--)
            {
                Shot shot = shots[i];
                Vector2 from = shot.Transform.position;
                Vector2 to = from + shot.Velocity * Time.deltaTime;
                shot.Lifetime -= Time.deltaTime;
                RaycastHit2D wall = Physics2D.Linecast(from, to, 1 << WorldLayer);
                if (wall) to = wall.point;
                bool consumed = false;
                if (shot.Hostile)
                {
                    if (SegmentDistance(from, to, body.position) < .4f) { Hurt(); consumed = true; }
                }
                else
                {
                    Drone hit = null;
                    float nearest = float.PositiveInfinity;
                    foreach (Drone drone in drones)
                    {
                        if (SegmentDistance(from, to, drone.Transform.position) >= .48f) continue;
                        float distance = Vector2.Distance(from, drone.Transform.position);
                        if (distance < nearest) { nearest = distance; hit = drone; }
                    }
                    if (hit != null)
                    {
                        consumed = true;
                        if (--hit.Health <= 0)
                        {
                            Destroy(hit.Transform.gameObject);
                            drones.Remove(hit);
                            monetization.AwardDrone(runId, defeated + 1);
                            if (++defeated == 4) Finish(true);
                        }
                    }
                }
                shot.Transform.position = to;
                if (consumed || wall || shot.Lifetime <= 0)
                {
                    Destroy(shot.Transform.gameObject);
                    shots.RemoveAt(i);
                }
                if (ended) break;
            }
        }

        private static float SegmentDistance(Vector2 from, Vector2 to, Vector2 point)
        {
            Vector2 segment = to - from;
            float t = segment.sqrMagnitude > 0 ? Mathf.Clamp01(Vector2.Dot(point - from, segment) / segment.sqrMagnitude) : 0;
            return Vector2.Distance(from + segment * t, point);
        }

        private void Hurt()
        {
            if (hurtTimer > 0 || ended) return;
            health -= 20;
            hurtTimer = 1;
            if (health <= 0) Finish(false);
        }

        private void Finish(bool won)
        {
            ended = true;
            body.simulated = false;
            result.text = won ? "ROOM CLEARED!\nPress R or RETRY" : "TRY AGAIN\nPress R or RETRY";
        }

        private void TogglePause()
        {
            if (ended || externalBlocked) return;
            paused = !paused;
            body.simulated = !paused && !externalBlocked;
            direction = 0;
            jumpQueued = flipQueued = false;
            left.Clear(); right.Clear(); jump.Clear(); gravity.Clear();
            result.text = paused ? "PAUSED\nPress II or Escape to resume" : "";
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused && body != null && !ended && !paused) TogglePause();
        }

        private void Restart()
        {
            // Rebuild only our runtime root; no dependency on the user's Build Profiles.
            enabled = false;
            Destroy(gameObject);
            new GameObject("Polyus Prototype").AddComponent<PolyusPrototype>();
        }

        private void OnDestroy()
        {
            if (square != null) Destroy(square);
            if (texture != null) Destroy(texture);
            if (playerMaterial != null) Destroy(playerMaterial);
        }
    }
}
