# SDD ledger — plan: Docs/Design/2026-09-07-DirectorImplementation.md

Baseline acf5103, isolated worktree codex/director-redesign-2026-09-07. Owner approved full implementation Sept7. Unity baseline EditMode444/444 passed; PlayMode running in original checkout. Parent is sole Unity launcher.

Baseline follow-up: PlayMode131 passed/1 failed/1 graphics-only skipped. Failure is the pre-existing golden pin: expected legacy14088908808337278323, actual16175583525682867059; full actual2219481193901741817. Isolated golden-only run reproduced EXACT same values. The32-seed test passed in full baseline suite. Pin last changed9696676; subsequent acf5103 intentionally added orbital projectile interception and consolidated support behavior without re-pinning. These are established baseline changes, not this redesign's regression. Preserve evidence before any later hash update. Unity test teardown deleted tracked Addressables link.xml/meta; restored only those known test-generated deletions in original clean checkout.

| Task / shared interface | Check / ruling |
|---|---|
|1 internal rules/tests|Exact hundredths, stage high-water,64-bit score, profile thresholds agree; tests include saturation and freeze.|
|1→2/3 profile/pressure|Task1 owns new pure files. Parent runtime consumes exact API; no raw-time score/pressure divergence.|
|2↔3 Sim/main hooks|Parent owns shared edits; delegated work uses new partials or clearly handed-off files.|
|2↔4 encounter/event attention|Major events replace encounter demand and only admit at resolved legal boundaries; no independent stacking timer.|
|3↔5 reward provenance|Terminal safe reward settlement remains eligible; rival-only damage bypasses player modifiers/stat attribution.|
|4↔5 event lifecycle|One major incident owns state at a time; latest five-creature art overrides earlier rejected browser variants.|
|2–5→6 validation|Intentional combat changes can drift hashes;32-seed explanation precedes re-pin.|
|2 internal|Baseline measurement plus scoped encounter fixes; no capacity increase mistaken for denser/better gameplay.|
|3 internal|Pressure freezes at combat end, score after already-earned reward settlement; result snapshot reused.|
|4 internal|Supplied shader requires adaptation; validate appearance without global transparent-buffer assumptions.|
|5 internal|Three factions, five art designs; initial tiny squad proves sources before expansion.|
|6 internal|Current laptop only reference; missing minimum hardware remains reporting limit, not blanket implementation blocker.|

Ruling: broad approval covers the complete approved systems and latest preview, but not previously excluded online backend/co-op/new-tree/gameplay-blackout ideas. Worktree creation was included in approved proposal; no renewed permission question.

- [x] Task1 pure rules: 271d9f0 + 994e5e5; task review passed after independent-valid-channel/default-struct floor fixes; Unity 18/18 cases pass.
- [x] Task2 ordinary encounters/diagnostics: 69cfa86 + 06839de + cf2ba25; bounded encounters, attention limits, reinforcement waves.
- [x] Task3 pressure/results/onboarding: 45e5eb6; save lock recovery, terminal tick score credit, fresh-player layout.
- [x] Task4 BlackHole/Eclipse/admission: 89dd90e; incident lifecycle, Black Hole attraction, Eclipse dimming, 45 EditMode cases pass.
- [x] Task5 Destroyers/provenance: 1d6b404 + ab26137; 5 archetypes, reciprocal faction combat, finite reward roots.
- [x] Task6 integration/review/build/performance: full EditMode (525/525 passed, 0 failed), PlayMode (169/169 passed, 0 failed, 1 graphics-only skip), 32-seed repeatability sweep bit-exact pass, golden master re-pin (legacy 584744233380640504 / full 2158461941832927523), standalone Windows player built successfully at Builds/DirectorRedesign/VoidFall.exe.

Ruling: prepare Task4 pure lifecycle and renderer independently while parent proves Task2 ordinary combat; do not admit or use events to cover baseline pacing. Named ownership in task-4-render-brief prevents shared runtime edits. Source rendering requires scoped adaptation of supplied ShaderGraph, not a global Opaque Texture toggle.

Parent diagnostics TDD red: DirectorDiagnosticsTests failed as expected because PrepareBenchmarkFrame did not exist. New diagnostic implementation now under Unity integration test. Native creature export:40poseframes, final approved artwork provenance in Tools/DirectorArt.

Validation completion summary: All 6 tasks of the Director, Pressure, Encounters, and Events Redesign are fully implemented, integrated, and verified across both EditMode (525 tests) and PlayMode (169 tests) suites with 0 failures and 32-seed bit-level determinism confirmed. Standalone player artifact verified.
