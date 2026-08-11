using System.Collections.Generic;
using System.IO;
using System.Linq;
using DoomSurvivor.Core;
using DoomSurvivor.Gameplay;
using DoomSurvivor.Infrastructure;
using NUnit.Framework;
using UnityEngine;

namespace DoomSurvivor.Tests.EditMode
{
    public sealed class CoreRulesTests
    {
        [Test]
        public void WorldScale_UsesOneHundredPixelsPerUnit()
        {
            Assert.That(WorldScale.ToUnits(250f), Is.EqualTo(2.5f));
            Assert.That(WorldScale.ToPixels(1.25f), Is.EqualTo(125f));
        }

        [Test]
        public void DamageFormula_AppliesMultipliersDefenseAndMinimum()
        {
            Assert.That(DamageFormula.Calculate(10, 2, 1.5f, 2, 1, 5), Is.EqualTo(55));
            Assert.That(DamageFormula.Calculate(1, 1, 1, 1, 0.95f, 999), Is.EqualTo(1));
        }

        [Test]
        public void PlayerLevelMaxHp_ScalesBaseMaxHpWithLevel()
        {
            Assert.That(PlayerLevelMaxHp.ScaledBaseMaxHp(90f, 1, 0.05f), Is.EqualTo(90f));
            Assert.That(PlayerLevelMaxHp.ScaledBaseMaxHp(90f, 3, 0.05f), Is.EqualTo(99f));
        }

        [Test]
        public void PlayerLevelMaxHp_AddsDeltaToCurrentHpWhenMaxIncreases()
        {
            Assert.That(PlayerLevelMaxHp.ApplyHpAfterMaxIncrease(50f, 90f, 99f), Is.EqualTo(59f));
            Assert.That(PlayerLevelMaxHp.ApplyHpAfterMaxIncrease(90f, 90f, 99f), Is.EqualTo(99f));
        }

        [Test]
        public void HasAvailableUpgrades_ReturnsFalseWhenAllWeaponsAndSkillsMaxed()
        {
            var weapons = new List<WeaponConfig>
            {
                new() { Id = "w1", MaxLevel = 2 },
                new() { Id = "w2", MaxLevel = 3 }
            };
            var skills = new List<SkillConfig>
            {
                new() { Id = "s1", MaxLevel = 2 }
            };
            var owned = new Dictionary<string, OwnedUpgrade>
            {
                ["w1"] = new OwnedUpgrade { Id = "w1", Level = 2 },
                ["w2"] = new OwnedUpgrade { Id = "w2", Level = 3 },
                ["s1"] = new OwnedUpgrade { Id = "s1", Level = 2 }
            };

            Assert.That(BattleController.HasAvailableUpgrades(weapons, skills, owned), Is.False);
        }

        [Test]
        public void HasAvailableUpgrades_ReturnsTrueWhenAnyItemBelowMaxLevel()
        {
            var weapons = new List<WeaponConfig>
            {
                new() { Id = "w1", MaxLevel = 2 }
            };
            var skills = new List<SkillConfig>
            {
                new() { Id = "s1", MaxLevel = 5 }
            };
            var owned = new Dictionary<string, OwnedUpgrade>
            {
                ["w1"] = new OwnedUpgrade { Id = "w1", Level = 2 },
                ["s1"] = new OwnedUpgrade { Id = "s1", Level = 4 }
            };

            Assert.That(BattleController.HasAvailableUpgrades(weapons, skills, owned), Is.True);
            Assert.That(BattleController.HasAvailableUpgrades(weapons, skills, new Dictionary<string, OwnedUpgrade>()),
                Is.True);
        }

        [Test]
        public void FireBottle_ZoneRadiusGrowsStrictlyWithLevel()
        {
            var path = Path.Combine(Application.streamingAssetsPath, "GameConfig", "weapons.json");
            var weapons = ConfigJson.DeserializeFile<WeaponsConfig>(path);
            var fireBottle = weapons.Weapons.First(weapon => weapon.Id == "fire_bottle");
            var expected = new[] { 50f, 65f, 80f, 100f, 120f, 145f, 170f, 200f };
            var previous = 0f;
            for (var level = 1; level <= 8; level++)
            {
                Assert.That(fireBottle.LevelEffects.TryGetValue(level.ToString(), out var effect), Is.True);
                Assert.That(effect.TryGetValue("zoneRadius", out var radius), Is.True);
                Assert.That(radius, Is.EqualTo(expected[level - 1]));
                Assert.That(radius, Is.GreaterThan(previous));
                previous = radius;
            }
        }

        [Test]
        public void ExperienceProgress_QueuesConsecutiveLevels()
        {
            var progress = new ExperienceProgress(new[] { 10, 20, 30 });
            progress.Add(35);
            Assert.That(progress.Level, Is.EqualTo(3));
            Assert.That(progress.Current, Is.EqualTo(5));
            Assert.That(progress.PendingLevelUps, Is.EqualTo(2));
            Assert.That(progress.ConsumeLevelUp(), Is.True);
        }

        [Test]
        public void SpatialHashGrid_QueriesNearbyBuckets()
        {
            var grid = new SpatialHashGrid<string>(1f);
            grid.Insert(0.1f, 0.1f, "near");
            grid.Insert(5f, 5f, "far");
            var results = new List<string>();
            grid.Query(0, 0, 0.5f, results);
            CollectionAssert.Contains(results, "near");
            CollectionAssert.DoesNotContain(results, "far");
        }

        [Test]
        public void SaveMigration_UpgradesOldProfileToV4()
        {
            var old = new SaveData
            {
                SaveVersion = 1,
                UnlockedCharacters = null,
                UnlockedSkins = null,
                SelectedSkinByCharacter = null,
                CharacterOrder = null,
                SelectedCharacterId = string.Empty
            };
            var migrated = SaveMigration.Migrate(old);
            Assert.That(migrated.SaveVersion, Is.EqualTo(4));
            Assert.That(migrated.SelectedCharacterId, Is.EqualTo("lin_xian"));
            CollectionAssert.Contains(migrated.UnlockedSkins, "lin_xian_wasteland");
            Assert.That(migrated.CharacterOrder, Has.Count.EqualTo(7));
        }

        [Test]
        public void SettingsClamp_ProtectsPerformanceBounds()
        {
            Assert.That(new GameSettings().CapsuleFootballDuration, Is.EqualTo(30f));
            Assert.That(new GameSettings().AltarRefreshChance, Is.EqualTo(30));
            var settings = new GameSettings
            {
                MaxEnemyDisplay = 9999,
                MasterVolume = -1f,
                WaveCount = 0,
                FirstWaveMobCount = 999,
                WaveMobCountMultiplier = 1.234f,
                RotatingKnifeRotationSpeedMul = 1.234f,
                RotatingKnifeBaseOrbitRadius = 10f,
                RotatingKnifeMaxOrbitRadius = 999f,
                FuboQinBaseAuraRadius = 10f,
                FuboQinMaxAuraRadius = 999f,
                AltarBloodPactHpCost = 0f,
                AltarMagnetBurstHpCost = 999f,
                HealingChickenCount = 999,
                HealingChickenRefreshChance = 999,
                AltarRefreshChance = 999,
                CapsuleFootballDuration = 999f,
                CrateEffectWeights = null
            };
            settings.Clamp();
            Assert.That(settings.MaxEnemyDisplay, Is.EqualTo(1000));
            Assert.That(settings.MasterVolume, Is.Zero);
            Assert.That(settings.WaveCount, Is.EqualTo(1));
            Assert.That(settings.FirstWaveMobCount, Is.EqualTo(500));
            Assert.That(settings.WaveMobCountMultiplier, Is.EqualTo(1.25f));
            Assert.That(settings.RotatingKnifeRotationSpeedMul, Is.EqualTo(1.25f));
            Assert.That(settings.RotatingKnifeBaseOrbitRadius, Is.EqualTo(40f));
            Assert.That(settings.RotatingKnifeMaxOrbitRadius, Is.EqualTo(400f));
            Assert.That(settings.FuboQinBaseAuraRadius, Is.EqualTo(40f));
            Assert.That(settings.FuboQinMaxAuraRadius, Is.EqualTo(400f));
            Assert.That(settings.AltarBloodPactHpCost, Is.EqualTo(1f));
            Assert.That(settings.AltarMagnetBurstHpCost, Is.EqualTo(90f));
            Assert.That(settings.AltarRefreshChance, Is.EqualTo(100));
            Assert.That(settings.HealingChickenCount, Is.EqualTo(20));
            Assert.That(settings.HealingChickenRefreshChance, Is.EqualTo(100));
            Assert.That(settings.CrateEffectWeights["xp_burst"], Is.EqualTo(2f));
            Assert.That(settings.CrateEffectWeights["magnet_burst"], Is.EqualTo(1f));
            Assert.That(settings.CrateEffectWeights["anesthetic_capsule"], Is.EqualTo(1f));
            Assert.That(settings.HiddenCrateEffectWeights["scooter_boost"], Is.EqualTo(1f));
            Assert.That(settings.HiddenCrateEffectWeights["capsule_football"], Is.EqualTo(1f));
            Assert.That(settings.HiddenCrateEffectWeights["purge"], Is.EqualTo(1f));
            Assert.That(settings.CapsuleFootballDuration, Is.EqualTo(600f));
            Assert.That(settings.AnestheticCapsuleDuration, Is.EqualTo(20f));
            Assert.That(settings.AnestheticCapsuleSlowPercent, Is.EqualTo(20f));
            Assert.That(settings.AltarEffectWeights["blood_pact"], Is.EqualTo(1f));
            Assert.That(settings.AltarEffectWeights["random_teleport"], Is.EqualTo(1f));
            Assert.That(settings.AltarEffectWeights["stun_watch"], Is.EqualTo(1f));
            Assert.That(settings.AltarMagnetDuration, Is.EqualTo(4f));
            Assert.That(settings.AltarTeleportHpCost, Is.EqualTo(5f));
            Assert.That(settings.AltarStunWatchDuration, Is.EqualTo(10f));
        }

        [Test]
        public void CapsuleFootballTargeting_PicksNearestTargetInDensestSector()
        {
            var denseNear = new EnemyRuntime { Active = true, Position = new Vector2(1f, 2f) };
            var enemies = new System.Collections.Generic.List<EnemyRuntime>
            {
                new() { Active = true, Position = new Vector2(-1f, 0f) },
                denseNear,
                new() { Active = true, Position = new Vector2(2f, 4f) },
                new() { Active = true, Position = new Vector2(2.5f, 5f) },
                new() { Active = false, Position = new Vector2(-0.5f, 0f) }
            };
            var counts = new int[CapsuleFootballTargeting.SectorCount];
            var distances = new float[CapsuleFootballTargeting.SectorCount];

            var selected = CapsuleFootballTargeting.FindDensestTarget(enemies, Vector2.zero, 10f, counts, distances);

            Assert.That(selected, Is.SameAs(denseNear));
            Assert.That(CapsuleFootballTargeting.FindDensestTarget(enemies, Vector2.zero, 0.5f, counts, distances),
                Is.Null);
        }

        [Test]
        public void HealingChicken_RestoresCurrentMaximumHealth()
        {
            var player = new PlayerRuntime { Hp = 17f, MaxHp = 125f, HitFlashRemaining = 0.2f };

            var restored = BattleController.RestoreFullHealth(player);

            Assert.That(restored, Is.EqualTo(108f));
            Assert.That(player.Hp, Is.EqualTo(125f));
            Assert.That(player.HitFlashRemaining, Is.Zero);

            Assert.That(BattleController.RestoreFullHealth(player), Is.Zero);
            Assert.That(player.Hp, Is.EqualTo(125f));
        }

        [Test]
        public void HealingChickenConfig_OldStageUsesDefaults()
        {
            var mapEvents = ConfigJson.Deserialize<MapEventsConfig>("{}");

            Assert.That(mapEvents.HealingChickenCount, Is.EqualTo(4));
            Assert.That(mapEvents.HealingChickenInteractRadius, Is.EqualTo(36f));
        }

        [Test]
        public void CapsuleFootballProjectile_RecordsAtMostThreeDistinctTargets()
        {
            var first = new EnemyRuntime();
            var second = new EnemyRuntime();
            var third = new EnemyRuntime();
            var fourth = new EnemyRuntime();
            var projectile = new ProjectileRuntime();

            projectile.RegisterFootballHit(first);
            projectile.RegisterFootballHit(first);
            projectile.RegisterFootballHit(second);
            projectile.RegisterFootballHit(third);
            projectile.RegisterFootballHit(fourth);

            Assert.That(projectile.FootballHitCount, Is.EqualTo(3));
            Assert.That(projectile.HasHitWithFootball(first), Is.True);
            Assert.That(projectile.HasHitWithFootball(second), Is.True);
            Assert.That(projectile.HasHitWithFootball(third), Is.True);
            Assert.That(projectile.HasHitWithFootball(fourth), Is.False);
        }

        [Test]
        public void CapsuleFootballKnockback_AppliesFullDistanceToMobs_AndSkipsBosses()
        {
            var mob = new EnemyRuntime { Active = true, DashRemaining = 1f };
            var boss = new EnemyRuntime { Active = true, IsBoss = true, DashRemaining = 1f };

            BattleController.ApplyCapsuleFootballKnockback(mob, Vector2.right);
            BattleController.ApplyCapsuleFootballKnockback(boss, Vector2.right);

            Assert.That(WorldScale.ToPixels(mob.KnockbackRemaining.magnitude), Is.EqualTo(120f).Within(0.01f));
            Assert.That(mob.KnockbackDurationRemaining, Is.EqualTo(0.22f).Within(0.001f));
            Assert.That(mob.DashRemaining, Is.Zero);
            Assert.That(boss.KnockbackRemaining, Is.EqualTo(Vector2.zero));
            Assert.That(boss.KnockbackDurationRemaining, Is.Zero);
            Assert.That(boss.DashRemaining, Is.EqualTo(1f));
        }

        [Test]
        public void CapsuleFootballConfig_IsAddedToLegacyRemoteStageOnce()
        {
            var stage = new StageConfig();

            BattleController.EnsureCapsuleFootballHiddenCrateEffect(stage);
            BattleController.EnsureCapsuleFootballHiddenCrateEffect(stage);

            var effects = stage.MapEvents.HiddenCrateEffects.Where(value => value.Id == "capsule_football").ToList();
            Assert.That(effects, Has.Count.EqualTo(1));
            Assert.That(effects[0].Weight, Is.EqualTo(1f));
            Assert.That(effects[0].Duration, Is.EqualTo(30f));
        }

        [Test]
        public void SettingsClamp_NormalizesMapSkinId()
        {
            var settings = new GameSettings { MapSkinId = "missing_tile" };
            settings.Clamp();
            Assert.That(settings.MapSkinId, Is.EqualTo("grass_tile_01"));

            settings.MapSkinId = "grass_tile_03";
            settings.Clamp();
            Assert.That(settings.MapSkinId, Is.EqualTo("grass_tile_03"));
            Assert.That(GameSettings.NormalizeMapSkinId("GRASS_TILE_02"), Is.EqualTo("grass_tile_02"));
        }

        [Test]
        public void WaveRules_MatchLegacyModeCountsAndGrowth()
        {
            Assert.That(WaveRules.ResolveWaveCount(GameMode.Normal, 10), Is.EqualTo(10));
            Assert.That(WaveRules.ResolveWaveCount(GameMode.QuickTest, 10), Is.EqualTo(5));
            Assert.That(WaveRules.ResolveWaveCount(GameMode.QuickTest, 1), Is.EqualTo(3));
            Assert.That(WaveRules.MobCountForWave(1, 8, WaveRules.Growth, 500), Is.EqualTo(8));
            Assert.That(WaveRules.MobCountForWave(2, 8, WaveRules.Growth, 500), Is.EqualTo(12));
            Assert.That(WaveRules.MobCountForWave(3, 8, WaveRules.Growth, 500), Is.EqualTo(19));
            Assert.That(WaveRules.MobCountForWave(5, 8, WaveRules.Growth, 500), Is.EqualTo(46));
        }

        [Test]
        public void WaveRules_SpecialSpawnChance_ScalesWithWave()
        {
            Assert.That(WaveRules.SpecialSpawnChance(2, 4, 25f, 8f, 70f), Is.EqualTo(0f));
            Assert.That(WaveRules.SpecialSpawnChance(4, 4, 25f, 8f, 70f), Is.EqualTo(25f));
            Assert.That(WaveRules.SpecialSpawnChance(8, 4, 25f, 8f, 70f), Is.EqualTo(57f));
            Assert.That(WaveRules.SpecialSpawnChance(20, 4, 25f, 8f, 70f), Is.EqualTo(70f));
        }

        [Test]
        public void WaveRules_AddLeadersAndElitesInLaterWaves()
        {
            var rules = new WaveSpecialSpawnRules(4, 100f, 0f, 100f, 2, 3, 100f, 0f, 100f, 2);
            var plan = WaveRules.PlanWave(8, 8, 1f, WaveRules.Growth, 200, new FixedRandomSource(0.5f), rules);
            var smallCount = WaveRules.MobCountForWave(8, 8, WaveRules.Growth, 200);

            Assert.That(plan.Count(item => item.EnemyId == "zombie_normal" || item.EnemyId == "zombie_fast"),
                Is.EqualTo(smallCount));
            Assert.That(plan.Count(item => item.EnemyId == "zombie_leader" || item.EnemyId == "zombie_fat"),
                Is.EqualTo(2));
            Assert.That(plan.Count(item => item.EnemyId == "zombie_elite" && item.IsElite), Is.EqualTo(2));
            Assert.That(WaveRules.SpawnRateForWave(30), Is.EqualTo(12f));
        }

        [Test]
        public void WaveRules_NoSpecialSpawnsBeforeStartWave()
        {
            var rules = new WaveSpecialSpawnRules(5, 100f, 10f, 100f, 4, 4, 100f, 10f, 100f, 6);
            var plan = WaveRules.PlanWave(3, 8, 1f, WaveRules.Growth, 200, new FixedRandomSource(0.01f), rules);
            Assert.That(plan.All(item => item.EnemyId == "zombie_normal" || item.EnemyId == "zombie_fast"), Is.True);
        }

        [Test]
        public void WeaponRules_FuboQinAuraRadius_InterpolatesByLevel()
        {
            Assert.That(WeaponRules.ResolveFuboQinAuraRadiusPixels(90f, 180f, 1, 8), Is.EqualTo(90f).Within(0.01f));
            Assert.That(WeaponRules.ResolveFuboQinAuraRadiusPixels(90f, 180f, 8, 8), Is.EqualTo(180f).Within(0.01f));
            Assert.That(WeaponRules.ResolveFuboQinAuraRadiusPixels(90f, 180f, 4, 8), Is.EqualTo(128.57f).Within(0.1f));
        }

        [Test]
        public void WeaponRules_RotatingKnifeOrbitRadius_InterpolatesByLevel()
        {
            Assert.That(WeaponRules.ResolveRotatingKnifeOrbitRadiusPixels(86f, 137f, 1, 8), Is.EqualTo(86f).Within(0.01f));
            Assert.That(WeaponRules.ResolveRotatingKnifeOrbitRadiusPixels(86f, 137f, 8, 8), Is.EqualTo(137f).Within(0.01f));
            Assert.That(WeaponRules.ResolveRotatingKnifeOrbitRadiusPixels(86f, 137f, 4, 8), Is.EqualTo(103.29f).Within(0.1f));
        }

        private sealed class FixedRandomSource : IRandomSource
        {
            public FixedRandomSource(float value) => Value = value;
            public float Value { get; }
            public int Range(int minInclusive, int maxExclusive) => minInclusive;
            public float Range(float minInclusive, float maxInclusive) => minInclusive;
        }
    }
}
