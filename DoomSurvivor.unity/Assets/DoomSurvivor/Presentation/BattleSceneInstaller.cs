using DoomSurvivor.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DoomSurvivor.Presentation
{
    public sealed class BattleSceneInstaller : MonoBehaviour
    {
        [SerializeField] private BattleController battle;
        [SerializeField] private Camera battleCamera;
        [SerializeField] private InputActionAsset inputActions;
        private UnityInputSource inputSource;

        public void Configure(BattleController controller, Camera camera, InputActionAsset actions)
        {
            battle = controller;
            battleCamera = camera;
            inputActions = actions;
        }

        private async void Start()
        {
            while (AppRoot.Instance == null || !AppRoot.Instance.Ready)
                await System.Threading.Tasks.Task.Yield();
            inputSource = new UnityInputSource(inputActions);
            battle.BattleEnded += AppRoot.Instance.RecordResult;
            if (ProceduralAudioManager.Instance != null)
                battle.AudioRequested += ProceduralAudioManager.Instance.Play;
            battle.Initialize(AppRoot.Instance.Session, AppRoot.Instance.StateMachine, inputSource, battleCamera);
        }

        private void OnDestroy()
        {
            if (battle != null && AppRoot.Instance != null)
                battle.BattleEnded -= AppRoot.Instance.RecordResult;
            if (battle != null && ProceduralAudioManager.Instance != null)
                battle.AudioRequested -= ProceduralAudioManager.Instance.Play;
            inputSource?.Dispose();
        }
    }
}
