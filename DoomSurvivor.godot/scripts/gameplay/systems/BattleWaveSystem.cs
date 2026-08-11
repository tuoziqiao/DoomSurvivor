using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using DoomSurvivor.Core;

namespace DoomSurvivor.Gameplay;

internal sealed class BattleWaveSystem
{
    private const float ClearInterval = 2.2f;
    private readonly RunLoadout loadout;
    private readonly Random random;
    private readonly Dictionary<string, EnemyConfig> enemyConfigs;
    private float spawnTimer;
    private float clearTimer;
    private float bossWarningTimer;
    private int spawnedThisWave;
    private int waveTarget;
    private bool bossWarningStarted;

    public BattleWaveSystem(RunLoadout runLoadout, Random rng)
    {
        loadout = runLoadout;
        random = rng;
        enemyConfigs = loadout.Enemies
            .Where(value => value is not null && !string.IsNullOrWhiteSpace(value.Id))
            .GroupBy(value => value.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        WaveCount = runLoadout.Mode == GameMode.QuickTest
            ? Math.Clamp(runLoadout.Settings.WaveCount, 3, 5)
            : Math.Clamp(runLoadout.Settings.WaveCount, 1, 30);
    }

    public int CurrentWave { get; private set; }
    public int WaveCount { get; }
    public bool BossSpawned { get; private set; }
    public string State { get; private set; } = "准备";
    public float ClearIntervalSeconds => ClearInterval;

    public void Update(
        float delta,
        float elapsed,
        IReadOnlyList<EnemyRuntime> enemies,
        int enemyLimit,
        Action<EnemyConfig, bool, bool, Vector2> spawn)
    {
        if (CurrentWave == 0) BeginWave(1);

        var duration = loadout.Mode == GameMode.QuickTest ? loadout.Stage.QuickTestDuration : loadout.Stage.NormalModeDuration;
        var waveDuration = duration > 0f ? duration / Math.Max(1, WaveCount) : 60f;
        var scheduledWave = Math.Clamp((int)MathF.Floor(elapsed / Math.Max(1f, waveDuration)) + 1, 1, WaveCount);
        var activeCount = enemies.Count(value => value.Active);

        if (scheduledWave > CurrentWave && (activeCount == 0 || clearTimer >= ClearInterval || spawnedThisWave >= waveTarget))
        {
            BeginWave(scheduledWave);
        }

        spawnTimer = Math.Max(0f, spawnTimer - delta);
        if (spawnedThisWave < waveTarget && activeCount < enemyLimit && spawnTimer <= 0f)
        {
            var entry = SelectEntry(elapsed, enemies);
            if (entry is not null && enemyConfigs.TryGetValue(entry.EnemyId, out var config))
            {
                var rate = Math.Max(0.15f, entry.SpawnRate);
                spawnTimer = Math.Clamp(1f / rate, 0.12f, 2f);
                var isElite = entry.IsElite || (CurrentWave >= loadout.Settings.EliteStartWave && string.Equals(config.Type, "elite", StringComparison.OrdinalIgnoreCase));
                spawn(config, isElite, false, SpawnPosition(enemies));
                spawnedThisWave++;
                State = isElite ? "精英来袭" : $"第 {CurrentWave} 波";
            }
            else
            {
                spawnedThisWave = waveTarget;
            }
        }

        if (spawnedThisWave >= waveTarget && activeCount == 0)
        {
            clearTimer += delta;
            State = $"清场 {Math.Max(0f, ClearInterval - clearTimer):0.0}s";
        }
        else
        {
            clearTimer = 0f;
        }

        var bossWarningLead = Math.Max(6f, waveDuration * 0.5f);
        if (loadout.Settings.BossCount > 0 && !BossSpawned && CurrentWave >= WaveCount && duration > 0f &&
            elapsed >= Math.Max(0f, duration - bossWarningLead))
        {
            if (!bossWarningStarted)
            {
                bossWarningStarted = true;
                bossWarningTimer = 0f;
                State = "BOSS WARNING";
            }
            else
            {
                bossWarningTimer += delta;
                if (bossWarningTimer >= ClearInterval) TrySpawnBoss(spawn, SpawnPosition(enemies));
            }
        }
    }

    public bool TrySpawnBoss(Action<EnemyConfig, bool, bool, Vector2> spawn, Vector2 position)
    {
        if (BossSpawned) return false;
        var boss = enemyConfigs.Values.FirstOrDefault(value => string.Equals(value.Type, "boss", StringComparison.OrdinalIgnoreCase));
        if (boss is null) return false;
        BossSpawned = true;
        State = "BOSS INCOMING";
        spawn(boss, false, true, position);
        return true;
    }

    private void BeginWave(int wave)
    {
        CurrentWave = Math.Clamp(wave, 1, WaveCount);
        var baseCount = Math.Max(1, loadout.Settings.FirstWaveMobCount);
        var multiplier = Math.Max(0.1f, loadout.Settings.WaveMobCountMultiplier);
        waveTarget = Math.Clamp((int)MathF.Round(baseCount * MathF.Pow(multiplier, CurrentWave - 1)), 1, 240);
        spawnedThisWave = 0;
        clearTimer = 0f;
        spawnTimer = 0f;
        State = $"第 {CurrentWave} 波";
    }

    private SpawnTimelineEntry? SelectEntry(float elapsed, IReadOnlyList<EnemyRuntime> enemies)
    {
        var entries = loadout.Stage.SpawnTimeline ?? new List<SpawnTimelineEntry>();
        var eligible = entries
            .Where(value => value is not null && !value.IsBoss && value.Time <= elapsed && enemyConfigs.ContainsKey(value.EnemyId))
            .OrderByDescending(value => value.Time)
            .ToList();
        if (eligible.Count == 0)
        {
            var fallback = enemyConfigs.Values.FirstOrDefault(value => !string.Equals(value.Type, "boss", StringComparison.OrdinalIgnoreCase));
            return fallback is null ? null : new SpawnTimelineEntry { EnemyId = fallback.Id, SpawnRate = 1f };
        }

        var latestTime = eligible[0].Time;
        var current = eligible.Where(value => Math.Abs(value.Time - latestTime) < 0.01f).ToList();
        var available = current.Where(entry =>
        {
            var max = entry.MaxConcurrent;
            if (max <= 0) return true;
            var count = enemies.Count(value => value.Active && string.Equals(value.Config.Id, entry.EnemyId, StringComparison.Ordinal));
            return count < max;
        }).ToList();
        if (available.Count == 0) available = current;
        var totalWeight = available.Sum(value => Math.Max(0.1f, value.WeightMultiplier));
        var roll = (float)random.NextDouble() * totalWeight;
        foreach (var entry in available)
        {
            roll -= Math.Max(0.1f, entry.WeightMultiplier);
            if (roll <= 0f) return entry;
        }
        return available[^1];
    }

    private Vector2 SpawnPosition(IReadOnlyList<EnemyRuntime> enemies)
    {
        var angle = (float)(random.NextDouble() * Math.PI * 2d);
        var distance = Math.Max(460f, loadout.Balance.Spawn.MinSpawnDistanceFromPlayer);
        // The actual player-relative offset is corrected by BattleSimulator when
        // this seam is invoked; returning a stable offset keeps the wave system pure.
        return new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * distance;
    }
}
