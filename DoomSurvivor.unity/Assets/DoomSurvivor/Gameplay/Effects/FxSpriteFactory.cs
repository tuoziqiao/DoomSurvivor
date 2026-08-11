using System.Collections.Generic;
using DoomSurvivor.Core;
using UnityEngine;

namespace DoomSurvivor.Gameplay.Effects
{
    internal static class FxSpriteFactory
    {
        private static Sprite circleSprite;
        private static Sprite rectSprite;
        private static Sprite flameSprite;
        private static Sprite knifeSprite;
        private static Sprite droneSprite;
        private static Sprite rippleRingSprite;
        private static readonly Dictionary<bool, Sprite> fuboQinIconCache = new();
        private static Material lineMaterial;
        private static Material additiveSpriteMaterial;

        public static Sprite Circle => circleSprite != null ? circleSprite : circleSprite = CreateCircle(32);
        public static Sprite Rect => rectSprite != null ? rectSprite : rectSprite = CreateRect(8, 4);
        public static Sprite Flame => flameSprite != null ? flameSprite : flameSprite = CreateFlame(24, 36);
        public static Sprite Knife => knifeSprite != null ? knifeSprite : knifeSprite = CreateKnife(28, 12);
        public static Sprite Drone => droneSprite != null ? droneSprite : droneSprite = CreateDrone(28, 20);
        public static Sprite RippleRing => rippleRingSprite != null ? rippleRingSprite : rippleRingSprite = CreateRippleRing(64);

        public static Material LineMaterial
        {
            get
            {
                if (lineMaterial != null) return lineMaterial;
                var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
                lineMaterial = new Material(shader) { name = "FxLineMaterial" };
                return lineMaterial;
            }
        }

        public static Material AdditiveSpriteMaterial
        {
            get
            {
                if (additiveSpriteMaterial != null) return additiveSpriteMaterial;
                var shader = Shader.Find("Sprites/Default");
                additiveSpriteMaterial = new Material(shader != null ? shader : Shader.Find("Unlit/Transparent"))
                {
                    name = "FxAdditiveSprite"
                };
                if (additiveSpriteMaterial.HasProperty("_SrcBlend"))
                {
                    additiveSpriteMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    additiveSpriteMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
                    additiveSpriteMaterial.SetInt("_ZWrite", 0);
                    additiveSpriteMaterial.renderQueue = 3000;
                }
                return additiveSpriteMaterial;
            }
        }

        public static Color FromRgb(int rgb, float alpha = 1f)
        {
            return new Color(
                ((rgb >> 16) & 0xFF) / 255f,
                ((rgb >> 8) & 0xFF) / 255f,
                (rgb & 0xFF) / 255f,
                alpha);
        }

        public static float Px(float pixels) => WorldScale.ToUnits(pixels);

        public static GameObject CreateSpriteView(string name, Sprite sprite, Color color, int sortingOrder,
            bool additive = false)
        {
            var view = new GameObject(name);
            var renderer = view.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite != null ? sprite : Circle;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            if (additive) renderer.sharedMaterial = AdditiveSpriteMaterial;
            return view;
        }

        public static GameObject CreateBoltView(string name)
        {
            var root = new GameObject(name);
            CreateLineChild(root, "Glow", 0.11f);
            CreateLineChild(root, "Energy", 0.05f);
            CreateLineChild(root, "Core", 0.018f);
            return root;
        }

        public static GameObject CreateSlashView(string name)
        {
            var root = new GameObject(name);
            CreateLineChild(root, "Outer", 0.03f);
            CreateLineChild(root, "Inner", 0.015f);
            return root;
        }

        public static void SetWorldSize(GameObject view, float width, float height)
        {
            if (view == null || !view.TryGetComponent<SpriteRenderer>(out var renderer) || renderer.sprite == null)
            {
                return;
            }

            var size = renderer.sprite.bounds.size;
            view.transform.localScale = new Vector3(
                size.x > 0f ? width / size.x : 1f,
                size.y > 0f ? height / size.y : 1f,
                1f);
        }

        public static void UpdateDepth(GameObject view, float y, int offset = 0)
        {
            if (view == null) return;
            var order = 1000 - Mathf.RoundToInt(y * 100f) + offset;
            if (view.TryGetComponent<SpriteRenderer>(out var spriteRenderer))
            {
                spriteRenderer.sortingOrder = order;
            }

            foreach (var line in view.GetComponentsInChildren<LineRenderer>(true))
            {
                line.sortingOrder = order;
            }
        }

        private static void CreateLineChild(GameObject parent, string name, float width)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent.transform, false);
            var line = child.AddComponent<LineRenderer>();
            line.sharedMaterial = LineMaterial;
            line.widthMultiplier = width;
            line.positionCount = 0;
            line.useWorldSpace = true;
            line.numCapVertices = 2;
            line.numCornerVertices = 2;
            line.sortingOrder = 40;
        }

        private static Sprite CreateCircle(int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "FxCircle" };
            var pixels = new Color[size * size];
            var center = (size - 1) * 0.5f;
            var radius = center - 1f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    var alpha = Mathf.Clamp01(1f - (distance - radius + 1f));
                    pixels[y * size + x] = distance <= radius + 1f
                        ? new Color(1f, 1f, 1f, alpha)
                        : Color.clear;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        private static Sprite CreateRect(int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { name = "FxRect" };
            var pixels = new Color[width * height];
            for (var i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
            texture.SetPixels(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), height);
        }

        private static Sprite CreateFlame(int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { name = "FxFlame" };
            var pixels = new Color[width * height];
            var cx = (width - 1) * 0.5f;
            for (var y = 0; y < height; y++)
            {
                var t = y / (float)(height - 1);
                var half = Mathf.Lerp(width * 0.42f, width * 0.08f, t);
                for (var x = 0; x < width; x++)
                {
                    var dx = Mathf.Abs(x - cx);
                    var edge = half > 0.01f ? 1f - dx / half : 0f;
                    var alpha = Mathf.Clamp01(edge) * Mathf.Lerp(0.35f, 1f, t);
                    pixels[y * width + x] = alpha > 0.02f
                        ? new Color(1f, Mathf.Lerp(0.35f, 0.9f, t), 0.15f, alpha)
                        : Color.clear;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.82f), height);
        }

        private static Sprite CreateKnife(int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { name = "FxKnife" };
            var pixels = new Color[width * height];
            var cy = (height - 1) * 0.5f;
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var tip = x / (float)(width - 1);
                    var half = Mathf.Lerp(height * 0.12f, height * 0.45f, 1f - tip);
                    var dy = Mathf.Abs(y - cy);
                    Color color;
                    if (x < width * 0.22f && dy < height * 0.18f)
                    {
                        color = new Color(0.55f, 0.35f, 0.18f, 1f);
                    }
                    else if (dy <= half)
                    {
                        color = tip > 0.75f
                            ? new Color(0.95f, 0.95f, 1f, 1f)
                            : new Color(0.72f, 0.76f, 0.82f, 1f);
                    }
                    else
                    {
                        color = Color.clear;
                    }

                    pixels[y * width + x] = color;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.35f, 0.5f), height);
        }

        private static Sprite CreateDrone(int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { name = "FxDrone" };
            var pixels = new Color[width * height];
            var cx = (width - 1) * 0.5f;
            var cy = (height - 1) * 0.55f;
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var body = Mathf.Abs(x - cx) < width * 0.18f && Mathf.Abs(y - cy) < height * 0.22f;
                    var arm = (Mathf.Abs(y - cy) < height * 0.08f && Mathf.Abs(x - cx) < width * 0.42f) ||
                              (Mathf.Abs(x - cx) < width * 0.08f && Mathf.Abs(y - cy) < height * 0.35f);
                    var rotor = Vector2.Distance(new Vector2(x, y), new Vector2(4, 4)) < 3.2f ||
                                Vector2.Distance(new Vector2(x, y), new Vector2(width - 5, 4)) < 3.2f ||
                                Vector2.Distance(new Vector2(x, y), new Vector2(4, height - 5)) < 3.2f ||
                                Vector2.Distance(new Vector2(x, y), new Vector2(width - 5, height - 5)) < 3.2f;
                    if (body) pixels[y * width + x] = new Color(0.25f, 0.28f, 0.32f, 1f);
                    else if (arm) pixels[y * width + x] = new Color(0.45f, 0.48f, 0.52f, 1f);
                    else if (rotor) pixels[y * width + x] = new Color(0.2f, 0.2f, 0.22f, 0.85f);
                    else pixels[y * width + x] = Color.clear;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), height);
        }

        public static Sprite FuboQinIcon(bool gold)
        {
            if (fuboQinIconCache.TryGetValue(gold, out var cached) && cached != null) return cached;
            var width = 48;
            var height = 40;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
                { name = gold ? "FxFuboQinIconGold" : "FxFuboQinIcon" };
            var pixels = new Color[width * height];
            var body = gold ? new Color(0.92f, 0.82f, 0.38f, 1f) : new Color(0.42f, 0.88f, 0.55f, 1f);
            var accent = gold ? new Color(1f, 0.94f, 0.62f, 1f) : new Color(0.62f, 0.98f, 0.72f, 1f);
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var board = x > 4 && x < width - 5 && y > height * 0.42f && y < height - 4;
                    var neck = x > width * 0.38f && x < width * 0.62f && y > 2 && y < height * 0.5f;
                    var stringLine = Mathf.Abs(x - width * 0.5f) < 1.2f && y > 6 && y < height - 6;
                    if (board) pixels[y * width + x] = body;
                    else if (neck) pixels[y * width + x] = accent;
                    else if (stringLine) pixels[y * width + x] = new Color(1f, 1f, 1f, 0.85f);
                    else pixels[y * width + x] = Color.clear;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            cached = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.45f), height);
            fuboQinIconCache[gold] = cached;
            return cached;
        }

        private static Sprite CreateRippleRing(int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "FxRippleRing" };
            var pixels = new Color[size * size];
            var center = (size - 1) * 0.5f;
            var outer = size * 0.46f;
            var inner = size * 0.34f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    var ring = dist <= outer && dist >= inner;
                    var feather = ring
                        ? Mathf.Clamp01(Mathf.Min(outer - dist, dist - inner) * 4f)
                        : 0f;
                    pixels[y * size + x] = new Color(1f, 1f, 1f, feather);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }
    }
}
