using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using DoomSurvivor.Core;
using DoomSurvivor.Infrastructure;

namespace DoomSurvivor.Tests.Infrastructure;

[TestClass]
public sealed class InfrastructurePipelineTests
{
    private readonly List<string> tempDirectories = new();

    [TestCleanup]
    public void Cleanup()
    {
        foreach (var directory in tempDirectories)
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [TestMethod]
    public async Task BuiltinConfigLoadsFromInjectedSectionSource()
    {
        var builtin = CreateBundle("builtin-1", "builtin-enemy");
        var service = CreateConfigService(
            enableRemote: false,
            builtin: CreateBuiltinSource(builtin),
            cachePath: GetTempFile("config-cache.json"));

        var loaded = await service.LoadAsync();

        Assert.AreEqual(ConfigLoadSource.Builtin, service.Source);
        Assert.AreEqual("builtin-1", loaded.Version);
        Assert.AreEqual("builtin-enemy", loaded.Enemies.Enemies[0].Id);
    }

    [TestMethod]
    public async Task RemoteFailureFallsBackToValidCache()
    {
        var builtin = CreateBundle("builtin-1", "builtin-enemy");
        var remote = CreateBundle("remote-2", "remote-enemy");
        var cachePath = GetTempFile("config-cache.json");

        var remoteService = CreateConfigService(
            enableRemote: true,
            remoteSource: new StubRemoteConfig(Json(remote)),
            builtin: CreateBuiltinSource(builtin),
            cachePath: cachePath);
        var remoteResult = await remoteService.LoadAsync();

        var fallbackService = CreateConfigService(
            enableRemote: true,
            remoteSource: new StubRemoteConfig(exception: new IOException("offline")),
            builtin: CreateBuiltinSource(builtin),
            cachePath: cachePath);
        var fallbackResult = await fallbackService.LoadAsync();

        Assert.AreEqual(ConfigLoadSource.Remote, remoteService.Source);
        Assert.AreEqual("remote-2", remoteResult.Version);
        Assert.AreEqual(ConfigLoadSource.Cache, fallbackService.Source);
        Assert.AreEqual("remote-2", fallbackResult.Version);
        Assert.AreEqual("remote-enemy", fallbackResult.Enemies.Enemies[0].Id);
    }

    [TestMethod]
    public async Task CorruptCacheIsMovedBeforeBuiltinFallback()
    {
        var builtin = CreateBundle("builtin-1", "builtin-enemy");
        var cachePath = GetTempFile("config-cache.json");
        File.WriteAllText(cachePath, "{ this is not json }");

        var service = CreateConfigService(
            enableRemote: true,
            remoteSource: new StubRemoteConfig(exception: new IOException("offline")),
            builtin: CreateBuiltinSource(builtin),
            cachePath: cachePath);
        var loaded = await service.LoadAsync();

        Assert.AreEqual(ConfigLoadSource.Builtin, service.Source);
        Assert.AreEqual("builtin-1", loaded.Version);
        Assert.IsFalse(File.Exists(cachePath));
        Assert.AreEqual(1, Directory.GetFiles(Path.GetDirectoryName(cachePath)!, "config-cache.json.corrupt-*").Length);
    }

    [TestMethod]
    public async Task SaveServiceRoundTripsClampedSettingsWithoutTempFile()
    {
        var profilePath = GetTempFile("profile.json");
        var settingsPath = GetTempFile("settings.json");
        var service = new SaveService(profilePath, settingsPath, _ => { });

        await service.SaveSettingsAsync(new GameSettings
        {
            MasterVolume = 2f,
            SfxVolume = -1f,
            MaxEnemyDisplay = 1,
            MapSkinId = "not-a-real-map"
        });
        var loaded = await service.LoadSettingsAsync();

        Assert.AreEqual(1f, loaded.MasterVolume);
        Assert.AreEqual(0f, loaded.SfxVolume);
        Assert.AreEqual(50, loaded.MaxEnemyDisplay);
        Assert.AreEqual(GameSettings.MapSkinOptions[0], loaded.MapSkinId);
        Assert.IsFalse(File.Exists(settingsPath + ".tmp"));
    }

    [TestMethod]
    public async Task CorruptProfileIsPreservedAndMigratedDefaultsAreReturned()
    {
        var profilePath = GetTempFile("profile.json");
        var settingsPath = GetTempFile("settings.json");
        File.WriteAllText(profilePath, "not-json");
        var service = new SaveService(profilePath, settingsPath, _ => { });

        var loaded = await service.LoadProfileAsync();

        Assert.AreEqual(SaveMigration.CurrentVersion, loaded.SaveVersion);
        Assert.IsFalse(File.Exists(profilePath));
        Assert.AreEqual(1, Directory.GetFiles(Path.GetDirectoryName(profilePath)!, "profile.json.corrupt-*").Length);
    }

    private ConfigService CreateConfigService(
        bool enableRemote,
        IBuiltinConfigSource builtin,
        string cachePath,
        IRemoteConfigSource? remoteSource = null)
    {
        return new ConfigService(enableRemote, remoteSource, cachePath, builtin, _ => { });
    }

    private string GetTempFile(string fileName)
    {
        var directory = Path.Combine(Path.GetTempPath(), "DoomSurvivorInfrastructureTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        tempDirectories.Add(directory);
        return Path.Combine(directory, fileName);
    }

    private static string Json<T>(T value)
    {
        return JsonSerializer.Serialize(value, new JsonSerializerOptions { IncludeFields = true });
    }

    private static IBuiltinConfigSource CreateBuiltinSource(GameConfigBundle bundle)
    {
        return new MemoryBuiltinConfigSource(new Dictionary<string, string>
        {
            ["characters.json"] = Json(bundle.Characters),
            ["skins.json"] = Json(bundle.Skins),
            ["enemies.json"] = Json(bundle.Enemies),
            ["weapons.json"] = Json(bundle.Weapons),
            ["skills.json"] = Json(bundle.Skills),
            ["stages.json"] = Json(bundle.Stages),
            ["balance.json"] = Json(bundle.Balance)
        });
    }

    private static GameConfigBundle CreateBundle(string version, string enemyId)
    {
        return new GameConfigBundle
        {
            Version = version,
            Characters = new CharactersConfig
            {
                Version = version,
                Characters = new() { new CharacterConfig { Id = "hero", Name = "Hero", UnlockByDefault = true } }
            },
            Skins = new SkinsConfig
            {
                Version = version,
                Skins = new() { new SkinConfig { Id = "hero_skin", CharacterId = "hero", UnlockByDefault = true } }
            },
            Enemies = new EnemiesConfig
            {
                Version = version,
                Enemies = new() { new EnemyConfig { Id = enemyId, Name = "Enemy", MaxHp = 10f } }
            },
            Weapons = new WeaponsConfig { Version = version },
            Skills = new SkillsConfig { Version = version },
            Stages = new StagesConfig
            {
                Version = version,
                Stages = new()
                {
                    new StageConfig
                    {
                        Id = "stage",
                        SpawnTimeline = new() { new SpawnTimelineEntry { EnemyId = enemyId } }
                    }
                }
            },
            Balance = new BalanceConfig { Version = version }
        };
    }

    private sealed class MemoryBuiltinConfigSource : IBuiltinConfigSource
    {
        private readonly IReadOnlyDictionary<string, string> sections;

        public MemoryBuiltinConfigSource(IReadOnlyDictionary<string, string> sections)
        {
            this.sections = sections;
        }

        public Task<string?> ReadTextAsync(string fileName)
        {
            return Task.FromResult(sections.TryGetValue(fileName, out var value) ? value : null);
        }
    }

    private sealed class StubRemoteConfig : IRemoteConfigSource
    {
        private readonly string? json;
        private readonly Exception? exception;

        public StubRemoteConfig(string? json = null, Exception? exception = null)
        {
            this.json = json;
            this.exception = exception;
        }

        public Task<string?> FetchBundleJsonAsync()
        {
            if (exception is not null) throw exception;
            return Task.FromResult(json);
        }
    }
}
