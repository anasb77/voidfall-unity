# Integrated build — 6 September 2026

Consolidates the current checkout's arena, shared roster, Arsenal, Workshop,
escape-window, presentation and music-remix work. The old migration branch is
already an ancestor. The audit branch's unique commit is content-equivalent to
the landed audit fix (apart from a regenerated Addressables meta GUID); merging
it again would not recover features.

## Fixes from integration review

- Eon Sea: the generic three-pass enemy separation ran after glacier collision
  resolution and could push enemies back inside ice. The Eon path now sweeps each
  separation displacement against terrain. It retains separation and does not
  apply freeze movement scaling twice. Non-Eon simulation is unchanged.
  `Tests/PlayMode/EonSeaIntegrationTests.cs` reproduces the defect with both one
  and three passes; both failed before the fix and pass afterward.
- Crascendo: the grown-hitbox area-damage regression test still invoked the old
  five-argument private method. Its reflection call now supplies `critical=false`.
- Escape countdown: `UpdateJourneyFlow` capped every elapsed frame at 0.1 seconds,
  stretching the 35-second window at low frame rates. The standalone route probe
  timed out in Monochrome Court's reward phase. The countdown now uses elapsed
  active time, with movement/pickup motion still capped and pause/reward guards
  retained. `Tests/PlayMode/EscapeWindowTests.cs` reproduced 30 seconds remaining
  after 25 elapsed seconds before the fix; it also checks pause and completion.
- Golden fixtures: `Tests/PlayMode/SimulationProfileScope.cs` uses a temporary
  default save, restores the real profile/store afterward and prevents Workshop
  purchases from contaminating the fixed-seed and 32-seed fixtures.

Paths above are relative to `Assets/VoidFall/`. Runtime changes are in
`Runtime/Gameplay/VoidFallGameRuntime.cs`, `.EonSea.cs`, `.Journey.cs` and `.Rift.cs`.

## Open gate: pinned simulation baseline

The single-seed golden test already failed before this review. Its expected
legacy/full hashes remain **14088908808337278323 / 14161069325177094174**.
The original real-profile run produced **7793928657042714437 /
6704778439981554091**, also recorded by the earlier Arsenal task. Isolating the
profile produces **17300903477073543990 / 2814646760629068057**. These are
repeatable, but repeatability alone does not justify replacing the old pin.

Targeted investigation ruled out the Eon-only fix, new active Arsenal weapons,
catalogue size alone, and historical-versus-default save JSON. The headless
viewport is 640×480 and automatic quality is off. A temporary 16:9 comparison
also did not recover the old pin. Preserved Crascendo player IL matches current
GameSim/FxSim, original controllers, stress setup and hashed struct schemas;
remaining changes are Arsenal hooks and music/presentation integration.
Executing old player assemblies in the Editor was inconclusive because of
incompatible music APIs/asset type registration; all diagnostic substitutions
were restored. No expected hash or test was disabled to hide this gate.

Before declaring a release clean, reproduce the old pin under controlled
viewport/input/quality conditions and locate the first differing simulation
state. Prefer this narrow investigation over repeating a repository audit.
The small reference DLLs and earlier passing test XML are retained locally in
`Logs/Integration-2026-09-06/reference-crascendo/`; full old player copies are
not needed for the IL comparison.

## Verification

- EditMode baseline: 423/423 passed.
- Final PlayMode suite: 101 passed, one pinned golden failure, one graphics-only
  skip. This includes the new glacier and slow-frame escape regressions and the
  passing 32-seed repeatability sweep.
- Graphics-enabled Workshop suite: 8/8 passed, including the headless skip.
- Final Windows player build: succeeded, reported size 232,698,876 bytes.
- Standalone journey: Abyss → Eon Sea → Null City → Monochrome Court → Hydra →
  Crascendo; returned to the main menu and saved exactly one run. The probe
  advances objectives/kills bosses diagnostically; this validates travel and
  completion, not an ordinary player balance run.

This is an integration checkpoint, not a clean release gate. The combined source
is published on `codex/integrated-build-2026-09-06`; `main` is not promoted while
the pinned simulation regression remains unresolved.

## Build and storage policy

The canonical integrated player is `../Builds/VoidFall.exe`. Old feature players
and build backups are disposable after replacement validation. `Library/` is
regenerated on the next Editor open; `Assets/VoidFall/Generated/` is required
baked content and must remain. Keep source, Git history, design tools, prototypes,
art, soundtrack and real saves. Detailed local validation evidence is retained
under `Logs/Integration-2026-09-06/` (ignored by Git).
