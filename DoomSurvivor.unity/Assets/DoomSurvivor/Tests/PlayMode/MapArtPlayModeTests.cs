using System.Collections;
using DoomSurvivor.Core;
using DoomSurvivor.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace DoomSurvivor.Tests.PlayMode
{
    public sealed class MapArtPlayModeTests
    {
        [UnityTest]
        public IEnumerator BattleScene_BindsMapAndTemporaryItemArt()
        {
            if (Presentation.AppRoot.Instance == null)
            {
                yield return SceneManager.LoadSceneAsync("Bootstrap");
                for (var attempt = 0; attempt < 100 && (Presentation.AppRoot.Instance == null || !Presentation.AppRoot.Instance.Ready); attempt++)
                    yield return new WaitForSecondsRealtime(0.1f);
            }

            Assert.That(Presentation.AppRoot.Instance?.Ready, Is.True);
            yield return SceneManager.LoadSceneAsync("Battle");
            var battle = Object.FindAnyObjectByType<BattleController>();
            for (var attempt = 0; attempt < 100 && battle != null && !battle.IsInitialized; attempt++)
                yield return new WaitForSecondsRealtime(0.1f);
            Assert.That(battle?.IsInitialized, Is.True);

            var selectedSkin = GameSettings.NormalizeMapSkinId(
                Presentation.AppRoot.Instance.Session.Settings.MapSkinId);
            if (selectedSkin == MapLayoutCatalog.DryHighlandCoastId)
            {
                Assert.That(CountSprites("dry_highland_steppe", "dry_highland_sandstone", "dry_highland_gravel",
                    "dry_highland_path", "dry_highland_shore", "dry_highland_water"), Is.EqualTo(48),
                    "The dry highland coast must be covered by an 8 x 6 authored tile layout.");
                Assert.That(CountSprites("dry_highland_water"), Is.GreaterThan(0),
                    "The authored coast layout must contain impassable water tiles.");
            }
            else
            {
                Assert.That(CountSprites("grass_tile_01", "grass_tile_02", "grass_tile_03", "grass_tile_04"), Is.EqualTo(48),
                    "The 64 x 48 world must be covered by an 8 x 6 grid of the selected map skin.");
                Assert.That(CountSprites(selectedSkin), Is.EqualTo(48),
                    $"Battle must use the configured map skin ({selectedSkin}).");
            }
            Assert.That(CountSprites("forest_edge"), Is.Zero,
                "No forest-edge bushes should be generated around the map perimeter.");
            if (selectedSkin == MapLayoutCatalog.DryHighlandCoastId)
            {
                Assert.That(CountSprites("tree_cluster", "bush"), Is.Zero,
                    "The dry highland coast must not reintroduce dense green vegetation.");
                Assert.That(CountSprites("rock"), Is.EqualTo(4));
                Assert.That(CountSprites("tree_stump"), Is.EqualTo(4));
            }
            else
            {
                Assert.That(CountSprites("tree_cluster"), Is.EqualTo(10));
                Assert.That(CountSprites("bush"), Is.EqualTo(8));
                Assert.That(CountSprites("rock"), Is.EqualTo(6));
                Assert.That(CountSprites("tree_stump"), Is.EqualTo(4));
            }

            var expected = new[] { "map_crate", "map_hidden_crate", "map_altar", "chicken_leg", "poison_fog", "poison_smoke_puff", "map_event_aura" };
            foreach (var spriteName in expected)
            {
                var found = false;
                foreach (var renderer in Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include))
                    if (renderer.sprite != null && renderer.sprite.name == spriteName) { found = true; break; }
                Assert.That(found, Is.True, $"Battle 必须绑定地图正式 Sprite: {spriteName}");
            }

            battle.DebugGrantTemporaryItem("scooter_boost");
            battle.DebugGrantTemporaryItem("sniper_rifle");
            battle.DebugGrantTemporaryItem("crate_guide");
            battle.DebugSpawnElite();
            battle.DebugGrantTemporaryItem("capsule_football");
            yield return new WaitForSecondsRealtime(0.1f);
            Assert.That(battle.ScooterRemaining, Is.GreaterThan(0f));
            Assert.That(battle.SniperRemaining, Is.GreaterThan(0f));
            Assert.That(battle.CrateGuideRemaining, Is.GreaterThan(0f));
            Assert.That(battle.CapsuleFootballRemaining, Is.GreaterThan(0f));
            Assert.That(FindSprite("player_scooter"), Is.True);
            Assert.That(FindSprite("player_sniper"), Is.True);
            Assert.That(FindSprite("capsule_football_belt"), Is.True);
            Assert.That(FindSprite("capsule_football_ball"), Is.True);
            Assert.That(battle.IsCrateGuideActive, Is.True);
            var document = Object.FindAnyObjectByType<UIDocument>();
            Assert.That(document?.rootVisualElement.Q<Image>("crate-guide-marker-0")?.sprite, Is.Not.Null);
            Assert.That(document?.rootVisualElement.Q("effect-chip-scooter_boost"), Is.Not.Null);
            Assert.That(document?.rootVisualElement.Q("effect-chip-sniper_rifle"), Is.Not.Null);
            var guideChip = document?.rootVisualElement.Q("effect-chip-crate_guide");
            Assert.That(guideChip, Is.Not.Null);
            Assert.That(guideChip.Q<Label>("effect-title")?.text, Does.Contain("追踪眼镜"));
            Assert.That(guideChip.Q<Label>("effect-detail")?.text, Does.Contain("显示补给箱方位"));
            var footballChip = document?.rootVisualElement.Q("effect-chip-capsule_football");
            Assert.That(footballChip, Is.Not.Null);
            Assert.That(footballChip.Q<Label>("effect-title")?.text, Does.Contain("胶囊足球"));
            Assert.That(footballChip.Q<Label>("effect-detail")?.text, Does.Contain("最多命中 3 人"));
            Assert.That(document?.rootVisualElement.Q("effect-bar")?.style.display.value, Is.EqualTo(DisplayStyle.Flex));

            battle.DebugSpawnCrystal();
            yield return null;
            Assert.That(FindSprite("experience_crystal"), Is.True,
                "经验晶体必须使用正式经验胶囊 Sprite");
        }

        [UnityTest]
        public IEnumerator CapsuleFootball_DamagesThreeTargets_KnocksBackMobs_AndDoesNotExecuteOrMoveBosses()
        {
            if (Presentation.AppRoot.Instance == null)
            {
                yield return SceneManager.LoadSceneAsync("Bootstrap");
                for (var attempt = 0; attempt < 100 &&
                     (Presentation.AppRoot.Instance == null || !Presentation.AppRoot.Instance.Ready); attempt++)
                    yield return new WaitForSecondsRealtime(0.1f);
            }

            yield return SceneManager.LoadSceneAsync("Battle");
            var battle = Object.FindAnyObjectByType<BattleController>();
            for (var attempt = 0; attempt < 100 && battle != null && !battle.IsInitialized; attempt++)
                yield return new WaitForSecondsRealtime(0.1f);
            Assert.That(battle?.IsInitialized, Is.True);

            battle.DebugDisableWeaponsForTests();
            battle.DebugClearEnemies();
            var origin = battle.DebugPlayerPosition;
            var direction = new Vector2(0.92388f, 0.38268f);
            var mobs = new EnemyRuntime[4];
            for (var i = 0; i < mobs.Length; i++)
                mobs[i] = battle.DebugSpawnEnemyAt("zombie_elite", origin + direction * (0.8f + i * 0.2f));
            var nearestStart = mobs[0].Position;

            battle.DebugGrantTemporaryItem("capsule_football");
            yield return new WaitForSecondsRealtime(0.35f);

            var damagedMobCount = 0;
            foreach (var mob in mobs)
                if (mob.Hp < mob.MaxHp) damagedMobCount++;
            Assert.That(damagedMobCount, Is.EqualTo(3), "单球必须只命中三个不同目标");
            Assert.That(Vector2.Distance(mobs[0].Position, origin),
                Is.GreaterThan(Vector2.Distance(nearestStart, origin) + 0.45f), "普通敌人必须被明显击退");

            battle.DebugClearEnemies();
            var bosses = new EnemyRuntime[4];
            for (var i = 0; i < bosses.Length; i++)
                bosses[i] = battle.DebugSpawnEnemyAt("boss_mutant_giant", origin + direction * (0.8f + i * 0.2f), true);
            battle.DebugGrantTemporaryItem("capsule_football");
            yield return new WaitForSecondsRealtime(0.35f);

            Assert.That(bosses[0].Hp, Is.LessThan(bosses[0].MaxHp), "Boss 必须受到足球伤害");
            foreach (var boss in bosses)
                Assert.That(boss.KnockbackDurationRemaining, Is.Zero, "Boss 不得进入足球击退状态");

            battle.DebugClearEnemies();
            var elites = new EnemyRuntime[4];
            for (var i = 0; i < elites.Length; i++)
                elites[i] = battle.DebugSpawnEnemyAt("zombie_elite", origin + direction * (0.8f + i * 0.2f));
            battle.DebugGrantTemporaryItem("sniper_rifle");
            battle.DebugGrantTemporaryItem("capsule_football");
            yield return new WaitForSecondsRealtime(0.35f);

            Assert.That(elites[0].Active, Is.True, "足球命中不得触发狙击枪的一击必杀");
            Assert.That(elites[0].Hp, Is.LessThan(elites[0].MaxHp));
        }

        private static bool FindSprite(string name)
        {
            foreach (var renderer in Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include))
                if (renderer.sprite != null && renderer.sprite.name == name) return true;
            return false;
        }

        private static int CountSprites(params string[] names)
        {
            var count = 0;
            foreach (var renderer in Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include))
                if (renderer.sprite != null && System.Array.IndexOf(names, renderer.sprite.name) >= 0) count++;
            return count;
        }
    }
}
