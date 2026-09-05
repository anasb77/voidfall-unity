# Null City Unity implementation

Implemented September 5, 2026 from approved browser prototype V.

## Delivered

- Prepared `null-city` arena, menu preview, seeded route metadata and standard
  survive-five-minutes / defeat-Motherload objective.
- Twelve exclusive robots, including three lockdown police; Motherload remains
  outside the shared boss pool. Broodmother releases four Crawlers on death.
- Fixed city bounds, local Space/controller-left-shoulder dash, combined lockdown
  purge/hangar cycle, directional Marshal shield and alternating Suppressor shots.
- Motherload carrier with permanent lockdown, twice-normal incoming damage,
  cannon lattice, Event Horizon, drone deployment, bombardments and natural vents.
- Existing native revives, game-over, boss dissolution, physical reward pickup and
  route transition remain authoritative. No sandbox auto-heal/retry replacement.
- Approved offline-authored plates, animated unit frames, moving traffic/transit,
  searchlights, hangar/LCD props, pooled telegraphs and clean asset ownership.
- Bestiary IDs are appended to the save whitelist, preserving old discoveries.

## Validation

| Evidence | Result |
|---|---|
| Initial arena baseline | 14/14 EditMode passed |
| Authored exporter | 90 PNGs; reproducible hashes, exact dimensions/transparency |
| Unity bake | Plates, all sprite states/props, PPU/FullRect, three recipes validated |
| Full EditMode before concurrent overclock changes | 313/313 passed |
| Full PlayMode | 34/34 passed, including 9 city tests, pinned golden master and 32-seed sweep |
| Latest full EditMode after save addition | City-specific tests 35/35 passed; overall 311/321, with 10 overclock failures from parallel work |
| Final Windows validation build | Succeeded, zero build errors |
| Final player captures | Surveillance, lockdown, Motherload and tractor inspected; no runtime exceptions in capture logs |

The ten full-suite failures were in `MusicReactiveFeatureTests` (four overclock
multiplier cases) and `OverclockPresentationTests` (six newly added presentation
cases). They were not changed to hide failures. A later extra PlayMode launch
exited before test discovery while other Unity instances were active; the successful
34-test run remains the recorded full runtime result.

The final build found an HLSL reserved-name error in concurrently edited
`MusicPerimeter.shader`. Only the `point` parameter was renamed to `localPoint`;
the shader design was preserved. The build wrapper now rejects nonzero build-error
counts even when Unity labels the build result `Succeeded`.

## Try it

The Windows executable is at `../Builds/NullCityValidation/VoidFall.exe`.
It is a separate build; the regular `../Builds/VoidFall.exe` was not replaced.
Null City appears in the menu preview carousel and generated route pool.

Captures are in `../Builds/NullCityValidation/Captures/`:
`surveillance.png`, `lockdown.png`, `motherload.png`, `tractor.png`.
They use isolated adjacent profiles. Batch-mode player capture produced a black
image and was rejected; the final images use a hidden, non-batch Windows player.

Rebuild assets through `Tools/VoidFall/Bake And Register Null City`, or batch method
`VoidFall.Editor.NullCityContentBaker.BakeAndRegisterBatch`. The offline exporter is
`node Tools/NullCity/export-null-city.cjs`; its verifier is alongside it.

Implementation and tests are left unstaged so existing route, roulette, music and
overclock work in the active checkout remains reviewable together.
