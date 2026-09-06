# Approved weapon prototypes → Unity

User approved Unity implementation after browser iterations. Preserve the existing six weapon IDs, saves, simulation ordering, authored Operative, and all unrelated working-tree changes. No support slot redesign or other audit proposals are included.

## Contract

Append Mines, Summons, Clock, Boomerang. Six ranks use the last proposed tables in this conversation. Evolutions: mine freeze (2.4 seconds), exploding summons, second counter-rotating clock hand, three boomerangs. Idle summons hover beside the player and wait for a nearby eligible target. Clock art keeps the accepted face at half opacity; mine range rings are reduced 30%. All six ranks have distinct cosmetics without cosmetic hitbox growth.

## Implementation sequence

1. Catalogue tests first: original ordering, ten weapon entries, approved rank endpoints, valid evolutions, upgrade offers. Append IDs and hand-authored data via the existing ContentCatalog static initialization; do not edit generated source. Extend HUD arrays and upgrade descriptions.
2. Add a runtime partial owning fixed-capacity mine/summon/boomerang arrays and clock/freeze sidecars keyed by spawn identity. Use existing enemy/boss damage, RNG, status, telemetry and effect paths. Skip entirely when the new weapons are unowned. Reset at run start, combat clear/travel, and teardown.
3. Add rank-authored weapon sprites through a ProceduralSpriteFactory partial using the existing raster authoring functions. Cache assets and destroy them through the existing cleanup lifecycle. Draw persistent entities from runtime views; do not add entity MonoBehaviours. Keep the hour face and rank hand distinct, and use the agreed opacity values.
4. Wire discovery/evolution descriptions, reward acquisition slot eligibility, HUD and records. Evolution supports: mines/amplifier, summons/dodge (owner journal), clock/cycling, boomerang/projectileSpeed. Fix evolution support lookup to the extended catalogue.
5. Add runtime tests covering arms/proximity, summon idle/target acquisition, clock hit cadence, bounce exclusions/evolution, cleanup, old slot reuse, and boss damage. Include a hermetic opt-in capture entry point for visual validation without affecting normal saves.
6. Run scoped EditMode and PlayMode tests, then the unchanged golden master and 32-seed sweep. Build a separate Windows validation player and inspect captures if tooling permits. Update REPO_MAP with the new owner and invariants. Report exact remaining limitations.

## Proposed rank data

- Mines damage 60/75/75/95/115/140; cooldown 1.6/1.6/1.35/1.35/1.15/1; radius 90/90/105/105/115/125. Arming .55; lifetime 15.
- Summons damage 40/50/50/65/80/100; count 2/2/3/3/3/3; cooldown 3/3/3/2.6/2.3/2. Acquisition 420. Idle creation stops at squad size; already-created returning units persist within the bounded active pool, matching the approved prototype.
- Clock damage 20/26/32/40/48/60; rotation seconds 5.5/5.5/4.7/4/3.4/3; reach125. Per-target, per-hand hit cooldown .32 before support modifiers.
- Boomerang damage 24/32/32/40/48/60; targets 2/2/3/3/4/5; cooldown1.8/1.8/1.65/1.65/1.45/1.3. Returning flight harmless; fresh targets tracked by spawn identity.

Numbers are a starting balance pass, not a claim of parity with all existing builds. Bosses retain their authored movement/phase rules; mine freeze should not stall boss encounter state machines.
