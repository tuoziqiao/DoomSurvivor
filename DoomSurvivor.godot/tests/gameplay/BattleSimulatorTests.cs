using DoomSurvivor.Core;
using DoomSurvivor.Gameplay;

namespace DoomSurvivor.Tests.Gameplay;

[TestClass]
public sealed class BattleSimulatorTests
{
    [TestMethod]
    public void TickDelegatesMovementAndSpawningToGameplaySystems()
    {
        var simulator = new BattleSimulator(CreateLoadout(enemyHp: 1000f, quickDuration: 30f));

        for (var index = 0; index < 90; index++) simulator.Tick(1f / 60f, new BattleInput(1f, 0f));

        var snapshot = simulator.CreateSnapshot();
        Assert.IsTrue(snapshot.Player.Position.X > 1000f);
        Assert.IsTrue(snapshot.Enemies.Count > 0);
    }

    [TestMethod]
    public void QuickRunCompletesThroughLifecycleSystem()
    {
        var simulator = new BattleSimulator(CreateLoadout(enemyHp: 1000f, quickDuration: 0.2f));

        simulator.Tick(0.1f, new BattleInput(0f, 0f));
        simulator.Tick(0.1f, new BattleInput(0f, 0f));
        simulator.Tick(0.1f, new BattleInput(0f, 0f));

        Assert.IsTrue(simulator.IsFinished);
        Assert.IsTrue(simulator.Victory);
    }

    [TestMethod]
    public void AutoAttackKillsEnemyAndCreatesCrystal()
    {
        var simulator = new BattleSimulator(CreateLoadout(enemyHp: 1f, quickDuration: 30f));

        for (var index = 0; index < 180; index++) simulator.Tick(1f / 60f, new BattleInput(0f, 0f));

        var snapshot = simulator.CreateSnapshot();
        Assert.IsTrue(simulator.KillCount > 0);
        Assert.IsTrue(snapshot.Crystals.Count > 0);
    }

    private static RunLoadout CreateLoadout(float enemyHp, float quickDuration)
    {
        var character = new CharacterConfig
        {
            Id = "test_character",
            Name = "Test Character",
            MaxHp = 100f,
            MoveSpeed = 120f,
            PickupRadius = 80f,
            CollisionRadius = 20f,
            DamageMultiplier = 1f,
            AttackSpeedMultiplier = 1f,
            CritDamage = 2f,
            ExperienceMultiplier = 1f,
            Armor = 0f
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
        var balance = new BalanceConfig
        {
            Experience = new ExperienceBalance { LevelThresholds = new List<int> { 5, 10, 20 } },
            Combat = new CombatBalance { DamageVarianceMin = 1f, DamageVarianceMax = 1f, InvincibilityDuration = 0.5f },
            Player = new PlayerBalance { LevelMaxHpGrowthPercent = 0.05f },
            Spawn = new SpawnBalance { MinSpawnDistanceFromPlayer = 460f },
            Performance = new PerformanceBalance { MobileMaxEnemies = 50 }
        };
        return new RunLoadout(GameMode.QuickTest, "grass_tile_01", character, skin, stage, balance, new[] { enemy });
    }
}
