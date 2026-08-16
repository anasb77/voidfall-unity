# VoidFall (Unity)

A high-performance C# Unity 6 port of **VoidFall**, an endless space-survival shooter featuring fluid procedural combat, upgrades, dynamic nebula environments, atmospheric particle rendering, and full audio/tactile feedback.

## Project Overview

- **Engine Version**: Unity 6 (`6000.5.7f1`)
- **Rendering**: Built-in pipeline, rendering direct to the backbuffer at native
  resolution. Sprites are rasterized procedurally at runtime; there are no
  authored texture assets for gameplay entities. No scriptable render pipeline
  is installed, so there is no post-processing stack (and therefore no bloom).
- **UI**: Legacy IMGUI (`OnGUI`)
- **Input System**: Unity New Input System (`com.unity.inputsystem`)
- **Target Platforms**: Windows/Standalone is the only configured and tested
  target. Android components are not installed; WebGL is untested.

## Architecture & Modules

There are five assembly definitions (`asmdef`) under `Assets/VoidFall/`:

- **VoidFall.Core**: Pure simulation rules — spatial collision grid, deterministic RNG, combat/movement/pickup/meteor/quality/balance math. No engine dependencies.
- **VoidFall.Content**: Generated content catalog plus hand-written elite, enemy-roster, upgrade-pool, and evolution rules.
- **VoidFall.Runtime**: All Unity behaviour. Simulation driver, procedural sprite and arena-plate factories, entity rendering, particles, camera effects, HUD, every menu, input, and telemetry.
- **VoidFall.Persistence**: Save store and browser save import/export.
- **VoidFall.Audio**: Procedural audio synthesis.

Two caveats for anyone navigating this for the first time:

- `Runtime/Gameplay/VoidFallGameRuntime.cs` is a single ~25,000-line class. The
  simulation, all enemy behaviours, rendering, HUD, and every menu live in it.
  Splitting it is tracked as open work in `MIGRATION_STATUS.md`.
- The `UI`, `Mobile`, and `Editor` folders are empty, and `Rendering` contains
  only shaders and icon assets. They are not assemblies. Earlier revisions of
  this README described them as such.

## Getting Started

1. **Clone the Repository**:
   ```bash
   git clone https://github.com/anasb77/voidfall-unity.git
   ```
2. **Open with Unity Hub**:
   - Unity Version: **Unity 6 (6000.5.7f1)**
   - Add the cloned folder as a project and open it.
3. **Run the Game**:
   - Open `Assets/Scenes/SampleScene.unity`.
   - Press **Play** in the Unity Editor.

## Controls

- **WASD / Left Stick / Arrow Keys**: Move Ship
- **Mouse / Right Stick**: Aim Direction
- **Left Mouse Button / Right Trigger / Space**: Fire Primary Weapon
- **Right Mouse Button / Left Trigger / Shift**: Special / Dash
- **Escape / P / Start**: Pause Menu
