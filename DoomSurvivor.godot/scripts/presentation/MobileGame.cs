using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DoomSurvivor.Application;
using DoomSurvivor.Core;
using DoomSurvivor.Gameplay;
using DoomSurvivor.Infrastructure;
using Godot;

namespace DoomSurvivor.Presentation;

public enum PresentationEntry
{
    Combined,
    MainMenu,
    Battle
}

public partial class MobileGame : Node2D
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

    private const float FixedStep = 1f / 60f;
    private GameCompositionRoot composition = null!;
    private IResourceProvider resources = null!;
    private BattleSimulator? battle;
    private BattleView? battleView;
    private MobileJoystick? joystick;
    private Control? menuLayer;
    private Control? battleLayer;
    private Label? menuSelectionLabel;
    private Label? menuStatusLabel;
    private Label? stageSelectionLabel;
    private Label? hudLabel;
    private Label? weaponLabel;
    private Label? bossLabel;
    private Label? resultLabel;
    private ProgressBar? hpBar;
    private ProgressBar? experienceBar;
    private Button? pauseButton;
    private Button? restartButton;
    private Button? skinButton;
    private Button? stageButton;
    private Button? normalRunButton;
    private Button? quickRunButton;
    private PanelContainer? runSetupCard;
    private Control? homeLayer;
    private Control? characterSelectionLayer;
    private Label? homeProfileLabel;
    private Label? homeRecordLabel;
    private TextureRect? homeCharacterVisual;
    private TextureRect? homePortrait;
    private PanelContainer? settingsPanel;
    private ColorRect? settingsScrim;
    private Control? settingsBackArt;
    private PanelContainer? levelUpPanel;
    private VBoxContainer? levelUpChoices;
    private ColorRect? pauseOverlay;
    private ProceduralAudioService? audio;
    private VBoxContainer? settingsContent;
    private readonly Dictionary<SettingsTab, Button> settingsTabButtons = new();
    private readonly List<TextureRect> characterGalleryFrames = new();
    private readonly List<TextureRect> characterGalleryVisuals = new();
    private readonly List<Label> characterGalleryNames = new();
    private readonly List<Label> characterGalleryRoles = new();
    private TextureRect? characterDetailVisual;
    private Label? characterDetailTitle;
    private Label? characterDetailLabel;
    private Label? characterDetailStageLabel;
    private Texture2D? buttonArt;
    private Texture2D? characterUiArt;
    private Texture2D? settingsUiArt;
    private SettingsTab activeSettingsTab = SettingsTab.Audio;
    private PresentationEntry entry = PresentationEntry.Combined;
    private RunLoadout? initialLoadout;
    private Action<RunRequest>? runRequested;
    private Action? menuRequested;
    private int characterIndex;
    private int stageIndex;
    private string selectedSkinId = "lin_xian_wasteland";
    private double accumulator;
    private bool paused;
    private bool resultShown;

    public void Configure(
        GameCompositionRoot root,
        IResourceProvider resourceProvider,
        PresentationEntry presentationEntry = PresentationEntry.Combined,
        Action<RunRequest>? onRunRequested = null,
        Action? onMenuRequested = null)
    {
        composition = root;
        resources = resourceProvider;
        entry = presentationEntry;
        runRequested = onRunRequested;
        menuRequested = onMenuRequested;
    }

    public void ConfigureBattle(
        GameCompositionRoot root,
        IResourceProvider resourceProvider,
        RunLoadout loadout,
        Action? onMenuRequested = null)
    {
        Configure(root, resourceProvider, PresentationEntry.Battle, null, onMenuRequested);
        initialLoadout = loadout;
    }

    public override void _Ready() => _ = InitializeAsync();

    public override void _Process(double delta)
    {
        if (battle is null) return;
        if (battle.IsFinished)
        {
            if (!resultShown) ShowResult();
            return;
        }
        if (battle.NeedsUpgradeChoice)
        {
            composition.StateMachine.Set(GameState.LevelUp);
            ShowLevelUpPanel();
            battleView?.QueueRedraw();
            RefreshHud();
            return;
        }
        if (paused) return;

        accumulator = Math.Min(0.25, accumulator + delta);
        var steps = 0;
        while (accumulator >= FixedStep && steps < 5)
        {
            var input = ReadInput();
            battle.Tick(FixedStep, new BattleInput(input.X, input.Y));
            accumulator -= FixedStep;
            steps++;
        }

        battleView?.QueueRedraw();
        RefreshHud();
        if (battle.NeedsUpgradeChoice)
        {
            composition.StateMachine.Set(GameState.LevelUp);
            ShowLevelUpPanel();
        }
        else if (battle.IsFinished)
        {
            ShowResult();
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.Escape)
        {
            TogglePause();
            GetViewport().SetInputAsHandled();
        }
        else if (@event is InputEventKey debugKey && debugKey.Pressed && !debugKey.Echo && debugKey.Keycode == Key.F2)
        {
            composition.Session.Settings.ShowPerformanceMonitor = !composition.Session.Settings.ShowPerformanceMonitor;
            PersistSettings();
            RefreshHud();
            GetViewport().SetInputAsHandled();
        }
    }

    private async Task InitializeAsync()
    {
        try
        {
            await composition.InitializeAsync();
            if (entry == PresentationEntry.Battle)
            {
                if (initialLoadout is null) throw new InvalidOperationException("Battle scene was created without a run loadout.");
                StartBattle(initialLoadout);
            }
            else
            {
                BuildMenu();
            }
        }
        catch (Exception exception)
        {
            BuildError(exception);
        }
    }

    private void BuildMenu()
    {
        menuLayer = new Control { Name = "MobileMainMenu" };
        menuLayer.Position = Vector2.Zero;
        AddChild(menuLayer);

        var viewportSize = GetViewportRect().Size;
        if (viewportSize.X < 1f || viewportSize.Y < 1f) viewportSize = new Vector2(1280, 720);
        menuLayer.Size = viewportSize;
        buttonArt = GD.Load<Texture2D>("res://resources/ui/button_art.png");
        characterUiArt = GD.Load<Texture2D>("res://resources/ui/character_ui_art.png");
        settingsUiArt = GD.Load<Texture2D>("res://resources/ui/settings_ui_art.png");

        var background = new TextureRect
        {
            Name = "HomeBackground",
            Texture = GD.Load<Texture2D>("res://resources/ui/home_background.png"),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        background.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        menuLayer.AddChild(background);

        var backgroundTint = new ColorRect
        {
            Name = "HomeBackgroundTint",
            Color = new Color(0.02f, 0.055f, 0.025f, 0.08f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        backgroundTint.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        menuLayer.AddChild(backgroundTint);

        var card = new PanelContainer { Name = "MenuCard" };
        card.Position = new Vector2(42, 30);
        card.Size = new Vector2(viewportSize.X - 84f, viewportSize.Y - 60f);
        SurvivorUiTheme.ApplyCard(card);
        runSetupCard = card;
        card.Visible = false;
        menuLayer.AddChild(card);

        var contentMargin = new MarginContainer { Name = "MenuContentMargin" };
        contentMargin.AddThemeConstantOverride("margin_left", 22);
        contentMargin.AddThemeConstantOverride("margin_top", 18);
        contentMargin.AddThemeConstantOverride("margin_right", 22);
        contentMargin.AddThemeConstantOverride("margin_bottom", 16);
        card.AddChild(contentMargin);

        var content = new VBoxContainer { Name = "MenuContent" };
        content.AddThemeConstantOverride("separation", 8);
        contentMargin.AddChild(content);

        var setupHeader = new HBoxContainer { Name = "SetupHeader" };
        setupHeader.CustomMinimumSize = new Vector2(0, 48);
        var setupTitle = new Label { Text = "战斗准备", VerticalAlignment = VerticalAlignment.Center };
        setupTitle.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        SurvivorUiTheme.ApplyHeading(setupTitle, 30);
        setupHeader.AddChild(setupTitle);
        var setupBackButton = new Button { Text = "返回首页" };
        setupBackButton.CustomMinimumSize = new Vector2(160, 48);
        SurvivorUiTheme.ApplyButton(setupBackButton, SurvivorButtonTone.Wood, true);
        setupBackButton.Pressed += ShowHome;
        setupHeader.AddChild(setupBackButton);
        content.AddChild(setupHeader);

        var title = new Label { Text = "选择角色与关卡", HorizontalAlignment = HorizontalAlignment.Center };
        SurvivorUiTheme.ApplyHeading(title, 40);
        content.AddChild(title);

        var subtitle = new Label
        {
            Text = "幸存者协议\n单机生存 · 自动攻击",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        subtitle.AddThemeFontSizeOverride("font_size", 15);
        SurvivorUiTheme.ApplyBody(subtitle, true);
        content.AddChild(subtitle);

        menuStatusLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        menuStatusLabel.AddThemeFontSizeOverride("font_size", 13);
        SurvivorUiTheme.ApplyBody(menuStatusLabel, true);
        content.AddChild(menuStatusLabel);

        var selectionRow = new HBoxContainer { Name = "SelectionRow" };
        selectionRow.CustomMinimumSize = new Vector2(0, 238);
        selectionRow.AddThemeConstantOverride("separation", 14);
        content.AddChild(selectionRow);

        var survivorPanel = new PanelContainer { Name = "SurvivorPanel" };
        survivorPanel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        SurvivorUiTheme.ApplySection(survivorPanel, SurvivorUiTheme.GreenBright);
        var survivorContent = new VBoxContainer { Name = "SurvivorContent" };
        survivorContent.AddThemeConstantOverride("separation", 7);
        survivorPanel.AddChild(survivorContent);
        selectionRow.AddChild(survivorPanel);

        var survivorHeader = new Label { Text = "角色 / 皮肤", HorizontalAlignment = HorizontalAlignment.Center };
        SurvivorUiTheme.ApplyHeading(survivorHeader, 22);
        survivorContent.AddChild(survivorHeader);

        menuSelectionLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        menuSelectionLabel.CustomMinimumSize = new Vector2(0, 118);
        menuSelectionLabel.AddThemeFontSizeOverride("font_size", 20);
        SurvivorUiTheme.ApplyBody(menuSelectionLabel);
        survivorContent.AddChild(menuSelectionLabel);

        var characterNavigation = new HBoxContainer { Name = "CharacterNavigation" };
        characterNavigation.AddThemeConstantOverride("separation", 7);
        var previousCharacterButton = new Button { Text = "< 上一名" };
        previousCharacterButton.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        previousCharacterButton.CustomMinimumSize = new Vector2(0, 48);
        SurvivorUiTheme.ApplyButton(previousCharacterButton, SurvivorButtonTone.Wood, true);
        previousCharacterButton.Pressed += () => ChangeCharacter(-1);
        characterNavigation.AddChild(previousCharacterButton);
        var nextCharacterButton = new Button { Text = "下一名 >" };
        nextCharacterButton.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        nextCharacterButton.CustomMinimumSize = new Vector2(0, 48);
        SurvivorUiTheme.ApplyButton(nextCharacterButton, SurvivorButtonTone.Wood, true);
        nextCharacterButton.Pressed += () => ChangeCharacter(1);
        characterNavigation.AddChild(nextCharacterButton);
        survivorContent.AddChild(characterNavigation);

        skinButton = new Button { Text = "更换皮肤" };
        skinButton.CustomMinimumSize = new Vector2(0, 48);
        SurvivorUiTheme.ApplyButton(skinButton, SurvivorButtonTone.Blue, true);
        skinButton.Pressed += SelectNextSkin;
        survivorContent.AddChild(skinButton);

        var stagePanel = new PanelContainer { Name = "StagePanel" };
        stagePanel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        SurvivorUiTheme.ApplySection(stagePanel, SurvivorUiTheme.BlueBright);
        var stageContent = new VBoxContainer { Name = "StageContent" };
        stageContent.AddThemeConstantOverride("separation", 7);
        stagePanel.AddChild(stageContent);
        selectionRow.AddChild(stagePanel);

        var stageHeader = new Label { Text = "关卡 / 战斗模式", HorizontalAlignment = HorizontalAlignment.Center };
        SurvivorUiTheme.ApplyHeading(stageHeader, 22);
        stageContent.AddChild(stageHeader);

        stageSelectionLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        stageSelectionLabel.CustomMinimumSize = new Vector2(0, 118);
        stageSelectionLabel.AddThemeFontSizeOverride("font_size", 20);
        SurvivorUiTheme.ApplyBody(stageSelectionLabel);
        stageContent.AddChild(stageSelectionLabel);

        stageButton = new Button { Text = "更换关卡" };
        stageButton.CustomMinimumSize = new Vector2(0, 48);
        SurvivorUiTheme.ApplyButton(stageButton, SurvivorButtonTone.Wood, true);
        stageButton.Pressed += SelectNextStage;
        stageContent.AddChild(stageButton);

        var modeRow = new HBoxContainer { Name = "ModeRow" };
        modeRow.AddThemeConstantOverride("separation", 10);
        normalRunButton = new Button { Text = "开始普通战斗" };
        normalRunButton.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        normalRunButton.CustomMinimumSize = new Vector2(0, 58);
        SurvivorUiTheme.ApplyButton(normalRunButton, SurvivorButtonTone.Green);
        normalRunButton.Pressed += StartNormalRun;
        modeRow.AddChild(normalRunButton);
        quickRunButton = new Button { Text = "开始快速战斗" };
        quickRunButton.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        quickRunButton.CustomMinimumSize = new Vector2(0, 58);
        SurvivorUiTheme.ApplyButton(quickRunButton, SurvivorButtonTone.Orange);
        quickRunButton.Pressed += StartQuickRun;
        modeRow.AddChild(quickRunButton);
        content.AddChild(modeRow);

        var settingsButton = new Button { Text = "系统设置" };
        settingsButton.CustomMinimumSize = new Vector2(0, 50);
        SurvivorUiTheme.ApplyButton(settingsButton, SurvivorButtonTone.Wood);
        settingsButton.Pressed += ToggleSettings;
        content.AddChild(settingsButton);

        var note = new Label
        {
            Text = "左侧摇杆移动 · 自动攻击已开启 · 桌面端支持 WASD / 方向键",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        SurvivorUiTheme.ApplyBody(note, true);
        note.AddThemeFontSizeOverride("font_size", 13);
        content.AddChild(note);

        InitializeMenuSelection();
        BuildSettingsPanel();
        BuildHomeLayer(viewportSize);
        BuildCharacterSelectionLayer(viewportSize);
        ApplyDisplaySettings();
        RefreshMenuSelection();
    }

    private void BuildHomeLayer(Vector2 viewportSize)
    {
        homeLayer = new Control { Name = "HomeLayer" };
        homeLayer.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        menuLayer!.AddChild(homeLayer);

        var character = CurrentCharacter();
        var skin = character is null
            ? null
            : composition.Skins.ForCharacter(character.Id).FirstOrDefault(value => value.Id == selectedSkinId);
        homeCharacterVisual = new TextureRect
        {
            Name = "HomeCharacterVisual",
            Texture = skin is null ? null : resources.LoadCharacterModel(skin.ModelAsset),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            Position = new Vector2(42, viewportSize.Y - 378f),
            Size = new Vector2(350, 360),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        homeLayer.AddChild(homeCharacterVisual);

        var profileCard = new PanelContainer
        {
            Name = "HomeProfileCard",
            Position = new Vector2(28, 24),
            Size = new Vector2(320, 90)
        };
        SurvivorUiTheme.ApplyWoodPanel(profileCard);
        homeLayer.AddChild(profileCard);
        var profileMargin = new MarginContainer { Name = "HomeProfileMargin" };
        profileMargin.AddThemeConstantOverride("margin_left", 10);
        profileMargin.AddThemeConstantOverride("margin_top", 8);
        profileMargin.AddThemeConstantOverride("margin_right", 10);
        profileMargin.AddThemeConstantOverride("margin_bottom", 8);
        profileCard.AddChild(profileMargin);
        var profileRow = new HBoxContainer { Name = "HomeProfileRow" };
        profileRow.AddThemeConstantOverride("separation", 10);
        profileMargin.AddChild(profileRow);
        homePortrait = new TextureRect
        {
            Name = "HomePortrait",
            Texture = homeCharacterVisual.Texture,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            CustomMinimumSize = new Vector2(68, 68),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        profileRow.AddChild(homePortrait);
        homeProfileLabel = new Label
        {
            Name = "HomeProfileLabel",
            Text = "幸存者档案",
            VerticalAlignment = VerticalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        homeProfileLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        homeProfileLabel.AddThemeFontSizeOverride("font_size", 17);
        SurvivorUiTheme.ApplyBody(homeProfileLabel);
        profileRow.AddChild(homeProfileLabel);

        var recordCard = new PanelContainer
        {
            Name = "HomeRecordCard",
            Position = new Vector2(viewportSize.X - 370f, 24),
            Size = new Vector2(342, 90)
        };
        SurvivorUiTheme.ApplyWoodPanel(recordCard);
        homeLayer.AddChild(recordCard);
        var recordMargin = new MarginContainer { Name = "HomeRecordMargin" };
        recordMargin.AddThemeConstantOverride("margin_left", 14);
        recordMargin.AddThemeConstantOverride("margin_top", 10);
        recordMargin.AddThemeConstantOverride("margin_right", 14);
        recordMargin.AddThemeConstantOverride("margin_bottom", 10);
        recordCard.AddChild(recordMargin);
        homeRecordLabel = new Label
        {
            Name = "HomeRecordLabel",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        homeRecordLabel.AddThemeFontSizeOverride("font_size", 15);
        SurvivorUiTheme.ApplyBody(homeRecordLabel);
        recordMargin.AddChild(homeRecordLabel);

        var logoPanel = new PanelContainer
        {
            Name = "HomeLogoPanel",
            Position = new Vector2((viewportSize.X - 470f) * 0.5f, 94),
            Size = new Vector2(470, 158)
        };
        SurvivorUiTheme.ApplyLogoPanel(logoPanel);
        homeLayer.AddChild(logoPanel);
        var logoMargin = new MarginContainer { Name = "HomeLogoMargin" };
        logoMargin.AddThemeConstantOverride("margin_left", 16);
        logoMargin.AddThemeConstantOverride("margin_top", 10);
        logoMargin.AddThemeConstantOverride("margin_right", 16);
        logoMargin.AddThemeConstantOverride("margin_bottom", 10);
        logoPanel.AddChild(logoMargin);
        var logo = new VBoxContainer { Name = "HomeLogo" };
        logo.AddThemeConstantOverride("separation", 0);
        logoMargin.AddChild(logo);
        var logoTop = new Label { Text = "末日", HorizontalAlignment = HorizontalAlignment.Center };
        SurvivorUiTheme.ApplyHeading(logoTop, 64);
        logoTop.AddThemeColorOverride("font_color", SurvivorUiTheme.GreenBright);
        logo.AddChild(logoTop);
        var logoBottom = new Label { Text = "求生物语", HorizontalAlignment = HorizontalAlignment.Center };
        SurvivorUiTheme.ApplyHeading(logoBottom, 38);
        logoBottom.AddThemeColorOverride("font_color", SurvivorUiTheme.GoldBright);
        logo.AddChild(logoBottom);

        var buttonColumn = new VBoxContainer
        {
            Name = "HomeButtonColumn",
            Position = new Vector2((viewportSize.X - 360f) * 0.5f, 300),
            Size = new Vector2(360, 270)
        };
        buttonColumn.AddThemeConstantOverride("separation", 4);
        homeLayer.AddChild(buttonColumn);

        if (buttonArt is not null)
        {
            buttonColumn.AddChild(CreateArtButton(buttonArt, new Rect2(24, 12, 500, 170), new Vector2(360, 84), "开始游戏", ShowRunSetup));
            buttonColumn.AddChild(CreateArtButton(buttonArt, new Rect2(30, 180, 500, 165), new Vector2(360, 84), "选择人物", ShowRunSetup));
            buttonColumn.AddChild(CreateArtButton(buttonArt, new Rect2(30, 338, 500, 165), new Vector2(360, 84), "设置", ToggleSettings));
        }
        else
        {
            var startButton = new Button { Text = "开始游戏" };
            startButton.CustomMinimumSize = new Vector2(0, 66);
            SurvivorUiTheme.ApplyButton(startButton, SurvivorButtonTone.Green);
            startButton.Pressed += ShowRunSetup;
            buttonColumn.AddChild(startButton);

            var characterButton = new Button { Text = "选择人物" };
            characterButton.CustomMinimumSize = new Vector2(0, 66);
            SurvivorUiTheme.ApplyButton(characterButton, SurvivorButtonTone.Blue);
            characterButton.Pressed += ShowRunSetup;
            buttonColumn.AddChild(characterButton);

            var homeSettingsButton = new Button { Text = "设置" };
            homeSettingsButton.CustomMinimumSize = new Vector2(0, 66);
            SurvivorUiTheme.ApplyButton(homeSettingsButton, SurvivorButtonTone.Orange);
            homeSettingsButton.Pressed += ToggleSettings;
            buttonColumn.AddChild(homeSettingsButton);
        }

        var footer = new Label
        {
            Text = "选择角色与关卡，开始你的生存挑战",
            Position = new Vector2(0, viewportSize.Y - 54f),
            Size = new Vector2(viewportSize.X, 28),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        footer.AddThemeFontSizeOverride("font_size", 14);
        SurvivorUiTheme.ApplyBody(footer, true);
        homeLayer.AddChild(footer);
    }

    private void BuildCharacterSelectionLayer(Vector2 viewportSize)
    {
        characterSelectionLayer = new Control
        {
            Name = "CharacterSelectionLayer",
            Size = viewportSize,
            Visible = false,
            ZIndex = 5,
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        menuLayer!.AddChild(characterSelectionLayer);

        if (characterUiArt is not null)
        {
            var back = CreateArtButton(characterUiArt, new Rect2(18, 12, 285, 150), new Vector2(190, 72), "返回首页", ShowHome);
            back.Position = new Vector2(24, 18);
            characterSelectionLayer.AddChild(back);
        }
        else
        {
            var back = new Button { Text = "返回首页", Position = new Vector2(24, 18), Size = new Vector2(190, 56) };
            SurvivorUiTheme.ApplyButton(back, SurvivorButtonTone.Wood);
            back.Pressed += ShowHome;
            characterSelectionLayer.AddChild(back);
        }

        var title = new Label
        {
            Text = "选择人物",
            Position = new Vector2(300, 24),
            Size = new Vector2(520, 64),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        SurvivorUiTheme.ApplyHeading(title, 42);
        characterSelectionLayer.AddChild(title);

        var profile = new Label
        {
            Text = "幸存者档案",
            Position = new Vector2(viewportSize.X - 280, 28),
            Size = new Vector2(240, 42),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        SurvivorUiTheme.ApplyHeading(profile, 18);
        characterSelectionLayer.AddChild(profile);

        characterGalleryFrames.Clear();
        characterGalleryVisuals.Clear();
        characterGalleryNames.Clear();
        characterGalleryRoles.Clear();
        for (var i = 0; i < 4; i++)
        {
            var slot = new Control
            {
                Name = $"CharacterSlot{i + 1}",
                Position = new Vector2(26 + i * 186, 112),
                Size = new Vector2(174, 360),
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            characterSelectionLayer.AddChild(slot);

            var frame = characterUiArt is null
                ? new TextureRect { Size = slot.Size }
                : CreateArtTexture(characterUiArt, i == 0 ? new Rect2(254, 180, 240, 380) : new Rect2(20, 178, 230, 380), slot.Size);
            frame.MouseFilter = Control.MouseFilterEnum.Ignore;
            slot.AddChild(frame);
            characterGalleryFrames.Add(frame);

            var visual = new TextureRect
            {
                Name = $"CharacterSlotVisual{i + 1}",
                Position = new Vector2(18, 18),
                Size = new Vector2(138, 254),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            slot.AddChild(visual);
            characterGalleryVisuals.Add(visual);

            var name = new Label
            {
                Position = new Vector2(10, 278),
                Size = new Vector2(154, 30),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            SurvivorUiTheme.ApplyInk(name);
            name.AddThemeFontSizeOverride("font_size", 21);
            slot.AddChild(name);
            characterGalleryNames.Add(name);

            var role = new Label
            {
                Position = new Vector2(10, 312),
                Size = new Vector2(154, 25),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            SurvivorUiTheme.ApplyInk(role, true);
            role.AddThemeFontSizeOverride("font_size", 14);
            slot.AddChild(role);
            characterGalleryRoles.Add(role);
        }

        var previous = new Button { Text = "‹ 上一名", Position = new Vector2(210, 482), Size = new Vector2(170, 48) };
        SurvivorUiTheme.ApplyParchmentButton(previous);
        previous.Pressed += () => ChangeCharacter(-1);
        characterSelectionLayer.AddChild(previous);
        var next = new Button { Text = "下一名 ›", Position = new Vector2(416, 482), Size = new Vector2(170, 48) };
        SurvivorUiTheme.ApplyParchmentButton(next);
        next.Pressed += () => ChangeCharacter(1);
        characterSelectionLayer.AddChild(next);

        var detail = new Control
        {
            Name = "CharacterDetailPanel",
            Position = new Vector2(viewportSize.X - 432, 108),
            Size = new Vector2(404, 438),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        characterSelectionLayer.AddChild(detail);
        if (characterUiArt is not null)
        {
            detail.AddChild(CreateArtTexture(characterUiArt, new Rect2(944, 98, 575, 505), detail.Size));
        }
        else
        {
            var fallback = new PanelContainer { Size = detail.Size };
            SurvivorUiTheme.ApplyParchment(fallback);
            detail.AddChild(fallback);
        }

        characterDetailVisual = new TextureRect
        {
            Name = "CharacterDetailVisual",
            Position = new Vector2(26, 90),
            Size = new Vector2(158, 250),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        detail.AddChild(characterDetailVisual);

        characterDetailTitle = new Label
        {
            Position = new Vector2(188, 48),
            Size = new Vector2(190, 48),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        SurvivorUiTheme.ApplyInk(characterDetailTitle);
        characterDetailTitle.AddThemeFontSizeOverride("font_size", 25);
        detail.AddChild(characterDetailTitle);

        characterDetailLabel = new Label
        {
            Position = new Vector2(190, 106),
            Size = new Vector2(184, 150),
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        SurvivorUiTheme.ApplyInk(characterDetailLabel);
        characterDetailLabel.AddThemeFontSizeOverride("font_size", 16);
        detail.AddChild(characterDetailLabel);
        menuSelectionLabel = characterDetailLabel;

        characterDetailStageLabel = new Label
        {
            Position = new Vector2(28, 350),
            Size = new Vector2(348, 50),
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        SurvivorUiTheme.ApplyInk(characterDetailStageLabel, true);
        characterDetailStageLabel.AddThemeFontSizeOverride("font_size", 14);
        detail.AddChild(characterDetailStageLabel);
        stageSelectionLabel = characterDetailStageLabel;

        skinButton = new Button { Text = "更换皮肤", Position = new Vector2(26, 548), Size = new Vector2(360, 48) };
        SurvivorUiTheme.ApplyParchmentButton(skinButton);
        skinButton.Pressed += SelectNextSkin;
        characterSelectionLayer.AddChild(skinButton);

        stageButton = new Button { Text = "更换关卡", Position = new Vector2(26, 604), Size = new Vector2(360, 48) };
        SurvivorUiTheme.ApplyParchmentButton(stageButton);
        stageButton.Pressed += SelectNextStage;
        characterSelectionLayer.AddChild(stageButton);

        menuStatusLabel = new Label
        {
            Position = new Vector2(26, 665),
            Size = new Vector2(760, 28),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        SurvivorUiTheme.ApplyInk(menuStatusLabel, true);
        menuStatusLabel.AddThemeFontSizeOverride("font_size", 13);
        characterSelectionLayer.AddChild(menuStatusLabel);

        if (characterUiArt is not null)
        {
            var startAdventure = CreateArtButton(characterUiArt, new Rect2(944, 562, 395, 165), new Vector2(300, 96), "开始普通战斗", StartNormalRun, out var artRunButton);
            startAdventure.Position = new Vector2(viewportSize.X - 360, 548);
            characterSelectionLayer.AddChild(startAdventure);
            normalRunButton = artRunButton;
        }
        else
        {
            normalRunButton = new Button { Text = "开始普通战斗", Position = new Vector2(viewportSize.X - 360, 560), Size = new Vector2(300, 64) };
            SurvivorUiTheme.ApplyButton(normalRunButton, SurvivorButtonTone.Orange);
            normalRunButton.Pressed += StartNormalRun;
            characterSelectionLayer.AddChild(normalRunButton);
        }

        quickRunButton = new Button { Text = "快速战斗 · 180 秒", Position = new Vector2(viewportSize.X - 360, 654), Size = new Vector2(300, 44) };
        SurvivorUiTheme.ApplyButton(quickRunButton, SurvivorButtonTone.Blue, true);
        quickRunButton.Pressed += StartQuickRun;
        characterSelectionLayer.AddChild(quickRunButton);
    }

    private void BuildSettingsPanel()
    {
        var viewportSize = GetViewportRect().Size;
        if (viewportSize.X < 1f || viewportSize.Y < 1f) viewportSize = new Vector2(1280, 720);

        settingsScrim = new ColorRect
        {
            Name = "SettingsScrim",
            Color = new Color(0.02f, 0.05f, 0.02f, 0.18f),
            ZIndex = 9,
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        settingsScrim.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        menuLayer!.AddChild(settingsScrim);

        settingsPanel = new PanelContainer
        {
            Name = "SettingsPanel",
            Position = new Vector2(220, 42),
            Size = new Vector2(Mathf.Min(850f, viewportSize.X - 250f), viewportSize.Y - 82f),
            ZIndex = 10,
            Visible = false
        };
        SurvivorUiTheme.ApplyLogoPanel(settingsPanel);
        menuLayer!.AddChild(settingsPanel);

        var settingsRoot = new Control { Name = "SettingsRoot", Size = settingsPanel.Size };
        settingsPanel.AddChild(settingsRoot);
        if (settingsUiArt is not null)
        {
            settingsRoot.AddChild(CreateArtTexture(settingsUiArt, new Rect2(4, 158, 655, 548), settingsRoot.Size));
        }

        var title = new Label
        {
            Text = "游戏设置",
            Position = new Vector2(252, 38),
            Size = new Vector2(330, 54),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        SurvivorUiTheme.ApplyInk(title);
        title.AddThemeFontSizeOverride("font_size", 32);
        settingsRoot.AddChild(title);

        var tabs = new VBoxContainer
        {
            Name = "SettingsTabs",
            Position = new Vector2(60, 130),
            Size = new Vector2(280, 430)
        };
        tabs.AddThemeConstantOverride("separation", 5);
        settingsRoot.AddChild(tabs);
        settingsTabButtons.Clear();
        foreach (SettingsTab tab in Enum.GetValues<SettingsTab>())
        {
            var captured = tab;
            var button = new Button { Text = SettingsTabLabel(tab) };
            button.CustomMinimumSize = new Vector2(280, 43);
            SurvivorUiTheme.ApplyParchmentButton(button);
            button.Pressed += () => ShowSettingsTab(captured);
            tabs.AddChild(button);
            settingsTabButtons[tab] = button;
        }

        var settingsContentPanel = new PanelContainer
        {
            Name = "SettingsContentPanel",
            Position = new Vector2(382, 124),
            Size = new Vector2(390, 414)
        };
        SurvivorUiTheme.ApplyParchment(settingsContentPanel);
        settingsRoot.AddChild(settingsContentPanel);
        var settingsContentMargin = new MarginContainer { Name = "SettingsContentMargin" };
        settingsContentMargin.AddThemeConstantOverride("margin_left", 18);
        settingsContentMargin.AddThemeConstantOverride("margin_top", 16);
        settingsContentMargin.AddThemeConstantOverride("margin_right", 18);
        settingsContentMargin.AddThemeConstantOverride("margin_bottom", 16);
        settingsContentPanel.AddChild(settingsContentMargin);
        settingsContent = new VBoxContainer { Name = "SettingsContent" };
        settingsContent.AddThemeConstantOverride("separation", 5);
        settingsContentMargin.AddChild(settingsContent);

        if (settingsUiArt is not null)
        {
            var restore = CreateArtButton(settingsUiArt, new Rect2(452, 680, 300, 150), new Vector2(175, 66), "恢复默认", RestoreDefaultSettings);
            restore.Position = new Vector2(90, 550);
            settingsRoot.AddChild(restore);
            var save = CreateArtButton(settingsUiArt, new Rect2(742, 680, 330, 150), new Vector2(190, 66), "保存设置", PersistSettings);
            save.Position = new Vector2(300, 550);
            settingsRoot.AddChild(save);
            var returnHome = CreateArtButton(settingsUiArt, new Rect2(1050, 680, 290, 150), new Vector2(175, 66), "返回首页", ToggleSettings);
            returnHome.Position = new Vector2(525, 550);
            settingsRoot.AddChild(returnHome);
        }

        settingsBackArt = settingsUiArt is null ? null : CreateArtButton(settingsUiArt, new Rect2(8, 10, 330, 155), new Vector2(190, 72), "返回首页", ToggleSettings);
        if (settingsBackArt is not null)
        {
            settingsBackArt.Position = new Vector2(24, 18);
            settingsBackArt.ZIndex = 11;
            settingsBackArt.Visible = false;
            menuLayer.AddChild(settingsBackArt);
        }

        ShowSettingsTab(SettingsTab.Audio);
    }

    private void InitializeMenuSelection()
    {
        var characters = composition.Characters.All.ToList();
        var selectedIndex = characters.FindIndex(value => string.Equals(value.Id, composition.Session.Profile.SelectedCharacterId, StringComparison.Ordinal));
        characterIndex = selectedIndex >= 0 ? selectedIndex : 0;
        var character = characters.Count > 0 ? characters[characterIndex] : null;
        if (character is not null && composition.Session.Profile.SelectedSkinByCharacter.TryGetValue(character.Id, out var skinId))
        {
            selectedSkinId = skinId;
        }

        var selectedStage = composition.Session.Launch.StageId;
        var stageIndexFromSave = composition.Stages.All.ToList().FindIndex(value => string.Equals(value.Id, selectedStage, StringComparison.Ordinal));
        stageIndex = stageIndexFromSave >= 0 ? stageIndexFromSave : 0;
    }

    private void ChangeCharacter(int direction)
    {
        var characters = composition.Characters.All.ToList();
        if (characters.Count == 0) return;
        characterIndex = (characterIndex + direction + characters.Count) % characters.Count;
        var character = characters[characterIndex];
        selectedSkinId = composition.Session.Profile.SelectedSkinByCharacter.TryGetValue(character.Id, out var savedSkin)
            ? savedSkin
            : composition.Skins.ForCharacter(character.Id).FirstOrDefault()?.Id ?? character.DefaultSkinId;
        RefreshMenuSelection();
    }

    private void SelectNextCharacter() => ChangeCharacter(1);

    private void SelectNextSkin()
    {
        var character = CurrentCharacter();
        if (character is null) return;
        var skins = composition.Skins.ForCharacter(character.Id).ToList();
        if (skins.Count == 0) return;
        var current = skins.FindIndex(value => string.Equals(value.Id, selectedSkinId, StringComparison.Ordinal));
        selectedSkinId = skins[(current + 1 + skins.Count) % skins.Count].Id;
        RefreshMenuSelection();
    }

    private void SelectNextStage()
    {
        var stages = composition.Stages.All.ToList();
        if (stages.Count == 0) return;
        stageIndex = (stageIndex + 1) % stages.Count;
        RefreshMenuSelection();
    }

    private void RefreshMenuSelection()
    {
        if (menuSelectionLabel is null || stageSelectionLabel is null) return;
        var characters = composition.Characters.All.ToList();
        var stages = composition.Stages.All.ToList();
        if (characters.Count == 0 || stages.Count == 0) return;
        characterIndex = Math.Clamp(characterIndex, 0, characters.Count - 1);
        stageIndex = Math.Clamp(stageIndex, 0, stages.Count - 1);
        var character = characters[characterIndex];
        var skin = composition.Skins.ForCharacter(character.Id).FirstOrDefault(value => value.Id == selectedSkinId) ??
            composition.Skins.ForCharacter(character.Id).FirstOrDefault();
        selectedSkinId = skin?.Id ?? character.DefaultSkinId;
        var characterUnlocked = composition.Unlocks.IsCharacterUnlocked(character.Id);
        var skinUnlocked = skin is not null && composition.Unlocks.IsSkinUnlocked(skin.Id);
        var lockText = characterUnlocked && skinUnlocked ? "可以出战" : "尚未解锁 · 请先完成解锁";
        menuSelectionLabel.Text = $"{character.Name}\n{skin?.Name ?? selectedSkinId}\n生命 {character.MaxHp:0}  ·  移速 {character.MoveSpeed:0}\n{lockText}";

        var stage = stages[stageIndex];
        stageSelectionLabel.Text = $"{stage.Name}\n{stage.Description}\n普通 {stage.NormalModeDuration:0} 秒  ·  快速 {stage.QuickTestDuration:0} 秒";
        if (stageButton is not null) stageButton.Text = $"更换关卡 · {stageIndex + 1}/{stages.Count}";
        if (skinButton is not null) skinButton.Text = $"更换皮肤 · {skin?.Name ?? selectedSkinId}";
        if (menuStatusLabel is not null)
        {
            menuStatusLabel.Text = $"配置：{ConfigSourceLabel(composition.Session.ConfigSource)}  ·  版本：v{composition.Session.Config.Version}  ·  地图：{MapSkinLabel(composition.Session.Settings.MapSkinId)}";
        }
        var profileText = $"{character.Name}\n生命 {character.MaxHp:0}  ·  移速 {character.MoveSpeed:0}";
        if (homeProfileLabel is not null) homeProfileLabel.Text = profileText;
        if (homeRecordLabel is not null)
        {
            homeRecordLabel.Text = $"幸存者档案\n最高击杀：{composition.Session.Profile.MaxKills}  ·  最高等级：{composition.Session.Profile.MaxLevel}";
        }
        var characterTexture = skin is null ? null : resources.LoadCharacterModel(skin.ModelAsset);
        if (homeCharacterVisual is not null) homeCharacterVisual.Texture = characterTexture;
        if (homePortrait is not null) homePortrait.Texture = characterTexture;

        var canStart = characterUnlocked && skinUnlocked;
        if (normalRunButton is not null) normalRunButton.Disabled = !canStart;
        if (quickRunButton is not null) quickRunButton.Disabled = !canStart;
        RefreshSettingsPanel();
        RefreshCharacterGallery();
    }

    private void RefreshCharacterGallery()
    {
        if (characterGalleryVisuals.Count == 0) return;
        var characters = composition.Characters.All.ToList();
        if (characters.Count == 0) return;

        for (var slot = 0; slot < characterGalleryVisuals.Count; slot++)
        {
            var index = (characterIndex + slot) % characters.Count;
            var character = characters[index];
            var skin = composition.Skins.ForCharacter(character.Id).FirstOrDefault();
            var texture = skin is null ? null : resources.LoadCharacterModel(skin.ModelAsset);
            characterGalleryVisuals[slot].Texture = texture;
            characterGalleryNames[slot].Text = character.Name;
            characterGalleryRoles[slot].Text = WeaponLabel(character.StartingWeaponId);
            if (characterUiArt is not null)
            {
                characterGalleryFrames[slot].Texture = CreateAtlasTexture(
                    characterUiArt,
                    slot == 0 ? new Rect2(254, 180, 240, 380) : new Rect2(20, 178, 230, 380));
            }
        }

        var current = CurrentCharacter();
        if (current is null) return;
        var currentSkin = composition.Skins.ForCharacter(current.Id).FirstOrDefault(value => value.Id == selectedSkinId) ??
            composition.Skins.ForCharacter(current.Id).FirstOrDefault();
        var currentTexture = currentSkin is null ? null : resources.LoadCharacterModel(currentSkin.ModelAsset);
        if (characterDetailVisual is not null) characterDetailVisual.Texture = currentTexture;
        if (characterDetailTitle is not null) characterDetailTitle.Text = current.Name;
        if (characterDetailLabel is not null)
        {
            characterDetailLabel.Text = $"职业：{WeaponLabel(current.StartingWeaponId)}\n生命：{current.MaxHp:0}\n移速：{current.MoveSpeed:0}\n\n皮肤：{currentSkin?.Name ?? selectedSkinId}";
        }
        if (characterDetailStageLabel is not null && composition.Stages.All.Any())
        {
            var stage = composition.Stages.All.ToList()[Math.Clamp(stageIndex, 0, composition.Stages.All.Count - 1)];
            characterDetailStageLabel.Text = $"关卡：{stage.Name}\n普通 {stage.NormalModeDuration:0} 秒 · 快速 {stage.QuickTestDuration:0} 秒";
        }
    }

    private CharacterConfig? CurrentCharacter()
    {
        var characters = composition.Characters.All.ToList();
        return characters.Count == 0 ? null : characters[Math.Clamp(characterIndex, 0, characters.Count - 1)];
    }

    private void StartNormalRun() => StartRun(GameMode.SoloSurvivor);

    private void StartQuickRun() => StartRun(GameMode.QuickTest);

    private void StartRun(GameMode mode)
    {
        var character = CurrentCharacter();
        var stages = composition.Stages.All.ToList();
        if (character is null || stages.Count == 0 || !composition.Unlocks.IsCharacterUnlocked(character.Id)) return;
        if (!composition.Skins.TryGet(selectedSkinId, out var skin) || !composition.Unlocks.IsSkinUnlocked(skin.Id)) return;
        var stage = stages[Math.Clamp(stageIndex, 0, stages.Count - 1)];
        var request = new RunRequest
        {
            Mode = mode,
            CharacterId = character.Id,
            SkinId = selectedSkinId,
            StageId = stage.Id,
            MapSkinId = composition.Session.Settings.MapSkinId
        };
        if (runRequested is not null)
        {
            runRequested(request);
            return;
        }

        var loadout = composition.StartRun(request);
        StartBattle(loadout);
    }

    private void StartBattle(RunLoadout loadout)
    {
        battle = new BattleSimulator(loadout);
        resultShown = false;
        paused = false;
        accumulator = 0d;
        composition.StateMachine.Set(GameState.Playing);
        battleView = new BattleView(
            battle,
            resources.LoadCharacterModel(loadout.Skin.ModelAsset),
            resources.LoadMapSkin(loadout.MapSkinId),
            loadout.Skin.Palette,
            loadout.MapSkinId);
        battleView.Position = GetViewportRect().Size * 0.5f;
        AddChild(battleView);
        audio = new ProceduralAudioService
        {
            Volume = composition.Session.Settings.MasterVolume * composition.Session.Settings.SfxVolume
        };
        AddChild(audio);
        BuildBattleHud(loadout);
        if (menuLayer is not null) menuLayer.Visible = false;
        if (settingsPanel is not null) settingsPanel.Visible = false;
        if (settingsBackArt is not null) settingsBackArt.Visible = false;
        if (characterSelectionLayer is not null) characterSelectionLayer.Visible = false;
        if (pauseOverlay is not null) pauseOverlay.Visible = false;
        if (levelUpPanel is not null) levelUpPanel.Visible = false;
    }

    private void ToggleSettings()
    {
        if (settingsPanel is null) return;
        var showSettings = !settingsPanel.Visible;
        settingsPanel.Visible = showSettings;
        if (settingsScrim is not null) settingsScrim.Visible = showSettings;
        if (settingsBackArt is not null) settingsBackArt.Visible = showSettings;
        if (showSettings)
        {
            if (homeLayer is not null) homeLayer.Visible = false;
            if (runSetupCard is not null) runSetupCard.Visible = false;
            if (characterSelectionLayer is not null) characterSelectionLayer.Visible = false;
            ShowSettingsTab(activeSettingsTab);
        }
        else
        {
            ShowHome();
        }
    }

    private void ShowRunSetup()
    {
        if (settingsPanel is not null) settingsPanel.Visible = false;
        if (settingsScrim is not null) settingsScrim.Visible = false;
        if (settingsBackArt is not null) settingsBackArt.Visible = false;
        if (homeLayer is not null) homeLayer.Visible = false;
        if (runSetupCard is not null) runSetupCard.Visible = false;
        if (characterSelectionLayer is not null) characterSelectionLayer.Visible = true;
        RefreshMenuSelection();
    }

    private void ShowHome()
    {
        if (settingsPanel is not null) settingsPanel.Visible = false;
        if (settingsScrim is not null) settingsScrim.Visible = false;
        if (settingsBackArt is not null) settingsBackArt.Visible = false;
        if (runSetupCard is not null) runSetupCard.Visible = false;
        if (characterSelectionLayer is not null) characterSelectionLayer.Visible = false;
        if (homeLayer is not null) homeLayer.Visible = true;
        RefreshMenuSelection();
    }

    private static string SettingsTabLabel(SettingsTab tab) => tab switch
    {
        SettingsTab.Audio => "音频",
        SettingsTab.Display => "显示",
        SettingsTab.Crates => "补给箱",
        SettingsTab.HiddenCrates => "隐藏箱",
        SettingsTab.Altar => "祭坛",
        SettingsTab.Map => "地图",
        SettingsTab.Waves => "波次",
        SettingsTab.Skills => "技能",
        _ => tab.ToString()
    };

    private static string ToggleLabel(bool enabled) => enabled ? "开启" : "关闭";

    private static string ParticleQualityLabel(ParticleQuality quality) => quality switch
    {
        ParticleQuality.Low => "低",
        ParticleQuality.High => "高",
        _ => "中"
    };

    private static string ConfigSourceLabel(ConfigLoadSource source) => source switch
    {
        ConfigLoadSource.Cache => "本地缓存",
        ConfigLoadSource.Remote => "远程配置",
        _ => "内置配置"
    };

    private static string MapSkinLabel(string? id) => id switch
    {
        "grass_tile_02" => "翠绿密林",
        "grass_tile_03" => "花野小径",
        "grass_tile_04" => "青绿山谷",
        "dry_highland_coast" => "干旱海岸",
        _ => "晨曦草坪"
    };

    private static string WeaponLabel(string? id) => id switch
    {
        "wind_blade" => "风刃",
        "rotating_knife" => "飞轮术",
        "fubo_qin" => "伏波琴",
        "fire_bottle" => "火焰瓶",
        "lightning_chain" => "闪电链",
        "drone" => "无人机",
        _ => "未配置"
    };

    private static string WaveStateLabel(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "准备中";
        return value
            .Replace("BOSS WARNING", "首领预警", StringComparison.Ordinal)
            .Replace("BOSS INCOMING", "首领即将到来", StringComparison.Ordinal)
            .Replace("BOSS", "首领", StringComparison.Ordinal)
            .Replace("WARNING", "预警", StringComparison.Ordinal)
            .Replace("INCOMING", "即将到来", StringComparison.Ordinal);
    }

    private void ShowSettingsTab(SettingsTab tab)
    {
        activeSettingsTab = tab;
        if (settingsContent is null) return;
        foreach (var child in settingsContent.GetChildren()) child.QueueFree();
        foreach (var pair in settingsTabButtons)
        {
            SurvivorUiTheme.ApplyParchmentButton(pair.Value, pair.Key == tab);
        }

        AddSettingsTitle(SettingsTabLabel(tab));
        switch (tab)
        {
            case SettingsTab.Audio:
                AddSettingsHint("音量会立即保存，并在下一次音频层播放时生效。点击项目即可循环调整数值。");
                AddSettingsButton($"主音量：{composition.Session.Settings.MasterVolume:0.0}", () => CycleFloatSetting(() => composition.Session.Settings.MasterVolume, value => composition.Session.Settings.MasterVolume = value, 0f, 1f, 0.1f));
                AddSettingsButton($"音效音量：{composition.Session.Settings.SfxVolume:0.0}", () => CycleFloatSetting(() => composition.Session.Settings.SfxVolume, value => composition.Session.Settings.SfxVolume = value, 0f, 1f, 0.1f));
                AddSettingsButton($"音乐音量：{composition.Session.Settings.MusicVolume:0.0}", () => CycleFloatSetting(() => composition.Session.Settings.MusicVolume, value => composition.Session.Settings.MusicVolume = value, 0f, 1f, 0.1f));
                break;

            case SettingsTab.Display:
                AddSettingsHint("显示选项独立于战斗模拟，修改后会保存，并在下一局继续生效。");
                AddSettingsButton($"全屏显示：{ToggleLabel(composition.Session.Settings.Fullscreen)}", ToggleFullscreen);
                AddSettingsButton($"特效质量：{ParticleQualityLabel(composition.Session.Settings.ParticleQuality)}", CycleParticleQuality);
                AddSettingsButton($"敌人显示上限：{composition.Session.Settings.MaxEnemyDisplay}", () => CycleIntSetting(() => composition.Session.Settings.MaxEnemyDisplay, value => composition.Session.Settings.MaxEnemyDisplay = value, 50, 1000, 50));
                AddSettingsButton($"性能监视：{ToggleLabel(composition.Session.Settings.ShowPerformanceMonitor)}", () => ToggleBoolSetting(() => composition.Session.Settings.ShowPerformanceMonitor, value => composition.Session.Settings.ShowPerformanceMonitor = value));
                AddSettingsButton($"屏幕震动：{ToggleLabel(composition.Session.Settings.ScreenShake)}", ToggleScreenShake);
                AddSettingsButton($"伤害数字：{ToggleLabel(composition.Session.Settings.DamageNumbers)}", ToggleDamageNumbers);
                break;

            case SettingsTab.Crates:
                AddSettingsHint("设置开局可见的补给箱数量，以及补给箱刷新概率。");
                AddSettingsButton($"补给箱数量：{composition.Session.Settings.CrateCount}", () => CycleIntSetting(() => composition.Session.Settings.CrateCount, value => composition.Session.Settings.CrateCount = value, 0, 30, 1));
                AddSettingsButton($"补给箱刷新：{composition.Session.Settings.CrateRefreshChance}%", () => CycleIntSetting(() => composition.Session.Settings.CrateRefreshChance, value => composition.Session.Settings.CrateRefreshChance = value, 0, 100, 10));
                break;

            case SettingsTab.HiddenCrates:
                AddSettingsHint("隐藏箱会在战斗中按靠近触发，并从隐藏箱效果表抽取奖励。");
                AddSettingsButton($"隐藏箱数量：{composition.Session.Settings.HiddenCrateCount}", () => CycleIntSetting(() => composition.Session.Settings.HiddenCrateCount, value => composition.Session.Settings.HiddenCrateCount = value, 0, 20, 1));
                AddSettingsButton($"隐藏箱刷新：{composition.Session.Settings.HiddenCrateRefreshChance}%", () => CycleIntSetting(() => composition.Session.Settings.HiddenCrateRefreshChance, value => composition.Session.Settings.HiddenCrateRefreshChance = value, 0, 100, 10));
                break;

            case SettingsTab.Altar:
                AddSettingsHint("祭坛会在战斗中按靠近触发，消耗生命并提供临时强化。");
                AddSettingsButton($"祭坛数量：{composition.Session.Settings.AltarCount}", () => CycleIntSetting(() => composition.Session.Settings.AltarCount, value => composition.Session.Settings.AltarCount = value, 0, 20, 1));
                AddSettingsButton($"祭坛刷新：{composition.Session.Settings.AltarRefreshChance}%", () => CycleIntSetting(() => composition.Session.Settings.AltarRefreshChance, value => composition.Session.Settings.AltarRefreshChance = value, 0, 100, 10));
                break;

            case SettingsTab.Map:
                AddSettingsHint("地图皮肤会在下一局开始时生效；地图事件数量会保存到本地设置。");
                AddSettingsButton($"地图皮肤：{MapSkinLabel(composition.Session.Settings.MapSkinId)}", CycleMapSkin);
                AddSettingsButton($"毒雾数量：{composition.Session.Settings.PoisonFogCount}", () => CycleIntSetting(() => composition.Session.Settings.PoisonFogCount, value => composition.Session.Settings.PoisonFogCount = value, 0, 20, 1));
                AddSettingsButton($"治疗鸡数量：{composition.Session.Settings.HealingChickenCount}", () => CycleIntSetting(() => composition.Session.Settings.HealingChickenCount, value => composition.Session.Settings.HealingChickenCount = value, 0, 20, 1));
                break;

            case SettingsTab.Waves:
                AddSettingsHint("波次由固定时间线与 2.2 秒清场间隔驱动，最后一波可召唤 Boss。");
                AddSettingsButton($"波次数量：{composition.Session.Settings.WaveCount}", () => CycleIntSetting(() => composition.Session.Settings.WaveCount, value => composition.Session.Settings.WaveCount = value, 1, 30, 1));
                AddSettingsButton($"首领数量：{composition.Session.Settings.BossCount}", () => CycleIntSetting(() => composition.Session.Settings.BossCount, value => composition.Session.Settings.BossCount = value, 0, 10, 1));
                AddSettingsButton($"首波敌人数量：{composition.Session.Settings.FirstWaveMobCount}", () => CycleIntSetting(() => composition.Session.Settings.FirstWaveMobCount, value => composition.Session.Settings.FirstWaveMobCount = value, 1, 100, 1));
                AddSettingsButton($"波次敌人倍率：{composition.Session.Settings.WaveMobCountMultiplier:0.0}", () => CycleFloatSetting(() => composition.Session.Settings.WaveMobCountMultiplier, value => composition.Session.Settings.WaveMobCountMultiplier = value, 0.1f, 5f, 0.1f));
                AddSettingsButton($"精英出现波次：{composition.Session.Settings.EliteStartWave}", () => CycleIntSetting(() => composition.Session.Settings.EliteStartWave, value => composition.Session.Settings.EliteStartWave = value, 1, 30, 1));
                AddSettingsButton($"队长出现波次：{composition.Session.Settings.LeaderStartWave}", () => CycleIntSetting(() => composition.Session.Settings.LeaderStartWave, value => composition.Session.Settings.LeaderStartWave = value, 1, 30, 1));
                break;

            case SettingsTab.Skills:
                AddSettingsHint("武器与被动从内容目录读取；升级时暂停并提供三选一。");
                AddSettingsButton($"武器目录：{composition.Session.Config.Weapons.Weapons.Count} 项", () => { });
                AddSettingsButton($"被动目录：{composition.Session.Config.Skills.Skills.Count} 项", () => { });
                AddSettingsButton($"初始武器：{WeaponLabel(CurrentCharacter()?.StartingWeaponId)}", () => { });
                break;
        }
    }

    private void AddSettingsTitle(string text)
    {
        var title = new Label { Text = text, HorizontalAlignment = HorizontalAlignment.Left };
        SurvivorUiTheme.ApplyInk(title);
        title.AddThemeFontSizeOverride("font_size", 25);
        settingsContent!.AddChild(title);
    }

    private void AddSettingsHint(string text)
    {
        var hint = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        hint.CustomMinimumSize = new Vector2(0, 36);
        SurvivorUiTheme.ApplyInk(hint, true);
        hint.AddThemeFontSizeOverride("font_size", 14);
        settingsContent!.AddChild(hint);
    }

    private void AddSettingsButton(string text, Action action)
    {
        var button = new Button { Text = text };
        button.CustomMinimumSize = new Vector2(0, 44);
        button.Alignment = HorizontalAlignment.Left;
        SurvivorUiTheme.ApplyParchmentButton(button);
        button.Pressed += action;
        settingsContent!.AddChild(button);
    }

    private void RestoreDefaultSettings()
    {
        composition.Session.Settings = new GameSettings();
        composition.Session.Settings.Clamp();
        ApplyDisplaySettings();
        PersistSettings();
        ShowSettingsTab(activeSettingsTab);
        RefreshMenuSelection();
    }

    private static AtlasTexture CreateAtlasTexture(Texture2D sheet, Rect2 region)
    {
        return new AtlasTexture
        {
            Atlas = sheet,
            Region = region
        };
    }

    private static TextureRect CreateArtTexture(Texture2D sheet, Rect2 region, Vector2 size)
    {
        return new TextureRect
        {
            Texture = CreateAtlasTexture(sheet, region),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            Size = size,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
    }

    private Control CreateArtButton(Texture2D sheet, Rect2 region, Vector2 size, string tooltip, Action pressed)
    {
        return CreateArtButton(sheet, region, size, tooltip, pressed, out _);
    }

    private Control CreateArtButton(Texture2D sheet, Rect2 region, Vector2 size, string tooltip, Action pressed, out Button button)
    {
        var wrapper = new Control
        {
            CustomMinimumSize = size,
            Size = size,
            MouseFilter = Control.MouseFilterEnum.Pass
        };
        var art = CreateArtTexture(sheet, region, size);
        wrapper.AddChild(art);

        button = new Button
        {
            Text = string.Empty,
            TooltipText = tooltip,
            Position = Vector2.Zero,
            Size = size,
            FocusMode = Control.FocusModeEnum.All,
            MouseDefaultCursorShape = Control.CursorShape.PointingHand
        };
        var transparent = CreateTransparentButtonBox();
        button.AddThemeStyleboxOverride("normal", transparent);
        button.AddThemeStyleboxOverride("hover", CreateTransparentButtonBox());
        button.AddThemeStyleboxOverride("pressed", CreateTransparentButtonBox());
        button.AddThemeStyleboxOverride("focus", CreateTransparentButtonBox());
        button.AddThemeStyleboxOverride("disabled", CreateTransparentButtonBox());
        button.AddThemeColorOverride("font_color", new Color(1f, 1f, 1f, 0f));
        button.Pressed += pressed;
        button.MouseEntered += () => art.Modulate = new Color(1.08f, 1.08f, 1.08f, 1f);
        button.MouseExited += () => art.Modulate = Colors.White;
        button.ButtonDown += () => art.Modulate = new Color(0.88f, 0.88f, 0.88f, 1f);
        button.ButtonUp += () => art.Modulate = Colors.White;
        wrapper.AddChild(button);
        return wrapper;
    }

    private static StyleBoxFlat CreateTransparentButtonBox()
    {
        var box = new StyleBoxFlat
        {
            BgColor = new Color(0f, 0f, 0f, 0f),
            BorderColor = new Color(0f, 0f, 0f, 0f)
        };
        box.SetBorderWidthAll(0);
        return box;
    }

    private void CycleIntSetting(Func<int> getter, Action<int> setter, int min, int max, int step)
    {
        var next = getter() + step;
        if (next > max) next = min;
        setter(next);
        PersistSettings();
        ShowSettingsTab(activeSettingsTab);
        RefreshMenuSelection();
    }

    private void CycleFloatSetting(Func<float> getter, Action<float> setter, float min, float max, float step)
    {
        var next = MathF.Round(getter() + step, 1);
        if (next > max) next = min;
        setter(next);
        PersistSettings();
        ShowSettingsTab(activeSettingsTab);
        RefreshMenuSelection();
    }

    private void ToggleBoolSetting(Func<bool> getter, Action<bool> setter)
    {
        setter(!getter());
        PersistSettings();
        ShowSettingsTab(activeSettingsTab);
        RefreshMenuSelection();
    }

    private void CycleMapSkin()
    {
        var options = GameSettings.MapSkinOptions;
        var current = Array.IndexOf(options, composition.Session.Settings.MapSkinId);
        composition.Session.Settings.MapSkinId = options[(current + 1 + options.Length) % options.Length];
        PersistSettings();
        RefreshMenuSelection();
    }

    private void CycleParticleQuality()
    {
        var values = Enum.GetValues<ParticleQuality>();
        var current = Array.IndexOf(values, composition.Session.Settings.ParticleQuality);
        composition.Session.Settings.ParticleQuality = values[(current + 1 + values.Length) % values.Length];
        PersistSettings();
        ShowSettingsTab(activeSettingsTab);
    }

    private void ToggleScreenShake()
    {
        composition.Session.Settings.ScreenShake = !composition.Session.Settings.ScreenShake;
        PersistSettings();
        RefreshSettingsPanel();
    }

    private void ToggleDamageNumbers()
    {
        composition.Session.Settings.DamageNumbers = !composition.Session.Settings.DamageNumbers;
        PersistSettings();
        RefreshSettingsPanel();
    }

    private void PersistSettings()
    {
        audio?.SetVolume(composition.Session.Settings.MasterVolume * composition.Session.Settings.SfxVolume);
        _ = composition.SaveSettingsAsync();
    }

    private void ToggleFullscreen()
    {
        composition.Session.Settings.Fullscreen = !composition.Session.Settings.Fullscreen;
        ApplyDisplaySettings();
        PersistSettings();
        ShowSettingsTab(activeSettingsTab);
    }

    private void ApplyDisplaySettings()
    {
        DisplayServer.WindowSetMode(composition.Session.Settings.Fullscreen
            ? DisplayServer.WindowMode.Fullscreen
            : DisplayServer.WindowMode.Windowed);
    }

    private void RefreshSettingsPanel() => ShowSettingsTab(activeSettingsTab);

    private void BuildBattleHud(RunLoadout loadout)
    {
        battleLayer = new Control { Name = "BattleHud", MouseFilter = Control.MouseFilterEnum.Ignore };
        battleLayer.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(battleLayer);

        hpBar = new ProgressBar { ShowPercentage = false, Value = 100, MaxValue = 100 };
        hpBar.Position = new Vector2(24, 24);
        hpBar.Size = new Vector2(380, 28);
        battleLayer.AddChild(hpBar);

        experienceBar = new ProgressBar { ShowPercentage = false, Value = 0, MaxValue = 10 };
        experienceBar.Position = new Vector2(24, 58);
        experienceBar.Size = new Vector2(380, 16);
        battleLayer.AddChild(experienceBar);

        hudLabel = new Label { Position = new Vector2(24, 84), Size = new Vector2(520, 70) };
        hudLabel.AddThemeFontSizeOverride("font_size", 18);
        hudLabel.AddThemeColorOverride("font_color", new Color("#E7F0E8"));
        battleLayer.AddChild(hudLabel);

        weaponLabel = new Label { Position = new Vector2(760, 84), Size = new Vector2(360, 110), HorizontalAlignment = HorizontalAlignment.Right };
        weaponLabel.AddThemeFontSizeOverride("font_size", 15);
        weaponLabel.AddThemeColorOverride("font_color", new Color("#D8E8DB"));
        battleLayer.AddChild(weaponLabel);

        bossLabel = new Label { Position = new Vector2(420, 24), Size = new Vector2(400, 42), HorizontalAlignment = HorizontalAlignment.Center };
        bossLabel.AddThemeFontSizeOverride("font_size", 18);
        bossLabel.AddThemeColorOverride("font_color", new Color("#F0B1C0"));
        battleLayer.AddChild(bossLabel);

        pauseButton = new Button { Text = "Ⅱ", Position = new Vector2(1160, 24), Size = new Vector2(90, 58) };
        pauseButton.AddThemeFontSizeOverride("font_size", 26);
        pauseButton.Pressed += TogglePause;
        battleLayer.AddChild(pauseButton);

        pauseOverlay = new ColorRect
        {
            Name = "PauseOverlay",
            Color = new Color(0.02f, 0.04f, 0.05f, 0.78f),
            Visible = false,
            ZIndex = 8,
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        pauseOverlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var pauseText = new Label
        {
            Text = "游戏暂停\n\n按 ESC 或右上角按钮继续",
            Position = new Vector2(420, 260),
            Size = new Vector2(440, 160),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        pauseText.AddThemeFontSizeOverride("font_size", 28);
        pauseText.AddThemeColorOverride("font_color", new Color("#E7F0E8"));
        pauseOverlay.AddChild(pauseText);
        battleLayer.AddChild(pauseOverlay);

        levelUpPanel = new PanelContainer
        {
            Name = "LevelUpPanel",
            Position = new Vector2(170, 145),
            Size = new Vector2(940, 430),
            ZIndex = 10,
            Visible = false
        };
        var levelUpContent = new VBoxContainer { Name = "LevelUpContent" };
        levelUpContent.AddThemeConstantOverride("separation", 12);
        levelUpPanel.AddChild(levelUpContent);
        var levelUpTitle = new Label { Text = "升级奖励 · 请选择一项", HorizontalAlignment = HorizontalAlignment.Center };
        levelUpTitle.AddThemeFontSizeOverride("font_size", 26);
        levelUpTitle.AddThemeColorOverride("font_color", new Color("#E0B477"));
        levelUpContent.AddChild(levelUpTitle);
        var levelUpHint = new Label { Text = "战斗已暂停。选择后继续运行。", HorizontalAlignment = HorizontalAlignment.Center };
        levelUpHint.AddThemeColorOverride("font_color", new Color("#80969B"));
        levelUpContent.AddChild(levelUpHint);
        levelUpChoices = new VBoxContainer { Name = "LevelUpChoices" };
        levelUpChoices.AddThemeConstantOverride("separation", 10);
        levelUpChoices.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        levelUpContent.AddChild(levelUpChoices);
        battleLayer.AddChild(levelUpPanel);

        joystick = new MobileJoystick { Position = new Vector2(24, GetViewportRect().Size.Y - 248), Size = new Vector2(220, 220) };
        battleLayer.AddChild(joystick);

        resultLabel = new Label { Position = new Vector2(120, 420), Size = new Vector2(480, 100), HorizontalAlignment = HorizontalAlignment.Center };
        resultLabel.AddThemeFontSizeOverride("font_size", 30);
        resultLabel.Visible = false;
        battleLayer.AddChild(resultLabel);

        restartButton = new Button { Text = "返回首页", Position = new Vector2(200, 540), Size = new Vector2(320, 76) };
        SurvivorUiTheme.ApplyButton(restartButton, SurvivorButtonTone.Orange);
        restartButton.Visible = false;
        restartButton.Pressed += ReturnToMenu;
        battleLayer.AddChild(restartButton);

        RefreshHud();
    }

    private void ShowLevelUpPanel()
    {
        if (battle is null || levelUpPanel is null || levelUpChoices is null || levelUpPanel.Visible) return;
        var snapshot = battle.CreateSnapshot();
        if (snapshot.UpgradeChoices.Count == 0) return;
        levelUpPanel.Visible = true;
        foreach (var child in levelUpChoices.GetChildren()) child.QueueFree();
        foreach (var choice in snapshot.UpgradeChoices)
        {
            var captured = choice;
            var type = choice.IsWeapon ? "武器" : "被动";
            var levelText = choice.CurrentLevel <= 0 ? "新获得" : $"等级 {choice.CurrentLevel} → {choice.NextLevel}/{choice.MaxLevel}";
            var button = new Button
            {
                Text = $"【{type}】  {choice.Name}   {levelText}\n{choice.Description}",
                CustomMinimumSize = new Vector2(0, 82),
                Alignment = HorizontalAlignment.Left
            };
            button.AddThemeFontSizeOverride("font_size", 16);
            button.Pressed += () => SelectUpgrade(captured);
            levelUpChoices.AddChild(button);
        }
    }

    private void SelectUpgrade(UpgradeChoiceSnapshot choice)
    {
        if (battle is null || !battle.ChooseUpgrade(choice.Id, choice.IsWeapon)) return;
        audio?.PlayCue("level_up");
        if (levelUpPanel is not null) levelUpPanel.Visible = false;
        if (battle.NeedsUpgradeChoice)
        {
            composition.StateMachine.Set(GameState.LevelUp);
        }
        else
        {
            composition.StateMachine.Set(GameState.Playing);
        }
        battleView?.QueueRedraw();
        RefreshHud();
    }

    private Godot.Vector2 ReadInput()
    {
        var keyboard = Godot.Vector2.Zero;
        if (Input.IsKeyPressed(Key.A) || Input.IsKeyPressed(Key.Left)) keyboard.X -= 1f;
        if (Input.IsKeyPressed(Key.D) || Input.IsKeyPressed(Key.Right)) keyboard.X += 1f;
        if (Input.IsKeyPressed(Key.W) || Input.IsKeyPressed(Key.Up)) keyboard.Y -= 1f;
        if (Input.IsKeyPressed(Key.S) || Input.IsKeyPressed(Key.Down)) keyboard.Y += 1f;
        if (keyboard.LengthSquared() > 1f) keyboard = keyboard.Normalized();
        if (joystick is null || joystick.Value.LengthSquared() < 0.01f) return keyboard;
        return joystick.Value;
    }

    private void RefreshHud()
    {
        if (battle is null || hudLabel is null || hpBar is null || experienceBar is null) return;
        var snapshot = battle.CreateSnapshot();
        hpBar.MaxValue = snapshot.Player.MaxHp;
        hpBar.Value = snapshot.Player.Hp;
        experienceBar.MaxValue = Math.Max(1, snapshot.Player.RequiredExperience);
        experienceBar.Value = snapshot.Player.Experience;
        var remaining = TimeSpan.FromSeconds(snapshot.Elapsed);
        hudLabel.Text = $"{remaining:mm\\:ss}   等级 {snapshot.Player.Level}   击杀 {snapshot.KillCount}\n" +
                        $"波次 {snapshot.CurrentWave}/{snapshot.WaveCount}   敌人 {snapshot.Enemies.Count}   经验 {snapshot.TotalExperience}\n" +
                        $"{WaveStateLabel(snapshot.WaveState)}   事件 {snapshot.MapEvents.Count(value => value.Active)}   {MapSkinLabel(composition.Session.Settings.MapSkinId)}";
        if (composition.Session.Settings.ShowPerformanceMonitor)
        {
            hudLabel.Text += $"\n帧率 {Engine.GetFramesPerSecond()}  特效 {snapshot.Effects.Count}  网格 {snapshot.Enemies.Count}";
        }
        if (weaponLabel is not null)
        {
            var weapons = snapshot.Weapons.Count == 0
                ? "武器：暂无"
                : "武器\n" + string.Join("  ·  ", snapshot.Weapons.Select(value => $"{value.Name} {value.Level}/{value.MaxLevel}"));
            var passives = snapshot.Passives.Count == 0
                ? "被动：暂无"
                : "被动\n" + string.Join("  ·  ", snapshot.Passives.Select(value => $"{value.Name} {value.Level}/{value.MaxLevel}"));
            weaponLabel.Text = weapons + "\n" + passives;
        }
        if (bossLabel is not null)
        {
            var boss = snapshot.Enemies.FirstOrDefault(value => value.IsBoss);
            bossLabel.Text = boss is null ? string.Empty : $"首领  {boss.Hp:0}/{boss.MaxHp:0}";
        }
    }

    private void TogglePause()
    {
        if (battle is null || battle.IsFinished) return;
        paused = !paused;
        composition.StateMachine.Set(paused ? GameState.Paused : GameState.Playing);
        if (pauseButton is not null) pauseButton.Text = paused ? "▶" : "Ⅱ";
        if (pauseOverlay is not null) pauseOverlay.Visible = paused;
        audio?.PlayCue("pause");
        RefreshHud();
    }

    private void ShowResult()
    {
        if (battle is null || resultLabel is null || restartButton is null) return;
        resultShown = true;
        if (levelUpPanel is not null) levelUpPanel.Visible = false;
        if (pauseOverlay is not null) pauseOverlay.Visible = false;
        var result = new GameResultStats
        {
            Victory = battle.Victory,
            CharacterId = composition.Session.Launch.CharacterId,
            SkinId = composition.Session.Launch.SkinId,
            StageId = composition.Session.Launch.StageId,
            SurvivalTime = battle.Elapsed,
            KillCount = battle.KillCount,
            MaxLevel = battle.Level,
            TotalExperience = battle.TotalExperience
        };
        _ = composition.RecordResultAsync(result);
        audio?.PlayCue(result.Victory ? "result" : "boss");
        resultLabel.Text = battle.Victory ? "战斗完成" : "战斗结束";
        resultLabel.Visible = true;
        restartButton.Visible = true;
        RefreshHud();
    }

    private void ReturnToMenu()
    {
        if (menuRequested is not null)
        {
            menuRequested();
            return;
        }

        if (battleView is not null) battleView.QueueFree();
        if (battleLayer is not null) battleLayer.QueueFree();
        battleView = null;
        battle = null;
        battleLayer = null;
        joystick = null;
        levelUpPanel = null;
        levelUpChoices = null;
        pauseOverlay = null;
        audio = null;
        weaponLabel = null;
        bossLabel = null;
        resultShown = false;
        paused = false;
        composition.StateMachine.Set(GameState.MainMenu);
        composition.Presentation.GoTo(PresentationScreen.MainMenu);
        menuLayer!.Visible = true;
        ShowHome();
    }

    private void BuildError(Exception exception)
    {
        var error = new Label
        {
            Text = $"启动失败\n\n{exception.Message}",
            Position = new Vector2(36, 240),
            Size = new Vector2(648, 460),
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        error.AddThemeColorOverride("font_color", new Color("#F28B82"));
        error.AddThemeFontSizeOverride("font_size", 22);
        AddChild(error);
    }
}
