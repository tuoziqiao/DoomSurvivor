using System;
using System.Collections.Generic;
using System.Numerics;
using DoomSurvivor.Core;

namespace DoomSurvivor.Gameplay;

internal sealed class EnemyRuntime
{
    public EnemyConfig Config = new();
    public Vector2 Position;
    public float Hp;
    public float MaxHp;
    public float Radius;
    public bool Active;
    public bool IsBoss;
    public bool IsElite;
    public float ContactCooldown;
    public float DashTimer;
}

internal sealed class CrystalRuntime
{
    public Vector2 Position;
    public int Value;
    public bool Active = true;
    public string Kind = "small";
}

internal sealed class WeaponRuntime
{
    public WeaponConfig Config = new();
    public int Level;
    public float Cooldown;
    public float Phase;
    public int ShotsFired;

    public bool IsMaxLevel => Level >= Math.Max(1, Config.MaxLevel);
}

internal sealed class PassiveRuntime
{
    public SkillConfig Config = new();
    public int Level;

    public bool IsMaxLevel => Level >= Math.Max(1, Config.MaxLevel);
}

internal sealed class UpgradeChoiceRuntime
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

internal sealed class FireZoneRuntime
{
    public Vector2 Position;
    public float Radius;
    public float Remaining;
    public float TickTimer;
    public float Damage;
    public string WeaponId = "fire_bottle";
}

internal enum MapEventType
{
    Crate,
    HiddenCrate,
    Altar,
    PoisonFog,
    HealingChicken
}

internal sealed class MapEventRuntime
{
    public string Id = string.Empty;
    public MapEventType Type;
    public Vector2 Position;
    public float Radius;
    public bool Active = true;
    public float TickTimer;
}

internal sealed class CombatEffectRuntime
{
    public string Kind = string.Empty;
    public Vector2 Position;
    public Vector2 Target;
    public float Radius;
    public float Remaining;
    public float Lifetime;
    public int Strength;
}

internal sealed class BattleBuffRuntime
{
    public float Remaining;
    public float DamageBonus;
    public float MoveSpeedMultiplier;
    public float PickupRadiusMultiplier;
}

internal static class ConfigEffectReader
{
    public static float Read(IReadOnlyDictionary<string, Dictionary<string, float>>? levels, int level, string key, float fallback = 0f)
    {
        if (levels is null || levels.Count == 0) return fallback;
        var safeLevel = Math.Max(1, level);
        if (levels.TryGetValue(safeLevel.ToString(), out var values) && values is not null && values.TryGetValue(key, out var value)) return value;
        if (levels.TryGetValue("1", out values) && values is not null && values.TryGetValue(key, out value)) return value;
        return fallback;
    }

    public static bool Has(IReadOnlyDictionary<string, Dictionary<string, float>>? levels, string key)
    {
        if (levels is null) return false;
        foreach (var values in levels.Values)
        {
            if (values is not null && values.ContainsKey(key)) return true;
        }
        return false;
    }
}
