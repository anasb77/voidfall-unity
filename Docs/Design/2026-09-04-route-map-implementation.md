# Route map and reliable Void travel implementation plan

Approved by the owner's instruction to proceed with the agreed design using
existing arenas and portal assets. Spec: `Docs/Design/RunJourney.md`.
Execution: subagent-driven development for disjoint graph and view work;
coordinator owns runtime integration and all Unity executions.

## Contract and decisions

- No new candidate arenas, packages, authored art or scene rewrites.
- Generate a finite route from prepared arenas with an implemented objective.
  Preserve `PrototypeGraph` for its legacy tests; normal runs use a new factory.
  Stable arena IDs remain graph node IDs; each arena occurs at most once.
- Five current arenas produce widths 1/2/1/1 (four arenas visited); larger
  catalogues can fill up to five rows after Abyss, with at most two exits.
  Every branch reaches the same last row. Graph generation uses its own RNG.
- Tab pauses and displays the complete node graph, history/current/next states,
  and seeded mystery destinations. Map clicks only mark a planned destination.
  The mystery's name is revealed on entry; hazard/threat hint remains visible.
- Completed combat stops safely, processes outstanding reward/upgrade UI, and
  advances into a small safe two-portal junction, or directly through one exit.
  Use `Resources/VoidFall/Portals/pipo-gate01*.png`; no gameplay-prefab dependency.
- Terminal success or death saves once then returns to Home. Failed saves keep
  the result available for retry rather than silently resetting it.
- Preserve the current golden master; gate new behavior to normal route flow.
- Work in the user's current checkout with explicitly disjoint files; don't
  move/commit another agent's arena work or run multiple Editors on this root.

## Tasks

- [x] Reproduce completion -> roulette -> prize -> travel, including the supplied
  double-boss seed 2848592627; preserve logs and save files before tests.
- [x] Add `Content/PlayableVoidRoutes.cs` and route metadata in `Core/VoidRoute.cs`.
  Public API: `PlayableVoidRoutes.Create(uint seed)`, `VoidRouteRun.Nodes`
  (`IReadOnlyList<VoidRouteNode>`), `VoidRouteNode.IsMystery` (`bool`).
  Add `Tests/Editor/PlayableVoidRouteTests.cs`: deterministic seed, varied routes,
  only ready objectives, <=2 exits, reachability/termination, no repeated arena,
  metadata does not reveal mystery after inspecting it or consume combat RNG.
- [x] Add `UI/Views/RouteMapView.cs`, derived from `UIViewBase` with
  `Show(VoidRouteRun run, string plannedId, Action<string> onPlan, Action onClose)`.
  Lay out rows by depth, draw connections behind nodes, make current/path/future/
  sealed states readable at 720p, and label terminal state as escape.
  UI never calls `SelectNextVoid`; coordinator adds it to `UIManager`.
- [x] Add runtime journey/junction partial; integrate explicit prize/map ownership
  into UI reconciliation. Check completion handoff outside paused combat ticks.
  Keep incoming-asset waits observable and invulnerable, reset arena-local state,
  and expose a small diagnostic state snapshot for regression/player evidence.
- [x] Add real-flow PlayMode regressions: double bosses, unclaimed chest, reward
  UI surviving a frame, automatic/split exits, map pause restoration, portal
  collision selecting the route, restart resetting map, and terminal save/Home.
- [x] Run related tests, full EditMode/PlayMode (incl. 32-seed sweep), Windows
  build, finite player flow capture, and inspect map/junction screenshots.
- [x] Independent review; resolve material findings; update repo map/design with
  actual behavior and precise limitations. Preserve all pre-existing edits.

## Validation commands

From the repository root, using the installed Unity 6000.5.7f1 executable:

```powershell
$unityEditor = 'C:/Program Files/Unity/Hub/Editor/6000.5.7f1/Editor/Unity.exe'
& $unityEditor -batchmode -nographics -projectPath $PWD -runTests -testPlatform EditMode -testResults Logs/RouteMap/editmode.xml -logFile Logs/RouteMap/editmode.log
& $unityEditor -batchmode -nographics -projectPath $PWD -runTests -testPlatform PlayMode -testResults Logs/RouteMap/playmode.xml -logFile Logs/RouteMap/playmode.log
& $unityEditor -batchmode -nographics -projectPath $PWD -executeMethod VoidFall.EditorTools.BuildScript.BuildWindows -logFile Logs/RouteMap/build.log
```

Do not add `-quit` to tests. Player capture must render without `-batchmode`;
completion of a wall-clock stress probe does not prove progression. Test XML,
player state events, inspected screenshots and restored user-save hashes are
the acceptance evidence. The unrelated candidate-arena proposals remain unapproved.

## Verified result — September 5, 2026

Starting commit: `2d184a4`; changes remain in the working tree. No package,
scene or arena-art changes were required. The generated Addressables link
metadata GUID was restored after the build regenerated it.

| Gate | Evidence |
|---|---|
| Graph baseline reproduced | `Logs/RouteMap/graph-red.xml`: six expected behavior failures against the legacy prototype |
| Original handoff defect reproduced | `handoff-red.xml`: supplied double-boss seed loses PrizeReveal to Pause on reconciliation |
| Full EditMode | `editmode.xml`: 274 passed, 0 failed |
| Full PlayMode | `playmode-final.xml`: 22 passed, 0 failed; includes physical portals, pause ownership, terminal XP/revive, save retry, junction rendering, and unchanged golden master/32-seed sweep |
| Windows build | `build-final.log`: succeeded, 159,316,345 bytes; `../Builds/VoidFall.exe` |
| Left branch in player | `check-final-left.json`: Abyss → Hydra → White Sakura → Monochrome Court; victory saved once and Home reached |
| Right branch in player | `check-final-right.json`: Abyss → Red Nebula → White Sakura → Monochrome Court; victory saved once and Home reached |
| Visual inspection | `map-720.png`, `map-1080.png`, `junction-final-720.png`: readable graph and visible portal assets/labels, no outgoing boss visuals in the crossing |
| User data | `save-restoration.json`: all six original backed-up files match byte-for-byte after diagnostics |
| Independent review | Graph/UI approved; terminal XP ordering finding reproduced and fixed, including post-revive pickup collection |

Artifact names above are under `Logs/RouteMap/` unless otherwise stated.
Runtime assembly SHA-256:
`8F010A387FA3FB82D4DA44FA52F56CA17F35D77A0B793AC0E59CFDAAD24303E8`.

Player journey checks fast-forward survival and kill spawned bosses through
the normal death/reward handlers, then use actual portal proximity and async
arena loading. They validate integration, not full-length balance or minimum
hardware performance. Existing shutdown ComputeBuffer/AssetBundle warnings
remain unisolated. Physical-controller navigation, the ten-Void catalogue,
Overseer/cutscene and live-run resume are outside this implementation.
