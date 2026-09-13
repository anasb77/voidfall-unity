# VoidFall repository map

Derived from the September 4, 2026 audit; update relevant sections as systems
change. This is a navigation aid, not a claim that every planned feature is
implemented. All paths below are relative to **`Assets/VoidFall/`**, except
paths explicitly starting with `Assets/`, `Docs/`, `Packages/` or `.github/`.

Canonical source: this `voidfall-unity` project. Canonical player:
`../Builds/VoidFall.exe`; launch instructions: `../START_HERE.md`. Sibling source
worktrees and `../Builds/Archive/` are recovery references. Source provenance is
recorded in `Docs/Design/2026-09-12-Consolidation.md`.

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
| Automatic run history / balance data | `Runtime/Telemetry/RunTelemetry.cs`; `Runtime/Gameplay/VoidFallGameRuntime.Telemetry.cs`, `.Persist.cs`; schema and extension contract in `Docs/RunExports.md` |
| Loot reachability / full pickup pools | `Runtime/Gameplay/VoidFallGameRuntime.Loot.cs`, `.Sim.cs`; generation-safe pickup iteration in `GameSim.cs`; `Tests/PlayMode/LootReachabilityTests.cs` |
| Simulation ordering / state reset | `Runtime/Gameplay/VoidFallGameRuntime.cs`: `Simulate`, `StartRunInternal`; `GameSim.cs`, `FxSim.cs` in the same directory |
| Movement / device polling | `Runtime/Input/InputReader.cs`; `Runtime/Gameplay/VoidFallGameRuntime.Sim.cs`: `MovePlayer` |
| Weapons / targeting / damage / pickups | `Runtime/Gameplay/VoidFallGameRuntime.Sim.cs`: `UpdateWeapons`, `FireWeapon`, `UpdateBlades`, `UpdateBullets`, `UpdatePickups`; `Core/CombatRules.cs`, `PickupRules.cs`, `BalanceRules.cs` |
| Enemies / spawn pressure / ordinary bosses | `Content/DirectorRules.cs`, `EnemyRosterRules.cs`, `EliteRules.cs`, `FormationRules.cs`; `Runtime/Gameplay/VoidFallGameRuntime.Sim.cs`: spawning, attacks and deaths |
| Upgrades / evolutions / support effects | `Content/UpgradeRules.cs`, `EvolutionRules.cs`, `SupportEffectRules.cs`, `ExtendedCatalog.cs`; `Core/ProgressionRules.cs`; runtime `RecalculatePlayerStats`, `RollLevelOptions`, `SelectLevelOption` |
| Roulette / reward ceremony | `Content/RouletteRules.cs`, `RoulettePresentationRules.cs`; `Runtime/Gameplay/VoidFallGameRuntime.Roulette.cs`, `.RouletteClaims.cs`, `.RouletteChest.cs`; `UI/Views/RouletteView.cs`, `RouletteWheelGraphic.cs`, `LevelUpView.cs` |
| Wild Cards / overclock / mutations | `Content/WildCardRules.cs`, `Core/OverclockRules.cs`, `MutationRules.cs`; `Runtime/Gameplay/VoidFallGameRuntime.WildCards.cs` and `.Sim.cs` |
| Meteors / arena hazards | `Core/MeteorRules.cs`, `HazardRules.cs`; `Runtime/Gameplay/GameSim.cs`, `VoidFallGameRuntime.NebulaStrikes.cs` |
| Player cosmetics / Workshop preview | `Runtime/Rendering/PlayerCosmetics.cs`, `PlayerFramePreview.cs`; `Runtime/Gameplay/VoidFallGameRuntime.Cosmetics.cs`; `UI/Views/WorkshopView.cs` |

Roulette relics emerge at the last defeated boss, ignore magnets, and require
physical pickup; the safe Rewards stage allows movement and waits for the relic.
`RoulettePresentationRules` projects the final probabilities, including the single
first/repeat protection re-sample, into segment sizes and readable reward facts.
The 6.8-second spin opens the mandatory reward-claim sequence. Runtime
`.RouletteClaims.cs` resolves one target, presents one card per actual rank, and
commits each reward only on Claim. Generation/index guards prevent duplicate
grants. Pause and the escape countdown remain held until the final claim.
`LevelUpView.ShowReward` uses the actual upgrade menu and its existing `BuildCard`
renderer for reward names, benefits and ranks. Click ordinary cards to take them;
Wild Cards show rule changes and explicit Take/Leave buttons. Leaving grants
nothing and does not reroll. The pending flag retains pause ownership while
`SyncUiScreen` routes it to `UIScreen.LevelUp`; normal pending upgrades are restored
afterward. The legacy `RareBoon` ID now pays 500 Parts without healing or score.
Rank caps and no-eligible-card fallbacks remain; a full pickup pool yields a
claimable 40 Parts fallback instead of losing a power-up. Stakes settle once at
landing; `roulette_claimed` records each committed outcome and `roulette_granted`
is the final aggregate. Declines have a separate event and no grant. See
`Docs/Design/2026-09-08-RewardMenuCorrection.md` for the current presentation contract.
`RouletteView` centers the wheel with a 1.5x target capped to fit the viewport;
the reduced spin button sits in its hub. Rewards/odds live in a dismissible drawer
that blocks the underlying actions and supports controller cancel. Idle decoration
text is removed; purchase/refund feedback and selected reward details remain.
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
`ProceduralSpriteFactory.Arsenal.cs`. Clock's face uses 18% opacity; its moving
hands retain their previous 50% opacity and authored shape. Rank III adds a
seconds attack hand with half the main hand's reach, width and damage, rotating
twice as fast. Evolution retains its full-size counterclockwise hand.
Boomerang visuals and hit radius use half their original size.
Mine range guides retain 70% of their original opacity. Idle summon creation
stops at squad size; existing returning summons persist within the active cap.
Mine placement uses rank delays 2.4–1.8s with a 0.9s minimum after recovery.
Mine freeze lasts 1.2s with 1.2s mobile recovery before another freeze; it keeps
damage-reception timers advancing and bosses resist freeze. Control state is
spawn-identity keyed and clears on new runs and travel.
Upgrade offers, roulette acquisition, HUD and records share the extended weapon
order. Evolution support lookup must use `ExtendedCatalog.AllSupports()`.
`Core/ProgressionRules` owns four initial weapon slots, expanding to five after
two rank-VI weapons; `Content/UpgradeRules` reuses this authority. Runs still
start with the Pistol. Exported build snapshots include the current slot limit.
Their paired extended supports are also included in the build HUD. Artwork is
warmed during stat recalculation/upgrade commit, with allocation-free cache keys.
Use `Editor/BuildScript.BuildWindows` for the current `../Builds/VoidFall.exe`.
The opt-in `-vfarsenal=all|mines|summons|clock|boomerang` starts a test loadout
(`-vfarsenal-rank=1..6`, `-vfarsenal-evolved=1`); `-vfarsenal-check=<folder>`
captures rank I, VI, evolved, rank-III Clock and idle-summon states. Both isolate the profile
before the first load. `Tests/PlayMode/ArsenalIntegrationTests.cs` covers these
combat and presentation boundaries.

`VoidFallGameRuntime.OrbitalDefense.cs` combines projectile speed with full
weapon recovery for blade/clock rotation (Overclock counted once), including
Hollow Blade travel. It intercepts shots through synchronized swept contact
with the actual orbit blades, launched hollow blade and clock hands, before
player impact. `GameSim.HostileShotBlockable` stores origin metadata outside
the hashed shot struct; every insertion clears it. Scoped enemy-controller
emission marks only ordinary enemy shots; boss, elite, meteor and unknown
sources remain protected. Standard expiry handles counters and view cleanup.
`OrbitalDefenseIntegrationTests.cs` covers provenance, near misses and reuse.

The live support list has 13 cards. Scholar combines XP and power-up drop
bonuses; Velocity Coils combines projectile/orbit speed and camera dezoom.
`ExtendedCatalog.CanonicalSupportId` maps retired fortune/spatialAwareness IDs
to scholar/projectileSpeed. Save sanitation merges legacy record ranks by
maximum (not addition), with the survivor's cap; extras must be resolved by ID.
Camera zoom recalculates on both level-up and roulette upgrade paths.

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
produce six visits per path across split/reconnect layouts. Nine route nodes
reuse one arena only across mutually exclusive branches; no legal path repeats
an arena. `VoidRouteNode.ArenaId` and `VoidRouteRun.CurrentArenaId` keep arena
identity separate from internal node IDs (duplicate nodes use an `@` suffix).
The minimal Tab overview is `UI/Views/RouteMapView.cs`; clicks highlight
`PlannedPathThrough`, while physical portals commit choices.
`Runtime/Gameplay/VoidFallGameRuntime.Journey.cs` owns reward/junction/travel
stages, map pause ownership, the safe portal room, load retry and terminal
return to Home. `VoidFallGameRuntime.LevelUps.cs` advances upgrade prompts in
both combat and safe reward phases. `.RouletteClaims.cs` owns pending rewards
through the shared upgrade menu until taken (or a Wild Card is left).
`SyncUiScreen` must preserve that ownership.

Escape timing lives in Journey/Rift and `.Escape.cs`: fifteen active seconds
with normal movement, staggered harmless enemy deaths, animated `Escaping...`
dots and three increasing shake patterns. At eleven seconds, remaining XP and
Parts sweep toward the player; departure settles them through the normal grant
path and drains queued level choices. Overclock time remains held until combat
resumes. Modal UI pauses the window. The relic stays at the boss's actual death
site and is delivered if unclaimed; roulette holds the window until all reward
cards are resolved. See `Docs/Design/2026-09-06-JourneyPolish.md` and
`EscapePolishTests.cs` / `EscapeWindowTests.cs`.

The map uses small arena thumbnails from `Resources/VoidFall/RouteThumbnails/`;
it does not load all full arena packages. `Editor/RouteMapThumbnailBaker.cs`
authors those previews from prepared plates. `Editor/JourneyVisualBaker.cs`
also neutralizes the existing portal sheet into `Resources/VoidFall/Portals/Neutral/`,
so portals can use each destination's native StarTint. Portals show names only.

`ArenaTransitionGraphic` is updated from the shared render path, including
custom Eon Sea/Crascendo rendering, so the fullscreen fold is retired on arrival.
Its horizons are tessellated as adjacent strips rather than a crossing fan.

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
  `Runtime/Gameplay/HydraRuntimeRules.cs`, `.Hydra.cs` and `.HydraTravel.cs`.
  HydraI360s survival is base-only with downward glyph drift; `.MapPresentation`
  owns its rendering/POV. HydraTravel reuses Rift collapse/swap/settle inside
  one route visit without resetting objectives/pressure. HydraII retains the
  original bone surface, boss/art/geometry/health and solo-boss suppression.
  `Core/HydraPopulationRules.cs`, runtime `.HydraPopulation.cs` and
  `ProceduralSpriteFactory.HydraPopulation.cs` own five hybrids and five Viruses.
  Spawn-ID sidecars hold behavior; bounded deferred offspring retain reward
  roots and cancel on transition. Reclaimer uses real harvested XP; repairs,
  shields, split, warnings and actual outcomes use the existing exporter.
- Court: `MonochromeContent`, `MonochromeEncounterRules`, `MonochromeRuntimeRules`,
  `.Monochrome.cs` and `.CourtField.cs`. One fixed28×28 board of129.6-unit tiles,
  clamped playable bounds, seeded spaced sentinel/fallen rooks. Sentinels use
  state90 on pooled court-rook,100k–150kHP,stationary contact damage, no shots.
  Native player weapons target them; death queues5–6 roster-one chasers and
  one requested notice. Existing Grandmasters share HP and each warns/fires
  one volley. Local cell scope freezes per5s phase, arms over2s, bursts at3.4s,
  alternates colors and damages all warned actors. `CourtTile.shader` owns
  red warning/burst and six-segment reticle; per-tile property blocks are
  initialized during setup, never in MonoBehaviour field constructors.
- Null City: `NullCityContent`, `Core/NullCityRules`, `.NullCity.cs`,
  `.NullCity.Render.cs` and `.NullCity.MapPresentation.cs`. Authored1600×900
  coordinates map to6400×3600world; World/Canvas helpers are reciprocal. Its
  original artwork/roster/nativeboss remain. Followcamera and rendering-only
  Zack scale live in shared `.MapPresentation`; local energized-road shake
  preserves collision and purge timing. The original LCD anchor has a cached
  world-space text overlay for welcome/lockdown state. Native projectile,
  tractor,bomb,purge and XP clamps convert world radii before authored tests.
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
  delegates to the canonical `../Builds/VoidFall.exe`. Diagnostic `-vfeonsea=terrain|frost|late|elites|boss`
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
  uses an isolated adjacent profile and the canonical `../Builds/VoidFall.exe` player.

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

Workshop lists Default, Dasher and Brute through `WorkshopController.BuildForms`
and `WorkshopView.PopulateForms`, including current base stats, starter and unlock
hints. `UICallbacks.SelectForm` routes to runtime `.Forms.cs`; both Workshop and
Home selection use the same guarded immediate save with rollback on failure.
Locked forms remain visible but cannot be selected, and active runs cannot be
changed through the callback. Existing shared ranks and eye artwork are retained.
Run-export context records the committed `formId` and `startingWeaponId`.
`WorkshopControllerTests` and `WorkshopCosmeticsIntegrationTests` cover these
selection/persistence boundaries and navigation focus. The editor-only
`WorkshopFormsCapture.Capture` renders locked/selected layouts with prepared
artwork to `Logs/WorkshopForms`, without reading or writing a real profile.

### Dealer crossing and manual legendaries

`Content/DealerRules.cs` owns fixed 100-Scrap transactions, three distinct offers,
fragment masks and late-stock fallbacks. `Content/LegendaryRules.cs` owns the
manual weapon timing/ranks and stable attribution IDs. Runtime `.Dealer.cs`
extends the existing Journey junction (including single exits) with fixed stock,
upper/lower placement, E/controller interaction, modal ownership and atomic
fragment saving. Closing/reopening never regenerates the session. One offer
can be purchased per crossing; ordinary spending uses the run wallet.

`UI/Views/DealerView.cs` provides cards/icons/puzzle art and prices;
`DealerPortraitView.cs` renders CSS-sized glyph quads with the preview's fixed
1.12 line spacing, small-font hinting, feature colors and individual hair-strand
offsets; it must not inherit UITheme's general text scaling. `DealerRoomView.cs`
uses the preview's 1200x760 reference coordinates on a screen-space stage,
unaffected by combat camera zoom or post effects. Native interaction coordinates
map preview pixels through `(x-600, 380-y)`. Existing destination sprites and Zack's current
Workshop artwork are presented over the shared floor; the route graph still
owns actual destination names and colors.
The room omits the heading and movement-help copy. A separate platform frame
leaves space beyond the selected upper/lower edge for the hovering face, with
an interaction anchor reachable from the legal walking area. The portrait has
no platform shadow. `DealerPortraitView.AnimationSpeed` scales head/gaze,
breathing, hair and room hover cycles by 1.3; reduced-motion settings still apply.
The platform's scale/offset is presentation-only: its portals, labels, player
art and browse prompt share that frame. Keep native interaction anchors within
reach of the walking bounds when changing its framing. Approved references and
later owner corrections are distinguished in
`Docs/Design/2026-09-12-DealerPreviewFidelity.md`.

`Resources/VoidFall/Dealer/` is exported by `Tools/Dealer/export-art.mjs` and
`Tools/Dealer/export-room.mjs` from approved references. The importer preserves
transparent NPOT art; the room textures retain their authored resolution.
The room canvas preserves gamma-space vertex colors to avoid dark-color
quantization. Mist rings are precomposed in sRGB because linear alpha blending
made the browser's faint glows too bright. The room portrait and text chrome
hide beneath the shop; do not let a second face show through its backdrop.

Runtime `.Legendaries.cs` owns a separate manual slot, keyed hit cooldowns,
held-input cancellation, waveform/rifle rendering and shared damage calls.
Sound Blade follows mouse/right-stick aim; Charged Rifle rotates independently
and fires on release, with a 3-second overload cutoff. The waveform is cosmetic.
Three distinct saved fragments assemble/equip a weapon; later run equipment and
three-tier upgrades are dealer offers. Normal arsenal slots remain unchanged.
`SaveData.soundBladeFragments` / `chargedRifleFragments` are compatible bit masks;
existing currency keys remain `parts`. Manual damage IDs are retained in run
records and the existing exporter; see `Docs/RunExports.md`.

`DealerRulesTests`, `LegendaryRulesTests` and `DealerIntegrationTests` cover
transactions, persistence, cooldown reuse, input guards, damage and exports.
`-vfdealer-check=<absolute-folder>` runs the rendered `DealerIntegrationProbe`
with a profile selected before the first load. It writes crossing/shop/weapon
captures and a success/failure file. No fake preview income controls ship.
Its `room-browse` capture exercises the lower dealer from the legal platform
edge before purchase; inspect upper/lower framing and prompt clearance too.
Roulette fragments and the three alternative legendary candidates are deferred.

`UI/Core/UIManager.cs` defines `UIScreen`, `UICallbacks` and `UIManager`.
The runtime supplies callbacks at startup; its `.UI.cs` partial's
`SyncUiScreen` selects the visible screen from runtime flags. Views must not
maintain a competing navigation state machine. Menus and overlays use uGUI;
the gameplay HUD has separate canvas ownership.

`UI/Core/UIBuilder.cs` contains construction helpers **and `UIViewBase`**.
Views build once via `Initialize`, reuse their hierarchy and toggle visibility
through a `CanvasGroup`. Look in `UI/Views/` for the screen being changed.
`CreateProfilePanel` (Workshop/Records/Settings shell) returns the padded
content area below its header and outputs the panel itself through
`panelRoot`; `UIBuilder.FitPanel` must be given that root — the root is
centre-anchored while the content area is stretch-anchored, so a sizeDelta on
the content inflates it over the header instead of resizing. FitPanel clamps
against the 1600x900 reference units (the canvas scaler matches height), not
raw screen pixels.
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

Player forms (spec §05) live in `Content/PlayerForms.cs`: engine-free
definitions, stats and unlock gates. The runtime partial `.Forms.cs` owns menu
selection cycling, unlock evaluation and void-clear recording; the gates are
the spec's proposals [P] (first guardian defeat → Dasher, three distinct Void
clears across runs → Brute) and evaluate on void completion and at terminal
save. `StartRunInternal` resolves the selected form's starter weapon (Dasher:
Arc Lash, Brute: Seeker Launcher; the default keeps the Operative's pistol)
and `RecalculatePlayerStats` applies the form's base health and movement
factor exactly once alongside the normal buffs — the default form defers to
`ContentCatalog.Operative`, so legacy profiles and the golden-master path are
unchanged. Form selection, unlocks and distinct-Void clears persist in
`SaveData` (`form`, `unlockedForms`, `voidsCleared`) separately from the
shared Workshop ranks; the main-menu form selector cycles unlocked forms only,
and the run result records the form and starter. Form silhouettes and mastery
benefits are intentionally deferred visual/balance work.

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
`Application.persistentDataPath`; schema v6 intentionally retains the filename
`voidfall_save_v4.json`. The currency's display name is **Scraps**; its
serialized keys (`SaveData.parts`, `LifetimeStats.totalPartsEarned`, browser
adapter `parts`/`totalPartsEarned`) intentionally keep the historical `parts`
names for save compatibility — rename display text, not schema. Writes use
temp + flush + atomic replacement + backup.
Recovery prioritizes the current backup over legacy profiles and protects it
across failed writes. `BrowserSaveImporter.cs` / `BrowserSaveExporter.cs` are
compatibility adapters, not a reason to reopen the deprecated browser project.

`Runtime/Gameplay/VoidFallGameRuntime.Pressure.cs` observes scaled survival and
complete boss health high-water per visit, then freezes the exact score once.
`.DirectorMenu.cs` owns first-completed-run selection and result acknowledgement;
`UI/Views/DirectorSelectionView.cs` projects its callbacks. The live pressure label
belongs beneath `.Hud.cs`'s timer. Result/save/telemetry share `FrozenRunScore`.
Schema v6 adds director choice/onboarding and versioned 64-bit score facts;
legacy records keep their legacy score/version. Historical protocol refunds
remain scoped to pre-v5 saves. Browser adapters preserve the new fields and
integer precision; local rankings select final score only for versioned runs.

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
  for bass, stereo and damage backspin. Bombs duck without triggering echoes.
  Magnet keeps the filter open with gentler bass/stereo targets. Preserve
  audio-thread ownership and lock-free handoff. Track Shift retains event tails
  and uses measured `MusicTrackEntries.cs` offsets; new runs retain track intros.
  Gameplay tracks loop natively in full. Only Track Shift changes the song during
  a run; its curated entry is used on the first pass, then loops return to zero.
  Unexpected stopped playback recovers the same song, preserving startup and
  focus guards. New-run selection and menu behavior are retained.
  Stopped-playback recovery follows observation and a grace, not `loadState`:
  streamed clips become Unloaded at their natural end. The real-stream regression
  covers all gameplay tracks looping; see `Docs/Design/2026-09-08-RewardMenuCorrection.md`.
  Tracks live in `Resources/VoidFall/Music/`; credits are in `Docs/AudioCredits.md`.
  Magnet green stays on `MusicPerimeterGraphic`/shader edges and fades with audio.
  See `Docs/Design/2026-09-06-MusicRemix.md`; focused tests are `MusicRemixTests`,
  `MusicDspRemixTests`, `MusicTrackEntryTests`, and `MusicRemixIntegrationTests`.
  `Editor/MusicRemixValidation.BuildPlayer` delegates to the canonical Windows build;
  `CapturePerimeter` renders synthetic component fixtures in `Logs/MusicRemix/Captures/`.
  The approved simplification is in `Docs/Design/2026-09-06-MusicRouletteRevision.md`;
  `Editor/RoulettePreviewCapture.BuildRevision` delegates to the same build.
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
  Monochrome's detail plate must use a two-triangle Full Rect mesh; the baker
  enforces it and the build gate rejects oversized geometry. Use
  `ArenaContentBaker.ReimportMonochromeDetailMesh` to repair its import without
  regenerating the artwork.
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

## Director I sustained combat (version 2)

Current duration is **360 seconds** for all eight voids (`Core/VoidProgressionRules`).
`LocalDirectorSurvivalSeconds` reports actual seconds; beat cutoff330 and
lead-in/incident cutoff345 follow remaining time. The arrival ramp still reaches
full strength at300. `DurationAdjustedDifficultySeconds` preserves the old boss
HP/tier clock by removing only added survival time; boss combat time still counts.
Pressure retains canonical300+60 credits and80/20 weights. Diagnostics use the
new360+60 raw stage model. `SixMinuteSurvivalIntegrationTests` and
`VoidObjectiveTrackerTests` cover this split; do not reintroduce hardcoded
five-minute completion calls. See the loot/six-minute design document.

`Runtime/Gameplay/VoidFallGameRuntime.DirectorI.cs` owns I-only continuous
arrivals, seeded Pursuit/Flank/Hunt/Breakthrough beats, temporary damage relief
and committed-attack reservations. `Core/EncounterPacingRules.cs` reuses the
existing clock via `BeginSustained`; survivors keep their native pursuit AI.
`Core/SimulationRules.MaxActiveEnemies` is the 750-slot pool authority, aliased
by `Content/DirectorRules`. The I runtime ceiling is750; its authored arrival
target is a softer pacing control, not a mandatory population/refill order.
Legacy profile64/128/192 bands, finite boss waves and old formation diagnostics
remain for II/III; I does not use those old bands, overlays or age retirements.

`GameSim.EnemyCanCommitAttack` is queried only at initial attack commitment;
`.RosterProgression.cs`, `.NullCity.cs` and `.Monochrome.cs` share this I policy.
Denied actors retain movement. Reservations use SpawnId plus unfinished states,
queued city shots and live shot provenance; death, slot reuse and freezing must
not erase an outstanding threat. Boss attacks retain their own controllers;
ordinary special admissions reduce during bosses/recovery/incidents.
`_pressureReliefTimer` advances once in I's simulation path, not its bypassed
legacy director. Do not introduce permanent health-based recovery or DPS matching.

`DirectorISustainedTests`, legacy `EncounterDirectorIntegrationTests` and
`DirectorCapacityGridTests` cover these contracts. Existing `StressBenchmarkProbe`
supports `-vfhold750` (checks every post-refill fixed-step boundary) and
`-vfscenario=directorI` (normal pacing, fresh profile, scripted pickup/avoidance
input, real health and real offered upgrades). These are diagnostic datasets.
`-vfscreenshot=<path>` captures after warmup in a rendered player. Read actual
occupancy, advancing combat, GC/frame data and exports; a configured ceiling
alone is not capacity evidence. Map dimensions and health normalization are
separate owner priorities. See `Docs/Design/2026-09-08-DirectorI-SustainedCombat.md`.

## Run exports and instrumentation ownership

Loot policy v2 (`.Loot.cs`) keeps the281-slot pool bounded:256 primary XP
positions,24 special-reserve positions and one final XP-only overflow slot.
Overflow XP merges locally or relocates the selected distant gem to the drop;
it must not add new XP to a faraway stationary pile. Specials can reclaim a
slot by conserving and consolidating XP/Parts, then duplicate power-up charges.
Power-up `Value` is a charge count; collection uses one charge and restores its
remainder deterministically. `GameSim` snapshots pickup generations so callback
compaction cannot process newborn rewards in the current tick. Distant loot is
returned into view on a bounded cadence, remains physically collectable, respects
Greed and existing Null City bounds. Roulette preview checks reclaimable room
without mutating the pool before Claim. No physical map resize was made.

`Runtime/Telemetry/RunTelemetry.cs` owns the existing schema-4 JSON summary and
bounded asynchronous JSONL journal. `Runtime/Gameplay/VoidFallGameRuntime.Telemetry.cs`
owns run identity/context, event helpers, one-second combat/sample observations,
thirty-second summary checkpoints, and finalization. `.Persist.cs` retains the
silent manual export entry point; gameplay terminal export is independent of
profile-save success. Main runtime startup distinguishes menu initialization
from actual play, and finalizes before restart/menu resets and on quit/destruction.

`RunExports` lives beside the built executable (project root in Editor).
`Docs/RunExports.md` defines joins, units, loss reporting and verification.
Every new feature worth balancing must add/update collection in the same change;
keep this contract in sync with `AGENTS.md`. Hook committed outcomes rather than
re-sampling rules. Spawn/removal/pickup/damage hooks live in `.Sim.cs`; encounter
decisions in `.Encounters.cs`; offers in `.LevelUps.cs`; roulette uses `.Roulette.cs`
and the view's post-spin notification. Storage/runtime tests are
`Tests/PlayMode/RunExportStorageTests.cs` and `RunExportIntegrationTests.cs`.

## Verification entry points

For incident work, `.Incidents.cs` and `Core/MajorIncidentRules.cs` share a
230-unit Black Hole radius, 165-unit center offset and 65%-base-speed peak
player pull. `.Destroyers.cs` uses role entry distances and bounded raid HP
scaling from `Content/DestroyerContent.cs`; no arrival invulnerability is used.
`IncidentEngagementIntegrationTests` verifies actual movement escape, strong
loadout attack exposure and weak-loadout release cleanup. The isolated
`-vfarsenal=all -vfincident=black-hole|raid|eclipse` path enables player captures.
Mine control/cadence and fixed-seed boss fixtures are in
`MineBalanceIntegrationTests`. See both 2026-09-08 balance design notes.

The canonical Windows builder prefers DX11, with DX12 available for explicit
diagnostics. Focus-loss handling remains in the main runtime file; entry and
completion events plus graphics/display context support hang investigation.
The original hard hang remains unconfirmed; see
`Docs/Design/2026-09-08-FocusFreeze-Mitigation.md` for evidence limits.

Use the commands in `AGENTS.md`. `Tests/Editor/` has targeted rules, catalogue,
asset, controller and save tests. `Tests/PlayMode/RuntimeFlowRegressionTests.cs`
covers integration boundaries; `SimulationGoldenMasterTests.cs` pins a state
hash and `SimulationGoldenMasterSweepTests.cs` runs 32 seeds twice. Keep the
authoritative hash and its change explanation in the test, not this map.

`VoidFall.EditorTools.BuildScript.BuildWindows` is the sole Windows player build
implementation and writes `../Builds/VoidFall.exe`. Legacy validation, preview,
delivery and baseline build methods delegate to it; they do not create separate
players. Content baking, Addressables builds and capture entry points retain
their own responsibilities. `VOIDFALL_BUILD_OUTPUT` temporarily redirects that
same builder for validation, while `VOIDFALL_SOURCE_REVISION` stamps BUILD_INFO.
Promote only a verified payload to the canonical path and remove the temporary
staging copy; retain designated archives and live run exports.
`.github/workflows/ci.yml` runs Unity
tests only with `UNITY_TESTS_ENABLED` and credentials configured. Runtime
`StressBenchmarkProbe.cs` is opt-in via `-vfbench`; verify simulated progress,
not just wall time. `Runtime/Telemetry/RunTelemetry.cs` records run events and
exports diagnostics. Capture arguments are parsed by `ConfigureVisualCapture`
in `VoidFallGameRuntime.cs`; `UpdateVisualCapture` in `.Sim.cs` writes images.
Capture runs use a fixed seed and a profile adjacent to the output, resolved
before the first save load. `-vfnebula-legacy` with `-vfcapture` renders the previous
Red Nebula ribbons for comparison. `NebulaVisualValidation.BuildPlayer` and
`NebulaVisualValidation.BuildDeliveryPlayer` delegate to the canonical Windows build.
`-vfvisual-check=<directory>` stages meteor, lane-wave, and boss/Overclock captures
via `VisualDeliveryProbe`, with its own profile selected before initial loading.
`Runtime/RouteJourneyProbe.cs` adds map/junction captures and accelerated whole-route
checks (`-vfjourney=map|junction|check`, `-vfoutput=...`, optionally `-vfbranch=right`),
with a separate profile beside the output. `Tests/PlayMode/JourneyIntegrationTests.cs`
covers physical choices, map pause, junction safety, terminal saves and retry.
`Tests/PlayMode/NullCityIntegrationTests.cs` covers reset, roster, deferred deaths,
boss cleanup and dash bounds. `-vfnullcity=surveillance|lockdown|motherload|tractor`
with `-vfcapture=<path>` selects diagnostic poses using an isolated adjacent profile.
`NullCityContentBaker.BuildValidationPlayer` delegates to the canonical Windows build.

Roulette visual QA: `Editor/RoulettePreviewCapture.cs` renders the actual views
to `Logs/RoulettePreview` via `Capture`; `BuildPlayer` delegates to the canonical
Windows build.
`-vfjourney=roulette -vfoutput=<absolute-prefix>` exercises boss defeat, relic
preservation, the spin and reveal, writing captures with an isolated profile.
Use a rendering player (without `-batchmode`) for screenshots; headless runs can
validate flow but produce black captures. Physical proximity is covered by the
PlayMode tests; the diagnostic driver deliberately invokes the pickup callback.

`Editor/OverclockHudValidation.BuildPlayer` delegates to the canonical Windows
build. Launch it with `-vfoverclock-check=<absolute-folder>`
to capture ×1, ×3, low-charge and ×12 states with bosses. The diagnostic profile
is selected before the first save load; the probe checks live music analysis,
2x playback targets, stack sizing, boss clearance and activation pattern lifetime.

Read `Docs/Design/VoidFallArenaArchitecture.md` for arena design constraints,
`Docs/RefactoringPlaybook.md` for larger ownership changes, and
`Docs/AI/ReleaseReadiness-2026-09-04.md` for dated audit evidence and unresolved
release risks. They are task-specific references, not required bulk reading.
