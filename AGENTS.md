# Working on VoidFall

## Start narrow

This repository is the active Unity game. Read `Docs/REPO_MAP.md`, select the
subsystem relevant to the task, then inspect its implementation, immediate
callers and relevant tests. **Do not repeat a whole-repository onboarding or
scan by default.** Expand only when evidence crosses a boundary or the user
explicitly requests a broad audit. Update the map when changing ownership,
entry points or important invariants; do not turn it into a change log.

Check `git status` before editing and preserve unrelated work. Historical
findings live in `Docs/AI/ReleaseReadiness-2026-09-04.md`; consult them when
relevant, not as mandatory context for every change.

## Architecture and locations

Product direction: VoidFall is a finite escape journey through randomized,
player-chosen branches, always starting in Abyss. Preserve meaningful arena
mechanics and the distinction between shared-pool and arena-exclusive bosses.
For route/map/ending work, read `Docs/Design/RunJourney.md`; it separates the
owner's intended design from proposals and current implementation gaps.
Names already present in the prototype graph are not evidence of approved
content; use the owner-approved list in that design document before adding arenas.

Unity `6000.5.7f1`, URP, Input System, Addressables and runtime-authored uGUI;
current build target is Windows x86-64. Verify versions in `ProjectSettings/`
and `Packages/` only when relevant to the task.

- `Assets/VoidFall/Core/`: engine-free rules, RNG, route/objective state.
- `Assets/VoidFall/Content/`: engine-free catalogues, spawning/upgrades/rewards.
- `Assets/VoidFall/Runtime/Gameplay/`: `VoidFallGameRuntime` partials compose
  the game; `GameSim` owns combat data, `FxSim` cosmetic data.
- `Assets/VoidFall/UI/`: views/controllers and runtime bridge contracts.
- `Assets/VoidFall/Audio/`, `Persistence/`: audio services and profile storage.
- `Assets/VoidFall/Editor/`: baking, build gates and Windows build entry point.
- `Assets/VoidFall/Tests/Editor/`, `Tests/PlayMode/`: rules/assets and runtime tests.

`Assets/Scenes/SampleScene.unity` is the single build scene. Most gameplay and
UI wiring is constructed in code, not prefabs. See the map before searching
for a scene object. `Core` and `Content` must remain free of UnityEngine;
Content deliberately uses the `VoidFall.Core` namespace.

## Preserve these contracts

- Follow local C# style: PascalCase types/methods, `_camelCase` private fields,
  existing namespaces/partials. Avoid unrelated formatting or broad extraction.
- Combat uses fixed-step simulation, struct pools and explicit order tables.
  Do not add per-enemy/projectile MonoBehaviours or casually reorder iteration,
  collision passes, RNG draws or slot reuse. Combat RNG and FX RNG are separate.
- Preserve the golden-master contract. Intentional simulation changes require
  an explained hash update and passing 32-seed sweep; never re-pin to hide drift.
- Runtime owns navigation/flow state; UI invokes callbacks/bridges. New runs
  start in Abyss regardless of menu preview. Every rift path must commit route
  selection before initializing the incoming objective.
- Preserve serialized IDs, `.meta` GUIDs and save compatibility. Storage read
  failures must not allow blank profiles to overwrite progression. Recovery
  must preserve the last good backup, including across failed writes.
- Arena textures are authored/baked in the Editor and loaded through
  Addressables. Preserve resource ownership and cleanup. Arena effects must
  not tint the player/HUD; keep authored Hydra art rather than replacing it
  with procedural approximations.

## Verification

Choose checks for the change: EditMode for rules/storage, PlayMode for runtime
flow, a Windows build for player/asset integration, captures for visuals.
Run Unity commands from the repo root with no other Editor holding the project:

```powershell
$unityEditor = 'C:/Program Files/Unity/Hub/Editor/6000.5.7f1/Editor/Unity.exe'
& $unityEditor -batchmode -nographics -projectPath $PWD -runTests -testPlatform EditMode -testResults Logs/editmode.xml -logFile Logs/editmode.log
& $unityEditor -batchmode -nographics -projectPath $PWD -runTests -testPlatform PlayMode -testResults Logs/playmode.xml -logFile Logs/playmode.log
& $unityEditor -batchmode -nographics -projectPath $PWD -executeMethod VoidFall.EditorTools.BuildScript.BuildWindows -logFile Logs/windows-build.log
```

Use the installed Editor path on other machines. Test commands must **not**
include `-quit`. Build output is `../Builds/VoidFall.exe`; preserve valuable
previous builds. `dotnet build VoidFall.Runtime.csproj -t:Rebuild` is only a
quick compile check when generated project files are current. Read test XML
and logs before claiming success. Historical passing counts are not validation.
Protect real saves during runtime tests. Stress-probe completion alone does
not prove active simulation or performance; inspect advancement and captures.

## Exclude by default

Do not scan or edit `Library/`, `Temp/`, `Logs/`, `TestResults/`, `obj/`, `.vs/`,
generated `.csproj`/solution files, player builds or third-party package caches
for ordinary gameplay work. Read a specific generated log only when needed.
Never commit these artifacts. The deprecated browser/React prototype is out
of scope. Do not hand-edit `ContentCatalog.Generated.cs`, generated arena/sprite
assets or Addressables output as a shortcut; use their authoring/baking path.
Change packages, project settings, scenes and prefabs only when the task requires it.
