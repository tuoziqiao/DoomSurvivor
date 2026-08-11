using System;
using System.Linq;
using System.Threading.Tasks;
using DoomSurvivor.Core;
using DoomSurvivor.Domains;

namespace DoomSurvivor.Application;

public sealed class GameCompositionRoot
{
    private readonly IConfigService configService;
    private readonly ISaveService saveService;

    public GameCompositionRoot(IConfigService configService, ISaveService saveService)
    {
        this.configService = configService;
        this.saveService = saveService;
    }

    public GameSession Session { get; } = new();
    public GameStateMachine StateMachine { get; } = new();
    public PresentationFlow Presentation { get; } = new();
    public ICharacterCatalog Characters { get; private set; } = new InMemoryCharacterCatalog(Array.Empty<CharacterConfig>());
    public ISkinCatalog Skins { get; private set; } = new InMemorySkinCatalog(Array.Empty<SkinConfig>());
    public IStageCatalog Stages { get; private set; } = new InMemoryStageCatalog(Array.Empty<StageConfig>());
    public IContentUnlockService Unlocks { get; private set; } = null!;
    public RunLoadoutFactory Loadouts { get; private set; } = null!;
    public bool Ready { get; private set; }

    public async Task InitializeAsync()
    {
        if (Ready) return;
        StateMachine.Set(GameState.Loading);
        var configTask = configService.LoadAsync();
        var profileTask = saveService.LoadProfileAsync();
        var settingsTask = saveService.LoadSettingsAsync();
        await Task.WhenAll(configTask, profileTask, settingsTask);

        Session.Config = configTask.Result;
        Session.ConfigSource = configService.Source;
        Session.Profile = SaveMigration.Migrate(profileTask.Result);
        Session.Settings = settingsTask.Result;
        Session.Settings.Clamp();

        Characters = new InMemoryCharacterCatalog(Session.Config.Characters.Characters);
        Skins = new InMemorySkinCatalog(Session.Config.Skins.Skins);
        Stages = new InMemoryStageCatalog(Session.Config.Stages.Stages);
        Unlocks = new SaveUnlockService(Characters, Skins, Session.Profile);
        Loadouts = new RunLoadoutFactory(
            Characters,
            Skins,
            Stages,
            Unlocks,
            Session.Config.Balance,
            Session.Config.Enemies.Enemies,
            Session.Config.Weapons.Weapons,
            Session.Config.Skills.Skills,
            () => Session.Settings);
        EnsureSelectionIsValid();
        Ready = true;
        StateMachine.Set(GameState.MainMenu);
        Presentation.GoTo(PresentationScreen.MainMenu);
    }

    public RunLoadout StartRun(RunRequest request)
    {
        if (!Ready) throw new InvalidOperationException("Game composition root is not ready.");
        var loadout = Loadouts.Create(request);
        Session.Launch = new GameLaunchOptions
        {
            Mode = request.Mode,
            CharacterId = request.CharacterId,
            SkinId = request.SkinId,
            StageId = request.StageId
        };
        Session.Profile.SelectedCharacterId = request.CharacterId;
        Session.Profile.SelectedSkinByCharacter[request.CharacterId] = request.SkinId;
        _ = saveService.SaveProfileAsync(Session.Profile);
        StateMachine.Set(GameState.Playing);
        Presentation.GoTo(PresentationScreen.Battle);
        return loadout;
    }

    public async Task SaveSettingsAsync()
    {
        Session.Settings.Clamp();
        await saveService.SaveSettingsAsync(Session.Settings);
    }

    public async Task RecordResultAsync(GameResultStats result)
    {
        Session.LastResult = result;
        Session.Profile.MaxKills = Math.Max(Session.Profile.MaxKills, result.KillCount);
        Session.Profile.MaxLevel = Math.Max(Session.Profile.MaxLevel, result.MaxLevel);
        Session.Profile.MaxSurvivalTime = Math.Max(Session.Profile.MaxSurvivalTime, result.SurvivalTime);
        await saveService.SaveProfileAsync(Session.Profile);
        StateMachine.Set(result.Victory ? GameState.Victory : GameState.Defeat);
        Presentation.GoTo(PresentationScreen.Result);
    }

    private void EnsureSelectionIsValid()
    {
        if (!Characters.TryGet(Session.Profile.SelectedCharacterId, out var character) || !Unlocks.IsCharacterUnlocked(character.Id))
        {
            character = Characters.All.FirstOrDefault(value => Unlocks.IsCharacterUnlocked(value.Id)) ?? Characters.All.First();
            Session.Profile.SelectedCharacterId = character.Id;
        }

        if (!Session.Profile.SelectedSkinByCharacter.TryGetValue(character.Id, out var skinId) ||
            !Skins.TryGet(skinId, out var skin) || !string.Equals(skin.CharacterId, character.Id, StringComparison.Ordinal) || !Unlocks.IsSkinUnlocked(skin.Id))
        {
            var fallback = Skins.ForCharacter(character.Id).FirstOrDefault(value => Unlocks.IsSkinUnlocked(value.Id));
            if (fallback is not null) Session.Profile.SelectedSkinByCharacter[character.Id] = fallback.Id;
        }
    }
}
