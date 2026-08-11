using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using DoomSurvivor.Core;
using Godot;

namespace DoomSurvivor.Infrastructure;

public sealed class SaveService : ISaveService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { IncludeFields = true, WriteIndented = true, PropertyNameCaseInsensitive = true };
    private readonly string profilePath;
    private readonly string settingsPath;
    private readonly Action<string> warningSink;

    public SaveService(string? profilePathOverride = null, string? settingsPathOverride = null, Action<string>? warningSink = null)
    {
        profilePath = string.IsNullOrWhiteSpace(profilePathOverride)
            ? Path.Combine(OS.GetUserDataDir(), "profile.json")
            : profilePathOverride;
        settingsPath = string.IsNullOrWhiteSpace(settingsPathOverride)
            ? Path.Combine(OS.GetUserDataDir(), "settings.json")
            : settingsPathOverride;
        this.warningSink = warningSink ?? (message => GD.PushWarning(message));
    }

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
        return AtomicFile.WriteTextAsync(profilePath, JsonSerializer.Serialize(data, JsonOptions));
    }

    public Task SaveSettingsAsync(GameSettings settings)
    {
        settings ??= new GameSettings();
        settings.Clamp();
        return AtomicFile.WriteTextAsync(settingsPath, JsonSerializer.Serialize(settings, JsonOptions));
    }

    public async Task ClearAsync()
    {
        await Task.Run(() =>
        {
            DeleteIfExists(profilePath);
            DeleteIfExists(settingsPath);
        });
    }

    private async Task<T> LoadOrDefaultAsync<T>(string path, Func<T> factory) where T : class
    {
        if (!File.Exists(path)) return factory();
        try
        {
            var json = await File.ReadAllTextAsync(path);
            return JsonSerializer.Deserialize<T>(json, JsonOptions) ?? factory();
        }
        catch (Exception exception)
        {
            var corrupt = $"{path}.corrupt-{DateTime.UtcNow:yyyyMMdd-HHmmssfff}";
            try { File.Move(path, corrupt, true); }
            catch (Exception moveException) { warningSink($"[SaveService] Could not preserve corrupt save: {moveException.Message}"); }
            warningSink($"[SaveService] Save recovered with defaults: {exception.Message}");
            return factory();
        }
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path)) File.Delete(path);
    }
}

internal static class AtomicFile
{
    public static async Task WriteTextAsync(string path, string contents)
    {
        await Task.Run(() =>
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            var temp = path + ".tmp";
            File.WriteAllText(temp, contents);
            File.Move(temp, path, true);
        });
    }
}
