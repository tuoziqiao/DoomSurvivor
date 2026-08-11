using System;
using System.IO;
using System.Threading.Tasks;
using DoomSurvivor.Core;
using Newtonsoft.Json;
using UnityEngine;

namespace DoomSurvivor.Infrastructure
{
    public sealed class SaveService : ISaveService
    {
        private readonly string profilePath = Path.Combine(Application.persistentDataPath, "profile.json");
        private readonly string settingsPath = Path.Combine(Application.persistentDataPath, "settings.json");

        public async Task<SaveData> LoadProfileAsync()
        {
            var data = await LoadOrDefaultAsync(profilePath, () => new SaveData());
            return SaveMigration.Migrate(data);
        }

        public async Task<GameSettings> LoadSettingsAsync()
        {
            var settings = await LoadOrDefaultAsync(settingsPath, () => new GameSettings());
            settings.Clamp();
            return settings;
        }

        public Task SaveProfileAsync(SaveData data)
        {
            data = SaveMigration.Migrate(data);
            return AtomicFile.WriteTextAsync(profilePath, JsonConvert.SerializeObject(data, Formatting.Indented));
        }

        public Task SaveSettingsAsync(GameSettings settings)
        {
            settings ??= new GameSettings();
            settings.Clamp();
            return AtomicFile.WriteTextAsync(settingsPath, JsonConvert.SerializeObject(settings, Formatting.Indented));
        }

        public async Task ClearAsync()
        {
            await Task.Run(() =>
            {
                DeleteIfExists(profilePath);
                DeleteIfExists(settingsPath);
            });
        }

        private static async Task<T> LoadOrDefaultAsync<T>(string path, Func<T> factory)
        {
            if (!File.Exists(path))
            {
                return factory();
            }
            try
            {
                var json = await Task.Run(() => File.ReadAllText(path));
                return JsonConvert.DeserializeObject<T>(json) ?? factory();
            }
            catch (Exception exception)
            {
                var corrupt = $"{path}.corrupt-{DateTime.UtcNow:yyyyMMdd-HHmmss}";
                try
                {
                    File.Move(path, corrupt);
                }
                catch (Exception moveException)
                {
                    Debug.LogWarning($"[SaveService] 无法保留损坏存档: {moveException.Message}");
                }
                Debug.LogWarning($"[SaveService] 存档损坏，已恢复默认值: {exception.Message}");
                return factory();
            }
        }

        private static void DeleteIfExists(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    internal static class AtomicFile
    {
        public static Task WriteTextAsync(string path, string contents)
        {
            return Task.Run(() =>
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path) ?? Application.persistentDataPath);
                var temp = path + ".tmp";
                File.WriteAllText(temp, contents);
                if (File.Exists(path))
                {
                    File.Replace(temp, path, null);
                }
                else
                {
                    File.Move(temp, path);
                }
            });
        }
    }
}
