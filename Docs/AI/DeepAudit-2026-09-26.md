# VoidFall deep audit — 26 September 2026

**Assessment:** fix the progression transactions, crossing flow, and validation gaps before adding more content. The game has substantial working foundations, especially deterministic simulation and bounded entity pools, but the current tests do not protect several interactions between otherwise tested systems.

This was an audit, not an implementation pass. Production fixes and balance changes were not applied. Evidence, isolated profiles, test sources, logs, captures, and diagnostic players are under `Logs/DeepAudit-2026-09-26/`. The canonical build in the outer `Builds/` directory was not replaced.

The report records **14 actionable findings**. Reproduced failures, measured performance, source-confirmed risks, and design questions are labeled separately. It does not certify that the game is bug-free.

## Scope and baseline

- Reviewed `AGENTS.md`, `Docs/REPO_MAP.md`, current journey/design contracts, subsystem implementations, callers, tests, package/build settings, and the dirty working tree as it existed at audit start.
- Source baseline: `afd885ef06e2e0acee41b5981db54cb2e1757b27` **plus the owner's existing uncommitted changes**. `baseline.patch`, `baseline-status.txt`, and `baseline-files.json` preserve that distinction; this is not an audit of the commit alone.
- Unity 6000.5.7f1; URP 17.5.0; Input System 1.20.0; Windows player using Direct3D 11.
- User-selected baseline PC: Intel i7-7700HQ, approximately 16 GB RAM, NVIDIA GTX 1060 with 6 GB VRAM; 1920×1080, 60 Hz display. The player used the NVIDIA adapter, not Intel HD 630. A steady **60 FPS / 16.7 ms** is the working comparison target, not a newly agreed minimum-system specification.
- Repository inventory: 370 first-party C# files, 105,277 lines including tests/generated code, 108 test-source files. This is a broad subsystem and integration audit; it is not a claim that every line received equal review or every possible playthrough was tested.

## Findings to act on

Priority means repair order: **P1** before more progression work; **P2** foundation, usability, or performance work; **P3** polish. Design questions below are separate from defects.

| ID | Priority | Finding | Evidence |
|---|---|---|---|
| F01 | P1 | Workshop purchases/refunds do not save wallet and ranks as one transaction | Reproduced through runtime callbacks and real isolated SaveStore |
| F02 | P1 | Single-exit crossings bypass the dealer | Reproduced through the actual completion/transition path |
| F03 | P2 | A newly unlocked form can partially commit a terminal run before a later save fails | Reproduced with failure injected between the two writes |
| F04 | P2 | Null City child-spawn admission bypasses the native population cap | Reproduced 50 → 53 bodies at the quiet-phase boundary |
| F05 | P2 | Sustained maximum-density combat misses 60 FPS on this PC | Visible 1080p player measurement |
| F06 | P2 | Enemy updates reconstruct identical callback delegates every simulation tick | Compiled IL and object-identity check |
| F07 | P2 | New map sprites bypass the arena residency lifetime | Source-confirmed retained Resources cache; no full-session memory plateau measured |
| F08 | P2 | Texture imports dominate a roughly 1.5 GB player build | Unity build report and importer settings |
| F09 | P2 | HUD readability is not stable across bright arena backgrounds | Inspected native game captures and HUD construction |
| F10 | P2 | Keyboard/controller navigation lacks a complete focus and binding policy | Missing menu focus reproduced; missing controller pause/map bindings confirmed in source |
| F11 | P2 | Test and diagnostic contracts have drifted from the current game | Complete suites, failing assertions, and packaged probes |
| F12 | P2 | Startup-only slow-frame logging continues throughout an early-started run | Source-confirmed lifetime error and native player logs beyond 100 seconds |
| F13 | P3 | Menu layouts lose spacing and containment | Native Records/Workshop captures and layout offsets |
| F14 | P2 | Run history serializes oversized events and loses records under dense load | 388/429 MB native stress exports; 444 dropped records in High |

### F01 — Workshop save transactions are broken

The UI copies `profile.parts` into a local variable, passes that local by reference to `WorkshopController`, and writes it back **after** the controller has saved the profile. The saved ranks are new, but the saved wallet is old.

Concrete isolated reproductions:

| Action | In memory after action | On disk after action |
|---|---|---|
| Start with 100 Scraps; buy Integrity rank 1 for 35 | 65 Scraps, rank 1 | **100 Scraps, rank 1** |
| Start with 65 Scraps and that rank; refund | 100 Scraps, rank 0 | **65 Scraps, rank 0** |
| Purchase when storage fails | **Rank 1 remains in the live profile despite failure** | Previous saved profile |
| Refund when storage fails | **Wallet/ranks change and success is presented** | Previous saved profile |

The failure rollback is also unsafe: `CommitSettings` sanitizes the profile, and `SanitizeWorkshop` replaces the workshop entries. The controller then rolls back its old, detached entry. Refund ignores the persistence result entirely. A later successful save can persist the incorrect live state; this is more serious than an out-of-date label.

**Repair:** construct one candidate profile with both wallet and rank changes, persist that candidate once, then install it as the live profile on success. Keep the old profile untouched on failure. Use this transaction pattern for purchase, refund, and other progression changes; test by reloading the actual serialized payload and injecting I/O failure, not only counting fake bridge calls.

**Locations:** `Runtime/Gameplay/VoidFallGameRuntime.UI.cs:190,2935`; `UI/Views/WorkshopController.cs:162,204`; `Runtime/Gameplay/VoidFallGameRuntime.Sim.cs:5098`; `Persistence/SaveStore.cs:593,703` (all paths under `Assets/VoidFall/`). Evidence: `targeted.xml`, `targeted.log`, `DeepAuditRegressionTests.cs`, `repro-profiles/`.

### F02 — Three of five crossings skip the dealer

The current route has six visited arenas, five crossings, and two branch-choice crossings. Only branch choices enter `BeginPortalJunction`, which creates the dealer. `OpenCompletedVoidRift` sends a single exit straight through `OnRouteVoidChosen` and travel.

The audit advanced a real single-exit completion: travel began with `_junctionTransition == false` and no dealer. The fork control did set the junction transition. This contradicts the current requirement that the dealer appears at every crossing.

**Impact:** only two purchase opportunities instead of five. The last single-exit crossing also loses its late-route offer opportunity. A fresh profile cannot assemble a three-fragment legendary from dealer purchases in that run when there are only two purchases available.

**Repair:** make the safe dealer crossing a shared transition step for one or multiple destinations; destination choice should be a separate concern. Verify all five crossings through the real objective → rewards → crossing → arrival flow, preserving one purchase per crossing and stable stock.

**Locations:** `Runtime/Gameplay/VoidFallGameRuntime.Rift.cs:102,151`; `VoidFallGameRuntime.Journey.cs` (`BeginPortalJunction` / `BeginDealerCrossing`). The current contract is explicit in `Docs/Design/2026-09-12-DealerIntegration.md:14` and `Docs/REPO_MAP.md:379`: single exits also get a safe dealer crossing. The older direct-travel description in `RunJourney.md` needs reconciliation. Existing dealer tests/probes that directly call `BeginPortalJunction` cannot detect this routing omission. Evidence: `targeted.xml` and `DeepAuditRegressionTests.cs`.

### F03 — Terminal save can leave a partial run on disk

When a form becomes eligible during `SaveRun`, the method has already added the run's wallet and lifetime totals. `EvaluateFormUnlocks` then persists that intermediate profile. `SaveRun` subsequently constructs the run/high-score records and performs another save.

The fault-injection test allowed the form-unlock write and failed the terminal write. Result: memory rolled back to **0 runs**, while disk held **1 run, 130 Scraps, and 0 recent-run records**. The run remained marked unsaved in memory. A process exit at that point preserves the inconsistent disk state.

This is conditional on a new unlock being evaluated there; it is not a claim that every completed run is saved incorrectly.

**Repair:** separate calculating form unlocks from persisting them. During a terminal transaction, collect every change into the same candidate profile and perform one commit. Retain immediate standalone unlock persistence only when it owns its own complete transaction.

**Locations:** `Runtime/Gameplay/VoidFallGameRuntime.Persist.cs:40,88`; `VoidFallGameRuntime.Forms.cs:42`. Evidence: `additional.xml`, `additional.log`, `additional-profiles/`, `DeepAuditAdditionalTests.cs`.

### F04 — Population limits are scattered across spawn callers

`UpdateNullCitySpawns` checks its ordinary target and heavy cap, and police arrivals check the lockdown cap. `ProcessNullCityBirths` instead calls `SpawnNullCityUnit`, whose admission ultimately checks the shared pool for a free slot. That pool can hold 750 bodies, while the native quiet/lockdown caps are 50/90.

**Targeted result:** starting at the quiet cap of 50, killing one Broodmother and processing its four queued children produced **53 active enemies**. The shared pool admitted all children. This reproduces cap bypass at the boundary; it does not establish the frequency or peak overshoot during a normal run.

**Repair:** explicitly define whether children and boss summons share the native cap or have reserved headroom, then enforce that policy at admission. Preserve queued children/reward-root ownership while deferring births rather than silently dropping them. Include simultaneous deaths and a phase change in the regression cases. Do not cull surviving actors merely because a quiet phase has a lower arrival budget.

**Locations:** `Runtime/Gameplay/VoidFallGameRuntime.NullCity.cs:146,266,390,417,687`; `VoidFallGameRuntime.Sim.cs:3219`; `Core/NullCityPacingRules.cs:14`. Evidence: `spawn-and-allocation.xml`, `spawn-and-allocation.log`, `DeepAuditAdditionalTests.cs`.

### F05 — The FPS target is not met at maximum density

The completed controlled High run maintained the requested 750 bodies at its refill checks, with zero capacity failures, and advanced combat/damage/kills. Its **45.1 average FPS, 37.4 ms p95, and 50.8 ms p99** are below a steady 60 FPS target. The longest measured frame was 444 ms. This was a synthetic maximum-load scenario with evolved weapons, initial bosses, dense projectiles/pickups, invulnerability, and automatic reward handling; it is not evidence of normal-player difficulty or average-run FPS.

The lighter Director I run was much closer to 60 FPS but still had uneven frame times. See the full measurement table below. There is no trustworthy GPU timing or allocated-bytes counter in these release-player reports; their zero fields mean unavailable.

**Repair order:** first establish repeatable advancing scenarios and supported profiler counters, then attribute simulation, rendering, HUD, asset-load, and collection spikes. Cache the confirmed callback churn in F06. Profile dense area-damage/death/birth chains and per-enemy presentation separately. Do not assume a lower graphics preset will solve simulation work, or lower enemy limits without reviewing the game-design effect.

The enabled history exporter is part of these measured costs. F14 identifies substantial serialization and output volume; its isolated contribution to frame time still needs measurement.

### F06 — 23 unnecessary delegate constructions per enemy tick

`UpdateEnemies` repeatedly assigns instance method groups to the same `GameSim` callback fields. Compiled IL shows **23 unconditional delegate `newobj` instructions before the enemy loop**, plus separately cached/conditional delegates. At 60 simulation steps per second, that is 1,380 avoidable delegate constructions per second even before processing enemy bodies.

This is distinct from allocating `Vector2`/other value types. The audit's `GC.GetAllocatedBytesForCurrentThread` counter proved unusable: allocating a known 1 MB array still reported zero. Consequently, no bytes-per-frame or GC-induced hitch claim is made from that counter.

**Repair:** bind stable instance callbacks once during runtime/simulation initialization, and cache conditional callbacks before assigning them. Preserve delegate target lifetime and the simulation's order/RNG contracts. Verify object identity, use supported allocation profiling, and rerun the golden-master sweep.

**Location:** `Runtime/Gameplay/VoidFallGameRuntime.Sim.cs:698–736`. Evidence: `enemy-loop-il.json`, `additional.xml`, `spawn-and-allocation.xml`.

### F07 — Arena residency does not own all arena memory

`ArenaResidencyManager` bounds and releases Addressables handles. The newer approved-map path calls synchronous `Resources.Load<Sprite>` and retains each sprite in `_approvedSprites` for the runtime's lifetime. There is no corresponding release/clear for that dictionary on arena exit or run restart. Animated frame arrays and renderer references also need to be included in any ownership fix.

This is **retained cache memory outside the package budget**, not evidence of unbounded allocation every frame. The asset set is finite. It nevertheless means the residency count can look healthy while previously visited map art remains retained, and new sprites can be loaded from a rendering path.

**Repair:** give map art an explicit package/lifetime owner and define intentional shared-cache exceptions. Measure current, neighboring, and departed arena textures across two complete routes and returns to the menu. Clear/release all owners together; adding only `UnloadUnusedAssets` while live references remain is insufficient.

**Locations:** `Runtime/Gameplay/VoidFallGameRuntime.ApprovedMaps.cs:24–29` and its render callers; `Runtime/Gameplay/ArenaResidencyManager.cs:33,105`.

### F08 — Texture footprint is a large optimization opportunity

The successful stock Windows build was **1,513,503,700 bytes**. Its Unity build report attributes **97.6% of uncompressed user-asset size to textures** and only 2.2% to sound. `resources.assets.resS` alone is about 1.28 GB.

Examples from the build report: approved Null City background 56.3 MB; several arena bases 42.2 MB each; Hydra guardian animation frames 12 MB each; dealer room rings 22.4 MB. The inspected approved-map importers use uncompressed textures, mipmaps, and an 8192 maximum platform size.

**Repair:** review import settings by asset role, compare compressed background/animation variants against native captures, and keep enough resolution for the actual camera zoom. Audit overlapping legacy/current assets and inclusion through Resources before removing anything. Preserve approved geometry, outlines, alpha edges, and GUIDs. Build-size numbers are not a measurement of simultaneous VRAM residency, and the soundtrack is not the main size problem.

**Evidence:** `build.log` (“Build Report” / largest used assets); `Resources/VoidFall/ApprovedMaps/null-city.png.meta`, `guardian-0.png.meta`.

### F09 — HUD readability depends too much on the world behind it

The Court capture shows score/timer/pressure text and corner labels losing contrast on white tiles and overlapping visual effects. Hydra's bright scenery similarly competes with labels and bottom slots. At the Court boundary, the player can also occupy the crowded upper-right HUD area because the camera is clamped while the player approaches the edge.

The HP-capacity/shield probes did render the 100/125/175 HP and 50-shield states; no timer overlap was found in those captures. The problem is contrast and presentation under bright/busy gameplay, not a reproduced HP arithmetic failure.

`SetupApprovedHud` clears the old health, clock, and metrics panel colors; the newer HP/shield path correctly supplies a separate dark vitals backing. The score/clock/corner information still lacks that consistent protection. The high-contrast option affects certain effects/notices but does not provide a comprehensive alternate treatment for this custom HUD. The muted audio button also uses an X, visually similar to close/quit; on the upgrade capture it moves into the lower-right passive-slot area.

**Repair:** retain the approved HUD scale/style, but add a restrained contrast treatment behind essential information and an explicit high-contrast path. Validate bright/dark arenas, corner movement, heavy effects, multiple aspect ratios, and the dimmed HUD behind modals. Give mute a recognizable audio state rather than a close-like X.

**Locations:** `Runtime/Gameplay/VoidFallGameRuntime.ApprovedHud.cs:27,96,143`, `VoidFallGameRuntime.SurvivalSupports.cs:76`; `UI/Core/UIManager.cs:353,449`. Captures: `Visuals/Maps/03b-court-edge-framing.png`, `06-hydra-ii-original-boss.png`; `Visuals/SurvivalHud/survival-cards.png`; `Visuals/Dealer/main-menu.png`.

### F10 — Menu/controller support is incomplete

An isolated test cleared EventSystem selection, entered the main menu, and waited a frame: `currentSelectedGameObject` remained null. Screen opening has no consistent initial-focus policy. A controller therefore lacks a dependable starting point for UI navigation.

The input source implements controller movement, dealer browsing, manual-weapon aim/fire, and the Null City dash, but pause/map shortcuts are keyboard-specific. That leaves controller-only gameplay incomplete.

There is also a source-level conflict to verify: Enter/Space are handled globally as Start Run on Home, while the quit confirmation does not become a runtime modal owner. Escape routes through the underlying menu instead of explicitly dismissing that dialog. **Do not count the attempted synthetic-key tests as reproduction:** their control assertions showed that this test environment did not register the injected key/button press. Earlier apparent pass/fail results from those tests are superseded.

**Repair:** establish initial/restored focus per screen and a shared action map with modal ownership. Bind controller pause/map, respect the focused UI control on submit, and verify keyboard/controller start → settings → back → run → pause → reward → result. Test remapping/device reconnect only after the basic flow is consistent.

**Locations:** `UI/Core/UIBuilder.cs` (`UIViewBase.SetVisible`); `UI/Core/UIManager.cs:403`; `UI/Views/MainMenuView.cs`, `QuitConfirmView.cs`; `Runtime/Gameplay/VoidFallGameRuntime.cs:1399–1500`; `Runtime/Input/InputReader.cs`. Evidence: `targeted.xml`; invalid input-control evidence is retained in `input-controls.xml` to make the limitation explicit.

### F11 — Existing gates miss current integration failures

Both full suites compile and run, but **10 existing assertions fail against the current working tree**. Inspection ties those failures to renamed content, changed counts/timing, and the expanded Court. They should be reconciled with current behavior; they are not ten independently confirmed gameplay defects.

The more consequential blind spots are cross-system: fake persistence tests do not reload the live transaction payload; dealer probes jump directly into a junction; the stress probe cannot finish roulette; the route probe waits for a menu without acknowledging the current result screen. The right-route probe also runs its boss-kill shortcut before Hydra II finishes its delayed boss arrival, so it misses that boss. These are automation gaps, not reproduced result-screen or Hydra softlocks.

CI also runs its Unity job only when `vars.UNITY_TESTS_ENABLED == 'true'`. A documentation-only green result is therefore possible. The remote variable/branch-protection configuration was not inspected, so this is not a claim that the repository's Unity job is currently disabled. The checked-in workflow has no Windows player smoke/build gate, and its normal step ordering can prevent PlayMode from running after an EditMode failure.

**Repair:** update obsolete expectations to observable current contracts, retain meaningful behavioral assertions, and make both test suites plus Windows smoke validation visible release gates. Add focused interaction tests for F01–F04. Test diagnostic drivers themselves, and export pause ownership plus supported/unsupported profiler status so a frozen scene cannot be mistaken for a fast game.

**Locations:** `.github/workflows/ci.yml`; `Tests/Editor/PlayableVoidRouteTests.cs`, `SupportMergeTests.cs`; `Tests/PlayMode/DestroyerFactionIntegrationTests.cs`, `IncidentEngagementIntegrationTests.cs`, `LootReachabilityTests.cs`, `RuntimeFlowRegressionTests.cs`; `Runtime/StressBenchmarkProbe.cs`, `RouteJourneyProbe.cs`.

### F12 — Startup diagnostics outlive startup

`LogSlowStartupPhase` stops emitting per-phase messages only after `_startupMenuReportLogged` becomes true. That flag is set by `RecordStartupMenuFrame`, which immediately returns outside the main menu. Starting a run before the ten-second menu sampling window finishes therefore leaves startup logging enabled for the run. Native benchmark logs contain `VOIDFALL_STARTUP_PHASE` messages more than 100 seconds after readiness.

This produces string formatting and log writes precisely on slow frames and pollutes startup evidence with combat work. The extra frame cost has not been isolated, so it is not presented as the cause of F05.

**Repair:** close the startup diagnostic window by elapsed time or an explicit transition, independently of whether the menu remains open. Keep ongoing performance data in the bounded aggregate telemetry path. Test starting immediately and remaining in the menu past ten seconds.

**Locations:** `Runtime/Gameplay/VoidFallGameRuntime.cs:1631–1643`; `VoidFallGameRuntime.UI.cs:19–49`. Evidence: accepted native performance `player.log` files.

### F13 — Menu layouts lose spacing and containment

At 1920×1080 the Workshop's Refund all button extends below the outer profile panel and nearly reaches the screen edge. It is visible in this capture; it is not a reproduced inaccessible-button failure. The preview fills the content height while its refund button is anchored another 42 layout units below the preview, with no corresponding bottom reservation in the parent layout.

The Records capture also shows metric captions crowding/overlapping their values, such as RUNS/0 and LONGEST RUN/0:00. `CreateMetricTile` aligns the caption to the bottom of the upper half and the value to the top of the lower half, then extends the value rectangle two units above their shared boundary. There is no positive line gap.

**Repair:** include the refund action in the column's allocated height and give metric captions/values explicit spacing that accommodates the active font. Preserve the existing Workshop and upgrade artwork. Check the complete panel at supported resolutions and UI scales, including scroll reachability of all upgrades and long metric values.

**Locations:** `UI/Views/WorkshopView.cs:125–129,214–219,344–349`; `UI/Core/UIBuilder.cs:1182–1222`. Evidence: `Visuals/Additional/Workshop/capture.png`, `Visuals/Additional/Records/capture.png`.

### F14 — Run history is expensive even when its queue is bounded

The accepted High stress run produced **387,872,706 bytes / 89,889 JSONL records** across its approximately 140-second run, including warm-up. Its final history counters report **90,333 submitted, 89,889 written, 444 dropped**, with no pending records or I/O error. Low produced **429,140,316 bytes / 99,442 records**, with zero reported drops. The lighter accepted Director I run still produced **36,853,962 bytes / 8,553 records**. These are measured diagnostic workloads, not an extrapolated size for every human run.

Most records are about 4.3 KB. Enemy spawn, first hit, damage window, death, drop spawn and consolidation dominate the dense runs. Each serializes the wide `UnityTelemetryHistoryEvent`, including large default progress/sample/context payloads irrelevant to that event. `JsonUtility.ToJson(value)` executes on the calling Unity thread; the background worker only receives the completed string. Moving disk writes to a worker therefore does not remove that serialization/allocation work.

The queue already has useful bounds and honestly reports dropped records. The problem is payload volume, main-thread preparation, incomplete high-load history, and cumulative disk growth. No profiler attribution yet establishes how much of F05 is caused by this exporter.

**Repair:** preserve required gameplay decisions, stable IDs and actual outcomes while using compact event-specific payloads with shared run metadata. Aggregate or sample continuous state deliberately, retain explicit loss counters, and version/document the schema for existing consumers. Consider compressed sequential output and an explicit storage budget/archive policy; do not silently delete the owner's run exports. Validate that dense combat retains critical events without growing the queue, and compare frame time with equivalent capture detail before/after the change.

**Locations:** `Runtime/Telemetry/RunTelemetry.cs:136–177,494–515,1170–1230`; runtime `.Telemetry.cs` event callers. Evidence: `export-volume.json`, `inspect_export_volume.py`, and the archived run JSON/JSONL files. High run ID `8f05e797da074ec9a5cbe892847d785b`; Low `de0f48536fd64e6c8d448a6079608980`; Director I `3b538fa95334460d8cc22b4aa59a02e1`.

## Test and build evidence

| Run | Result | Interpretation |
|---|---|---|
| Full EditMode | 668 total: **666 passed, 2 failed**, 341.7 s | Failures: old Grandmasters name; support count 20 instead of 22 |
| Full PlayMode | 410 total: **402 passed, 8 failed**, 427.7 s | Stale raid size/timings and Court geometry/visibility assumptions |
| First targeted suite | 8 cases | Six expected-contract failures reproduce Workshop, dealer, and missing menu-focus issues; fork control passes; zero-allocation result invalidated by later control |
| Additional suite | 8 cases | Partial terminal save reproduced; current black-hole timing and Court boundaries/culling pass; allocation API control fails; input results superseded by failed injection controls |
| Input control check | 3 cases | All failed to register the injected input; no input-flow verdict from this run |
| Spawn/callback checks | 2 expected-contract failures | Null City 50 → 53 bodies; equivalent enemy callbacks had different object identities on consecutive updates |
| Stock Windows build | **Succeeded** | Original game source with the existing working-tree changes; canonical outer build untouched |
| Audit-driver Windows build | **Succeeded** | Temporary, opt-in automation only; production fixes excluded |
| Dealer player probe | **Passed** | Room/shop, purchase, saved assembly/equip, both manual weapons; does not test single-exit routing |
| Map player probe | **Passed**, 15 diagnostic phases | Null City, Court, Hydra I and Hydra II; simulation advanced and visible sprites checked |
| Survival HUD probe | **Completed**, five captures | HP capacities, shield, Life Steal/Scavenger cards |
| Additional native captures | **Completed**, eight captures | Workshop, settings, records, White Sakura, Red Nebula, Eon Sea early/frost, Crascendo growth |
| Loot/notices probe | **Passed**, six captures | 300 fresh gems at 1.9 s; 257 after merging, preserving 300 XP; upper dealer placement |
| Left journey with audit result acknowledgement | **Passed**, six arenas, one saved run | Actual result acknowledgement callback; accelerated clocks/boss kills |
| Unmodified left / right journey probes | **Incomplete due to probe lifecycle gaps** | Left omitted result acknowledgement; right missed delayed Hydra II boss arrival |

The 10 baseline failures group as follows: one content rename; one added-support count; one eight-versus-five Destroyer count; five incident tests still using the old warning/release timing; one old 28-column Court boundary; one assertion that an offscreen Court corner tile must remain enabled. The targeted current-contract checks measured black-hole pull of 38.19 units after activation, the current Court bound at 3596.8, and 120 enabled tiles out of 3136 with the far corner correctly culled.

The deterministic simulation golden master and 32-seed sweep passed. Save recovery/import tests, many gameplay/arena/reward tests, and the real streamed-loop music tests also passed. An asset-reference scan checked 60 text-serialized assets and 1548 Assets metadata files, finding no duplicate asset GUIDs or unresolved scanned GUID references. All 15 imported soundtrack assets use streaming load type. These are useful positive signals with defined scope, not proof of complete runtime correctness.

The successful native journey visited **Abyss → Eon Sea → Red Nebula → Monochrome Court → White Sakura → Crascendo**, with seed 2848592627. The isolated saved profile contained one run, nine boss kills, and 202 Scraps. The audit helper only added the missing result acknowledgement for this journey; it did not repair the single-exit dealer routing. The accelerated survival and boss shortcuts make this flow evidence, not a full-length playtest or proof of dealer economy balance.

There are **43 native captures**, with representative images inspected across the tested screens and arenas. White Sakura's simple capture was covered by the pause screen, so it provides only partial arena visibility. The simple White Sakura/Red Nebula capture override changes the rendered arena without changing the route objective, which explains the diagnostic “Abyss” label; that label is not counted as a gameplay defect. Eon Sea and Crascendo used their dedicated diagnostic setup. These captures are not performance samples.

## Performance evidence

All accepted measurements use a visible Windows DX11 player at 1920×1080. No Unity Editor/build was running during the accepted benchmark windows. VSync/presentation behavior remained as configured by the game; these are player frame times, not uncapped GPU throughput measurements.

| Scenario | Warm-up / measurement | Average FPS | Median frame | p95 | p99 | Maximum | Mean simulation CPU/frame |
|---|---|---:|---:|---:|---:|---:|---:|
| Director I, High | 30 / 120 s | 58.51 | 16.67 ms | 21.85 ms | 35.48 ms | 294.76 ms | 5.07 ms |
| Controlled 750, High | 20 / 120 s | 45.08 | 19.74 ms | 37.37 ms | 50.79 ms | 444.08 ms | 10.99 ms |
| Controlled 750, Low power | 20 / 120 s | 40.36 | 22.94 ms | 39.16 ms | 53.17 ms | 420.81 ms | 12.43 ms |

The first Director I run included two failed OS capture attempts; its worst spike cannot be confidently attributed to gameplay alone. The controlled High run had no capture attempt during measurement. It advanced 5,216 simulation ticks and 81.80 combat seconds, with kills increasing from 1,822 to 14,266. Combat time differs from wall time because the game includes freeze/slowdown and modal behavior; this alone is not proof of a fixed-step clock defect.

Low power also maintained 750 bodies at its refill checks with no capacity failures, advancing 5,057 ticks and 78.28 combat seconds. Its kills increased from 2,192 to 15,849. This run used the revised audit reward driver and exercised a roulette claim; the High run did not encounter the same reward sequence. Combat states and thermal/power conditions were not matched tightly enough to conclude that Low itself makes the game slower. The supported conclusion is that this preset did not recover 60 FPS under this sustained load.

For that stress run, Unity-reported allocated memory was about 192 MB at the end and managed memory about 26 MB. This short, single-arena observation cannot establish a multi-run memory plateau or rule out the retained map cache in F07.

Fresh-process Workshop, Settings and Records captures logged startup readiness at approximately **15.4–17.8 engine seconds**, before their first useful captured UI. Initialization, asset preparation, driver startup and storage effects were not separately timed, so no specific cause is assigned. These diagnostic launch observations warrant an ordinary cold/warm menu-start benchmark alongside frame-time optimization.

**Excluded evidence:** the two initially hidden-window stress runs (including a black capture and implausible FPS); the unmodified 750 probe that stalled in roulette; the initial Low run that paused after a reward; the clean stock Director I repeat that stalled in a pause after about 61 combat seconds of measurement; and every missing GPU/main/render/GC recorder value. Raw logs/reports are retained with their validity flags. The benchmark reports' `sourceCommit: unrecorded` field is incomplete provenance; use the archived stock/audit-driver build-info files and build GUIDs, not that field.

## Game-design questions worth resolving

1. **Run length:** six 360-second survival phases already require 36 minutes before bosses, reward interactions, and crossing/arrival sequences. Five ten-second escape windows leave at most about 3 minutes 10 seconds of a 40-minute target for all remaining activity. The lower end of the stated 30–40 minute target cannot be reached under those timers. Decide whether the target or the stage structure should change before further pacing work; this is not a request to silently rebalance it.
2. **Survival-card value:** the captured max Life Steal card grants 0.9 HP per 200 kills. Max Scavenger increases an ordinary 2% Scrap chance to 2.4%, a 0.4-percentage-point increase, and needs 200 collected Scraps for 5 shield. Other reward sources matter, so this is not a full economy simulation. Measure actual healing/shield earned per run and opportunity cost versus other support choices. Clarify the relative “+20%” wording if players interpret it as +20 percentage points.
3. **Black-hole engagement:** the force activates after a 10-second warning, with its center locked 165 units ahead and a 230-unit radius. At 235 units/s, straight movement exits that circle after about 1.68 seconds. The current code comment promises that continuing straight crosses the warned force zone, but the timing makes avoiding the active pull easy in open space. The force itself works after activation. Decide whether this incident is intended as space denial or as a dodge/juke threat, then tune and test that intent.

## Coverage and remaining uncertainty

| Area | What was checked | Remaining limit |
|---|---|---|
| Bootstrap/build/assets | Fresh Windows build; scene/package configuration; GUID scan; texture/audio import footprint | No clean-machine installer/distribution test |
| Core combat and pools | Full suites and seed sweep; pool/order/identity, collision, loot, damage, weapons and spawn callers; advancing density probe | No exhaustive combination of every weapon/support/arena/Director |
| Journey/dealer/progression | Route contracts; actual single/fork crossings; isolated persisted transactions; terminal fault injection; journey probes | Accelerated route completion does not evaluate human pacing |
| Abyss/shared Director | Normal advancing Director I and synthetic 750 stress; sustained schedule/attack budget review | Longer steady-state and Director II/III performance matrix remain |
| White Sakura | Arena/render/transition ownership, regression suite, completed accelerated route, partial native capture behind pause | Unobstructed dedicated capture and long-duration native performance run remain |
| Red Nebula | Meteor/strike paths, arena switching, regression suite, basic native capture and accelerated route | Full meteor-heavy native performance/balance run remains |
| Hydra I/II | Population/birth/guardian paths, original boss transition, rendered map captures and tests | Long hive-backlog/maximum-density soak remains |
| Monochrome Court | Board dimensions/culling; factions, rooks/attacks; boss/edge/roster captures and tests | More aspect ratios and sustained dense combat performance remain |
| Null City | Phase/police/heavy/brood paths; boundaries/purge/boss; native captures and tests | Full phase-transition economy/density soak remains |
| Eon Sea / Crascendo | Terrain/current and per-hit growth logic, route integration, relevant tests; native early/frost/growth captures and completed accelerated route | Sustained arena performance and human balance sessions remain |
| Menus/HUD/input/settings | Main menu/Workshop/settings/records/pause/dealer/upgrade/HP/shield captures; focus test; keyboard/controller source paths; persistence/display code and tests | OS frame capture failed twice; synthetic key controls also failed. No claim of a completed physical-controller or multi-monitor run |
| Audio | Music source isolation, streamed looping, focus/pause state, DSP/thread ownership, procedural voice paths, import settings and tests | Listening quality, output-device changes, peak full-mix audibility not certified; captures show the existing mute state |
| Saves/telemetry | Real payload transactions, recovery/import tests, failure paths; telemetry queue/error/flush bounds; native export sizes and dropped-record counts | Full human-session export size and long-duration memory remain unmeasured |

The OS capture tool timed out twice; the user confirmed the desktop was unlocked and gameplay visible. In-engine screenshot probes succeeded and were inspected. This was a capture-tool limitation, not evidence of a black-screen gameplay bug.

Unity logged a ComputeBuffer finalizer warning on player shutdown. No first-party ComputeBuffer allocation was found in the source scan. Keep the warning for profiler/stack follow-up; it is not yet attributed to a VoidFall leak. Likewise, review static UI-click subscription teardown and persistent UI callback ownership before adding runtime/scene recreation, but do not label them a reproduced ordinary-run leak.

## Workspace and evidence preservation

The temporary regression-test sources and opt-in benchmark driver were removed from `Assets/`. The Windows build regenerated `Assets/AddressableAssetsData/link.xml.meta`; its exact baseline bytes/GUID were restored and hash-checked. The existing ignored Addressables content-state binary was regenerated by the build; its older contents were not in the source snapshot. A separate `Docs/Design/2026-09-26-RenderingFidelity-Handoff.md` appeared during the audit and was preserved untouched.

`source-preservation.json` records the final comparison: **all 3,126 baseline files match their original SHA-256 hashes, with none missing**. Git status differs only by this new audit report and the separate handoff document. `native-evidence.zip` preserves all diagnostic run exports, first-party player assemblies and build provenance; every archive member passed CRC verification (approximately 1.196 GB compressed to 72.4 MB). Reproduction test sources, profiles, XML results, raw player logs, captures, and summary JSON remain alongside it.

Automatic approval review blocked removal of the two temporary full player folders (`Player/`, `PlayerWithAuditDriver/`), including a retry with verified literal paths. The only returned reason was “blocked by policy.” They remain under the audit log directory and are not promoted releases. The canonical outer `Builds/` player and real profile were not replaced.

## Repair sequence

1. **Progression integrity:** implement one profile transaction boundary for Workshop and terminal runs; add serialization reload and mid-write failure cases. Verify successful and failed purchase/refund/unlock paths.
2. **Journey and population contracts:** route every crossing through its required dealer stage; centralize spawn admission policy without losing queued-child accounting. Test the real flow and simultaneous events.
3. **Trustworthy validation:** reconcile the 10 stale baseline assertions, fix probe lifecycle/acknowledgement coverage, and make Unity + Windows smoke results explicit release gates.
4. **Presentation and input:** initial/restored focus, modal submit/cancel ownership, controller pause/map, bright-arena HUD contrast, and audio-button clarity.
5. **Measured optimization:** slim history payloads while preserving capture contracts, cache stable callbacks, attribute heavy combat costs with supported counters, bring approved-map assets into residency ownership, and compare texture import variants visually and in a build report.
6. **Balance review:** use full-length human playtests and exports to resolve the run-length target, survival support value, dealer affordability, and incident purpose.

Avoid a wholesale rewrite. Extract narrow owners for profile transactions, journey transitions, input/modal ownership, and asset residency while keeping `GameSim` ordering, pool identities, and RNG separation protected. Those boundaries address the observed integration failures more directly than adding more local flags and rollback patches.
