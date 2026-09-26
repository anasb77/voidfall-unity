# Titriakis Tehedron — Final Boss Design

Date: 2026-09-19. Status: design approved by owner in conversation; implementation not started.

The final boss of VoidFall and the end of Zack Hazard's escape. One boss entity,
three forms, each with its own health-bar label:

1. **Titriakis Tehedron — Rubix Form** (the cube shell)
2. **Titriakis Tehedron — Hexahedron Form** (the 12-sided die, pinball jail)
3. **Titriakis Tehedron — Tehedron Form** (the true form; Displeased = hostile,
   Pleased = friendly chat state, **out of scope for the fight implementation**)

Owner naming note: display names use the owner's spellings ("Titriakis Tehedron",
"Rubix", "Hexahedron Form" for the d12 shell). These are player-facing strings;
internal ids stay lowercase-technical (`titriakis-rubix`, `titriakis-hexahedron`,
`titriakis-tehedron`).

## Art

Authored sprite sheets live on the owner's Desktop (`titriakis tehedron/`) and
must be moved into `Assets/VoidFall/Resources/VoidFall/Titriakis/` at
implementation time (Hydra Prime precedent: authored PNG loaded via
`Resources.Load`, never the procedural fallback):

| File | Use |
|---|---|
| `stable titriakis tehedron.png` | Tehedron Form (Pleased) — 5×6 frame sheet, cyan |
| `unstable titriakis tehedron phase 1.png` | Tehedron Form (Displeased) phase A — gold |
| `unstable titriakis tehedron phase 2.png` | Tehedron Form (Displeased) phase B |
| `minions/unstable reaver.png` | Echo of Reaver (corrupted recolor) |
| `minions/unstable warden.png` | Echo of Warden (corrupted recolor) |

Rubix and Hexahedron forms still need art (or procedural treatment in the
`ProceduralSpriteFactory` style) — open task.

## Arena

New final arena (working name **The Core**) reached as the terminus of the route
map, after the last exclusive void. Killing the boss is the game's ending; the
existing 15 s escape-collapse flow (`.Escape.cs`) is the handoff point for a
future ending cinematic. The Pleased-form chat is a later feature on the same
arena.

## Form 1 — Rubix Form (the puzzle box)

A giant rotating cube. Owner approved the proposed moveset:

- **Face lasers:** telegraphed beams fire from individual faces; the cube's
  rotation determines which faces can fire, so players read the rotation.
- **Row shuffle:** periodically the cube "turns" a row/column, displacing
  hazards or minion positions in the arena (presentation twist, same math).
- **Shield orbit (unlocked at 50% HP):** the cube summons a ring of small
  squares that spin fast around it as a guard. The boss is immune while any
  shield square remains — the player must shoot the squares down (they also
  hurt on contact). Once the ring is cleared the cube resummons it after a
  short breather, so the mechanic stays live for the rest of the form.
- **Break:** on defeat the cube shatters into mini-cube shards (FX only) and
  the Hexahedron Form is revealed.

## Form 2 — Hexahedron Form (pinball jail)

Owner's signature mechanic; reference sketch shows the boss bouncing inside a
rectangular laser jail with the player trapped inside:

- The boss projects a **screen-fitting rectangular laser jail** around the
  player; touching the walls damages the player. The player is trapped inside
  with the boss.
- The boss **bounces continuously** off the jail walls (billiard reflection),
  aiming roughly at the player's position on each wall impact.
- **Escalation:** bounce speed and contact damage scale up as the form's HP
  drops (enrage curve).
- On defeat the shell cracks — Tehedron Form emerges.

## Form 3 — Tehedron Form (Displeased) — "The Brawler"

Owner picked the busiest structure: the boss fights **alongside** its echoes.

- Teleports between arena corners (Hydra-evasion-style sockets) and fires
  **aimed beams** at the player.
- **Summons both echoes at form start — the fight becomes a 1v3 at once**
  (owner decision, 2026-09-22; supersedes the earlier threshold-staggered plan):
  **Unstable Warden** and **Unstable Reaver** enter together. Both use their
  existing attack logic with the corrupted authored sprites; they are echoes,
  so no Scraps/objective rewards of their own.
- Two visual sub-phases (`unstable phase 1` → `unstable phase 2` sheet) as HP
  falls; attack cadence rises in phase B.
- Death of the Tehedron Form ends the fight and the run → ending handoff.

## Implementation mapping (for the build session)

Follows the Hydra/Monochrome/NullCity precedent:

- `Content/TitriakisContent.cs` — arena + `BossDefinition`s; health-bar names
  come free from `BossDefinition.Name` via `Hud.cs`.
- `Core/TitriakisEncounterRules.cs` — engine-free timing/geometry: jail rect,
  bounce reflection, enrage curve, echo spawn (both at form start), teleport
  socket order.
  Deterministic RNG through the seeded `Rng`, never Unity random.
- `Runtime/Gameplay/VoidFallGameRuntime.Titriakis.cs` — pooled state, custom
  attack stepping (`ApplyTitriakisAttack` alongside `ApplyHydraAttack`),
  presentation (jail walls via line renderers, sprite-sheet animation).
- `BossState.State` machine reused; forms are separate `BossDefinition`s
  swapped by the encounter controller (form break = kill + scripted spawn of
  the next form at the same position).
- EditMode tests for the rules class (bounce math, enrage curve, thresholds);
  PlayMode probe for the full three-form fight.

## Revision log

- 2026-09-22 — Form 3 summon design locked by owner: both echoes spawn at form
  start (1v3 at once). Prototype updated to match; the unused lucky-roll
  shockwave idea was cut from the prototype entirely.
- 2026-09-22 — Prototype polish pass 2: aurora edge glow reworked (tight,
  saturated, edge-hugging); Form 1 face lasers are now flowing aurora "elixir"
  beams instead of red; row-twist telegraph halved (1.2s → 0.6s) and a curved
  sine-wave twist lane added; jail/transition tiles now render as mini cube
  faces (3×3 violet grid); transitions gained an aurora swell + twin shockwave
  rings; Form 2 has a ringside audience of early enemies that turns hostile
  when the jail breaks.
- 2026-09-22 — Prototype tuning pass 3 (owner feedback): Rubix Form HP doubled
  (600 → 1200); new 50%-HP unlock — the shield orbit, 8 fast-spinning guard
  squares that make the cube immune until destroyed (resummon ~7s after the
  ring is cleared); square-attack telegraph cut another 20% (0.6s → 0.48s);
  fixed a crash where a beam's fade could go a hair negative and throw on
  `arc()` (negative radius).

## Explicit non-goals (this feature)

- Pleased-form chat / friendly ending dialogue.
- Ending cinematic beyond the existing escape-collapse handoff.
- Rubix/Hexahedron final art (placeholder procedural art acceptable behind the
  authored-art Resources path).
