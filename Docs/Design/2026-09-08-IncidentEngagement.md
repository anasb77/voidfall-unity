# Black Hole and Destroyer engagement tuning

The owner run from build `17127f1e74df473daccec32f1c0666fd` exposed two
specific opening problems: Black Hole started outside its own influence, and
the five Destroyers all died within 3.13 seconds of their common spawn.

Black Hole now locks its center 165 units ahead of current movement inside a
230-unit radius; stationary players retain the existing enemy-direction
fallback. A one-second warning explicitly asks the player to move beyond
the ring. Pull rises over 0.5 seconds, reaches 65% of
base movement speed through most of the disk, softens inside the central 8%
and outer 20%, and never overshoots the center. Ordinary outward movement
can beat its peak force. Enemies retain their stronger pull. The existing
authored shader uses the same radius mapping; its active boundary is clearer,
including reduced effects. Pause, safe flow, boss exclusion and release remain.

A steady runner crossed the previous warning area before attraction began:
even with the center directly ahead, the far edge is only 395 units away,
reached after 1.681 seconds at base speed 235 or 1.519 seconds at speed 260.
The actual MovePlayer regression confirmed zero exported pull at both speeds
with the previous 2.5-second warning. The shorter warning and ramp expose a
straight runner to attraction while a prompt outward juke can avoid it.
The center never follows the player after the warning appears.

Black Hole's active period remains 10 seconds and release remains 1.5, making
its total duration 12.5 seconds instead of 14. Destroyer Raid and Eclipse keep
their 2.5-second warning, 1.5-second ramp and original total durations. Core
lifecycle, half-release, invalid-step and admission tests use these explicit
per-kind timings; Black Hole's required incident-plus-boss-lead window is now
27.5 seconds instead of 29.

Destroyer role health is now Maw 360, Razor 270, Husk 760, Grasp 620 and Spite
320 before the existing enemy scale. Raids add a bounded multiplier from 1
to 2.5 across challenge seconds 0 to 900. This gives later strong builds a
larger durability budget without imposing that entire budget on early raids.
Entry distances are 300/300/230/250/340 respectively, allowing the slow melee
roles to reach their attack ranges earlier. Maw becomes ready to start its
unchanged 0.95-second warning after 0.2 seconds, before the other roles' 0.4s
arrival delay. This gives a charge warning an attention slot before Spite and
Razor compete for the remaining slot. Damage, speed, attack telegraphs,
recovery and the finite raid duration are unchanged. Damage reception remains
ordinary throughout; there is no arrival invulnerability or health floor.

Existing exporter events now report `incident_phase`, sampled
`black_hole_pull`, committed `destroyer_attack` transitions and
`destroyer_raid_resolved`. Pull amount is actual world displacement accumulated
over durationSeconds; detail gives final radial distance, radius, peak speed
and locked center coordinates. Samples are at most roughly one per second
plus a final remainder. Raid resolution reports survivors and elapsed active
raid time, with defeated/release/cancelled reason. Attack events link role and
spawn identity to incident sequence, with first/repeat reason, age and health.

`IncidentEngagementIntegrationTests` covers pull, escape, core stability,
warning/paused/cancelled states, rendered radius, sampled JSONL telemetry and
damage during freeze followed by resumed attack. Its late-raid combat probe
uses the recorded checkpoint Pistol/Scattergun/Arc/Mines/Clock evolved VI
build and exact recorded support ranks, zero Workshop, a fixed seed and
pressure corresponding to the raid's old 2.62 health scale. It checks an
actual first attack before 3.13 seconds, a survivor at that old raid endpoint,
and defeat within the 32-second active window. Its strengthened acceptance
requires Maw and Spite first attacks and at least 100 units of actual Maw
sweep displacement while alive. This stationary combat probe
is not a replay of the owner's movement or proof of subjective difficulty.

The escape regression uses the existing DirectorPlaytestInput override with
a fixed outward pickup goal to drive the real MovePlayer path, including
acceleration and force ordering. It restores the prior override afterward.
Running-entry tests begin at real movement velocity and maintain straight or
outward diagnostic input through MovePlayer, then inspect actual pull JSONL.
They require more than 20 units of pull for straight running at 235 and 260,
zero pull for the outward warning response, and an eventual escape. A vertical
velocity test verifies placement follows initial movement and remains locked.
A Pistol-I first-void probe checks
survivors reach release and that the complete incident leaves no bodies or
hostile shots. Its player has infinite iframes to isolate cleanup; it makes no
claim that the weak build can comfortably survive or defeat the raid.

For an isolated native visual check, the existing arsenal validation launch
accepts `-vfincident=black-hole`, `-vfincident=raid` or `-vfincident=eclipse`
alongside `-vfarsenal=all` (and optional rank/evolution flags). The incident is
forced after the existing validation run starts. The flag alone does not
create a new startup/profile path; existing validation profile isolation stays
authoritative.

Validation is coordinated by the parent task. `Logs/incident-refined.xml`
passed 19 selected cases, including real movement escape, frozen warnings and
weak-loadout withdrawal. The strong probe reported raid HP 15,262, three
survivors at 3.13 seconds, Maw's first charge at 1.16 seconds with 186 units of
actual sweep movement, Spite at 3.54 seconds, and defeat at 8.46 seconds.
`Logs/black-hole-running-red.xml` confirmed both straight-running cases fail
with zero pull before the Black Hole timing correction; the outward juke
passed. Final green running-entry and updated Editor timing gates are pending.

No Unity Editor or player was launched by this implementation worker. Actual
engine-free incident rules compiled using PowerShell Add-Type and passed six
force samples plus activation/completion checks. An optional dotnet compile
could not begin because generated project.assets.json was absent; the parent
Unity gate supplied compilation and runtime evidence for the initial tests.

Final parent gates: `Logs/freeze-balance-final-editmode.xml` 575 passed;
`Logs/freeze-balance-final-playmode.xml` 276 passed, zero failures, one graphics
skip. Includes the real movement and first-void release checks. The final
strong fixture records Maw at1.16s, Spite at3.54s,186 units of actual Maw dash,
three bodies alive at3.13s and complete defeat at8.46s. Straight running
accumulates133.27 units of pull at235 speed and59.02 at260; deliberate outward
juke records0. All escape. Existing golden hash and32-seed sweep pass unchanged.

Built player GUID `aa3ed9c818b748ae95d3398eb611fc9c` was visually inspected in
both Black Hole warning and active phases on DX11. Pull visibly shifted the
player toward the center while the camera tracked it. The isolated visual
player closed through Alt+F4; its logs/exports are under
`Logs/IncidentPlayerVisual`. No art assets or arena geometry were replaced.
