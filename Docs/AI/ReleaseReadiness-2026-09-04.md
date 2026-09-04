# VoidFall release readiness — September 4, 2026

## Decision and scope

**The current project is not ready for a full release.** The combat foundation
passes useful automated checks, but the advertised branching journey cannot
reach a finished ending. Treat October 15 as a target with acceptance gates,
not evidence of readiness. There are 41 calendar days between September 4 and
October 15. The owner clarified that we should work toward a full game, rather
than assuming a limited demo.

This is a release-readiness audit plus focused remediation, not an exhaustive
proof of every mechanic or a full manual playthrough. It covers the actual
Unity project at `C:/Users/anasb/Desktop/voidfall/voidfall-unity`, commit
`049500d`, its pre-existing audio/cosmetics/Workshop/settings changes, and the
audit fixes. No browser prototype work, package upgrades, backend switch,
gameplay rebalance, or new arena design was performed.

## Evidence

| Check | Result | Artifact / scope |
|---|---|---|
| Unity environment | Confirmed | 6000.5.7f1; URP 17.5.0; Input System 1.20.0; Addressables 2.8.1 |
| Initial EditMode suite | 250 passed, 0 failed | `Logs/Audit-2026-09-04/editmode-baseline.xml` |
| Save reproductions before fixes | 3 failed, 2 passed | `save-red.xml`: missing/corrupt primary returned 0 Parts rather than 125; court-pawn absent |
| Recovery edge reproductions | 2 failed, 7 passed | `save-edge-red.xml`: stale legacy profile won; deferred restore overwrote good backup |
| PlayMode before travel fix | 6 passed, 1 failed | `playmode-red.xml`: automatic travel left current route at Abyss while entering Hydra |
| Final EditMode suite | **259 passed, 0 failed, 0 skipped** | `editmode-final.xml`; includes nine filesystem/save tests |
| Final PlayMode suite | **7 passed, 0 failed, 0 skipped** | `playmode-final.xml`; route flow, UI lifecycle, pinned hash, 32 seeds run twice |
| Simulation contract | Unchanged | Hash `14713629958221367877`; no fixture re-pin |
| Asset metadata | Passed static scan | 658 `.meta` files; no duplicate GUIDs or assets missing file metadata; `asset-integrity.json` |
| Startup scene script reference | Resolved | SampleScene's serialized script is URP UniversalAdditionalCameraData |
| Windows player build | **Succeeded** | `windows-build.log`; 159,288,994 bytes; `C:/Users/anasb/Desktop/voidfall/Builds/VoidFall.exe` |
| Player startup/render | Passed bounded capture check | `menu.png`, `menu.log`, `stress-rendered.png`; Windows D3D12 / RTX 3080 Laptop GPU |
| Dense-combat performance | **Not validated** | Probe completed, but all seven batch samples and all five normal-player samples had identical combat counts; see HF-016 |
| Independent code review | No remaining material findings | Save recovery, bestiary, route changes and tests; two initial findings reproduced and repaired |

Logs above are relative to `Logs/Audit-2026-09-04/` unless fully specified.
Expected locked-file errors in save tests are asserted; they are not new game
errors. Existing `Resolution.refreshRate` deprecation warnings remain.
Player shutdown logs also contain a ComputeBuffer disposal warning; ownership
was not isolated, so no first-party leak is claimed. Normal-player logs include
a D3D12 info-queue query warning, but rendering still produced the attached
captures. A batch-mode capture was black and is not visual validation.

The previous Windows build is retained at `Logs/Audit-2026-09-04/previous-build/`.
Diagnostics caused bestiary-only profile changes; the original primary and
backup save bytes were restored from `profile-backup/` after all player
processes exited. No player progression was intentionally changed.

## Fixed in this audit

### HF-007 — last good save backup was ignored

**High / confirmed by failing tests / fixed.** Missing or malformed primary
save data reset the profile despite a valid `.bak`. Subsequent saves could
replace that backup with the corrupt primary. A retained v3 profile also took
priority over the more recent backup when the primary disappeared.

`Assets/VoidFall/Persistence/SaveStore.cs` now recovers the current backup
before legacy candidates, preserves backup bytes during recovery, and retains
that protection until a write succeeds. Locked primary/backup files latch
write protection. Existing schema-v4 protocol refunds are applied once and
persisted as v5. Tests use disposable folders, not the player's save.

Regression evidence covers missing/corrupt primary, stale legacy precedence,
failed recovery write followed by retry, locked primary and backup, healthy
primary priority, and one-time legacy migration.

### HF-008 — newer bestiary discoveries never persisted

**Medium / confirmed by failing test / fixed.** `DiscoverBestiary` only marks
existing profile entries; the hardcoded list omitted all five Court enemies,
Hydra Prime and both Grandmasters. Save sanitization also dropped these IDs.

`SaveStore.BuildBestiaryOrder` now reads the existing content definitions,
preserves the legacy entry order, and appends Unity-authored content. A real
save/reload test checks discovery survives and unrelated enemies stay hidden.
Previously discarded discoveries cannot be reconstructed from old saves.

### HF-009 — automatic rift travel did not advance the route

**High / confirmed by PlayMode reproduction / fixed.** The single-exit branch
called `EnterVoidThroughRift` directly. Only the card-click path called the
route controller's `Confirm`, so the next objective initialized against the
old route ID. `VoidFallGameRuntime.Rift.cs:129` now uses that same confirmed
selection path for automatic travel.

The regression uses two implemented arenas to isolate this bug from missing
late-game content. It checks current ID, selected state, history, incoming
Hydra objective, and the route model's subsequent completion.

### HF-010 — CI omitted flow regressions and the seed sweep

**Medium / confirmed configuration / partially fixed.** The PlayMode job in
`.github/workflows/ci.yml` filtered to one golden-master class. Removed that
filter so the flow regressions and 32-seed sweep are eligible to run. The
equivalent full suite passes locally. Hosted CI remains opt-in, and no player
build job is configured. Repository secrets/variables and hosted runs were
not accessed or changed.

## Remaining release blockers

### HF-001 — no complete playable route and victory resolution

**High / confirmed in code / open / large implementation task.**

`Core/VoidRoute.cs:192` defines ten nodes. `Core/VoidObjectives.cs:27` only
implements Abyss, Red Nebula, White Sakura, Hydra and Monochrome Court. Dead
Orbit, Graveyard, Null City, Last Gate and Final Void return null. These nodes
are reachable; for example, both Red Nebula exits lead to unimplemented nodes.
The route model's `HasEscaped` is never consumed by the runtime;
`Runtime/Gameplay/VoidFallGameRuntime.Rift.cs:89` returns for terminal nodes,
and the game-over summary at `VoidFallGameRuntime.cs:3202` hardcodes defeat.

**Required:** implement the agreed full route, final encounter, victory result,
one-time rewards, records and restart. Validate every branch from a fresh
profile through victory and defeat. An automatic travel repair alone does not
finish any missing destination. Do not hide unfinished content and call that
the complete planned game without a deliberate scope decision.

### HF-002 — half the route lacks a prepared visual package

**High / confirmed configuration and mappings / open / large content task.**

Five prepared arena identities exist. The other five graph nodes do not have
their own arena IDs/packages and fall back to Abyss presentation. The existing
prepared-content build gate validates the five declared arenas, so a successful
build does not prove the full graph has authored content.

**Required:** complete the packages for the shipping route, with readable
hazards and enemy identity, then verify Addressables transitions in the player.
Lost City currently uses the route ID `null-city`; preserve save/data identity.

### HF-011 — controller play is incomplete

**High if controller support is advertised / confirmed omissions, device
behavior untested / open / medium task.**

`Runtime/Input/InputReader.cs:44` reads the left stick for movement, but the
runtime's pause/restart/level-choice shortcuts in `VoidFallGameRuntime.cs:1355`
only read the keyboard. The UI has no `SetSelectedGameObject`/first-selection
handoff when menus become visible. A physical gamepad-only session has not
been validated. The old README's right-stick aim, trigger fire and Start pause
claims were inaccurate: weapons auto-target and auto-fire in `Sim.cs:2140`.
The README now reflects current behavior.

**Required:** complete focus, navigation, submit/cancel and pause ownership;
test launch, start, upgrades, roulette, routes, workshop, settings, death,
restart, unplug/reconnect and focus loss without touching a mouse. Verify on
actual supported hardware before making controller/Deck support claims.

### HF-012 — run persistence stops at game over; failed saves can be abandoned

**High for progression reliability / confirmed call paths / open / medium to
large task depending on resume scope.**

`Runtime/Gameplay/VoidFallGameRuntime.Persist.cs:44` explicitly excludes live
runs. `SaveData` contains no live route/combat snapshot. Closing the game during
a long run loses that run and its uncommitted Parts. This is existing behavior,
not a corruption bug. Separately, failed terminal saves roll back correctly,
but `StartRun` and `EnterMainMenu` reset run state without first retrying that
failed result. Restarting can therefore discard earned rewards after a
temporary storage failure.

**Required:** protect a completed but unsaved result until saved or explicitly
abandoned; exercise disk-full/locked-file recovery followed by restart/menu.
Decide the public contract for mid-run quitting. If resume is promised, persist
an actual versioned run snapshot and verify replay/reset compatibility; do not
pretend saving profile settings resumes a run.

## Other actionable risks

| ID | Severity / confidence | Evidence and action |
|---|---|---|
| HF-013 | Medium / confirmed | Route-card objectives/rewards still promise anchors, gene nodes, capture zones and boons while the implemented objectives are five-minute survival plus bosses. Threat multipliers are displayed but `ThreatOf` has no gameplay consumer. Reconcile the product rules, UI copy and implementation, then test each promise. Sources: `Core/VoidRoute.cs`, `Core/VoidObjectives.cs`, `UI/Views/RouteSelectController.cs`. |
| HF-014 | Medium / likely performance risk | Boss HUD still calls `ToUpperInvariant()` before change detection on every frame (`Hud.cs:276`). Bloom/chromatic intensities do not depend on quality (`VideoSettings.cs:94`). Damage floaters and LineRenderer-heavy effects warrant profiling. Measure frame-time percentiles/GC/draw calls before choosing further optimization. |
| HF-015 | Medium / confirmed forward-compatibility risk | Native saves with a version newer than v5 are sanitized to v5 and persisted (`SaveStore.cs:239`). A future player rollback could discard newer progression. Reject future schemas without overwriting them before testing public updates/rollback. |
| HF-016 | High validation gap / confirmed probe limitation; suspected stall unresolved | `StressBenchmarkProbe` measures wall time and reports completion without checking simulated time advanced. Both batch and normal runs held exactly 191 enemies, 2 bosses, 87 shots and 225 pickups throughout measurement. Roughly 16.67 ms frame averages therefore do not prove sustained combat performance. Add simulation-time/paused-state evidence and reject stalled runs before using this probe as a release gate. |
| HF-017 | Medium / possible visual defect | The normal-player stress capture contains opaque gray rectangles around several effects and overlapping reward text. The pixels are visible in `stress-rendered.png`; effect ownership and intended appearance were not established. Reproduce in ordinary combat and isolate texture alpha/material/view ownership before editing art or shaders. |
| HF-003 | Medium / confirmed maintainability risk | About 28,018 lines remain across runtime partials. Flow state is shared across UI, simulation, roulette and rifts. Keep fixes narrow and protect boundary transitions; a wholesale rewrite is not a release prerequisite. |
| HF-004 | Medium / mixed confirmed and unknown | Product identity is now set correctly; hosted test execution, Steam build/depot configuration, public versioning and minimum-spec hardware validation remain unverified. Windows is the only current build target inspected. |
| HF-006 | Low / confirmed | Existing obsolete `Resolution.refreshRate` use remains in video rules/tests. Migrate in a focused settings change, with no reason to change Unity/package versions. |
| HF-005 | Low / informational | Generated cache/log/build directories dominate local workspace size and are ignored. No cleanup is needed to improve gameplay; the audit retained its evidence and preceding build. |

Music credits identify authors in `Docs/AudioCredits.md`, but this audit did
not establish license/permission records or public Steam configuration.
Record that release evidence; absence in inspected docs is not a claim of
infringement. No network/multiplayer implementation was found in first-party
code; the Multiplayer Center package alone does not make this multiplayer.

## Corrections to earlier audit assumptions

- URP already uses 2x MSAA, HDR off, intermediate texture Auto, shadows off and
  SRP Batcher. Do not repeat the old 4x/HDR/Always finding as current.
- Toast text and much HUD text are already change-gated. The remaining boss
  name formatting allocation is narrower than the original HUD report.
- The audio pad's time-invariant `Pow` calls have been hoisted. The pad is a
  fallback when authored music is unavailable. An eight-second clip length
  does not demonstrate that every callback synthesizes eight seconds; the
  previous claim of guaranteed periodic underruns was not established here.
- Company/application identifiers are no longer Unity defaults.
- Mono-to-IL2CPP migration is not a prerequisite demonstrated by this audit.
  Avoid changing backend simply to satisfy an earlier generic recommendation.

## Route to October 15

These are proposed acceptance dates, not estimates that missing content is
guaranteed to fit. Build speed alone does not establish player enjoyment.

| Window | Work and exit gate |
|---|---|
| Sep 4–10 | Stabilize persistence and all existing transitions; decide exact full-release route, supported devices and quitting/resume contract. Every currently implemented arena can be entered, completed and left repeatedly. |
| Sep 11–20 | Finish missing route content and victory/defeat/reward/restart lifecycle. Every shipping branch reaches an ending, with no placeholder objective or visual package. Start external playtests during this window. |
| Sep 21–27 | Complete controller/UI flow and tune pacing from observed first-time players. Track first-upgrade timing, damage causes, boss duration, route completion, restart choice and points of confusion. Freeze new systems at the end of this window. |
| Sep 28–Oct 4 | Produce a content-complete candidate, run save/update/recovery checks, full branch playthroughs, low-spec performance and Steam installation/launch tests. Submit a near-final build with review margin. |
| Oct 5–11 | Fix release blockers and verify the same candidate on supported machines. Conduct long sessions, repeated restarts, focus loss and controller reconnect tests. Retest any changed behavior. |
| Oct 12–15 | Release only if gates pass. If core content, stability or review approval is missing, revise the date or explicitly choose a smaller release format. |

Steam has timing gates separate from engineering. Its onboarding documentation
lists a 30-day app-fee wait for the first few titles and a public Coming Soon
page for at least two weeks. For October 15, the corresponding latest calendar
dates would be September 15 and October 1, respectively, with extra margin
needed. Account-specific eligibility is unknown.
[Steamworks onboarding](https://partner.steamgames.com/doc/gettingstarted/onboarding).

Valve currently advises allowing at least seven business days for store/build
review, though review typically takes three to five. The account's actual
review state was not inspected; do not wait until October 14.
[Steamworks review process](https://partner.steamgames.com/doc/store/review_process).

## What a release pass must prove

- Every shipping route completes; victory and defeat both record rewards once.
- Fresh install, old profile, corrupt primary, locked storage, update and
  rollback scenarios preserve the player's progress under the chosen contract.
- All advertised controls and UI choices work on supported devices.
- Dense fights meet a stated frame-time target on a named minimum-spec device;
  no performance claim rests solely on a developer-machine average.
- First-time players can explain their objective and why they died without
  developer coaching. Repeated playtests identify whether upgrades and bosses
  support replay, rather than assuming technical correctness guarantees fun.
- The reviewed Steam build matches the features and content advertised.

An Overwhelmingly Positive rating cannot be promised or inferred from a code
audit. Complete, fair and readable runs plus reliable progression are the
immediate quality goals this work can directly improve.

## Coverage limits

Inspected first-party structure (164 C# files inventoried), six runtime assembly
boundaries, two test assemblies, packages, startup/build configuration, route
and objective integration, sampled combat/pooling/lifecycle paths, UI/input,
saves, audio, URP and asset residency, existing audits and CI. The review sampled
large implementation files; it did not examine every line or certify all assets.

No Unity MCP was available. Local Unity batch tests/build tools provide execution
evidence. Full human playthroughs, hardware controller/Deck testing, minimum-spec
profiling, listening tests, localization, Steam account state and all content
rights remain outside the completed automated checks. No publication or
external messages were sent. Existing user source edits were preserved.

## Reproducing the automated checks

Run from the Unity repository root, with no other Editor instance holding it.
Do not add `-quit` to Unity test commands; the test runner exits itself.

```powershell
& 'C:/Program Files/Unity/Hub/Editor/6000.5.7f1/Editor/Unity.exe' -batchmode -nographics -projectPath $PWD -runTests -testPlatform EditMode -testResults Logs/Audit-2026-09-04/editmode-final.xml -logFile Logs/Audit-2026-09-04/editmode-final.log
& 'C:/Program Files/Unity/Hub/Editor/6000.5.7f1/Editor/Unity.exe' -batchmode -nographics -projectPath $PWD -runTests -testPlatform PlayMode -testResults Logs/Audit-2026-09-04/playmode-final.xml -logFile Logs/Audit-2026-09-04/playmode-final.log
& 'C:/Program Files/Unity/Hub/Editor/6000.5.7f1/Editor/Unity.exe' -batchmode -nographics -projectPath $PWD -executeMethod VoidFall.EditorTools.BuildScript.BuildWindows -logFile Logs/Audit-2026-09-04/windows-build.log
```

The normal-player diagnostic used `-vfbench=1 -vfscenario=productionMax
-vfwarmup=5 -vfmeasure=20`, 1280x720 windowed, with explicit `-vfoutput`,
`-vfcapture` and `-logFile` paths under the audit directory. `stress-rendered.json`
is the result. Its hardcoded `sourceCommit` field is stale; use the source state
at the top of this report. This run is evidence of the probe limitation, not a
performance certificate.
