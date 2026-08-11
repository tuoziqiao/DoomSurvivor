using DoomSurvivor.Core;
using DoomSurvivor.Infrastructure;

namespace DoomSurvivor.Tests.Infrastructure;

[TestClass]
public sealed class ConfigValidationTests
{
    [TestMethod]
    public void ValidationRejectsUnknownSpawnEnemy()
    {
        var bundle = new GameConfigBundle
        {
            Characters = new CharactersConfig { Characters = new() { new CharacterConfig { Id = "hero" } } },
            Skins = new SkinsConfig { Skins = new() { new SkinConfig { Id = "skin", CharacterId = "hero" } } },
            Enemies = new EnemiesConfig { Enemies = new() { new EnemyConfig { Id = "known" } } },
            Stages = new StagesConfig { Stages = new() { new StageConfig { Id = "stage", SpawnTimeline = new() { new SpawnTimelineEntry { EnemyId = "missing" } } } } }
        };
        Assert.ThrowsException<InvalidDataException>(() => ConfigService.Validate(bundle));
    }
}
