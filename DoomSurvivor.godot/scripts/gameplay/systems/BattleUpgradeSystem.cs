using System;
using System.Collections.Generic;
using System.Linq;
using DoomSurvivor.Core;

namespace DoomSurvivor.Gameplay;

internal sealed class BattleUpgradeSystem
{
    private readonly RunLoadout loadout;
    private readonly Random random;
    private readonly Dictionary<string, WeaponConfig> weaponConfigs;
    private readonly Dictionary<string, SkillConfig> skillConfigs;
    private readonly List<WeaponRuntime> weapons = new();
    private readonly List<PassiveRuntime> passives = new();

    public BattleUpgradeSystem(RunLoadout runLoadout, Random rng)
    {
        loadout = runLoadout;
        random = rng;
        weaponConfigs = loadout.Weapons
            .Where(value => value is not null && !string.IsNullOrWhiteSpace(value.Id))
            .GroupBy(value => value.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        skillConfigs = loadout.Skills
            .Where(value => value is not null && !string.IsNullOrWhiteSpace(value.Id))
            .GroupBy(value => value.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
    }

    public IReadOnlyList<WeaponRuntime> Weapons => weapons;
    public IReadOnlyList<PassiveRuntime> Passives => passives;

    public void Initialize(BattleSimulator.PlayerRuntime player)
    {
        var startingWeaponId = loadout.Character.StartingWeaponId;
        if (!string.IsNullOrWhiteSpace(startingWeaponId) && weaponConfigs.TryGetValue(startingWeaponId, out var startingConfig))
        {
            AddWeapon(startingConfig);
        }
        else if (weaponConfigs.Count > 0)
        {
            AddWeapon(weaponConfigs.Values.First());
        }
        else
        {
            var fallback = CreateFallbackWeapon();
            weaponConfigs[fallback.Id] = fallback;
            AddWeapon(fallback);
        }

        RecalculatePlayer(player);
    }

    public List<UpgradeChoiceRuntime> CreateChoices()
    {
        var candidates = new List<UpgradeChoiceRuntime>();

        foreach (var weapon in weapons.Where(value => !value.IsMaxLevel))
        {
            candidates.Add(CreateWeaponChoice(weapon.Config, weapon.Level));
        }

        if (weapons.Count < Math.Max(1, loadout.Balance.Player.MaxWeapons))
        {
            foreach (var config in weaponConfigs.Values)
            {
                if (weapons.Any(value => string.Equals(value.Config.Id, config.Id, StringComparison.Ordinal))) continue;
                candidates.Add(CreateWeaponChoice(config, 0));
            }
        }

        foreach (var passive in passives.Where(value => !value.IsMaxLevel))
        {
            candidates.Add(CreatePassiveChoice(passive.Config, passive.Level));
        }

        if (passives.Count < Math.Max(1, loadout.Balance.Player.MaxPassiveSkills))
        {
            foreach (var config in skillConfigs.Values)
            {
                if (passives.Any(value => string.Equals(value.Config.Id, config.Id, StringComparison.Ordinal))) continue;
                candidates.Add(CreatePassiveChoice(config, 0));
            }
        }

        if (candidates.Count == 0)
        {
            var fallback = weapons.FirstOrDefault();
            if (fallback is not null) candidates.Add(CreateWeaponChoice(fallback.Config, fallback.Level));
        }

        var choices = new List<UpgradeChoiceRuntime>(Math.Min(3, candidates.Count));
        while (choices.Count < 3 && candidates.Count > 0)
        {
            var totalWeight = candidates.Sum(value => Math.Max(1f, GetWeight(value)));
            var roll = (float)random.NextDouble() * totalWeight;
            var index = 0;
            for (; index < candidates.Count - 1; index++)
            {
                roll -= Math.Max(1f, GetWeight(candidates[index]));
                if (roll <= 0f) break;
            }

            choices.Add(candidates[index]);
            candidates.RemoveAt(index);
        }
        return choices;
    }

    public bool Apply(string id, bool isWeapon, BattleSimulator.PlayerRuntime player)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;
        if (isWeapon)
        {
            if (weapons.FirstOrDefault(value => string.Equals(value.Config.Id, id, StringComparison.Ordinal)) is { } existing)
            {
                if (existing.IsMaxLevel) return false;
                existing.Level++;
                existing.Cooldown = 0f;
            }
            else if (weaponConfigs.TryGetValue(id, out var config) && weapons.Count < Math.Max(1, loadout.Balance.Player.MaxWeapons))
            {
                AddWeapon(config);
            }
            else
            {
                return false;
            }
        }
        else
        {
            if (passives.FirstOrDefault(value => string.Equals(value.Config.Id, id, StringComparison.Ordinal)) is { } existing)
            {
                if (existing.IsMaxLevel) return false;
                existing.Level++;
            }
            else if (skillConfigs.TryGetValue(id, out var config) && passives.Count < Math.Max(1, loadout.Balance.Player.MaxPassiveSkills))
            {
                passives.Add(new PassiveRuntime { Config = config, Level = 1 });
            }
            else
            {
                return false;
            }
        }

        RecalculatePlayer(player);
        return true;
    }

    public void RecalculatePlayer(BattleSimulator.PlayerRuntime player)
    {
        var previousMax = Math.Max(1f, player.MaxHp);
        var strength = GetPassiveBonus("passive_strength", "damageMultiplierBonus");
        var swift = GetPassiveBonus("passive_swift", "moveSpeedBonus");
        var haste = GetPassiveBonus("passive_haste", "attackSpeedBonus");
        var magnet = GetPassiveBonus("passive_magnet", "pickupRadiusBonus");
        var toughnessHp = GetPassiveBonus("passive_toughness", "maxHpBonus");
        var toughnessArmor = GetPassiveBonus("passive_toughness", "armorBonus");

        player.BonusDamageMultiplier = strength;
        player.BonusMoveSpeedMultiplier = swift;
        player.BonusAttackSpeedMultiplier = haste;
        player.BonusPickupRadiusMultiplier = magnet;
        player.BonusMaxHp = toughnessHp;
        player.BonusArmor = toughnessArmor;
        player.MaxHp = Math.Max(1f, player.BaseMaxHp + toughnessHp + player.FlatMaxHpBonus);
        player.MoveSpeed = Math.Max(1f, player.BaseMoveSpeed * (1f + swift) + player.FlatMoveSpeedBonus);
        player.PickupRadius = Math.Max(1f, player.BasePickupRadius * (1f + magnet) * player.PickupRadiusMultiplier);
        player.DamageMultiplier = Math.Max(0.1f, player.BaseDamageMultiplier + strength + player.FlatDamageBonus);
        player.AttackSpeedMultiplier = Math.Max(0.1f, player.BaseAttackSpeedMultiplier * (1f + haste));
        player.Armor = Math.Max(0f, player.BaseArmor + toughnessArmor);
        if (player.MaxHp > previousMax) player.Hp = Math.Min(player.MaxHp, player.Hp + player.MaxHp - previousMax);
        player.Hp = Math.Clamp(player.Hp, 0f, player.MaxHp);
    }

    public IReadOnlyList<WeaponSnapshotData> GetWeaponSnapshotData()
    {
        return weapons.Select(value => new WeaponSnapshotData(
            value.Config.Id,
            value.Config.Name,
            value.Level,
            Math.Max(1, value.Config.MaxLevel),
            value.Config.Icon,
            value.Level >= Math.Max(1, value.Config.MaxLevel) && value.Config.Promotion is not null && !string.IsNullOrWhiteSpace(value.Config.Promotion.Name))).ToList();
    }

    public IReadOnlyList<PassiveSnapshotData> GetPassiveSnapshotData()
    {
        return passives.Select(value => new PassiveSnapshotData(
            value.Config.Id,
            value.Config.Name,
            value.Level,
            Math.Max(1, value.Config.MaxLevel),
            value.Config.Icon)).ToList();
    }

    private void AddWeapon(WeaponConfig config)
    {
        weapons.Add(new WeaponRuntime { Config = config, Level = 1, Cooldown = 0f });
    }

    private float GetPassiveBonus(string id, string effect)
    {
        var passive = passives.FirstOrDefault(value => string.Equals(value.Config.Id, id, StringComparison.Ordinal));
        return passive is null ? 0f : ConfigEffectReader.Read(passive.Config.LevelEffects, passive.Level, effect);
    }

    private float GetWeight(UpgradeChoiceRuntime choice)
    {
        if (choice.IsWeapon && weaponConfigs.TryGetValue(choice.Id, out var weapon)) return weapon.Weight;
        if (!choice.IsWeapon && skillConfigs.TryGetValue(choice.Id, out var skill)) return skill.Weight;
        return 1f;
    }

    private static UpgradeChoiceRuntime CreateWeaponChoice(WeaponConfig config, int currentLevel)
    {
        return new UpgradeChoiceRuntime
        {
            Id = config.Id,
            Name = config.Name,
            Description = config.Description,
            Icon = config.Icon,
            IsWeapon = true,
            CurrentLevel = currentLevel,
            NextLevel = Math.Max(1, currentLevel + 1),
            MaxLevel = Math.Max(1, config.MaxLevel)
        };
    }

    private static UpgradeChoiceRuntime CreatePassiveChoice(SkillConfig config, int currentLevel)
    {
        return new UpgradeChoiceRuntime
        {
            Id = config.Id,
            Name = config.Name,
            Description = config.Description,
            Icon = config.Icon,
            IsWeapon = false,
            CurrentLevel = currentLevel,
            NextLevel = Math.Max(1, currentLevel + 1),
            MaxLevel = Math.Max(1, config.MaxLevel)
        };
    }

    private static WeaponConfig CreateFallbackWeapon()
    {
        return new WeaponConfig
        {
            Id = "basic_auto",
            Name = "基础火力",
            Description = "阶段 5 的安全后备自动武器。",
            MaxLevel = 8,
            Icon = "wind_blade",
            LevelEffects = new Dictionary<string, Dictionary<string, float>>
            {
                ["1"] = new() { ["damage"] = 28f, ["cooldown"] = 0.78f, ["range"] = 920f, ["penetration"] = 1f, ["projectileCount"] = 1f }
            }
        };
    }
}

internal readonly record struct WeaponSnapshotData(string Id, string Name, int Level, int MaxLevel, string Icon, bool IsPromoted);
internal readonly record struct PassiveSnapshotData(string Id, string Name, int Level, int MaxLevel, string Icon);
