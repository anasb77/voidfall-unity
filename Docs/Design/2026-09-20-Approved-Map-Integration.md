# Approved maps: Unity integration

The owner approved the final `map-camera-study` designs and requested integration
into the main Windows build. This supersedes the earlier native-size city,
downward-scrolling Hydra I, and player-centered Court floor studies.

## Framing and artwork

The browser slider covers 620–1100 vertical world units. Null City at 50% is
860 units; Hydra and Court at 60% are 908 units. These are viewport heights,
not resolution settings or actor multipliers. Spatial Awareness still applies
its ordinary zoom bonus. Zack retains the game's normal presentation size.

`Tools/ApprovedMaps` contains the approved authoring functions and reproducible
exporter. Unity imports the exported resources through `ApprovedMapAssetImporter`.
The city plate is 5120×2880; the stationary Hydra ground is 3840×2160. Enemies
use four raster pixels per world unit, with four authored animation poses for
warhorses, city additions, insects and guardians. Court's board and cell attacks
remain resolution-independent geometry and shader effects.

## Null City

- Finite original city, expanded 1.6× in each dimension: 2560×1440 overall,
  1984×841.6 playable floor. Buildings, props, actors and combat radii retain
  their physical size. The coordinate conversion moves positions; it does not
  enlarge sprites. Horizontal conduits remain 68 units thick, vertical ones 54.
- Approved matte floor panels and original architecture. Surveillance, police,
  lockdown, dash and Motherload encounter remain in the existing native systems.
- Energized roads have local displacement, layered discharge and electrical
  arcs. Reduced motion fixes their visual phase and disables displacement.
- Transit is clipped between both portal mouths in play and menu presentation.
- Five specialists: Prism Lancer, Orbit Warden, Phase Skimmer, Grav Loom and
  Relay Tender. Warnings commit their aim; repairs require the same living
  target identity and cannot exceed its maximum health.
- Five contact-only pursuers: Grid Walker, Needle Runner, Arc Hound, Ram Crawler
  and Latch Drone. Normal ambient selection is approximately 71.25% simple
  pursuit, including the original crawler. Existing heavy/police schedules stay.

## Hydra

- Hydra I's floor stays anchored to world coordinates. Camera movement does
  not stretch the surface, and the ground never scrolls beneath a stationary Zack.
- Three hives. Each live hive requests five offspring every three seconds:
  three original hybrid/virus specimens and two insects. Full pools defer the
  requested counts; they do not silently reduce a brood. Destroying a hive
  cancels further broods and queues exactly one guardian.
- Five insect bodies: Needlewasp, Hookmantis, Blisterbeetle, Sawroach and
  Mourningmoth. Their regular artwork uses the approved 90% scale.
- Two guardians: warned mantis charge and armored beetle shockwave.
- Blisterbeetles warn before a 100-unit blast; lethal damage shortens the fuse
  to 0.45 seconds. A blast deals 18 player damage or 24 nearby-enemy damage.
- Darker toothed nests and the six-unit `HYDRA HIVE` label. Existing native
  health bars show damage. Hive/guardian base health is 180/480 before ordinary
  native progression modifiers.
- The ten original Hydra populations remain active. Hydra II retains the
  existing boss, arena, attacks and same-visit transition.

## Monochrome Court

- Six families, nineteen forms in both factions. Pawn, Bishop, Queen and Rook I
  preserve their source silhouettes. Rook II/III use the approved castle forms;
  the fourth rook is Elite. Low ranks remain eligible as difficulty rises.
- Horse Knights commit to a two-cell leg and a perpendicular one-cell leg,
  warn the complete path, pause at the corner, and recover. Paths stay inside
  the board and avoid living Sentinels.
- Armored Knights are large mythical warhorses carrying the source Pawn III.
  Three ranks, twice the same-rank pawn radius, 1.2× pawn speed, and independent
  20% incoming player-hit blocks. The rider is part of the same enemy.
- Bishops warn their diagonal fire; rank III has the extra perpendicular shot.
  Queens cast promotions on one/two living allied pawns; rank III can instead
  give a maximum-rank pawn a 24-point, six-second ward. Death interrupts casting.
- Sentinels retain their original native silhouette and tracking eye, with
  three small full-toothed mouths. Existing native 100,000–150,000 health and
  sacrifice/release behavior are preserved; they are separate from mobile rooks.
- During survival each living Sentinel owns a fixed, grid-aligned 4×4 area.
  It warns for three seconds, bursts for 0.45 seconds, and repeats every fourteen
  seconds with staggered timing. Its own army is immune. Nearby enemies gain a
  nonstacking 24-point shield, with four-second recharge after break while inside.
  Leaving every live territory or losing the Sentinel removes that protection.
- Only the twin-boss encounter activates a whole checker color. White attacks
  white cells, black attacks black cells; the other color is safe. Bosses keep
  their shared health and their native aimed attacks.
- Soft white stone, scoring and grain keep white pieces legible. Warning cracks,
  hot borders, a square pressure front, brief burst flash and debris make the
  strike forceful. Reduced motion removes the flash, moving pressure front and
  debris. These attacks do not shake the camera.

## Runtime ownership and evidence

Content IDs live in `ApprovedMapContent`, `MonochromeContent` and `NullCityContent`.
The `ApprovedMaps`, `ApprovedCity`, `ApprovedCourt` and `ApprovedHydra` runtime
partials use the existing fixed simulation, enemy pool, director attack budget,
faction ancestry and renderers. Sidecars are keyed by spawn identity.

Run-history map presentation version is 3. Events record map policy, tier IDs,
territory shields, warnings/bursts, promotions, hive requests/deferred children,
guardian releases and insect fuses/blasts. Ordinary damage stays aggregated by
the existing recorder. The packaged `-vfmapcheck=` diagnostic captures all three
maps and both native boss transitions using an isolated profile/export directory.

Validation results and the final source revision are recorded with the release
handoff. Diagnostic screenshots demonstrate rendering and framing, not normal-run
balance or gameplay performance under an uncontrolled population.

## Executed validation

- Unity6000.5.7f1:647/647 EditMode tests passed. Across the map, arsenal, mines,
  director and run-export suites,132 distinct PlayMode checks passed, including
  hive pool deferral, single guardian release, beetle fuses, nonstacking Sentinel
  protection, mounted-horse presentation and progression-preserving promotions.
  Earlier assertions for the superseded1× city and486-unit half-height were
  updated to the owner's approved geometry and viewport values.
- Release Windows build succeeded September20 at14:34:23 local, GUID
  `8a5661c8e8dc4a5bbd8b9a46178c7250`, using DX11. The143 approved PNGs and their
  importer metadata are included. Both backgrounds exceed1080p; actor art is
  rendered at four pixels per world unit without changing actor/camera scale.
- Packaged diagnostic passed all12 phases: City surveillance, energized roads
  and additions; Court roster, edges, Sentinel warning/burst and both bosses;
  Hydra original populations, hive/insect roster and the original Hydra II boss.
  Every phase reported Zack scale74, with860-unit City or908-unit Court/Hydra
  viewport height. Inspected the actual native screenshots for art and framing.
- Evidence: `Logs/approved-map-edit-final.xml`, `approved-map-play-release.xml`
  plus successful export/mechanics reruns, `approved-map-test-summary.json`,
  `approved-map-build-release.log` and `approved-map-release-captures/`.
  The native log has no exceptions; it retains the existing ComputeBuffer
  disposal warning on application shutdown. Final capture left the real profile
  hash unchanged; diagnostic display settings were restored.
- Main player promoted to `../Builds/VoidFall.exe`; all218 staged files matched SHA-256 after copying. Existing RunExports and other build folders were preserved.
