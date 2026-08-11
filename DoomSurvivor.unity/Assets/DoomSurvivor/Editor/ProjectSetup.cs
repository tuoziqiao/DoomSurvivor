using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using DoomSurvivor.Gameplay;
using DoomSurvivor.Presentation;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace DoomSurvivor.Editor
{
    public static class ProjectSetup
    {
        private const string Generated = "Assets/DoomSurvivor/Generated";
        private const string Scenes = "Assets/DoomSurvivor/Scenes";
        private const string InputPath = Generated + "/DoomSurvivorInput.asset";
        private const string LegacyInputPath = Generated + "/DoomSurvivorInput.inputactions";
        private const string PanelPath = Generated + "/RuntimePanelSettings.asset";
        private const string RendererPath = Generated + "/DoomSurvivor2DRenderer.asset";
        private const string PipelinePath = Generated + "/DoomSurvivorURP.asset";

        [MenuItem("DoomSurvivor/Run Project Setup")]
        public static void Run()
        {
            EnsureDirectories();
            SyncConfig();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ConfigureTextures();
            EnsureInputActions();
            EnsurePanelSettings();
            EnsureRenderPipeline();
            CreatePrefabs();
            CreateBootstrapScene();
            CreateMainMenuScene();
            CreateBattleScene();
            ConfigureBuildAndPlayer();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ProjectSetup] 完成。工程设置可安全重复执行。");
        }

        [MenuItem("DoomSurvivor/Build/Windows Development")]
        public static void BuildWindowsDevelopment()
        {
            Run();
            BuildWindows(true);
        }

        [MenuItem("DoomSurvivor/Build/Windows Release")]
        public static void BuildWindowsRelease()
        {
            Run();
            BuildWindows(false);
        }

        public static void PackageWindowsRelease()
        {
            var root = Directory.GetParent(Application.dataPath)?.Parent?.FullName ?? throw new InvalidOperationException();
            var folder = Path.Combine(root, "artifacts", "windows", "DoomSurvivor");
            var zip = Path.Combine(root, "artifacts", "windows", "DoomSurvivor-Windows-x64.zip");
            CreatePortableZip(folder, zip);
            Debug.Log($"[ProjectSetup] 便携 ZIP 完成: {zip}");
        }

        public static void PrepareBuildContent()
        {
            EnsureDirectories();
            SyncConfig();
        }

        private static void EnsureDirectories()
        {
            Directory.CreateDirectory(Generated);
            Directory.CreateDirectory(Scenes);
            Directory.CreateDirectory("Assets/DoomSurvivor/Prefabs");
            Directory.CreateDirectory("Assets/StreamingAssets/GameConfig");
        }

        private static void SyncConfig()
        {
            var project = Directory.GetParent(Application.dataPath)?.FullName ?? throw new InvalidOperationException("无法定位工程目录");
            var root = Directory.GetParent(project)?.FullName ?? throw new InvalidOperationException("无法定位仓库目录");
            var target = Path.Combine(Application.dataPath, "StreamingAssets", "GameConfig");
            var candidates = new[]
            {
                Path.Combine(root, "DoomSurvivor.vue", "backend", "mini.game", "shared", "game-config"),
                Path.Combine(root, "backend", "mini.game", "shared", "game-config"),
            };
            var source = candidates.FirstOrDefault(Directory.Exists);
            if (source == null)
            {
                if (Directory.EnumerateFiles(target, "*.json").Any())
                {
                    Debug.Log("[ProjectSetup] 未找到外部 game-config 源目录，使用现有 StreamingAssets/GameConfig。");
                    return;
                }

                throw new DirectoryNotFoundException(
                    $"未找到配置源目录。已尝试: {string.Join("; ", candidates)}；且 {target} 为空。");
            }

            foreach (var file in Directory.EnumerateFiles(source, "*.json"))
                File.Copy(file, Path.Combine(target, Path.GetFileName(file)), true);
        }

        private static void ConfigureTextures()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/DoomSurvivor/Presentation/Resources/Models" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is not TextureImporter importer) continue;
                var changed = importer.textureType != TextureImporterType.Sprite || Math.Abs(importer.spritePixelsPerUnit - 100f) > 0.01f;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100f;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                if (changed) importer.SaveAndReimport();
            }

            ConfigureWeaponTextureFolder("Assets/DoomSurvivor/Presentation/Resources/Art/Weapons/Icons", 256);
            ConfigureWeaponTextureFolder("Assets/DoomSurvivor/Presentation/Resources/Art/Weapons/Battle", 512);
            ConfigureWeaponTextureFolder("Assets/DoomSurvivor/Presentation/Resources/Art/Skills/Icons", 256);
            ConfigureMapTextureFolder("Assets/DoomSurvivor/Presentation/Resources/Art/Map/Props", 1024);
            ConfigureMapTextureFolder("Assets/DoomSurvivor/Presentation/Resources/Art/Map/Effects", 1024);
            ConfigureMapTextureFolder("Assets/DoomSurvivor/Presentation/Resources/Art/Map/Tiles", 1024);
            ConfigureMapTextureFolder("Assets/DoomSurvivor/Presentation/Resources/Art/Map/Environment", 1024);
            ConfigureMapTextureFolder("Assets/DoomSurvivor/Presentation/Resources/Art/Items", 1024);
            ConfigureMapTextureFolder("Assets/DoomSurvivor/Presentation/Resources/Art/Pickups", 1024);
        }

        private static void ConfigureWeaponTextureFolder(string folder, int maxTextureSize)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is not TextureImporter importer) continue;

                var pivot = WeaponSpritePivot(path);
                var changed = importer.textureType != TextureImporterType.Sprite ||
                              importer.spriteImportMode != SpriteImportMode.Single ||
                              Math.Abs(importer.spritePixelsPerUnit - 100f) > 0.01f ||
                              importer.mipmapEnabled ||
                              !importer.alphaIsTransparency ||
                              importer.maxTextureSize != maxTextureSize ||
                              importer.textureCompression != TextureImporterCompression.Uncompressed ||
                              importer.wrapMode != TextureWrapMode.Clamp ||
                              Vector2.SqrMagnitude(importer.spritePivot - pivot) > 0.0001f;

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100f;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.maxTextureSize = maxTextureSize;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.spritePivot = pivot;
                if (changed) importer.SaveAndReimport();
            }
        }

        private static Vector2 WeaponSpritePivot(string path)
        {
            if (path.EndsWith("/Battle/rotating_knife.png", StringComparison.OrdinalIgnoreCase))
                return new Vector2(0.45f, 0.5f);
            if (path.EndsWith("/Battle/drone.png", StringComparison.OrdinalIgnoreCase))
                return new Vector2(0.5f, 0.45f);
            if (path.EndsWith("/Battle/fire_flame.png", StringComparison.OrdinalIgnoreCase))
                return new Vector2(0.5f, 0.1f);
            return new Vector2(0.5f, 0.5f);
        }

        private static void ConfigureMapTextureFolder(string folder, int maxTextureSize)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is not TextureImporter importer) continue;

                var maximum = path.EndsWith("/crate_guide.png", StringComparison.OrdinalIgnoreCase) ? 512 : maxTextureSize;
                var pivot = MapSpritePivot(path);
                var changed = importer.textureType != TextureImporterType.Sprite ||
                              importer.spriteImportMode != SpriteImportMode.Single ||
                              Math.Abs(importer.spritePixelsPerUnit - 100f) > 0.01f ||
                              importer.mipmapEnabled || !importer.alphaIsTransparency ||
                              importer.maxTextureSize != maximum ||
                              importer.textureCompression != TextureImporterCompression.Uncompressed ||
                              importer.wrapMode != TextureWrapMode.Clamp ||
                              Vector2.SqrMagnitude(importer.spritePivot - pivot) > 0.0001f;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100f;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.maxTextureSize = maximum;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.spritePivot = pivot;
                if (changed) importer.SaveAndReimport();
            }
        }

        private static Vector2 MapSpritePivot(string path)
        {
            if (path.EndsWith("/map_crate.png", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith("/map_hidden_crate.png", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith("/map_altar.png", StringComparison.OrdinalIgnoreCase))
                return new Vector2(0.5f, 0f);
            if (path.EndsWith("/player_scooter.png", StringComparison.OrdinalIgnoreCase)) return new Vector2(0.5f, 0.55f);
            if (path.EndsWith("/player_sniper.png", StringComparison.OrdinalIgnoreCase)) return new Vector2(0.15f, 0.55f);
            return new Vector2(0.5f, 0.5f);
        }

        private static InputActionAsset EnsureInputActions()
        {
            if (AssetDatabase.LoadMainAssetAtPath(LegacyInputPath) != null || File.Exists(LegacyInputPath))
                AssetDatabase.DeleteAsset(LegacyInputPath);
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputPath);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<InputActionAsset>();
            var map = asset.AddActionMap("Gameplay");
            var move = map.AddAction("Move", InputActionType.Value);
            move.expectedControlType = "Vector2";
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            move.AddBinding("<Gamepad>/leftStick");
            map.AddAction("Pause", InputActionType.Button, "<Keyboard>/escape");
            map.AddAction("Interact", InputActionType.Button, "<Keyboard>/e");
            AssetDatabase.CreateAsset(asset, InputPath);
            return asset;
        }

        private static PanelSettings EnsurePanelSettings()
        {
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelPath);
            if (panel != null) return panel;
            panel = ScriptableObject.CreateInstance<PanelSettings>();
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1920, 1080);
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = 0.5f;
            panel.sortingOrder = 100;
            AssetDatabase.CreateAsset(panel, PanelPath);
            return panel;
        }

        private static void EnsureRenderPipeline()
        {
            var renderer = AssetDatabase.LoadAssetAtPath<Renderer2DData>(RendererPath);
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance<Renderer2DData>();
                AssetDatabase.CreateAsset(renderer, RendererPath);
            }
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                pipeline = ScriptableObject.CreateInstance<UniversalRenderPipelineAsset>();
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
            }
            var serialized = new SerializedObject(pipeline);
            var list = serialized.FindProperty("m_RendererDataList");
            list.arraySize = 1;
            list.GetArrayElementAtIndex(0).objectReferenceValue = renderer;
            serialized.FindProperty("m_DefaultRendererIndex").intValue = 0;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
            EditorUtility.SetDirty(pipeline);
        }

        private static void CreatePrefabs()
        {
            CreatePrefab("PlayerView", Color.white);
            CreatePrefab("EnemyView", new Color(0.4f, 0.8f, 0.45f));
            CreatePrefab("ProjectileView", new Color(0.5f, 0.9f, 1f));
            CreatePrefab("ExperienceCrystalView", new Color(0.2f, 1f, 0.75f));
        }

        private static void CreatePrefab(string name, Color color)
        {
            var path = $"Assets/DoomSurvivor/Prefabs/{name}.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
            var go = new GameObject(name);
            go.AddComponent<SpriteRenderer>().color = color;
            PrefabUtility.SaveAsPrefabAsset(go, path);
            UnityEngine.Object.DestroyImmediate(go);
        }

        private static void CreateBootstrapScene()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(Scenes + "/Bootstrap.unity") != null) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("AppRoot");
            root.AddComponent<AppRoot>();
            root.AddComponent<ProceduralAudioManager>();
            EditorSceneManager.SaveScene(scene, Scenes + "/Bootstrap.unity");
        }

        private static void CreateMainMenuScene()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(Scenes + "/MainMenu.unity") != null) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var panel = EnsurePanelSettings();
            CreateCamera("Main Camera", new Color(0.025f, 0.04f, 0.04f));
            var ui = new GameObject("MainMenuUI");
            var document = ui.AddComponent<UIDocument>();
            AssignPanelSettings(document, panel);
            ui.AddComponent<MainMenuController>();
            EditorSceneManager.SaveScene(scene, Scenes + "/MainMenu.unity");
        }

        private static void CreateBattleScene()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(Scenes + "/Battle.unity") != null) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var panel = EnsurePanelSettings();
            var input = EnsureInputActions();
            var camera = CreateCamera("Main Camera", new Color(0.12f, 0.18f, 0.12f));
            camera.orthographicSize = 5.4f;
            var root = new GameObject("BattleRuntime");
            var battle = root.AddComponent<BattleController>();
            root.AddComponent<BattleSceneInstaller>().Configure(battle, camera, input);
            var ui = new GameObject("BattleUI");
            var document = ui.AddComponent<UIDocument>();
            AssignPanelSettings(document, panel);
            ui.AddComponent<BattleHudController>().Configure(battle);
            EditorSceneManager.SaveScene(scene, Scenes + "/Battle.unity");
        }

        private static void AssignPanelSettings(UIDocument document, PanelSettings panel)
        {
            if (document == null || panel == null)
                throw new InvalidOperationException("UIDocument 与 PanelSettings 必须存在");

            document.panelSettings = panel;
            var serialized = new SerializedObject(document);
            serialized.Update();
            var property = serialized.FindProperty("m_PanelSettings")
                           ?? throw new InvalidOperationException("Unity UIDocument 缺少 m_PanelSettings 序列化字段");
            property.objectReferenceValue = panel;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(document);
        }

        private static Camera CreateCamera(string name, Color background)
        {
            var go = new GameObject(name) { tag = "MainCamera" };
            var camera = go.AddComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = background;
            camera.transform.position = new Vector3(0, 0, -10);
            go.AddComponent<AudioListener>();
            return camera;
        }

        private static void ConfigureBuildAndPlayer()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(Scenes + "/Bootstrap.unity", true),
                new EditorBuildSettingsScene(Scenes + "/MainMenu.unity", true),
                new EditorBuildSettingsScene(Scenes + "/Battle.unity", true)
            };
            PlayerSettings.companyName = "DoomSurvivor";
            PlayerSettings.productName = "DoomSurvivor";
            PlayerSettings.defaultScreenWidth = 2560;
            PlayerSettings.defaultScreenHeight = 1440;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.Standalone, ApiCompatibilityLevel.NET_Standard);
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);
        }

        private static void BuildWindows(bool development)
        {
            var root = Directory.GetParent(Application.dataPath)?.Parent?.FullName ?? throw new InvalidOperationException();
            var folder = Path.Combine(root, "artifacts", "windows", development ? "DoomSurvivor-Development" : "DoomSurvivor");
            Directory.CreateDirectory(folder);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(value => value.enabled).Select(value => value.path).ToArray(),
                locationPathName = Path.Combine(folder, "DoomSurvivor.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = development ? BuildOptions.Development | BuildOptions.AllowDebugging : BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException($"Windows 构建失败: {report.summary.result}");
            if (!development)
            {
                var zip = Path.Combine(Directory.GetParent(folder)?.FullName ?? root, "DoomSurvivor-Windows-x64.zip");
                CreatePortableZip(folder, zip);
            }
            Debug.Log($"[ProjectSetup] Windows {(development ? "Development" : "Release")} 构建完成: {folder}");
        }

        private static void CreatePortableZip(string source, string zipPath)
        {
            if (!Directory.Exists(source)) throw new DirectoryNotFoundException(source);
            if (File.Exists(zipPath)) File.Delete(zipPath);
            using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);
            foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            {
                var relative = file.Substring(source.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (relative.StartsWith("DoomSurvivor_BackUpThisFolder_ButDontShipItWithYourGame", StringComparison.OrdinalIgnoreCase) ||
                    relative.StartsWith("DoomSurvivor_BurstDebugInformation_DoNotShip", StringComparison.OrdinalIgnoreCase))
                    continue;
                archive.CreateEntryFromFile(file, relative.Replace(Path.DirectorySeparatorChar, '/'),
                    System.IO.Compression.CompressionLevel.Optimal);
            }
        }
    }

    public sealed class ConfigBuildSynchronizer : UnityEditor.Build.IPreprocessBuildWithReport
    {
        public int callbackOrder => -1000;
        public void OnPreprocessBuild(BuildReport report) => ProjectSetup.PrepareBuildContent();
    }
}
