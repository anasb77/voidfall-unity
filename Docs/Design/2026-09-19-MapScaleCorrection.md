# Camera and map scale correction

The owner reported excessive camera distance in Court and Hydra, and conflicting
scenery/player/camera proportions in Null City after the browser prototype was
integrated. The owner explicitly chose to restore Null City's original scale
throughout, superseding the September 12 4× geometry direction.

## Findings

At 16:9 without Velocity Coils, ordinary gameplay shows1728×972 world units.
Court instead showed3911.11×2200, even though its fixed board is3628.8 units
wide. Hydra I showed2468.57×1388.57, then returned to1728×972 for Hydra II.
Null City showed2390.85×1344.85 while scenery was enlarged4× and Zack's
artwork1.8837×. Court also enlarged Zack according to raw screen height,
changing his proportion to world geometry between resolutions.

Null City's renderer overwrote the normal smooth-follow camera with the
player's exact position. Both City and Court allowed camera framing beyond
their finite surfaces. These are code-derived mechanisms explaining the
reported mismatch; the earlier integration's passing stationary captures did
not establish consistent map feel.

The first corrected player capture also exposed a hardcoded1060×180 City LCD
overlay left over from the4× conversion. It now covers its authored265×45 inner
screen, while retaining higher-resolution text metrics.

## Correction

- All three use the normal gameplay zoom, including the existing Velocity
  Coils modifier. Hydra I no longer changes zoom on entering Hydra II.
- Zack and his cosmetics use their normal world scale across maps; there is
  no resolution-dependent enlargement.
- Null City's existing artwork returns to1600×900 world units with1240×526
  playable bounds. World/canvas conversions, props and hazard geometry use1×.
- Court retains28×28 tiles of129.6 units. Its camera uses ordinary smooth
  follow, constrained to the board. City uses the same constraint against its
  authored surface; an axis smaller than the viewport stays centred.
- No enemy speed/health, Court hazard timing, authored art, or Hydra II boss
  geometry/behavior was changed.
- Export context records map presentation version2; samples include the
  camera centre and count visible enemies relative to it. City policy events
  distinguish the restored scale from old4× exports.

## Verification

`Logs/map-scale-editmode.xml`:53 passed,0 failed (City rules, Court field rules,
Hydra runtime rules). `Logs/map-scale-playmode.xml`:48 passed,0 failed (City,
Court, Hydra travel, export integration, golden master and32-seed sweep).
The golden hash was not changed. `Logs/map-scale-windows-build.log` records a
successful Windows DX11 player build.
`Logs/map-scale-sign-playmode.xml`:12 City integration tests passed, including
the new sign-size/state regression (11 overlap the earlier runtime suite).
The existing map probe includes a Court corner pose and reports camera/world
extents and player scale. Diagnostics use isolated profiles and run exports.

Final rendered checks: `Logs/MapScaleCorrection/Verified1080p/mapcheck.json`
and `Verified720p/mapcheck.json`, each successful with7 captures. All phases
report1728×972 world-unit framing and the same74-unit player sprite canvas.
The probe verifies that active bosses are visible. The added corner pose must
restore the actual player position before choosing the Grandmasters' centre.
Build GUID: `926dffb843f94ab49cf3b6ffb1b2c52c`, built September20 at00:02:50
local. The verified payload is installed at `../Builds/VoidFall.exe`.
All218 copied files matched SHA-256 hashes before updating the launch path in
BUILD_INFO. Automatic approval review blocked deletion of the temporary
`../Builds/MapScaleValidation` copy ("blocked by policy"); cleanup remains.

Representative captures:

- [Null City](../../Logs/MapScaleCorrection/Verified1080p/01-null-surveillance-sign.png)
- [Court and Grandmasters](../../Logs/MapScaleCorrection/Verified1080p/04-court-grandmaster-warning.png)
- [Hydra I](../../Logs/MapScaleCorrection/Verified1080p/05-hydra-i-specimens.png)
- [Original Hydra II](../../Logs/MapScaleCorrection/Verified1080p/06-hydra-ii-original-boss.png)

Restoring the original City floor reduces traversal distances and concentrates
the existing roster in a smaller area. Difficulty and long-run crowding still
need ordinary playtesting; these changes do not retune the director.
