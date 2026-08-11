using System;
using DoomSurvivor.Application;
using DoomSurvivor.Core;
using DoomSurvivor.Infrastructure;
using Godot;

namespace DoomSurvivor.Presentation;

public partial class MainMenuScene : Node2D
{
    private GameCompositionRoot composition = null!;
    private IResourceProvider resources = null!;
    private Action<RunRequest>? runRequested;

    public void Configure(GameCompositionRoot root, IResourceProvider resourceProvider, Action<RunRequest> onRunRequested)
    {
        composition = root;
        resources = resourceProvider;
        runRequested = onRunRequested;
    }

    public override void _Ready()
    {
        var mobileGame = new MobileGame();
        mobileGame.Configure(composition, resources, PresentationEntry.MainMenu, runRequested);
        AddChild(mobileGame);
    }
}
