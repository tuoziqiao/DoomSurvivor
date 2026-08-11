using DoomSurvivor.Core;
using DoomSurvivor.Gameplay;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DoomSurvivor.Tests.EditMode
{
    public sealed class WeaponArtCatalogTests
    {
        private static readonly string[] IconKeys =
        {
            "wind_blade", "rotating_knife", "rotating_knife_gold", "fire_bottle", "lightning_chain", "drone", "capsule_football"
        };

        private static readonly string[] BattleKeys =
        {
            "wind_blade", "rotating_knife", "rotating_knife_gold", "rotating_knife_gold_aura", "fire_bottle", "fire_zone", "fire_flame", "drone", "drone_bolt",
            "capsule_football_belt", "capsule_football_ball", "capsule_football_impact",
            "fubo_qin_aura", "fubo_qin_aura_gold"
        };

        private static WeaponPromotionConfig WheelPromotion => new()
        {
            Name = "金轮术",
            Icon = "rotating_knife_gold",
            BattleSprite = "rotating_knife_gold"
        };

        private static WeaponPromotionConfig FuboPromotion => new()
        {
            Name = "长生伏波琴",
            Icon = "fubo_qin_gold",
            BattleSprite = "fubo_qin_gold"
        };

        [Test]
        public void WeaponArtCatalog_LoadsAllAuthoredSprites()
        {
            foreach (var key in IconKeys)
                Assert.That(WeaponArtCatalog.LoadIcon(key), Is.Not.Null, $"缺少武器图标: {key}");
            foreach (var key in BattleKeys)
                Assert.That(WeaponArtCatalog.LoadBattle(key), Is.Not.Null, $"缺少战斗 Sprite: {key}");
        }

        [TestCase("Icons/wind_blade.png", 256, 0.5f, 0.5f)]
        [TestCase("Icons/rotating_knife.png", 256, 0.5f, 0.5f)]
        [TestCase("Icons/rotating_knife_gold.png", 256, 0.5f, 0.5f)]
        [TestCase("Battle/wind_blade.png", 512, 0.5f, 0.5f)]
        [TestCase("Battle/rotating_knife.png", 512, 0.5f, 0.5f)]
        [TestCase("Battle/rotating_knife_gold.png", 512, 0.5f, 0.5f)]
        [TestCase("Battle/rotating_knife_gold_aura.png", 512, 0.5f, 0.5f)]
        [TestCase("Battle/drone.png", 512, 0.5f, 0.45f)]
        [TestCase("Battle/fire_flame.png", 512, 0.5f, 0.1f)]
        [TestCase("Icons/capsule_football.png", 256, 0.5f, 0.5f)]
        [TestCase("Battle/capsule_football_belt.png", 512, 0.5f, 0.5f)]
        [TestCase("Battle/capsule_football_ball.png", 512, 0.5f, 0.5f)]
        [TestCase("Battle/capsule_football_impact.png", 512, 0.5f, 0.5f)]
        [TestCase("Battle/fubo_qin_aura.png", 512, 0.5f, 0.5f)]
        [TestCase("Battle/fubo_qin_aura_gold.png", 512, 0.5f, 0.5f)]
        public void WeaponTextureImport_IsConfigured(string relativePath, int maxSize, float pivotX, float pivotY)
        {
            var path = $"Assets/DoomSurvivor/Presentation/Resources/Art/Weapons/{relativePath}";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Assert.That(importer, Is.Not.Null, $"找不到纹理导入器: {path}");
            Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite));
            Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Single));
            Assert.That(importer.spritePixelsPerUnit, Is.EqualTo(100f).Within(0.01f));
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.alphaIsTransparency, Is.True);
            Assert.That(importer.maxTextureSize, Is.EqualTo(maxSize));
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
            Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
            Assert.That(importer.spritePivot.x, Is.EqualTo(pivotX).Within(0.001f));
            Assert.That(importer.spritePivot.y, Is.EqualTo(pivotY).Within(0.001f));
        }

        [Test]
        public void Promotion_UsesGoldAssetsOnlyAtMaximumLevel()
        {
            Assert.That(WeaponArtCatalog.ResolveIconKey("rotating_knife", WheelPromotion, 7, 8),
                Is.EqualTo("rotating_knife"));
            Assert.That(WeaponArtCatalog.ResolveIconKey("rotating_knife", WheelPromotion, 8, 8),
                Is.EqualTo("rotating_knife_gold"));
            Assert.That(WeaponArtCatalog.ResolveDisplayName("飞轮术", WheelPromotion, 8, 8),
                Is.EqualTo("金轮术"));
            Assert.That(WeaponArtCatalog.ResolveBattleKey("rotating_knife", WheelPromotion, 8, 8),
                Is.EqualTo("rotating_knife_gold"));
            Assert.That(WeaponArtCatalog.ResolveDisplayName("伏波琴", FuboPromotion, 7, 8),
                Is.EqualTo("伏波琴"));
            Assert.That(WeaponArtCatalog.ResolveDisplayName("伏波琴", FuboPromotion, 8, 8),
                Is.EqualTo("长生伏波琴"));
            Assert.That(WeaponArtCatalog.ResolveIconKey("fubo_qin", FuboPromotion, 8, 8),
                Is.EqualTo("fubo_qin_gold"));
        }

        [Test]
        public void FuboQinPlaceholderSprites_AreAvailableWithoutAuthoredAssets()
        {
            Assert.That(WeaponArtCatalog.LoadIcon("fubo_qin"), Is.Not.Null);
            Assert.That(WeaponArtCatalog.LoadIcon("fubo_qin_gold"), Is.Not.Null);
            Assert.That(WeaponArtCatalog.LoadBattle("fubo_qin"), Is.Not.Null);
            Assert.That(WeaponArtCatalog.LoadBattle("fubo_qin_gold"), Is.Not.Null);
        }

        [Test]
        public void FuboQinAuraSprites_LoadViaCatalog()
        {
            Assert.That(WeaponArtCatalog.LoadFuboQinAura(false), Is.Not.Null);
            Assert.That(WeaponArtCatalog.LoadFuboQinAura(true), Is.Not.Null);
        }

        [Test]
        public void RotatingKnifeGoldAura_LoadViaCatalog()
        {
            Assert.That(WeaponArtCatalog.LoadRotatingKnifeGoldAura(), Is.Not.Null);
        }
    }
}
