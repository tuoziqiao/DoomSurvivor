using System;
using System.Collections.Generic;
using System.Numerics;
using DoomSurvivor.Core;

namespace DoomSurvivor.Gameplay;

internal sealed class BattleMovementSystem
{
    private readonly RunLoadout loadout;

    public BattleMovementSystem(RunLoadout runLoadout)
    {
        loadout = runLoadout;
    }

    public void MovePlayer(BattleSimulator.PlayerRuntime player, BattleInput input, float delta)
    {
        var direction = new Vector2(input.X, input.Y);
        if (direction.LengthSquared() > 1f) direction = Vector2.Normalize(direction);
        var targetVelocity = direction * player.MoveSpeed;
        var acceleration = direction.LengthSquared() > 0.01f
            ? (loadout.Balance.Player.Acceleration > 0f ? loadout.Balance.Player.Acceleration : 99999f)
            : (loadout.Balance.Player.Deceleration > 0f ? loadout.Balance.Player.Deceleration : 99999f);
        var maxChange = acceleration * delta;
        var velocityDelta = targetVelocity - player.Velocity;
        if (velocityDelta.Length() > maxChange) velocityDelta = Vector2.Normalize(velocityDelta) * maxChange;
        player.Velocity += velocityDelta;
        player.Position += player.Velocity * delta;
        player.Position = new Vector2(
            Math.Clamp(player.Position.X, BattleSimulationConstants.PlayerWorldMargin, Math.Max(BattleSimulationConstants.PlayerWorldMargin, loadout.Stage.MapWidth - BattleSimulationConstants.PlayerWorldMargin)),
            Math.Clamp(player.Position.Y, BattleSimulationConstants.PlayerWorldMargin, Math.Max(BattleSimulationConstants.PlayerWorldMargin, loadout.Stage.MapHeight - BattleSimulationConstants.PlayerWorldMargin)));
    }

    public void MoveEnemies(BattleSimulator.PlayerRuntime player, List<EnemyRuntime> enemies, float delta)
    {
        foreach (var enemy in enemies)
        {
            if (!enemy.Active) continue;
            enemy.ContactCooldown = Math.Max(0f, enemy.ContactCooldown - delta);
            enemy.DashTimer = Math.Max(0f, enemy.DashTimer - delta);
            var towardPlayer = player.Position - enemy.Position;
            if (towardPlayer.LengthSquared() <= 0.01f) continue;
            towardPlayer = Vector2.Normalize(towardPlayer);
            var speed = Math.Max(8f, enemy.Config.MoveSpeed);
            if (enemy.IsElite && enemy.Config.DashCooldown > 0f && enemy.DashTimer <= 0f)
            {
                enemy.DashTimer = enemy.Config.DashCooldown;
                enemy.Position += towardPlayer * Math.Max(speed, enemy.Config.DashSpeed) * Math.Max(0.1f, enemy.Config.DashDuration) * 0.25f;
            }
            enemy.Position += towardPlayer * speed * delta;
        }
    }
}
