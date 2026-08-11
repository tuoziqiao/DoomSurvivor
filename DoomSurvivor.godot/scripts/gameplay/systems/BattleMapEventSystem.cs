using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using DoomSurvivor.Core;

namespace DoomSurvivor.Gameplay;

internal sealed class BattleMapEventSystem
{
    private readonly RunLoadout loadout;
    private readonly Random random;
    private readonly List<BattleBuffRuntime> buffs = new();

    public BattleMapEventSystem(RunLoadout runLoadout, Random rng)
    {
        loadout = runLoadout;
        random = rng;
    }

    public IReadOnlyList<BattleBuffRuntime> ActiveBuffs => buffs;

    public void Initialize(List<MapEventRuntime> events)
    {
        var config = loadout.Stage.MapEvents ?? new MapEventsConfig();
        AddEvents(events, MapEventType.Crate, Math.Max(0, loadout.Settings.CrateCount), config.CrateInteractRadius, "crate");
        AddEvents(events, MapEventType.HiddenCrate, Math.Max(0, loadout.Settings.HiddenCrateCount), config.CrateInteractRadius, "hidden_crate");
        AddEvents(events, MapEventType.Altar, Math.Max(0, loadout.Settings.AltarCount), config.AltarInteractRadius, "altar");
        AddEvents(events, MapEventType.PoisonFog, Math.Max(0, loadout.Settings.PoisonFogCount), RandomRange(config.PoisonFogRadiusMin, config.PoisonFogRadiusMax), "poison_fog");
        AddEvents(events, MapEventType.HealingChicken, Math.Max(0, loadout.Settings.HealingChickenCount), config.HealingChickenInteractRadius, "healing_chicken");
    }

    public void Update(
        float delta,
        BattleSimulator.PlayerRuntime player,
        List<MapEventRuntime> events,
        ExperienceProgress experience,
        Action<int> addExperience,
        Action<float> takeDamage,
        Action<int> spawnPoisonFog,
        Action spawnBoss,
        Action purgeEnemies,
        Action recalculatePlayer)
    {
        UpdateBuffs(delta, player, recalculatePlayer);
        var config = loadout.Stage.MapEvents ?? new MapEventsConfig();
        foreach (var mapEvent in events)
        {
            if (!mapEvent.Active) continue;
            var distance = Vector2.Distance(player.Position, mapEvent.Position);
            if (mapEvent.Type == MapEventType.PoisonFog)
            {
                mapEvent.TickTimer -= delta;
                if (distance <= mapEvent.Radius && mapEvent.TickTimer <= 0f)
                {
                    mapEvent.TickTimer = Math.Max(0.1f, config.PoisonFogTickInterval);
                    takeDamage(Math.Max(1f, config.PoisonFogDps));
                }
                continue;
            }

            if (distance > mapEvent.Radius) continue;
            mapEvent.Active = false;
            switch (mapEvent.Type)
            {
                case MapEventType.Crate:
                    ApplyCrateEffect(config.CrateEffects, player, experience, addExperience, spawnPoisonFog, spawnBoss, recalculatePlayer);
                    break;
                case MapEventType.HiddenCrate:
                    ApplyCrateEffect(config.HiddenCrateEffects, player, experience, addExperience, spawnPoisonFog, spawnBoss, recalculatePlayer, hidden: true);
                    break;
                case MapEventType.Altar:
                    ApplyAltarEffect(config.AltarEffects, player, spawnBoss, purgeEnemies, recalculatePlayer);
                    break;
                case MapEventType.HealingChicken:
                    player.Hp = Math.Min(player.MaxHp, player.Hp + Math.Max(1f, player.MaxHp * 0.25f));
                    break;
            }
        }
    }

    public void AddPoisonFog(List<MapEventRuntime> events, int count)
    {
        var config = loadout.Stage.MapEvents ?? new MapEventsConfig();
        AddEvents(events, MapEventType.PoisonFog, Math.Max(0, count), RandomRange(config.PoisonFogRadiusMin, config.PoisonFogRadiusMax), "poison_fog");
    }

    private void ApplyCrateEffect(
        IReadOnlyList<CrateEffectConfig>? effects,
        BattleSimulator.PlayerRuntime player,
        ExperienceProgress experience,
        Action<int> addExperience,
        Action<int> spawnPoisonFog,
        Action spawnBoss,
        Action recalculatePlayer,
        bool hidden = false)
    {
        var effect = PickWeighted(effects);
        if (effect is null)
        {
            addExperience(Math.Max(1, RandomInt(12, 30)));
            return;
        }

        if (effect.FogCount > 0) spawnPoisonFog(effect.FogCount);
        if (effect.Id.Contains("boss", StringComparison.OrdinalIgnoreCase)) spawnBoss();
        if (effect.Id.Contains("xp", StringComparison.OrdinalIgnoreCase)) addExperience(RandomInt(12, 30));
        if (effect.LevelUps > 0)
        {
            var amount = 0;
            for (var index = 0; index < effect.LevelUps; index++) amount += experience.RequiredForLevel(experience.Level);
            addExperience(amount);
        }
        if (effect.MaxHpBonus > 0f)
        {
            player.FlatMaxHpBonus += effect.MaxHpBonus;
            player.MaxHp += effect.MaxHpBonus;
            player.Hp = Math.Min(player.MaxHp, player.Hp + effect.MaxHpBonus);
        }
        if (effect.MoveSpeedBonus > 0f || effect.PickupRadiusMul > 0f)
        {
            buffs.Add(new BattleBuffRuntime
            {
                Remaining = effect.Duration > 0f ? effect.Duration : (hidden ? 60f : 30f),
                MoveSpeedMultiplier = effect.MoveSpeedBonus,
                PickupRadiusMultiplier = effect.PickupRadiusMul > 0f ? effect.PickupRadiusMul : 0f
            });
            recalculatePlayer();
        }
    }

    private void ApplyAltarEffect(
        IReadOnlyList<AltarEffectConfig>? effects,
        BattleSimulator.PlayerRuntime player,
        Action spawnBoss,
        Action purgeEnemies,
        Action recalculatePlayer)
    {
        var effect = PickWeighted(effects);
        if (effect is null) return;
        player.Hp = Math.Max(1f, player.Hp - Math.Max(0f, effect.HpCost));
        if (effect.KeepBoss) spawnBoss();
        if (string.Equals(effect.Id, "purge", StringComparison.OrdinalIgnoreCase)) purgeEnemies();
        if (effect.DamageBonus > 0f || effect.PickupRadiusMul > 0f)
        {
            buffs.Add(new BattleBuffRuntime
            {
                Remaining = effect.Duration > 0f ? effect.Duration : 30f,
                DamageBonus = effect.DamageBonus,
                PickupRadiusMultiplier = effect.PickupRadiusMul > 0f ? effect.PickupRadiusMul : 0f
            });
            recalculatePlayer();
        }
    }

    private void UpdateBuffs(float delta, BattleSimulator.PlayerRuntime player, Action recalculatePlayer)
    {
        if (buffs.Count == 0) return;
        foreach (var buff in buffs) buff.Remaining -= delta;
        buffs.RemoveAll(value => value.Remaining <= 0f);
        var damage = buffs.Sum(value => value.DamageBonus);
        var move = buffs.Sum(value => value.MoveSpeedMultiplier);
        var pickup = buffs.Aggregate(1f, (current, value) => current * Math.Max(1f, value.PickupRadiusMultiplier <= 0f ? 1f : value.PickupRadiusMultiplier));
        player.FlatDamageBonus = damage;
        player.FlatMoveSpeedBonus = player.BaseMoveSpeed * move;
        player.PickupRadiusMultiplier = pickup;
        recalculatePlayer();
    }

    private void AddEvents(List<MapEventRuntime> events, MapEventType type, int count, float radius, string id)
    {
        if (count <= 0) return;
        var safeRadius = type == MapEventType.PoisonFog ? Math.Max(24f, radius) : Math.Max(12f, radius);
        for (var index = 0; index < count; index++)
        {
            events.Add(new MapEventRuntime
            {
                Id = $"{id}_{index + 1}",
                Type = type,
                Position = RandomPosition(),
                Radius = safeRadius,
                TickTimer = 0f
            });
        }
    }

    private Vector2 RandomPosition()
    {
        var margin = 300f;
        return new Vector2(
            RandomRange(margin, Math.Max(margin, loadout.Stage.MapWidth - margin)),
            RandomRange(margin, Math.Max(margin, loadout.Stage.MapHeight - margin)));
    }

    private T? PickWeighted<T>(IReadOnlyList<T>? values) where T : class
    {
        if (values is null || values.Count == 0) return null;
        var weighted = values.Where(value => value is not null).ToList();
        var total = weighted.Sum(value => value switch
        {
            CrateEffectConfig crate => Math.Max(0.1f, crate.Weight),
            AltarEffectConfig altar => Math.Max(0.1f, altar.Weight),
            _ => 1f
        });
        var roll = (float)random.NextDouble() * total;
        foreach (var value in weighted)
        {
            roll -= value switch
            {
                CrateEffectConfig crate => Math.Max(0.1f, crate.Weight),
                AltarEffectConfig altar => Math.Max(0.1f, altar.Weight),
                _ => 1f
            };
            if (roll <= 0f) return value;
        }
        return weighted[^1];
    }

    private int RandomInt(int min, int maxInclusive) => random.Next(Math.Min(min, maxInclusive), Math.Max(min, maxInclusive) + 1);
    private float RandomRange(float min, float max) => min + (float)random.NextDouble() * Math.Max(0f, max - min);
}
