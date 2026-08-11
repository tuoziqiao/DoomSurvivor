using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DoomSurvivor.Core;
using DoomSurvivor.Gameplay;
using UnityEngine;
using UnityEngine.UIElements;

namespace DoomSurvivor.Presentation
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class MainMenuController : MonoBehaviour
    {
        private enum SettingsTab
        {
            Audio,
            Display,
            Crates,
            HiddenCrates,
            Altar,
            Map,
            Waves,
            Skills
        }

        private static readonly (string Id, string Label)[] CrateEffectSettings =
        {
            ("xp_burst", "经验爆发"),
            ("spawn_boss", "召唤 Boss"),
            ("spawn_poison_fog", "蔓延毒雾"),
            ("double_level", "连升两级"),
            ("max_hp_bonus", "最大生命"),
            ("move_speed_bonus", "移速强化"),
            ("magnet_burst", "磁力爆发"),
            ("anesthetic_capsule", "麻醉胶囊")
        };

        private static readonly (string Id, string Label)[] HiddenCrateEffectSettings =
        {
            ("scooter_boost", "滑板加速"),
            ("sniper_rifle", "狙击步枪"),
            ("crate_guide", "追踪眼镜"),
            ("capsule_football", "胶囊足球"),
            ("purge", "清屏献祭")
        };

        private static readonly (string Id, string Label)[] AltarEffectSettings =
        {
            ("blood_pact", "献血加攻"),
            ("magnet_burst", "磁力爆发"),
            ("random_teleport", "全图传送"),
            ("stun_watch", "麻醉型手表")
        };

        private static readonly Color Background = Html("#080B12");
        private static readonly Color BackgroundMid = Html("#101923");
        private static readonly Color BackgroundEnd = Html("#15231F");
        private static readonly Color Panel = Html("#111823");
        private static readonly Color PanelRaised = Html("#192433");
        private static readonly Color Border = Html("#314052");
        private static readonly Color Text = Html("#F4F0E8");
        private static readonly Color Muted = Html("#AAB5C0");
        private static readonly Color Dim = Html("#718092");
        private static readonly Color Brand = Html("#D7A76A");
        private static readonly Color Accent = Html("#D64B50");
        private static readonly Color AccentDark = Html("#8D2938");
        private static readonly Color Secondary = Html("#274A48");
        private static readonly Color Success = Html("#73C7A2");

        private int characterIndex;
        private int skinIndex;
        private VisualElement screen;
        private VisualElement mainContent;
        private VisualElement heroPanel;
        private VisualElement previewPanel;
        private VisualElement skinList;
        private VisualElement characterRail;
        private VisualElement portraitGlow;
        private VisualElement previewAccent;
        private UIDocument document;
        private Image portrait;
        private Label characterName;
        private Label characterDescription;
        private Label skinName;
        private Label characterPosition;
        private Label healthValue;
        private Label moveValue;
        private Label pickupValue;
        private Label armorValue;
        private Label criticalValue;
        private Label weaponValue;
        private Button windowedButton;
        private Button fullscreenButton;
        private Button normalModeButton;
        private Label selectedCharacterCallout;
        private bool compactLayout;

        private async void Start()
        {
            document = GetComponent<UIDocument>();
            while (AppRoot.Instance == null || !AppRoot.Instance.Ready)
            {
                if (AppRoot.Instance != null && !string.IsNullOrEmpty(AppRoot.Instance.StartupError))
                {
                    BuildStartupError(AppRoot.Instance.StartupError);
                    return;
                }

                await Task.Yield();
            }

            Build();
        }

        private void BuildStartupError(string error)
        {
            screen = CreateScreen(document.rootVisualElement);
            var card = CreateCard();
            card.style.maxWidth = 760;
            card.style.alignSelf = Align.Center;
            card.style.marginTop = 120;
            card.Add(CreateLabel("启动失败", 32, Accent));
            var detail = CreateLabel(error, 16, Text);
            detail.style.whiteSpace = WhiteSpace.Normal;
            card.Add(detail);
            screen.Add(card);
        }

        private void Build()
        {
            var session = AppRoot.Instance.Session;
            var characters = session.Config.Characters.Characters;
            characterIndex = Mathf.Max(0,
                characters.FindIndex(value => value.Id == session.Profile.SelectedCharacterId));

            var selectedCharacter = characters[characterIndex];
            var selectedSkins = GetSkins(selectedCharacter.Id);
            session.Profile.SelectedSkinByCharacter.TryGetValue(selectedCharacter.Id, out var selectedSkinId);
            skinIndex = Mathf.Max(0, selectedSkins.FindIndex(value => value.Id == selectedSkinId));

            screen = CreateScreen(document.rootVisualElement);
            screen.name = "main-menu-root";
            AddAtmosphere(screen);

            var content = new VisualElement { name = "main-menu-content" };
            content.style.flexGrow = 1;
            content.style.width = Length.Percent(100);
            content.style.maxWidth = 1680;
            content.style.alignSelf = Align.Center;
            content.style.justifyContent = Justify.Center;
            content.style.paddingLeft = 48;
            content.style.paddingRight = 48;
            content.style.paddingTop = 30;
            content.style.paddingBottom = 26;
            screen.Add(content);

            content.Add(BuildHeader(session));

            mainContent = new VisualElement { name = "main-menu-columns" };
            mainContent.style.flexGrow = 1;
            mainContent.style.flexDirection = FlexDirection.Row;
            mainContent.style.alignItems = Align.Stretch;
            mainContent.style.justifyContent = Justify.SpaceBetween;
            mainContent.style.marginTop = 22;
            mainContent.style.marginBottom = 16;
            content.Add(mainContent);

            heroPanel = BuildHeroPanel();
            previewPanel = BuildPreviewPanel();
            mainContent.Add(heroPanel);
            mainContent.Add(previewPanel);

            content.Add(BuildFooter(session));
            RefreshSelection();

            screen.RegisterCallback<GeometryChangedEvent>(evt =>
                ApplyResponsiveLayout(evt.newRect.width < 1180 || evt.newRect.height < 720));
            screen.RegisterCallback<KeyDownEvent>(OnKeyDown);
            screen.schedule.Execute(() => ApplyResponsiveLayout(screen.resolvedStyle.width < 1180 ||
                                                                  screen.resolvedStyle.height < 720));
        }

        private VisualElement BuildHeader(GameSession session)
        {
            var header = new VisualElement { name = "main-menu-header" };
            header.style.minHeight = 58;
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.paddingBottom = 14;
            header.style.borderBottomWidth = 1;
            header.style.borderBottomColor = new Color(Border.r, Border.g, Border.b, 0.7f);

            var brand = new VisualElement();
            brand.style.flexDirection = FlexDirection.Row;
            brand.style.alignItems = Align.Center;

            var brandMark = new VisualElement();
            brandMark.style.width = 8;
            brandMark.style.height = 38;
            brandMark.style.marginRight = 14;
            brandMark.style.backgroundColor = Brand;
            brandMark.style.borderTopLeftRadius = 3;
            brandMark.style.borderTopRightRadius = 3;
            brandMark.style.borderBottomLeftRadius = 3;
            brandMark.style.borderBottomRightRadius = 3;
            brand.Add(brandMark);

            var brandCopy = new VisualElement();
            var wordmark = CreateLabel("DOOM / SURVIVOR", 18, Text);
            wordmark.name = "brand-wordmark";
            wordmark.style.unityFontStyleAndWeight = FontStyle.Bold;
            wordmark.style.letterSpacing = 4;
            wordmark.style.marginBottom = 2;
            brandCopy.Add(wordmark);
            brandCopy.Add(CreateLabel("末日行动档案 · SURVIVAL PROTOCOL", 11, Dim));
            brand.Add(brandCopy);
            header.Add(brand);

            var headerActions = new VisualElement { name = "header-actions" };
            headerActions.style.flexDirection = FlexDirection.Row;
            headerActions.style.alignItems = Align.Center;
            headerActions.style.flexWrap = Wrap.Wrap;

            var displayModeSelector = new VisualElement { name = "display-mode-selector" };
            displayModeSelector.style.flexDirection = FlexDirection.Row;
            displayModeSelector.style.alignItems = Align.Center;
            displayModeSelector.style.marginRight = 10;
            displayModeSelector.style.paddingLeft = 5;
            displayModeSelector.style.paddingRight = 5;
            displayModeSelector.style.paddingTop = 5;
            displayModeSelector.style.paddingBottom = 5;
            displayModeSelector.style.backgroundColor = new Color(Panel.r, Panel.g, Panel.b, 0.9f);
            SetBorder(displayModeSelector, Border, 1, 10);

            var displayModeLabel = CreateLabel("显示模式", 12, Dim);
            displayModeLabel.style.marginLeft = 7;
            displayModeLabel.style.marginRight = 8;
            displayModeSelector.Add(displayModeLabel);

            windowedButton = CreateSecondaryButton("窗口模式", () => SetDisplayMode(false));
            windowedButton.name = "display-mode-windowed";
            windowedButton.style.width = 92;
            windowedButton.style.marginRight = 5;
            displayModeSelector.Add(windowedButton);

            fullscreenButton = CreateSecondaryButton("全屏模式", () => SetDisplayMode(true));
            fullscreenButton.name = "display-mode-fullscreen";
            fullscreenButton.style.width = 92;
            displayModeSelector.Add(fullscreenButton);
            headerActions.Add(displayModeSelector);
            UpdateDisplayModeButtons();

            var configChip = new VisualElement { name = "config-source-chip" };
            configChip.style.flexDirection = FlexDirection.Row;
            configChip.style.alignItems = Align.Center;
            configChip.style.paddingLeft = 13;
            configChip.style.paddingRight = 13;
            configChip.style.paddingTop = 8;
            configChip.style.paddingBottom = 8;
            configChip.style.backgroundColor = new Color(0.07f, 0.15f, 0.13f, 0.92f);
            SetBorder(configChip, new Color(Success.r, Success.g, Success.b, 0.35f), 1, 20);

            var dot = new VisualElement();
            dot.style.width = 8;
            dot.style.height = 8;
            dot.style.marginRight = 8;
            dot.style.backgroundColor = Success;
            dot.style.borderTopLeftRadius = 4;
            dot.style.borderTopRightRadius = 4;
            dot.style.borderBottomLeftRadius = 4;
            dot.style.borderBottomRightRadius = 4;
            configChip.Add(dot);
            configChip.Add(CreateLabel($"配置 {FormatConfigSource(session.ConfigSource)} · v{session.Config.Version}", 12,
                Success));
            headerActions.Add(configChip);
            header.Add(headerActions);
            return header;
        }

        private VisualElement BuildHeroPanel()
        {
            var panel = new VisualElement { name = "hero-panel" };
            panel.style.width = Length.Percent(39);
            panel.style.paddingRight = 46;
            panel.style.justifyContent = Justify.Center;

            var operation = new VisualElement { name = "cover-kicker" };
            operation.style.alignSelf = Align.FlexStart;
            operation.style.flexDirection = FlexDirection.Row;
            operation.style.alignItems = Align.Center;
            operation.style.paddingLeft = 12;
            operation.style.paddingRight = 12;
            operation.style.paddingTop = 7;
            operation.style.paddingBottom = 7;
            operation.style.marginBottom = 16;
            operation.style.backgroundColor = new Color(Brand.r, Brand.g, Brand.b, 0.08f);
            SetBorder(operation, new Color(Brand.r, Brand.g, Brand.b, 0.45f), 1, 18);
            var operationDot = new VisualElement();
            operationDot.style.width = 7;
            operationDot.style.height = 7;
            operationDot.style.marginRight = 8;
            operationDot.style.backgroundColor = Brand;
            SetRadius(operationDot, 4);
            operation.Add(operationDot);
            operation.Add(CreateLabel("OPERATION  /  NIGHTFALL", 11, Brand));
            panel.Add(operation);

            var eyebrow = CreateLabel("DOOM SURVIVOR", 13, Brand);
            eyebrow.style.unityFontStyleAndWeight = FontStyle.Bold;
            eyebrow.style.letterSpacing = 6;
            eyebrow.style.marginBottom = 10;
            panel.Add(eyebrow);

            var title = CreateLabel("末日\n幸存者", 66, Text);
            title.name = "main-menu-title";
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.letterSpacing = 6;
            title.style.whiteSpace = WhiteSpace.Normal;
            title.style.marginBottom = 12;
            panel.Add(title);

            var subtitle = CreateLabel("城市已经失守。选择最后的幸存者，\n穿过尸潮，把黎明带回来。", 16,
                Muted);
            subtitle.style.whiteSpace = WhiteSpace.Normal;
            subtitle.style.marginBottom = 22;
            panel.Add(subtitle);

            var configuredWaveCount = AppRoot.Instance?.Session?.Settings?.WaveCount ?? 10;
            var normalWaveCount = WaveRules.ResolveWaveCount(GameMode.Normal, configuredWaveCount);
            var missionBrief = new VisualElement { name = "mission-brief" };
            missionBrief.style.flexDirection = FlexDirection.Row;
            missionBrief.style.marginBottom = 14;
            missionBrief.style.backgroundColor = new Color(Panel.r, Panel.g, Panel.b, 0.62f);
            SetBorder(missionBrief, new Color(Border.r, Border.g, Border.b, 0.7f), 1, 10);
            missionBrief.Add(CreateMissionMetric("行动波次", normalWaveCount.ToString("00")));
            missionBrief.Add(CreateMissionMetric("作战模式", "单人"));
            missionBrief.Add(CreateMissionMetric("档案状态", "已同步"));
            panel.Add(missionBrief);

            selectedCharacterCallout = CreateLabel(string.Empty, 12, Brand);
            selectedCharacterCallout.name = "selected-character-callout";
            selectedCharacterCallout.style.unityFontStyleAndWeight = FontStyle.Bold;
            selectedCharacterCallout.style.letterSpacing = 1;
            selectedCharacterCallout.style.marginBottom = 5;
            panel.Add(selectedCharacterCallout);

            normalModeButton = CreateActionButton("进入战场", $"{normalWaveCount} 波完整生存", () => Launch(GameMode.Normal), true);
            normalModeButton.name = "mode-normal-button";
            normalModeButton.style.height = 74;
            panel.Add(normalModeButton);

            var quickWaveCount = WaveRules.ResolveWaveCount(GameMode.QuickTest, configuredWaveCount);
            var quick = CreateActionButton("训练模拟", $"{quickWaveCount} 波快速验证", () => Launch(GameMode.QuickTest), false);
            quick.name = "mode-quick-button";
            quick.style.height = 54;
            quick.style.fontSize = 14;
            panel.Add(quick);

            var utilityRow = new VisualElement();
            utilityRow.style.flexDirection = FlexDirection.Row;
            utilityRow.style.marginTop = 8;
            var settings = CreateSecondaryButton("设置", ShowSettings);
            settings.name = "settings-button";
            settings.style.flexGrow = 1;
            settings.style.marginRight = 10;
            utilityRow.Add(settings);
            var quit = CreateSecondaryButton("退出游戏", QuitGame);
            quit.name = "quit-button";
            quit.style.flexGrow = 1;
            utilityRow.Add(quit);
            panel.Add(utilityRow);

            var clearSave = CreateDangerButton("清除本地存档", ConfirmClearSave);
            clearSave.name = "clear-save-button";
            clearSave.style.height = 40;
            clearSave.style.marginTop = 8;
            panel.Add(clearSave);

            var hint = CreateLabel("←  →  切换幸存者    ·    ENTER 进入行动", 11, Dim);
            hint.style.marginTop = 14;
            hint.style.letterSpacing = 1;
            panel.Add(hint);
            return panel;
        }

        private VisualElement BuildPreviewPanel()
        {
            var card = CreateCard();
            card.name = "survivor-preview";
            card.style.width = Length.Percent(61);
            card.style.maxWidth = 880;
            card.style.minWidth = 560;
            card.style.paddingLeft = 26;
            card.style.paddingRight = 26;
            card.style.paddingTop = 22;
            card.style.paddingBottom = 22;
            card.style.justifyContent = Justify.SpaceBetween;
            card.style.backgroundColor = new Color(Panel.r, Panel.g, Panel.b, 0.94f);
            SetBorder(card, new Color(Brand.r, Brand.g, Brand.b, 0.36f), 1, 18);

            previewAccent = new VisualElement { name = "preview-accent" };
            previewAccent.pickingMode = PickingMode.Ignore;
            previewAccent.style.position = Position.Absolute;
            previewAccent.style.left = 0;
            previewAccent.style.top = 0;
            previewAccent.style.bottom = 0;
            previewAccent.style.width = 4;
            previewAccent.style.backgroundColor = Brand;
            previewAccent.style.borderTopLeftRadius = 18;
            previewAccent.style.borderBottomLeftRadius = 18;
            card.Add(previewAccent);

            var top = new VisualElement();
            top.style.flexDirection = FlexDirection.Row;
            top.style.alignItems = Align.Center;
            top.style.justifyContent = Justify.SpaceBetween;
            var titleGroup = new VisualElement();
            var kicker = CreateLabel("SELECT YOUR SURVIVOR", 11, Brand);
            kicker.style.unityFontStyleAndWeight = FontStyle.Bold;
            kicker.style.letterSpacing = 3;
            titleGroup.Add(kicker);
            var dossierTitle = CreateLabel("选择幸存者", 24, Text);
            dossierTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            dossierTitle.style.marginTop = 3;
            titleGroup.Add(dossierTitle);
            top.Add(titleGroup);
            characterPosition = CreateLabel(string.Empty, 13, Brand);
            characterPosition.name = "character-position";
            characterPosition.style.unityFontStyleAndWeight = FontStyle.Bold;
            characterPosition.style.letterSpacing = 2;
            top.Add(characterPosition);
            card.Add(top);

            var identity = new VisualElement { name = "character-stage" };
            identity.style.flexDirection = FlexDirection.Row;
            identity.style.alignItems = Align.Center;
            identity.style.marginTop = 14;
            identity.style.marginBottom = 12;
            identity.style.paddingTop = 12;
            identity.style.paddingBottom = 12;
            identity.style.paddingLeft = 10;
            identity.style.paddingRight = 10;
            identity.style.backgroundColor = Html("#0B111A");
            SetBorder(identity, new Color(Border.r, Border.g, Border.b, 0.72f), 1, 14);

            var previous = CreateArrowButton("‹", () => ChangeCharacter(-1));
            previous.name = "previous-character-button";
            identity.Add(previous);

            var portraitFrame = new VisualElement();
            portraitFrame.name = "portrait-frame";
            portraitFrame.style.width = 246;
            portraitFrame.style.height = 294;
            portraitFrame.style.marginLeft = 12;
            portraitFrame.style.marginRight = 24;
            portraitFrame.style.backgroundColor = Html("#0D1620");
            SetBorder(portraitFrame, new Color(Brand.r, Brand.g, Brand.b, 0.46f), 1, 14);

            var portraitIndex = CreateLabel("SURVIVOR", 10, Dim);
            portraitIndex.pickingMode = PickingMode.Ignore;
            portraitIndex.style.position = Position.Absolute;
            portraitIndex.style.left = 13;
            portraitIndex.style.top = 11;
            portraitIndex.style.letterSpacing = 2;
            portraitFrame.Add(portraitIndex);

            portraitGlow = new VisualElement();
            portraitGlow.pickingMode = PickingMode.Ignore;
            portraitGlow.style.position = Position.Absolute;
            portraitGlow.style.left = 16;
            portraitGlow.style.right = 16;
            portraitGlow.style.bottom = 10;
            portraitGlow.style.height = 96;
            portraitGlow.style.backgroundColor = new Color(Brand.r, Brand.g, Brand.b, 0.18f);
            SetRadius(portraitGlow, 48);
            portraitFrame.Add(portraitGlow);

            portrait = new Image { name = "character-portrait", scaleMode = ScaleMode.ScaleToFit };
            portrait.pickingMode = PickingMode.Ignore;
            portrait.style.position = Position.Absolute;
            portrait.style.left = 10;
            portrait.style.right = 10;
            portrait.style.top = 8;
            portrait.style.bottom = 8;
            portraitFrame.Add(portrait);
            identity.Add(portraitFrame);

            var copy = new VisualElement();
            copy.style.flexGrow = 1;
            copy.style.minWidth = 210;
            copy.style.paddingRight = 10;
            var clearance = CreateLabel("CLEARED FOR DEPLOYMENT", 10, Success);
            clearance.style.unityFontStyleAndWeight = FontStyle.Bold;
            clearance.style.letterSpacing = 2;
            clearance.style.marginBottom = 8;
            copy.Add(clearance);
            skinName = CreateLabel(string.Empty, 12, Brand);
            skinName.name = "skin-name";
            skinName.style.unityFontStyleAndWeight = FontStyle.Bold;
            skinName.style.letterSpacing = 2;
            copy.Add(skinName);
            characterName = CreateLabel(string.Empty, 38, Text);
            characterName.name = "character-name";
            characterName.style.unityFontStyleAndWeight = FontStyle.Bold;
            characterName.style.marginTop = 5;
            characterName.style.marginBottom = 10;
            copy.Add(characterName);
            characterDescription = CreateLabel(string.Empty, 15, Muted);
            characterDescription.name = "character-description";
            characterDescription.style.whiteSpace = WhiteSpace.Normal;
            copy.Add(characterDescription);

            var dossierLine = new VisualElement();
            dossierLine.style.height = 1;
            dossierLine.style.marginTop = 18;
            dossierLine.style.marginBottom = 12;
            dossierLine.style.backgroundColor = new Color(Border.r, Border.g, Border.b, 0.75f);
            copy.Add(dossierLine);
            var controls = CreateLabel("战术档案已载入  ·  方向键可切换", 11, Dim);
            controls.style.letterSpacing = 1;
            copy.Add(controls);
            identity.Add(copy);

            var next = CreateArrowButton("›", () => ChangeCharacter(1));
            next.name = "next-character-button";
            identity.Add(next);
            card.Add(identity);

            var railHeader = new VisualElement();
            railHeader.style.flexDirection = FlexDirection.Row;
            railHeader.style.alignItems = Align.Center;
            railHeader.style.justifyContent = Justify.SpaceBetween;
            railHeader.Add(CreateSectionTitle("幸存者席位"));
            railHeader.Add(CreateLabel("点击编号直接选择", 11, Dim));
            card.Add(railHeader);

            characterRail = new VisualElement { name = "character-rail" };
            characterRail.style.flexDirection = FlexDirection.Row;
            characterRail.style.flexWrap = Wrap.Wrap;
            characterRail.style.marginTop = 7;
            characterRail.style.marginBottom = 12;
            card.Add(characterRail);

            var stats = new VisualElement { name = "character-stats" };
            stats.style.flexDirection = FlexDirection.Row;
            stats.style.flexWrap = Wrap.Wrap;
            stats.style.backgroundColor = Html("#0D1620");
            SetBorder(stats, Border, 1, 10);
            healthValue = AddStat(stats, "生命");
            moveValue = AddStat(stats, "移动");
            pickupValue = AddStat(stats, "拾取");
            armorValue = AddStat(stats, "护甲");
            criticalValue = AddStat(stats, "暴击");
            weaponValue = AddStat(stats, "初始武器");
            card.Add(stats);

            var skinHeader = new VisualElement();
            skinHeader.style.flexDirection = FlexDirection.Row;
            skinHeader.style.alignItems = Align.Center;
            skinHeader.style.justifyContent = Justify.SpaceBetween;
            skinHeader.style.marginTop = 14;
            skinHeader.Add(CreateSectionTitle("作战涂装"));
            skinHeader.Add(CreateLabel("选择角色外观", 11, Dim));
            card.Add(skinHeader);

            skinList = new VisualElement { name = "skin-list" };
            skinList.style.flexDirection = FlexDirection.Row;
            skinList.style.flexWrap = Wrap.Wrap;
            skinList.style.marginTop = 8;
            card.Add(skinList);
            return card;
        }

        private VisualElement BuildFooter(GameSession session)
        {
            var footer = new VisualElement { name = "main-menu-footer" };
            footer.style.minHeight = 38;
            footer.style.flexDirection = FlexDirection.Row;
            footer.style.alignItems = Align.Center;
            footer.style.justifyContent = Justify.SpaceBetween;
            footer.style.borderTopWidth = 1;
            footer.style.borderTopColor = new Color(Border.r, Border.g, Border.b, 0.7f);
            footer.style.paddingTop = 14;
            footer.Add(CreateLabel("WASD / 方向键移动  ·  ESC 暂停  ·  F2 调试", 12, Dim));

            var result = session.LastResult ?? session.Profile.LastResult;
            if (result == null)
            {
                footer.Add(CreateLabel("单机模式 · 自动保存", 12, Dim));
                return footer;
            }

            var resultColor = result.Victory ? Success : Accent;
            footer.Add(CreateLabel(
                $"上局 { (result.Victory ? "胜利" : "失败") }  ·  {result.SurvivalTime:0}s  ·  {result.KillCount} 击杀  ·  Lv.{result.MaxLevel}",
                12, resultColor));
            return footer;
        }

        private void RefreshSelection()
        {
            var config = AppRoot.Instance.Session.Config;
            var character = config.Characters.Characters[characterIndex];
            var skins = GetSkins(character.Id);
            skinIndex = Mathf.Clamp(skinIndex, 0, Mathf.Max(0, skins.Count - 1));
            var skin = skins[skinIndex];
            var weapon = config.Weapons.Weapons.FirstOrDefault(value => value.Id == character.StartingWeaponId);

            characterPosition.text = $"{characterIndex + 1:00} / {config.Characters.Characters.Count:00}";
            characterName.text = character.Name;
            characterDescription.text = character.Description;
            skinName.text = skin.Name.ToUpperInvariant();
            healthValue.text = character.MaxHp.ToString("0");
            moveValue.text = character.MoveSpeed.ToString("0");
            pickupValue.text = character.PickupRadius.ToString("0");
            armorValue.text = character.Armor.ToString("0");
            criticalValue.text = $"{character.CritRate * 100f:0}%";
            weaponValue.text = weapon?.Name ?? character.StartingWeaponId;
            portrait.sprite = LoadPortrait(skin.ModelAsset);
            selectedCharacterCallout.text = $"当前选择  /  {character.Name}  ·  {weaponValue.text}";

            if (ColorUtility.TryParseHtmlString(skin.Palette.Accent, out var paletteAccent))
            {
                portraitGlow.style.backgroundColor = new Color(paletteAccent.r, paletteAccent.g, paletteAccent.b, 0.2f);
                previewAccent.style.backgroundColor = paletteAccent;
            }

            characterRail.Clear();
            for (var index = 0; index < config.Characters.Characters.Count; index++)
            {
                var captured = index;
                characterRail.Add(CreateCharacterSlot(config.Characters.Characters[index], index,
                    index == characterIndex, () => SelectCharacter(captured)));
            }

            skinList.Clear();
            for (var index = 0; index < skins.Count; index++)
            {
                var captured = index;
                skinList.Add(CreateSkinButton(skins[index], index == skinIndex, () =>
                {
                    skinIndex = captured;
                    RefreshSelection();
                }));
            }
        }

        private void ChangeCharacter(int direction)
        {
            var count = AppRoot.Instance.Session.Config.Characters.Characters.Count;
            if (count == 0) return;
            SelectCharacter((characterIndex + direction + count) % count);
        }

        private void SelectCharacter(int index)
        {
            characterIndex = index;
            var character = AppRoot.Instance.Session.Config.Characters.Characters[characterIndex];
            var skins = GetSkins(character.Id);
            AppRoot.Instance.Session.Profile.SelectedSkinByCharacter.TryGetValue(character.Id, out var selectedSkinId);
            skinIndex = Mathf.Max(0, skins.FindIndex(value => value.Id == selectedSkinId));
            RefreshSelection();
        }

        private void Launch(GameMode mode)
        {
            var config = AppRoot.Instance.Session.Config;
            var character = config.Characters.Characters[characterIndex];
            var skins = GetSkins(character.Id);
            AppRoot.Instance.StartGame(mode, character.Id, skins[skinIndex].Id);
        }

        private void ShowSettings()
        {
            BuildSettingsPage(SettingsTab.Audio);
        }

        private void BuildSettingsPage(SettingsTab activeTab)
        {
            screen.Clear();
            screen.style.backgroundColor = Background;
            AddAtmosphere(screen);

            var container = new VisualElement();
            container.style.width = Length.Percent(100);
            container.style.height = Length.Percent(100);
            container.style.maxWidth = 980;
            container.style.alignSelf = Align.Center;
            container.style.flexGrow = 1;
            container.style.paddingLeft = 40;
            container.style.paddingRight = 40;
            container.style.paddingTop = 28;
            container.style.paddingBottom = 24;
            screen.Add(container);

            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.justifyContent = Justify.SpaceBetween;
            var titleGroup = new VisualElement();
            var eyebrow = CreateLabel("SYSTEM SETTINGS", 12, Accent);
            eyebrow.style.unityFontStyleAndWeight = FontStyle.Bold;
            eyebrow.style.letterSpacing = 3;
            titleGroup.Add(eyebrow);
            titleGroup.Add(CreateLabel("游戏设置", 36, Text));
            header.Add(titleGroup);
            var returnButton = CreateSecondaryButton("返回主菜单", () =>
            {
                AppRoot.Instance.SaveSettings();
                Build();
            });
            returnButton.name = "settings-back-button";
            returnButton.style.width = 130;
            header.Add(returnButton);
            container.Add(header);

            var body = new VisualElement { name = "settings-body" };
            body.style.flexDirection = FlexDirection.Row;
            body.style.flexGrow = 1;
            body.style.minHeight = 0;
            body.style.marginTop = 18;
            body.style.alignItems = Align.Stretch;
            container.Add(body);

            var tabs = new VisualElement { name = "settings-tabs" };
            tabs.style.flexDirection = FlexDirection.Column;
            tabs.style.flexShrink = 0;
            tabs.style.width = 148;
            tabs.style.marginRight = 18;
            tabs.style.paddingTop = 4;
            foreach (SettingsTab tab in Enum.GetValues(typeof(SettingsTab)))
            {
                var captured = tab;
                var button = CreateSecondaryButton(SettingsTabLabel(tab), () => BuildSettingsPage(captured));
                button.name = $"settings-tab-{tab}";
                button.style.width = Length.Percent(100);
                button.style.marginLeft = 0;
                button.style.marginRight = 0;
                button.style.marginTop = 0;
                button.style.marginBottom = 8;
                if (tab == activeTab)
                {
                    button.style.backgroundColor = AccentDark;
                    SetBorder(button, Accent, 1, 8);
                }
                tabs.Add(button);
            }
            body.Add(tabs);

            var content = new VisualElement { name = "settings-content" };
            content.style.flexGrow = 1;
            content.style.flexShrink = 1;
            content.style.minWidth = 0;
            content.style.minHeight = 0;
            content.style.flexDirection = FlexDirection.Column;
            body.Add(content);

            var scroll = new ScrollView(ScrollViewMode.Vertical) { name = "settings-scroll" };
            scroll.style.flexGrow = 1;
            scroll.style.minHeight = 0;
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            content.Add(scroll);
            var card = CreateCard();
            card.name = $"settings-panel-{activeTab}";
            card.style.paddingLeft = 24;
            card.style.paddingRight = 24;
            card.style.paddingTop = 18;
            card.style.paddingBottom = 22;
            var settings = AppRoot.Instance.Session.Settings;

            switch (activeTab)
            {
                case SettingsTab.Audio:
                    AddSettingsIntro(card, "音量与听觉反馈");
                    AddIntSlider(card, "主音量", "master-volume", 0, 100,
                        Mathf.RoundToInt(settings.MasterVolume * 100f), value => settings.MasterVolume = value / 100f, "%");
                    AddIntSlider(card, "音效音量", "sfx-volume", 0, 100,
                        Mathf.RoundToInt(settings.SfxVolume * 100f), value => settings.SfxVolume = value / 100f, "%");
                    AddIntSlider(card, "音乐音量", "music-volume", 0, 100,
                        Mathf.RoundToInt(settings.MusicVolume * 100f), value => settings.MusicVolume = value / 100f, "%");
                    break;
                case SettingsTab.Display:
                    AddSettingsIntro(card, "显示效果与性能相关选项");
                    AddToggle(card, "全屏显示", "fullscreen", settings.Fullscreen, value =>
                    {
                        settings.Fullscreen = value;
                        DisplaySettingsService.Apply(settings);
                        UpdateDisplayModeButtons();
                    });
                    AddToggle(card, "屏幕震动", "screen-shake", settings.ScreenShake,
                        value => settings.ScreenShake = value);
                    AddToggle(card, "显示伤害数字", "damage-numbers", settings.DamageNumbers,
                        value => settings.DamageNumbers = value);
                    AddToggle(card, "显示性能监控", "performance-monitor", settings.ShowPerformanceMonitor,
                        value => settings.ShowPerformanceMonitor = value);
                    AddParticleQualityButton(card, settings);
                    AddIntSlider(card, "敌人最大显示数量", "max-enemy-display", 50, 1000,
                        settings.MaxEnemyDisplay, value => settings.MaxEnemyDisplay = value, string.Empty, 10);
                    break;
                case SettingsTab.Crates:
                    AddSettingsIntro(card, "可见废墟补给箱的数量、补刷与开箱效果权重");
                    AddIntSlider(card, "补给箱生成个数", "crate-count", 0, 30, settings.CrateCount,
                        value => settings.CrateCount = value);
                    AddIntSlider(card, "宝箱刷新概率", "crate-refresh-chance", 0, 100,
                        settings.CrateRefreshChance, value => settings.CrateRefreshChance = value, "%");
                    AddSettingsHint(card, "每 40 秒判定一次；场上未满且概率通过时补充 1 个。");
                    AddSettingsGroup(card, "开箱效果权重");
                    AddSettingsHint(card, "相对权重，0 表示该效果不会出现。");
                    foreach (var effect in CrateEffectSettings)
                    {
                        settings.CrateEffectWeights.TryGetValue(effect.Id, out var value);
                        AddIntSlider(card, effect.Label, $"crate-weight-{effect.Id}", 0, 20,
                            Mathf.RoundToInt(value), weight => settings.CrateEffectWeights[effect.Id] = weight);
                    }
                    AddSettingsGroup(card, "麻醉胶囊参数");
                    AddSettingsHint(card, "开箱后降低场上怪物移动速度，含冲刺。");
                    AddIntSlider(card, "持续时间", "anesthetic-capsule-duration", 5, 600,
                        Mathf.RoundToInt(settings.AnestheticCapsuleDuration),
                        value => settings.AnestheticCapsuleDuration = value, " 秒", 5);
                    AddIntSlider(card, "减速幅度", "anesthetic-capsule-slow", 5, 80,
                        Mathf.RoundToInt(settings.AnestheticCapsuleSlowPercent),
                        value => settings.AnestheticCapsuleSlowPercent = value, "%", 5);
                    break;
                case SettingsTab.HiddenCrates:
                    AddSettingsIntro(card, "透明隐藏宝箱：滑板、狙击、追踪眼镜、胶囊足球与清屏献祭");
                    AddIntSlider(card, "隐藏宝箱个数", "hidden-crate-count", 0, 20,
                        settings.HiddenCrateCount, value => settings.HiddenCrateCount = value);
                    AddIntSlider(card, "隐藏宝箱补刷概率", "hidden-crate-refresh-chance", 0, 100,
                        settings.HiddenCrateRefreshChance, value => settings.HiddenCrateRefreshChance = value, "%");
                    AddSettingsHint(card, "每 40 秒判定一次；0% 表示不补刷。");
                    AddSettingsGroup(card, "开箱效果权重");
                    AddSettingsHint(card, "相对权重，0 表示该效果不会出现。");
                    foreach (var effect in HiddenCrateEffectSettings)
                    {
                        var captured = effect.Id;
                        settings.HiddenCrateEffectWeights.TryGetValue(captured, out var weight);
                        AddIntSlider(card, effect.Label, $"hidden-crate-weight-{captured}", 0, 20,
                            Mathf.RoundToInt(weight), value => settings.HiddenCrateEffectWeights[captured] = value);
                    }
                    AddSettingsGroup(card, "效果持续时间");
                    AddIntSlider(card, "滑板车", "scooter-duration", 5, 600,
                        Mathf.RoundToInt(settings.ScooterBoostDuration), value => settings.ScooterBoostDuration = value, " 秒", 5);
                    AddIntSlider(card, "狙击枪", "sniper-duration", 5, 600,
                        Mathf.RoundToInt(settings.SniperRifleDuration), value => settings.SniperRifleDuration = value, " 秒", 5);
                    AddIntSlider(card, "追踪眼镜", "crate-guide-duration", 5, 600,
                        Mathf.RoundToInt(settings.CrateGuideDuration), value => settings.CrateGuideDuration = value, " 秒", 5);
                    AddIntSlider(card, "胶囊足球", "capsule-football-duration", 5, 600,
                        Mathf.RoundToInt(settings.CapsuleFootballDuration), value => settings.CapsuleFootballDuration = value, " 秒", 5);
                    break;
                case SettingsTab.Altar:
                    AddSettingsIntro(card, "祭坛数量、补刷、耗血、效果权重与效果参数");
                    AddIntSlider(card, "祭坛生成个数", "altar-count", 0, 20, settings.AltarCount,
                        value => settings.AltarCount = value);
                    AddIntSlider(card, "祭坛补刷概率", "altar-refresh-chance", 0, 100,
                        settings.AltarRefreshChance, value => settings.AltarRefreshChance = value, "%");
                    AddSettingsHint(card, "每 40 秒判定一次；0% 表示不补刷。");
                    AddSettingsGroup(card, "祭坛耗血（最大生命）");
                    AddSettingsHint(card, "踩祭坛时按效果扣除最大生命百分比，至少保留 1 点生命。");
                    AddIntSlider(card, "献血加攻", "altar-blood-cost", 1, 90,
                        Mathf.RoundToInt(settings.AltarBloodPactHpCost), value => settings.AltarBloodPactHpCost = value, "%");
                    AddIntSlider(card, "磁力爆发", "altar-magnet-cost", 1, 90,
                        Mathf.RoundToInt(settings.AltarMagnetBurstHpCost), value => settings.AltarMagnetBurstHpCost = value, "%");
                    AddIntSlider(card, "全图传送", "altar-teleport-cost", 1, 90,
                        Mathf.RoundToInt(settings.AltarTeleportHpCost), value => settings.AltarTeleportHpCost = value, "%");
                    AddIntSlider(card, "麻醉型手表", "altar-stun-watch-cost", 1, 90,
                        Mathf.RoundToInt(settings.AltarStunWatchHpCost), value => settings.AltarStunWatchHpCost = value, "%");
                    AddSettingsGroup(card, "效果权重");
                    AddSettingsHint(card, "相对权重，0 表示该效果不会出现。");
                    foreach (var effect in AltarEffectSettings)
                    {
                        var captured = effect.Id;
                        settings.AltarEffectWeights.TryGetValue(captured, out var weight);
                        AddFloatSlider(card, effect.Label, $"altar-weight-{captured}", 0f, 20f, weight,
                            value => settings.AltarEffectWeights[captured] = value, 0.5f, "");
                    }
                    AddSettingsGroup(card, "献血加攻参数");
                    AddFloatSlider(card, "伤害加成", "altar-blood-damage", 0.05f, 2f,
                        settings.AltarBloodPactDamageBonus, value => settings.AltarBloodPactDamageBonus = value, 0.05f, "");
                    AddIntSlider(card, "持续时间", "altar-blood-duration", 1, 120,
                        Mathf.RoundToInt(settings.AltarBloodPactDuration), value => settings.AltarBloodPactDuration = value, " 秒");
                    AddSettingsGroup(card, "磁力爆发参数");
                    AddSettingsHint(card, "祭坛磁力爆发固定为全图吸附，持续期间新掉落的经验也会被吸入。");
                    AddIntSlider(card, "持续时间", "altar-magnet-duration", 1, 60,
                        Mathf.RoundToInt(settings.AltarMagnetDuration), value => settings.AltarMagnetDuration = value, " 秒");
                    AddSettingsGroup(card, "麻醉型手表参数");
                    AddSettingsHint(card, "眩晕场上全部 Boss，期间 Boss 无法移动或发动攻击。");
                    AddIntSlider(card, "眩晕时间", "altar-stun-watch-duration", 1, 60,
                        Mathf.RoundToInt(settings.AltarStunWatchDuration), value => settings.AltarStunWatchDuration = value, " 秒");
                    break;
                case SettingsTab.Map:
                    AddSettingsIntro(card, "地图皮肤、烤鸡腿与毒雾相关参数");
                    AddSettingsGroup(card, "地图皮肤");
                    AddSettingsHint(card, "战斗开局铺设所选地图；海水区域不可进入。");
                    AddMapSkinPicker(card, settings);
                    AddSettingsGroup(card, "烤鸡腿");
                    AddSettingsHint(card, "靠近拾取后生命值回满；被吃掉后可按概率补刷。");
                    AddIntSlider(card, "烤鸡腿生成个数", "healing-chicken-count", 0, 20,
                        settings.HealingChickenCount, value => settings.HealingChickenCount = value);
                    AddIntSlider(card, "烤鸡腿刷新概率", "healing-chicken-refresh-chance", 0, 100,
                        settings.HealingChickenRefreshChance, value => settings.HealingChickenRefreshChance = value, "%");
                    AddSettingsHint(card, "每 40 秒判定一次；场上未满且概率通过时补充 1 个。");
                    AddSettingsGroup(card, "毒雾");
                    AddIntSlider(card, "毒雾生成个数", "poison-fog-count", 0, 20,
                        settings.PoisonFogCount, value => settings.PoisonFogCount = value);
                    AddIntSlider(card, "毒雾半径下限", "poison-radius-min", 50, 400,
                        Mathf.RoundToInt(settings.PoisonFogRadiusMin), value =>
                        {
                            settings.PoisonFogRadiusMin = value;
                            if (settings.PoisonFogRadiusMax < value) settings.PoisonFogRadiusMax = value;
                        }, " 像素", 5);
                    AddIntSlider(card, "毒雾半径上限", "poison-radius-max", 50, 400,
                        Mathf.RoundToInt(settings.PoisonFogRadiusMax), value =>
                        {
                            settings.PoisonFogRadiusMax = value;
                            if (settings.PoisonFogRadiusMin > value) settings.PoisonFogRadiusMin = value;
                        }, " 像素", 5);
                    AddFloatSlider(card, "毒雾每秒伤害", "poison-fog-dps", 0f, 50f,
                        settings.PoisonFogDps, value => settings.PoisonFogDps = value, 0.5f, "% 最大生命");
                    break;
                case SettingsTab.Waves:
                    AddSettingsIntro(card, "波次节奏、精英/首领刷新与 Boss；无时间限制，清完全部波次后结算");
                    AddIntSlider(card, "总波数", "wave-count", 1, 30, settings.WaveCount,
                        value => settings.WaveCount = value);
                    AddIntSlider(card, "首波小怪个数", "first-wave-mob-count", 1, 500,
                        settings.FirstWaveMobCount, value => settings.FirstWaveMobCount = value);
                    AddFloatSlider(card, "每波小怪数量倍率", "wave-mob-multiplier", 0.25f, 50f,
                        settings.WaveMobCountMultiplier, value => settings.WaveMobCountMultiplier = value, 0.05f, "×");
                    AddSettingsHint(card, "首波个数为基准；倍率作用于各波普通小怪数量，后续波次按增长系数递推。");
                    AddIntSlider(card, "Boss 个数", "boss-count", 0, 10, settings.BossCount,
                        value => settings.BossCount = value);

                    AddSettingsGroup(card, "精英怪刷新（随波次）");
                    AddSettingsHint(card, "从起始波起按概率追加精英；每波对「数量上限」次独立判定。概率 = 基础 +（当前波 - 起始波）× 增幅。");
                    AddIntSlider(card, "精英起始波次", "elite-start-wave", 1, 30, settings.EliteStartWave,
                        value => settings.EliteStartWave = value);
                    AddIntSlider(card, "精英基础概率", "elite-chance-base", 0, 100,
                        Mathf.RoundToInt(settings.EliteChanceBase), value => settings.EliteChanceBase = value, "%");
                    AddIntSlider(card, "精英每波增幅", "elite-chance-growth", 0, 50,
                        Mathf.RoundToInt(settings.EliteChanceGrowthPerWave),
                        value => settings.EliteChanceGrowthPerWave = value, "%");
                    AddIntSlider(card, "精英概率上限", "elite-chance-max", 0, 100,
                        Mathf.RoundToInt(settings.EliteChanceMax), value => settings.EliteChanceMax = value, "%");
                    AddIntSlider(card, "精英每波数量上限", "elite-max-count", 0, 20, settings.EliteMaxCountPerWave,
                        value => settings.EliteMaxCountPerWave = value);

                    AddSettingsGroup(card, "首领怪刷新（随波次）");
                    AddSettingsHint(card, "首领包含领袖尸与肥尸，判定规则与精英相同。");
                    AddIntSlider(card, "首领起始波次", "leader-start-wave", 1, 30, settings.LeaderStartWave,
                        value => settings.LeaderStartWave = value);
                    AddIntSlider(card, "首领基础概率", "leader-chance-base", 0, 100,
                        Mathf.RoundToInt(settings.LeaderChanceBase), value => settings.LeaderChanceBase = value, "%");
                    AddIntSlider(card, "首领每波增幅", "leader-chance-growth", 0, 50,
                        Mathf.RoundToInt(settings.LeaderChanceGrowthPerWave),
                        value => settings.LeaderChanceGrowthPerWave = value, "%");
                    AddIntSlider(card, "首领概率上限", "leader-chance-max", 0, 100,
                        Mathf.RoundToInt(settings.LeaderChanceMax), value => settings.LeaderChanceMax = value, "%");
                    AddIntSlider(card, "首领每波数量上限", "leader-max-count", 0, 20, settings.LeaderMaxCountPerWave,
                        value => settings.LeaderMaxCountPerWave = value);
                    break;
                case SettingsTab.Skills:
                    AddSettingsIntro(card, "武器技能相关参数");
                    AddSettingsGroup(card, "飞轮术");
                    AddSettingsHint(card, "轨道半径按等级在初始与满级范围之间线性插值；旋转速度倍率作用于各等级基础速度。");
                    AddIntSlider(card, "飞轮术初始范围", "rotating-knife-base-orbit-radius", 40, 400,
                        Mathf.RoundToInt(settings.RotatingKnifeBaseOrbitRadius),
                        value => settings.RotatingKnifeBaseOrbitRadius = value, " 像素", 5);
                    AddIntSlider(card, "飞轮术满级范围", "rotating-knife-max-orbit-radius", 40, 400,
                        Mathf.RoundToInt(settings.RotatingKnifeMaxOrbitRadius), value =>
                        {
                            settings.RotatingKnifeMaxOrbitRadius = value;
                            if (settings.RotatingKnifeBaseOrbitRadius > value)
                                settings.RotatingKnifeBaseOrbitRadius = value;
                        }, " 像素", 5);
                    AddFloatSlider(card, "飞轮术旋转速度倍率", "rotating-knife-rotation-speed-mul", 0.25f, 3f,
                        settings.RotatingKnifeRotationSpeedMul,
                        value => settings.RotatingKnifeRotationSpeedMul = value, 0.05f, "×");
                    AddSettingsGroup(card, "伏波琴");
                    AddSettingsHint(card, "光环半径按等级在初始与满级范围之间线性插值。");
                    AddIntSlider(card, "伏波琴初始范围", "fubo-qin-base-radius", 40, 400,
                        Mathf.RoundToInt(settings.FuboQinBaseAuraRadius),
                        value => settings.FuboQinBaseAuraRadius = value, " 像素", 5);
                    AddIntSlider(card, "伏波琴满级范围", "fubo-qin-max-radius", 40, 400,
                        Mathf.RoundToInt(settings.FuboQinMaxAuraRadius), value =>
                        {
                            settings.FuboQinMaxAuraRadius = value;
                            if (settings.FuboQinBaseAuraRadius > value)
                                settings.FuboQinBaseAuraRadius = value;
                        }, " 像素", 5);
                    break;
            }
            scroll.Add(card);

            var actions = new VisualElement();
            actions.style.flexDirection = FlexDirection.Row;
            actions.style.marginTop = 12;
            var reset = CreateDangerButton("恢复全部默认", () =>
            {
                AppRoot.Instance.Session.Settings = new GameSettings();
                AppRoot.Instance.SaveSettings();
                BuildSettingsPage(activeTab);
            });
            reset.name = "reset-settings-button";
            reset.style.width = 180;
            reset.style.marginRight = 10;
            actions.Add(reset);

            var save = CreateActionButton("保存并返回", "应用本机设置", () =>
            {
                AppRoot.Instance.SaveSettings();
                Build();
            }, true);
            save.name = "save-settings-button";
            save.style.flexGrow = 1;
            save.style.marginTop = 0;
            save.style.marginBottom = 0;
            actions.Add(save);
            content.Add(actions);
        }

        private static string SettingsTabLabel(SettingsTab tab) => tab switch
        {
            SettingsTab.Audio => "音效",
            SettingsTab.Display => "画面",
            SettingsTab.Crates => "补给箱",
            SettingsTab.HiddenCrates => "隐藏箱",
            SettingsTab.Altar => "祭坛",
            SettingsTab.Map => "地图",
            SettingsTab.Skills => "技能",
            _ => "波次"
        };

        private static void AddSettingsIntro(VisualElement parent, string text)
        {
            var label = CreateLabel(text, 14, Muted);
            label.style.marginBottom = 8;
            parent.Add(label);
        }

        private static void AddSettingsGroup(VisualElement parent, string text)
        {
            var label = CreateLabel(text, 14, Muted);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.marginTop = 15;
            label.style.paddingTop = 12;
            label.style.borderTopWidth = 1;
            label.style.borderTopColor = Border;
            parent.Add(label);
        }

        private static void AddSettingsHint(VisualElement parent, string text)
        {
            var label = CreateLabel(text, 12, Dim);
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.marginTop = 0;
            label.style.marginBottom = 6;
            parent.Add(label);
        }

        private void AddIntSlider(VisualElement parent, string label, string controlName, int min, int max,
            int value, Action<int> changed, string suffix = "", int step = 1)
        {
            var field = new VisualElement { name = controlName };
            field.style.marginTop = 7;
            field.style.marginBottom = 7;
            var header = CreateSettingFieldHeader(label, FormatIntSettingValue(value, suffix), out var valueLabel);
            field.Add(header);
            var slider = new SliderInt(min, max)
            {
                name = controlName + "-slider",
                value = Mathf.Clamp(value, min, max),
                showInputField = false,
                tooltip = suffix
            };
            StyleField(slider);
            slider.style.minHeight = 24;
            slider.style.marginTop = 3;
            slider.style.marginBottom = 0;
            slider.RegisterValueChangedCallback(evt =>
            {
                var snapped = Mathf.Clamp(Mathf.RoundToInt(evt.newValue / (float)Mathf.Max(1, step)) * step, min, max);
                slider.SetValueWithoutNotify(snapped);
                valueLabel.text = FormatIntSettingValue(snapped, suffix);
                changed(snapped);
            });
            field.Add(slider);
            parent.Add(field);
        }

        private void AddFloatSlider(VisualElement parent, string label, string controlName, float min, float max,
            float value, Action<float> changed, float step, string suffix)
        {
            var field = new VisualElement { name = controlName };
            field.style.marginTop = 7;
            field.style.marginBottom = 7;
            var header = CreateSettingFieldHeader(label, FormatFloatSettingValue(value, suffix), out var valueLabel);
            field.Add(header);
            var slider = new Slider(min, max)
            {
                name = controlName + "-slider",
                value = Mathf.Clamp(value, min, max),
                showInputField = false,
                tooltip = suffix
            };
            StyleField(slider);
            slider.style.minHeight = 24;
            slider.style.marginTop = 3;
            slider.style.marginBottom = 0;
            slider.RegisterValueChangedCallback(evt =>
            {
                var snapped = Mathf.Clamp(Mathf.Round(evt.newValue / step) * step, min, max);
                slider.SetValueWithoutNotify(snapped);
                valueLabel.text = FormatFloatSettingValue(snapped, suffix);
                changed(snapped);
            });
            field.Add(slider);
            parent.Add(field);
        }

        private static VisualElement CreateSettingFieldHeader(string label, string value, out Label valueLabel)
        {
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.justifyContent = Justify.SpaceBetween;
            header.Add(CreateLabel(label, 14, Text));
            valueLabel = CreateLabel(value, 13, Brand);
            valueLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.Add(valueLabel);
            return header;
        }

        private static string FormatIntSettingValue(int value, string suffix) => $"{value}{suffix}";

        private static string FormatFloatSettingValue(float value, string suffix) =>
            suffix == "×" ? $"×{value:0.00}" : $"{value:0.0}{suffix}";

        private void AddToggle(VisualElement parent, string label, string controlName, bool value, Action<bool> changed)
        {
            var toggle = new Toggle(label) { name = controlName, value = value };
            StyleField(toggle);
            toggle.RegisterValueChangedCallback(evt => changed(evt.newValue));
            parent.Add(toggle);
        }

        private void AddParticleQualityButton(VisualElement parent, GameSettings settings)
        {
            var button = new Button { name = "particle-quality", text = ParticleQualityLabel(settings.ParticleQuality) };
            StyleField(button);
            button.style.unityTextAlign = TextAnchor.MiddleLeft;
            button.clicked += () =>
            {
                settings.ParticleQuality = settings.ParticleQuality switch
                {
                    ParticleQuality.Low => ParticleQuality.Medium,
                    ParticleQuality.Medium => ParticleQuality.High,
                    _ => ParticleQuality.Low
                };
                button.text = ParticleQualityLabel(settings.ParticleQuality);
            };
            parent.Add(button);
        }

        private void AddMapSkinPicker(VisualElement parent, GameSettings settings)
        {
            settings.MapSkinId = GameSettings.NormalizeMapSkinId(settings.MapSkinId);
            var row = new VisualElement { name = "map-skin-picker" };
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;
            row.style.marginTop = 6;
            row.style.marginBottom = 10;

            foreach (var skinId in GameSettings.MapSkinOptions)
            {
                var captured = skinId;
                var selected = string.Equals(settings.MapSkinId, captured, StringComparison.OrdinalIgnoreCase);
                var card = new Button { name = $"map-skin-{captured}" };
                card.style.width = 118;
                card.style.height = 148;
                card.style.marginRight = 10;
                card.style.marginBottom = 8;
                card.style.paddingLeft = 8;
                card.style.paddingRight = 8;
                card.style.paddingTop = 8;
                card.style.paddingBottom = 8;
                card.style.flexDirection = FlexDirection.Column;
                card.style.alignItems = Align.Center;
                card.style.backgroundColor = selected
                    ? new Color(Accent.r, Accent.g, Accent.b, 0.2f)
                    : Html("#111B23");
                SetBorder(card, selected ? Accent : Border, selected ? 2 : 1, 10);
                card.clicked += () =>
                {
                    settings.MapSkinId = captured;
                    BuildSettingsPage(SettingsTab.Map);
                };

                var previewKey = captured == MapLayoutCatalog.DryHighlandCoastId
                    ? "dry_highland_coast"
                    : captured;
                var preview = new Image
                {
                    name = $"map-skin-preview-{captured}",
                    sprite = MapArtCatalog.LoadTile(previewKey),
                    scaleMode = ScaleMode.ScaleToFit
                };
                preview.pickingMode = PickingMode.Ignore;
                preview.style.width = 96;
                preview.style.height = 96;
                preview.style.marginBottom = 8;
                card.Add(preview);

                var label = CreateLabel(MapSkinLabel(captured), 12, selected ? Brand : Text);
                label.style.marginBottom = 0;
                label.style.unityTextAlign = TextAnchor.MiddleCenter;
                label.pickingMode = PickingMode.Ignore;
                card.Add(label);
                row.Add(card);
            }

            parent.Add(row);
        }

        private static string MapSkinLabel(string skinId) => skinId switch
        {
            "grass_tile_02" => "草地 02",
            "grass_tile_03" => "草地 03",
            "grass_tile_04" => "草地 04",
            "dry_highland_coast" => "干旱高地·海岸",
            _ => "草地 01"
        };

        private static string ParticleQualityLabel(ParticleQuality quality) => quality switch
        {
            ParticleQuality.Low => "特效质量：低",
            ParticleQuality.High => "特效质量：高",
            _ => "特效质量：中"
        };

        private static void StyleField(VisualElement field)
        {
            field.style.minHeight = 42;
            field.style.marginTop = 8;
            field.style.marginBottom = 8;
            field.style.color = Text;
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.LeftArrow)
            {
                ChangeCharacter(-1);
                evt.StopPropagation();
            }
            else if (evt.keyCode == KeyCode.RightArrow)
            {
                ChangeCharacter(1);
                evt.StopPropagation();
            }
            else if (evt.keyCode == KeyCode.Return && !evt.altKey)
            {
                Launch(GameMode.Normal);
                evt.StopPropagation();
            }
            else if (evt.keyCode == KeyCode.F11 ||
                     (evt.keyCode == KeyCode.Return && evt.altKey))
            {
                ToggleFullscreen();
                evt.StopPropagation();
            }
        }

        private void ToggleFullscreen()
        {
            SetDisplayMode(!AppRoot.Instance.Session.Settings.Fullscreen);
        }

        private void SetDisplayMode(bool fullscreen)
        {
            var settings = AppRoot.Instance.Session.Settings;
            if (settings.Fullscreen == fullscreen)
            {
                UpdateDisplayModeButtons();
                return;
            }

            settings.Fullscreen = fullscreen;
            UpdateDisplayModeButtons();
            AppRoot.Instance.SaveSettings();
        }

        private void UpdateDisplayModeButtons()
        {
            if (windowedButton == null || fullscreenButton == null || AppRoot.Instance == null)
                return;

            var fullscreen = AppRoot.Instance.Session.Settings.Fullscreen;
            StyleDisplayModeButton(windowedButton, !fullscreen);
            StyleDisplayModeButton(fullscreenButton, fullscreen);
        }

        private static void StyleDisplayModeButton(Button button, bool selected)
        {
            button.style.backgroundColor = selected ? AccentDark : PanelRaised;
            button.style.color = selected ? Text : Muted;
            SetBorder(button, selected ? Accent : Border, 1, 8);
        }

        private void ConfirmClearSave()
        {
            ShowConfirmDialog("清除本地存档", "确定要清除本地存档吗？此操作不可撤销。", () => _ = ClearSaveAndRebuild());
        }

        private async Task ClearSaveAndRebuild()
        {
            await AppRoot.Instance.ClearSaveAsync();
            Build();
        }

        private void ShowConfirmDialog(string title, string message, Action onConfirm)
        {
            var overlay = new VisualElement { name = "confirm-overlay" };
            overlay.style.position = Position.Absolute;
            overlay.style.left = 0;
            overlay.style.right = 0;
            overlay.style.top = 0;
            overlay.style.bottom = 0;
            overlay.style.backgroundColor = new Color(0f, 0f, 0f, 0.65f);
            overlay.style.justifyContent = Justify.Center;
            overlay.style.alignItems = Align.Center;

            var card = CreateCard();
            card.style.width = 420;
            card.style.paddingLeft = 28;
            card.style.paddingRight = 28;
            card.style.paddingTop = 24;
            card.style.paddingBottom = 24;
            card.Add(CreateLabel(title, 22, Text));

            var detail = CreateLabel(message, 15, Muted);
            detail.style.whiteSpace = WhiteSpace.Normal;
            detail.style.marginTop = 12;
            card.Add(detail);

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.FlexEnd;
            row.style.marginTop = 20;

            var cancel = CreateSecondaryButton("取消", () => overlay.RemoveFromHierarchy());
            cancel.style.width = 96;
            cancel.style.marginRight = 10;
            row.Add(cancel);

            var confirm = CreateSecondaryButton("确定", () =>
            {
                overlay.RemoveFromHierarchy();
                onConfirm();
            });
            confirm.style.width = 96;
            confirm.style.color = Text;
            confirm.style.backgroundColor = Accent;
            SetBorder(confirm, Accent, 1, 8);
            row.Add(confirm);
            card.Add(row);
            overlay.Add(card);
            screen.Add(overlay);
        }

        private void ApplyResponsiveLayout(bool compact)
        {
            if (mainContent == null || heroPanel == null || previewPanel == null || compactLayout == compact) return;
            compactLayout = compact;
            mainContent.style.flexDirection = compact ? FlexDirection.Column : FlexDirection.Row;
            heroPanel.style.width = Length.Percent(compact ? 100 : 39);
            heroPanel.style.paddingRight = compact ? 0 : 46;
            heroPanel.style.paddingBottom = compact ? 28 : 0;
            previewPanel.style.width = Length.Percent(compact ? 100 : 61);
            previewPanel.style.maxWidth = compact ? StyleKeyword.None : 880;
            previewPanel.style.minWidth = compact ? 0 : 560;

            if (screen == null)
                return;

            var horizontalPadding = compact ? 24 : 48;
            var verticalPadding = compact ? 24 : 30;
            var content = screen.Q<VisualElement>("main-menu-content");
            if (content != null)
            {
                content.style.paddingLeft = horizontalPadding;
                content.style.paddingRight = horizontalPadding;
                content.style.paddingTop = verticalPadding;
                content.style.paddingBottom = verticalPadding;
            }

            var header = screen.Q<VisualElement>("main-menu-header");
            if (header != null)
            {
                header.style.flexDirection = compact ? FlexDirection.Column : FlexDirection.Row;
                header.style.alignItems = compact ? Align.FlexStart : Align.Center;
            }

            var headerActions = screen.Q<VisualElement>("header-actions");
            if (headerActions != null)
                headerActions.style.marginTop = compact ? 12 : 0;
        }

        private static VisualElement CreateScreen(VisualElement root)
        {
            root.Clear();
            root.style.flexGrow = 1;
            root.style.width = Length.Percent(100);
            root.style.height = Length.Percent(100);
            root.style.backgroundColor = Background;
            var result = new VisualElement();
            result.style.flexGrow = 1;
            result.style.width = Length.Percent(100);
            result.style.height = Length.Percent(100);
            result.style.flexDirection = FlexDirection.Column;
            result.style.backgroundColor = Background;
            root.Add(result);
            return result;
        }

        private static void AddAtmosphere(VisualElement root)
        {
            var gradient = new VisualElement();
            gradient.pickingMode = PickingMode.Ignore;
            gradient.style.position = Position.Absolute;
            gradient.style.left = 0;
            gradient.style.right = 0;
            gradient.style.top = 0;
            gradient.style.bottom = 0;
            gradient.style.backgroundColor = new Color(BackgroundMid.r, BackgroundMid.g, BackgroundMid.b, 0.42f);
            root.Add(gradient);

            var redGlow = CreateGlow(new Color(0.35f, 0.09f, 0.12f, 0.24f), 700, -160, -220, null, null);
            root.Add(redGlow);
            var greenGlow = CreateGlow(new Color(0.07f, 0.25f, 0.21f, 0.2f), 820, null, null, -240, -280);
            root.Add(greenGlow);

            var grid = new VisualElement { name = "cover-grid" };
            grid.pickingMode = PickingMode.Ignore;
            grid.style.position = Position.Absolute;
            grid.style.left = 0;
            grid.style.right = 0;
            grid.style.top = 0;
            grid.style.bottom = 0;
            for (var index = 1; index < 8; index++)
            {
                var vertical = new VisualElement();
                vertical.pickingMode = PickingMode.Ignore;
                vertical.style.position = Position.Absolute;
                vertical.style.left = Length.Percent(index * 12.5f);
                vertical.style.top = 0;
                vertical.style.bottom = 0;
                vertical.style.width = 1;
                vertical.style.backgroundColor = new Color(Border.r, Border.g, Border.b, 0.06f);
                grid.Add(vertical);
            }
            for (var index = 1; index < 5; index++)
            {
                var horizontal = new VisualElement();
                horizontal.pickingMode = PickingMode.Ignore;
                horizontal.style.position = Position.Absolute;
                horizontal.style.left = 0;
                horizontal.style.right = 0;
                horizontal.style.top = Length.Percent(index * 20f);
                horizontal.style.height = 1;
                horizontal.style.backgroundColor = new Color(Border.r, Border.g, Border.b, 0.05f);
                grid.Add(horizontal);
            }
            root.Add(grid);

            var horizon = new VisualElement();
            horizon.pickingMode = PickingMode.Ignore;
            horizon.style.position = Position.Absolute;
            horizon.style.left = 0;
            horizon.style.right = 0;
            horizon.style.bottom = 0;
            horizon.style.height = 210;
            horizon.style.backgroundColor = new Color(BackgroundEnd.r, BackgroundEnd.g, BackgroundEnd.b, 0.34f);
            horizon.style.borderTopWidth = 1;
            horizon.style.borderTopColor = new Color(0.25f, 0.42f, 0.36f, 0.14f);
            root.Add(horizon);
        }

        private static VisualElement CreateGlow(Color color, float size, float? left, float? top, float? right,
            float? bottom)
        {
            var glow = new VisualElement();
            glow.pickingMode = PickingMode.Ignore;
            glow.style.position = Position.Absolute;
            glow.style.width = size;
            glow.style.height = size;
            if (left.HasValue) glow.style.left = left.Value;
            if (top.HasValue) glow.style.top = top.Value;
            if (right.HasValue) glow.style.right = right.Value;
            if (bottom.HasValue) glow.style.bottom = bottom.Value;
            glow.style.backgroundColor = color;
            SetRadius(glow, size * 0.5f);
            return glow;
        }

        private static VisualElement CreateCard()
        {
            var card = new VisualElement();
            card.style.backgroundColor = new Color(Panel.r, Panel.g, Panel.b, 0.96f);
            SetBorder(card, Border, 1, 16);
            return card;
        }

        private static Label CreateLabel(string text, int size, Color color)
        {
            var label = new Label(text);
            label.style.fontSize = size;
            label.style.color = color;
            label.style.marginTop = 0;
            label.style.marginBottom = 0;
            return label;
        }

        private static Label CreateSectionTitle(string text)
        {
            var label = CreateLabel(text, 14, Muted);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.letterSpacing = 2;
            return label;
        }

        private static VisualElement CreateMissionMetric(string title, string value)
        {
            var metric = new VisualElement();
            metric.style.width = Length.Percent(33.333f);
            metric.style.minHeight = 62;
            metric.style.justifyContent = Justify.Center;
            metric.style.paddingLeft = 12;
            metric.style.paddingRight = 8;
            metric.style.borderRightWidth = 1;
            metric.style.borderRightColor = new Color(Border.r, Border.g, Border.b, 0.62f);
            metric.Add(CreateLabel(title, 10, Dim));
            var valueLabel = CreateLabel(value, 15, Text);
            valueLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            valueLabel.style.marginTop = 4;
            metric.Add(valueLabel);
            return metric;
        }

        private static Button CreateCharacterSlot(CharacterConfig character, int index, bool active, Action clicked)
        {
            var button = new Button(clicked)
            {
                name = $"character-slot-{index + 1:00}",
                text = $"{index + 1:00}  {character.Name}",
                tooltip = character.Description
            };
            button.style.minWidth = 112;
            button.style.height = 46;
            button.style.marginRight = 7;
            button.style.marginBottom = 7;
            button.style.paddingLeft = 10;
            button.style.paddingRight = 10;
            button.style.fontSize = 12;
            button.style.unityFontStyleAndWeight = active ? FontStyle.Bold : FontStyle.Normal;
            button.style.unityTextAlign = TextAnchor.MiddleLeft;
            button.style.color = active ? Text : Muted;
            var normal = active ? new Color(Brand.r, Brand.g, Brand.b, 0.18f) : Html("#0D1620");
            var hover = active
                ? new Color(Brand.r, Brand.g, Brand.b, 0.28f)
                : new Color(PanelRaised.r, PanelRaised.g, PanelRaised.b, 0.95f);
            button.style.backgroundColor = normal;
            SetBorder(button, active ? Brand : Border, active ? 2 : 1, 8);
            button.RegisterCallback<PointerEnterEvent>(_ => button.style.backgroundColor = hover);
            button.RegisterCallback<PointerLeaveEvent>(_ => button.style.backgroundColor = normal);
            button.RegisterCallback<FocusInEvent>(_ => SetBorder(button, Brand, active ? 2 : 1, 8));
            button.RegisterCallback<FocusOutEvent>(_ => SetBorder(button, active ? Brand : Border, active ? 2 : 1, 8));
            return button;
        }

        private static Button CreateActionButton(string title, string detail, Action clicked, bool primary)
        {
            var button = new Button(clicked);
            button.text = $"{title}    {detail}";
            button.style.height = 66;
            button.style.width = Length.Percent(100);
            button.style.marginTop = 6;
            button.style.marginBottom = 6;
            button.style.fontSize = 17;
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            button.style.unityTextAlign = TextAnchor.MiddleLeft;
            button.style.paddingLeft = 22;
            button.style.paddingRight = 22;
            button.style.color = Text;
            var normal = primary ? AccentDark : PanelRaised;
            var hover = primary ? Accent : Html("#22343E");
            button.style.backgroundColor = normal;
            SetBorder(button, primary ? Accent : Border, 1, 9);
            RegisterButtonFeedback(button, normal, hover);
            return button;
        }

        private static Button CreateSecondaryButton(string text, Action clicked)
        {
            var button = new Button(clicked) { text = text };
            button.style.height = 45;
            button.style.fontSize = 14;
            button.style.color = Text;
            button.style.backgroundColor = PanelRaised;
            SetBorder(button, Border, 1, 8);
            RegisterButtonFeedback(button, PanelRaised, Html("#22343E"));
            return button;
        }

        private static Button CreateDangerButton(string text, Action clicked)
        {
            var button = new Button(clicked) { text = text };
            button.style.height = 45;
            button.style.fontSize = 14;
            button.style.color = Accent;
            button.style.backgroundColor = Color.clear;
            SetBorder(button, Accent, 1, 8);
            RegisterButtonFeedback(button, Color.clear, new Color(Accent.r, Accent.g, Accent.b, 0.12f));
            return button;
        }

        private static Button CreateArrowButton(string text, Action clicked)
        {
            var button = new Button(clicked) { text = text };
            button.style.width = 44;
            button.style.height = 72;
            button.style.fontSize = 36;
            button.style.color = Muted;
            button.style.backgroundColor = Html("#111A22");
            SetBorder(button, Border, 1, 9);
            RegisterButtonFeedback(button, Html("#111A22"), Html("#22343E"));
            return button;
        }

        private static Button CreateSkinButton(SkinConfig skin, bool active, Action clicked)
        {
            var button = new Button(clicked) { text = skin.Name };
            button.style.minWidth = 142;
            button.style.height = 44;
            button.style.marginRight = 8;
            button.style.marginBottom = 8;
            button.style.paddingLeft = 13;
            button.style.paddingRight = 13;
            button.style.fontSize = 13;
            button.style.color = active ? Text : Muted;
            var baseColor = active ? new Color(Accent.r, Accent.g, Accent.b, 0.18f) : Html("#111B23");
            button.style.backgroundColor = baseColor;
            SetBorder(button, active ? Accent : Border, active ? 2 : 1, 8);

            if (ColorUtility.TryParseHtmlString(skin.Palette.Accent, out var paletteAccent))
                button.style.borderLeftColor = paletteAccent;

            RegisterButtonFeedback(button, baseColor, new Color(Accent.r, Accent.g, Accent.b, 0.28f));
            button.tooltip = skin.Description;
            return button;
        }

        private static void RegisterButtonFeedback(Button button, Color normal, Color hover)
        {
            button.RegisterCallback<PointerEnterEvent>(_ => button.style.backgroundColor = hover);
            button.RegisterCallback<PointerLeaveEvent>(_ => button.style.backgroundColor = normal);
            button.RegisterCallback<FocusInEvent>(_ => button.style.borderTopColor = Accent);
            button.RegisterCallback<FocusOutEvent>(_ => button.style.borderTopColor =
                button.name == "mode-normal-button" ? Accent : Border);
        }

        private static Label AddStat(VisualElement grid, string title)
        {
            var cell = new VisualElement();
            cell.style.width = Length.Percent(33.333f);
            cell.style.minHeight = 64;
            cell.style.paddingLeft = 14;
            cell.style.paddingRight = 12;
            cell.style.paddingTop = 10;
            cell.style.paddingBottom = 9;
            cell.style.borderRightWidth = 1;
            cell.style.borderBottomWidth = 1;
            cell.style.borderRightColor = Border;
            cell.style.borderBottomColor = Border;
            cell.Add(CreateLabel(title, 11, Dim));
            var value = CreateLabel(string.Empty, 16, Text);
            value.style.unityFontStyleAndWeight = FontStyle.Bold;
            value.style.marginTop = 5;
            cell.Add(value);
            grid.Add(cell);
            return value;
        }

        private static void SetBorder(VisualElement element, Color color, float width, float radius)
        {
            element.style.borderLeftWidth = width;
            element.style.borderRightWidth = width;
            element.style.borderTopWidth = width;
            element.style.borderBottomWidth = width;
            element.style.borderLeftColor = color;
            element.style.borderRightColor = color;
            element.style.borderTopColor = color;
            element.style.borderBottomColor = color;
            SetRadius(element, radius);
        }

        private static void SetRadius(VisualElement element, float radius)
        {
            element.style.borderTopLeftRadius = radius;
            element.style.borderTopRightRadius = radius;
            element.style.borderBottomLeftRadius = radius;
            element.style.borderBottomRightRadius = radius;
        }

        private static Color Html(string value)
        {
            return ColorUtility.TryParseHtmlString(value, out var color) ? color : Color.white;
        }

        private static string FormatConfigSource(ConfigLoadSource source)
        {
            return source switch
            {
                ConfigLoadSource.Remote => "远端",
                ConfigLoadSource.Cache => "缓存",
                _ => "内置"
            };
        }

        private List<SkinConfig> GetSkins(string characterId)
        {
            var skins = AppRoot.Instance.Session.Config.Skins.Skins
                .Where(value => value.CharacterId == characterId)
                .ToList();
            if (skins.Count == 0)
                throw new InvalidOperationException($"角色 {characterId} 缺少皮肤配置");
            return skins;
        }

        private static Sprite LoadPortrait(string modelAsset)
        {
            var resourceName = Path.GetFileNameWithoutExtension(modelAsset);
            return Resources.Load<Sprite>($"Models/Characters/{resourceName}");
        }

        private static void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
