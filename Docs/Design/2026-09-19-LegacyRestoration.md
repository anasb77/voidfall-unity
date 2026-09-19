# Legacy restoration — September 19, 2026

Approved for the canonical project and ../Builds/VoidFall.exe. No parallel release build.

## Presentation

Chromatic aberration defaults to zero; explicit video settings remain adjustable. Zack's original cyan middle gradient stop is restored. Regular retains the existing asymmetric contour with the original three-stop radial body shading. Broad shared-enemy halos are reduced; luminous outlines and cores remain.

HUD restores a prominent timer, framed green level badge, cyan-to-green XP and red-to-pink HP gradients. Pressure sits below the level. The approved browser HUD runs at 0.66 scale with Chakra Petch, custom weapon/passive slots and a right-aligned score/kills/Scraps block. The mute control is 35% smaller beneath the score. HP remains current/max.

Pulse Pistol I retains the dart. II uses a 60%-size original pulse silhouette; III uses full size. IV/V warm the core while retaining cyan light. V retains its gameplay double shot. VI brightens the core and uses a tight afterimage trail. Existing evolution burst/third-round behavior remains. Amplifier scaling remains functional.

## Cards

Phase Rounds is the single cumulative piercing card: +1/+2/+3 additional enemies for pistol, scattergun and railgun. It requires ownership of a compatible weapon; the offer names affected weapons. Split Shot has separate weapon-bound offers for pistol/scattergun/railgun/seeker, two ranks adding one projectile per attack each. Offers name their recipient before selection. It never targets Clock or other non-projectile systems; the dealer's existing extra projectile stacks additively. Giant Slayer adds 15/30/45% damage against bosses/elites. Second Wind has one rank: while alive at 10% max HP or below, heal current-level HP, capped at max; cooldown 180 active combat seconds. It does not revive lethal damage.

## Enemies and Director I v5

Swarmer is a fragile green five-point unit. Ninja Shuriken is an amber spinning four-point unit that approaches with a smooth left/right weave, phase-varied per identity. Spiky has a purple circular core and eight radial spikes; its visible body/collision uses a 19.5-unit base radius and cycles between 100% and 300% size in alternating half-second phases, with short eased boundaries and staggered identities. Its baked sprite uses four times the source raster resolution per dimension, preserving world size. Shuriken spins at 14 radians/second. Regular shading clips to its star outline; Runner restores the pre-diamond Unity silhouette.

Spiky deaths queue a local enemy-damaging, player-safe 85-unit blast after 75ms. Damage is 105% of that Spiky's max HP. Children wait at least until a later tick; at most eight blasts resolve per simulation step. This is a tuning starting point, not a claim of playtest balance.

Signature V1 rings use a dedicated 30 + N×34 active-second clock, 10 + floor(time/18) members capped at 26, a full circle at viewport half-diagonal + 50, and a uniform Runner or (after the Dasher introduction) 50% Dasher composition. They may be deferred by damage relief, incidents, arrival grace or boss/travel windows, but not tactical encounter phases. Deferred rings skip backlog. Dasher ring members use V1 .45s windup, .38s committed 570-unit/s dash and .7s recovery at .35 movement; ordinary dashers retain the shared attack budget. Every third eligible tactical beat can use a separate mixed green/Runner ring. All share the existing 750-body limit.

Shared-family reveals (active run seconds): Regular 0; runner 18; green 40; gunner 75; dasher 70; shuriken 210; brute 270; exploder 60; Spiky 420; guard 480; technician 540; twin gunner 600; splitter 660; mortar 720; bulwark 780; harvester 840; carrier 900. First introduction is a small tier-I group, with a 12-second gap before another introduction/beat and a 60-second learning interval before higher tiers. Recovery, boss windows and incidents can defer introductions. Arena-exclusive populations and boss summons retain their own contracts.

## Density, rewards and display preferences

Ordinary arrival multiplier is 2.5 (25% above the previous 2.0). Tactical recovery no longer reduces fodder arrivals; damage relief and boss arrival pacing retain their prior values. Ordinary rare powerups roll 1/300 instead of 1/120, preserving elite reward rules, Scraps and the type distribution. Overclock banks at most 30 seconds while retaining tiers and streak counts. Levels 1–5 retain XP requirements; level 6 onward costs ceil(previous cost ×1.25). These are starting values for owner playtesting, not a measured difficulty guarantee.

Clock face alpha is .126 (30% below .18); hands retain their alpha. Boomerang uses .675 scale (35% above .5) for artwork and matching projectile radius. Existing rank/evolution scaling remains.

Video adds a persisted monitor index (-1 AUTO). Connected screens are selectable by number and resolution. The runtime moves the main player window asynchronously before reapplying resolution/display mode. AUTO and disconnected selections preserve the OS-selected current screen. A disconnected preference is retained for a later startup. Editor tests never move the Editor window. Settings snapshots/rollback include the field.

## Escape and travel

Escape lasts 10 active seconds; rewards/modals still hold the timer. Enemy retirement ends by 5s, loot sweep begins at 6s, final drain completes before departure. Unclaimed relic delivery occurs after enemy retirement. One escape status replaces duplicate copy. Overclock presentation hides during safe travel while its duration remains held.

Dealer/crossing art is prepared during run startup; arena-neighborhood residency remains unchanged. Both arena travel and entry to the crossing use collapse/covered relocation/settle. Reduced motion uses a fade. The cover draws over crossing UI. Combat arrivals receive 2.5 seconds without director arrivals, plus matching player invulnerability and meteor delay.

## Ownership and validation

Content/LegacyRestorationRules.cs owns appended stable IDs, compatibility, progression caps and reveal times. Runtime/Gameplay/VoidFallGameRuntime.LegacyRestoration.cs owns runtime counters, queued chains, movement and introductions. DirectorI, Sim, Hud, Journey and Rift retain their existing integration responsibilities. ProceduralSpriteBaker preserves existing catalogue-key asset paths rather than renumbering them. New raster assets are baked and packed through the existing editor path.

Tests cover card eligibility/progression, actual projectiles/damage, healing cooldown, chain delay/player safety, enemy motion/size, escape reward retention and covered travel, plus JSON/JSONL policy evidence. -vfrestoration-check=<directory> captures the canonical player with an isolated profile and exports. Test/build results are recorded after execution; authored changes alone are not validation.
