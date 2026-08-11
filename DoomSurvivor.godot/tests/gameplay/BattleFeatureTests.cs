using System;
using System.Collections.Generic;
using System.Linq;
using DoomSurvivor.Core;
using DoomSurvivor.Gameplay;

namespace DoomSurvivor.Tests.Gameplay;

[TestClass]
public sealed class BattleFeatureTests
{
    [TestMethod]
    public void LevelUpPausesSimulationAndAppliesSelectedUpgrade()
    {
        var simulator = new BattleSimulator(CreateLoadout(experienceThreshold: 1, enemyHp: 1f, quickDuration: 30f));

        for (var index = 0; index < 120 && !simulator.NeedsUpgradeChoice; index++)
        {
            simulator.Tick(1f / 60f, new BattleInput(0f, 0f));
        }

        Assert.IsTrue(simulator.NeedsUpgradeChoice);
        var before = simulator.Elapsed;
        simulator.Tick(1f, new BattleInput(1f, 0f));
        Assert.AreEqual(before, simulator.Elapsed, 0.0001f);

        var choice = simulator.CreateSnapshot().UpgradeChoices.First();
        Assert.IsTrue(simulator.ChooseUpgrade(choice.Id, choice.IsWeapon));
        Assert.IsFalse(simulator.NeedsUpgradeChoice);
        Assert.IsTrue(simulator.CreateSnapshot().Weapons.Any(value => value.Id == "wind_blade"));
    }

    [TestMethod]
    public void QuickRunCapsWaveCountAtFiveAndCreatesMapEventSnapshot()
    {
        var loadout = CreateLoadout(experienceThreshold: 50, enemyHp: 1000f, quickDuration: 30f);
        var simulator = new BattleSimulator(loadout);
        var snapshot = simulator.CreateSnapshot();

        Assert.AreEqual(5, snapshot.WaveCount);
        Assert.IsTrue(snapshot.MapEvents.Any(value => value.Type == "PoisonFog"));
        Assert.IsTrue(snapshot.MapEvents.Any(value => value.Type == "Crate"));
    }

    private static RunLoadout CreateLoadout(int experienceThreshold, float enemyHp, float quickDuration)
    {
        var character = new CharacterConfig
        {
            Id = "test_character",
            Name = "Test Character",
            MaxHp = 100f,
            MoveSpeed = 120f,
            PickupRadius = 1000f,
            CollisionRadius = 20f,
            DamageMultiplier = 1f,
            AttackSpeedMultiplier = 1f,
            CritDamage = 2f,
            ExperienceMultiplier = 1f,
            StartingWeaponId = "wind_blade"
        };
        var skin = new SkinConfig { Id = "test_skin", CharacterId = character.Id, ModelAsset = "p1.png" };
        var stage = new StageConfig
        {
            Id = "test_stage",
            MapWidth = 2000f,
            MapHeight = 1500f,
            QuickTestDuration = quickDuration,
            NormalModeDuration = 30f,
            MaxEnemies = 30,
            SpawnTimeline = new List<SpawnTimelineEntry>
            {
                new() { Time = 0f, EnemyId = "test_enemy", SpawnRate = 4f }
            }
        };
        var enemy = new EnemyConfig
        {
            Id = "test_enemy",
            MaxHp = enemyHp,
            MoveSpeed = 10f,
            ContactDamage = 1f,
            ExperienceReward = 1,
            CollisionRadius = 10f
        };
        var weapon = new WeaponConfig
        {
            Id = "wind_blade",
            Name = "Wind Blade",
            MaxLevel = 8,
            LevelEffects = new Dictionary<string, Dictionary<string, float>>
            {
                ["1"] = new() { ["damage"] = 28f, ["cooldown"] = 0.2f, ["range"] = 920f, ["penetration"] = 1f, ["projectileCount"] = 1f }
            }
        };
        var balance = new BalanceConfig
        {
            Experience = new ExperienceBalance { LevelThresholds = new List<int> { experienceThreshold, 10, 20 } },
            Combat = new CombatBalance { DamageVarianceMin = 1f, DamageVarianceMax = 1f, InvincibilityDuration = 0.5f },
            Player = new PlayerBalance { LevelMaxHpGrowthPercent = 0.05f },
            Spawn = new SpawnBalance { MinSpawnDistanceFromPlayer = 460f },
            Performance = new PerformanceBalance { MobileMaxEnemies = 50 }
        };
        return new RunLoadout(
            GameMode.QuickTest,
            "grass_tile_01",
            character,
            skin,
            stage,
            balance,
            new[] { enemy },
            new[] { weapon },
            Array.Empty<SkillConfig>(),
            new GameSettings { WaveCount = 10, CrateCount = 1, HiddenCrateCount = 0, AltarCount = 0, PoisonFogCount = 1, HealingChickenCount = 0 });
    }
}
