# VoidFall repository map

Derived from the September 4, 2026 audit; update relevant sections as systems
change. This is a navigation aid, not a claim that every planned feature is
implemented. All paths below are relative to **`Assets/VoidFall/`**, except
paths explicitly starting with `Assets/`, `Docs/`, `Packages/` or `.github/`.

## Boot, ownership and dependencies

`Runtime/ParityFixtureProbe.cs` uses `BeforeSceneLoad` to create a persistent
root with `ParityFixtureProbe`, `FixedGameLoop` and `VoidFallGameRuntime`.
The only enabled scene is `Assets/Scenes/SampleScene.unity`; there are no
separate menu/gameplay scenes to load. The probe also checks the historical
fixture at `Assets/StreamingAssets/VoidFall/web-parity.json`.

`Runtime/Gameplay/VoidFallGameRuntime.cs` is the composition root. `Awake`
assembles camera/world views, audio, persistence, arena residency and UI.
`EnterMainMenu` is in its `.UI.cs` partial; `StartRunInternal`, `Simulate`,
`EndRun`, application focus and teardown are in the main file. Its `Update`
consumes a `FixedStepClock` to call `Simulate`. **`Runtime/FixedGameLoop.cs`
has a separate elapsed/tick counter; it does not call the combat simulation.**

Assembly direction (references, not namespaces):

```text
Core        engine-free rules; no dependencies
Content     -> Core (also uses namespace VoidFall.Core)
Persistence -> Core + Content
Audio       -> Core
UI          -> Core + Content + Persistence
Runtime     -> Core + Content + Persistence + UI + Audio
               + Input System + Addressables + URP
```

Assembly definitions live at each subsystem root. `Core` and `Content` have
`noEngineReferences: true`; `GameSim` is in Runtime and uses Unity types.

## Locate a gameplay change

| Task | First files to inspect |
|---|---|
| Simulation ordering / state reset | `Runtime/Gameplay/VoidFallGameRuntime.cs`: `Simulate`, `StartRunInternal`; `GameSim.cs`, `FxSim.cs` in the same directory |
| Movement / device polling | `Runtime/Input/InputReader.cs`; `Runtime/Gameplay/VoidFallGameRuntime.Sim.cs`: `MovePlayer` |
| Weapons / targeting / damage / pickups | `Runtime/Gameplay/VoidFallGameRuntime.Sim.cs`: `UpdateWeapons`, `FireWeapon`, `UpdateBlades`, `UpdateBullets`, `UpdatePickups`; `Core/CombatRules.cs`, `PickupRules.cs`, `BalanceRules.cs` |
| Enemies / spawn pressure / ordinary bosses | `Content/DirectorRules.cs`, `EnemyRosterRules.cs`, `EliteRules.cs`, `FormationRules.cs`; `Runtime/Gameplay/VoidFallGameRuntime.Sim.cs`: spawning, attacks and deaths |
| Upgrades / evolutions / support effects | `Content/UpgradeRules.cs`, `EvolutionRules.cs`, `SupportEffectRules.cs`, `ExtendedCatalog.cs`; `Core/ProgressionRules.cs`; runtime `RecalculatePlayerStats`, `RollLevelOptions`, `SelectLevelOption` |
| Roulette / reward ceremony | `Content/RouletteRules.cs`, `RoulettePresentationRules.cs`; `Runtime/Gameplay/VoidFallGameRuntime.Roulette.cs`, `.RouletteChest.cs`; `UI/Views/RouletteView.cs`, `RouletteWheelGraphic.cs`, `PrizeRevealView.cs` |
| Wild Cards / overclock / mutations | `Content/WildCardRules.cs`, `Core/OverclockRules.cs`, `MutationRules.cs`; `Runtime/Gameplay/VoidFallGameRuntime.WildCards.cs` and `.Sim.cs` |
| Meteors / arena hazards | `Core/MeteorRules.cs`, `HazardRules.cs`; `Runtime/Gameplay/GameSim.cs`, `VoidFallGameRuntime.NebulaStrikes.cs` |
| Player cosmetics / Workshop preview | `Runtime/Rendering/PlayerCosmetics.cs`, `PlayerFramePreview.cs`; `Runtime/Gameplay/VoidFallGameRuntime.Cosmetics.cs`; `UI/Views/WorkshopView.cs` |

Roulette relics emerge at the last defeated boss, ignore magnets, and require
physical pickup; the safe Rewards stage allows movement and waits for the relic.
`RoulettePresentationRules` projects the final probabilities, including the single
first/repeat protection re-sample, into segment sizes and readable reward facts.
The 6.8-second spin uses accumulated rotation and automatically opens one actual
prize card. Runtime grants the reward once and keeps pause ownership until Continue.
Improve Odds upgrades the Parts cache from 60 to 90 and rejects further no-op
purchases. UI ticks use the existing audio service. The relic owns its generated
sprite/texture and releases both at runtime teardown.

`Runtime/Gameplay/CombatStateTypes.cs` defines entity structs such as
`EnemyState`. Fixed-capacity arrays and order tables live in `GameSim`;
`Runtime/Gameplay/SlotOrder.cs` supports ordered pool traversal.
`Core/CollisionGrid.cs` supplies the spatial broad phase. Cosmetic pools and
their independent RNG live in `FxSim`. Some behavior remains split between
`GameSim` and runtime partials: inspect the actual caller before editing a
similarly named helper. Weapons currently auto-target and auto-fire; input
polling is chiefly movement, with menu shortcuts in the runtime's `Update`.

## Route, objectives and special encounters

Normal runs use `Content/PlayableVoidRoutes.cs`: a seeded finite graph of
prepared, objective-ready arenas with known metadata. The six prepared arenas
produce widths 1/2/1/1/1, with five arenas visited per path. The Tab overview is
`UI/Views/RouteMapView.cs`; clicks plan, while physical portals commit choices.
`Runtime/Gameplay/VoidFallGameRuntime.Journey.cs` owns reward/junction/travel
stages, map pause ownership, the safe portal room, load retry and terminal
return to Home. `VoidFallGameRuntime.LevelUps.cs` advances upgrade prompts in
both combat and safe reward phases. `.Roulette.cs` explicitly owns PrizeReveal
until Continue; `SyncUiScreen` must preserve that ownership.

- `Core/VoidRoute.cs`: `VoidRouteNode`, `VoidRouteRun`, `RouteNodeState`, graph
  definitions, history, sibling locking, `NotifyVoidCompleted`, `SelectNextVoid`.
- `Core/VoidObjective.cs`: `IVoidObjective` and `VoidObjectiveFeed` contracts.
  `Core/VoidObjectives.cs` provides objective implementations and `ForArena`.
  `Core/VoidObjectiveTracker.cs` batches kill/spawn/zone facts; `Step` consumes
  and resets the feed once per tick. Named IDs and encounter counts are distinct.
- `Runtime/Gameplay/VoidFallGameRuntime.Objectives.cs` reports simulation facts
  and handles completion. `.Rift.cs` schedules boss encounters, post-boss delay,
  route selection and collapse/swap/settle travel. `Core/VoidProgressionRules.cs`
  owns shared cadence rules.
- `UI/Views/RouteSelectController.cs` projects route cards and confirms choices;
  `RouteSelectView.cs` renders them. Automatic single-exit travel must pass
  through confirmation too. `CommitRiftTransitionSwap` clears enemies, shots
  and meteors before initializing the next arena/objective.
- Hydra: `Content/HydraContent.cs`, `Core/HydraEncounterRules.cs`,
  `Runtime/Gameplay/HydraRuntimeRules.cs`, `VoidFallGameRuntime.Hydra.cs`.
  Its route-owned boss suppresses ambient spawning; rib boundary collision
  differs from the non-colliding central spine.
- Court: matching `MonochromeContent.cs`, `MonochromeEncounterRules.cs`,
  `MonochromeRuntimeRules.cs`, `VoidFallGameRuntime.Monochrome.cs`. It owns a
  five-enemy chess roster. Two simultaneous Grandmasters share health; floor
  warning/burning phases alternate safe colors.
- Null City: `Content/NullCityContent.cs`, `Core/NullCityRules.cs`, runtime
  `VoidFallGameRuntime.NullCity.cs` and `.NullCity.Render.cs`. Twelve robot types
  share existing combat pools. The fixed city floor has Surveillance/Lockdown,
  purge lanes and hangar police; Motherload owns a permanent-lockdown encounter.
  Deferred birth/blast queues preserve slot reuse. Its death clears hostiles while
  retaining native boss dissolution and reward/relic flow. Space or controller
  left shoulder dashes only in this arena and resists the warned tractor cone.

**Do not equate route nodes with prepared arenas.** The historical prototype graph had ten
nodes but only five implemented objectives/packages: Abyss, Red Nebula, White
Sakura, Hydra and Monochrome Court. Normal runs now avoid those unfinished
destinations and resolve `HasEscaped` into a saved result and Home. The final
Overseer/cutscene remains future content. Null City uses stable route ID
`null-city`, a prepared package, survival/Motherload objective and playable-route metadata.

**Owner clarification:** the approved identities are Abyss, White Sakura, Red
Nebula, Monochrome Court, Hydra and Null City (also referred to
as Void City). Other names in the old graph are AI-generated placeholders,
not an approved content roadmap. Preserve technical IDs until deliberately
migrated, but do not implement filler arenas merely because their nodes exist.
The intended pool is ten Voids, with a randomized subset per run and a target
successful-run duration of roughly 30–40 minutes. See `Docs/Design/RunJourney.md`
for proposed additions and unresolved counting/pacing decisions.

## UI, settings and profile progression

`UI/Core/UIManager.cs` defines `UIScreen`, `UICallbacks` and `UIManager`.
The runtime supplies callbacks at startup; its `.UI.cs` partial's
`SyncUiScreen` selects the visible screen from runtime flags. Views must not
maintain a competing navigation state machine. Menus and overlays use uGUI;
the gameplay HUD has separate canvas ownership.

`UI/Core/UIBuilder.cs` contains construction helpers **and `UIViewBase`**.
Views build once via `Initialize`, reuse their hierarchy and toggle visibility
through a `CanvasGroup`. Look in `UI/Views/` for the screen being changed.
Live HUD synchronization is chiefly `Runtime/Gameplay/VoidFallGameRuntime.Hud.cs`
and `.UI.cs`; `UI/Hud/HudPresenter.cs` is not the sole live HUD owner.

The approved HUD remaster is composed by `.OverclockHud.cs`: the upper overclock
notification remains as a charged word and countdown underline, below the boss
bar. Text grows 10% per additional stack, with viewport fitting only at extreme
sizes. `Core/OverclockPresentationRules.cs` owns scale, pulse and charge math.
`UI/Core/MusicPerimeterGraphic.cs` keeps one static edge mesh; its resource shader
animates seeded per-activation rails, the 24-band spectrum and five runners in
each direction. `MusicPerimeterRules.CreateActivationLayout` consumes no combat
RNG. Stacking preserves the layout and retriggers a victory lap. Music remains
2x during overclock; the current soundtrack is retained. Existing high-contrast,
reduced-motion and UI pause ownership remain runtime settings concerns.

`UI/Core/IGameBridge.cs` exposes settings snapshots, restore, persistence,
live application and record reads. The nested `RuntimeGameBridge` in
`VoidFallGameRuntime.cs` implements it. Active controllers include
`UI/Core/SettingsController.cs`, `UI/Views/WorkshopController.cs` and
`RecordsController.cs`. Settings writes are debounced; callers must preserve
rollback on persistence failure. Workshop purchases affect profile ranks;
run upgrades are a separate state and lifecycle.

`Persistence/SaveStore.cs` defines `SaveData`, `SaveSettings`, `LifetimeStats`,
record/bestiary entries and schema handling. It saves under
`Application.persistentDataPath`; schema v5 intentionally retains the filename
`voidfall_save_v4.json`. Writes use temp + flush + atomic replacement + backup.
Recovery prioritizes the current backup over legacy profiles and protects it
across failed writes. `BrowserSaveImporter.cs` / `BrowserSaveExporter.cs` are
compatibility adapters, not a reason to reopen the deprecated browser project.

`Runtime/Gameplay/VoidFallGameRuntime.Persist.cs` owns `SaveRun`, reward/stat
commit and failure rollback. Live-run snapshots/resume are not implemented in
the audited schema; `SaveRun` only commits terminal game-over results.
`UI/Core/VideoSettingsRules.cs` supplies display rules;
`Runtime/Gameplay/VoidFallGameRuntime.VideoSettings.cs` applies resolution and
runtime volume overrides. Saved `arena` preview values, route IDs and `ArenaId`
are different representations: use existing mapping helpers.

## Audio, rendering and authored data

- `Audio/ProceduralAudio.cs`: SFX cues, voice/gate limits and fallback pad.
  `Audio/MusicDirector.cs`: streamed tracks and `SetReactiveState`;
  `Audio/MusicDspFilter.cs`: audio-thread DSP. Runtime `.Audio.cs` creates the
  services; main-file updates feed health/overclock/magnet state. Preserve
  audio-thread ownership and lock-free handoff. Tracks live in
  `Resources/VoidFall/Music/`; credits are in `Docs/AudioCredits.md`.
- `Runtime/Gameplay/VoidFallGameRuntime.Render.cs`, `.Fx.cs`, `.Arena.cs`:
  view synchronization, effects and arena presentation. Shared render material
  ownership is in `Runtime/Rendering/VoidFallRenderMaterials.cs`.
- `Runtime/Gameplay/ArenaRecipeAsset.cs`, `ArenaPlateAsset.cs` and
  `ProceduralSpriteCatalog.cs`: prepared asset contracts. `ArenaResidencyManager.cs`
  owns Addressables handles; `Core/ArenaResidencyPlanner.cs` bounds residency.
  `Core/ArenaCatalogRules.cs` maps identities/package addresses.
- `Editor/ArenaContentBaker.cs`, `ProceduralSpriteBaker.cs`,
  `ArenaAddressableMigration.cs`: authoring and registration. The
  `PreparedContentBuildGate` in `Editor/PreparedContentBuildSetup.cs` rejects
  missing/invalid declared content. Assets live under `Generated/` and
  `Assets/AddressableAssetsData/`; URP configuration is in `Rendering/URP/`.
- `Tools/NullCity/` exports approved artwork offline into `Art/NullCity/`.
  `Editor/NullCityContentBaker.cs` validates every frame, crop, PPU and FullRect
  bound. `NullCityVisualAsset` is referenced by `ArenaPlateAsset`, so Addressables
  owns the extra sprites with the plate. `BakeAndRegisterBatch` updates only the
  city package. Menu residency holds six packages; gameplay remains current plus
  two exits. Recipe seeds vary moving compositions without moving authored lanes.
- `Content/ContentCatalog.cs` defines data types;
  `ContentCatalog.Generated.cs` contains historical generated definitions.
  Unity-authored `HydraContent`, `MonochromeContent` and `ExtendedCatalog`
  extend that data. Legacy enums/counts are not necessarily the full live
  catalogue; consumers such as support selection use `ExtendedCatalog.AllSupports()`.

## Verification and deeper references

Use the commands in `AGENTS.md`. `Tests/Editor/` has targeted rules, catalogue,
asset, controller and save tests. `Tests/PlayMode/RuntimeFlowRegressionTests.cs`
covers integration boundaries; `SimulationGoldenMasterTests.cs` pins a state
hash and `SimulationGoldenMasterSweepTests.cs` runs 32 seeds twice. Keep the
authoritative hash and its change explanation in the test, not this map.

`Editor/BuildScript.cs` builds Windows. `.github/workflows/ci.yml` runs Unity
tests only with `UNITY_TESTS_ENABLED` and credentials configured. Runtime
`StressBenchmarkProbe.cs` is opt-in via `-vfbench`; verify simulated progress,
not just wall time. `Runtime/Telemetry/RunTelemetry.cs` records run events and
exports diagnostics. Capture arguments are parsed by `ConfigureVisualCapture`
in `VoidFallGameRuntime.cs`; `UpdateVisualCapture` in `.Sim.cs` writes images.
`Runtime/RouteJourneyProbe.cs` adds map/junction captures and accelerated whole-route
checks (`-vfjourney=map|junction|check`, `-vfoutput=...`, optionally `-vfbranch=right`),
with a separate profile beside the output. `Tests/PlayMode/JourneyIntegrationTests.cs`
covers physical choices, map pause, junction safety, terminal saves and retry.
`Tests/PlayMode/NullCityIntegrationTests.cs` covers reset, roster, deferred deaths,
boss cleanup and dash bounds. `-vfnullcity=surveillance|lockdown|motherload|tractor`
with `-vfcapture=<path>` selects diagnostic poses using an isolated adjacent profile.
`NullCityContentBaker.BuildValidationPlayer` writes to `../Builds/NullCityValidation/`.

Roulette visual QA: `Editor/RoulettePreviewCapture.cs` renders the actual views
to `Logs/RoulettePreview` via `Capture`; `BuildPlayer` writes a separate Windows
player to `../Builds/RoulettePreview` without replacing the normal build.
`-vfjourney=roulette -vfoutput=<absolute-prefix>` exercises boss defeat, relic
preservation, the spin and reveal, writing captures with an isolated profile.
Use a rendering player (without `-batchmode`) for screenshots; headless runs can
validate flow but produce black captures. Physical proximity is covered by the
PlayMode tests; the diagnostic driver deliberately invokes the pickup callback.

`Editor/OverclockHudValidation.BuildPlayer` creates a separate Windows build in
`../Builds/HudOverclock`. Launch it with `-vfoverclock-check=<absolute-folder>`
to capture ×1, ×3, low-charge and ×12 states with bosses. The diagnostic profile
is selected before the first save load; the probe checks live music analysis,
2x playback targets, stack sizing, boss clearance and activation pattern lifetime.

Read `Docs/Design/VoidFallArenaArchitecture.md` for arena design constraints,
`Docs/RefactoringPlaybook.md` for larger ownership changes, and
`Docs/AI/ReleaseReadiness-2026-09-04.md` for dated audit evidence and unresolved
release risks. They are task-specific references, not required bulk reading.
