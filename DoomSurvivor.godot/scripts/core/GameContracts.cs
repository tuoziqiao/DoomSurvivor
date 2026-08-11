using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DoomSurvivor.Core;

public enum GameState { Boot, Loading, MainMenu, Playing, LevelUp, Paused, Victory, Defeat, Result }
public enum GameMode { SoloSurvivor, QuickTest }
public enum ParticleQuality { Low, Medium, High }
public enum ConfigLoadSource { Builtin, Cache, Remote }

[Serializable]
public sealed class GameSettings
{
    public static readonly string[] MapSkinOptions =
    {
        "grass_tile_01", "grass_tile_02", "grass_tile_03", "grass_tile_04", "dry_highland_coast"
    };

    public float MasterVolume = 0.8f;
    public float SfxVolume = 0.8f;
    public float MusicVolume = 0.6f;
    public bool Fullscreen = true;
    public bool ScreenShake = true;
    public bool DamageNumbers = true;
    public ParticleQuality ParticleQuality = ParticleQuality.Medium;
    public int MaxEnemyDisplay = 250;
    public bool ShowPerformanceMonitor;
    public string MapSkinId = "grass_tile_01";
    public int CrateCount = 6;
    public int CrateRefreshChance = 30;
    public int HiddenCrateCount = 3;
    public int HiddenCrateRefreshChance;
    public int AltarCount = 3;
    public int AltarRefreshChance = 30;
    public int PoisonFogCount = 4;
    public int HealingChickenCount = 4;
    public int BossCount = 1;
    public int WaveCount = 10;
    public int FirstWaveMobCount = 8;
    public float WaveMobCountMultiplier = 1f;
    public int EliteStartWave = 4;
    public int LeaderStartWave = 3;

    public GameSettings Clone()
    {
        return new GameSettings
        {
            MasterVolume = MasterVolume,
            SfxVolume = SfxVolume,
            MusicVolume = MusicVolume,
            Fullscreen = Fullscreen,
            ScreenShake = ScreenShake,
            DamageNumbers = DamageNumbers,
            ParticleQuality = ParticleQuality,
            MaxEnemyDisplay = MaxEnemyDisplay,
            ShowPerformanceMonitor = ShowPerformanceMonitor,
            MapSkinId = MapSkinId,
            CrateCount = CrateCount,
            CrateRefreshChance = CrateRefreshChance,
            HiddenCrateCount = HiddenCrateCount,
            HiddenCrateRefreshChance = HiddenCrateRefreshChance,
            AltarCount = AltarCount,
            AltarRefreshChance = AltarRefreshChance,
            PoisonFogCount = PoisonFogCount,
            HealingChickenCount = HealingChickenCount,
            BossCount = BossCount,
            WaveCount = WaveCount,
            FirstWaveMobCount = FirstWaveMobCount,
            WaveMobCountMultiplier = WaveMobCountMultiplier,
            EliteStartWave = EliteStartWave,
            LeaderStartWave = LeaderStartWave
        };
    }

    public void Clamp()
    {
        MasterVolume = Math.Clamp(MasterVolume, 0f, 1f);
        SfxVolume = Math.Clamp(SfxVolume, 0f, 1f);
        MusicVolume = Math.Clamp(MusicVolume, 0f, 1f);
        MaxEnemyDisplay = Math.Clamp(MaxEnemyDisplay, 50, 1000);
        MapSkinId = NormalizeMapSkinId(MapSkinId);
        CrateCount = Math.Clamp(CrateCount, 0, 30);
        CrateRefreshChance = Math.Clamp(CrateRefreshChance, 0, 100);
        HiddenCrateCount = Math.Clamp(HiddenCrateCount, 0, 20);
        HiddenCrateRefreshChance = Math.Clamp(HiddenCrateRefreshChance, 0, 100);
        AltarCount = Math.Clamp(AltarCount, 0, 20);
        AltarRefreshChance = Math.Clamp(AltarRefreshChance, 0, 100);
        PoisonFogCount = Math.Clamp(PoisonFogCount, 0, 20);
        HealingChickenCount = Math.Clamp(HealingChickenCount, 0, 20);
        BossCount = Math.Clamp(BossCount, 0, 10);
        WaveCount = Math.Clamp(WaveCount, 1, 30);
        FirstWaveMobCount = Math.Clamp(FirstWaveMobCount, 1, 100);
        WaveMobCountMultiplier = Math.Clamp(WaveMobCountMultiplier, 0.1f, 5f);
        EliteStartWave = Math.Clamp(EliteStartWave, 1, 30);
        LeaderStartWave = Math.Clamp(LeaderStartWave, 1, 30);
    }

    public static string NormalizeMapSkinId(string? skinId)
    {
        if (string.IsNullOrWhiteSpace(skinId)) return MapSkinOptions[0];
        foreach (var option in MapSkinOptions)
        {
            if (string.Equals(option, skinId, StringComparison.OrdinalIgnoreCase)) return option;
        }
        return MapSkinOptions[0];
    }
}

[Serializable]
public sealed class SaveData
{
    public int SaveVersion = 4;
    public int MaxKills;
    public float MaxSurvivalTime;
    public int MaxLevel;
    public List<string> UnlockedCharacters = new() { "lin_xian" };
    public List<string> UnlockedSkins = new() { "lin_xian_wasteland" };
    public string SelectedCharacterId = "lin_xian";
    public Dictionary<string, string> SelectedSkinByCharacter = new() { ["lin_xian"] = "lin_xian_wasteland" };
    public List<string> CharacterOrder = new() { "gu_chen", "ye_qing", "lin_xian", "su_lan", "han_duo", "mu_xue", "lu_chuan" };
}

[Serializable]
public sealed class GameLaunchOptions
{
    public GameMode Mode = GameMode.QuickTest;
    public string CharacterId = "lin_xian";
    public string SkinId = "lin_xian_wasteland";
    public string StageId = "abandoned_city";
}

[Serializable]
public sealed class GameResultStats
{
    public bool Victory;
    public string CharacterId = string.Empty;
    public string SkinId = string.Empty;
    public string StageId = string.Empty;
    public float SurvivalTime;
    public int KillCount;
    public int MaxLevel;
    public int TotalExperience;
}

public sealed class GameSession
{
    public GameConfigBundle Config { get; set; } = new();
    public GameSettings Settings { get; set; } = new();
    public SaveData Profile { get; set; } = new();
    public GameLaunchOptions Launch { get; set; } = new();
    public GameResultStats? LastResult { get; set; }
    public ConfigLoadSource ConfigSource { get; set; } = ConfigLoadSource.Builtin;
}

public sealed class RunRequest
{
    public GameMode Mode { get; init; } = GameMode.QuickTest;
    public string CharacterId { get; init; } = "lin_xian";
    public string SkinId { get; init; } = "lin_xian_wasteland";
    public string StageId { get; init; } = "abandoned_city";
    public string MapSkinId { get; init; } = "grass_tile_01";
}

public sealed class RunLoadout
{
    public RunLoadout(GameMode mode, string mapSkinId, CharacterConfig character, SkinConfig skin, StageConfig stage, BalanceConfig balance, IReadOnlyList<EnemyConfig> enemies)
        : this(mode, mapSkinId, character, skin, stage, balance, enemies, null, null, null)
    {
    }

    public RunLoadout(GameMode mode, string mapSkinId, CharacterConfig character, SkinConfig skin, StageConfig stage, BalanceConfig balance, IReadOnlyList<EnemyConfig> enemies, GameSettings? settings)
        : this(mode, mapSkinId, character, skin, stage, balance, enemies, null, null, settings)
    {
    }

    public RunLoadout(
        GameMode mode,
        string mapSkinId,
        CharacterConfig character,
        SkinConfig skin,
        StageConfig stage,
        BalanceConfig balance,
        IReadOnlyList<EnemyConfig> enemies,
        IReadOnlyList<WeaponConfig>? weapons,
        IReadOnlyList<SkillConfig>? skills,
        GameSettings? settings)
    {
        Mode = mode;
        MapSkinId = mapSkinId;
        Character = character;
        Skin = skin;
        Stage = stage;
        Balance = balance;
        Enemies = enemies;
        Weapons = weapons ?? Array.Empty<WeaponConfig>();
        Skills = skills ?? Array.Empty<SkillConfig>();
        Settings = (settings ?? new GameSettings()).Clone();
    }

    public GameMode Mode { get; }
    public string MapSkinId { get; }
    public CharacterConfig Character { get; }
    public SkinConfig Skin { get; }
    public StageConfig Stage { get; }
    public BalanceConfig Balance { get; }
    public IReadOnlyList<EnemyConfig> Enemies { get; }
    public IReadOnlyList<WeaponConfig> Weapons { get; }
    public IReadOnlyList<SkillConfig> Skills { get; }
    public GameSettings Settings { get; }
}

public interface IConfigService
{
    ConfigLoadSource Source { get; }
    Task<GameConfigBundle> LoadAsync();
}

public interface ISaveService
{
    Task<SaveData> LoadProfileAsync();
    Task<GameSettings> LoadSettingsAsync();
    Task SaveProfileAsync(SaveData data);
    Task SaveSettingsAsync(GameSettings settings);
    Task ClearAsync();
}

public interface IRandomSource
{
    float Value { get; }
    int Range(int minInclusive, int maxExclusive);
    float Range(float minInclusive, float maxInclusive);
}

public sealed class GameStateMachine
{
    public GameState Current { get; private set; } = GameState.Boot;
    public event Action<GameState>? Changed;

    public void Set(GameState next)
    {
        if (Current == next) return;
        Current = next;
        Changed?.Invoke(next);
    }
}
