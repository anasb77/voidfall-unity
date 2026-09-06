# Music Remix Implementation Plan

> Execution: approved by the owner; implement in this session with focused
> subagent assistance for the independent audio processor and track analysis.

**Goal:** Make mechanics compose into recognizable temporary soundtrack remixes.
**Architecture:** Keep runtime as the source of gameplay facts; a plain C# event
envelope composes with the current MusicReactiveState. Audio-thread DSP receives
bounded, lock-free parameters and remains separate from combat SFX.
**Tech Stack:** Unity 6000.5.7f1, C#, existing music-only AudioSource and filters.
**Spec:** Docs/Design/2026-09-06-MusicRemix.md

## Global constraints

- Preserve soundtrack, combat RNG/structs, saves and unrelated working changes.
- Healthy overclock is 2x; low-health drag multiplies the active rate.
- Main-thread history; audio-thread buffers; allocation-free processing.
- No new packages, scenes or prefab wiring.

## Tasks

- [x] 1. Add failing composition and event-lifecycle tests; run focused EditMode.
  Implement `MusicRemixEnvelope` with `BeginMagnet`, `CollectMagnetGem`,
  `Step(dt, critical, gameplayActive, pulledCount)`, `NotifyOverclockStreak`,
  `Reset`, and read-only intensity/release/recovery/accent properties.
  Extend `Compose(state, pulse, magnetRelease = 0, recovery = 0,
  stackAccent = 0, recoveryWave = 0)` and mix BassBoost/Gain targets.
  Verify actual collected gems accumulate, ordinary XP cannot charge it,
  effects expire at 25 seconds, pause freezes history, and recovery requires
  sustained critical health. Existing priority test must now assert slowed
  overclock remains faster than unboosted critical and preserves both colors.
- [x] 2. Implement and sample-test bounded bass, wider stereo and bomb echo in
  MusicDspFilter / MusicSampleProcessor. Keep RequestBackspin/ResetHistory;
  add SetBassBoost(float), RequestBombEcho(float playbackRate). Test impulses,
  steady tones, reset, repeated requests, channel layouts and finite output.
- [x] 3. Integrate runtime pickup facts and pause into the director; preserve
  remix through Track Shift. Curate energetic start offsets in MusicTrackEntries.
  Add sustained green perimeter using its existing magnet argument and shader.
  Test real runtime pickup hooks, Greed, pause, reset and track switching.
- [x] 4. Run focused EditMode and PlayMode, create a separate Windows validation
  player, inspect rendered green perimeter, review focused diff and update map.

## Execution record

- Baseline: workspace has extensive unrelated changes, including main runtime
  and Sim partial. Their pre-task copies are in Logs/MusicRemix/Before.
- Ruling: use dedicated branch codex/music-remix-events in the current checkout
  so existing uncommitted prepared-arena dependencies remain available. No reset,
  stash, bulk commit or worktree that omits those dependencies.
- Ruling: combine bass and inward pull for Magnet; the owner approved all ideas
  without choosing between these complementary sound treatments.
- Interface review: tasks 1/3 share composer/envelope; task 2 owns only DSP files;
  track analysis owns only MusicTrackEntries and its tests. Parent serializes
  Unity runs and performs runtime integration. All tasks preserve the spec.
- Review: fixed limiter activation discontinuity, echo retrigger discontinuity,
  and a manual-loop volume spike during bomb duck. Scoped re-review found no
  remaining actionable issues. Added regressions reproduced failures first.
- Verification: 69 focused EditMode cases passed (`Logs/MusicRemix/editmode-final.xml`),
  6 runtime PlayMode cases passed (`Logs/MusicRemix/playmode-final.xml`). These
  cover actual Magnet collections, Greed, pause/focus, track changes, low-health
  threshold recovery, and the manual-loop mix boundary.
- Audio processor harness: 31 sample-level cases passed, zero allocations across
  10,000 stereo blocks, 0.0334ms per 1,024-frame block on this machine. See
  `Logs/MusicRemix/dsp-continuity-green.log`; timing is a local observation.
- Windows build succeeded with zero build errors. Player launched with an
  isolated capture profile and rendered low HP + overclock x2 before exiting.
  Evidence: `Logs/MusicRemix/build.log`, `player.log`, `player-overclock-critical.png`.
- Visual QA: inspected actual perimeter shader/component renders including
  neutral, full Magnet, reduced motion and combined critical/overclock/Magnet.
  These fixtures use synthetic spectrum inputs, not live gameplay audio.
- Listening limitation: waveform/continuity and runtime routing verified;
  subjective sound balance has not been auditioned. Owner listening is the next
  tuning step. No soundtrack files, combat state layouts or save schemas changed.
- Delivery: `../Builds/MusicRemix/VoidFall.exe`; current checkout remains on
  `codex/music-remix-events` with changes uncommitted, preserving unrelated work.
