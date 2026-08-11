using DoomSurvivor.Gameplay;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DoomSurvivor.Tests.EditMode
{
    public sealed class PassiveArtCatalogTests
    {
        private static readonly string[] IconKeys =
        {
            "passive_toughness", "passive_swift", "passive_strength", "passive_haste", "passive_magnet"
        };

        [Test]
        public void PassiveArtCatalog_LoadsAllAuthoredSprites()
        {
            foreach (var key in IconKeys)
                Assert.That(PassiveArtCatalog.LoadIcon(key), Is.Not.Null, $"Missing passive icon: {key}");
        }

        [Test]
        public void PassiveTextureImport_IsConfigured()
        {
            foreach (var key in IconKeys)
            {
                var path = $"Assets/DoomSurvivor/Presentation/Resources/Art/Skills/Icons/{key}.png";
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                Assert.That(importer, Is.Not.Null, $"Missing texture importer: {path}");
                Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite));
                Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Single));
                Assert.That(importer.spritePixelsPerUnit, Is.EqualTo(100f).Within(0.01f));
                Assert.That(importer.mipmapEnabled, Is.False);
                Assert.That(importer.alphaIsTransparency, Is.True);
                Assert.That(importer.maxTextureSize, Is.EqualTo(256));
                Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
                Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
            }
        }
    }
}
