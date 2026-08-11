using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using DoomSurvivor.Core;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace DoomSurvivor.Infrastructure
{
    public sealed class ConfigService : IConfigService
    {
        private const string CacheFileName = "config-cache.json";
        private static readonly string[] SectionFiles =
        {
            "characters.json", "skins.json", "enemies.json", "weapons.json",
            "skills.json", "stages.json", "balance.json"
        };

        private readonly bool enableRemote;
        private readonly string apiBaseUrl;
        private readonly string cachePath;

        public ConfigLoadSource Source { get; private set; } = ConfigLoadSource.Builtin;

        public ConfigService(bool enableRemote, string apiBaseUrl)
        {
            this.enableRemote = enableRemote;
            this.apiBaseUrl = (apiBaseUrl ?? string.Empty).TrimEnd('/');
            cachePath = Path.Combine(Application.persistentDataPath, CacheFileName);
        }

        public async Task<GameConfigBundle> LoadAsync()
        {
            var builtin = await LoadBuiltinAsync();
            Source = ConfigLoadSource.Builtin;
            if (!enableRemote || string.IsNullOrWhiteSpace(apiBaseUrl))
            {
                return builtin;
            }

            CachedConfig cached = null;
            try
            {
                cached = await ReadCacheAsync();
                var version = await GetJsonAsync<ApiResponse<ConfigVersionData>>(
                    $"{apiBaseUrl}/api/game-config/version");
                if (version == null || !version.Success || version.Data == null)
                {
                    throw new InvalidDataException(version?.Error ?? "配置版本响应无效");
                }

                if (cached?.Bundle != null && cached.Version == version.Data.Version)
                {
                    Validate(cached.Bundle);
                    Source = ConfigLoadSource.Cache;
                    return cached.Bundle;
                }

                var response = await GetJsonAsync<ApiResponse<GameConfigBundle>>(
                    $"{apiBaseUrl}/api/game-config/bundle");
                if (response == null || !response.Success || response.Data == null)
                {
                    throw new InvalidDataException(response?.Error ?? "配置包响应无效");
                }

                Validate(response.Data);
                await WriteCacheAsync(new CachedConfig
                {
                    Version = version.Data.Version,
                    Bundle = response.Data
                });
                Source = ConfigLoadSource.Remote;
                return response.Data;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[ConfigService] 远程配置不可用: {exception.Message}");
                if (cached?.Bundle != null)
                {
                    try
                    {
                        Validate(cached.Bundle);
                        Source = ConfigLoadSource.Cache;
                        return cached.Bundle;
                    }
                    catch (Exception cacheException)
                    {
                        Debug.LogWarning($"[ConfigService] 缓存配置无效: {cacheException.Message}");
                    }
                }
                return builtin;
            }
        }

        public static void Validate(GameConfigBundle bundle)
        {
            if (bundle == null)
            {
                throw new InvalidDataException("配置包为空");
            }
            var enemyIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var enemy in bundle.Enemies.Enemies)
            {
                if (string.IsNullOrWhiteSpace(enemy.Id) || !enemyIds.Add(enemy.Id))
                {
                    throw new InvalidDataException("敌人 id 为空或重复");
                }
            }
            foreach (var stage in bundle.Stages.Stages)
            {
                foreach (var entry in stage.SpawnTimeline)
                {
                    if (!enemyIds.Contains(entry.EnemyId))
                    {
                        throw new InvalidDataException($"关卡引用了未知敌人: {entry.EnemyId}");
                    }
                }
            }
        }

        private async Task<GameConfigBundle> LoadBuiltinAsync()
        {
            var json = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in SectionFiles)
            {
                json[file] = await ReadStreamingTextAsync(file);
            }

            var bundle = new GameConfigBundle
            {
                Version = "builtin",
                Characters = JsonConvert.DeserializeObject<CharactersConfig>(json["characters.json"]),
                Skins = JsonConvert.DeserializeObject<SkinsConfig>(json["skins.json"]),
                Enemies = JsonConvert.DeserializeObject<EnemiesConfig>(json["enemies.json"]),
                Weapons = JsonConvert.DeserializeObject<WeaponsConfig>(json["weapons.json"]),
                Skills = JsonConvert.DeserializeObject<SkillsConfig>(json["skills.json"]),
                Stages = JsonConvert.DeserializeObject<StagesConfig>(json["stages.json"]),
                Balance = JsonConvert.DeserializeObject<BalanceConfig>(json["balance.json"])
            };
            Validate(bundle);
            return bundle;
        }

        private static async Task<string> ReadStreamingTextAsync(string fileName)
        {
            var path = Path.Combine(Application.streamingAssetsPath, "GameConfig", fileName);
            if (Application.platform == RuntimePlatform.WindowsEditor ||
                Application.platform == RuntimePlatform.WindowsPlayer ||
                Application.platform == RuntimePlatform.OSXEditor ||
                Application.platform == RuntimePlatform.OSXPlayer ||
                Application.platform == RuntimePlatform.LinuxEditor ||
                Application.platform == RuntimePlatform.LinuxPlayer)
            {
                return await Task.Run(() => File.ReadAllText(path));
            }

            using var request = UnityWebRequest.Get(path);
            var operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                await Task.Yield();
            }
            if (request.result != UnityWebRequest.Result.Success)
            {
                throw new IOException($"无法读取内置配置 {fileName}: {request.error}");
            }
            return request.downloadHandler.text;
        }

        private static async Task<T> GetJsonAsync<T>(string url)
        {
            using var request = UnityWebRequest.Get(url);
            request.timeout = 4;
            var operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                await Task.Yield();
            }
            if (request.result != UnityWebRequest.Result.Success)
            {
                throw new IOException($"{url}: {request.error}");
            }
            return JsonConvert.DeserializeObject<T>(request.downloadHandler.text);
        }

        private async Task<CachedConfig> ReadCacheAsync()
        {
            if (!File.Exists(cachePath))
            {
                return null;
            }
            var json = await Task.Run(() => File.ReadAllText(cachePath));
            return JsonConvert.DeserializeObject<CachedConfig>(json);
        }

        private async Task WriteCacheAsync(CachedConfig cache)
        {
            var json = JsonConvert.SerializeObject(cache, Formatting.None);
            await AtomicFile.WriteTextAsync(cachePath, json);
        }

        [Serializable]
        private sealed class CachedConfig
        {
            public string Version = string.Empty;
            public GameConfigBundle Bundle;
        }
    }
}
