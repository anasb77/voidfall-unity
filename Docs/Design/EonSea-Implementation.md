# Eon Sea and final rosters — Unity implementation

Implemented in the active Unity project on September5,2026. The corrected identity is authoritative: **Regular I is the original star**, II–IV evolve from it, and Dasher retains its original single design. Technical IDs and existing profile schema are preserved.

## Native gameplay

Eon Sea is the seventh prepared Void. Its continuous, camera-following glacier field has physical cover, persistent visit-local melting, explosion-accelerated cracks, warned collapse pulses, and slippery movement patches. Collapse slows nearby player/enemies/bosses50% for20seconds without damage or stacking the multiplier. New freezes and melting stop during native reward collection. Normal control, scene travel, revives and saved results remain owned by the existing runtime.

Eon Sea uses native five-minute survival and a random shared-boss encounter. It joins the normal branch graph: seven prepared destinations, six visited on a path. The browser's90-second duration, manual study grenade and lab/gallery controls are not production mechanics; native explosive weapons, pickups and enemy/boss blasts drive glacier stress.

All14shared enemy families and the three existing elite families now support tiersI–IV. Global run time drives II9–15minutes, III24–30, IV34–40. Higher tiers implement the approved role traits, including warning-preserving dash chains, support ranges/target counts, higher-tier offspring, shields, artillery groups and curved volleys with a rotating gap. Native tierI behavior and exclusive Void pools/bosses are retained. Controller sidecars are keyed by SpawnId and reset on slot reuse.

## Art and assets

The imported art includes4ground textures,32glacier stamps, slippery ice, prepared plate/detail layers, and68shared/elite roster forms. `Tools/EonSea/export-eon.cjs` snapshots the corrected approved code art and exports PNGs offline. `EonSeaContentBaker.BakeBatch` imports and validates dimensions, configures native sprites, creates visual assets and registers three Addressables recipes. Eon views detach before package release; shared roster sprites use the existing global Resources pattern.

## Validation evidence

- Baseline EditMode:326/326passed, `Logs/eon-baseline-editmode.xml`.
- Final EditMode:365/365passed, `Logs/eon-final-editmode.xml`.
- Final full PlayMode:62/62passed, `Logs/eon-confirm-playmode.xml`, including32-seed deterministic sweep and glacier/roster runtime integration.
- Windows build succeeded with0errors, `Logs/eon-capture-build.log`.
- Inspected actual rendering-player captures: terrain, frost, late shared roster, elites and shared Warden boss. All five capture processes exited0. Active captures show simulation advancement; no gameplay exceptions appeared. Existing D3D12 debug-info and shutdown ComputeBuffer disposal warnings remain unrelated engine/runtime diagnostics.
- Focused independent review found no actionable terrain, freeze, streaming, collision, renderer ownership or asset-release issues.

The golden master intentionally changed because `productionMax` starts at1500seconds (25minutes), where approved newII/III controllers are active. The32-seed sweep passed before re-pinning. New roster state does not alter EnemyState's reflected schema. New full hash14161069325177094174; legacy meteor-schema hash14088908808337278323. The reason is recorded beside the expected values.

## Delivery

Windows build: `../Builds/EonSeaValidation/VoidFall.exe` (normal existing build preserved).

Captures: `../Builds/EonSeaValidation/Captures/{terrain,frost,late,elites,boss}.png`.

Diagnostic poses use `-vfeonsea=terrain|frost|late|elites|boss -vfcapture=<path> -vfcapture-quit`, isolated profiles and invulnerability. They are visual validation poses, not ordinary-play shortcuts. This opt-in capture path resumes hidden-window focus pauses; normal focus/pause behavior is unchanged.

No package changes, main scene rewrites, save migration or unrelated work restoration was performed. Concurrent nebula/HUD/escape changes were preserved. Changes are in the working checkout and were not committed as part of this task.
