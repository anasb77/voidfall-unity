# Null City — approved Unity integration contract

Approved by the owner after interactive prototype V, September 5, 2026.
Visual and behavior reference: `../Prototypes/null-city/` relative to the repository.
The prototype is a design reference, not a replacement game runtime.

- Stable ID `null-city`; append ArenaId without renumbering serialized identities.
- Widened fixed city floor, reflective purple/teal tiles, layered illuminated alien
  architecture, LCD INTRUDER DETECTED, animated traffic, southern hangar.
- Surveillance: 22 seconds. Lockdown: 24 seconds, combining blacked-out lighting,
  telegraphed purge lanes and blue police. Purge hurts player and enemies.
- Two horizontal conduits; a left solo vertical and a slightly right-offset double
  vertical. Preserve the approved 1600x900 authoring coordinate system. No relays.
- Nine regular robots: Patrol, Enforcer, Sentinel, Crawler, Volatile Crawler,
  Heavy Gunship, Siege Mech, Broodmother, Light Gunship.
- Three lockdown police: Interceptor, Marshal, Suppressor. Suppressor alternates
  single right/left shots. Marshal protects its front while braced.
- Broodmother has animated toothed jaws; periodically releases two Crawlers and
  releases exactly four on death. Volatile explosions also damage machines.
- Motherload is a large SHIP, not an insect: eight deck cannons, detailed armor,
  circuit channels, foundries, radar, cooling fins and four plasma engines.
- Motherload is exclusive to Null City. After the standard five-minute survival
  objective, it owns a permanent lockdown encounter with bounded police reinforcements.
- Cannon lattice, Event Horizon tractor cone, drone deployment, bombardment,
  natural reactor vents. Tractor warns 1.8s, pulls 4s, holds its direction, fires
  aimed shots, can be escaped sideways or resisted by a dash, then vents 4s.
- Boss pressure: twice normal incoming damage, quicker cannon cadence, sustained
  lockdown; reactor exposure provides a natural damage window. No health-bar captions.
- Preserve native progression, weapon damage, armor, pickups, revives and game-over;
  the browser's sandbox shield reset and standalone retry UI are not production systems.
- Motherload death clears city hazards/hostile units before the existing reward,
  physical relic pickup and journey transition. Starts in Abyss as before.
- Arena art is authored/baked offline and referenced by Addressables recipes. No
  player-time full-screen texture generation, external webview, or per-enemy MonoBehaviour.
- Preserve fixed-step pool order, existing arena behavior, save IDs and golden masters.

Integration decisions: use a fixed arena origin when entering Null City, clamp only
Null City movement, and fit its complete authored floor with an arena-specific camera.
The City dash is local to this arena (Space / controller shoulder) and does not alter
legacy input or simulation outside it. Validate the changed route catalogue separately
from unchanged legacy endless rotation.
