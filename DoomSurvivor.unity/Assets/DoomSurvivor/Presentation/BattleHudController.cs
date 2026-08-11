using System.Collections.Generic;
using System.Linq;
using DoomSurvivor.Core;
using DoomSurvivor.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace DoomSurvivor.Presentation
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class BattleHudController : MonoBehaviour
    {
        private sealed class DamageNumberVisual
        {
            public Label Label;
            public Vector2 WorldPosition;
            public float Age;
        }

        private static readonly string[] WeaponDisplayOrder =
        {
            "wind_blade", "rotating_knife", "fubo_qin", "fire_bottle", "lightning_chain", "drone"
        };

        private static readonly Color HpFill = new(0.78f, 0.22f, 0.28f, 0.95f);
        private static readonly Color ExpFill = new(0.28f, 0.72f, 0.48f, 0.95f);
        private static readonly Color BossFill = new(0.92f, 0.28f, 0.22f, 0.95f);
        private static readonly Color TrackBg = new(1f, 1f, 1f, 0.18f);
        private static readonly Color LevelUpTitle = new(0.96f, 0.88f, 0.52f);
        private static readonly Color LevelUpCardIdle = new(0.09f, 0.14f, 0.12f, 0.96f);
        private static readonly Color LevelUpCardSelected = new(0.14f, 0.24f, 0.18f, 0.98f);
        private static readonly Color LevelUpBorderIdle = new(0.32f, 0.48f, 0.38f, 0.55f);
        private static readonly Color LevelUpBorderSelected = new(0.95f, 0.82f, 0.38f, 1f);

        [SerializeField] private BattleController battle;
        private Label levelLabel;
        private Label fpsLabel;
        private Label attackLabel;
        private Label moveSpeedLabel;
        private Label hpValueLabel;
        private Label expValueLabel;
        private VisualElement hpFill;
        private VisualElement expFill;
        private Label waveLabel;
        private Label toast;
        private VisualElement effectBar;
        private VisualElement effectChips;
        private VisualElement bossBars;
        private VisualElement overlay;
        private VisualElement debugPanel;
        private Label debugInfoLabel;
        private bool debugPanelVisible;
        private VisualElement weaponBar;
        private VisualElement crateGuideLayer;
        private VisualElement damageNumberLayer;
        private readonly Image[] crateGuideMarkers = new Image[16];
        private readonly List<DamageNumberVisual> damageNumbers = new(32);
        private readonly Dictionary<string, VisualElement> bossBarMap = new();
        private readonly Dictionary<string, VisualElement> effectChipMap = new();
        private float toastRemaining;
        private IReadOnlyList<UpgradeOffer> lastLevelUpOffers = System.Array.Empty<UpgradeOffer>();
        private bool lastLevelUpCanRefresh;
        private bool levelUpUiActive;
        private bool levelUpFocusActions;
        private int levelUpCardIndex;
        private int levelUpActionIndex;
        private int levelUpConfirmFrame = -1;
        private readonly List<Button> levelUpCards = new(4);
        private readonly List<Button> levelUpActionButtons = new(2);
        private readonly List<System.Action> levelUpActionCallbacks = new(2);

        public void Configure(BattleController controller) => battle = controller;

        private void Start()
        {
            Build(GetComponent<UIDocument>().rootVisualElement);
            battle.SnapshotChanged += UpdateSnapshot;
            battle.LevelUpRequested += ShowLevelUp;
            battle.StateChanged += HandleState;
            battle.ToastRequested += ShowToast;
            battle.DamageNumberRequested += ShowDamageNumber;
            battle.UpgradesChanged += RefreshWeaponBar;
            RefreshWeaponBar();
        }

        private void OnDestroy()
        {
            if (battle == null) return;
            battle.SnapshotChanged -= UpdateSnapshot;
            battle.LevelUpRequested -= ShowLevelUp;
            battle.StateChanged -= HandleState;
            battle.ToastRequested -= ShowToast;
            battle.DamageNumberRequested -= ShowDamageNumber;
            battle.UpgradesChanged -= RefreshWeaponBar;
        }

        private void Update()
        {
            if (Keyboard.current?.f2Key.wasPressedThisFrame == true)
                ToggleDebugPanel();
            if (debugPanelVisible)
                HandleDebugHotkeys();
            if (levelUpUiActive)
                HandleLevelUpHotkeys();
            if (toastRemaining > 0f)
            {
                toastRemaining -= Time.unscaledDeltaTime;
                if (toastRemaining <= 0f)
                {
                    toast.style.display = DisplayStyle.None;
                    RefreshEffectBarVisibility();
                }
            }
            UpdateCrateGuideMarkers();
            UpdateDamageNumbers();
        }

        private void HandleLevelUpHotkeys()
        {
            if (battle == null || Keyboard.current == null) return;
            var keyboard = Keyboard.current;
            if (keyboard.leftArrowKey.wasPressedThisFrame)
                MoveLevelUpSelection(-1, 0);
            else if (keyboard.rightArrowKey.wasPressedThisFrame)
                MoveLevelUpSelection(1, 0);
            else if (keyboard.upArrowKey.wasPressedThisFrame)
                MoveLevelUpSelection(0, -1);
            else if (keyboard.downArrowKey.wasPressedThisFrame)
                MoveLevelUpSelection(0, 1);
            else if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
                ConfirmLevelUpSelection();
        }

        private void MoveLevelUpSelection(int deltaX, int deltaY)
        {
            if (deltaY < 0 && levelUpFocusActions)
            {
                levelUpFocusActions = false;
                levelUpCardIndex = Mathf.Clamp(levelUpCardIndex, 0, Mathf.Max(0, levelUpCards.Count - 1));
                ApplyLevelUpSelectionVisuals();
                return;
            }

            if (deltaY > 0 && !levelUpFocusActions && levelUpActionButtons.Count > 0)
            {
                levelUpFocusActions = true;
                if (levelUpCards.Count > 0 && levelUpActionButtons.Count > 0)
                {
                    var ratio = (levelUpCardIndex + 0.5f) / levelUpCards.Count;
                    levelUpActionIndex = Mathf.Clamp(Mathf.FloorToInt(ratio * levelUpActionButtons.Count),
                        0, levelUpActionButtons.Count - 1);
                }
                ApplyLevelUpSelectionVisuals();
                return;
            }

            if (levelUpFocusActions)
            {
                if (levelUpActionButtons.Count == 0) return;
                var count = levelUpActionButtons.Count;
                levelUpActionIndex = (levelUpActionIndex + deltaX % count + count) % count;
            }
            else
            {
                if (levelUpCards.Count == 0) return;
                var count = levelUpCards.Count;
                levelUpCardIndex = (levelUpCardIndex + deltaX % count + count) % count;
            }
            ApplyLevelUpSelectionVisuals();
        }

        private void FocusLevelUpCard(int index)
        {
            if (levelUpCards.Count == 0) return;
            levelUpFocusActions = false;
            levelUpCardIndex = Mathf.Clamp(index, 0, levelUpCards.Count - 1);
            ApplyLevelUpSelectionVisuals();
        }

        private void FocusLevelUpAction(int index)
        {
            if (levelUpActionButtons.Count == 0) return;
            levelUpFocusActions = true;
            levelUpActionIndex = Mathf.Clamp(index, 0, levelUpActionButtons.Count - 1);
            ApplyLevelUpSelectionVisuals();
        }

        private void ConfirmLevelUpSelection()
        {
            if (battle == null) return;
            levelUpConfirmFrame = Time.frameCount;
            if (levelUpFocusActions)
            {
                if (levelUpActionIndex < 0 || levelUpActionIndex >= levelUpActionCallbacks.Count) return;
                levelUpActionCallbacks[levelUpActionIndex]?.Invoke();
                return;
            }

            if (lastLevelUpOffers.Count == 0) return;
            var index = Mathf.Clamp(levelUpCardIndex, 0, lastLevelUpOffers.Count - 1);
            battle.ApplyUpgrade(lastLevelUpOffers[index]);
        }

        private void ApplyLevelUpSelectionVisuals()
        {
            for (var i = 0; i < levelUpCards.Count; i++)
                StyleUpgradeCard(levelUpCards[i], !levelUpFocusActions && i == levelUpCardIndex);
            for (var i = 0; i < levelUpActionButtons.Count; i++)
                StyleLevelUpActionButton(levelUpActionButtons[i], levelUpFocusActions && i == levelUpActionIndex);
        }

        private void ToggleDebugPanel()
        {
            if (debugPanel == null) return;
            debugPanelVisible = !debugPanelVisible;
            debugPanel.style.display = debugPanelVisible ? DisplayStyle.Flex : DisplayStyle.None;
            if (debugPanelVisible) debugPanel.BringToFront();
        }

        private void HandleDebugHotkeys()
        {
            if (battle == null || Keyboard.current == null) return;
            var keyboard = Keyboard.current;
            if (keyboard.iKey.wasPressedThisFrame) battle.DebugToggleInvincible();
            else if (keyboard.xKey.wasPressedThisFrame) battle.DebugAddExperience();
            else if (keyboard.rKey.wasPressedThisFrame) battle.DebugAddRandomWeapon();
            else if (keyboard.tKey.wasPressedThisFrame) battle.DebugCycleSpeed();
            else if (keyboard.gKey.wasPressedThisFrame) battle.DebugSpawnElite();
            else if (keyboard.bKey.wasPressedThisFrame) battle.DebugSpawnBoss();
            else if (keyboard.kKey.wasPressedThisFrame) battle.DebugClearEnemies();
            else if (keyboard.hKey.wasPressedThisFrame) battle.DebugHealFull();
            else if (keyboard.oKey.wasPressedThisFrame) battle.DebugGrantTemporaryItem("scooter_boost");
            else if (keyboard.nKey.wasPressedThisFrame) battle.DebugGrantTemporaryItem("sniper_rifle");
            else if (keyboard.mKey.wasPressedThisFrame) battle.DebugGrantTemporaryItem("crate_guide");
            else if (keyboard.pKey.wasPressedThisFrame) battle.DebugGrantTemporaryItem("capsule_football");
            else if (keyboard.fKey.wasPressedThisFrame) battle.DebugGrantMaxLevelWeapon("rotating_knife");
            else if (keyboard.vKey.wasPressedThisFrame) battle.DebugGrantMaxLevelWeapon("fubo_qin");
        }

        private void Build(VisualElement root)
        {
            root.Clear();
            root.style.flexGrow = 1;
            root.style.paddingLeft = 16;
            root.style.paddingRight = 16;
            root.style.paddingTop = 12;
            root.style.paddingBottom = 12;
            root.pickingMode = PickingMode.Ignore;

            var topBar = new VisualElement { name = "battle-top-bar" };
            topBar.style.flexDirection = FlexDirection.Row;
            topBar.style.alignItems = Align.FlexStart;
            topBar.style.justifyContent = Justify.SpaceBetween;
            topBar.style.width = Length.Percent(100);
            topBar.pickingMode = PickingMode.Ignore;

            topBar.Add(BuildLeftColumn());
            topBar.Add(BuildRightColumn());
            root.Add(topBar);

            crateGuideLayer = new VisualElement { name = "crate-guide-layer" };
            crateGuideLayer.style.position = Position.Absolute;
            crateGuideLayer.style.left = 0;
            crateGuideLayer.style.right = 0;
            crateGuideLayer.style.top = 0;
            crateGuideLayer.style.bottom = 0;
            crateGuideLayer.pickingMode = PickingMode.Ignore;
            var guideSprite = MapArtCatalog.LoadItem("crate_guide");
            for (var i = 0; i < crateGuideMarkers.Length; i++)
            {
                var marker = new Image { name = $"crate-guide-marker-{i}", sprite = guideSprite, scaleMode = ScaleMode.ScaleToFit };
                marker.style.position = Position.Absolute;
                marker.style.width = 54;
                marker.style.height = 72;
                marker.style.display = DisplayStyle.None;
                marker.pickingMode = PickingMode.Ignore;
                crateGuideMarkers[i] = marker;
                crateGuideLayer.Add(marker);
            }
            root.Add(crateGuideLayer);

            damageNumberLayer = new VisualElement { name = "damage-number-layer" };
            damageNumberLayer.style.position = Position.Absolute;
            damageNumberLayer.style.left = 0;
            damageNumberLayer.style.right = 0;
            damageNumberLayer.style.top = 0;
            damageNumberLayer.style.bottom = 0;
            damageNumberLayer.pickingMode = PickingMode.Ignore;
            root.Add(damageNumberLayer);

            overlay = new VisualElement();
            overlay.style.position = Position.Absolute;
            overlay.style.left = 0;
            overlay.style.right = 0;
            overlay.style.top = 0;
            overlay.style.bottom = 0;
            overlay.style.alignItems = Align.Center;
            overlay.style.justifyContent = Justify.Center;
            overlay.style.backgroundColor = new Color(0.01f, 0.02f, 0.02f, 0.82f);
            overlay.style.display = DisplayStyle.None;
            overlay.pickingMode = PickingMode.Position;
            root.Add(overlay);

            debugPanel = new VisualElement { name = "debug-panel" };
            debugPanel.style.position = Position.Absolute;
            debugPanel.style.left = 16;
            debugPanel.style.bottom = 16;
            debugPanel.style.right = StyleKeyword.Auto;
            debugPanel.style.top = StyleKeyword.Auto;
            debugPanel.style.width = 320;
            debugPanel.style.backgroundColor = Color.clear;
            debugPanel.style.display = DisplayStyle.None;
            debugPanel.pickingMode = PickingMode.Ignore;
            var debugTitle = UiFactory.Label("F2 调试", 17, new Color(0.95f, 0.9f, 0.65f));
            debugTitle.style.marginBottom = 6;
            debugTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            debugPanel.Add(debugTitle);
            debugInfoLabel = UiFactory.Label("等待战斗数据…", 12, new Color(0.78f, 0.88f, 0.82f));
            debugInfoLabel.name = "debug-info";
            debugInfoLabel.style.whiteSpace = WhiteSpace.Normal;
            debugInfoLabel.style.marginBottom = 10;
            debugPanel.Add(debugInfoLabel);

            var hotkeys = new VisualElement { name = "debug-hotkeys" };
            hotkeys.style.flexDirection = FlexDirection.Column;
            hotkeys.pickingMode = PickingMode.Ignore;
            AddDebugHotkeyRow(hotkeys, "I", "无敌");
            AddDebugHotkeyRow(hotkeys, "X", "增加经验");
            AddDebugHotkeyRow(hotkeys, "R", "随机武器");
            AddDebugHotkeyRow(hotkeys, "T", "时间倍率");
            AddDebugHotkeyRow(hotkeys, "G", "生成精英");
            AddDebugHotkeyRow(hotkeys, "B", "生成 Boss");
            AddDebugHotkeyRow(hotkeys, "K", "清除敌人");
            AddDebugHotkeyRow(hotkeys, "H", "回满血");
            AddDebugHotkeyRow(hotkeys, "O", "滑板车");
            AddDebugHotkeyRow(hotkeys, "N", "狙击枪");
            AddDebugHotkeyRow(hotkeys, "M", "追踪眼镜");
            AddDebugHotkeyRow(hotkeys, "P", "胶囊足球");
            AddDebugHotkeyRow(hotkeys, "F", "飞轮术满级");
            AddDebugHotkeyRow(hotkeys, "V", "伏波琴满级");
            debugPanel.Add(hotkeys);
            root.Add(debugPanel);
            debugPanelVisible = false;
        }

        private static void AddDebugHotkeyRow(VisualElement parent, string key, string action)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.height = 16;
            row.style.marginTop = 0;
            row.style.marginBottom = 1;
            row.style.paddingTop = 0;
            row.style.paddingBottom = 0;
            row.pickingMode = PickingMode.Ignore;

            var keyLabel = UiFactory.Label(key, 12, new Color(0.98f, 0.86f, 0.42f));
            keyLabel.style.marginTop = 0;
            keyLabel.style.marginBottom = 0;
            keyLabel.style.marginRight = 10;
            keyLabel.style.paddingTop = 0;
            keyLabel.style.paddingBottom = 0;
            keyLabel.style.width = 18;
            keyLabel.style.height = 16;
            keyLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            keyLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            keyLabel.pickingMode = PickingMode.Ignore;
            row.Add(keyLabel);

            var actionLabel = UiFactory.Label(action, 12, new Color(0.86f, 0.92f, 0.88f));
            actionLabel.style.marginTop = 0;
            actionLabel.style.marginBottom = 0;
            actionLabel.style.paddingTop = 0;
            actionLabel.style.paddingBottom = 0;
            actionLabel.style.height = 16;
            actionLabel.pickingMode = PickingMode.Ignore;
            row.Add(actionLabel);

            parent.Add(row);
        }

        private VisualElement BuildLeftColumn()
        {
            var left = new VisualElement { name = "hud-left" };
            left.style.flexDirection = FlexDirection.Column;
            left.style.alignItems = Align.FlexStart;
            left.style.flexGrow = 1;
            left.style.flexShrink = 1;
            left.style.marginRight = 16;
            left.style.maxWidth = Length.Percent(58);
            left.pickingMode = PickingMode.Ignore;

            left.Add(BuildPlayerStatusRow());

            weaponBar = new VisualElement { name = "weapon-bar" };
            weaponBar.style.flexDirection = FlexDirection.Row;
            weaponBar.style.alignItems = Align.Center;
            weaponBar.style.height = 58;
            weaponBar.style.marginTop = 6;
            weaponBar.style.marginBottom = 6;
            weaponBar.pickingMode = PickingMode.Ignore;
            left.Add(weaponBar);

            left.Add(BuildEffectRow());
            return left;
        }

        private VisualElement BuildPlayerStatusRow()
        {
            var row = CreatePanel("player-status");
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.minWidth = 420;

            levelLabel = UiFactory.Label("Lv.1", 20, new Color(0.95f, 0.92f, 0.7f));
            levelLabel.name = "level-label";
            levelLabel.style.marginBottom = 0;
            levelLabel.style.marginRight = 12;
            levelLabel.style.minWidth = 54;
            levelLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            levelLabel.pickingMode = PickingMode.Ignore;
            row.Add(levelLabel);

            var bars = new VisualElement();
            bars.style.flexGrow = 1;
            bars.style.flexShrink = 1;
            bars.style.minWidth = 160;
            bars.pickingMode = PickingMode.Ignore;

            var hpRow = BuildStatBarRow("hp", "HP", HpFill, out hpFill, out hpValueLabel);
            hpRow.style.marginBottom = 5;
            bars.Add(hpRow);
            bars.Add(BuildStatBarRow("exp", "EXP", ExpFill, out expFill, out expValueLabel));
            row.Add(bars);

            var combatStats = new VisualElement { name = "combat-stats" };
            combatStats.style.flexDirection = FlexDirection.Column;
            combatStats.style.marginLeft = 12;
            combatStats.style.minWidth = 88;
            combatStats.pickingMode = PickingMode.Ignore;

            attackLabel = UiFactory.Label("攻击 100", 13, new Color(1f, 0.82f, 0.62f));
            attackLabel.name = "attack-label";
            attackLabel.style.marginBottom = 2;
            attackLabel.pickingMode = PickingMode.Ignore;
            combatStats.Add(attackLabel);

            moveSpeedLabel = UiFactory.Label("移速 0", 13, new Color(0.72f, 0.88f, 1f));
            moveSpeedLabel.name = "move-speed-label";
            moveSpeedLabel.style.marginBottom = 0;
            moveSpeedLabel.pickingMode = PickingMode.Ignore;
            combatStats.Add(moveSpeedLabel);
            row.Add(combatStats);

            fpsLabel = UiFactory.Label(string.Empty, 13, new Color(0.65f, 0.75f, 0.7f));
            fpsLabel.name = "fps-label";
            fpsLabel.style.marginBottom = 0;
            fpsLabel.style.marginLeft = 12;
            fpsLabel.style.minWidth = 58;
            fpsLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            fpsLabel.pickingMode = PickingMode.Ignore;
            row.Add(fpsLabel);
            return row;
        }

        private static VisualElement BuildStatBarRow(string id, string caption, Color fillColor,
            out VisualElement fill, out Label valueLabel)
        {
            var row = new VisualElement { name = $"{id}-row" };
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.pickingMode = PickingMode.Ignore;

            var captionLabel = UiFactory.Label(caption, 11, new Color(0.7f, 0.8f, 0.76f));
            captionLabel.style.marginBottom = 0;
            captionLabel.style.marginRight = 6;
            captionLabel.style.minWidth = 28;
            captionLabel.pickingMode = PickingMode.Ignore;
            row.Add(captionLabel);

            var track = new VisualElement { name = $"{id}-bar" };
            track.style.flexGrow = 1;
            track.style.height = 12;
            track.style.backgroundColor = TrackBg;
            track.style.borderTopLeftRadius = 4;
            track.style.borderTopRightRadius = 4;
            track.style.borderBottomLeftRadius = 4;
            track.style.borderBottomRightRadius = 4;
            track.style.overflow = Overflow.Hidden;
            track.pickingMode = PickingMode.Ignore;

            fill = new VisualElement { name = $"{id}-fill" };
            fill.style.height = Length.Percent(100);
            fill.style.width = Length.Percent(100);
            fill.style.backgroundColor = fillColor;
            fill.pickingMode = PickingMode.Ignore;
            track.Add(fill);
            row.Add(track);

            valueLabel = UiFactory.Label("0/0", 11, new Color(0.82f, 0.9f, 0.86f));
            valueLabel.name = $"{id}-value";
            valueLabel.style.marginBottom = 0;
            valueLabel.style.marginLeft = 8;
            valueLabel.style.minWidth = 72;
            valueLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            valueLabel.pickingMode = PickingMode.Ignore;
            row.Add(valueLabel);
            return row;
        }

        private VisualElement BuildEffectRow()
        {
            effectBar = CreatePanel("effect-bar");
            effectBar.style.flexDirection = FlexDirection.Column;
            effectBar.style.alignItems = Align.Stretch;
            effectBar.style.minWidth = 280;
            effectBar.style.maxWidth = 520;
            effectBar.style.display = DisplayStyle.None;

            toast = UiFactory.Label(string.Empty, 14, new Color(1f, 0.93f, 0.55f));
            toast.name = "crate-effect-toast";
            toast.style.marginBottom = 4;
            toast.style.unityFontStyleAndWeight = FontStyle.Bold;
            toast.style.whiteSpace = WhiteSpace.Normal;
            toast.style.display = DisplayStyle.None;
            toast.pickingMode = PickingMode.Ignore;
            effectBar.Add(toast);

            effectChips = new VisualElement { name = "effect-chips" };
            effectChips.style.flexDirection = FlexDirection.Column;
            effectChips.style.alignItems = Align.Stretch;
            effectChips.pickingMode = PickingMode.Ignore;
            effectBar.Add(effectChips);
            return effectBar;
        }

        private VisualElement BuildRightColumn()
        {
            var right = new VisualElement { name = "hud-right" };
            right.style.flexDirection = FlexDirection.Column;
            right.style.alignItems = Align.FlexEnd;
            right.style.flexShrink = 0;
            right.pickingMode = PickingMode.Ignore;

            waveLabel = UiFactory.Label("第 1/1 波", 51, new Color(0.9f, 0.94f, 0.9f));
            waveLabel.name = "wave-label";
            waveLabel.style.marginBottom = 10;
            waveLabel.style.backgroundColor = Color.clear;
            waveLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            waveLabel.pickingMode = PickingMode.Ignore;
            right.Add(waveLabel);

            var exit = UiFactory.Button("退出", RequestExitToMenu, 84);
            exit.name = "exit-button";
            exit.style.height = 32;
            exit.style.marginTop = 0;
            exit.style.marginBottom = 8;
            exit.style.fontSize = 14;
            exit.style.backgroundColor = new Color(0.55f, 0.18f, 0.22f);
            exit.pickingMode = PickingMode.Position;
            // Mouse-only: Enter is reserved for level-up confirm and must not activate Exit.
            exit.focusable = false;
            right.Add(exit);

            bossBars = new VisualElement { name = "boss-bars" };
            bossBars.style.flexDirection = FlexDirection.Column;
            bossBars.style.alignItems = Align.Stretch;
            bossBars.style.minWidth = 260;
            bossBars.style.maxWidth = 340;
            bossBars.style.display = DisplayStyle.None;
            bossBars.pickingMode = PickingMode.Ignore;
            right.Add(bossBars);
            return right;
        }

        private static VisualElement CreatePanel(string name)
        {
            var panel = new VisualElement { name = name };
            panel.style.backgroundColor = Color.clear;
            panel.style.paddingLeft = 4;
            panel.style.paddingRight = 4;
            panel.style.paddingTop = 2;
            panel.style.paddingBottom = 2;
            panel.pickingMode = PickingMode.Ignore;
            return panel;
        }

        private void UpdateSnapshot(BattleSnapshot value)
        {
            levelLabel.text = $"Lv.{value.Level}";
            SetBar(hpFill, hpValueLabel, value.Hp, value.MaxHp);
            SetBar(expFill, expValueLabel, value.Experience, Mathf.Max(1, value.RequiredExperience));
            attackLabel.text = $"攻击 {Mathf.RoundToInt(value.AttackMultiplier * 100f)}";
            moveSpeedLabel.text = $"移速 {Mathf.RoundToInt(value.MoveSpeedPixels)}";
            var showFps = AppRoot.Instance?.Session.Settings.ShowPerformanceMonitor == true;
            fpsLabel.style.display = showFps ? DisplayStyle.Flex : DisplayStyle.None;
            fpsLabel.text = showFps ? $"FPS {value.Fps:0}" : string.Empty;

            waveLabel.text = $"第 {value.Wave}/{value.WaveCount} 波";
            RefreshBossBars(value.Bosses);
            RefreshEffectChips(value.Effects);
            RefreshEffectBarVisibility();
            RefreshDebugInfo(value);
        }

        private void RefreshDebugInfo(BattleSnapshot value)
        {
            if (debugInfoLabel == null) return;
            debugInfoLabel.text =
                $"击杀 {value.Kills}    敌人 {value.EnemyCount}\n" +
                $"对象池 {value.ActivePoolObjects}    FPS {value.Fps:0}\n" +
                $"攻击 {Mathf.RoundToInt(value.AttackMultiplier * 100f)}    移速 {Mathf.RoundToInt(value.MoveSpeedPixels)}";
        }

        private static void SetBar(VisualElement fill, Label valueLabel, float current, float max)
        {
            var ratio = max <= 0f ? 0f : Mathf.Clamp01(current / max);
            fill.style.width = Length.Percent(ratio * 100f);
            valueLabel.text = $"{current:0}/{max:0}";
        }

        private void RefreshBossBars(BossHudEntry[] bosses)
        {
            if (bosses == null || bosses.Length == 0)
            {
                bossBars.Clear();
                bossBarMap.Clear();
                bossBars.style.display = DisplayStyle.None;
                return;
            }

            bossBars.style.display = DisplayStyle.Flex;
            var alive = new HashSet<string>();
            for (var i = 0; i < bosses.Length; i++)
            {
                var entry = bosses[i];
                var key = $"{entry.Name}:{i}";
                alive.Add(key);
                if (!bossBarMap.TryGetValue(key, out var row))
                {
                    row = CreateBossBarRow(entry.Name, key);
                    bossBarMap[key] = row;
                    bossBars.Add(row);
                }

                var fill = row.Q<VisualElement>("boss-fill");
                var value = row.Q<Label>("boss-value");
                var title = row.Q<Label>("boss-title");
                if (title != null) title.text = entry.Name;
                SetBar(fill, value, entry.Hp, entry.MaxHp);
            }

            var stale = new List<string>();
            foreach (var pair in bossBarMap)
            {
                if (alive.Contains(pair.Key)) continue;
                pair.Value.RemoveFromHierarchy();
                stale.Add(pair.Key);
            }
            foreach (var key in stale) bossBarMap.Remove(key);
        }

        private static VisualElement CreateBossBarRow(string name, string key)
        {
            var row = CreatePanel($"boss-bar-{key}");
            row.style.marginBottom = 6;
            row.style.minWidth = 260;

            var title = UiFactory.Label(name, 13, new Color(1f, 0.55f, 0.45f));
            title.name = "boss-title";
            title.style.marginBottom = 4;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.pickingMode = PickingMode.Ignore;
            row.Add(title);

            var barRow = new VisualElement();
            barRow.style.flexDirection = FlexDirection.Row;
            barRow.style.alignItems = Align.Center;
            barRow.pickingMode = PickingMode.Ignore;

            var track = new VisualElement { name = "boss-track" };
            track.style.flexGrow = 1;
            track.style.height = 12;
            track.style.backgroundColor = TrackBg;
            track.style.borderTopLeftRadius = 4;
            track.style.borderTopRightRadius = 4;
            track.style.borderBottomLeftRadius = 4;
            track.style.borderBottomRightRadius = 4;
            track.style.overflow = Overflow.Hidden;
            track.pickingMode = PickingMode.Ignore;

            var fill = new VisualElement { name = "boss-fill" };
            fill.style.height = Length.Percent(100);
            fill.style.width = Length.Percent(100);
            fill.style.backgroundColor = BossFill;
            fill.pickingMode = PickingMode.Ignore;
            track.Add(fill);
            barRow.Add(track);

            var value = UiFactory.Label("0/0", 11, new Color(0.92f, 0.86f, 0.82f));
            value.name = "boss-value";
            value.style.marginBottom = 0;
            value.style.marginLeft = 8;
            value.style.minWidth = 78;
            value.style.unityTextAlign = TextAnchor.MiddleRight;
            value.pickingMode = PickingMode.Ignore;
            barRow.Add(value);
            row.Add(barRow);
            return row;
        }

        private void RefreshEffectChips(EffectHudEntry[] effects)
        {
            if (effects == null || effects.Length == 0)
            {
                effectChips.Clear();
                effectChipMap.Clear();
                return;
            }

            var alive = new HashSet<string>();
            foreach (var effect in effects)
            {
                alive.Add(effect.Id);
                if (!effectChipMap.TryGetValue(effect.Id, out var chip))
                {
                    chip = CreateEffectChip(effect.Id);
                    effectChipMap[effect.Id] = chip;
                    effectChips.Add(chip);
                }

                var title = chip.Q<Label>("effect-title");
                var detail = chip.Q<Label>("effect-detail");
                var timer = chip.Q<Label>("effect-timer");
                if (title != null) title.text = effect.Title;
                if (detail != null)
                {
                    detail.text = effect.Detail;
                    detail.style.display = string.IsNullOrWhiteSpace(effect.Detail)
                        ? DisplayStyle.None : DisplayStyle.Flex;
                }
                if (timer != null) timer.text = FormatCountdown(effect.RemainingSeconds);
            }

            var stale = new List<string>();
            foreach (var pair in effectChipMap)
            {
                if (alive.Contains(pair.Key)) continue;
                pair.Value.RemoveFromHierarchy();
                stale.Add(pair.Key);
            }
            foreach (var key in stale) effectChipMap.Remove(key);
        }

        private static VisualElement CreateEffectChip(string id)
        {
            var chip = new VisualElement { name = $"effect-chip-{id}" };
            chip.style.marginBottom = 1;
            chip.style.paddingLeft = 2;
            chip.style.paddingRight = 2;
            chip.style.paddingTop = 0;
            chip.style.paddingBottom = 0;
            chip.style.backgroundColor = Color.clear;
            chip.pickingMode = PickingMode.Ignore;

            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 0;
            header.pickingMode = PickingMode.Ignore;

            var title = UiFactory.Label(string.Empty, 24, new Color(1f, 0.93f, 0.55f));
            title.name = "effect-title";
            title.style.marginTop = 0;
            title.style.marginBottom = 0;
            title.style.flexGrow = 1;
            title.style.flexShrink = 1;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.whiteSpace = WhiteSpace.Normal;
            title.pickingMode = PickingMode.Ignore;
            header.Add(title);

            var timer = UiFactory.Label(string.Empty, 24, new Color(0.92f, 1f, 0.78f));
            timer.name = "effect-timer";
            timer.style.marginTop = 0;
            timer.style.marginBottom = 0;
            timer.style.marginLeft = 10;
            timer.style.minWidth = 64;
            timer.style.unityFontStyleAndWeight = FontStyle.Bold;
            timer.style.unityTextAlign = TextAnchor.MiddleRight;
            timer.pickingMode = PickingMode.Ignore;
            header.Add(timer);
            chip.Add(header);

            var detail = UiFactory.Label(string.Empty, 14, new Color(0.82f, 0.9f, 0.85f));
            detail.name = "effect-detail";
            detail.style.marginTop = 0;
            detail.style.marginBottom = 0;
            detail.style.whiteSpace = WhiteSpace.Normal;
            detail.pickingMode = PickingMode.Ignore;
            chip.Add(detail);
            return chip;
        }

        private static string FormatCountdown(float seconds)
        {
            var total = Mathf.Max(0, Mathf.CeilToInt(seconds));
            var minutes = total / 60;
            var remain = total % 60;
            return minutes > 0 ? $"{minutes}:{remain:00}" : $"{remain}s";
        }

        private void ShowDamageNumber(Vector2 worldPosition, float amount, bool critical)
        {
            if (damageNumberLayer == null || AppRoot.Instance?.Session.Settings.DamageNumbers != true)
                return;

            var label = UiFactory.Label(Mathf.Max(1, Mathf.RoundToInt(amount)).ToString(), critical ? 23 : 17,
                critical ? new Color(1f, 0.36f, 0.25f) : new Color(1f, 0.92f, 0.58f));
            label.style.position = Position.Absolute;
            label.style.width = 90;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.unityFontStyleAndWeight = critical ? FontStyle.Bold : FontStyle.Normal;
            label.pickingMode = PickingMode.Ignore;
            damageNumberLayer.Add(label);
            damageNumbers.Add(new DamageNumberVisual
            {
                Label = label,
                WorldPosition = worldPosition,
                Age = 0f
            });
        }

        private void UpdateDamageNumbers()
        {
            if (damageNumberLayer == null || battle == null)
                return;

            var width = damageNumberLayer.resolvedStyle.width;
            var height = damageNumberLayer.resolvedStyle.height;
            for (var i = damageNumbers.Count - 1; i >= 0; i--)
            {
                var item = damageNumbers[i];
                item.Age += Time.unscaledDeltaTime;
                if (item.Age >= 0.8f)
                {
                    item.Label.RemoveFromHierarchy();
                    damageNumbers.RemoveAt(i);
                    continue;
                }

                var viewport = battle.WorldToViewport(item.WorldPosition);
                if (viewport.z <= 0f)
                {
                    item.Label.style.display = DisplayStyle.None;
                    continue;
                }

                item.Label.style.display = DisplayStyle.Flex;
                item.Label.style.left = viewport.x * width - 45f;
                item.Label.style.top = (1f - viewport.y) * height - 34f - item.Age * 48f;
                item.Label.style.opacity = 1f - item.Age / 0.8f;
            }
        }

        private void ShowLevelUp(IReadOnlyList<UpgradeOffer> offers, bool canRefresh)
        {
            lastLevelUpOffers = offers;
            lastLevelUpCanRefresh = canRefresh;
            RenderLevelUp(offers, canRefresh);
        }

        private void RenderLevelUp(IReadOnlyList<UpgradeOffer> offers, bool canRefresh)
        {
            levelUpUiActive = true;
            levelUpCards.Clear();
            levelUpActionButtons.Clear();
            levelUpActionCallbacks.Clear();
            levelUpFocusActions = false;
            levelUpCardIndex = 0;
            levelUpActionIndex = 0;
            overlay.Clear();
            overlay.style.display = DisplayStyle.Flex;

            var panel = new VisualElement { name = "level-up-panel" };
            panel.style.alignItems = Align.Center;
            panel.style.justifyContent = Justify.Center;
            panel.style.paddingLeft = 36;
            panel.style.paddingRight = 36;
            panel.style.paddingTop = 28;
            panel.style.paddingBottom = 24;
            panel.style.backgroundColor = new Color(0.04f, 0.07f, 0.06f, 0.88f);
            panel.style.borderTopLeftRadius = 16;
            panel.style.borderTopRightRadius = 16;
            panel.style.borderBottomLeftRadius = 16;
            panel.style.borderBottomRightRadius = 16;
            panel.style.borderTopWidth = 1;
            panel.style.borderRightWidth = 1;
            panel.style.borderBottomWidth = 1;
            panel.style.borderLeftWidth = 1;
            panel.style.borderTopColor = new Color(0.42f, 0.58f, 0.45f, 0.45f);
            panel.style.borderRightColor = new Color(0.42f, 0.58f, 0.45f, 0.45f);
            panel.style.borderBottomColor = new Color(0.42f, 0.58f, 0.45f, 0.45f);
            panel.style.borderLeftColor = new Color(0.42f, 0.58f, 0.45f, 0.45f);
            panel.pickingMode = PickingMode.Ignore;

            var title = UiFactory.Label("升  级", 38, LevelUpTitle);
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 2;
            title.style.unityTextAlign = TextAnchor.MiddleCenter;
            panel.Add(title);

            var subtitle = UiFactory.Label("选择一项强化", 16, new Color(0.72f, 0.82f, 0.76f));
            subtitle.style.marginBottom = 18;
            subtitle.style.unityTextAlign = TextAnchor.MiddleCenter;
            panel.Add(subtitle);

            var row = UiFactory.Row();
            row.name = "level-up-cards";
            row.style.justifyContent = Justify.Center;
            row.style.alignItems = Align.Stretch;
            row.style.marginBottom = 14;
            for (var i = 0; i < offers.Count; i++)
            {
                var index = i;
                var captured = offers[i];
                var card = CreateUpgradeCard(captured, () => battle.ApplyUpgrade(captured));
                card.RegisterCallback<MouseEnterEvent>(_ =>
                {
                    if (!levelUpUiActive) return;
                    FocusLevelUpCard(index);
                });
                levelUpCards.Add(card);
                row.Add(card);
            }
            panel.Add(row);

            var hint = UiFactory.Label("← → 选择    ↑ ↓ 切换栏    Enter 确认", 13, new Color(0.62f, 0.72f, 0.66f));
            hint.style.marginBottom = 12;
            hint.style.unityTextAlign = TextAnchor.MiddleCenter;
            panel.Add(hint);

            var controls = UiFactory.Row();
            controls.style.justifyContent = Justify.Center;
            if (canRefresh)
                AddLevelUpAction(controls, "刷新一次", battle.RefreshUpgradeOffers, 148);
            AddLevelUpAction(controls, "跳过", battle.SkipUpgrade, 148);
            panel.Add(controls);
            overlay.Add(panel);
            ApplyLevelUpSelectionVisuals();
        }

        private void AddLevelUpAction(VisualElement parent, string text, System.Action clicked, int width)
        {
            var index = levelUpActionButtons.Count;
            var button = CreateLevelUpActionButton(text, clicked, width);
            button.focusable = false;
            button.RegisterCallback<MouseEnterEvent>(_ =>
            {
                if (!levelUpUiActive) return;
                FocusLevelUpAction(index);
            });
            levelUpActionButtons.Add(button);
            levelUpActionCallbacks.Add(clicked);
            parent.Add(button);
        }

        private static Button CreateLevelUpActionButton(string text, System.Action clicked, int width)
        {
            var button = UiFactory.Button(text, clicked, width);
            button.style.height = 38;
            button.style.fontSize = 15;
            button.style.marginLeft = 8;
            button.style.marginRight = 8;
            button.style.marginTop = 0;
            button.style.marginBottom = 0;
            button.style.borderTopLeftRadius = 8;
            button.style.borderTopRightRadius = 8;
            button.style.borderBottomLeftRadius = 8;
            button.style.borderBottomRightRadius = 8;
            StyleLevelUpActionButton(button, false);
            return button;
        }

        private static void StyleLevelUpActionButton(Button button, bool selected)
        {
            if (button == null) return;
            button.style.backgroundColor = selected
                ? LevelUpCardSelected
                : new Color(0.12f, 0.18f, 0.16f, 0.95f);
            var border = selected ? LevelUpBorderSelected : LevelUpBorderIdle;
            var width = selected ? 3 : 1;
            button.style.borderTopWidth = width;
            button.style.borderRightWidth = width;
            button.style.borderBottomWidth = width;
            button.style.borderLeftWidth = width;
            button.style.borderTopColor = border;
            button.style.borderRightColor = border;
            button.style.borderBottomColor = border;
            button.style.borderLeftColor = border;
            button.style.color = selected ? LevelUpTitle : Color.white;
            button.style.opacity = selected ? 1f : 0.9f;
        }

        private static Button CreateUpgradeCard(UpgradeOffer offer, System.Action clicked)
        {
            var button = new Button(clicked)
            {
                name = $"upgrade-card-{offer.Id}",
                text = string.Empty,
                tooltip = $"{offer.Name}  Lv.{offer.NextLevel}"
            };
            button.style.width = 300;
            button.style.minHeight = 168;
            button.style.marginLeft = 10;
            button.style.marginRight = 10;
            button.style.marginTop = 4;
            button.style.marginBottom = 4;
            button.style.paddingLeft = 16;
            button.style.paddingRight = 16;
            button.style.paddingTop = 14;
            button.style.paddingBottom = 14;
            button.style.flexDirection = FlexDirection.Column;
            button.style.alignItems = Align.Stretch;
            button.style.justifyContent = Justify.FlexStart;
            button.style.color = Color.white;
            button.style.borderTopLeftRadius = 12;
            button.style.borderTopRightRadius = 12;
            button.style.borderBottomLeftRadius = 12;
            button.style.borderBottomRightRadius = 12;
            button.focusable = false;
            button.pickingMode = PickingMode.Position;

            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 10;
            header.pickingMode = PickingMode.Ignore;

            var icon = CreateUpgradeIcon(offer);
            icon.style.marginRight = 12;
            header.Add(icon);

            var titleBlock = new VisualElement();
            titleBlock.style.flexGrow = 1;
            titleBlock.style.flexShrink = 1;
            titleBlock.pickingMode = PickingMode.Ignore;

            var kindColor = offer.Kind == UpgradeKind.Weapon
                ? new Color(0.62f, 0.86f, 1f)
                : new Color(1f, 0.78f, 0.48f);
            var kind = UiFactory.Label(offer.Kind == UpgradeKind.Weapon ? "武器" : "被动", 12, kindColor);
            kind.style.marginBottom = 2;
            kind.pickingMode = PickingMode.Ignore;
            titleBlock.Add(kind);

            var title = UiFactory.Label(offer.Name, 20, Color.white);
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 2;
            title.pickingMode = PickingMode.Ignore;
            titleBlock.Add(title);

            var level = UiFactory.Label($"Lv.{offer.NextLevel}", 14, LevelUpTitle);
            level.style.marginBottom = 0;
            level.pickingMode = PickingMode.Ignore;
            titleBlock.Add(level);
            header.Add(titleBlock);
            button.Add(header);

            var description = UiFactory.Label(offer.Description, 14, new Color(0.78f, 0.88f, 0.82f));
            description.style.whiteSpace = WhiteSpace.Normal;
            description.style.marginBottom = 0;
            description.pickingMode = PickingMode.Ignore;
            button.Add(description);

            StyleUpgradeCard(button, false);
            return button;
        }

        private static void StyleUpgradeCard(Button button, bool selected)
        {
            if (button == null) return;
            button.style.backgroundColor = selected ? LevelUpCardSelected : LevelUpCardIdle;
            var border = selected ? LevelUpBorderSelected : LevelUpBorderIdle;
            var width = selected ? 3 : 1;
            button.style.borderTopWidth = width;
            button.style.borderRightWidth = width;
            button.style.borderBottomWidth = width;
            button.style.borderLeftWidth = width;
            button.style.borderTopColor = border;
            button.style.borderRightColor = border;
            button.style.borderBottomColor = border;
            button.style.borderLeftColor = border;
            button.style.translate = selected
                ? new Translate(0, -6)
                : new Translate(0, 0);
            button.style.opacity = selected ? 1f : 0.88f;
        }

        private static VisualElement CreateUpgradeIcon(UpgradeOffer offer)
        {
            var holder = CreateIconHolder(76);
            var sprite = offer.Kind == UpgradeKind.Weapon
                ? WeaponArtCatalog.LoadIcon(offer.Icon)
                : PassiveArtCatalog.LoadIcon(offer.Id);
            if (sprite != null)
            {
                var image = new Image
                {
                    name = $"upgrade-icon-{offer.Id}",
                    sprite = sprite,
                    scaleMode = ScaleMode.ScaleToFit,
                    pickingMode = PickingMode.Ignore
                };
                image.style.width = 68;
                image.style.height = 68;
                holder.Add(image);
            }
            else
            {
                var fallback = UiFactory.Label(offer.Kind == UpgradeKind.Weapon ? "武" : "被动", 18,
                    new Color(0.72f, 0.84f, 0.76f));
                fallback.style.marginBottom = 0;
                fallback.style.unityTextAlign = TextAnchor.MiddleCenter;
                fallback.pickingMode = PickingMode.Ignore;
                holder.Add(fallback);
            }
            return holder;
        }

        private void RefreshWeaponBar()
        {
            if (weaponBar == null || battle == null) return;
            weaponBar.Clear();
            var count = 0;
            foreach (var id in WeaponDisplayOrder)
            {
                if (!battle.OwnedUpgrades.TryGetValue(id, out var weapon) || weapon.Kind != UpgradeKind.Weapon)
                    continue;

                var weaponConfig = AppRoot.Instance?.Session?.Config?.Weapons?.Weapons
                    ?.FirstOrDefault(value => value.Id == id);
                var promotion = weaponConfig?.Promotion;
                var slot = CreateIconHolder(52, false);
                slot.name = $"weapon-slot-{id}";
                slot.style.marginRight = 7;
                var iconKey = WeaponArtCatalog.ResolveIconKey(weapon.Icon, promotion, weapon.Level, weapon.MaxLevel);
                var displayName = WeaponArtCatalog.ResolveDisplayName(weapon.Name, promotion, weapon.Level, weapon.MaxLevel);
                slot.tooltip = $"{displayName}  Lv.{weapon.Level}";
                var sprite = WeaponArtCatalog.LoadIcon(iconKey);
                if (sprite != null)
                {
                    var image = new Image
                    {
                        name = $"weapon-icon-{id}",
                        sprite = sprite,
                        scaleMode = ScaleMode.ScaleToFit,
                        pickingMode = PickingMode.Ignore
                    };
                    image.style.width = 46;
                    image.style.height = 46;
                    slot.Add(image);
                }
                else
                {
                    var fallback = UiFactory.Label("武", 18, new Color(0.72f, 0.84f, 0.76f));
                    fallback.style.marginBottom = 0;
                    fallback.style.unityTextAlign = TextAnchor.MiddleCenter;
                    fallback.pickingMode = PickingMode.Ignore;
                    slot.Add(fallback);
                }

                var level = UiFactory.Label($"{weapon.Level}", 13, Color.white);
                level.name = $"weapon-level-{id}";
                level.style.position = Position.Absolute;
                level.style.right = 2;
                level.style.bottom = 0;
                level.style.marginBottom = 0;
                level.style.paddingLeft = 2;
                level.style.paddingRight = 2;
                level.style.backgroundColor = Color.clear;
                level.pickingMode = PickingMode.Ignore;
                slot.Add(level);
                weaponBar.Add(slot);
                count++;
            }
            weaponBar.style.display = count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void UpdateCrateGuideMarkers()
        {
            if (crateGuideLayer == null || battle == null) return;
            var width = crateGuideLayer.resolvedStyle.width;
            var height = crateGuideLayer.resolvedStyle.height;
            var active = battle.IsCrateGuideActive && width > 1f && height > 1f;
            var displayed = 0;
            if (active)
            {
                foreach (var target in battle.ActiveSupplyCratePositions)
                {
                    if (displayed >= crateGuideMarkers.Length) break;
                    var viewport = battle.WorldToViewport(target);
                    if (viewport.z <= 0f || (viewport.x >= 0.07f && viewport.x <= 0.93f && viewport.y >= 0.09f && viewport.y <= 0.91f))
                        continue;
                    var direction = new Vector2(viewport.x - 0.5f, viewport.y - 0.5f);
                    if (direction.sqrMagnitude < 0.0001f) continue;
                    var scale = 0.44f / Mathf.Max(Mathf.Abs(direction.x), Mathf.Abs(direction.y));
                    var point = new Vector2(0.5f, 0.5f) + direction * scale;
                    var marker = crateGuideMarkers[displayed++];
                    marker.style.left = Mathf.Clamp(point.x * width - 27f, 18f, width - 72f);
                    marker.style.top = Mathf.Clamp((1f - point.y) * height - 36f, 48f, height - 90f);
                    marker.style.display = marker.sprite != null ? DisplayStyle.Flex : DisplayStyle.None;
                }
            }
            for (var i = displayed; i < crateGuideMarkers.Length; i++) crateGuideMarkers[i].style.display = DisplayStyle.None;
        }

        private static VisualElement CreateIconHolder(int size, bool withBackground = true)
        {
            var holder = new VisualElement();
            holder.style.width = size;
            holder.style.height = size;
            holder.style.minWidth = size;
            holder.style.alignItems = Align.Center;
            holder.style.justifyContent = Justify.Center;
            holder.style.backgroundColor = withBackground
                ? new Color(0.05f, 0.08f, 0.075f, 0.92f)
                : Color.clear;
            if (withBackground)
            {
                holder.style.borderTopLeftRadius = 8;
                holder.style.borderTopRightRadius = 8;
                holder.style.borderBottomLeftRadius = 8;
                holder.style.borderBottomRightRadius = 8;
            }
            holder.pickingMode = PickingMode.Ignore;
            return holder;
        }

        private void HandleState(GameState state)
        {
            if (state == GameState.Playing)
            {
                levelUpUiActive = false;
                overlay.style.display = DisplayStyle.None;
            }
            else if (state == GameState.Paused)
            {
                levelUpUiActive = false;
                overlay.Clear();
                overlay.style.display = DisplayStyle.Flex;
                overlay.Add(UiFactory.Label("已暂停", 34));
                overlay.Add(UiFactory.Button("继续", battle.TogglePause));
                overlay.Add(UiFactory.Button("返回主菜单", ConfirmExitToMenu));
            }
            else if (state == GameState.BossIntro)
            {
                levelUpUiActive = false;
                overlay.Clear();
                overlay.style.display = DisplayStyle.Flex;
                overlay.Add(UiFactory.Label("警告：变异巨尸正在逼近", 34, new Color(1f, 0.25f, 0.2f)));
            }
        }

        private void RequestExitToMenu()
        {
            if (battle == null || AppRoot.Instance == null)
                return;
            // Enter during level-up must not open the exit dialog (UITK may still submit a HUD button).
            if (levelUpUiActive || Time.frameCount == levelUpConfirmFrame)
                return;

            var state = AppRoot.Instance.StateMachine.Current;
            if (state == GameState.LevelUp || state == GameState.BossIntro)
                return;
            if (state == GameState.Playing)
                battle.TogglePause();

            ShowExitConfirm();
        }

        private void ShowExitConfirm()
        {
            levelUpUiActive = false;
            overlay.Clear();
            overlay.style.display = DisplayStyle.Flex;
            overlay.Add(UiFactory.Label("退出战斗", 30));
            overlay.Add(UiFactory.Label("当前进度将不会保留，确定返回主菜单？", 16,
                new Color(0.72f, 0.78f, 0.76f)));

            var row = UiFactory.Row();
            row.style.marginTop = 12;
            row.Add(UiFactory.Button("取消", CancelExitConfirm, 150));
            var confirm = UiFactory.Button("确定退出", ConfirmExitToMenu, 150);
            confirm.style.backgroundColor = new Color(0.55f, 0.18f, 0.22f);
            row.Add(confirm);
            overlay.Add(row);
        }

        private void CancelExitConfirm()
        {
            var state = AppRoot.Instance?.StateMachine.Current ?? GameState.Playing;
            if (state == GameState.Paused)
            {
                HandleState(GameState.Paused);
                return;
            }

            if (state == GameState.BossIntro)
            {
                HandleState(GameState.BossIntro);
                return;
            }

            if (state == GameState.LevelUp && lastLevelUpOffers.Count > 0)
            {
                RenderLevelUp(lastLevelUpOffers, lastLevelUpCanRefresh);
                return;
            }

            overlay.style.display = DisplayStyle.None;
        }

        private void ConfirmExitToMenu()
        {
            AppRoot.Instance.ReturnToMenu();
        }

        private void ShowToast(string message)
        {
            toast.text = message;
            toast.style.display = DisplayStyle.Flex;
            toastRemaining = 3.5f;
            RefreshEffectBarVisibility();
        }

        private void RefreshEffectBarVisibility()
        {
            if (effectBar == null) return;
            var hasToast = toast != null && toast.style.display == DisplayStyle.Flex;
            var hasChips = effectChipMap.Count > 0;
            effectBar.style.display = hasToast || hasChips ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
