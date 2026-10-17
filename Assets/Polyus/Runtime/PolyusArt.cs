using System.Collections.Generic;
using UnityEngine;

namespace Polyus
{
    // Owns temporary primitives; authored illustrations are loaded from Resources.
    public sealed class PolyusArt
    {
        public Sprite Hero { get; }
        public Sprite Drone { get; }
        public Sprite Boss { get; }
        public Sprite Background { get; }
        public Sprite White { get; }
        public Sprite Glow { get; }
        public Sprite Disc { get; }
        public Font Font { get; }
        private readonly List<Object> owned = new List<Object>();
        public static readonly Color Cyan = new Color(.19f, .91f, 1f);
        public static readonly Color Orange = new Color(1f, .55f, .2f);
        public static readonly Color Ink = new Color(.025f, .047f, .085f);

        public PolyusArt()
        {
            White = Make(4, false, false);
            Glow = Make(64, true, false);
            Disc = Make(64, false, true);
            Hero = Resources.Load<Sprite>("Polyus/Art/hero") ?? Disc;
            Drone = Resources.Load<Sprite>("Polyus/Art/drone") ?? Disc;
            Boss = Resources.Load<Sprite>("Polyus/Art/boss") ?? Drone;
            Background = Resources.Load<Sprite>("Polyus/Art/background");
            Font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private Sprite Make(int size, bool glow, bool disc)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float radius = Vector2.Distance(new Vector2(x + .5f, y + .5f), Vector2.one * (size / 2f)) / (size / 2f);
                    float alpha = glow ? Mathf.Pow(Mathf.Clamp01(1 - radius), 2) : disc ? Mathf.Clamp01((1 - radius) * size) : 1;
                    pixels[y * size + x] = new Color(1, 1, 1, alpha);
                }
            texture.SetPixels(pixels);
            texture.Apply();
            var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * .5f, size);
            owned.Add(sprite); owned.Add(texture);
            return sprite;
        }

        public SpriteRenderer Draw(Transform parent, string name, Sprite sprite, Vector2 position, Vector2 size, Color color, int order)
        {
            var item = new GameObject(name);
            item.transform.SetParent(parent, false);
            item.transform.localPosition = position;
            item.transform.localScale = new Vector3(size.x / sprite.bounds.size.x, size.y / sprite.bounds.size.y, 1);
            if (sprite == Hero || sprite == Drone || sprite == Boss)
            {
                float fit = Mathf.Min(size.x / sprite.bounds.size.x, size.y / sprite.bounds.size.y);
                item.transform.localScale = new Vector3(fit, fit, 1);
            }
            var renderer = item.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = order;
            return renderer;
        }

        public void Dispose() { foreach (var item in owned) if (item != null) Object.Destroy(item); }
    }
}
