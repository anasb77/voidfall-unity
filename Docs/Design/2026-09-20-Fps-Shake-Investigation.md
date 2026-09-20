# FPS and sustained shake investigation — September 20

Investigation only. No gameplay code or canonical build changed for this request.

## Run evidence

Latest available ordinary run: 79dabfe3701447f68c34d2d6aa235ce8,
build 32676184283c49388144867249a9aab7, loot policy 2. This predates the
latest HUD/merchant/two-second loot build. Source and journal were inspected;
no new native player was launched. Hardware recorded: i7-7700HQ, GTX 1060,
1920x1080, DX11.

At 210–240 combat seconds sampled FPS remains 60. At 270–300 it averages
49.9; at 300–330 it averages 18.2. These are arithmetic sample averages,
not wall-time-weighted frame statistics. The worst observed samples reach
5 FPS, with about 195 ms in simulation and 3.3 ms in render-script work.
The latter excludes actual GPU timing. Several severe samples have zero
generation-zero collections, so collection alone cannot explain the collapse.
The preceding ordinary run d2153e0d54da4521870c8ec02f2adb7b stays near 60 FPS
through its 274.64-second end: there is no universal four-minute FPS switch.

## Reproduced grid defect

CollisionGrid.cs fixes its 64 by 64 grid at world cells -32 through 31,
72 units per cell. CellCoordinate clamps all outside positions to an edge.
In an unbounded arena, leaving both axes collapses geographically separated
enemies into a corner bucket. Neighborhood queries then return the entire
bucket. Separation performs three passes per simulation step; fixed-step
catch-up allows three simulation steps per rendered frame.

The slow run's player moves from approximately (-1888,-4065) at 241.78s to
(-3297,-5386) at 306.06s. At the latter sample there are 638 enemies,
5 FPS, and about 168 ms of simulation work.

Compiled the repository's actual CollisionGrid source with PowerShell Add-Type
and queried an identical synthetic 650-body layout at (0,0) and translated
by (-3600,-5040), an integer-cell offset. Bodies were spaced 48 units in
26 columns, with a one-cell neighborhood and identical distance filtering.
Near origin: 11,124 candidates/pass, mean 17.1 candidates/body.
Far offset: 422,500 candidates/pass, mean 650 candidates/body (38x).
150 iterations measured roughly .195 ms versus 3.956 ms per query/filter
pass in desktop .NET, not Unity. These timings are NOT an in-game FPS
prediction. Exact candidate counts demonstrate translation-dependent work.
The defect is reproduced; contribution to the full native slowdown still
needs a before/after Unity capture with stage-level profiling.

Recommended repair: a bounded-allocation spatial index keyed by actual world
cell coordinates, with deterministic traversal and identity checks. A moving
origin is also possible but must explicitly handle distant actors; merely
enlarging the fixed grid postpones the defect. Preserve three-pass separation,
combat order, density and damage, and verify translated-layout queries plus
the existing golden/sweep contracts.

## Shake mechanism and limits

AddCameraShake adds every request to one clamped trauma value. Decay is
1.7 per unscaled second. Ordinary deaths add .055, brutes .12, scattergun
volleys .12, railgun volleys .24, rail impacts .022; explosions and elites
add stronger requests. These share one accumulator without event grouping
or a lower budget for ordinary kills. Approximately 31 ordinary kills/sec
alone can match decay at the default setting. Actual 20-combat-second
journal bins, including pauses/slowdown in elapsed wall time, contribute
roughly .8–1.1 trauma/sec from ordinary death requests alone, before weapon
and explosion requests. This supports saturation as a mechanism, but does
not prove a specific 40-second saturated interval.

CameraShakeOffset samples independent random X/Y each rendered frame, with
amplitude 14*trauma^2 world units. At the recorded 972-world-unit viewport
height and 1080 pixels this allows about +/-15.6 pixels on each axis at
full trauma. Low frame rates present these offsets as coarser jumps.
No shake-request, trauma or freeze-duty telemetry exists in these exports.
The expensive CPU computation is not shown to originate in camera shake.

Proposed presentation changes, not yet implemented:

1. Coalesce ordinary kills within a short 60–100 ms window into one pulse;
   scale with diminishing returns. Give a clock sweep one coherent accent.
2. Use separate ordinary-impact and major-event budgets. Sustained automatic
   fire cannot hold boss-strength shake; major events remain distinguishable.
3. Give pulses a fast attack and 120–200 ms settling envelope, with directional
   impact and time-based smooth noise instead of fresh frame-random offsets.
   Continuous kills may sustain subtle vibration but must not lock maximum
   displacement. Exact tuning requires comparison in a real player.
4. Preserve local death effects, audio, HUD stability, shake settings and
   reduced-motion support. Leave gameplay hitstop unchanged during this pass;
   freeze affects enemy movement and invulnerability, not just presentation.
5. Add bounded per-second shake source/peak/duty and simulation-step/subsystem
   timings to the existing exporter before claiming a complete FPS/shake fix.

Next implementation order: repair and profile grid behavior first, then
compare current and proposed shake under matched weapon/kill bursts.
