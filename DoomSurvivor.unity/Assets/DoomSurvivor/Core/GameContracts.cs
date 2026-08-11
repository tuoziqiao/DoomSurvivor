using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DoomSurvivor.Core
{
    public enum GameState
    {
        Boot,
        Loading,
        MainMenu,
        Playing,
        LevelUp,
        Paused,
        BossIntro,
        Victory,
        Defeat,
        Result
    }

    public enum GameMode
    {
        Normal,
        QuickTest
    }

    public enum ParticleQuality
    {
        Low,
        Medium,
        High
    }

    public enum ConfigLoadSource
    {
        Builtin,
        Cache,
        Remote
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
    public sealed class SkillResult
    {
        public string Id = string.Empty;
        public string Name = string.Empty;
        public int Level;
    }

    [Serializable]
    public sealed class GameResultStats
    {
        public bool Victory;
        public string CharacterId = string.Empty;
        public string CharacterName = string.Empty;
        public string SkinId = string.Empty;
        public string SkinName = string.Empty;
        public float SurvivalTime;
        public int KillCount;
        public int MaxLevel;
        public float TotalDamage;
        public float MaxSingleDamage;
        public bool BossKilled;
        public int TotalExperience;
        public List<SkillResult> Skills = new();
    }

    public sealed class GameSession
    {
        public GameConfigBundle Config { get; set; }
        public GameSettings Settings { get; set; } = new();
        public SaveData Profile { get; set; } = new();
        public GameLaunchOptions Launch { get; set; } = new();
        public GameResultStats LastResult { get; set; }
        public ConfigLoadSource ConfigSource { get; set; } = ConfigLoadSource.Builtin;
    }

    [Serializable]
    public sealed class GameSettings
    {
        public static readonly string[] MapSkinOptions =
        {
            "grass_tile_01",
            "grass_tile_02",
            "grass_tile_03",
            "grass_tile_04",
            "dry_highland_coast"
        };

        private static readonly IReadOnlyDictionary<string, float> DefaultCrateEffectWeights =
            new Dictionary<string, float>
            {
                ["xp_burst"] = 2f,
                ["spawn_boss"] = 1f,
                ["spawn_poison_fog"] = 1f,
                ["double_level"] = 1f,
                ["max_hp_bonus"] = 1f,
                ["move_speed_bonus"] = 1f,
                ["magnet_burst"] = 1f,
                ["anesthetic_capsule"] = 1f
            };

        private static readonly IReadOnlyDictionary<string, float> DefaultAltarEffectWeights =
            new Dictionary<string, float>
            {
                ["blood_pact"] = 1f,
                ["magnet_burst"] = 1f,
                ["random_teleport"] = 1f,
                ["stun_watch"] = 1f
            };

        private static readonly IReadOnlyDictionary<string, float> DefaultHiddenCrateEffectWeights =
            new Dictionary<string, float>
            {
                ["scooter_boost"] = 1f,
                ["sniper_rifle"] = 1f,
                ["crate_guide"] = 1f,
                ["capsule_football"] = 1f,
                ["purge"] = 1f
            };

        public float MasterVolume = 0.8f;
        public float SfxVolume = 0.8f;
        public float MusicVolume = 0.6f;
        public bool Fullscreen = true;
        public bool ScreenShake = true;
        public bool DamageNumbers = true;
        public ParticleQuality ParticleQuality = ParticleQuality.Medium;
        public int MaxEnemyDisplay = 500;
        public bool ShowPerformanceMonitor;
        public string MapSkinId = "grass_tile_01";
        public int CrateCount = 6;
        public int CrateRefreshChance = 30;
        public int HiddenCrateCount = 3;
        public int HiddenCrateRefreshChance;
        public float ScooterBoostDuration = 30f;
        public float SniperRifleDuration = 60f;
        public float CrateGuideDuration = 120f;
        public float CapsuleFootballDuration = 30f;
        public float AnestheticCapsuleDuration = 20f;
        public float AnestheticCapsuleSlowPercent = 20f;
        public Dictionary<string, float> CrateEffectWeights = CreateDefaultCrateEffectWeights();
        public Dictionary<string, float> HiddenCrateEffectWeights = CreateDefaultHiddenCrateEffectWeights();
        public int AltarCount = 3;
        public int AltarRefreshChance = 30;
        public float AltarBloodPactHpCost = 5f;
        public float AltarMagnetBurstHpCost = 5f;
        public float AltarTeleportHpCost = 5f;
        public float AltarStunWatchHpCost = 5f;
        public float AltarBloodPactDamageBonus = 0.3f;
        public float AltarBloodPactDuration = 25f;
        public float AltarMagnetPickupRadiusMul = 5f;
        public float AltarMagnetDuration = 4f;
        public float AltarStunWatchDuration = 10f;
        public Dictionary<string, float> AltarEffectWeights = CreateDefaultAltarEffectWeights();
        public int PoisonFogCount = 4;
        public float PoisonFogRadiusMin = 120f;
        public float PoisonFogRadiusMax = 220f;
        public float PoisonFogDps = 10f;
        /** 每局地图默认生成的烤鸡腿数量 */
        public int HealingChickenCount = 4;
        /** 烤鸡腿补刷概率（0–100），每 40 秒判定一次 */
        public int HealingChickenRefreshChance;
        public int BossCount = 1;
        public int WaveCount = 10;
        /** 首波普通小怪刷新个数（后续波次按倍率与增长系数递推） */
        public int FirstWaveMobCount = 8;
        public float WaveMobCountMultiplier = 1f;
        /** 旋转飞刀旋转速度倍率（1 = 默认） */
        public float RotatingKnifeRotationSpeedMul = 1f;
        /** 飞轮术 L1 轨道半径（像素） */
        public float RotatingKnifeBaseOrbitRadius = 86f;
        /** 飞轮术满级轨道半径（像素） */
        public float RotatingKnifeMaxOrbitRadius = 137f;
        /** 伏波琴 L1 光环半径（像素） */
        public float FuboQinBaseAuraRadius = 90f;
        /** 伏波琴满级光环半径（像素） */
        public float FuboQinMaxAuraRadius = 180f;
        public int EliteStartWave = 4;
        public float EliteChanceBase = 25f;
        public float EliteChanceGrowthPerWave = 8f;
        public float EliteChanceMax = 70f;
        public int EliteMaxCountPerWave = 4;
        public int LeaderStartWave = 3;
        public float LeaderChanceBase = 30f;
        public float LeaderChanceGrowthPerWave = 10f;
        public float LeaderChanceMax = 85f;
        public int LeaderMaxCountPerWave = 6;

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
            ScooterBoostDuration = Math.Clamp(ScooterBoostDuration, 5f, 600f);
            SniperRifleDuration = Math.Clamp(SniperRifleDuration, 5f, 600f);
            CrateGuideDuration = Math.Clamp(CrateGuideDuration, 5f, 600f);
            CapsuleFootballDuration = Math.Clamp(CapsuleFootballDuration, 5f, 600f);
            AnestheticCapsuleDuration = Math.Clamp(AnestheticCapsuleDuration, 5f, 600f);
            AnestheticCapsuleSlowPercent = Math.Clamp(AnestheticCapsuleSlowPercent, 5f, 80f);
            AnestheticCapsuleSlowPercent = MathF.Round(AnestheticCapsuleSlowPercent);
            CrateEffectWeights ??= CreateDefaultCrateEffectWeights();
            foreach (var pair in DefaultCrateEffectWeights)
            {
                if (!CrateEffectWeights.TryGetValue(pair.Key, out var weight))
                    CrateEffectWeights[pair.Key] = pair.Value;
                else
                    CrateEffectWeights[pair.Key] = Math.Clamp(weight, 0f, 20f);
            }
            HiddenCrateEffectWeights ??= CreateDefaultHiddenCrateEffectWeights();
            foreach (var pair in DefaultHiddenCrateEffectWeights)
            {
                if (!HiddenCrateEffectWeights.TryGetValue(pair.Key, out var weight))
                    HiddenCrateEffectWeights[pair.Key] = pair.Value;
                else
                    HiddenCrateEffectWeights[pair.Key] = Math.Clamp(weight, 0f, 20f);
            }
            AltarCount = Math.Clamp(AltarCount, 0, 20);
            AltarRefreshChance = Math.Clamp(AltarRefreshChance, 0, 100);
            AltarBloodPactHpCost = Math.Clamp(AltarBloodPactHpCost, 1f, 90f);
            AltarMagnetBurstHpCost = Math.Clamp(AltarMagnetBurstHpCost, 1f, 90f);
            AltarTeleportHpCost = Math.Clamp(AltarTeleportHpCost, 1f, 90f);
            AltarStunWatchHpCost = Math.Clamp(AltarStunWatchHpCost, 1f, 90f);
            AltarBloodPactDamageBonus = Math.Clamp(AltarBloodPactDamageBonus, 0.05f, 2f);
            AltarBloodPactDamageBonus = MathF.Round(AltarBloodPactDamageBonus * 100f) / 100f;
            AltarBloodPactDuration = Math.Clamp(AltarBloodPactDuration, 1f, 120f);
            AltarMagnetPickupRadiusMul = Math.Clamp(AltarMagnetPickupRadiusMul, 1f, 20f);
            AltarMagnetPickupRadiusMul = MathF.Round(AltarMagnetPickupRadiusMul * 10f) / 10f;
            AltarMagnetDuration = Math.Clamp(AltarMagnetDuration, 1f, 60f);
            AltarStunWatchDuration = Math.Clamp(AltarStunWatchDuration, 1f, 60f);
            AltarEffectWeights ??= CreateDefaultAltarEffectWeights();
            foreach (var pair in DefaultAltarEffectWeights)
            {
                if (!AltarEffectWeights.TryGetValue(pair.Key, out var weight))
                    AltarEffectWeights[pair.Key] = pair.Value;
                else
                    AltarEffectWeights[pair.Key] = Math.Clamp(weight, 0f, 20f);
            }
            PoisonFogCount = Math.Clamp(PoisonFogCount, 0, 20);
            PoisonFogRadiusMin = Math.Clamp(PoisonFogRadiusMin, 50f, 400f);
            PoisonFogRadiusMax = Math.Clamp(PoisonFogRadiusMax, 50f, 400f);
            if (PoisonFogRadiusMin > PoisonFogRadiusMax)
            {
                (PoisonFogRadiusMin, PoisonFogRadiusMax) = (PoisonFogRadiusMax, PoisonFogRadiusMin);
            }
            PoisonFogDps = Math.Clamp(PoisonFogDps, 0f, 50f);
            HealingChickenCount = Math.Clamp(HealingChickenCount, 0, 20);
            HealingChickenRefreshChance = Math.Clamp(HealingChickenRefreshChance, 0, 100);
            BossCount = Math.Clamp(BossCount, 0, 10);
            WaveCount = Math.Clamp(WaveCount, 1, 30);
            FirstWaveMobCount = Math.Clamp(FirstWaveMobCount, 1, 500);
            WaveMobCountMultiplier = Math.Clamp(WaveMobCountMultiplier, 0.25f, 50f);
            WaveMobCountMultiplier = MathF.Round(WaveMobCountMultiplier * 20f) / 20f;
            RotatingKnifeRotationSpeedMul = Math.Clamp(RotatingKnifeRotationSpeedMul, 0.25f, 3f);
            RotatingKnifeRotationSpeedMul = MathF.Round(RotatingKnifeRotationSpeedMul * 20f) / 20f;
            RotatingKnifeBaseOrbitRadius = Math.Clamp(RotatingKnifeBaseOrbitRadius, 40f, 400f);
            RotatingKnifeMaxOrbitRadius = Math.Clamp(RotatingKnifeMaxOrbitRadius, 40f, 400f);
            if (RotatingKnifeMaxOrbitRadius < RotatingKnifeBaseOrbitRadius)
                RotatingKnifeMaxOrbitRadius = RotatingKnifeBaseOrbitRadius;
            FuboQinBaseAuraRadius = Math.Clamp(FuboQinBaseAuraRadius, 40f, 400f);
            FuboQinMaxAuraRadius = Math.Clamp(FuboQinMaxAuraRadius, 40f, 400f);
            if (FuboQinMaxAuraRadius < FuboQinBaseAuraRadius)
                FuboQinMaxAuraRadius = FuboQinBaseAuraRadius;
            EliteStartWave = Math.Clamp(EliteStartWave, 1, 30);
            EliteChanceBase = Math.Clamp(EliteChanceBase, 0f, 100f);
            EliteChanceGrowthPerWave = Math.Clamp(EliteChanceGrowthPerWave, 0f, 50f);
            EliteChanceMax = Math.Clamp(EliteChanceMax, 0f, 100f);
            EliteMaxCountPerWave = Math.Clamp(EliteMaxCountPerWave, 0, 20);
            LeaderStartWave = Math.Clamp(LeaderStartWave, 1, 30);
            LeaderChanceBase = Math.Clamp(LeaderChanceBase, 0f, 100f);
            LeaderChanceGrowthPerWave = Math.Clamp(LeaderChanceGrowthPerWave, 0f, 50f);
            LeaderChanceMax = Math.Clamp(LeaderChanceMax, 0f, 100f);
            LeaderMaxCountPerWave = Math.Clamp(LeaderMaxCountPerWave, 0, 20);
        }

        public static string NormalizeMapSkinId(string skinId)
        {
            if (string.IsNullOrWhiteSpace(skinId))
                return MapSkinOptions[0];
            foreach (var option in MapSkinOptions)
            {
                if (string.Equals(option, skinId, StringComparison.OrdinalIgnoreCase))
                    return option;
            }
            return MapSkinOptions[0];
        }

        private static Dictionary<string, float> CreateDefaultCrateEffectWeights() =>
            new(DefaultCrateEffectWeights, StringComparer.Ordinal);

        private static Dictionary<string, float> CreateDefaultHiddenCrateEffectWeights() =>
            new(DefaultHiddenCrateEffectWeights, StringComparer.Ordinal);

        private static Dictionary<string, float> CreateDefaultAltarEffectWeights() =>
            new(DefaultAltarEffectWeights, StringComparer.Ordinal);
    }

    [Serializable]
    public sealed class SaveData
    {
        public int SaveVersion = 4;
        public int MaxKills;
        public float MaxSurvivalTime;
        public int MaxLevel;
        public GameResultStats LastResult;
        public List<string> UnlockedCharacters = new() { "lin_xian" };
        public List<string> UnlockedSkins = new() { "lin_xian_wasteland" };
        public string SelectedCharacterId = "lin_xian";
        public Dictionary<string, string> SelectedSkinByCharacter = new()
        {
            ["lin_xian"] = "lin_xian_wasteland"
        };
        public List<string> CharacterOrder = new()
        {
            "gu_chen", "ye_qing", "lin_xian", "su_lan", "han_duo", "mu_xue", "lu_chuan"
        };
    }

    public interface IGameSystem
    {
        void Initialize();
        void Tick(float deltaTime);
        void Shutdown();
    }

    public interface IPoolable
    {
        void OnAcquire();
        void OnRelease();
        void Reset();
    }

    public interface IRandomSource
    {
        float Value { get; }
        int Range(int minInclusive, int maxExclusive);
        float Range(float minInclusive, float maxInclusive);
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

    public sealed class GameStateMachine
    {
        public GameState Current { get; private set; } = GameState.Boot;
        public event Action<GameState> Changed;

        public void Set(GameState next)
        {
            if (Current == next)
            {
                return;
            }

            Current = next;
            Changed?.Invoke(next);
        }
    }
}
