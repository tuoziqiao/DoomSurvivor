using DoomSurvivor.Core;
using DoomSurvivor.Domains;

namespace DoomSurvivor.Tests.Domains;

[TestClass]
public sealed class CatalogTests
{
    [TestMethod]
    public void SkinCatalogFiltersByCharacter()
    {
        var catalog = new InMemorySkinCatalog(new[]
        {
            new SkinConfig { Id = "a", CharacterId = "hero" },
            new SkinConfig { Id = "b", CharacterId = "other" }
        });
        Assert.AreEqual(1, catalog.ForCharacter("hero").Count);
        Assert.AreEqual("a", catalog.ForCharacter("hero")[0].Id);
    }

    [TestMethod]
    public void LoadoutRejectsSkinFromAnotherCharacter()
    {
        var character = new CharacterConfig { Id = "hero", UnlockByDefault = true, DefaultSkinId = "hero_skin" };
        var skin = new SkinConfig { Id = "other_skin", CharacterId = "other", UnlockByDefault = true };
        var stage = new StageConfig { Id = "stage" };
        var characters = new InMemoryCharacterCatalog(new[] { character });
        var skins = new InMemorySkinCatalog(new[] { skin });
        var stages = new InMemoryStageCatalog(new[] { stage });
        var unlocks = new SaveUnlockService(characters, skins, new SaveData());
        var factory = new RunLoadoutFactory(characters, skins, stages, unlocks, new BalanceConfig(), Array.Empty<EnemyConfig>());
        Assert.ThrowsException<InvalidOperationException>(() => factory.Create(new RunRequest { CharacterId = "hero", SkinId = "other_skin", StageId = "stage" }));
    }
}
