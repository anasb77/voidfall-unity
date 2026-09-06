# Orbital defense and merged supports

Implemented directly into the current integrated Windows player at `../Builds/VoidFall.exe`.

## Approved changes

- Clock's decorative face/rings/markings use .35 opacity. The moving damaging hands retain their existing .5 opacity and shape, per owner clarification.
- Velocity Coils retains its `projectileSpeed` ID and three ranks: +10% projectile/orbit speed and +5% camera dezoom per rank. Spatial Awareness is removed from the live catalogue.
- Scholar retains its `scholar` ID and four ranks: +8% XP and +5 percentage points to the existing power-up drop roll per rank. Fortune Magnet is removed from the live catalogue.
- Saved legacy support entries normalize to the surviving IDs, combining duplicate ranks by maximum and applying the new card's cap. Repeated sanitation is idempotent. Existing save-store backup and atomic-write behavior is unchanged.
- Blade and Clock rotation, plus Hollow Blade travel, use projectile-speed multiplier divided by weapon recovery. Recovery includes fire-delay upgrades and Overclock exactly once.
- Ordinary enemy projectiles can be stopped by physical blade/clock-hand contact. Boss, elite, meteor and unknown-origin shots cannot. The ability and exclusion appear in weapon/rank/evolution card descriptions.

## Implementation invariants

Shot origin is a separate slot array in GameSim, reset on every insertion and cleared on retirement/run reset. Enemy emission context is scoped with try/finally around each controller. Interception uses relative swept motion before player-impact resolution; Clock transforms synchronized motion into its rotating local frame rather than creating a shield over its whole swept sector. Hollow Blade tracks simulation positions with reset-safe history. No new fields were added to the golden-master-hashed entity structs.

## Verification

- Full EditMode suite: 427 passed (`Logs/orbital-editmode.xml`).
- Runtime suite: 28 passed, comprising 12 orbital regressions, 15 Arsenal integration checks and the 32-seed repeatability sweep (`Logs/orbital-validation.xml`).
- Main Windows player build: succeeded (`Logs/orbital-main-build.log`). No separate player build was created.
- Main-player capture run: 13 states completed using an isolated profile (`Logs/OrbitalMainCaptures/`). Clock's evolved rendering was inspected.

The existing pinned legacy golden test failed before this iteration: expected `14088908808337278323`, observed `17300903477073543990` (`Logs/orbital-baseline.xml`). The requested new recovery/interception behavior intentionally changes combat, but the inherited pin mismatch was not concealed by repinning. Repeatability passed; reconciliation of the inherited baseline remains separate work.
