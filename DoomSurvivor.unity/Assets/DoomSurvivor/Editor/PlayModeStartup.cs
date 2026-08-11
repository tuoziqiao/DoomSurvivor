using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DoomSurvivor.Editor
{
    /// <summary>
    /// Keeps the Editor Play button on the same entry point as a standalone build.
    /// </summary>
    [InitializeOnLoad]
    public static class PlayModeStartup
    {
        internal const string BootstrapScenePath = "Assets/DoomSurvivor/Scenes/Bootstrap.unity";

        static PlayModeStartup()
        {
            EditorApplication.delayCall += ConfigureStartScene;
        }

        private static void ConfigureStartScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene) == BootstrapScenePath)
                return;

            var bootstrapScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(BootstrapScenePath);
            if (bootstrapScene != null)
                EditorSceneManager.playModeStartScene = bootstrapScene;
        }
    }
}
