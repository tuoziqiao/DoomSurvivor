using System;
using System.Collections.Generic;

namespace DoomSurvivor.Core
{
    [Serializable]
    public sealed class ApiResponse<T>
    {
        public bool Success;
        public T Data;
        public string Error;
        public string RequestId;
    }

    [Serializable]
    public sealed class ConfigVersionData
    {
        public string Version = string.Empty;
        public string LoadedAt = string.Empty;
    }

    [Serializable]
    public sealed class GameConfigBundle
    {
        public string Version = string.Empty;
        public CharactersConfig Characters = new();
        public SkinsConfig Skins = new();
        public EnemiesConfig Enemies = new();
        public WeaponsConfig Weapons = new();
        public SkillsConfig Skills = new();
        public StagesConfig Stages = new();
        public BalanceConfig Balance = new();
    }

    [Serializable] public sealed class CharactersConfig { public string Version = string.Empty; public List<CharacterConfig> Characters = new(); }
    [Serializable] public sealed class SkinsConfig { public string Version = string.Empty; public List<SkinConfig> Skins = new(); }
    [Serializable] public sealed class EnemiesConfig { public string Version = string.Empty; public List<EnemyConfig> Enemies = new(); }
    [Serializable] public sealed class WeaponsConfig { public string Version = string.Empty; public List<WeaponConfig> Weapons = new(); }
    [Serializable] public sealed class SkillsConfig { public string Version = string.Empty; public List<SkillConfig> Skills = new(); }
    [Serializable] public sealed class StagesConfig { public string Version = string.Empty; public List<StageConfig> Stages = new(); }

    [Serializable]
    public sealed class CharacterConfig
    {
        public string Id = string.Empty;
        public string Name = string.Empty;
        public string Description = string.Empty;
        public float MaxHp;
        public float MoveSpeed;
        public float PickupRadius;
        public float Armor;
        public float CritRate;
        public float CritDamage;
        public float DamageMultiplier;
        public float AttackSpeedMultiplier;
        public float ExperienceMultiplier;
        public string StartingWeaponId = string.Empty;
        public bool UnlockByDefault;
        public float Scale = 1f;
        public float CollisionRadius;
        public string DefaultSkinId = string.Empty;
        public string VisualArchetype = string.Empty;
        public string SkinTone = "#FFFFFF";
        public string HairColor = "#000000";
    }

    [Serializable]
    public sealed class SkinPalette
    {
        public string Primary = "#FFFFFF";
        public string Secondary = "#888888";
        public string Accent = "#FFCC33";
    }

    [Serializable]
    public sealed class SkinConfig
    {
        public string Id = string.Empty;
        public string CharacterId = string.Empty;
        public string Name = string.Empty;
        public string Description = string.Empty;
        public bool UnlockByDefault;
        public string ModelAsset = string.Empty;
        public SkinPalette Palette = new();
    }

    [Serializable]
    public sealed class EnemyConfig
    {
        public string Id = string.Empty;
        public string Name = string.Empty;
        public float MaxHp;
        public float MoveSpeed;
        public float ContactDamage;
        public float AttackCooldown;
        public int ExperienceReward;
        public float SpawnWeight;
        public float SpawnStartTime;
        public float SpawnEndTime;
        public float Scale = 1f;
        public float CollisionRadius;
        public string Type = "normal";
        public string Color = "#FFFFFF";
        public float DashCooldown;
        public float DashSpeed;
        public float DashDuration;
    }

    [Serializable]
    public sealed class WeaponPromotionConfig
    {
        public string Name = string.Empty;
        public string Icon = string.Empty;
        public string BattleSprite = string.Empty;
        public string Description = string.Empty;

        public bool IsConfigured => !string.IsNullOrWhiteSpace(Name);
    }

    [Serializable]
    public sealed class WeaponConfig
    {
        public string Id = string.Empty;
        public string Name = string.Empty;
        public string Description = string.Empty;
        public string Icon = string.Empty;
        public string Type = "weapon";
        public int MaxLevel;
        public string Rarity = "common";
        public float Weight;
        public List<string> Prerequisites = new();
        public WeaponPromotionConfig Promotion = new();
        public Dictionary<string, Dictionary<string, float>> LevelEffects = new();
    }

    [Serializable]
    public sealed class SkillConfig
    {
        public string Id = string.Empty;
        public string Name = string.Empty;
        public string Description = string.Empty;
        public string Icon = string.Empty;
        public string Type = "passive";
        public int MaxLevel;
        public string Rarity = "common";
        public float Weight;
        public List<string> Prerequisites = new();
        public Dictionary<string, Dictionary<string, float>> LevelEffects = new();
    }

    [Serializable]
    public sealed class StageConfig
    {
        public string Id = string.Empty;
        public string Name = string.Empty;
        public string Description = string.Empty;
        public float MapWidth;
        public float MapHeight;
        public float NormalModeDuration;
        public float QuickTestDuration;
        public int MaxEnemies;
        public List<SpawnTimelineEntry> SpawnTimeline = new();
        public MapEventsConfig MapEvents = new();
    }

    [Serializable]
    public sealed class SpawnTimelineEntry
    {
        public float Time;
        public string EnemyId = string.Empty;
        public float SpawnRate;
        public int MaxConcurrent;
        public float WeightMultiplier = 1f;
        public bool IsElite;
        public bool IsBoss;
    }

    [Serializable]
    public sealed class MapEventsConfig
    {
        public int CrateCount;
        public int HiddenCrateCount;
        public List<CrateEffectConfig> HiddenCrateEffects = new();
        public int AltarCount;
        public int PoisonFogCount;
        public int HealingChickenCount = 4;
        public float CrateInteractRadius;
        public float AltarInteractRadius;
        public float HealingChickenInteractRadius = 36f;
        public int CrateXpMin;
        public int CrateXpMax;
        public float PoisonFogRadiusMin;
        public float PoisonFogRadiusMax;
        public float PoisonFogDps;
        public float PoisonFogTickInterval;
        public List<AltarEffectConfig> AltarEffects = new();
        public List<CrateEffectConfig> CrateEffects = new();
    }

    [Serializable]
    public sealed class CrateEffectConfig
    {
        public string Id = string.Empty;
        public float Weight;
        public int FogCount;
        public int LevelUps;
        public float MaxHpBonus;
        public float MoveSpeedBonus;
        public float Duration;
        public float PickupRadiusMul;
    }

    [Serializable]
    public sealed class AltarEffectConfig
    {
        public string Id = string.Empty;
        public float Weight;
        public float HpCost;
        public float DamageBonus;
        public float Duration;
        public bool KeepBoss;
        public float PickupRadiusMul;
    }

    [Serializable]
    public sealed class BalanceConfig
    {
        public string Version = string.Empty;
        public ExperienceBalance Experience = new();
        public CombatBalance Combat = new();
        public PlayerBalance Player = new();
        public SpawnBalance Spawn = new();
        public PerformanceBalance Performance = new();
    }

    [Serializable] public sealed class ExperienceBalance { public List<int> LevelThresholds = new(); public CrystalValues CrystalValues = new(); public int CrystalMergeThreshold; public float CrystalMergeDistance; }
    [Serializable] public sealed class CrystalValues { public int Small; public int Medium; public int Large; }
    [Serializable] public sealed class CombatBalance { public float DamageVarianceMin = 0.95f; public float DamageVarianceMax = 1.05f; public float InvincibilityDuration; public float KnockbackForce; public int MaxDamageNumbersPerSecond; }
    [Serializable] public sealed class PlayerBalance
    {
        public float Acceleration;
        public float Deceleration;
        public int MaxWeapons = 5;
        public int MaxPassiveSkills = 5;
        public float LevelMaxHpGrowthPercent = 0.05f;
    }
    [Serializable] public sealed class SpawnBalance { public float MinSpawnDistanceFromPlayer; public float SpawnPadding; }
    [Serializable] public sealed class PerformanceBalance { public int DesktopMaxEnemies = 500; public int MobileMaxEnemies = 200; public float SpatialHashCellSize = 96; public int OffscreenUpdateInterval = 3; }
}
