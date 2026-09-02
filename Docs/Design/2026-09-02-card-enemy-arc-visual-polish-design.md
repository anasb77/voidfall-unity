# Card, Enemy, and Arc Lash Visual Polish Design

## Goal

Improve upgrade-card readability and combat feedback for an experienced game audience while preserving VoidFall's deterministic combat behavior and fixed-pool runtime architecture.

## Approved Visual Direction

The selected direction is the high-impact Option C treatment with controlled screen footprint. Upgrade cards use the approved power-transfer arrow. Arc Lash uses the approved layered lightning and electrified-hit presentation.

## Upgrade Cards

### Copy

- Weapon upgrades list one changed stat per line.
- Each line uses the compact form `Label before → after`.
- Card copy does not use prose such as `Damage increased from 12 to 14`.
- Units stay compact and attached to their values, for example `0.80s` and `14°`.
- Acquisition, support, evolution, late-upgrade, and repair cards use short sentence-per-line copy when a numeric before/after comparison does not apply.
- Existing content meaning and gameplay values remain unchanged.

### Number hierarchy

- The current value uses the card's cyan visual language at regular weight.
- The transition is a cyan-to-mint power-transfer beam ending in a glowing arrowhead.
- The upgraded value is larger, bold, mint-green, and carries a restrained green glow.
- The transfer remains right-facing for all stats. This avoids misleading direction semantics for lower-is-better values such as fire delay.
- Labels remain high-contrast neutral text so the numeric progression dominates the scan path.

### Icons and layout

- Every weapon, support, evolution, and late-upgrade card uses its content-specific icon from the existing build-chip icon atlas and ID mapping.
- Repair keeps the existing heart icon.
- The runtime prepares each content-specific icon as texture/UV data and passes it through `UpgradeCardData`, keeping `LevelUpView` independent of runtime rendering code while replacing the current generic circle or diamond glyph.
- The IMGUI fallback/debug level-up presentation uses the same icon mapping and number hierarchy.
- Cards retain one visual column per choice on desktop and the existing stacked layout on short or narrow viewports.
- Stat lines do not wrap under normal supported desktop layouts; the description region may grow vertically on narrow layouts.

## Combat Damage Numbers

- Normal damage numbers use larger, heavy-weight light text with a dark outline and a compact neutral glow.
- Critical damage remains larger than normal damage and uses the existing red family with a stronger but short-lived glow.
- Existing floater merge behavior, values, lifetime, movement, ordering, and pooling remain unchanged.
- Styling changes apply through the existing pooled uGUI `Text` views. No per-hit GameObjects or MonoBehaviours are introduced.

## Enemy Presentation Tweaks

- Rusher and ranged attack-preview alpha is multiplied by `0.8` at render time. Existing geometry, timing, progress curves, and gameplay telegraph duration remain unchanged.
- The alpha reduction applies to the existing charge-lane/arrow treatment used by the Dasher and Roster II Chaser and to the existing firing-line treatment used by ranged Gunner variants.
- The Roster II Chaser uses the Dasher's violet identity instead of its current red/pink identity. Its body, glow, and attack preview share that violet family.
- The standard yellow Elite spins at `3.5×` its current visual rate while its velocity is above the project's movement threshold.
- The standard Elite's extra spin pauses during charge telegraphs, stationary beats, and any other state where it is not moving.
- Elite spin acceleration is presentation-only. It does not modify `EnemyState`, collision, movement velocity, targeting, attack timing, combat RNG, or deterministic hashes.

## Arc Lash

### Bolt rendering

- Arc Lash remains an instantaneous chain weapon; gameplay targeting, range, jump count, damage, and resolution order do not change.
- Each pooled Arc effect renders three layers: a wide low-alpha blue bloom, a narrower saturated blue body, and a thin white-hot core.
- Each link uses more intermediate points than the current two-offset construction. Offsets alternate across the link normal with bounded FX-RNG variation, producing a lightning silhouette without harsh long zigzags.
- Short secondary branches are cosmetic and use only the FX RNG stream.
- Line renderers use rounded caps and corners. The complete effect remains brief and fades quickly to avoid clutter during high fire-rate builds.
- Existing fixed Arc-effect capacity and oldest-effect replacement behavior remain intact.

### Electrified target feedback

- Every Arc Lash target receives a short white-blue flash, a compact blue ring, and several outward sparks at the moment damage resolves.
- The target tint/flash is stored in presentation-only pooled state keyed to the enemy slot and reset when a slot is inactive or reused.
- Boss targets receive the same visual language at a scale appropriate to their radius.
- No stun, status effect, damage-over-time behavior, or new gameplay state is added.

## Architecture and Ownership

- `UpgradeRules` owns compact stat-change string generation because it already compares adjacent weapon ranks.
- `UpgradeCardData` and `LevelUpView` own card icon identity and uGUI layout.
- `VoidFallGameRuntime` presentation partials own IMGUI parity, damage-number styling, telegraph alpha, Elite visual rotation, Arc layers, and target-hit feedback.
- `ProceduralSpriteFactory` remains the source of content-specific icon and Roster II sprite visuals.
- Existing pools and arrays are extended where needed. No manager, singleton, package, prefab, scene edit, or per-actor MonoBehaviour is introduced.

## Performance and Accessibility

- Arc geometry is generated only when an Arc fires, not every rendered frame.
- Runtime rendering reuses pooled `LineRenderer`, `SpriteRenderer`, particle, ring, and text views.
- The new glow layers use the project's existing additive material and URP bloom path.
- Current and upgraded values differ by weight, size, and glow in addition to hue, so the hierarchy is not color-only.
- Reduced-motion behavior keeps the static readability treatment and suppresses or minimizes cosmetic motion where existing project settings require it.

## Testing and Validation

- Add EditMode tests first for compact one-line-per-stat formatting, compact units, and unchanged-stat omission.
- Add tests for content-specific icon mapping, repair fallback, and card-data propagation.
- Add presentation-rule tests for the `0.8` attack-preview multiplier, violet Roster II Chaser palette, and movement-gated `3.5×` Elite visual spin.
- Add Arc geometry tests for endpoint preservation, bounded intermediate offsets, layer widths/colors, and fixed-capacity replacement behavior.
- Add tests proving Arc hit feedback is presentation-only and resets on pooled slot reuse.
- Run the relevant tests red before implementation, then green after the smallest production change.
- Rebuild `VoidFall.Runtime.csproj`, run complete EditMode and PlayMode suites, and inspect Unity logs for new errors.
- Capture level-up cards, normal and critical damage floaters, both affected telegraph families, moving/stationary standard Elites, and Arc Lash hitting normal enemies and a boss.
- Run the existing deterministic sweep and confirm no gameplay golden-master hash change. Any hash change is treated as a regression because the approved work is presentation-only.

## Acceptance Criteria

- Upgrade cards scan as direct stat deltas with one changed stat per line.
- Old values are cyan; new values are bold mint-green with a small glow; the approved power-transfer arrow clearly connects them.
- Every card displays a recognizable content-specific icon.
- Normal and critical combat damage numbers remain legible over dense combat.
- Rusher and ranged attack previews retain timing and shape at 80% of their former opacity.
- Roster II Chaser reads as violet and visually matches the earlier Dasher family.
- The yellow standard Elite visibly spins 3.5× faster only while moving.
- Arc Lash reads immediately as bright lightning and struck targets visibly react as electrified.
- No gameplay values, RNG order, deterministic hashes, scenes, prefabs, packages, or save data change.
