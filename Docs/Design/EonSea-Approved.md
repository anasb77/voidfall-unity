# Eon Sea and shared roster expansion — approved contract

Latest owner correction: the star-shaped original `chaser` sprite belongs to **Regular I**. Its II–IV designs must evolve that star. It is not a Dasher variant. Keep the single original Dasher I. Preserve technical `chaser` IDs; display Regular. Regular stays a direct pursuer at all tiers.

Implement the approved browser content in Unity:

- Eon Sea is a regular Void, sharing ordinary enemies and random shared bosses. Native five-minute survival and existing boss/escape/reward/travel flow apply; do not port the90-second study shortcut or special manual explosive charge into production.
- Continuous glacier world, normal following camera, physical glacier collision/cover, scattered slippery patches. No enclosing arena/shoreline boundary.
- Glaciers autonomously crack/melt. Ordinary weapon/contact damage cannot destroy them. Nearby real combat explosions add up to three stress cracks, accelerating future melt. Collapse slows nearby player/enemies/bosses50% for20seconds without damage. Refresh duration; never stack the multiplier. Warn the pulse radius late in melting. Preserve visited destruction state for the current arena visit.
- Slippery patches retain player momentum and soften steering. Ordinary control returns outside. Frozen movement remains capped at half speed even on slippery ice.
- Shared enemy tiers I–IV across14families, and existing Elite Exploder / Siege Mortar / Curved Gunner tiers I–IV. Use approved prototype traits and the final star-based Regular correction. Global run time controls progression, not local arena time: II9–15min, III24–30, IV34–40. Respect existing global difficulty scaling and exclusive rosters/bosses.
- Artwork: approved glacier textures/forms and compact shared enemy silhouettes, saturated thick edges and luminous cores; no Null City mechanical-panel restyle. Export source art offline and bake native imported assets.
- Preserve IDs, save compatibility, pooled simulation ordering, combat-vs-FX RNG separation, native reward and escape ownership, prepared arena loading/unloading, and unrelated concurrent changes.

Prototype source: `../Prototypes/eon-sea/` (relative to repo root). Unity implementation supersedes browser-only status once verified. No promise of frame-for-frame browser parity; native combat/progression ownership is authoritative.
