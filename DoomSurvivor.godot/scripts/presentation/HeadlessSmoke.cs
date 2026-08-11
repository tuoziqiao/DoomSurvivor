using System;
using System.Linq;
using DoomSurvivor.Application;
using DoomSurvivor.Core;
using DoomSurvivor.Gameplay;
using DoomSurvivor.Infrastructure;
using Godot;

namespace DoomSurvivor.Presentation;

public partial class HeadlessSmoke : Node
{
    public override async void _Ready()
    {
        try
        {
            var composition = new GameCompositionRoot(new ConfigService(), new SaveService());
            await composition.InitializeAsync();
            var character = composition.Characters.All.First(value => composition.Unlocks.IsCharacterUnlocked(value.Id));
            var skin = composition.Skins.ForCharacter(character.Id).First(value => composition.Unlocks.IsSkinUnlocked(value.Id));
            var stage = composition.Stages.All.First();
            var loadout = composition.StartRun(new RunRequest
            {
                Mode = GameMode.QuickTest,
                CharacterId = character.Id,
                SkinId = skin.Id,
                StageId = stage.Id,
                MapSkinId = composition.Session.Settings.MapSkinId
            });
            var battle = new BattleSimulator(loadout, 20260811);
            for (var index = 0; index < 600 && !battle.IsFinished && !battle.NeedsUpgradeChoice; index++)
            {
                battle.Tick(1f / 60f, new BattleInput(0f, 0f));
            }

            var snapshot = battle.CreateSnapshot();
            GD.Print($"[HeadlessSmoke] wave={snapshot.CurrentWave}/{snapshot.WaveCount} enemies={snapshot.Enemies.Count} kills={snapshot.KillCount} weapons={snapshot.Weapons.Count} events={snapshot.MapEvents.Count}");
            if (snapshot.CurrentWave < 1 || snapshot.Weapons.Count < 1 || snapshot.MapEvents.Count < 1)
            {
                GD.PushError("[HeadlessSmoke] Battle smoke assertions failed.");
                GetTree().Quit(1);
                return;
            }
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"[HeadlessSmoke] {exception}");
            GetTree().Quit(1);
        }
    }
}
