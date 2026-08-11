using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using DoomSurvivor.Core;

namespace DoomSurvivor.Gameplay;

public readonly record struct BattleInput(float X, float Y);

public sealed class PlayerSnapshot
{
    public Vector2 Position;
    public float Hp;
    public float MaxHp;
    public float MoveSpeed;
    public float Armor;
    public int Level;
    public int Experience;
    public int RequiredExperience;
}

public sealed class EnemySnapshot
{
    public string Id = string.Empty;
    public Vector2 Position;
    public float Hp;
    public float MaxHp;
    public float Radius;
    public bool IsBoss;
    public bool IsElite;
}

public sealed class CrystalSnapshot
{
    public Vector2 Position;
    public int Value;
    public string Kind = "small";
}

public sealed class WeaponSnapshot
{
    public string Id = string.Empty;
    public string Name = string.Empty;
    public string Icon = string.Empty;
    public int Level;
    public int MaxLevel;
    public bool IsPromoted;
}

public sealed class PassiveSnapshot
{
    public string Id = string.Empty;
    public string Name = string.Empty;
    public string Icon = string.Empty;
    public int Level;
    public int MaxLevel;
}

public sealed class UpgradeChoiceSnapshot
{
    public string Id = string.Empty;
    public string Name = string.Empty;
    public string Description = string.Empty;
    public string Icon = string.Empty;
    public bool IsWeapon;
    public int CurrentLevel;
    public int NextLevel;
    public int MaxLevel;
}

public sealed class MapEventSnapshot
{
    public string Id = string.Empty;
    public string Type = string.Empty;
    public Vector2 Position;
    public float Radius;
    public bool Active;
}

public sealed class CombatEffectSnapshot
{
    public string Kind = string.Empty;
    public Vector2 Position;
    public Vector2 Target;
    public float Radius;
    public float Remaining;
    public float Lifetime;
    public int Strength;
}

public sealed class BattleSnapshot
{
    public PlayerSnapshot Player = new();
    public List<EnemySnapshot> Enemies = new();
    public List<CrystalSnapshot> Crystals = new();
    public List<WeaponSnapshot> Weapons = new();
    public List<PassiveSnapshot> Passives = new();
    public List<UpgradeChoiceSnapshot> UpgradeChoices = new();
    public List<MapEventSnapshot> MapEvents = new();
    public List<CombatEffectSnapshot> Effects = new();
    public float Elapsed;
    public int KillCount;
    public float TotalDamage;
    public int TotalExperience;
    public int PendingLevelUps;
    public int CurrentWave;
    public int WaveCount;
    public string WaveState = string.Empty;
    public bool BossSpawned;
    public bool IsFinished;
    public bool Victory;
}

public sealed class BattleSimulator
{
    private readonly RunLoadout loadout;
    private readonly List<EnemyRuntime> enemies = new();
    private readonly List<CrystalRuntime> crystals = new();
    private readonly List<FireZoneRuntime> fireZones = new();
    private readonly List<MapEventRuntime> mapEvents = new();
    private readonly List<CombatEffectRuntime> effects = new();
    private readonly List<UpgradeChoiceRuntime> upgradeChoices = new();
    private readonly ExperienceProgress experience;
    private readonly SpatialHashGrid<EnemyRuntime> spatialHash;
    private readonly BattleMovementSystem movementSystem;
    private readonly BattleWaveSystem waveSystem;
    private readonly BattleCombatSystem combatSystem;
    private readonly BattlePickupSystem pickupSystem;
    private readonly BattleUpgradeSystem upgradeSystem;
    private readonly BattleMapEventSystem mapEventSystem;
    private readonly BattleLifecycleSystem lifecycleSystem;
    private float elapsed;
    private int kills;
    private float totalDamage;
    private bool finished;
    private bool victory;

    public BattleSimulator(RunLoadout loadout, int seed = 20260811)
    {
        this.loadout = loadout;
        var random = new Random(seed);
        experience = new ExperienceProgress(loadout.Balance.Experience.LevelThresholds);
        spatialHash = new SpatialHashGrid<EnemyRuntime>(Math.Max(16f, loadout.Balance.Performance.SpatialHashCellSize));
        var mapCenter = new Vector2(loadout.Stage.MapWidth * 0.5f, loadout.Stage.MapHeight * 0.5f);
        Player = new PlayerRuntime
        {
            Position = mapCenter,
            Hp = Math.Max(1f, loadout.Character.MaxHp),
            MaxHp = Math.Max(1f, loadout.Character.MaxHp),
            BaseMaxHp = Math.Max(1f, loadout.Character.MaxHp),
            BaseMoveSpeed = Math.Max(1f, loadout.Character.MoveSpeed),
            BasePickupRadius = Math.Max(1f, loadout.Character.PickupRadius),
            BaseArmor = Math.Max(0f, loadout.Character.Armor),
            BaseDamageMultiplier = Math.Max(0.1f, loadout.Character.DamageMultiplier),
            BaseAttackSpeedMultiplier = Math.Max(0.1f, loadout.Character.AttackSpeedMultiplier),
            CollisionRadius = Math.Max(8f, loadout.Character.CollisionRadius),
            CritRate = Math.Clamp(loadout.Character.CritRate, 0f, 1f),
            CritDamage = Math.Max(1f, loadout.Character.CritDamage),
            PickupRadiusMultiplier = 1f
        };

        enemyLimit = Math.Clamp(Math.Min(
            loadout.Stage.MaxEnemies > 0 ? loadout.Stage.MaxEnemies : loadout.Settings.MaxEnemyDisplay,
            Math.Max(12, loadout.Settings.MaxEnemyDisplay)), 12, 250);
        movementSystem = new BattleMovementSystem(loadout);
        waveSystem = new BattleWaveSystem(loadout, random);
        combatSystem = new BattleCombatSystem(loadout, random);
        pickupSystem = new BattlePickupSystem(loadout, experience);
        upgradeSystem = new BattleUpgradeSystem(loadout, random);
        mapEventSystem = new BattleMapEventSystem(loadout, random);
        lifecycleSystem = new BattleLifecycleSystem(loadout);
        upgradeSystem.Initialize(Player);
        mapEventSystem.Initialize(mapEvents);
    }

    private readonly int enemyLimit;

    public PlayerRuntime Player { get; }
    public float Elapsed => elapsed;
    public int KillCount => kills;
    public float TotalDamage => totalDamage;
    public int TotalExperience => experience.Total;
    public int Level => experience.Level;
    public bool IsFinished => finished;
    public bool Victory => victory;
    public bool NeedsUpgradeChoice => upgradeChoices.Count > 0;
    public int PendingLevelUps => experience.PendingLevelUps;
    public int CurrentWave => waveSystem.CurrentWave;
    public int WaveCount => waveSystem.WaveCount;
    public bool BossSpawned => waveSystem.BossSpawned;

    public void Tick(float deltaTime, BattleInput input)
    {
        if (finished || NeedsUpgradeChoice) return;
        var delta = Math.Clamp(deltaTime, 0f, 0.1f);
        if (delta <= 0f) return;
        elapsed += delta;

        combatSystem.BeginStep(delta);
        movementSystem.MovePlayer(Player, input, delta);
        waveSystem.Update(delta, elapsed, enemies, enemyLimit, SpawnEnemy);
        movementSystem.MoveEnemies(Player, enemies, delta);
        RebuildSpatialHash();

        mapEventSystem.Update(
            delta,
            Player,
            mapEvents,
            experience,
            AddExperience,
            amount => combatSystem.TakeEnvironmentalDamage(Player, amount),
            count => mapEventSystem.AddPoisonFog(mapEvents, count),
            SpawnBoss,
            PurgeNonBossEnemies,
            RecalculatePlayer);

        RebuildSpatialHash();
        combatSystem.ResolveContactAndAttack(
            delta,
            Player,
            enemies,
            spatialHash,
            upgradeSystem.Weapons,
            fireZones,
            effects,
            Kill,
            damage => totalDamage += damage);

        pickupSystem.Collect(Player, crystals);
        UpdatePlayerLevelStats();
        UpdateEffects(delta);
        lifecycleSystem.Cleanup(enemies, crystals, fireZones, effects);
        RebuildSpatialHash();

        if (experience.PendingLevelUps > 0)
        {
            OpenUpgradeChoice();
        }

        if (lifecycleSystem.TryFinish(elapsed, Player, enemies.Any(value => value.Active && value.IsBoss), out var completedVictory))
        {
            finished = true;
            victory = completedVictory;
        }
    }

    public void AddExperience(int amount)
    {
        if (amount <= 0) return;
        experience.Add(amount);
        UpdatePlayerLevelStats();
    }

    public bool ChooseUpgrade(string id, bool isWeapon)
    {
        if (!NeedsUpgradeChoice) return false;
        var choice = upgradeChoices.FirstOrDefault(value => value.IsWeapon == isWeapon && string.Equals(value.Id, id, StringComparison.Ordinal));
        if (choice is null || !upgradeSystem.Apply(choice.Id, choice.IsWeapon, Player)) return false;
        experience.ConsumeLevelUp();
        upgradeChoices.Clear();
        RecalculatePlayer();
        if (experience.PendingLevelUps > 0) OpenUpgradeChoice();
        return true;
    }

    public BattleSnapshot CreateSnapshot()
    {
        var snapshot = new BattleSnapshot
        {
            Player = new PlayerSnapshot
            {
                Position = Player.Position,
                Hp = Player.Hp,
                MaxHp = Player.MaxHp,
                MoveSpeed = Player.MoveSpeed,
                Armor = Player.Armor,
                Level = experience.Level,
                Experience = experience.Current,
                RequiredExperience = experience.RequiredForLevel(experience.Level)
            },
            Elapsed = elapsed,
            KillCount = kills,
            TotalDamage = totalDamage,
            TotalExperience = experience.Total,
            PendingLevelUps = experience.PendingLevelUps,
            CurrentWave = waveSystem.CurrentWave,
            WaveCount = waveSystem.WaveCount,
            WaveState = waveSystem.State,
            BossSpawned = waveSystem.BossSpawned,
            IsFinished = finished,
            Victory = victory
        };

        foreach (var enemy in enemies)
        {
            if (!enemy.Active) continue;
            snapshot.Enemies.Add(new EnemySnapshot
            {
                Id = enemy.Config.Id,
                Position = enemy.Position,
                Hp = enemy.Hp,
                MaxHp = enemy.MaxHp,
                Radius = enemy.Radius,
                IsBoss = enemy.IsBoss,
                IsElite = enemy.IsElite
            });
        }

        foreach (var crystal in crystals)
        {
            if (crystal.Active) snapshot.Crystals.Add(new CrystalSnapshot { Position = crystal.Position, Value = crystal.Value, Kind = crystal.Kind });
        }

        foreach (var data in upgradeSystem.GetWeaponSnapshotData())
        {
            snapshot.Weapons.Add(new WeaponSnapshot { Id = data.Id, Name = data.Name, Level = data.Level, MaxLevel = data.MaxLevel, Icon = data.Icon, IsPromoted = data.IsPromoted });
        }
        foreach (var data in upgradeSystem.GetPassiveSnapshotData())
        {
            snapshot.Passives.Add(new PassiveSnapshot { Id = data.Id, Name = data.Name, Level = data.Level, MaxLevel = data.MaxLevel, Icon = data.Icon });
        }
        foreach (var choice in upgradeChoices)
        {
            snapshot.UpgradeChoices.Add(new UpgradeChoiceSnapshot
            {
                Id = choice.Id,
                Name = choice.Name,
                Description = choice.Description,
                Icon = choice.Icon,
                IsWeapon = choice.IsWeapon,
                CurrentLevel = choice.CurrentLevel,
                NextLevel = choice.NextLevel,
                MaxLevel = choice.MaxLevel
            });
        }
        foreach (var mapEvent in mapEvents)
        {
            snapshot.MapEvents.Add(new MapEventSnapshot
            {
                Id = mapEvent.Id,
                Type = mapEvent.Type.ToString(),
                Position = mapEvent.Position,
                Radius = mapEvent.Radius,
                Active = mapEvent.Active
            });
        }
        foreach (var effect in effects)
        {
            snapshot.Effects.Add(new CombatEffectSnapshot
            {
                Kind = effect.Kind,
                Position = effect.Position,
                Target = effect.Target,
                Radius = effect.Radius,
                Remaining = effect.Remaining,
                Lifetime = effect.Lifetime,
                Strength = effect.Strength
            });
        }
        return snapshot;
    }

    private void OpenUpgradeChoice()
    {
        if (upgradeChoices.Count > 0) return;
        upgradeChoices.AddRange(upgradeSystem.CreateChoices());
    }

    private void SpawnEnemy(EnemyConfig config, bool isElite, bool isBoss, Vector2 offset)
    {
        var position = Player.Position + offset;
        var margin = BattleSimulationConstants.PlayerWorldMargin;
        position = new Vector2(
            Math.Clamp(position.X, margin, Math.Max(margin, loadout.Stage.MapWidth - margin)),
            Math.Clamp(position.Y, margin, Math.Max(margin, loadout.Stage.MapHeight - margin)));
        var hpMultiplier = isElite ? 2.2f : 1f;
        if (isBoss) hpMultiplier = 1f;
        enemies.Add(new EnemyRuntime
        {
            Config = config,
            Position = position,
            Hp = Math.Max(1f, config.MaxHp * hpMultiplier),
            MaxHp = Math.Max(1f, config.MaxHp * hpMultiplier),
            Radius = Math.Max(8f, config.CollisionRadius),
            Active = true,
            IsElite = isElite,
            IsBoss = isBoss
        });
    }

    private void SpawnBoss()
    {
        var offset = new Vector2(Math.Max(460f, loadout.Balance.Spawn.MinSpawnDistanceFromPlayer), 0f);
        waveSystem.TrySpawnBoss(SpawnEnemy, offset);
    }

    private void PurgeNonBossEnemies()
    {
        foreach (var enemy in enemies)
        {
            if (enemy.Active && !enemy.IsBoss) enemy.Active = false;
        }
    }

    private void Kill(EnemyRuntime enemy)
    {
        if (!enemy.Active) return;
        enemy.Active = false;
        kills++;
        var value = Math.Max(1, enemy.Config.ExperienceReward);
        var kind = value >= 100 ? "large" : value >= 10 ? "medium" : "small";
        crystals.Add(new CrystalRuntime { Position = enemy.Position, Value = value, Kind = kind });
    }

    private void RebuildSpatialHash()
    {
        spatialHash.Clear();
        foreach (var enemy in enemies)
        {
            if (enemy.Active) spatialHash.Insert(enemy.Position.X, enemy.Position.Y, enemy);
        }
    }

    private void RecalculatePlayer() => upgradeSystem.RecalculatePlayer(Player);

    private void UpdatePlayerLevelStats()
    {
        var oldMax = Player.MaxHp;
        Player.BaseMaxHp = PlayerLevelMaxHp.ScaledBaseMaxHp(loadout.Character.MaxHp, experience.Level, loadout.Balance.Player.LevelMaxHpGrowthPercent);
        upgradeSystem.RecalculatePlayer(Player);
        if (Player.MaxHp > oldMax) Player.Hp = PlayerLevelMaxHp.ApplyHpAfterMaxIncrease(Player.Hp, oldMax, Player.MaxHp);
    }

    private void UpdateEffects(float delta)
    {
        foreach (var effect in effects) effect.Remaining -= delta;
    }

    public sealed class PlayerRuntime
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Hp;
        public float MaxHp;
        public float BaseMaxHp;
        public float BaseMoveSpeed;
        public float MoveSpeed;
        public float BasePickupRadius;
        public float PickupRadius;
        public float PickupRadiusMultiplier = 1f;
        public float CollisionRadius;
        public float BaseDamageMultiplier;
        public float DamageMultiplier;
        public float BaseAttackSpeedMultiplier;
        public float AttackSpeedMultiplier;
        public float BaseArmor;
        public float Armor;
        public float CritRate;
        public float CritDamage;
        public float BonusDamageMultiplier;
        public float BonusMoveSpeedMultiplier;
        public float BonusAttackSpeedMultiplier;
        public float BonusPickupRadiusMultiplier;
        public float BonusMaxHp;
        public float BonusArmor;
        public float FlatDamageBonus;
        public float FlatMoveSpeedBonus;
        public float FlatMaxHpBonus;
    }
}
