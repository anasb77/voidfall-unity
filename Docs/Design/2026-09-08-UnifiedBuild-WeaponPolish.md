# Unified Windows build and weapon polish

Owner scope: one current Windows player at `../Builds/VoidFall.exe`, a fresh
Windows Last Modified timestamp, four starting weapon slots and a fifth after
two weapons reach rank VI, smaller boomerangs, and Clock polish. The later
owner correction sets Clock face opacity to **18%**.

## Behavior

- Capacity is four initially and five after two rank-VI weapons. The Pulse
  Pistol still occupies the first slot. Capacity makes new weapons eligible;
  the existing weighted offer system still chooses the cards.
- Boomerang render dimensions and contact radius both halve. Damage, return
  behavior, chain count, cooldown and travel speed retain their prior rules.
- The Clock face renderer uses 0.18 alpha. Original hand size/opacity remain
  unchanged. Rank III adds a half-size seconds attack hand at twice the main
  rotation speed, half reach and half damage, with its own repeat-hit timers.
  The evolution retains its full-size counterclockwise hand. Three stable
  hand slots share activation/size/speed rules across damage and rendering;
  ordinary-shot interception uses the same geometry. Transition/reset hides
  all three hands and clears hit identities.
- Existing run exports identify the progression and arsenal balance policy.
  Weapon ranks and damage history allow subsequent balance comparisons.

## Delivery

`BuildScript.BuildWindows` owns the canonical player output. Legacy feature
build entrypoints delegate to it while retaining required baking steps.
Only a successful Unity player build updates the executable timestamp and
`BUILD_INFO.txt` (build GUID, timestamp, source project, key policies).

After replacement validation, remove the three obsolete players under Builds:
`DirectorBaseline-acf5103`, `DirectorRedesign`, `MusicRouletteRevision`.
They were inspected and contain player payloads, with no saved profiles or
RunExports. The canonical `RunExports` directory and source worktrees stay.

## Verification

Fresh Unity 6000.5.7f1 validation:

- `Logs/polish-red.xml`: rank-III regression fails before implementation
  (expected three hand slots, found two).
- `Logs/unified-final-editmode.xml`: **573 passed, zero failures**.
- `Logs/unified-final-playmode.xml`: **251 passed, zero failures, one graphics
  skip**. Includes real rank-III damage/interception boundaries, 18% face and
  unchanged main-hand opacity, boomerang size, transition cleanup, actual
  second-rank-VI upgrade/export, golden master and 32-seed repeatability.
- Existing golden hashes remain unchanged; no re-pin was necessary.

`Logs/unified-windows-build.log`: successful Windows build, 256837789 bytes,
GUID `17127f1e74df473daccec32f1c0666fd`. Executable Last Modified is
2026-09-08 19:00:07 +01:00, matching the generated `BUILD_INFO.txt`.

The canonical executable launched and completed all 14 authored weapon
captures in `Logs/UnifiedArsenal` with an isolated profile/export directory.
Rank-III Clock and rank-VI Boomerang were visually inspected. Its actual
exports confirm the same build GUID, 360-second survival and 4/5 slot policy.
Player shutdown logs a ComputeBuffer disposal warning; no capture failure.

Cleanup remains blocked: automatic approval review rejected both the combined
validated cleanup command and explicit `Remove-Item -LiteralPath` deletion of
the three known duplicate folders, stating only `blocked by policy`. No
deletion occurred. The three obsolete folders still require manual removal.
All 16 canonical RunExports files retain their original SHA-256 hashes.
