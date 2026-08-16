# VoidFall (Unity)

A high-performance C# Unity 6 port of **VoidFall**, an endless space-survival shooter featuring fluid procedural combat, upgrades, dynamic nebula environments, atmospheric particle rendering, and full audio/tactile feedback.

## Project Overview

- **Engine Version**: Unity 6 (`6000.5.7f1`)
- **Rendering Pipeline**: Universal 2D / Custom procedural canvas rendering
- **Input System**: Unity New Input System (`com.unity.inputsystem`)
- **Target Platforms**: PC / Standalone (Windows, macOS, Linux), WebGL, Mobile

## Architecture & Modules

The codebase is organized into clean, modular assembly definitions (`asmdef`) under `Assets/VoidFall/`:

- **VoidFall.Core**: Pure simulation models, spatial collision grid, entity math, movement, combat math, procedural generators, deterministic RNG, telemetry, and game loop abstractions (zero engine dependencies).
- **VoidFall.Content**: Content catalogs, enemy definitions, elite abilities, director wave spawners, upgrade trees, and arena cycle configurations.
- **VoidFall.Runtime**: Unity-specific gameplay behaviors, entity rendering bridges, particle emitters, camera effects, HUD, controller binding, and audio triggers.
- **VoidFall.Persistence**: Save system, loadout state serialization, cross-platform persistence, and save data migration.
- **VoidFall.Audio**: Synthesized audio manager, procedural sound effects, and spatial audio feedback.
- **VoidFall.UI**: HUD widgets, upgrade selection screens, loadout panels, and pause menus.
- **VoidFall.Mobile**: Mobile touch controls and virtual joysticks.
- **VoidFall.Editor**: Editor tooling, asset processors, and build automation.

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
