using System;
using DoomSurvivor.Application;
using DoomSurvivor.Core;
using DoomSurvivor.Infrastructure;
using Godot;

namespace DoomSurvivor.Presentation;

public partial class BattleScene : Node2D
{
    private GameCompositionRoot composition = null!;
    private IResourceProvider resources = null!;
    private RunLoadout loadout = null!;
    private Action? menuRequested;

    public void Configure(
        GameCompositionRoot root,
        IResourceProvider resourceProvider,
        RunLoadout runLoadout,
        Action onMenuRequested)
    {
        composition = root;
        resources = resourceProvider;
        loadout = runLoadout;
        menuRequested = onMenuRequested;
    }

    public override void _Ready()
    {
        var mobileGame = new MobileGame();
        mobileGame.ConfigureBattle(composition, resources, loadout, menuRequested);
        AddChild(mobileGame);
    }
}
