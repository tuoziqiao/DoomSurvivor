using System;
using System.Collections.Generic;
using System.Linq;
using DoomSurvivor.Core;

namespace DoomSurvivor.Domains;

public interface ICharacterCatalog
{
    IReadOnlyList<CharacterConfig> All { get; }
    bool TryGet(string characterId, out CharacterConfig config);
}

public interface ISkinCatalog
{
    IReadOnlyList<SkinConfig> All { get; }
    IReadOnlyList<SkinConfig> ForCharacter(string characterId);
    bool TryGet(string skinId, out SkinConfig config);
}

public interface IStageCatalog
{
    IReadOnlyList<StageConfig> All { get; }
    bool TryGet(string stageId, out StageConfig config);
}

public interface IContentUnlockService
{
    bool IsCharacterUnlocked(string characterId);
    bool IsSkinUnlocked(string skinId);
}

public sealed class InMemoryCharacterCatalog : ICharacterCatalog
{
    private readonly IReadOnlyList<CharacterConfig> all;
    private readonly Dictionary<string, CharacterConfig> byId;

    public InMemoryCharacterCatalog(IEnumerable<CharacterConfig> values)
    {
        all = values.Where(value => value is not null).ToList();
        byId = all.GroupBy(value => value.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
    }

    public IReadOnlyList<CharacterConfig> All => all;

    public bool TryGet(string characterId, out CharacterConfig config)
    {
        if (!string.IsNullOrWhiteSpace(characterId) && byId.TryGetValue(characterId, out config!)) return true;
        config = new CharacterConfig();
        return false;
    }
}

public sealed class InMemorySkinCatalog : ISkinCatalog
{
    private readonly IReadOnlyList<SkinConfig> all;
    private readonly Dictionary<string, SkinConfig> byId;

    public InMemorySkinCatalog(IEnumerable<SkinConfig> values)
    {
        all = values.Where(value => value is not null).ToList();
        byId = all.GroupBy(value => value.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
    }

    public IReadOnlyList<SkinConfig> All => all;

    public IReadOnlyList<SkinConfig> ForCharacter(string characterId) =>
        all.Where(value => string.Equals(value.CharacterId, characterId, StringComparison.Ordinal)).ToList();

    public bool TryGet(string skinId, out SkinConfig config)
    {
        if (!string.IsNullOrWhiteSpace(skinId) && byId.TryGetValue(skinId, out config!)) return true;
        config = new SkinConfig();
        return false;
    }
}

public sealed class InMemoryStageCatalog : IStageCatalog
{
    private readonly IReadOnlyList<StageConfig> all;
    private readonly Dictionary<string, StageConfig> byId;

    public InMemoryStageCatalog(IEnumerable<StageConfig> values)
    {
        all = values.Where(value => value is not null).ToList();
        byId = all.GroupBy(value => value.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
    }

    public IReadOnlyList<StageConfig> All => all;

    public bool TryGet(string stageId, out StageConfig config)
    {
        if (!string.IsNullOrWhiteSpace(stageId) && byId.TryGetValue(stageId, out config!)) return true;
        config = new StageConfig();
        return false;
    }
}

public sealed class SaveUnlockService : IContentUnlockService
{
    private readonly ICharacterCatalog characters;
    private readonly ISkinCatalog skins;
    private readonly SaveData save;

    public SaveUnlockService(ICharacterCatalog characters, ISkinCatalog skins, SaveData save)
    {
        this.characters = characters;
        this.skins = skins;
        this.save = save;
    }

    public bool IsCharacterUnlocked(string characterId)
    {
        return characters.TryGet(characterId, out var config) &&
            (config.UnlockByDefault || save.UnlockedCharacters.Contains(characterId, StringComparer.Ordinal));
    }

    public bool IsSkinUnlocked(string skinId)
    {
        return skins.TryGet(skinId, out var config) &&
            (config.UnlockByDefault || save.UnlockedSkins.Contains(skinId, StringComparer.Ordinal));
    }
}

public sealed class RunLoadoutFactory
{
    private readonly ICharacterCatalog characters;
    private readonly ISkinCatalog skins;
    private readonly IStageCatalog stages;
    private readonly IContentUnlockService unlocks;
    private readonly BalanceConfig balance;
    private readonly IReadOnlyList<EnemyConfig> enemies;
    private readonly IReadOnlyList<WeaponConfig> weapons;
    private readonly IReadOnlyList<SkillConfig> skills;
    private readonly Func<GameSettings>? settingsProvider;

    public RunLoadoutFactory(
        ICharacterCatalog characters,
        ISkinCatalog skins,
        IStageCatalog stages,
        IContentUnlockService unlocks,
        BalanceConfig balance,
        IReadOnlyList<EnemyConfig> enemies,
        IReadOnlyList<WeaponConfig>? weapons = null,
        IReadOnlyList<SkillConfig>? skills = null,
        Func<GameSettings>? settingsProvider = null)
    {
        this.characters = characters;
        this.skins = skins;
        this.stages = stages;
        this.unlocks = unlocks;
        this.balance = balance;
        this.enemies = enemies;
        this.weapons = weapons ?? Array.Empty<WeaponConfig>();
        this.skills = skills ?? Array.Empty<SkillConfig>();
        this.settingsProvider = settingsProvider;
    }

    public RunLoadout Create(RunRequest request)
    {
        if (!characters.TryGet(request.CharacterId, out var character)) throw new InvalidOperationException($"Unknown character: {request.CharacterId}");
        if (!unlocks.IsCharacterUnlocked(character.Id)) throw new InvalidOperationException($"Character is locked: {character.Id}");
        if (!skins.TryGet(request.SkinId, out var skin) || !string.Equals(skin.CharacterId, character.Id, StringComparison.Ordinal))
            throw new InvalidOperationException($"Skin does not belong to character: {request.SkinId}");
        if (!unlocks.IsSkinUnlocked(skin.Id)) throw new InvalidOperationException($"Skin is locked: {skin.Id}");
        if (!stages.TryGet(request.StageId, out var stage)) throw new InvalidOperationException($"Unknown stage: {request.StageId}");
        return new RunLoadout(
            request.Mode,
            GameSettings.NormalizeMapSkinId(request.MapSkinId),
            character,
            skin,
            stage,
            balance,
            enemies,
            weapons,
            skills,
            settingsProvider?.Invoke());
    }
}
