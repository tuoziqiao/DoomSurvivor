using System;
using System.Collections.Generic;

namespace DoomSurvivor.Core;

public static class DamageFormula
{
    public static float Calculate(float baseDamage, float playerMultiplier, float skillMultiplier, float criticalMultiplier, float variance, float defenseReduction)
    {
        return Math.Max(1f, baseDamage * playerMultiplier * skillMultiplier * criticalMultiplier * variance - defenseReduction);
    }
}

public static class PlayerLevelMaxHp
{
    public static float ScaledBaseMaxHp(float characterBaseMaxHp, int level, float growthPercentPerLevel)
    {
        var growth = Math.Clamp(growthPercentPerLevel, 0f, 1f);
        var safeLevel = Math.Max(1, level);
        return characterBaseMaxHp * (1f + (safeLevel - 1) * growth);
    }

    public static float ApplyHpAfterMaxIncrease(float oldHp, float oldMax, float newMax)
    {
        var delta = Math.Max(0f, newMax - oldMax);
        return Math.Clamp(oldHp + delta, 0f, newMax);
    }
}

public sealed class ExperienceProgress
{
    private readonly IReadOnlyList<int> thresholds;

    public ExperienceProgress(IReadOnlyList<int> thresholds) => this.thresholds = thresholds;
    public int Level { get; private set; } = 1;
    public int Current { get; private set; }
    public int Total { get; private set; }
    public int PendingLevelUps { get; private set; }

    public void Add(int amount)
    {
        if (amount <= 0) return;
        Current += amount;
        Total += amount;
        while (Current >= RequiredForLevel(Level))
        {
            Current -= RequiredForLevel(Level);
            Level++;
            PendingLevelUps++;
        }
    }

    public bool ConsumeLevelUp()
    {
        if (PendingLevelUps <= 0) return false;
        PendingLevelUps--;
        return true;
    }

    public int RequiredForLevel(int level)
    {
        if (thresholds is null || thresholds.Count == 0) return 10 + level * 5;
        return thresholds[Math.Min(Math.Max(0, level - 1), thresholds.Count - 1)];
    }
}

public readonly struct GridPoint : IEquatable<GridPoint>
{
    public GridPoint(int x, int y) { X = x; Y = y; }
    public int X { get; }
    public int Y { get; }
    public bool Equals(GridPoint other) => X == other.X && Y == other.Y;
    public override bool Equals(object? obj) => obj is GridPoint other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(X, Y);
}

public sealed class SpatialHashGrid<T>
{
    private readonly float cellSize;
    private readonly Dictionary<GridPoint, List<T>> cells = new();

    public SpatialHashGrid(float cellSize) => this.cellSize = Math.Max(0.01f, cellSize);
    public void Clear() => cells.Clear();

    public void Insert(float x, float y, T value)
    {
        var point = ToCell(x, y);
        if (!cells.TryGetValue(point, out var bucket))
        {
            bucket = new List<T>();
            cells.Add(point, bucket);
        }
        bucket.Add(value);
    }

    public void Query(float x, float y, float radius, List<T> results)
    {
        results.Clear();
        var min = ToCell(x - radius, y - radius);
        var max = ToCell(x + radius, y + radius);
        for (var cy = min.Y; cy <= max.Y; cy++)
        for (var cx = min.X; cx <= max.X; cx++)
        {
            if (cells.TryGetValue(new GridPoint(cx, cy), out var bucket)) results.AddRange(bucket);
        }
    }

    private GridPoint ToCell(float x, float y) => new((int)Math.Floor(x / cellSize), (int)Math.Floor(y / cellSize));
}

public static class SaveMigration
{
    public const int CurrentVersion = 4;

    public static SaveData Migrate(SaveData? value)
    {
        value ??= new SaveData();
        value.UnlockedCharacters ??= new List<string>();
        value.UnlockedSkins ??= new List<string>();
        value.SelectedSkinByCharacter ??= new Dictionary<string, string>();
        value.CharacterOrder ??= new List<string>();
        AddUnique(value.UnlockedCharacters, "lin_xian");
        AddUnique(value.UnlockedSkins, "lin_xian_wasteland");
        if (string.IsNullOrWhiteSpace(value.SelectedCharacterId)) value.SelectedCharacterId = "lin_xian";
        if (!value.SelectedSkinByCharacter.ContainsKey("lin_xian")) value.SelectedSkinByCharacter["lin_xian"] = "lin_xian_wasteland";
        if (value.SaveVersion < CurrentVersion || value.CharacterOrder.Count == 0)
        {
            value.CharacterOrder = new List<string> { "gu_chen", "ye_qing", "lin_xian", "su_lan", "han_duo", "mu_xue", "lu_chuan" };
        }
        value.SaveVersion = CurrentVersion;
        return value;
    }

    private static void AddUnique(List<string> values, string value)
    {
        if (!values.Contains(value)) values.Add(value);
    }
}
