using System;
using Godot;

namespace DoomSurvivor.Infrastructure;

public interface IResourceProvider
{
    Texture2D? LoadCharacterModel(string modelAsset);
    Texture2D? LoadMapSkin(string mapSkinId);
    Texture2D? LoadWeaponIcon(string iconId);
}

public sealed class GodotResourceProvider : IResourceProvider
{
    public Texture2D? LoadCharacterModel(string modelAsset)
    {
        if (string.IsNullOrWhiteSpace(modelAsset)) return null;
        return GD.Load<Texture2D>($"res://resources/models/Characters/{modelAsset}");
    }

    public Texture2D? LoadMapSkin(string mapSkinId)
    {
        var normalized = DoomSurvivor.Core.GameSettings.NormalizeMapSkinId(mapSkinId);
        var path = normalized.StartsWith("dry_", StringComparison.Ordinal)
            ? $"res://resources/art/Map/Tiles/{normalized}.png"
            : $"res://resources/art/Map/Tiles/{normalized}.png";
        return GD.Load<Texture2D>(path);
    }

    public Texture2D? LoadWeaponIcon(string iconId)
    {
        if (string.IsNullOrWhiteSpace(iconId)) return null;
        return GD.Load<Texture2D>($"res://resources/art/Weapons/Icons/{iconId}.png");
    }
}
