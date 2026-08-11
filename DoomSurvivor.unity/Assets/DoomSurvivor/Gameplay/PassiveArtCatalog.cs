using System;
using System.Collections.Generic;
using UnityEngine;

namespace DoomSurvivor.Gameplay
{
    public static class PassiveArtCatalog
    {
        private const string IconRoot = "Art/Skills/Icons/";
        private static readonly Dictionary<string, Sprite> Icons = new(StringComparer.Ordinal);
        private static readonly HashSet<string> MissingPaths = new(StringComparer.Ordinal);

        public static Sprite LoadIcon(string iconKey)
        {
            if (string.IsNullOrWhiteSpace(iconKey)) return null;
            if (Icons.TryGetValue(iconKey, out var cached)) return cached;

            var path = IconRoot + iconKey;
            var sprite = Resources.Load<Sprite>(path);
            Icons[iconKey] = sprite;
            if (sprite == null && MissingPaths.Add(path))
                Debug.LogWarning($"[PassiveArtCatalog] Missing passive Sprite. Falling back to text: Resources/{path}");
            return sprite;
        }
    }
}
