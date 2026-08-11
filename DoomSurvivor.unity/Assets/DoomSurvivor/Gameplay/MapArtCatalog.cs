using System;
using System.Collections.Generic;
using UnityEngine;

namespace DoomSurvivor.Gameplay
{
    /// <summary>Centralized, cached access to authored map and temporary-item sprites.</summary>
    public static class MapArtCatalog
    {
        private const string PropsRoot = "Art/Map/Props/";
        private const string EffectsRoot = "Art/Map/Effects/";
        private const string TilesRoot = "Art/Map/Tiles/";
        private const string EnvironmentRoot = "Art/Map/Environment/";
        private const string ItemsRoot = "Art/Items/";
        private const string PickupsRoot = "Art/Pickups/";
        private static readonly Dictionary<string, Sprite> Cache = new(StringComparer.Ordinal);
        private static readonly HashSet<string> MissingPaths = new(StringComparer.Ordinal);

        public static Sprite LoadProp(string key) => Load(PropsRoot, key);
        public static Sprite LoadEffect(string key) => Load(EffectsRoot, key);
        public static Sprite LoadTile(string key) => Load(TilesRoot, key);
        public static Sprite LoadEnvironment(string key) => Load(EnvironmentRoot, key);
        public static Sprite LoadItem(string key) => Load(ItemsRoot, key);
        public static Sprite LoadPickup(string key) => Load(PickupsRoot, key);

        private static Sprite Load(string root, string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return null;
            var path = root + key;
            if (Cache.TryGetValue(path, out var cached)) return cached;

            var sprite = Resources.Load<Sprite>(path);
            Cache[path] = sprite;
            if (sprite == null && MissingPaths.Add(path))
                Debug.LogWarning($"[MapArtCatalog] 缺少地图 Sprite，将使用程序占位图: Resources/{path}");
            return sprite;
        }
    }
}
