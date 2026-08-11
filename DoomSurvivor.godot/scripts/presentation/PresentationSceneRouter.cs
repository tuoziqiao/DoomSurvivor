using System;
using DoomSurvivor.Application;
using DoomSurvivor.Core;
using DoomSurvivor.Infrastructure;
using Godot;

namespace DoomSurvivor.Presentation;

public partial class PresentationSceneRouter : Node
{
    private const string MainMenuScenePath = "res://scenes/main_menu.tscn";
    private const string BattleScenePath = "res://scenes/battle.tscn";

    private GameCompositionRoot composition = null!;
    private IResourceProvider resources = null!;
    private Node? currentScene;
    private bool configured;

    public void Configure(GameCompositionRoot root, IResourceProvider resourceProvider)
    {
        composition = root;
        resources = resourceProvider;
        configured = true;
    }

    public void ShowMainMenu()
    {
        EnsureConfigured();
        composition.StateMachine.Set(GameState.MainMenu);
        composition.Presentation.GoTo(PresentationScreen.MainMenu);

        var scene = LoadScene<MainMenuScene>(MainMenuScenePath);
        scene.Configure(composition, resources, OnRunRequested);
        ReplaceCurrent(scene);
    }

    private void OnRunRequested(RunRequest request)
    {
        EnsureConfigured();
        var loadout = composition.StartRun(request);
        ShowBattle(loadout);
    }

    private void ShowBattle(RunLoadout loadout)
    {
        EnsureConfigured();
        var scene = LoadScene<BattleScene>(BattleScenePath);
        scene.Configure(composition, resources, loadout, ShowMainMenu);
        ReplaceCurrent(scene);
    }

    private void ReplaceCurrent(Node nextScene)
    {
        currentScene?.QueueFree();
        currentScene = nextScene;
        AddChild(nextScene);
    }

    private static T LoadScene<T>(string path) where T : Node
    {
        var packed = ResourceLoader.Load<PackedScene>(path);
        if (packed is null) throw new InvalidOperationException($"Presentation scene not found: {path}");
        var instance = packed.Instantiate<T>();
        if (instance is null) throw new InvalidOperationException($"Presentation scene root type mismatch: {path}");
        return instance;
    }

    private void EnsureConfigured()
    {
        if (!configured) throw new InvalidOperationException("PresentationSceneRouter is not configured.");
    }
}
