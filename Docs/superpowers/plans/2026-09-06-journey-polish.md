# Journey Polish Implementation Plan

> Execute the approved work continuously with focused implementation and review; keep Unity test/build ownership in the primary agent.

**Goal:** Make Void completion, route planning and actual arena arrival reliable and visually coherent.

**Architecture:** Runtime Journey/Rift own transitions. A small pure presentation-rules helper controls the escape envelope/pattern. The map consumes immutable route metadata and small baked thumbnails; the route owns a dedicated seeded RNG.

**Tech stack:** Unity 6000.5.7f1, C#, uGUI, Addressables, NUnit.

**Spec:** `Docs/Design/2026-09-06-JourneyPolish.md`

## Global constraints

Preserve existing saves/arena IDs, combat RNG/iteration and golden master. Work only on journey dependencies. Do not overwrite the baseline's unrelated Arsenal/Support work. User's explicit approval covers this implementation.

## Tasks

- [x] Runtime regression: exercise `ArenaIdForVoidId("crascendo")` and `ArenaIdForVoidId("eon-sea")` and assert actual destination enums; exercise escape through existing runtime fixture with distant XP/Parts, queued levels and Overclock. Run before edits.
- [x] Escape implementation: update Journey/Rift and a focused `.Escape.cs` partial; stop projectiles/hazards but queue remaining enemy IDs for staggered retirement. Sweep XP/Parts through normal pickup callbacks before leaving. Drive animated status and three deterministic shake patterns outside combat ticks. Remove only the redundant post-roulette prize screen and maintain exactly-once ownership.
- [x] Route implementation: update `Content/PlayableVoidRoutes.cs` and `Core/VoidRoute.cs` with explicit arena identity and seeded split/reconnect layouts. Add tests proving identical seed replay, varied topology, six nodes per path, no arena repetitions, terminal reachability, and no combat RNG consumption. Adapt only immediate runtime identity consumers.
- [x] Map implementation: replace `UI/Views/RouteMapView.cs` presentation with thumbnail nodes and minimal copy. Preserve `Show(VoidRouteRun, string, Action<string>, Action)` and physical-commit ownership. Add a bounded Editor thumbnail baker using existing arena assets, then produce its Resources output.
- [x] Portal integration: load neutralized existing portal animation in destination colors; names only, planned/approach emphasis. Keep asset disposal and pause/retry behavior intact.
- [x] Validation: focused EditMode and PlayMode, existing journey regressions adjusted to the approved contract, golden master and 32-seed sweep, Windows build and map/escape/junction/arrival captures. Review complete task diff. Apply only the task diff to the active Unity project, preserving concurrent edits, and verify the integrated build.

## Progress

Baseline snapshot: `e649703deee98f6ba03e999771ad2b8ddfc13274`. Plan/design approved in conversation. Current focus: regression and narrow dependency inspection.

### Validation checkpoint

Full EditMode: 438/438 passed. First full PlayMode: 115 passed, 7 failed, 1 skipped. Task failures include outdated popup/terminal fixture assumptions; review also found warning renderer reactivation, portal/map mystery inconsistency, full-pool Part grant bypass, and promoted-pawn StoredXp reuse. These are being corrected before delivery.

The golden-master failure is reproduced unchanged on pre-task C# snapshot e649703: expected14088908808337278323, actual16175583525682867059 in both `playmode.xml` and `golden-baseline.xml` under `Logs/JourneyPolish/`. No hash re-pin is permitted. All temporarily restored baseline C# was restored to the working implementation afterward. This is an existing integration failure, not introduced by journey changes.

Final isolated validation: EditMode438 passed; PlayMode125 passed, one existing graphics test skipped, only the unchanged pre-task golden mismatch remains. The32-seed deterministic sweep passed. Scoped review is clear, including explicit arena metadata and exactly-once reward effects. Integration/build/captures remain.
Delivery complete: active-project source integrated, Windows player rebuilt and native map/escape/arrival captures inspected. See Docs/Design/2026-09-06-JourneyPolish-validation.md for exact checks and the unchanged pre-existing golden-reference gate.