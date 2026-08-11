using DoomSurvivor.Application;
using DoomSurvivor.Infrastructure;
using Godot;

namespace DoomSurvivor.Presentation;

public partial class AppRoot : Node2D
{
	public GameCompositionRoot Composition { get; private set; } = null!;

	public override void _Ready()
	{
		Composition = new GameCompositionRoot(new ConfigService(), new SaveService());
		var router = new PresentationSceneRouter();
		AddChild(router);
		router.Configure(Composition, new GodotResourceProvider());
		router.ShowMainMenu();
	}
}
