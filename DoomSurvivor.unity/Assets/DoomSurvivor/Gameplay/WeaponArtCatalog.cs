using System;
using System.Collections.Generic;
using DoomSurvivor.Core;
using DoomSurvivor.Gameplay.Effects;
using UnityEngine;

namespace DoomSurvivor.Gameplay
{
    public static class WeaponArtCatalog
    {
        private const string IconRoot = "Art/Weapons/Icons/";
        private const string BattleRoot = "Art/Weapons/Battle/";
        private static readonly Dictionary<string, Sprite> Icons = new(StringComparer.Ordinal);
        private static readonly Dictionary<string, Sprite> BattleSprites = new(StringComparer.Ordinal);
        private static readonly HashSet<string> MissingPaths = new(StringComparer.Ordinal);

        public static bool IsPromoted(WeaponPromotionConfig promotion, int level, int maxLevel) =>
            promotion != null && promotion.IsConfigured && maxLevel > 0 && level >= maxLevel;

        public static Sprite LoadIcon(string iconKey) => Load(Icons, IconRoot, iconKey, true);

        public static string ResolveIconKey(string defaultIcon, WeaponPromotionConfig promotion, int level, int maxLevel)
        {
            if (IsPromoted(promotion, level, maxLevel) && !string.IsNullOrWhiteSpace(promotion.Icon))
                return promotion.Icon;
            return defaultIcon;
        }

        public static string ResolveDisplayName(string defaultName, WeaponPromotionConfig promotion, int level, int maxLevel)
        {
            if (IsPromoted(promotion, level, maxLevel))
                return promotion.Name;
            return defaultName;
        }

        public static string ResolveBattleKey(string defaultKey, WeaponPromotionConfig promotion, int level, int maxLevel)
        {
            if (IsPromoted(promotion, level, maxLevel) && !string.IsNullOrWhiteSpace(promotion.BattleSprite))
                return promotion.BattleSprite;
            return defaultKey;
        }

        public static Sprite LoadBattle(string assetKey) => Load(BattleSprites, BattleRoot, assetKey, false);

        public static Sprite LoadFuboQinAura(bool gold) =>
            LoadBattle(gold ? "fubo_qin_aura_gold" : "fubo_qin_aura");

        public static Sprite LoadRotatingKnifeGoldAura() =>
            LoadBattle("rotating_knife_gold_aura");

        private static Sprite Load(Dictionary<string, Sprite> cache, string root, string key, bool isIcon)
        {
            if (string.IsNullOrWhiteSpace(key)) return null;
            if (cache.TryGetValue(key, out var cached) && cached != null) return cached;

            var path = root + key;
            var sprite = Resources.Load<Sprite>(path);
            if (sprite == null)
            {
                sprite = CreatePlaceholder(key, isIcon);
                if (sprite == null && MissingPaths.Add(path))
                    Debug.LogWarning($"[WeaponArtCatalog] 缺少武器 Sprite，将使用程序占位图: Resources/{path}");
            }

            cache[key] = sprite;
            return sprite;
        }

        private static Sprite CreatePlaceholder(string key, bool isIcon)
        {
            if (key.StartsWith("fubo_qin_aura", StringComparison.Ordinal))
                return FxSpriteFactory.RippleRing;
            if (key.StartsWith("fubo_qin", StringComparison.Ordinal))
                return isIcon ? FxSpriteFactory.FuboQinIcon(key.EndsWith("_gold", StringComparison.Ordinal)) : FxSpriteFactory.RippleRing;
            return null;
        }
    }
}
