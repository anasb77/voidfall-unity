# VoidFall — run journey and route design

Owner's direction recorded September 4, 2026. **The owner subsequently approved
implementing the map and travel using existing arenas and portal assets.** New
arena candidates remain unapproved. An independent agent
is working on additional arenas; coordinate shared interfaces and avoid
overwriting that work. This document is not an arena implementation inventory.

## Owner-stated intent

- The game has a finite purpose: escape the Void and return to your body.
  It is not intended to be an endless sequence of survival arenas without an ending.
- The protagonist is a man trapped in the Void. **Ending spoiler:** defeating
  the Void Overseer, currently named **Titriakis Tehedron**, enables escape.
  A future cutscene returns him to his world and body, but he has aged forty
  years and returns as an old man. Do not expose this twist in player-facing
  map descriptions or treat the cutscene/boss as already implemented.
- Every run begins in Abyss. The route varies between runs, and the player
  chooses branches rather than receiving an entirely automatic shuffled tour.
- Target successful-run length is approximately **30–40 minutes**. The planned
  content pool is **ten Voids**, with a randomized subset visited in each run.
  Whether the eventual Overseer arena counts within those ten is not yet settled.
- Owner-approved arena identities are **Abyss, White Sakura, Red Nebula,
  Monochrome Court, Hydra and Null City**. Five are currently available by the
  owner's report; Null City is being authored by another agent. The owner also
  used the working name Void City. Keep its existing `null-city` technical ID
  unless a deliberate migration is agreed.
- **Other arena names in the old codebase are AI-generated filler.** They are
  not approved concepts or promised content. Their presence in `PrototypeNodes`
  does not justify implementing them. The final Overseer encounter/escape is
  approved story direction, but an old graph label is not its approved arena name.
- Tab should expose a node map showing the current Void and possible next
  Voids, allowing the player to consider a route toward the exit.
- After a Void's escape condition is met, progression must continue. At a fork,
  send the player to a small temporary map with two portals and the destination
  names below them. Walking into a portal chooses that branch. The owner
  envisions randomized temporary-map presentation.
- Some nodes may display `?` and lead to a randomized Void. Exactly what is
  hidden and when its identity is revealed remain open design questions.
- Arena identity includes a mechanic/challenge: Red Nebula has meteors, Hydra
  has mutated enemies, and Monochrome Court has its special chess roster.
- Abyss, Red Nebula and White Sakura use randomized bosses from the ordinary
  boss pool. Hydra and Monochrome Court retain their exclusive encounters;
  do not shuffle those bosses into unrelated arenas.
- Work with the currently available arenas while additional ones are authored.
  The owner expects three or four additions soon; do not encode that expectation
  as an already-existing catalogue or silently settle the final route length.
- **Interim win condition:** complete the escape condition on the last node of
  the generated run. The eventual ending will instead culminate in the Overseer.
  On run end, return to the main screen, as requested in the preceding message.

The owner cited the current Dofus Infinite Dreams map as inspiration, not a
specification to copy. The supplied image shows a connected branching graph,
a highlighted path and mystery nodes. Its value here is readable route choices
and uncertainty. Use VoidFall's own art, fiction, encounters and pacing.
[Reference supplied by the owner](https://dofuswiki.fandom.com/wiki/Infinite_Dreams).

## Agreed map/travel direction

- Generate a seeded, finite graph at run start using playable arena definitions
  (assets, objective and encounter supported). Branches reconnect and all offered
  paths reach a terminal node. New arenas join through content configuration.
  Give route generation its own deterministic random stream so graph inspection
  and planning never consume combat RNG.
- Tab opens a paused overview; Tab/Escape closes it. Show traversed connections,
  current position, legal next choices and the exit. Allow marking a planned
  route, with the physical portal as the actual commitment. Map clicking does
  not teleport or bypass the current objective.
- Keep each junction safe and small. Distinct portal visuals plus names and
  concise mechanic/threat information make the choice readable. A short,
  reversible step into the approach area may highlight the intended choice;
  entering the portal commits it. Presentation can vary without changing the
  meaning or accessibility of left/right exits.
- Completion enters an explicit clear/reward/travel sequence: stop ambient
  spawning, remove remaining hazards safely, finish rewards, then transition.
  A branch uses the junction; a single exit can advance directly. Completion
  must not leave the player farming enemies indefinitely in a finished Void.
- Resolve mystery destinations from the run seed, keep them hidden until the
  agreed reveal point, and avoid rerolling them when Tab is reopened. Reveal
  some useful risk/reward information so mystery choices are not blind punishment.
- Avoid repeating the same arena on the path actually taken while the content
  pool supports it. The graph's number of nodes is not the number of arenas a
  player must clear: alternative branches are skipped in a given run.
- Save terminal rewards once before returning to the main screen. A brief
  completion acknowledgement can live on the main screen. Do not treat failure
  to save as permission to discard the result. No final cutscene is needed to
  validate the interim last-node completion flow.

The owner approved proceeding with this map/travel scope. Avoid adding
shops, currencies, new reward systems or generated encounter modifiers to this
scope until the owner chooses them.

## Decisions still to make

Implementation choices now settled for the current pool: four arenas per path
(row widths 1/2/1/1); Tab previews and marks a destination, portals commit it;
one occasional mystery stays hidden until entry; single exits travel directly;
safe junctions use existing portal sheets and seeded presentation variation.
The last generated node is the interim finale. Earned rewards/upgrades finish
before saving, and the next frame returns Home with a result notice. Failed
saves preserve the result for retry. These are implemented behavior; overall
future run length and final story content remain open below.

1. Number of Voids on a chosen path within the 30–40 minute target, and whether
   the mandatory start/finale count inside the ten-Void pool. A candidate is
   Abyss + four branch-selected Voids + a finale. Current shared survival phase
   is five minutes before bosses; future objective types can use different pacing.
2. New arena identities/mechanics and the eventual Overseer arena/cutscene.
3. Further art/pacing polish for the utility crossing after full-length playtests.
4. The intended mid-run quit/resume contract. Do not infer resume from profile persistence.

## New arena candidates — brainstorming only, not approved content

The owner was not sold on these candidates and deferred new-arena design.
They are recorded for discussion history only, not for implementation.

Keep each arena recognizable through one principal gameplay rule and one
strong visual landmark. Additional mechanics should develop that rule rather
than compete with it. These working names must not enter the production route
catalogue until the owner selects and refines them.

| Candidate | Visual identity | Principal mechanic and player agency | Scope / fairness considerations |
|---|---|---|---|
| The Hourglass | A shattered hourglass hanging over upward-flowing sand | Periodic time arrests freeze hostile projectiles; their restart is clearly telegraphed. Reposition through suspended patterns before they resume. An escape objective could culminate after several increasingly complex time cycles. | Start with existing projectile patterns and a bounded arena time effect. Preserve player responsiveness; avoid stacking an unreadable wall of newly emitted frozen bullets. Strong thematic connection to distorted time without revealing the ending. |
| The Mirror Wake | A black mirror-lake showing a delayed reflection of the player | Brief movement recordings become warned echo trails that later sweep the arena. Plan a path now to create a safe opening later; enemies can also be caught by the echo. | Only a few bounded trails; avoid copying the player's full upgraded damage output into an unavoidable attack. A bespoke reflection boss is optional later content. |
| The Marionette Theatre | A ruined stage beneath enormous hands and luminous strings | Puppet knots link groups of enemies into coordinated formations. Destroy a knot to break the formation or turn its release into an opening. The player chooses between thinning the swarm and severing its controller. | Reuse formation/enemy pools first. Strings must remain legible and clearly distinguish decorative tethers from damaging attacks. Curtain/scenery changes are optional, not a second required system. |
| The Orrery | Broken bronze rings and suspended celestial spheres | Clearly marked gravity wells curve hostile projectiles and enemy movement. Manipulate which wells are active to redirect pressure and open routes through the swarm. | Highest trajectory/readability risk of these concepts. Keep player controls unchanged and prototype one well before adding moving planets, orbit physics or more complex effects. |

Suggested first prototypes: Hourglass for a strong time-related identity, and
Marionette Theatre for a distinct enemy-control interaction. The owner has not
selected these. A ten-Void pool should increase route variety, not force players
to visit all ten on every run. A shorter interim run is preferable to using
unapproved filler or repetitive encounters solely to reach the duration target.

## Stuck-Abyss reproduction to protect

Owner-supplied telemetry: `voidfall-run-2848592627-1053s.json`, run seed
`2848592627`. Original file supplied from the player's local save directory;
do not check personal telemetry into source control without a task need.

- Matriarch and Warden spawned at approximately 299.98 seconds (5:00).
- Warden died at 341.23 seconds (5:41); Matriarch died at 367.32 (6:07).
- Owner reports the UI displayed **Abyss Complete**, but travel never occurred.
- Export remained active at 1053.71 seconds (17:33), with zero active bosses,
  158 enemies, all recorded arena time under `void`, and no recorded transitions.

This supports a failure to leave Abyss after the encounter. The export does
not contain the objective-completion, reward-ceremony and travel ownership
flags needed to identify the exact failing handoff. Do not claim the root
cause is established from telemetry alone. The earlier single-exit route-state
repair covers a different path and did not prove this first-Abyss issue fixed.

Implementation evidence: a regression with this double-boss seed demonstrated
that `SyncUiScreen` replaced PrizeReveal with Pause on the next frame, removing
the reward Continue callback. Explicit prize ownership now survives that frame.
The new journey stages update rewards/travel outside combat ticks. Tests also
cover terminal XP, both physical portals, save retry and visible junction
renderers. See `Docs/Design/2026-09-04-route-map-implementation.md` for results.

When implementation begins, reproduce the full completion/reward/travel
sequence with this seed, including double bosses and rewards collected both
before and after final completion. Validate one-exit and fork paths, map
open/close, death/restart, last-node completion and newly added arenas. Directly
invoking the completion callback alone is insufficient regression coverage.

Existing owners: `Core/VoidRoute.cs`, `Core/VoidObjectiveTracker.cs`, runtime
`VoidFallGameRuntime.Objectives.cs`, `.Rift.cs`, `.Roulette.cs`, `.RouletteChest.cs`
and `.UI.cs`, plus `UI/Views/RouteSelectController.cs` / `RouteSelectView.cs`.
These abbreviated paths are relative to `Assets/VoidFall/`; runtime partials
are under `Runtime/Gameplay/`. See `Docs/REPO_MAP.md` for the architecture.
