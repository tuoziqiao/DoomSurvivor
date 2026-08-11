using System.Collections;
using System.Linq;
using DoomSurvivor.Core;
using DoomSurvivor.Gameplay;
using DoomSurvivor.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace DoomSurvivor.Tests.PlayMode
{
    public sealed class SceneSmokeTests
    {
        [UnityTest]
        public IEnumerator BootstrapScene_CreatesPersistentAppRoot()
        {
            yield return SceneManager.LoadSceneAsync("Bootstrap");
            yield return null;
            Assert.That(Object.FindAnyObjectByType<AppRoot>(), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator BattleScene_ContainsRuntimeAndHudBridge()
        {
            if (AppRoot.Instance == null)
            {
                yield return SceneManager.LoadSceneAsync("Bootstrap");
                for (var attempt = 0; attempt < 100 && (AppRoot.Instance == null || !AppRoot.Instance.Ready); attempt++)
                    yield return new WaitForSecondsRealtime(0.1f);
            }

            Assert.That(AppRoot.Instance?.Ready, Is.True, "Battle 测试等待应用配置初始化超时");
            yield return SceneManager.LoadSceneAsync("Battle");
            var battle = Object.FindAnyObjectByType<BattleController>();
            for (var attempt = 0; attempt < 100 && battle != null && !battle.IsInitialized; attempt++)
                yield return new WaitForSecondsRealtime(0.1f);

            Assert.That(battle, Is.Not.Null);
            Assert.That(battle.IsInitialized, Is.True, "Battle 场景初始化超时");
            Assert.That(Object.FindAnyObjectByType<BattleSceneInstaller>(), Is.Not.Null);
            Assert.That(Object.FindAnyObjectByType<BattleHudController>(), Is.Not.Null);
            var document = Object.FindAnyObjectByType<UIDocument>();
            Assert.That(document?.panelSettings, Is.Not.Null,
                "Battle UIDocument 必须引用运行时 PanelSettings");
            for (var attempt = 0; attempt < 60 && document?.rootVisualElement.Q("weapon-bar") == null; attempt++)
                yield return null;
            var startingWeapon = System.Linq.Enumerable.First(
                System.Linq.Enumerable.Where(battle.OwnedUpgrades.Values, value => value.Kind == UpgradeKind.Weapon));
            Assert.That(document?.rootVisualElement.Q("weapon-bar"), Is.Not.Null, "HUD 必须创建武器栏");
            Assert.That(document?.rootVisualElement.Q<Image>($"weapon-icon-{startingWeapon.Id}")?.sprite, Is.Not.Null,
                "HUD 必须显示初始武器图标");
            Assert.That(document?.rootVisualElement.Q("player-status"), Is.Not.Null, "HUD 必须创建角色状态行");
            Assert.That(document?.rootVisualElement.Q("effect-bar"), Is.Not.Null, "HUD 必须创建道具效果行");
            Assert.That(document?.rootVisualElement.Q("boss-bars"), Is.Not.Null, "HUD 必须创建 Boss 血条区域");
            var waveLabel = document?.rootVisualElement.Q<Label>("wave-label");
            for (var attempt = 0; attempt < 30 && waveLabel != null && !waveLabel.text.Contains("波"); attempt++)
                yield return null;
            Assert.That(waveLabel?.text, Does.Contain($"第 {battle.CurrentWave}/{battle.TotalWaveCount} 波"));
            Assert.That(waveLabel?.text, Does.Not.Match(@"\d{2}:\d{2}"), "波次 HUD 不应再显示倒计时");

            System.Collections.Generic.IReadOnlyList<UpgradeOffer> offers = null;
            void CaptureOffers(System.Collections.Generic.IReadOnlyList<UpgradeOffer> value, bool _) => offers = value;
            battle.LevelUpRequested += CaptureOffers;
            battle.DebugAddExperience();
            UpgradeOffer weaponOffer = null;
            for (var attempt = 0; attempt < 10 && weaponOffer == null; attempt++)
            {
                if (offers != null)
                {
                    foreach (var offer in offers)
                    {
                        if (offer.Kind != UpgradeKind.Weapon) continue;
                        weaponOffer = offer;
                        break;
                    }
                }
                if (weaponOffer != null) break;
                if (battle.State == GameState.LevelUp)
                {
                    battle.RefreshUpgradeOffers();
                    yield return null;
                    if (offers != null)
                    {
                        foreach (var offer in offers)
                        {
                            if (offer.Kind != UpgradeKind.Weapon) continue;
                            weaponOffer = offer;
                            break;
                        }
                    }
                    if (weaponOffer == null) battle.SkipUpgrade();
                }
                else
                {
                    battle.DebugAddExperience();
                }
                yield return null;
            }
            battle.LevelUpRequested -= CaptureOffers;
            Assert.That(weaponOffer, Is.Not.Null, "升级卡测试必须获得至少一个武器候选");
            Assert.That(document?.rootVisualElement.Q<Image>($"upgrade-icon-{weaponOffer.Id}")?.sprite, Is.Not.Null,
                "武器升级卡必须显示正式图标");
        }

        [UnityTest]
        public IEnumerator BattleScene_UsesAuthoredWeaponSpritesAndLandsFireBottle()
        {
            if (AppRoot.Instance == null)
            {
                yield return SceneManager.LoadSceneAsync("Bootstrap");
                for (var attempt = 0; attempt < 100 && (AppRoot.Instance == null || !AppRoot.Instance.Ready); attempt++)
                    yield return new WaitForSecondsRealtime(0.1f);
            }

            Assert.That(AppRoot.Instance?.Ready, Is.True, "战斗素材测试等待应用配置初始化超时");
            yield return SceneManager.LoadSceneAsync("Battle");
            var battle = Object.FindAnyObjectByType<BattleController>();
            for (var attempt = 0; attempt < 100 && battle != null && !battle.IsInitialized; attempt++)
                yield return new WaitForSecondsRealtime(0.1f);
            Assert.That(battle?.IsInitialized, Is.True, "Battle 场景初始化超时");

            for (var i = 0; i < 40; i++) battle.DebugAddRandomWeapon();
            battle.DebugSpawnElite();
            battle.DebugCycleSpeed();
            battle.DebugCycleSpeed();
            yield return null;

            var document = Object.FindAnyObjectByType<UIDocument>();
            foreach (var id in new[] { "wind_blade", "rotating_knife", "fire_bottle", "lightning_chain", "drone" })
                Assert.That(document?.rootVisualElement.Q<Image>($"weapon-icon-{id}")?.sprite, Is.Not.Null,
                    $"HUD 必须显示已拥有武器图标: {id}");

            var sawWindBlade = false;
            var sawKnife = false;
            var sawDrone = false;
            var sawDroneBolt = false;
            var sawFireBottle = false;
            var sawFireBottleBeforeZone = false;
            var sawFireZone = false;
            var sawFireFlame = false;
            for (var attempt = 0; attempt < 120; attempt++)
            {
                foreach (var renderer in Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Exclude))
                {
                    switch (renderer.sprite?.name)
                    {
                        case "wind_blade": sawWindBlade = true; break;
                        case "rotating_knife": sawKnife = true; break;
                        case "drone": sawDrone = true; break;
                        case "drone_bolt": sawDroneBolt = true; break;
                        case "fire_bottle":
                            sawFireBottle = true;
                            if (!sawFireZone) sawFireBottleBeforeZone = true;
                            break;
                        case "fire_zone": sawFireZone = true; break;
                        case "fire_flame": sawFireFlame = true; break;
                    }
                }

                if (sawWindBlade && sawKnife && sawDrone && sawDroneBolt && sawFireBottle &&
                    sawFireBottleBeforeZone && sawFireZone && sawFireFlame)
                    break;
                yield return new WaitForSecondsRealtime(0.05f);
            }

            Assert.That(sawWindBlade, Is.True, "风刃必须使用正式 Sprite");
            Assert.That(sawKnife, Is.True, "旋转短刀必须使用正式 Sprite");
            Assert.That(sawDrone, Is.True, "无人机必须使用正式 Sprite");
            Assert.That(sawDroneBolt, Is.True, "无人机弹必须使用正式 Sprite");
            Assert.That(sawFireBottle, Is.True, "火焰瓶必须显示飞行 Sprite");
            Assert.That(sawFireBottleBeforeZone, Is.True, "火焰瓶必须先飞行再生成燃烧区");
            Assert.That(sawFireZone, Is.True, "火焰瓶落地后必须使用正式火圈 Sprite");
            Assert.That(sawFireFlame, Is.True, "燃烧区必须使用正式火舌 Sprite");

            yield return SceneManager.LoadSceneAsync("MainMenu");
            yield return null;
            Assert.That(Object.FindAnyObjectByType<BattleController>(), Is.Null,
                "退出 Battle 后不得残留战斗控制器或对象池");
        }

        [UnityTest]
        public IEnumerator MainMenuScene_HasRenderableUiPanel()
        {
            if (AppRoot.Instance == null)
            {
                yield return SceneManager.LoadSceneAsync("Bootstrap");
                for (var frame = 0; frame < 600 && (AppRoot.Instance == null || !AppRoot.Instance.Ready); frame++)
                    yield return null;
            }
            else
            {
                yield return SceneManager.LoadSceneAsync("MainMenu");
            }

            for (var attempt = 0; attempt < 100 && (AppRoot.Instance == null || !AppRoot.Instance.Ready); attempt++)
                yield return new WaitForSecondsRealtime(0.1f);

            Assert.That(AppRoot.Instance?.Ready, Is.True, "首页测试等待应用配置初始化超时");

            for (var attempt = 0; attempt < 60 &&
                                Object.FindAnyObjectByType<UIDocument>()?.rootVisualElement.Q("main-menu-root") == null;
                 attempt++)
                yield return new WaitForSecondsRealtime(0.05f);

            var document = Object.FindAnyObjectByType<UIDocument>();
            Assert.That(document, Is.Not.Null);
            Assert.That(document.panelSettings, Is.Not.Null,
                "MainMenu UIDocument 必须引用运行时 PanelSettings，否则 Player 只显示背景");

            var root = document.rootVisualElement;
            Assert.That(root.Q("main-menu-root"), Is.Not.Null, "首页主视觉必须完成构建");
            Assert.That(root.Q<Button>("mode-normal-button"), Is.Not.Null, "首页必须保留正常模式入口");
            Assert.That(root.Q<Button>("mode-quick-button"), Is.Not.Null, "首页必须保留快速测试入口");
            Assert.That(root.Q<Button>("settings-button"), Is.Not.Null, "首页必须保留设置入口");
            Assert.That(root.Q<Button>("display-mode-windowed")?.text, Is.EqualTo("窗口模式"),
                "首页必须提供窗口模式选择");
            Assert.That(root.Q<Button>("display-mode-fullscreen")?.text, Is.EqualTo("全屏模式"),
                "首页必须提供全屏模式选择");
            Assert.That(root.Q("survivor-preview"), Is.Not.Null, "首页必须显示幸存者预览卡");
            Assert.That(root.Q("character-rail"), Is.Not.Null, "首页必须显示可直接选择的幸存者席位");
            Assert.That(root.Query<Button>().ToList().Count(button => button.name.StartsWith("character-slot-")),
                Is.EqualTo(AppRoot.Instance.Session.Config.Characters.Characters.Count), "幸存者席位数量必须与角色配置一致");
            Assert.That(root.Q<Image>("character-portrait")?.sprite, Is.Not.Null, "首页必须加载角色立绘");
        }
    }
}
