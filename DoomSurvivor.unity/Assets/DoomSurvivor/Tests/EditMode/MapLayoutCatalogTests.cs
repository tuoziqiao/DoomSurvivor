using DoomSurvivor.Gameplay;
using NUnit.Framework;

namespace DoomSurvivor.Tests.EditMode
{
    public sealed class MapLayoutCatalogTests
    {
        [Test]
        public void DryHighlandCoast_UsesEightBySixAuthoredLayout()
        {
            Assert.That(MapLayoutCatalog.UsesAuthoredLayout(MapLayoutCatalog.DryHighlandCoastId), Is.True);
            Assert.That(MapLayoutCatalog.DryHighlandCoastColumns, Is.EqualTo(8));
            Assert.That(MapLayoutCatalog.DryHighlandCoastRows, Is.EqualTo(6));
        }

        [Test]
        public void DryHighlandCoast_CenterIsWalkableAndWaterIsBlocked()
        {
            var center = MapLayoutCatalog.GetTerrain(MapLayoutCatalog.DryHighlandCoastId, 4, 2);
            var water = MapLayoutCatalog.GetTerrain(MapLayoutCatalog.DryHighlandCoastId, 7, 2);

            Assert.That(MapLayoutCatalog.IsWalkable(center), Is.True);
            Assert.That(water, Is.EqualTo(MapTerrainKind.Water));
            Assert.That(MapLayoutCatalog.IsWalkable(water), Is.False);
            Assert.That(MapLayoutCatalog.GetTileKey(water), Is.EqualTo("dry_highland_water"));
        }

        [Test]
        public void NonAuthoredSkin_FallsBackToWalkableSteppe()
        {
            Assert.That(MapLayoutCatalog.GetTerrain("grass_tile_01", 0, 0), Is.EqualTo(MapTerrainKind.Steppe));
            Assert.That(MapLayoutCatalog.IsWalkable(MapLayoutCatalog.GetTerrain("grass_tile_01", 0, 0)), Is.True);
        }
    }
}
