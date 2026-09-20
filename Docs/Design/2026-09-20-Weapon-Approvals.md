# Weapon approval record — September 20, 2026

The owner explicitly selected the following browser studies, then authorized their implementation in the main build on September 20. This is the current scope authority; it supersedes broader proposals in the earlier weapon iteration brief. The replacement concepts below remain unapproved.

## Locked selections

| Selection | Approved reference and scope |
|---|---|
| Summons | Independent per-unit acquisition, retained targets, distribution across enemies, return leash, pursuit trails and impact presentation. Keep the kamikaze identity and evolved area impact. |
| Mines | Armed-neighbor chain reactions with a short propagation delay, readable links/detonation effects and revised explosion sound. Preserve the established freeze/recovery restriction when integrating. |
| Clock | Multiply only Roman-numeral opacity by 0.9. Keep dial, tick marks, hands, size and behavior. |
| Pulse Pistol | Proposed rank I–VI and evolution projectile artwork. This approves presentation, not the browser's illustrative combat tuning. |
| Railgun | Proposed rank I–VI and evolution projectile artwork. Preserve native rail behavior and evolved energized wake. |

The exact retained browser reference is `../Prototypes/weapon-lab/approved/` relative to this Unity project. Open `http://127.0.0.1:4318/approved/`. This isolated page exposes only the five approved families. `approved/lock.json` records SHA-256 hashes of the frozen page sources. Baked assets remain in the parent `assets/` folder. `check-v2.cjs` verifies the frozen sources have not changed.

The approved page retains current/proposed comparisons and rank/evolution selection for review. Changing a preview control does not change this recorded approval. Do not continue redesigning the two approved projectile families during new-concept exploration.

## Rejected and removed

All eight first-round concepts are rejected: Seam, Vector, Anchor, Wake, Rift Jaw, Triptych, Parallax and Slingshot. The owner considered their starting power excessive, their fiction difficult to sell, and Vector too close to the existing Railgun philosophy. Their entries and active mechanics were removed from the browser study; do not implement or revive them as renamed concepts.

The unselected Scattergun, Orbit Blades, Arc Lash and Seeker Launcher projectile/art proposals were removed from the review menu and are not approved. Existing Unity versions of those weapons are unchanged.

## Replacement study — unapproved

The active page now explores three narrower interactions: Latch (circle one attached target), Resonance (hold a useful distance from one target), and Reversal (face an incoming ordinary shot during a brief timed guard to return it). They default to Base, with separate Mature and Evolution previews and explicit target, damage and cadence limits. These are new proposals, not approved weapons or canonical lore.

The browser revision itself did not modify Unity. Native implementation now follows the telemetry, deterministic simulation and visual-validation contracts in `AGENTS.md`. See `2026-09-20-Approved-Weapon-Implementation.md` for implementation and verification evidence.
