using System.Collections.Generic;
using DoomSurvivor.Core;

namespace DoomSurvivor.Gameplay;

internal sealed class BattleLifecycleSystem
{
    private readonly RunLoadout loadout;

    public BattleLifecycleSystem(RunLoadout runLoadout)
    {
        loadout = runLoadout;
    }

    public bool TryFinish(float elapsed, BattleSimulator.PlayerRuntime player, bool bossActive, out bool victory)
    {
        var duration = loadout.Mode == GameMode.QuickTest ? loadout.Stage.QuickTestDuration : loadout.Stage.NormalModeDuration;
        if (duration > 0f && elapsed >= duration && !bossActive)
        {
            victory = true;
            return true;
        }

        if (player.Hp <= 0f)
        {
            player.Hp = 0f;
            victory = false;
            return true;
        }

        victory = false;
        return false;
    }

    public void Cleanup(List<EnemyRuntime> enemies, List<CrystalRuntime> crystals, List<FireZoneRuntime> fireZones, List<CombatEffectRuntime> effects)
    {
        enemies.RemoveAll(value => !value.Active);
        crystals.RemoveAll(value => !value.Active);
        fireZones.RemoveAll(value => value.Remaining <= 0f);
        effects.RemoveAll(value => value.Remaining <= 0f);

        var maxEffects = loadout.Settings.ParticleQuality switch
        {
            ParticleQuality.Low => 24,
            ParticleQuality.High => 128,
            _ => 72
        };
        if (effects.Count > maxEffects) effects.RemoveRange(0, effects.Count - maxEffects);
    }
}
