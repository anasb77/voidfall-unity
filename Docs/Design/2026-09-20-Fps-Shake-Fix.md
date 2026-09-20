# World-grid and camera-impulse repair

Owner approved implementation after the FPS/shake investigation. The canonical
Windows build remains the only release target. Enemy counts, director cadence,
damage, separation passes and gameplay hitstop are retained.

## Spatial queries

CollisionGrid now stores exact signed world-cell coordinates in preallocated
open-addressed buckets sized to at least twice actor capacity. Cell size stays
72, query traversal stays X then Y, and each cell retains insertion order.
No per-step dictionary/list allocation or far-world coordinate clamping is
introduced. Storage remains bounded even when the player travels indefinitely.
Queries clip to occupied cell bounds, not a fixed world rectangle.

The matched 650-body standalone .NET benchmark retained11,124 candidates per
pass at both origin and(-3600,-5040). The previous implementation returned
422,500 at the distant offset. In this local run the far query/filter pass
measured6.017ms before and0.321ms after; this is a component benchmark, not
a claim of an equivalent whole-game FPS multiplier. Near-origin measurements
were0.252ms before and0.327ms after. Raw output: Logs/PerfShake/grid-comparison.txt.

## Camera presentation

CameraImpulse is pure presentation state, reset at run start and advanced once
per rendered frame using unscaled time, before new combat requests arrive.
Ordinary requests arriving within75ms strengthen a single pulse with diminishing
returns; they do not extend its160ms envelope. A210ms start interval leaves
settling gaps under sustained fire. Ordinary amplitude is capped at4.5 world
units. Major accents use a280ms envelope/340ms interval, capped at13; combined
motion is bounded at14. A bomb/boss request can replace a weaker major accent
once, but repeated equal requests cannot continually restart it.

Kill/exploder impulses recoil away from their location and scattergun/railgun
fire recoils against the firing direction. Time-based damped oscillation
replaces independent per-render-frame random displacement. The user's slider
scales displacement linearly. Pause and reduced motion suppress it immediately.
Local particles, sound, damage, freeze durations and authored escape tremor
remain in their existing systems. Ordinary camera motion consumes no RNG.

## Observation and checks

Existing run exports report grid/shake versions and timing version3. Bounded
sample windows include simulation stages, fixed/frozen step counts and
ordinary/major request counts, peak envelope and active-time fraction.
StressBenchmarkProbe accepts-vffargrid with-vfbench to place its isolated
combat fixture at(-3600,-5040); the report records farGridRequested.

Focused tests cover translated query identities/order, full capacity, sparse
cells and reuse, a40-second100-kills/sec pulse train, frame-rate independence,
direction, resetting, major accents, runtime settings/hitstop and JSON/JSONL
exports. The existing golden master and32-seed sweep remain required; no
baseline is changed merely to hide drift.

## Completed validation

57 focused tests passed:9 EditMode and48 PlayMode, including the unchanged
single-seed golden and32-seed determinism sweep. Results are in
Logs/PerfShake/edit-final.xml and play.xml. Canonical build succeeded at
2026-09-20 17:35:49 +01:00, GUID12014dd1e7874cb6b3648c3afe7fa1ca.

Native1920x1080 DX11 productionMax stress runs used the main executable on
secondary DISPLAY1 (window bounds -1920,0 to0,1080), with an isolated profile.
Both matched12-second measurement windows maintained750 actors at every
post-refill boundary, zero capacity failures, damage/kills and advancing combat:

| Position | Median frame | p95 frame | Mean simulation/frame | Combat advanced |
|---|---:|---:|---:|---:|
| (-3600,-5040) |16.55ms|24.29ms|6.74ms|7.50s|
| Origin |16.42ms|27.38ms|7.14ms|8.59s|

Both reports valid=true. These are short synthetic, rendered stress windows,
not identical combat replays or guarantees for every natural run. Remaining
frame spikes are observed and are not claimed eliminated. The original longer
attempt became invalid when its defeated bosses opened roulette; that report
is retained as far.json and is not used as a completed performance pass.
Far-combat peak sampled shake was11.82 world units, with about47% mean sampled
active fraction. The strong full-frame shake is not continuously pinned.

Reports/capture/logs live under the session visualization folder perf-shake:
far-combat.json, origin-combat.json, far.png and RunExports. The captured
frame was inspected; both successful player logs had no matched runtime
exception/error. Windows display preferences were restored and verified, and
the real profile SHA256 remained unchanged. No separate player or release
executable was created.
