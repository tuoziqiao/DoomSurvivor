using UnityEngine;

namespace DoomSurvivor.Gameplay
{
    internal static class RuntimeSpriteFactory
    {
        private static Sprite whiteSprite;
        private static Sprite circleSprite;

        public static Sprite White => whiteSprite != null ? whiteSprite : whiteSprite = CreateWhite();
        public static Sprite Circle => circleSprite != null ? circleSprite : circleSprite = CreateCircle();

        public static GameObject CreateSpriteView(string name, Sprite sprite, Color color, int sortingOrder)
        {
            var view = new GameObject(name);
            var renderer = view.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite != null ? sprite : White;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            return view;
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
            if (view != null && view.TryGetComponent<SpriteRenderer>(out var renderer))
            {
                renderer.sortingOrder = 1000 - Mathf.RoundToInt(y * 100f) + offset;
            }
        }

        public static void AddShadow(GameObject parent, float width)
        {
            var shadow = CreateSpriteView("Shadow", Circle, new Color(0f, 0f, 0f, 0.28f), -1);
            shadow.transform.SetParent(parent.transform, false);
            shadow.transform.localPosition = new Vector3(0f, -0.18f, 0f);
            SetWorldSize(shadow, width, width * 0.28f);
        }

        private static Sprite CreateWhite()
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                name = "RuntimeWhite"
            };
            texture.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2f);
        }

        private static Sprite CreateCircle()
        {
            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "RuntimeCircle"
            };
            var pixels = new Color[size * size];
            var center = (size - 1) * 0.5f;
            var radius = center - 1f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    pixels[y * size + x] = distance <= radius ? Color.white : Color.clear;
                }
            }
            texture.SetPixels(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }
    }
}
