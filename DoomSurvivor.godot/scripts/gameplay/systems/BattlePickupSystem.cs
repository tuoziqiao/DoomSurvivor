using System;
using System.Collections.Generic;
using System.Numerics;
using DoomSurvivor.Core;

namespace DoomSurvivor.Gameplay;

internal sealed class BattlePickupSystem
{
    private readonly RunLoadout loadout;
    private readonly ExperienceProgress experience;

    public BattlePickupSystem(RunLoadout runLoadout, ExperienceProgress progress)
    {
        loadout = runLoadout;
        experience = progress;
    }

    public int Collect(BattleSimulator.PlayerRuntime player, List<CrystalRuntime> crystals)
    {
        var collected = 0;
        foreach (var crystal in crystals)
        {
            if (!crystal.Active) continue;
            var distance = Vector2.Distance(crystal.Position, player.Position);
            if (distance > player.PickupRadius * 1.65f) continue;
            if (distance > player.PickupRadius)
            {
                var direction = player.Position - crystal.Position;
                if (direction.LengthSquared() > 0.01f)
                {
                    var pull = Vector2.Normalize(direction) * Math.Min(360f, distance * 4f) * (1f / 60f);
                    crystal.Position += pull;
                }
                continue;
            }

            crystal.Active = false;
            experience.Add((int)MathF.Round(crystal.Value * Math.Max(0.1f, loadout.Character.ExperienceMultiplier)));
            collected++;
        }

        MergeNearbyCrystals(crystals);
        return collected;
    }

    private void MergeNearbyCrystals(List<CrystalRuntime> crystals)
    {
        var threshold = Math.Max(0, loadout.Balance.Experience.CrystalMergeThreshold);
        var distance = Math.Max(0f, loadout.Balance.Experience.CrystalMergeDistance);
        if (threshold <= 0 || distance <= 0f) return;

        for (var index = 0; index < crystals.Count; index++)
        {
            var first = crystals[index];
            if (!first.Active || first.Value < threshold) continue;
            for (var otherIndex = index + 1; otherIndex < crystals.Count; otherIndex++)
            {
                var other = crystals[otherIndex];
                if (!other.Active || Vector2.Distance(first.Position, other.Position) > distance) continue;
                first.Value += other.Value;
                other.Active = false;
            }
        }
    }
}
