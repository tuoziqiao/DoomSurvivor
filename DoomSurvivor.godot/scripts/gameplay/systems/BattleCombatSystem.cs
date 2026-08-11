using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using DoomSurvivor.Core;

namespace DoomSurvivor.Gameplay;

internal sealed class BattleCombatSystem
{
    private readonly RunLoadout loadout;
    private readonly Random random;
    private readonly List<EnemyRuntime> nearby = new();
    private readonly List<EnemyRuntime> chainTargets = new();
    private float invulnerability;

    public BattleCombatSystem(RunLoadout runLoadout, Random rng)
    {
        loadout = runLoadout;
        random = rng;
    }

    public void BeginStep(float delta)
    {
        invulnerability = Math.Max(0f, invulnerability - delta);
    }

    public void ResolveContactAndAttack(
        float delta,
        BattleSimulator.PlayerRuntime player,
        List<EnemyRuntime> enemies,
        SpatialHashGrid<EnemyRuntime> spatialHash,
        IReadOnlyList<WeaponRuntime> weapons,
        List<FireZoneRuntime> fireZones,
        List<CombatEffectRuntime> effects,
        Action<EnemyRuntime> onEnemyKilled,
        Action<float> onDamageDealt)
    {
        ResolveContactDamage(delta, player, enemies);
        UpdateFireZones(delta, player, enemies, spatialHash, fireZones, effects, onEnemyKilled, onDamageDealt);
        UpdateWeapons(delta, player, enemies, spatialHash, weapons, fireZones, effects, onEnemyKilled, onDamageDealt);
    }

    public void TakeEnvironmentalDamage(BattleSimulator.PlayerRuntime player, float amount)
    {
        TakeDamage(player, amount);
    }

    private void ResolveContactDamage(float delta, BattleSimulator.PlayerRuntime player, List<EnemyRuntime> enemies)
    {
        foreach (var enemy in enemies)
        {
            if (!enemy.Active || enemy.ContactCooldown > 0f) continue;
            if (Vector2.Distance(enemy.Position, player.Position) > enemy.Radius + player.CollisionRadius) continue;
            TakeDamage(player, Math.Max(1f, enemy.Config.ContactDamage));
            enemy.ContactCooldown = Math.Max(0.1f, enemy.Config.AttackCooldown);
            break;
        }
    }

    private void UpdateWeapons(
        float delta,
        BattleSimulator.PlayerRuntime player,
        List<EnemyRuntime> enemies,
        SpatialHashGrid<EnemyRuntime> spatialHash,
        IReadOnlyList<WeaponRuntime> weapons,
        List<FireZoneRuntime> fireZones,
        List<CombatEffectRuntime> effects,
        Action<EnemyRuntime> onEnemyKilled,
        Action<float> onDamageDealt)
    {
        foreach (var weapon in weapons)
        {
            weapon.Cooldown -= delta;
            var rotationSpeed = ConfigEffectReader.Read(weapon.Config.LevelEffects, weapon.Level, "rotationSpeed", 2f);
            weapon.Phase += delta * rotationSpeed;
            if (weapon.Cooldown > 0f) continue;

            var cooldown = ConfigEffectReader.Read(weapon.Config.LevelEffects, weapon.Level, "cooldown", 0.78f);
            cooldown = Math.Max(0.12f, cooldown / Math.Max(0.1f, player.AttackSpeedMultiplier));
            weapon.Cooldown = cooldown;
            weapon.ShotsFired++;

            switch (weapon.Config.Id)
            {
                case "wind_blade":
                    FireWindBlade(player, enemies, spatialHash, weapon, effects, onEnemyKilled, onDamageDealt);
                    break;
                case "rotating_knife":
                    FireRotatingKnife(player, enemies, weapon, effects, onEnemyKilled, onDamageDealt);
                    break;
                case "fubo_qin":
                    FireFuboQin(player, enemies, spatialHash, weapon, effects, onEnemyKilled, onDamageDealt);
                    break;
                case "fire_bottle":
                    FireBottle(player, enemies, spatialHash, weapon, fireZones, effects);
                    break;
                case "lightning_chain":
                    FireLightningChain(player, enemies, spatialHash, weapon, effects, onEnemyKilled, onDamageDealt);
                    break;
                case "drone":
                    FireDrone(player, enemies, spatialHash, weapon, effects, onEnemyKilled, onDamageDealt);
                    break;
                default:
                    FireBasic(player, enemies, spatialHash, weapon, effects, onEnemyKilled, onDamageDealt);
                    break;
            }
        }
    }

    private void FireWindBlade(
        BattleSimulator.PlayerRuntime player,
        List<EnemyRuntime> enemies,
        SpatialHashGrid<EnemyRuntime> spatialHash,
        WeaponRuntime weapon,
        List<CombatEffectRuntime> effects,
        Action<EnemyRuntime> onEnemyKilled,
        Action<float> onDamageDealt)
    {
        var range = ConfigEffectReader.Read(weapon.Config.LevelEffects, weapon.Level, "range", BattleSimulationConstants.AttackRange);
        var count = Math.Max(1, (int)ConfigEffectReader.Read(weapon.Config.LevelEffects, weapon.Level, "projectileCount", 1f));
        var penetration = Math.Max(1, (int)ConfigEffectReader.Read(weapon.Config.LevelEffects, weapon.Level, "penetration", 1f));
        var targets = FindTargets(player.Position, range, enemies, spatialHash).Take(count * penetration).ToList();
        foreach (var target in targets)
        {
            DamageTarget(player, target, weapon, effects, "wind_blade", onEnemyKilled, onDamageDealt);
        }
        if (targets.Count > 0) AddEffect(effects, "wind_blade", player.Position, targets[0].Position, 24f, 0.16f, count);
    }

    private void FireBasic(
        BattleSimulator.PlayerRuntime player,
        List<EnemyRuntime> enemies,
        SpatialHashGrid<EnemyRuntime> spatialHash,
        WeaponRuntime weapon,
        List<CombatEffectRuntime> effects,
        Action<EnemyRuntime> onEnemyKilled,
        Action<float> onDamageDealt)
    {
        var target = FindTargets(player.Position, BattleSimulationConstants.AttackRange, enemies, spatialHash).FirstOrDefault();
        if (target is null) return;
        DamageTarget(player, target, weapon, effects, "basic_auto", onEnemyKilled, onDamageDealt);
        AddEffect(effects, "wind_blade", player.Position, target.Position, 16f, 0.12f, 1);
    }

    private void FireRotatingKnife(
        BattleSimulator.PlayerRuntime player,
        List<EnemyRuntime> enemies,
        WeaponRuntime weapon,
        List<CombatEffectRuntime> effects,
        Action<EnemyRuntime> onEnemyKilled,
        Action<float> onDamageDealt)
    {
        var orbitRadius = ConfigEffectReader.Read(weapon.Config.LevelEffects, weapon.Level, "orbitRadius", 70f);
        var count = Math.Max(1, (int)ConfigEffectReader.Read(weapon.Config.LevelEffects, weapon.Level, "knifeCount", 2f));
        var targets = enemies.Where(value => value.Active && Vector2.Distance(value.Position, player.Position) <= orbitRadius + value.Radius + 12f).Take(count * 2).ToList();
        foreach (var target in targets)
        {
            DamageTarget(player, target, weapon, effects, "rotating_knife", onEnemyKilled, onDamageDealt);
        }
        AddEffect(effects, weapon.IsMaxLevel ? "rotating_knife_gold" : "rotating_knife", player.Position, player.Position, orbitRadius, 0.2f, count);
    }

    private void FireFuboQin(
        BattleSimulator.PlayerRuntime player,
        List<EnemyRuntime> enemies,
        SpatialHashGrid<EnemyRuntime> spatialHash,
        WeaponRuntime weapon,
        List<CombatEffectRuntime> effects,
        Action<EnemyRuntime> onEnemyKilled,
        Action<float> onDamageDealt)
    {
        var radius = 150f + ConfigEffectReader.Read(weapon.Config.LevelEffects, weapon.Level, "ripplePulseSpeed", 2f) * 8f;
        var targets = FindTargets(player.Position, radius, enemies, spatialHash);
        foreach (var target in targets)
        {
            DamageTarget(player, target, weapon, effects, "fubo_qin", onEnemyKilled, onDamageDealt);
        }
        AddEffect(effects, weapon.IsMaxLevel ? "fubo_qin_gold" : "fubo_qin", player.Position, player.Position, radius, 0.34f, targets.Count());
    }

    private void FireBottle(
        BattleSimulator.PlayerRuntime player,
        List<EnemyRuntime> enemies,
        SpatialHashGrid<EnemyRuntime> spatialHash,
        WeaponRuntime weapon,
        List<FireZoneRuntime> fireZones,
        List<CombatEffectRuntime> effects)
    {
        var target = FindTargets(player.Position, BattleSimulationConstants.AttackRange, enemies, spatialHash).FirstOrDefault();
        if (target is null) return;
        var radius = ConfigEffectReader.Read(weapon.Config.LevelEffects, weapon.Level, "zoneRadius", 60f);
        var duration = ConfigEffectReader.Read(weapon.Config.LevelEffects, weapon.Level, "zoneDuration", 3f);
        var tick = ConfigEffectReader.Read(weapon.Config.LevelEffects, weapon.Level, "tickInterval", 0.5f);
        fireZones.Add(new FireZoneRuntime
        {
            Position = target.Position,
            Radius = radius,
            Remaining = duration,
            TickTimer = 0f,
            Damage = ConfigEffectReader.Read(weapon.Config.LevelEffects, weapon.Level, "damage", 6f)
        });
        AddEffect(effects, "fire_bottle", player.Position, target.Position, 18f, 0.24f, 1);
        AddEffect(effects, "fire_zone", target.Position, target.Position, radius, duration, Math.Max(1, (int)(1f / Math.Max(0.1f, tick))));
    }

    private void FireLightningChain(
        BattleSimulator.PlayerRuntime player,
        List<EnemyRuntime> enemies,
        SpatialHashGrid<EnemyRuntime> spatialHash,
        WeaponRuntime weapon,
        List<CombatEffectRuntime> effects,
        Action<EnemyRuntime> onEnemyKilled,
        Action<float> onDamageDealt)
    {
        var first = FindTargets(player.Position, BattleSimulationConstants.AttackRange, enemies, spatialHash).FirstOrDefault();
        if (first is null) return;
        chainTargets.Clear();
        chainTargets.Add(first);
        var count = Math.Max(1, (int)ConfigEffectReader.Read(weapon.Config.LevelEffects, weapon.Level, "chainCount", 2f));
        var range = ConfigEffectReader.Read(weapon.Config.LevelEffects, weapon.Level, "chainRange", 140f);
        while (chainTargets.Count < count)
        {
            var previous = chainTargets[^1];
            var next = enemies
                .Where(value => value.Active && !chainTargets.Contains(value) && Vector2.Distance(value.Position, previous.Position) <= range)
                .OrderBy(value => Vector2.DistanceSquared(value.Position, previous.Position))
                .FirstOrDefault();
            if (next is null) break;
            chainTargets.Add(next);
        }

        var origin = player.Position;
        foreach (var target in chainTargets)
        {
            DamageTarget(player, target, weapon, effects, "lightning_chain", onEnemyKilled, onDamageDealt);
            AddEffect(effects, "lightning_chain", origin, target.Position, 4f, 0.18f, chainTargets.Count);
            origin = target.Position;
        }
    }

    private void FireDrone(
        BattleSimulator.PlayerRuntime player,
        List<EnemyRuntime> enemies,
        SpatialHashGrid<EnemyRuntime> spatialHash,
        WeaponRuntime weapon,
        List<CombatEffectRuntime> effects,
        Action<EnemyRuntime> onEnemyKilled,
        Action<float> onDamageDealt)
    {
        var count = Math.Max(1, (int)ConfigEffectReader.Read(weapon.Config.LevelEffects, weapon.Level, "droneCount", 1f));
        var targets = FindTargets(player.Position, BattleSimulationConstants.AttackRange, enemies, spatialHash).Take(count).ToList();
        foreach (var target in targets)
        {
            DamageTarget(player, target, weapon, effects, "drone", onEnemyKilled, onDamageDealt);
            AddEffect(effects, "drone_bolt", player.Position, target.Position, 7f, 0.16f, count);
        }
        AddEffect(effects, "drone", player.Position, player.Position, ConfigEffectReader.Read(weapon.Config.LevelEffects, weapon.Level, "orbitRadius", 106f), 0.2f, count);
    }

    private void UpdateFireZones(
        float delta,
        BattleSimulator.PlayerRuntime player,
        List<EnemyRuntime> enemies,
        SpatialHashGrid<EnemyRuntime> spatialHash,
        List<FireZoneRuntime> fireZones,
        List<CombatEffectRuntime> effects,
        Action<EnemyRuntime> onEnemyKilled,
        Action<float> onDamageDealt)
    {
        for (var index = fireZones.Count - 1; index >= 0; index--)
        {
            var zone = fireZones[index];
            zone.Remaining -= delta;
            zone.TickTimer -= delta;
            if (zone.TickTimer <= 0f)
            {
                zone.TickTimer = 0.5f;
                foreach (var target in FindTargets(zone.Position, zone.Radius, enemies, spatialHash))
                {
                    var damage = DamageFormula.Calculate(zone.Damage, player.DamageMultiplier, 1f, 1f, 1f, 0f);
                    target.Hp -= damage;
                    onDamageDealt(damage);
                    if (target.Hp <= 0f) onEnemyKilled(target);
                }
                AddEffect(effects, "fire_zone", zone.Position, zone.Position, zone.Radius, 0.12f, 1);
            }
            if (zone.Remaining <= 0f) fireZones.RemoveAt(index);
        }
    }

    private void DamageTarget(
        BattleSimulator.PlayerRuntime player,
        EnemyRuntime target,
        WeaponRuntime weapon,
        List<CombatEffectRuntime> effects,
        string effectKind,
        Action<EnemyRuntime> onEnemyKilled,
        Action<float> onDamageDealt)
    {
        if (!target.Active) return;
        var baseDamage = ConfigEffectReader.Read(weapon.Config.LevelEffects, weapon.Level, "damage", 28f);
        var critical = random.NextDouble() < player.CritRate ? player.CritDamage : 1f;
        var variance = loadout.Balance.Combat.DamageVarianceMin + (float)random.NextDouble() *
            (loadout.Balance.Combat.DamageVarianceMax - loadout.Balance.Combat.DamageVarianceMin);
        var damage = DamageFormula.Calculate(baseDamage, player.DamageMultiplier, 1f, critical, variance, 0f);
        target.Hp -= damage;
        onDamageDealt(damage);
        AddEffect(effects, effectKind, player.Position, target.Position, target.Radius, 0.12f, critical > 1f ? 2 : 1);
        if (target.Hp <= 0f) onEnemyKilled(target);
    }

    private IEnumerable<EnemyRuntime> FindTargets(Vector2 origin, float radius, List<EnemyRuntime> enemies, SpatialHashGrid<EnemyRuntime> spatialHash)
    {
        spatialHash.Query(origin.X, origin.Y, radius, nearby);
        if (nearby.Count == 0) nearby.AddRange(enemies);
        return nearby
            .Where(value => value.Active && Vector2.Distance(value.Position, origin) <= radius + value.Radius)
            .OrderBy(value => Vector2.DistanceSquared(value.Position, origin))
            .ToList();
    }

    private void TakeDamage(BattleSimulator.PlayerRuntime player, float amount)
    {
        if (invulnerability > 0f || player.Hp <= 0f) return;
        var reduced = Math.Max(1f, amount - player.Armor);
        player.Hp -= reduced;
        invulnerability = Math.Max(0.1f, loadout.Balance.Combat.InvincibilityDuration);
    }

    private static void AddEffect(List<CombatEffectRuntime> effects, string kind, Vector2 position, Vector2 target, float radius, float lifetime, int strength)
    {
        if (effects.Count >= 96) effects.RemoveAt(0);
        effects.Add(new CombatEffectRuntime
        {
            Kind = kind,
            Position = position,
            Target = target,
            Radius = radius,
            Remaining = lifetime,
            Lifetime = lifetime,
            Strength = strength
        });
    }
}
