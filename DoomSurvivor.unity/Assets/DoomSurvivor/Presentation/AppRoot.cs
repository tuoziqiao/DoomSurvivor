using System;
using System.Linq;
using System.Threading.Tasks;
using DoomSurvivor.Core;
using DoomSurvivor.Infrastructure;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DoomSurvivor.Presentation
{
    public sealed class AppRoot : MonoBehaviour
    {
        private static AppRoot instance;
        private SaveService saveService;

        public static AppRoot Instance => instance;
        public GameSession Session { get; } = new();
        public GameStateMachine StateMachine { get; } = new();
        public bool Ready { get; private set; }
        public string StartupError { get; private set; } = string.Empty;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private async void Start()
        {
            if (instance != this) return;
            await InitializeAsync();
        }

        public async Task InitializeAsync()
        {
            if (Ready) return;
            try
            {
                StateMachine.Set(GameState.Loading);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                const bool remoteEnabled = true;
#else
                const bool remoteEnabled = false;
#endif
                var configService = new ConfigService(remoteEnabled, "http://localhost:5266");
                saveService = new SaveService();
                var configTask = configService.LoadAsync();
                var profileTask = saveService.LoadProfileAsync();
                var settingsTask = saveService.LoadSettingsAsync();
                await Task.WhenAll(configTask, profileTask, settingsTask);

                Session.Config = configTask.Result;
                Session.ConfigSource = configService.Source;
                Session.Profile = profileTask.Result;
                Session.Settings = settingsTask.Result;
                Session.Settings.Clamp();
                DisplaySettingsService.Apply(Session.Settings);
                EnsureSelectionIsValid();
                Ready = true;
                StateMachine.Set(GameState.MainMenu);
                if (SceneManager.GetActiveScene().name != "MainMenu")
                    await SceneManager.LoadSceneAsync("MainMenu");
            }
            catch (Exception exception)
            {
                StartupError = exception.ToString();
                Debug.LogException(exception);
            }
        }

        public void StartGame(GameMode mode, string characterId, string skinId)
        {
            if (!Ready) return;
            Session.Launch = new GameLaunchOptions
            {
                Mode = mode,
                CharacterId = characterId,
                SkinId = skinId,
                StageId = Session.Config.Stages.Stages[0].Id
            };
            Session.Profile.SelectedCharacterId = characterId;
            Session.Profile.SelectedSkinByCharacter[characterId] = skinId;
            _ = saveService.SaveProfileAsync(Session.Profile);
            SceneManager.LoadScene("Battle");
        }

        public async void RecordResult(GameResultStats result)
        {
            Session.LastResult = result;
            Session.Profile.LastResult = result;
            Session.Profile.MaxKills = Math.Max(Session.Profile.MaxKills, result.KillCount);
            Session.Profile.MaxLevel = Math.Max(Session.Profile.MaxLevel, result.MaxLevel);
            Session.Profile.MaxSurvivalTime = Math.Max(Session.Profile.MaxSurvivalTime, result.SurvivalTime);
            await saveService.SaveProfileAsync(Session.Profile);
            StateMachine.Set(GameState.Result);
            SceneManager.LoadScene("MainMenu");
        }

        public async void SaveSettings()
        {
            Session.Settings.Clamp();
            DisplaySettingsService.Apply(Session.Settings);
            await saveService.SaveSettingsAsync(Session.Settings);
        }

        public async Task ClearSaveAsync()
        {
            await saveService.ClearAsync();
            Session.Profile = await saveService.LoadProfileAsync();
            Session.LastResult = null;
            EnsureSelectionIsValid();
        }

        public void ReturnToMenu()
        {
            StateMachine.Set(GameState.MainMenu);
            SceneManager.LoadScene("MainMenu");
        }

        private void EnsureSelectionIsValid()
        {
            var profile = Session.Profile;
            if (Session.Config.Characters.Characters.All(value => value.Id != profile.SelectedCharacterId))
                profile.SelectedCharacterId = Session.Config.Characters.Characters[0].Id;
            var character = Session.Config.Characters.Characters.First(value => value.Id == profile.SelectedCharacterId);
            if (!profile.SelectedSkinByCharacter.TryGetValue(character.Id, out var skinId) ||
                Session.Config.Skins.Skins.All(value => value.Id != skinId || value.CharacterId != character.Id))
                profile.SelectedSkinByCharacter[character.Id] = character.DefaultSkinId;
        }
    }
}
