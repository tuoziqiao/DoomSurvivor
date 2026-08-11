using System;
using System.Collections.Generic;
using DoomSurvivor.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DoomSurvivor.Gameplay
{
    public interface IInputSource : IDisposable
    {
        Vector2 Move { get; }
        bool PausePressed { get; }
        bool InteractPressed { get; }
        void Enable();
        void Disable();
    }

    public sealed class UnityInputSource : IInputSource
    {
        private readonly InputActionAsset asset;
        private readonly InputActionMap gameplay;
        private readonly InputAction move;
        private readonly InputAction pause;
        private readonly InputAction interact;

        public Vector2 Move => move?.ReadValue<Vector2>() ?? Vector2.zero;
        public bool PausePressed => pause?.WasPressedThisFrame() ?? false;
        public bool InteractPressed => interact?.WasPressedThisFrame() ?? false;

        public UnityInputSource(InputActionAsset source)
        {
            asset = source != null ? UnityEngine.Object.Instantiate(source) : CreateFallback();
            gameplay = asset.FindActionMap("Gameplay", true);
            move = gameplay.FindAction("Move", true);
            pause = gameplay.FindAction("Pause", true);
            interact = gameplay.FindAction("Interact", true);
        }

        public void Enable() => gameplay.Enable();
        public void Disable() => gameplay.Disable();
        public void Dispose()
        {
            Disable();
            if (asset != null)
            {
                UnityEngine.Object.Destroy(asset);
            }
        }

        private static InputActionAsset CreateFallback()
        {
            var result = ScriptableObject.CreateInstance<InputActionAsset>();
            var map = result.AddActionMap("Gameplay");
            var moveAction = map.AddAction("Move", InputActionType.Value);
            moveAction.expectedControlType = "Vector2";
            moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/rightArrow");
            moveAction.AddBinding("<Gamepad>/leftStick");
            map.AddAction("Pause", InputActionType.Button, "<Keyboard>/escape");
            map.AddAction("Interact", InputActionType.Button, "<Keyboard>/e");
            return result;
        }
    }

    public sealed class UnityRandomSource : IRandomSource
    {
        private readonly System.Random random;
        public float Value => (float)random.NextDouble();

        public UnityRandomSource(int seed)
        {
            random = new System.Random(seed);
        }

        public int Range(int minInclusive, int maxExclusive) => random.Next(minInclusive, maxExclusive);
        public float Range(float minInclusive, float maxInclusive) =>
            minInclusive + (maxInclusive - minInclusive) * Value;
    }

    public enum UpgradeKind
    {
        Weapon,
        Passive
    }

    public sealed class UpgradeOffer
    {
        public string Id = string.Empty;
        public string Name = string.Empty;
        public string Description = string.Empty;
        public string Icon = string.Empty;
        public UpgradeKind Kind;
        public int NextLevel;
    }

    public readonly struct WaveSpawnPlanItem
    {
        public WaveSpawnPlanItem(string enemyId, float difficultyMultiplier, bool isElite = false)
        {
            EnemyId = enemyId;
            DifficultyMultiplier = difficultyMultiplier;
            IsElite = isElite;
        }

        public string EnemyId { get; }
        public float DifficultyMultiplier { get; }
        public bool IsElite { get; }
    }

    public readonly struct WaveSpecialSpawnRules
    {
        public readonly int EliteStartWave;
        public readonly float EliteChanceBase;
        public readonly float EliteChanceGrowthPerWave;
        public readonly float EliteChanceMax;
        public readonly int EliteMaxCountPerWave;
        public readonly int LeaderStartWave;
        public readonly float LeaderChanceBase;
        public readonly float LeaderChanceGrowthPerWave;
        public readonly float LeaderChanceMax;
        public readonly int LeaderMaxCountPerWave;

        public WaveSpecialSpawnRules(int eliteStartWave, float eliteChanceBase, float eliteChanceGrowthPerWave,
            float eliteChanceMax, int eliteMaxCountPerWave, int leaderStartWave, float leaderChanceBase,
            float leaderChanceGrowthPerWave, float leaderChanceMax, int leaderMaxCountPerWave)
        {
            EliteStartWave = eliteStartWave;
            EliteChanceBase = eliteChanceBase;
            EliteChanceGrowthPerWave = eliteChanceGrowthPerWave;
            EliteChanceMax = eliteChanceMax;
            EliteMaxCountPerWave = eliteMaxCountPerWave;
            LeaderStartWave = leaderStartWave;
            LeaderChanceBase = leaderChanceBase;
            LeaderChanceGrowthPerWave = leaderChanceGrowthPerWave;
            LeaderChanceMax = leaderChanceMax;
            LeaderMaxCountPerWave = leaderMaxCountPerWave;
        }

        public static WaveSpecialSpawnRules FromSettings(GameSettings settings)
        {
            settings ??= new GameSettings();
            return new WaveSpecialSpawnRules(
                settings.EliteStartWave, settings.EliteChanceBase, settings.EliteChanceGrowthPerWave,
                settings.EliteChanceMax, settings.EliteMaxCountPerWave,
                settings.LeaderStartWave, settings.LeaderChanceBase, settings.LeaderChanceGrowthPerWave,
                settings.LeaderChanceMax, settings.LeaderMaxCountPerWave);
        }

        public static WaveSpecialSpawnRules Default => FromSettings(new GameSettings());
    }

    public static class WaveRules
    {
        public const float Growth = 1.55f;
        public const float IntermissionSeconds = 2.2f;

        public static int ResolveWaveCount(GameMode mode, int configuredWaveCount)
        {
            var configured = Mathf.Clamp(configuredWaveCount, 1, 30);
            return mode == GameMode.QuickTest ? Mathf.Clamp(configured, 3, 5) : configured;
        }

        public static int BaseMobCount(GameMode mode) => mode == GameMode.QuickTest ? 6 : 8;

        public static int MobCountForWave(int wave, int baseCount, float growth, int cap)
        {
            var normalizedWave = Mathf.Max(1, wave);
            var count = Mathf.RoundToInt(Mathf.Max(1, baseCount) * Mathf.Pow(growth, normalizedWave - 1));
            return Mathf.Clamp(count, 1, Mathf.Max(1, cap));
        }

        public static float SpecialSpawnChance(int wave, int startWave, float baseChance, float growthPerWave,
            float maxChance)
        {
            if (wave < startWave || maxChance <= 0f) return 0f;
            return Mathf.Clamp(baseChance + (wave - startWave) * growthPerWave, 0f, maxChance);
        }

        public static int RollSpecialSpawnCount(float chancePercent, int maxCount, IRandomSource random)
        {
            if (maxCount <= 0 || chancePercent <= 0f || random == null) return 0;
            var count = 0;
            for (var i = 0; i < maxCount; i++)
            {
                if (random.Value * 100f < chancePercent) count++;
            }
            return count;
        }

        public static List<WaveSpawnPlanItem> PlanWave(int wave, int baseCount, float mobCountMultiplier,
            float growth, int cap, IRandomSource random, WaveSpecialSpawnRules? specialRules = null)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));
            var rules = specialRules ?? WaveSpecialSpawnRules.Default;

            var normalizedWave = Mathf.Max(1, wave);
            var scaledBaseCount = Mathf.Max(1, Mathf.RoundToInt(baseCount * Mathf.Max(0.25f, mobCountMultiplier)));
            var smallCount = MobCountForWave(normalizedWave, scaledBaseCount, growth, cap);
            var difficulty = 1f + (normalizedWave - 1) * 0.1f;
            var leaderChance = SpecialSpawnChance(normalizedWave, rules.LeaderStartWave, rules.LeaderChanceBase,
                rules.LeaderChanceGrowthPerWave, rules.LeaderChanceMax);
            var eliteChance = SpecialSpawnChance(normalizedWave, rules.EliteStartWave, rules.EliteChanceBase,
                rules.EliteChanceGrowthPerWave, rules.EliteChanceMax);
            var leaderCount = RollSpecialSpawnCount(leaderChance, rules.LeaderMaxCountPerWave, random);
            var eliteCount = RollSpecialSpawnCount(eliteChance, rules.EliteMaxCountPerWave, random);
            var plan = new List<WaveSpawnPlanItem>(smallCount + leaderCount + eliteCount);

            for (var i = 0; i < smallCount; i++)
            {
                var useFast = normalizedWave >= 2 && random.Value < 0.38f;
                plan.Add(new WaveSpawnPlanItem(useFast ? "zombie_fast" : "zombie_normal", difficulty));
            }

            for (var i = 0; i < leaderCount; i++)
            {
                plan.Add(new WaveSpawnPlanItem(i % 2 == 0 ? "zombie_leader" : "zombie_fat", difficulty + 0.15f));
            }

            for (var i = 0; i < eliteCount; i++)
            {
                plan.Add(new WaveSpawnPlanItem("zombie_elite", difficulty + 0.25f, true));
            }

            return plan;
        }

        public static float SpawnRateForWave(int wave) => Mathf.Min(12f, 2.2f + Mathf.Max(1, wave) * 0.45f);
    }

    public static class WeaponRules
    {
        public static float ResolveInterpolatedRadiusPixels(float baseRadius, float maxRadius, int level, int maxLevel)
        {
            var normalizedLevel = Mathf.Max(1, level);
            var normalizedMax = Mathf.Max(1, maxLevel);
            var t = normalizedMax <= 1 ? 0f : (normalizedLevel - 1f) / (normalizedMax - 1f);
            return Mathf.Lerp(Mathf.Max(40f, baseRadius), Mathf.Max(baseRadius, maxRadius), t);
        }

        public static float ResolveFuboQinAuraRadiusPixels(float baseRadius, float maxRadius, int level, int maxLevel) =>
            ResolveInterpolatedRadiusPixels(baseRadius, maxRadius, level, maxLevel);

        public static float ResolveRotatingKnifeOrbitRadiusPixels(float baseRadius, float maxRadius, int level, int maxLevel) =>
            ResolveInterpolatedRadiusPixels(baseRadius, maxRadius, level, maxLevel);
    }

    internal static class CapsuleFootballTargeting
    {
        public const int SectorCount = 8;

        public static EnemyRuntime FindDensestTarget(IReadOnlyList<EnemyRuntime> enemies, Vector2 origin,
            float radius, int[] sectorCounts, float[] sectorNearestDistances)
        {
            if (enemies == null || sectorCounts == null || sectorNearestDistances == null ||
                sectorCounts.Length < SectorCount || sectorNearestDistances.Length < SectorCount)
                return null;

            for (var sector = 0; sector < SectorCount; sector++)
            {
                sectorCounts[sector] = 0;
                sectorNearestDistances[sector] = float.PositiveInfinity;
            }

            var radiusSquared = radius * radius;
            for (var i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                if (enemy == null || !enemy.Active) continue;
                var delta = enemy.Position - origin;
                var distanceSquared = delta.sqrMagnitude;
                if (distanceSquared > radiusSquared) continue;
                var sector = SectorFor(delta);
                sectorCounts[sector]++;
                if (distanceSquared < sectorNearestDistances[sector])
                    sectorNearestDistances[sector] = distanceSquared;
            }

            var bestSector = -1;
            for (var sector = 0; sector < SectorCount; sector++)
            {
                if (sectorCounts[sector] == 0) continue;
                if (bestSector < 0 || sectorCounts[sector] > sectorCounts[bestSector] ||
                    (sectorCounts[sector] == sectorCounts[bestSector] &&
                     sectorNearestDistances[sector] < sectorNearestDistances[bestSector]))
                {
                    bestSector = sector;
                }
            }

            if (bestSector < 0) return null;
            EnemyRuntime nearest = null;
            var nearestDistance = float.PositiveInfinity;
            for (var i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                if (enemy == null || !enemy.Active) continue;
                var delta = enemy.Position - origin;
                var distanceSquared = delta.sqrMagnitude;
                if (distanceSquared > radiusSquared || SectorFor(delta) != bestSector || distanceSquared >= nearestDistance)
                    continue;
                nearest = enemy;
                nearestDistance = distanceSquared;
            }
            return nearest;
        }

        private static int SectorFor(Vector2 delta)
        {
            var normalized = (Mathf.Atan2(delta.y, delta.x) + Mathf.PI) / (Mathf.PI * 2f);
            return Mathf.Clamp(Mathf.FloorToInt(normalized * SectorCount), 0, SectorCount - 1);
        }
    }

    public readonly struct BossHudEntry
    {
        public readonly string Name;
        public readonly float Hp;
        public readonly float MaxHp;

        public BossHudEntry(string name, float hp, float maxHp)
        {
            Name = name;
            Hp = hp;
            MaxHp = maxHp;
        }
    }

    public readonly struct EffectHudEntry
    {
        public readonly string Id;
        public readonly string Title;
        public readonly string Detail;
        public readonly float RemainingSeconds;

        public EffectHudEntry(string id, string title, string detail, float remainingSeconds)
        {
            Id = id;
            Title = title;
            Detail = detail ?? string.Empty;
            RemainingSeconds = remainingSeconds;
        }
    }

    public readonly struct BattleSnapshot
    {
        public readonly float Hp;
        public readonly float MaxHp;
        public readonly int Level;
        public readonly int Experience;
        public readonly int RequiredExperience;
        public readonly int Wave;
        public readonly int WaveCount;
        public readonly int Kills;
        public readonly int EnemyCount;
        public readonly float BossHp;
        public readonly float BossMaxHp;
        public readonly BossHudEntry[] Bosses;
        public readonly EffectHudEntry[] Effects;
        public readonly int ActivePoolObjects;
        public readonly float Fps;
        public readonly float AttackMultiplier;
        public readonly float MoveSpeedPixels;

        public BattleSnapshot(float hp, float maxHp, int level, int experience, int requiredExperience,
            int wave, int waveCount, int kills, int enemyCount, float bossHp, float bossMaxHp,
            BossHudEntry[] bosses, EffectHudEntry[] effects, int activePoolObjects, float fps,
            float attackMultiplier, float moveSpeedPixels)
        {
            Hp = hp;
            MaxHp = maxHp;
            Level = level;
            Experience = experience;
            RequiredExperience = requiredExperience;
            Wave = wave;
            WaveCount = waveCount;
            Kills = kills;
            EnemyCount = enemyCount;
            BossHp = bossHp;
            BossMaxHp = bossMaxHp;
            Bosses = bosses ?? System.Array.Empty<BossHudEntry>();
            Effects = effects ?? System.Array.Empty<EffectHudEntry>();
            ActivePoolObjects = activePoolObjects;
            Fps = fps;
            AttackMultiplier = attackMultiplier;
            MoveSpeedPixels = moveSpeedPixels;
        }
    }
}
