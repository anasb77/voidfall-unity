# VoidFall repository map

Derived from the September 4, 2026 audit; update relevant sections as systems
change. This is a navigation aid, not a claim that every planned feature is
implemented. All paths below are relative to **`Assets/VoidFall/`**, except
paths explicitly starting with `Assets/`, `Docs/`, `Packages/` or `.github/`.

## Character identity and visual references

**Owner-established context:** the blue player eye belongs to **Zack Hazard**,
who is trying to escape the Void. `Operative` remains the implementation name.
The owner describes the art as "a bunch of nothing," "robotics but not
robotics," with shapes sometimes resembling insects. Keep the forms ambiguous;
conventional futuristic armor, reactors and engines are not a default brief
for the protagonist. See `AGENTS.md` for the standing Workshop decision.

The current artwork uses compact silhouettes, saturated outlines, dark space
and luminous centers. Shared progression develops recognizable shapes through
contour changes, repeated points and divided forms. This description is based
on the implemented artwork; it does not establish new lore or proposed content.
For visual work, view the relevant assets as well as reading their code:

| Character group | Current sources and identities |
|---|---|
| Zack's eye | `Runtime/Gameplay/ProceduralSpriteFactory.cs`: `Operative()` draws the circular blue iris, dark center and central light. Current baked sprite: `Generated/ProceduralSprites/Sprite_0089_fixed_operative.png`. `VoidFallGameRuntime.Render.cs` composes the eye, aura, ring and Workshop cosmetics. |
| Shared enemies and elites | Original forms: `ProceduralSpriteFactory.cs`; higher tiers: `Art/RosterProgression/`, `Resources/VoidFall/RosterProgressionVisuals.asset`, and runtime `.RosterProgression.cs`. Four tiers across 14 shared families and three elite families: Exploder, Siege Mortar and Curved Gunner. |
| Shared bosses | Herald, Warden, Matriarch and Reaver: definitions in `Content/ContentCatalog.Generated.cs`, artwork in `ProceduralSpriteFactory.cs` and baked `Generated/ProceduralSprites/` boss images. |
| Monochrome Court | Pawn, Rook, Bishop, Knight and Queen in black/white forms, plus Black and White Grandmasters. `Content/MonochromeContent.cs`, `ProceduralSpriteFactory.cs`, and baked Court sprites. |
| Hydra | Shared enemies with mutation traits in `Core/MutationRules.cs`; `Content/HydraContent.cs` defines Hydra Prime. The live boss uses `Resources/VoidFall/Hydra/HydraPrime.png`, loaded by runtime `.Hydra.cs`; inspect that authored art rather than assuming its procedural fallback is the live appearance. |
| Null City | Nine regular units, three lockdown police, and exclusive boss Motherload: `Content/NullCityContent.cs`, `Art/NullCity/Units/`, `Runtime/Gameplay/NullCityVisualAsset.cs`, and runtime `.NullCity.Render.cs`. Motherload is explicitly a detailed ship. |

The 14 shared families are Regular (`chaser`), Runner, Gunner, Twin Gunner,
Dasher, Brute, Exploder, Guard, Technician, Mortar, Splitter, Bulwark, Harvester
and Carrier. Regular's star identity is preserved across tiers.
`Docs/Design/EonSea-Approved.md` specifies thick saturated edges and luminous
cores, explicitly excluding a Null City mechanical-panel restyle of the shared
roster. `Docs/Design/NullCity-Approved.md` records that arena's distinct machine
and ship designs. Preserve these differences between arena identities.

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

The four appended weapons (Mines, Summons, Clock, Boomerang) are authored in
`Content/ArsenalContent.cs`; `ContentCatalog.DisplayNames.cs` appends them and
their evolutions after generated initialization, preserving the first six IDs.
Runtime `.Arsenal.cs` owns fixed-capacity entity pools and spawn-identity-keyed
freeze/clock hit timers. `.Arsenal.Render.cs` draws cached rank-specific art from
`ProceduralSpriteFactory.Arsenal.cs`. Clock's face and hands use 50% opacity;
mine range guides retain 70% of their original opacity. Idle summon creation
stops at squad size; existing returning summons persist within the active cap.
Mine freeze pauses ordinary enemy behavior but keeps damage-reception timers
advancing; bosses resist freeze. All state clears on new runs and travel.
Upgrade offers, roulette acquisition, HUD and records share the extended weapon
order. Evolution support lookup must use `ExtendedCatalog.AllSupports()`.
Their paired extended supports are also included in the build HUD. Artwork is
warmed during stat recalculation/upgrade commit, with allocation-free cache keys.
`Editor/ArsenalValidationBuild.BuildPlayer` writes `../Builds/Arsenal/`.
The opt-in `-vfarsenal=all|mines|summons|clock|boomerang` starts a test loadout
(`-vfarsenal-rank=1..6`, `-vfarsenal-evolved=1`); `-vfarsenal-check=<folder>`
captures rank I, VI, evolved and idle-summon states. Both isolate the profile
before the first load. `Tests/PlayMode/ArsenalIntegrationTests.cs` covers these
combat and presentation boundaries.

Shared tiers I–IV use `Content/EnemyRosterRules.cs` and
`Content/RosterProgressionTraits.cs`; runtime `.RosterProgression.cs` owns
higher-tier controllers and SpawnId-keyed sidecar state. Global run time drives
II9–15min, III24–30min, IV34–40min. Regular retains technical ID `chaser` and
its original star; `ContentCatalog.DisplayNames.cs` supplies its display name.
Dasher retains its original single design. The three elite families retain
native tierI logic and develop through II–IV. Imported sprites come from
`RosterProgressionVisualAsset` in Resources. Do not add higher-tier counters
to the golden-master-hashed EnemyState; use the sidecar state.
`Core/CollisionGrid.cs` supplies the spatial broad phase. Cosmetic pools and
their independent RNG live in `FxSim`. Some behavior remains split between
`GameSim` and runtime partials: inspect the actual caller before editing a
similarly named helper. Weapons currently auto-target and auto-fire; input
polling is chiefly movement, with menu shortcuts in the runtime's `Update`.

## Route, objectives and special encounters

Normal runs use `Content/PlayableVoidRoutes.cs`: a seeded finite graph of
prepared, objective-ready arenas with known metadata. The eight prepared arenas
produce widths 1/2/2/1/1/1, with six arenas visited per path. The Tab overview is
`UI/Views/RouteMapView.cs`; clicks plan, while physical portals commit choices.
`Runtime/Gameplay/VoidFallGameRuntime.Journey.cs` owns reward/junction/travel
stages, map pause ownership, the safe portal room, load retry and terminal
return to Home. `VoidFallGameRuntime.LevelUps.cs` advances upgrade prompts in
both combat and safe reward phases. `.Roulette.cs` explicitly owns PrizeReveal
until Continue; `SyncUiScreen` must preserve that ownership.

Escape timing now lives in Journey/Rift: 25 seconds of normal loot collection
with camera follow and "Initiating Escape", then a ten-second visible countdown.
Modal UI pauses that clock; early relic Continue preserves the remainder.
An unclaimed relic is delivered before countdown, and its drop uses the boss's
actual world position. Uncollected ordinary pickups are left in the outgoing
arena. See `Docs/Design/2026-09-05-escape-window-fix.md` and `EscapeWindowTests.cs`.

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
  Authored bone surfaces remain in `Art/Hydra/HydraDetails.png`; installation
  uses the sprite's actual bounds so source-resolution changes preserve layout.
- Court: matching `MonochromeContent.cs`, `MonochromeEncounterRules.cs`,
  `MonochromeRuntimeRules.cs`, `VoidFallGameRuntime.Monochrome.cs`. It owns a
  five-enemy chess roster. Two simultaneous Grandmasters share health; floor
  warning/burning phases alternate safe colors.
  Black Rule/White Rule cycles show a screen-relative split field: white armies
  enter the black left half and black armies the white right half. Other cycles
  retain their existing presentation. `Resources/VoidFall/CourtTile.shader`
  draws monochrome hazard borders and bounded brightness pulses; reduced motion
  holds the pulse steady without changing danger state or timing.
- Null City: `Content/NullCityContent.cs`, `Core/NullCityRules.cs`, runtime
  `VoidFallGameRuntime.NullCity.cs` and `.NullCity.Render.cs`. Twelve robot types
  share existing combat pools. The fixed city floor has Surveillance/Lockdown,
  purge lanes and hangar police; Motherload owns a permanent-lockdown encounter.
  Deferred birth/blast queues preserve slot reuse. Its death clears hostiles while
  retaining native boss dissolution and reward/relic flow. Space or controller
  left shoulder dashes only in this arena and resists the warned tractor cone.
- Eon Sea: `Content/EonSeaContent.cs`, `Core/EonSeaTerrain.cs`, runtime
  `.EonSea.cs` / `.EonSea.Render.cs`. Streamed world-space glaciers provide
  cover, autonomous melting and explosion-accelerated stress. Collapse applies
  non-stacking50%slow for20seconds; slippery patches preserve player momentum.
  `GameSim` uses cached optional projectile-cover hooks. Rewards clear frost
  and stop new melting/pulses. It uses shared enemies and a random boss, normal
  camera follow and native travel. Its plate owns `EonSeaVisualAsset`; views
  detach before arena-package release. `Tools/EonSea/export-eon.cjs` exports
  approved art; `Editor/EonSeaContentBaker.cs` imports/bakes/registers terrain
  and68shared/elite forms. `BakeBatch` scopes content work; `BuildValidationPlayer`
  writes `../Builds/EonSeaValidation/`. Diagnostic `-vfeonsea=terrain|frost|late|elites|boss`
  with `-vfcapture=<path>` uses an isolated adjacent profile.

- Crascendo: `Content/CrascendoContent.cs`, `Core/CrascendoRules.cs`, runtime
  `.Crascendo.cs` / `.Crascendo.Render.cs`. Standard shared tiers and random boss;
  positive hits add20%spawn radius, capped5x, to normal enemies, elites and bosses.
  Spawn-ID/telemetry-ID sidecars preserve struct/hash contracts; giant deaths
  push survivors without damage or additional growth. Native harvester growth
  accumulates through GameSim's optional natural-radius hook. Wider queries are
  enabled only during this arena. Ground progresses Indigo/Amber through
  Violet/Coral to Crying Violet using local survival time; boss/rewards hold
  maximum while animated tears continue under native pause ownership. The plate
  owns `CrascendoVisualAsset`; presentation detaches before package release.
  `Tools/Crascendo/export-crascendo.cjs` and `Editor/CrascendoContentBaker.cs`
  own authoring/import. Diagnostic `-vfcrascendo=early|mid|late|growth|boss`
  uses an isolated adjacent profile and `Builds/CrascendoValidation/` player.

**Do not equate route nodes with prepared arenas.** The historical prototype graph had ten
nodes but only five implemented objectives/packages: Abyss, Red Nebula, White
Sakura, Hydra and Monochrome Court. Normal runs now avoid those unfinished
destinations and resolve `HasEscaped` into a saved result and Home. The final
Overseer/cutscene remains future content. Null City uses stable route ID
`null-city`, a prepared package, survival/Motherload objective and playable-route metadata.

**Owner clarification:** the approved identities are Abyss, White Sakura, Red
Nebula, Monochrome Court, Hydra, Eon Sea, Crascendo and Null City (also referred to
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
RNG. Stacking preserves the layout and retriggers a victory lap. Healthy overclock
music remains 2x; critical health multiplies that rate by its drag. The current
soundtrack is retained. Existing high-contrast,
reduced-motion and UI pause ownership remain runtime settings concerns.

`UI/Core/IGameBridge.cs` exposes settings snapshots, restore, persistence,
live application and record reads. The nested `RuntimeGameBridge` in
`VoidFallGameRuntime.cs` implements it. Active controllers include
`UI/Core/SettingsController.cs`, `UI/Views/WorkshopController.cs` and
`RecordsController.cs`. Settings writes are debounced; callers must preserve
rollback on persistence failure. Workshop purchases affect profile ranks;
run upgrades are a separate state and lifecycle.

Workshop keeps its existing eight tracks: Integrity, Power, Mobility, Recovery,
Magnet, Precision, Arsenal and Revival Protocol. `WorkshopController` owns
costs, rank projection and purchase/refund logic; `WorkshopView` presents them.
Runtime `.UI.cs` handles purchases and preview selection. `StartRunInternal`
reloads profile ranks through `RefreshWorkshopCosmeticRanks` in `.Cosmetics.cs`.
`Runtime/Rendering/PlayerCosmetics.cs` supplies shared artwork and placement;
`PlayerFramePreview.cs` renders the UI preview and `.Cosmetics.cs` renders the
in-game decorations. Retain the existing Workshop design and artwork; the
exploratory browser alternatives are not approved replacements.

**Cosmetic sizing invariant:** the sprite factory normalizes cosmetic canvases
to one world unit. In-game renderers restore design-pixel dimensions using
`sprite.pixelsPerUnit`, then apply the preview-to-game scale `74 / 94`.
Mobility renderers need an assigned trail sprite and must size its actual
bounds to the preview's width and animated length. Applying `74 / 94` alone
shrinks decorations beneath the eye. `Tests/PlayMode/WorkshopCosmeticsIntegrationTests.cs`
covers purchased ranks, preview/world dimensions, trails, refunds and hiding.
Its native rendering test requires a graphics device (omit `-nographics`) and
writes origin/upgraded captures to `Logs/WorkshopCosmetics/` using an isolated
test profile. `Tests/Editor/WorkshopControllerTests.cs` covers transactions.

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
  `MusicReactiveState.cs` composes independent rate, tone and stereo effects.
  `MusicRemixEnvelope.cs` owns collected-gem buildup, the 25-second Magnet tail,
  collection release, sustained-danger recovery and stack accents. Runtime
  `.Audio.cs` owns Magnet pickup-slot tags; `.Sim.cs` counts actual tagged gem
  collections, preserving Greed and combat state. Main-file updates feed health,
  pause and pending gems. Critical enters at 20% and clears above 25%.
  `MusicDspFilter.cs` hands targets to the allocation-free `MusicSampleProcessor.cs`
  for bass, stereo, damage backspin and playback-rate-scaled bomb echo. Preserve
  audio-thread ownership and lock-free handoff. Track Shift retains event tails
  and uses measured `MusicTrackEntries.cs` offsets; new runs retain track intros.
  Tracks live in `Resources/VoidFall/Music/`; credits are in `Docs/AudioCredits.md`.
  Magnet green stays on `MusicPerimeterGraphic`/shader edges and fades with audio.
  See `Docs/Design/2026-09-06-MusicRemix.md`; focused tests are `MusicRemixTests`,
  `MusicDspRemixTests`, `MusicTrackEntryTests`, and `MusicRemixIntegrationTests`.
  `Editor/MusicRemixValidation.BuildPlayer` writes `../Builds/MusicRemix/`;
  `CapturePerimeter` renders synthetic component fixtures in `Logs/MusicRemix/Captures/`.
- `Runtime/Gameplay/VoidFallGameRuntime.Render.cs`, `.Fx.cs`, `.Arena.cs`:
  view synchronization, effects and arena presentation. Shared render material
  ownership is in `Runtime/Rendering/VoidFallRenderMaterials.cs`.
  Red Nebula gas uses a subdivided continuous ribbon in `.Arena.cs` and
  `Resources/VoidFall/FilamentGas.shader` / `NebulaGas.hlsl`; other arenas retain
  the layered filament path. Flow is cosmetic and freezes with reduced motion.
  `.NebulaArt.cs` owns runtime slices of the approved `Resources/VoidFall/NebulaMeteors.png`
  sheet. Physical Nebula rocks opt into `GameSim` orbit state; visual and collision
  radii shrink together for ordinary rocks. `.NebulaStrikes.cs` owns warned groups
  of 3–4 heads at 625 units/s, swept collision and per-instance hit tracking.
  `.VideoSettings.cs` owns the mild camera grade; Sakura keeps its existing palette.
- `Runtime/Gameplay/ArenaRecipeAsset.cs`, `ArenaPlateAsset.cs` and
  `ProceduralSpriteCatalog.cs`: prepared asset contracts. `ArenaResidencyManager.cs`
  owns Addressables handles; `Core/ArenaResidencyPlanner.cs` bounds residency.
  `Core/ArenaCatalogRules.cs` maps identities/package addresses.
- `Editor/ArenaContentBaker.cs`, `ProceduralSpriteBaker.cs`,
  `ArenaAddressableMigration.cs`: authoring and registration. The
  `PreparedContentBuildGate` in `Editor/PreparedContentBuildSetup.cs` rejects
  missing/invalid declared content. Assets live under `Generated/` and
  `Assets/AddressableAssetsData/`; URP configuration is in `Rendering/URP/`.
  Procedural snapshot baking retains CPU pixels. Unreadable GPU fallback is
  rejected under a null graphics device. `ProceduralSpriteBaker.RepairCorruptedSpritesBatch`
  regenerates uniform RGBA-205 readback failures through their original authoring
  entries, preserving asset paths and GUIDs.
- `Tools/NullCity/` exports approved artwork offline into `Art/NullCity/`.
  `Editor/NullCityContentBaker.cs` validates every frame, crop, PPU and FullRect
  bound. `NullCityVisualAsset` is referenced by `ArenaPlateAsset`, so Addressables
  owns the extra sprites with the plate. `BakeAndRegisterBatch` updates only the
  city package. Menu residency holds seven packages; gameplay remains current plus
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
Capture runs use a fixed seed and a profile adjacent to the output, resolved
before the first save load. `-vfnebula-legacy` with `-vfcapture` renders the previous
Red Nebula ribbons for comparison. `NebulaVisualValidation.BuildPlayer` writes
to `../Builds/VisualRemaster/` without replacing the normal player.
`NebulaVisualValidation.BuildDeliveryPlayer` writes `../Builds/VisualDelivery/`.
`-vfvisual-check=<directory>` stages meteor, lane-wave, and boss/Overclock captures
via `VisualDeliveryProbe`, with its own profile selected before initial loading.
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
