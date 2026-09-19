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

`voidfall-unity` is the canonical source project. The sibling director/journey
worktrees are preserved recovery references, not competing release projects.
Use `../START_HERE.md` for launch instructions and
`Docs/Design/2026-09-12-Consolidation.md` for source provenance. Preserve the
designated `../Builds/Archive/` releases and the live `../Builds/RunExports/`.

## Character, art direction and Workshop

Owner-established fiction: the playable blue eye is **the eye of Zack Hazard**,
the real character trying to escape the Void. Preserve this identity. Existing
technical names such as `Operative`, `frame` and `arsenal` do not establish a
spaceship or a conventional robot protagonist.

The owner describes the world as "a bunch of nothing": "robotics but not
robotics," shapes that sometimes suggest insects. Preserve that ambiguity.
The shared visual language uses sparse geometry, strong colored outlines,
dark interiors and luminous centers. Shapes and motion can suggest creatures,
machines or symbols without resolving into literal equipment. Null City's
explicit machinery is an arena-specific identity; do not apply its detailed
mechanical panels as the default style for Zack or the shared roster.

For character/art work, inspect the actual relevant sprites and their live
rendering path before proposing designs. `Docs/REPO_MAP.md` identifies these
sources; a roster-wide scan is unnecessary unless the task calls for one.

Workshop direction: **retain the existing Workshop and its upgrade artwork**.
Its purpose is permanent meta progression with visible changes to Zack, as
well as stat benefits. Purchased ranks must appear on the next run's character
at the scale shown by the existing preview; refunds must remove them.
The currency displays as **Scraps** in every player-facing string; serialized
save keys intentionally remain `parts`/`totalPartsEarned` for save
compatibility, so rename display text and identifiers, never schema names.
The browser studies (evolving bodies, specialized frames, modules, Iris,
Instinct and Fragments) were exploration, not approved replacement designs.
Do not infer additional Zack lore or a recovery/corruption arc from those studies.

## Approved dealer and legendary direction

The dealer studies in `../Prototypes/dealer-travel` and `../Prototypes/dealer-shop`
are approved visual references. Follow the owner's subsequent floating-placement
changes in `Docs/Design/2026-09-12-DealerPreviewFidelity.md`; do not restore the
original platform overlap simply to match an older screenshot. Preserve the same
creepy ASCII face, four appearances, feature shading, moving hair, breathing,
head/gaze motion and purchase grin. The face floats beyond the platform with
occasional upper and lower appearances, no platform shadow, and 1.3x animation
speed. Keep reduced-motion support. The room heading and movement-help text
stay removed; retain destination labels, Scraps and reachable E Browse.

The shop follows the level-up card design: icon/puzzle art, name, description,
and price below. No dealer headline, Gives/Takes sections or explanatory copy.
Browsing is free; three stable offers cost 100 **run Scraps** each, with one
purchase per crossing. Reopening must not reroll stock. Keep fragment saves
atomic; save failures must leave the wallet and fragment ownership unchanged.

Sound Blade and Charged Rifle are approved, each assembled from three distinct
permanent fragments and equipped in a separate manual slot. Do not replace
Workshop progression or consume automatic weapon slots. Roulette fragments and
the three alternative legendary candidates remain deferred. See
`Docs/Design/2026-09-12-DealerIntegration.md` for implemented scope; the earlier
unapproved Workshop "Fragments" study is a separate concept.

For dealer visuals, compare real Windows captures against the approved reference
and later owner changes. Compilation and gameplay tests alone do not establish
visual fidelity. Preserve the portrait's explicit glyph metrics and the room's
dark colors/faint rings; generic UI text scaling and linear alpha blending
previously changed their appearance. `Docs/REPO_MAP.md` locates the renderers,
art exporters and isolated capture probe.

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

## Music and reward presentation

Owner direction: a gameplay song loops in full until a Track Shift pickup changes
it; do not automatically select another song at its end. Roulette rewards must
use the existing `LevelUpView` upgrade menu and its card renderer, not a separate
reward-screen theme. Click ordinary reward cards to take them. Rule-changing
Wild Cards require explicit Take and Leave actions with their effects visible.
On the wheel, "Random" is bold; the stacked text beneath it is 20% smaller.
Keep thin-slice rewards readable using clearly connected callouts when needed.

## Run data is part of a gameplay feature

The owner uses automatic run exports to iterate on the director and balance.
When adding or changing observable gameplay, spawning, enemy behavior/stats,
arena rules/dimensions, progression, drops, upgrades, roulette or rewards,
**extend the existing exporter in the same change**. Read `Docs/RunExports.md`
and the telemetry section in `Docs/REPO_MAP.md` for schemas and hook ownership.
Record meaningful decisions/offers and the actual committed outcome, including
rejection/despawn reasons where relevant; provide stable IDs, run/simulation
time, arena/visit and source links. Do not consume RNG to discover an outcome.

Use `RunTelemetryRecorder` and the runtime `.Telemetry.cs` helpers rather than
creating another logger/exporter. Update schema documentation and add a focused
test proving new data appears in exported JSON/JSONL. Prefer aggregated damage
and sampled continuous state to per-frame event spam. No player notifications,
network uploads, per-hit file writes or unbounded queues. Keep exports outside
Assets, in `RunExports` beside the player; isolate automated tests. If capture
is incomplete, report counters/errors explicitly rather than hiding losses.

Director I version 6 is implemented with a 750-actor ceiling; owner difficulty
tuning remains a playtest decision. Its live path is `.DirectorI.cs`, not the
legacy 64/128/192 profile bands. See the sustained-combat design and validation
documents in `Docs/Design/`. Next owner priorities are map-size correction,
then adapted enemy health. Keep these separate from exporter maintenance.

Owner-approved follow-up: survival is **360 seconds per void**. Loot policy v2
reserves special-drop capacity, preserves currency/charges when consolidating,
and returns distant earned loot into reach without directly granting it.
That earlier loot pass did not include physical arena changes. The owner-approved
September12 map integration now owns those changes separately.
Read `Docs/Design/2026-09-08-LootReachability-SixMinutes.md` before changing
pickup allocation/iteration or time-dependent arena/director rules. Pickup
generation snapshots must prevent same-tick collection of newborn rewards.

## Approved map integration (September12)

Preserve the existing Null City artwork. Its authored coordinates convert to
world space at4× scale; scale geometry/props/hazard radii consistently rather
than changing enemy combat speeds or general health balance. Player size
adjustments are rendering-only. The city follows Zack and its existing sign
switches WELCOME TO NULL CITY / INTRUDER DETECTED. Active laser roads shake,
not the camera; reduced motion disables that shake.

Court is a single fixed neutral board, with129.6-unit tiles, local snapshotted
hazards sequentially armed over2s, one3.4s burst, and native mortar reticles.
Keep the existing chess roster and Grandmaster designs. Living sentinel rooks
are stationary/contact-only,100k–150kHP,targetable; fallen rooks have X eyes.
Show bars without numeric HP. Kill notification is exactly “Sacificed the RoooK !”
and5–6 roster-one children are queued idempotently. Preserve world boundaries
and viewport-aware recycling; do not restore scrolling split-field wallpaper.

Hydra I is360s survival without gene nodes/bone detail, with fast downward
original glyph animation. Its ten spawn-identity-keyed hybrids/Viruses are in
HydraPopulationRules and runtime HydraPopulation. Hydra I uses the existing
collapse/swap/settle flow to the boss in the SAME route visit. **Hydra II keeps
the old Unity map, authored art, boss behavior and health rules unchanged.**
Never copy the browser's experimental HydraII camera or replacement lair.

## Verification commands

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
include `-quit`. The canonical integrated player is `../Builds/VoidFall.exe`.
`VoidFall.EditorTools.BuildScript.BuildWindows` owns the Windows build pipeline;
legacy validation/preview/baseline helpers delegate to it. Keep new player build
entry points on that path rather than adding separate output directories.
The same builder accepts `VOIDFALL_BUILD_OUTPUT` for temporary validation and
`VOIDFALL_SOURCE_REVISION` for provenance. Verify the replacement before promoting
it to the canonical path; do not leave the staging player as another release.
Windows builds explicitly prefer DX11; DX12 is retained for opt-in diagnostics.
This mitigates the owner's fullscreen/focus freeze risk; the original forced-kill
hang had no captured stack and must not be described as conclusively fixed.
Preserve focus/graphics export evidence when investigating recurrence.
Keep temporary validation builds/backups only until the replacement is verified;
do not accumulate full player copies inside `Logs/` or `Builds/`. Preserve any
explicitly designated release archive. `Library/` is a regenerable Unity cache;
`Assets/VoidFall/Generated/` contains required baked assets, not cache.
`dotnet build VoidFall.Runtime.csproj -t:Rebuild` is only a
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
