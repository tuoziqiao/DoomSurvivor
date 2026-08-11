using DoomSurvivor.Gameplay;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DoomSurvivor.Tests.EditMode
{
    public sealed class MapArtCatalogTests
    {
        [Test]
        public void ExperienceCapsule_IsLoadableAndImportedForRuntime()
        {
            Assert.That(MapArtCatalog.LoadPickup("experience_crystal"), Is.Not.Null);
            var importer = AssetImporter.GetAtPath("Assets/DoomSurvivor/Presentation/Resources/Art/Pickups/experience_crystal.png") as TextureImporter;
            Assert.That(importer, Is.Not.Null);
            Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite));
            Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Single));
            Assert.That(importer.spritePixelsPerUnit, Is.EqualTo(100f).Within(0.01f));
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.alphaIsTransparency, Is.True);
            Assert.That(importer.maxTextureSize, Is.EqualTo(1024));
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
            Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
            Assert.That(importer.spritePivot, Is.EqualTo(new Vector2(0.5f, 0.5f)));
        }

        [Test]
        public void HealingChicken_IsLoadableAndImportedForRuntime()
        {
            Assert.That(MapArtCatalog.LoadPickup("chicken_leg"), Is.Not.Null);
            const string path = "Assets/DoomSurvivor/Presentation/Resources/Art/Pickups/chicken_leg.png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Assert.That(importer, Is.Not.Null);
            Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite));
            Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Single));
            Assert.That(importer.spritePixelsPerUnit, Is.EqualTo(100f).Within(0.01f));
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.alphaIsTransparency, Is.True);
            Assert.That(importer.maxTextureSize, Is.EqualTo(1024));
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
            Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
            Assert.That(importer.spritePivot, Is.EqualTo(new Vector2(0.5f, 0.5f)));
        }

        [TestCase("map_crate", true, 1024, 0.5f, 0f)]
        [TestCase("map_hidden_crate", true, 1024, 0.5f, 0f)]
        [TestCase("map_altar", true, 1024, 0.5f, 0f)]
        [TestCase("map_event_aura", false, 1024, 0.5f, 0.5f)]
        [TestCase("poison_fog", false, 1024, 0.5f, 0.5f)]
        [TestCase("poison_smoke_puff", false, 1024, 0.5f, 0.5f)]
        [TestCase("player_scooter", false, 1024, 0.5f, 0.55f)]
        [TestCase("player_sniper", false, 1024, 0.15f, 0.55f)]
        [TestCase("crate_guide", false, 512, 0.5f, 0.5f)]
        public void AuthoredMapSprites_AreLoadableAndImportedForRuntime(string key, bool prop, int maxSize, float pivotX, float pivotY)
        {
            var sprite = prop ? MapArtCatalog.LoadProp(key) : key.StartsWith("player_") || key == "crate_guide"
                ? MapArtCatalog.LoadItem(key) : MapArtCatalog.LoadEffect(key);
            Assert.That(sprite, Is.Not.Null, $"正式地图 Sprite 必须可从 Resources 加载: {key}");

            var folder = prop ? "Map/Props" : key.StartsWith("player_") || key == "crate_guide" ? "Items" : "Map/Effects";
            var path = $"Assets/DoomSurvivor/Presentation/Resources/Art/{folder}/{key}.png";
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

        [TestCase("grass_tile_01", true)]
        [TestCase("grass_tile_02", true)]
        [TestCase("grass_tile_03", true)]
        [TestCase("grass_tile_04", true)]
        [TestCase("dry_highland_coast", true)]
        [TestCase("dry_highland_steppe", true)]
        [TestCase("dry_highland_sandstone", true)]
        [TestCase("dry_highland_gravel", true)]
        [TestCase("dry_highland_path", true)]
        [TestCase("dry_highland_water", true)]
        [TestCase("dry_highland_shore", true)]
        [TestCase("forest_edge", false)]
        [TestCase("tree_cluster", false)]
        [TestCase("bush", false)]
        [TestCase("rock", false)]
        [TestCase("tree_stump", false)]
        public void TerrainSprites_AreLoadableAndImportedForRuntime(string key, bool tile)
        {
            var sprite = tile ? MapArtCatalog.LoadTile(key) : MapArtCatalog.LoadEnvironment(key);
            Assert.That(sprite, Is.Not.Null, $"Terrain Sprite must be loadable from Resources: {key}");

            var folder = tile ? "Map/Tiles" : "Map/Environment";
            var path = $"Assets/DoomSurvivor/Presentation/Resources/Art/{folder}/{key}.png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Assert.That(importer, Is.Not.Null, $"Texture importer was not found: {path}");
            Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite));
            Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Single));
            Assert.That(importer.spritePixelsPerUnit, Is.EqualTo(100f).Within(0.01f));
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.alphaIsTransparency, Is.True);
            Assert.That(importer.maxTextureSize, Is.EqualTo(1024));
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
            Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
            Assert.That(importer.spritePivot, Is.EqualTo(new Vector2(0.5f, 0.5f)));
            Assert.That(sprite.texture.width, Is.LessThanOrEqualTo(1024));
            Assert.That(sprite.texture.height, Is.LessThanOrEqualTo(1024));
        }
    }
}
