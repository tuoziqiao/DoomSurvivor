# DoomSurvivor Unity Project Context

## Project Summary

`DoomSurvivor.unity` is the read-only Unity reference project for the Godot
mobile port. The target is `DoomSurvivor.godot`, a Godot 4.7.1 .NET project.
The immediate product target is a landscape single-player survivor slice; the
Godot code keeps configuration, catalog, session, and remote-config seams open
for later features.

## Confirmed Environment

- Unity reference version: `6000.5.4f1`
- Godot target version: `4.7.1-stable` Mono/.NET
- Godot target display: `1280 x 720`, landscape
- Reference path: `C:/MyProject/Game/DoomSurvivor/DoomSurvivor.unity`
- Target path: `C:/MyProject/Game/DoomSurvivor/DoomSurvivor.godot`
- Unity source is read-only during migration.

## Important Packages and Systems

- Unity Input System `1.19.0`
- Unity Universal Render Pipeline `17.5.0`
- Unity Test Framework `1.7.0`
- Newtonsoft Json `3.2.2`
- Godot target uses C# and `Godot.NET.Sdk/4.7.1`.

## Reference Layout

- `Assets/DoomSurvivor/Core`: DTOs, formulas, state contracts, save migration.
- `Assets/DoomSurvivor/Gameplay`: fixed-step combat, weapons, enemies, waves,
  events, and boss flow.
- `Assets/DoomSurvivor/Infrastructure`: config and save services.
- `Assets/DoomSurvivor/Presentation`: bootstrap, menus, HUD, input, and audio.
- `Assets/StreamingAssets/GameConfig`: seven JSON config sections.
- `Assets/DoomSurvivor/Presentation/Resources/Art` and `Models`: reference
  visual assets.

## Reference Scenes and Startup

Build settings contain `Bootstrap.unity`, `MainMenu.unity`, and `Battle.unity`.
The Godot target starts at `res://scenes/bootstrap.tscn`; its persistent
composition root routes child scenes through `main_menu.tscn` and
`battle.tscn`, while C# presentation nodes build the current mobile UI.

## Architecture Boundary

The Godot migration follows:

```text
presentation -> application -> gameplay/domains -> core
                              infrastructure (injected by application)
```

Core rules and catalog contracts do not depend on Godot. Godot-specific
resource loading, file persistence, scene nodes, and touch input stay in the
infrastructure/presentation layers.

## Current Godot Slice

- Core DTOs, rules, settings clamp, save migration, and spatial hash.
- Character, skin, stage, and unlock catalogs with `RunLoadout` creation.
- Built-in JSON config loading with cache/remote extension points.
- Atomic local save service and null network/session seams.
- Fixed-step player movement, auto-attack, enemy chase, crystals, XP, level-up,
  defeat/victory timing, landscape HUD, and touch joystick.
- Landscape menu settings grouped into Audio, Display, Crates, Hidden Crates,
  Altar, Map, Waves, and Skills tabs, with a `Bootstrap → MainMenu → Battle →
  Result` presentation flow contract.
- The first stage-5 gameplay split keeps `BattleSimulator` as the fixed-step
  orchestrator and separates movement, spawning, combat, pickup, and lifecycle
  systems behind the same `RunLoadout` snapshot.
- Reference JSON and PNG assets copied into `resources/config`, `resources/art`,
  and `resources/models` without editing the Unity project.

## Testing and Validation

- `dotnet build --no-restore DoomSurvivor.godot.csproj`: 0 warnings, 0 errors.
- `dotnet test --no-restore tests/DoomSurvivor.Tests.csproj`: 17 passed.
- Godot AI MCP run: real `MainMenuScene` and `BattleScene` child roots were
  observed at 1280x720; the current run has no editor/game errors or warnings.
- Android export, physical touch-device testing, and full Unity gameplay parity
  remain unverified.

## Known Gaps

The full Unity combat controller, all weapons/passives, map events, wave/Boss
flow, complete combat HUD parity, effects/audio, and release exports are not
yet migrated. Stage 5 systems still need spatial broad-phase integration,
weapons/passives, and the complete combat rules. Continue in small stages and
keep the Unity project unchanged.

_Last reviewed: 2026-08-11._
