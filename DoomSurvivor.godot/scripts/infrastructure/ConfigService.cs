using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using DoomSurvivor.Core;
using Godot;

namespace DoomSurvivor.Infrastructure;

public interface IRemoteConfigSource
{
    Task<string?> FetchBundleJsonAsync();
}

public interface IBuiltinConfigSource
{
    Task<string?> ReadTextAsync(string fileName);
}

public sealed class GodotBuiltinConfigSource : IBuiltinConfigSource
{
    public Task<string?> ReadTextAsync(string fileName)
    {
        var path = $"res://resources/config/{fileName}";
        if (!Godot.FileAccess.FileExists(path)) return Task.FromResult<string?>(null);
        return Task.FromResult<string?>(Godot.FileAccess.GetFileAsString(path));
    }
}

public sealed class NullRemoteConfigSource : IRemoteConfigSource
{
    public Task<string?> FetchBundleJsonAsync() => Task.FromResult<string?>(null);
}

public sealed class ConfigService : IConfigService
{
    private static readonly string[] SectionFiles =
    {
        "characters.json", "skins.json", "enemies.json", "weapons.json", "skills.json", "stages.json", "balance.json"
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        IncludeFields = true,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    private readonly IRemoteConfigSource? remote;
    private readonly IBuiltinConfigSource builtinSource;
    private readonly string cachePath;
    private readonly Action<string> warningSink;

    public ConfigService(
        bool enableRemote = false,
        IRemoteConfigSource? remoteSource = null,
        string? cachePathOverride = null,
        IBuiltinConfigSource? builtinSource = null,
        Action<string>? warningSink = null)
    {
        remote = enableRemote ? remoteSource ?? new NullRemoteConfigSource() : null;
        cachePath = string.IsNullOrWhiteSpace(cachePathOverride)
            ? Path.Combine(OS.GetUserDataDir(), "config-cache.json")
            : cachePathOverride;
        this.builtinSource = builtinSource ?? new GodotBuiltinConfigSource();
        this.warningSink = warningSink ?? (message => GD.PushWarning(message));
    }

    public ConfigLoadSource Source { get; private set; } = ConfigLoadSource.Builtin;

    public async Task<GameConfigBundle> LoadAsync()
    {
        var builtin = await LoadBuiltinAsync();
        Source = ConfigLoadSource.Builtin;
        if (remote is null) return builtin;

        try
        {
            var remoteJson = await remote.FetchBundleJsonAsync();
            if (!string.IsNullOrWhiteSpace(remoteJson))
            {
                var bundle = Deserialize<GameConfigBundle>(remoteJson);
                Validate(bundle);
                await WriteCacheAsync(bundle);
                Source = ConfigLoadSource.Remote;
                return bundle;
            }
        }
        catch (Exception exception)
        {
            warningSink($"[ConfigService] Remote config unavailable: {exception.Message}");
        }

        try
        {
            var cached = await ReadCacheAsync();
            if (cached is not null)
            {
                Validate(cached);
                Source = ConfigLoadSource.Cache;
                return cached;
            }
        }
        catch (Exception exception)
        {
            warningSink($"[ConfigService] Cached config invalid: {exception.Message}");
            PreserveCorruptCache();
        }

        return builtin;
    }

    public static void Validate(GameConfigBundle? bundle)
    {
        if (bundle is null) throw new InvalidDataException("Config bundle is null.");
        if (bundle.Characters?.Characters is null || bundle.Characters.Characters.Count == 0) throw new InvalidDataException("No characters configured.");
        if (bundle.Skins?.Skins is null || bundle.Skins.Skins.Count == 0) throw new InvalidDataException("No skins configured.");
        if (bundle.Enemies?.Enemies is null || bundle.Enemies.Enemies.Count == 0) throw new InvalidDataException("No enemies configured.");
        if (bundle.Stages?.Stages is null || bundle.Stages.Stages.Count == 0) throw new InvalidDataException("No stages configured.");

        var enemyIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var enemy in bundle.Enemies.Enemies)
        {
            if (string.IsNullOrWhiteSpace(enemy.Id) || !enemyIds.Add(enemy.Id)) throw new InvalidDataException($"Duplicate or empty enemy id: {enemy.Id}");
        }

        var characterIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var character in bundle.Characters.Characters)
        {
            if (string.IsNullOrWhiteSpace(character.Id) || !characterIds.Add(character.Id)) throw new InvalidDataException($"Duplicate or empty character id: {character.Id}");
        }

        var skinIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var skin in bundle.Skins.Skins)
        {
            if (string.IsNullOrWhiteSpace(skin.Id) || !skinIds.Add(skin.Id)) throw new InvalidDataException($"Duplicate or empty skin id: {skin.Id}");
            if (!characterIds.Contains(skin.CharacterId)) throw new InvalidDataException($"Skin references unknown character: {skin.CharacterId}");
        }

        foreach (var stage in bundle.Stages.Stages)
        {
            if (string.IsNullOrWhiteSpace(stage.Id)) throw new InvalidDataException("Stage id is empty.");
            foreach (var entry in stage.SpawnTimeline)
            {
                if (!enemyIds.Contains(entry.EnemyId)) throw new InvalidDataException($"Stage references unknown enemy: {entry.EnemyId}");
            }
        }
    }

    private async Task<GameConfigBundle> LoadBuiltinAsync()
    {
        var json = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in SectionFiles)
        {
            var text = await builtinSource.ReadTextAsync(file);
            if (string.IsNullOrWhiteSpace(text)) throw new FileNotFoundException($"Builtin config file not found or empty: {file}");
            json[file] = text;
        }

        var characters = Deserialize<CharactersConfig>(json["characters.json"]);
        var bundle = new GameConfigBundle
        {
            Version = string.IsNullOrWhiteSpace(characters.Version) ? "builtin" : characters.Version,
            Characters = characters,
            Skins = Deserialize<SkinsConfig>(json["skins.json"]),
            Enemies = Deserialize<EnemiesConfig>(json["enemies.json"]),
            Weapons = Deserialize<WeaponsConfig>(json["weapons.json"]),
            Skills = Deserialize<SkillsConfig>(json["skills.json"]),
            Stages = Deserialize<StagesConfig>(json["stages.json"]),
            Balance = Deserialize<BalanceConfig>(json["balance.json"])
        };
        Validate(bundle);
        return bundle;
    }

    private static T Deserialize<T>(string json) where T : class
    {
        return JsonSerializer.Deserialize<T>(json, JsonOptions) ?? throw new InvalidDataException($"Unable to deserialize {typeof(T).Name}.");
    }

    private async Task<GameConfigBundle?> ReadCacheAsync()
    {
        if (!File.Exists(cachePath)) return null;
        var json = await File.ReadAllTextAsync(cachePath);
        return Deserialize<CachedConfig>(json).Bundle;
    }

    private async Task WriteCacheAsync(GameConfigBundle bundle)
    {
        var json = JsonSerializer.Serialize(new CachedConfig { Version = bundle.Version, Bundle = bundle }, JsonOptions);
        await AtomicFile.WriteTextAsync(cachePath, json);
    }

    private void PreserveCorruptCache()
    {
        if (!File.Exists(cachePath)) return;
        var corruptPath = $"{cachePath}.corrupt-{DateTime.UtcNow:yyyyMMdd-HHmmssfff}";
        try
        {
            File.Move(cachePath, corruptPath, true);
        }
        catch (Exception exception)
        {
            warningSink($"[ConfigService] Could not preserve corrupt cache: {exception.Message}");
        }
    }

    private sealed class CachedConfig
    {
        public string Version = string.Empty;
        public GameConfigBundle Bundle = new();
    }
}
