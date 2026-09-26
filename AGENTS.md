# Working on VoidFall

## Start narrow

This repository is the active Unity game. Read `Docs/REPO_MAP.md`, select the
subsystem relevant to the task, then inspect its implementation, immediate
callers and relevant tests. **Do not repeat a whole-repository onboarding or
scan by default.** Expand only when evidence crosses a boundary or the user
explicitly requests a broad audit. Update the map when changing ownership,
entry points or important invariants; do not turn it into a change log.

Check `git status` before editing and preserve unrelated work. This file and
`Docs/REPO_MAP.md` define standing development contracts, including the intended
behavior after foundation repairs. They do not certify that a repair has passed.
Current implementation/validation status belongs in
`Docs/AI/FoundationRepairStatus.md`. The September 26 evidence is in
`Docs/AI/DeepAudit-2026-09-26.md` and
`Docs/AI/MuseAuditVerification-2026-09-26.md`; consult the relevant finding,
not both reports in full for an unrelated task. The latter distinguishes
verified defects from rejected claims and untested design proposals.

The directory containing this file, `Assets/`, `Packages/` and `ProjectSettings/`
is the canonical Unity source root. Its parent holds `Builds/`, `Prototypes/`
and launch helpers; do not mistake the outer folder for the Unity project.
Sibling director/journey worktrees are preserved recovery references.
Use `../START_HERE.md` for launch instructions and
`Docs/Design/2026-09-12-Consolidation.md` for source provenance. Preserve the
designated `../Builds/Archive/` releases and the live `../Builds/RunExports/`.

## Parallel work and prototypes

Map/asset prototyping and foundation repair may proceed in parallel. Keep
exploratory output in a named folder under `../Prototypes/` or the task's
agreed output location. Label the study's purpose, references and approval
status. A prototype's existence, polish or inclusion in the dashboard does
not approve production integration, new lore or replacement artwork.

For prototype work, start with the art/arena sections of `Docs/REPO_MAP.md`
and the relevant approved design reference. Preserve Zack's normal scale,
arena-specific identities and the readability of enemies, hazards and HUD.
Record intended world dimensions, camera framing, collision bounds, texture
sizes and effect density so a later integration can be assessed concretely.

Keep prototype tasks away from profile transactions, journey flow, pool
admission, telemetry, shared input and production rendering changes unless
their task includes that integration. Before touching a shared file, inspect
its current diff and the repair status record. Never discard another task's
edits or repin a golden master to make concurrent work pass. Coordinate Unity
Editor/test/build/bake use: only one task may operate on this checkout's
Editor/import state at a time. Prototype previews need not build the game.

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

## Approved weapon studies

Weapon-study approval (September20): the owner locked the Summons, Mines and
Clock iterations plus Pulse Pistol and Railgun rank/evolution projectile art
for later implementation. The exact scope and frozen browser references are in
`Docs/Design/2026-09-20-Weapon-Approvals.md`. The other projectile proposals and
all eight first-round new weapon concepts are rejected. New concepts remain
browser exploration; do not infer their approval from prototype presence.

## Approved dealer and legendary direction

The dealer studies in `../Prototypes/dealer-travel` and `../Prototypes/dealer-shop`
are approved visual references. Follow the owner's subsequent floating-placement
changes in `Docs/Design/2026-09-12-DealerPreviewFidelity.md`; do not restore the
original platform overlap simply to match an older screenshot. Preserve the same
creepy ASCII face, four appearances, feature shading, moving hair, breathing,
head/gaze motion and purchase grin. The face floats beyond the platform with
upper-side appearances only (owner revision September20), no platform shadow, and 1.3x animation
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

## Foundation boundaries

- **Profile commits:** stage a complete candidate profile, persist it once,
  then publish the committed state and success UI. Workshop Scraps and ranks,
  form unlocks, and terminal run totals/records must commit together within
  their transaction. Nested helpers must not write an intermediate profile.
  A failed write leaves live progression and its last good disk state intact.
- **Wallets and compatibility:** dealer purchases spend the current run's
  earned Scraps; Workshop spends saved profile Scraps. Do not debit one wallet
  to repair the other. Preserve existing schema IDs and legacy adapters. A
  complete Unity profile transfer must retain forms, unlocks, cleared-void IDs
  and all settings. Browser compatibility exports must state their scope.
  Preserve/quarantine future-version saves without silently downgrading them.
- **Journey:** each of the five between-void crossings includes the safe
  dealer room, including a single destination. Route planning is separate
  from committed travel. Commit the destination before its objective begins;
  retain the same-visit Hydra I/II transition. Reward choices own their pause
  until resolved, and an off-screen relic must not strand departure.
- **Pool retirement:** keep active flags, order tables, admission counters,
  provenance, identity sidecars and views consistent on expiry, interception,
  mass clear and slot reuse. Clearing hostile shots also restores curved-shot
  capacity. Reuse the owning cleanup path rather than another partial reset.
- **Spawn admission:** native arena caps apply to every applicable arrival
  path, including queued offspring. Defer eligible births with their identity
  and reward ownership intact. Do not cull survivors on a lower-cap phase or
  silently use the global 750-body pool as a native arena budget.
- **Assets:** define one release owner for each generated object and loaded
  asset family. Detach views/caches before releasing packages. Register owned
  material instances for destruction; do not mutate a shared material to fix
  an instance leak. New full arena art must not acquire a second permanent
  Resources cache outside the arena lifetime. Tiny route thumbnails are separate.
- **UI/input:** the active modal owns submit/cancel and suppresses background
  shortcuts. Set valid initial/restored focus for keyboard/controller users;
  selection and pointer hover must expose equivalent actionable information.
  Check HUD contrast over bright arenas and respect reduced-motion settings.
- **Hot paths:** bind stable simulation callbacks outside per-tick loops.
  Keep collision queries conservative for grown actors without scanning a
  maximum-sized neighborhood unnecessarily. Preserve query order, collision
  coverage and RNG behavior. Keep telemetry queues bounded and loss visible;
  improve serialization without dropping the facts needed for balance work.

Treat these as requirements for new work and regression tests. Use the repair
status record and actual results to establish which existing paths satisfy
them; do not infer completion from this document's present-tense contracts.

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

Director I version 8 is implemented with a 750-actor ceiling; owner difficulty
tuning remains a playtest decision. Its live path is `.DirectorI.cs`, not the
legacy 64/128/192 profile bands. See the sustained-combat design and validation
documents in `Docs/Design/`. Approved map dimensions are recorded below.
Further health, density, run-length and arena-objective changes are balance
decisions; an audit suggestion does not authorize silently retuning them.

Owner-approved follow-up: survival is **360 seconds per void**. Loot policy v3
reserves special-drop capacity, preserves currency/charges when consolidating,
and returns distant earned loot into reach without directly granting it.
That earlier loot pass did not include physical arena changes. The owner-approved
September12 map integration now owns those changes separately.
Read `Docs/Design/2026-09-08-LootReachability-SixMinutes.md` before changing
pickup allocation/iteration or time-dependent arena/director rules. Pickup
generation snapshots must prevent same-tick collection of newborn rewards.
The safe escape window is ten active seconds; modal choices suspend it.
Six survival phases already require 36 minutes before bosses and crossings.
Do not promise a 30-minute completed run or shorten the phases implicitly.

## Approved map integration (September20)

The owner approved the final browser map study for native implementation.
`Docs/Design/2026-09-20-Approved-Map-Integration.md` is the detailed scope;
`Tools/ApprovedMaps` freezes the exact art and its reproducible exporter.
Null City uses slider50% (860 vertical world units); Hydra and Court use60%
(908 units). Preserve normal Zack size and the Spatial Awareness zoom bonus.
Court and City cameras remain bounded to their authored surfaces.
Null City's full artwork is centered on its origin; its offset walkable floor
is a different rectangle. Do not recenter the camera using the floor midpoint
or confuse world-layout scale with prop, actor or effect scale.

Null City retains its original architecture and mechanics with a1.6× expanded
layout (2560×1440 art,1984×841.6 floor). Positions expand; props, enemies and
combat radii keep native size. Five specialists and five ordinary pursuers
supplement the original roster. Laser roads shake locally, respecting reduced
motion; transit clips between both portals in play and menu previews.

Court is one fixed56×56 board of129.6-unit soft-white/black tiles. Twenty
forms preserve source Pawn/Bishop/Queen/Rook I designs and restore the original
geometric Knight alongside the newer Knights. Pawns and mounted pawn riders
have eight points. Knights dash in an L;
three large armored warhorses carry Pawn III, pursue at1.2× pawn speed and
independently block20% of player hits. Sentinels keep their tracking eye with
three small tooth-filled mouths and100k–150kHP. Preserve crowd-pushed movement;
the eye,4×4 attack area and shield follow the actual body. Attacks alternate
eight black cells, then eight white cells, with3s warning and0.45s burst every14s.
The nonstacking24-point allied shield shows only “Enemy shielded”, coalesced
for simultaneous grants. Wing and Wang share the encounter name Wingwang and
the same cell strike effects as Sentinels. Whole-color board attacks occur
only during their fight; their army
is immune. Preserve fallen X eyes, HP bars without numbers, exactly
“Sacificed the RoooK !”, and idempotently queued5–6 roster-one children.

Hydra I's dark glyph floor is stationary. Its ten original hybrid/Virus
populations remain active alongside five insects. Three hives each request
five offspring every3s (three original, two insects); destroying one stops
its broods and releases one of two guardians. Full pools defer births.
Preserve the SAME-visit collapse/swap/settle transition and Hydra II's original
Unity map, authored boss, behavior and health rules. Do not restore the
rejected browser replacement lair or downward floor scrolling.

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

The current comparison PC is the owner's i7-7700HQ / GTX 1060 6 GB / approximately
16 GB RAM machine at 1920×1080, 60 Hz. Use 60 FPS / 16.7 ms as the working comparison
target, not an established minimum-spec guarantee. Compare the same build
configuration, seed, loadout, arena, density and measurement window; verify
combat ticks/kills advance and report median, p95, p99 and spikes. A missing
GPU/GC counter means unavailable, not zero cost. Run Editor/build work outside
accepted player benchmark windows. Report measured gains after repairs rather
than predicting a percentage or equating lower graphics settings with faster
simulation. Asset, input and visual changes need their own relevant checks.

## Exclude by default

Do not scan or edit `Library/`, `Temp/`, `Logs/`, `TestResults/`, `obj/`, `.vs/`,
generated `.csproj`/solution files, player builds or third-party package caches
for ordinary gameplay work. Read a specific generated log only when needed.
Never commit these artifacts. The deprecated browser/React prototype is out
of scope. Do not hand-edit `ContentCatalog.Generated.cs`, generated arena/sprite
assets or Addressables output as a shortcut; use their authoring/baking path.
Change packages, project settings, scenes and prefabs only when the task requires it.

## Internal development dashboard

The owner’s content inventory lives in `Internal Dashboard/` beside `Assets/`.
Read `Internal Dashboard/Tool Description.md` for scope and maintenance.
Keep it in this repository and include its relevant files/generated snapshots
when the owner asks to commit or push the project. After changing cataloged
content or art, run `Internal Dashboard/Refresh-Inventory.ps1` and validate the
updated live inventory and legacy comparison. Archive presence does not approve
migration of old browser designs. Dashboard work must not modify gameplay.
