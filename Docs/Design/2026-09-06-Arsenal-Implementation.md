# Mines, Summons, Clock and Boomerang — Unity implementation

Implemented from the owner-approved browser prototypes and final six-rank tables. Weapons append at indices 6–9; the original six IDs are preserved. Hand-authored data extends the generated catalogue through its existing static initialization.

## Player behavior

- Mines arm after 0.55 seconds, expire after 15 seconds, and detonate by proximity. Permafrost Mines freeze ordinary enemies for 2.4 seconds; bosses take damage but resist freeze.
- Summons spawn 2–3 friendly rushers. With no eligible target within 420 units of the player, they persist in hover formation. Idle creation stops at squad size; surviving combat summons remain within the bounded pool. Volatile Brood adds impact explosions.
- Clock rotates clockwise; Hazard's Clock adds an independent counterclockwise hand. Its face and hands retain 50% opacity. Per-target hit gates prevent a hand from dealing damage every simulation tick.
- Boomerang hits distinct spawn identities, then returns harmlessly. Triple Return throws three projectiles per activation.
- Each rank has distinct authored pixels. Cosmetic details do not change collision geometry; ordinary support modifiers still apply. Mine range guides retain 70% of their previous opacity.

## Integration

Content: `Assets/VoidFall/Content/ArsenalContent.cs`.

Runtime: `VoidFallGameRuntime.Arsenal.cs` and `.Arsenal.Render.cs`. Fixed-capacity pools, spawn-identity status sidecars, and generation guards preserve slot reuse and combat-clear callbacks. Art uses `ProceduralSpriteFactory.Arsenal.cs`, warms during upgrade stat recalculation and uses integer cache keys on the render path.

Evolution pairings: Mines/Amplifier, Summons/Reflex Matrix, Clock/Cycle Tuning, Boomerang/Velocity Coils. Weapon and paired support must be maxed. Extended supports are included in evolution lookup and the build HUD. Roulette new-card acquisition now respects the weapon slot limit.

## Verification

- EditMode suite: 379 passed (`Logs/arsenal-release-editmode.xml`).
- Arsenal PlayMode integration: 15 passed (`Logs/arsenal-verified-playmode.xml`).
- Existing 32-seed repeatability sweep: passed (`Logs/arsenal-validation.xml`).
- Separate Windows build: succeeded, zero build errors (`Logs/arsenal-verified-build.log`).
- Unity player captures cover rank I, VI, evolution and idle summons. Capture profiles are isolated before initial load.

The pinned legacy golden test remains a separate unresolved gate. With the new catalogue entries and simulation/render hooks temporarily disabled, it produced the same mismatching hash as with the feature enabled: legacy `7793928657042714437` (expected `14088908808337278323`), full `6704778439981554091` (expected `14161069325177094174`). Temporary disables were restored and the pinned test was not edited. This comparison isolates the mismatch from the new weapons' active simulation; it does not identify the underlying pre-existing workspace/profile difference.

## Try it

`../Builds/Arsenal/Try New Weapons.cmd` starts the four rank-I weapons. `Try Evolutions.cmd` starts their rank-VI evolutions. Both use the dedicated test profile. The executable without diagnostic flags runs normal progression with the new cards available.

No main player build was replaced. No package or scene changes are required. Balance values are implemented as approved starting values and still need ordinary player balance testing.
