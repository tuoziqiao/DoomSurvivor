using System;
using DoomSurvivor.Core;
using DoomSurvivor.Gameplay;
using Godot;
using NumericsVector2 = System.Numerics.Vector2;

namespace DoomSurvivor.Presentation;

public partial class BattleView : Node2D
{
    private readonly BattleSimulator simulator;
    private readonly Texture2D? playerTexture;
    private readonly Texture2D? mapTexture;
    private readonly Color primary;
    private readonly Color secondary;
    private readonly Color accent;
    private readonly string mapSkinId;

    public BattleView(BattleSimulator source, Texture2D? texture, Texture2D? map, SkinPalette palette, string mapSkin)
    {
        simulator = source;
        playerTexture = texture;
        mapTexture = map;
        primary = ParseColor(palette.Primary, new Color("#6CA8A5"));
        secondary = ParseColor(palette.Secondary, new Color("#203A48"));
        accent = ParseColor(palette.Accent, new Color("#E7B85C"));
        mapSkinId = mapSkin;
        ZIndex = -1;
    }

    public override void _Draw()
    {
        var snapshot = simulator.CreateSnapshot();
        var playerPosition = snapshot.Player.Position;
        var background = mapSkinId.StartsWith("dry_", StringComparison.Ordinal)
            ? new Color("#69513B")
            : new Color("#19362E");
        DrawRect(new Rect2(-1800, -1800, 3600, 3600), background);
        if (mapTexture is not null) DrawTextureRect(mapTexture, new Rect2(-1800, -1800, 3600, 3600), true, new Color(1f, 1f, 1f, 0.78f));

        var gridColor = new Color(1f, 1f, 1f, 0.06f);
        for (var x = -1800; x <= 1800; x += 96) DrawLine(new Vector2(x, -1800), new Vector2(x, 1800), gridColor, 1f);
        for (var y = -1800; y <= 1800; y += 96) DrawLine(new Vector2(-1800, y), new Vector2(1800, y), gridColor, 1f);

        foreach (var crystal in snapshot.Crystals)
        {
            var position = ToScreen(crystal.Position, playerPosition);
            var crystalColor = crystal.Kind switch
            {
                "large" => new Color("#F5C96A"),
                "medium" => new Color("#B9E6FF"),
                _ => new Color("#7BE7D3")
            };
            DrawCircle(position, crystal.Kind == "large" ? 10f : 7f, crystalColor);
            DrawCircle(position, 12f, new Color(crystalColor, 0.16f));
        }

        foreach (var mapEvent in snapshot.MapEvents)
        {
            if (!mapEvent.Active) continue;
            var position = ToScreen(mapEvent.Position, playerPosition);
            var color = mapEvent.Type switch
            {
                "PoisonFog" => new Color(0.56f, 0.82f, 0.43f, 0.38f),
                "Altar" => new Color("#D28BDE"),
                "HiddenCrate" => new Color("#C9A36A"),
                "HealingChicken" => new Color("#F2E7AF"),
                _ => new Color("#D9A35E")
            };
            DrawCircle(position, mapEvent.Type == "PoisonFog" ? mapEvent.Radius : 18f, new Color(color, mapEvent.Type == "PoisonFog" ? 0.18f : 0.3f));
            DrawCircle(position, mapEvent.Type == "PoisonFog" ? Math.Min(18f, mapEvent.Radius * 0.12f) : 10f, color);
        }

        foreach (var effect in snapshot.Effects)
        {
            var start = ToScreen(effect.Position, playerPosition);
            var target = ToScreen(effect.Target, playerPosition);
            var color = effect.Kind switch
            {
                "lightning_chain" => new Color("#B9D6FF"),
                "fire_zone" or "fire_bottle" => new Color("#F29E54"),
                "fubo_qin" or "fubo_qin_gold" => new Color("#A8E6D3"),
                "drone" or "drone_bolt" => new Color("#E3D38D"),
                _ => new Color("#C2F0FF")
            };
            if (start.DistanceTo(target) > 2f) DrawLine(start, target, new Color(color, 0.8f), effect.Kind == "lightning_chain" ? 3f : 2f, true);
            if (effect.Radius > 8f) DrawCircle(start, Math.Min(180f, effect.Radius), new Color(color, 0.1f));
        }

        foreach (var enemy in snapshot.Enemies)
        {
            var position = ToScreen(enemy.Position, playerPosition);
            var color = enemy.IsBoss ? new Color("#D95D9A") : enemy.IsElite ? new Color("#E7A94B") : new Color("#B95B56");
            var radius = Math.Clamp(enemy.Radius * (enemy.IsBoss ? 1.35f : 1f), 10f, 28f);
            DrawCircle(position, radius + 4f, new Color(0f, 0f, 0f, 0.22f));
            DrawCircle(position, radius, color);
            DrawLine(position + new Vector2(-radius, -radius - 8f), position + new Vector2(radius, -radius - 8f), new Color("#53292D"), 4f);
            DrawLine(position + new Vector2(-radius, -radius - 8f), position + new Vector2(-radius + 2f * radius * Math.Clamp(enemy.Hp / enemy.MaxHp, 0f, 1f), -radius - 8f), new Color("#8DE0A6"), 4f);
        }

        foreach (var weapon in snapshot.Weapons)
        {
            if (weapon.Id == "rotating_knife")
            {
                var count = Math.Clamp(weapon.Level + 1, 2, 5);
                var radius = 58f + weapon.Level * 5f;
                for (var index = 0; index < count; index++)
                {
                    var angle = snapshot.Elapsed * (1.6f + weapon.Level * 0.2f) + Mathf.Tau * index / count;
                    DrawCircle(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, weapon.IsPromoted ? 9f : 7f, weapon.IsPromoted ? new Color("#F5D77E") : new Color("#C5E0E2"));
                }
            }
            else if (weapon.Id == "fubo_qin")
            {
                DrawArc(Vector2.Zero, 112f + weapon.Level * 4f, 0f, Mathf.Tau, 64, weapon.IsPromoted ? new Color("#F2D47B") : new Color("#82D7C1"), 2.5f, true);
            }
            else if (weapon.Id == "drone")
            {
                var count = Math.Clamp(weapon.Level / 2 + 1, 1, 4);
                for (var index = 0; index < count; index++)
                {
                    var angle = -snapshot.Elapsed * 1.2f + Mathf.Tau * index / count;
                    var point = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 106f;
                    DrawCircle(point, 8f, new Color("#E3D38D"));
                    DrawLine(point, point + new Vector2(0f, 9f), new Color("#8A6E43"), 2f);
                }
            }
        }

        if (playerTexture is not null)
        {
            DrawTextureRect(playerTexture, new Rect2(-38f, -38f, 76f, 76f), false, Colors.White);
        }
        else
        {
            DrawCircle(Vector2.Zero, 27f, secondary);
            DrawCircle(Vector2.Zero, 21f, primary);
        }
        DrawCircle(Vector2.Zero, 43f, new Color(accent, 0.25f));
        DrawArc(Vector2.Zero, 42f, -Mathf.Pi * 0.5f, Mathf.Pi * 1.2f, 36, accent, 3f, true);
    }

    private static Vector2 ToScreen(NumericsVector2 world, NumericsVector2 player) => new(world.X - player.X, world.Y - player.Y);

    private static Color ParseColor(string value, Color fallback)
    {
        try { return Color.FromHtml(value); }
        catch { return fallback; }
    }
}
