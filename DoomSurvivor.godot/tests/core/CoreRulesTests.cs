using DoomSurvivor.Core;

namespace DoomSurvivor.Tests.Core;

[TestClass]
public sealed class CoreRulesTests
{
    [TestMethod]
    public void DamageFormulaNeverReturnsBelowOne()
    {
        var result = DamageFormula.Calculate(1f, 0.1f, 1f, 1f, 0.5f, 100f);
        Assert.AreEqual(1f, result);
    }

    [TestMethod]
    public void ExperienceCreatesPendingLevelUps()
    {
        var progress = new ExperienceProgress(new[] { 10, 20 });
        progress.Add(30);
        Assert.AreEqual(3, progress.Level);
        Assert.AreEqual(2, progress.PendingLevelUps);
        Assert.AreEqual(0, progress.Current);
    }

    [TestMethod]
    public void SpatialHashReturnsNearbyBuckets()
    {
        var grid = new SpatialHashGrid<string>(10f);
        var results = new List<string>();
        grid.Insert(5f, 5f, "near");
        grid.Insert(100f, 100f, "far");
        grid.Query(5f, 5f, 8f, results);
        CollectionAssert.Contains(results, "near");
        CollectionAssert.DoesNotContain(results, "far");
    }

    [TestMethod]
    public void SaveMigrationRestoresRequiredDefaults()
    {
        var migrated = SaveMigration.Migrate(new SaveData { SaveVersion = 1, UnlockedCharacters = null!, UnlockedSkins = null!, SelectedSkinByCharacter = null!, CharacterOrder = null! });
        Assert.AreEqual(SaveMigration.CurrentVersion, migrated.SaveVersion);
        CollectionAssert.Contains(migrated.UnlockedCharacters, "lin_xian");
        CollectionAssert.Contains(migrated.UnlockedSkins, "lin_xian_wasteland");
        Assert.AreEqual(7, migrated.CharacterOrder.Count);
    }

    [TestMethod]
    public void SettingsClampNormalizesMapSkin()
    {
        var settings = new GameSettings { MasterVolume = 3f, MaxEnemyDisplay = 1, MapSkinId = "missing" };
        settings.Clamp();
        Assert.AreEqual(1f, settings.MasterVolume);
        Assert.AreEqual(50, settings.MaxEnemyDisplay);
        Assert.AreEqual("grass_tile_01", settings.MapSkinId);
    }
}
