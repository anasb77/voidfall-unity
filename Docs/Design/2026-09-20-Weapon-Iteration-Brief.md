# Weapon iteration: inspection and browser prototype brief

Status: superseded by `2026-09-20-Weapon-Approvals.md`. The owner approved only Summons, Mines, Clock, Pulse Pistol projectile art and Railgun projectile art for later implementation. All first-round new concepts and other projectile proposals below are historical, rejected exploration. Current work is new browser concepts only.
Inspected September 20, 2026 at HEAD `ebfc32d` with existing unrelated working-tree changes.

## Owner request

1. Give the original automatic weapons a visible upgrade at every rank I–VI, plus a distinct evolution. Preserve their identities; this request is about projectile/attack presentation, not a balance overhaul.
2. Iterate on Mines: explore chain reactions and improve placement/detonation presentation and explosion sound.
3. Improve Summons' willingness to pursue enemies while retaining their basic identity, rather than replacing the weapon.
4. Keep Clock as it is, with approximately 10% less opacity on its numbers only. Preserve dial/ticks, moving hands and behavior. Interpret 10% as a relative reduction for the prototype.
5. Inspect first, then show browser prototypes for owner review. Implement in Unity only after owner approval. Use the local `i-have-adhd` skill throughout the conversation.

The fourth newer weapon is Boomerang. No Boomerang redesign was requested. The separate manual legendary slot is outside this request.

## Confirmed original weapon baseline

All six have ranks I–VI and an evolution requiring the corresponding maxed support. The generated catalogue is read-only authoring output.

| Weapon | Current presentation | Existing evolution to preserve |
|---|---|---|
| Pulse Pistol | Rank I uses pistol art; II–III use pulse art (II smaller); IV–V use pulse-warm; VI uses pulse-bright with two afterimages. Several ranks share a silhouette. | Pulse Repeater: three-round bursts, third round ricochets. |
| Scattergun | Same projectile sprite family across ranks; visual size scales with rank/radius and actual pellet counts change. | Breach Cannon: tighter spread and a heavy central slug. |
| Railgun | Same projectile sprite family, rank/radius scaling and two afterimages. | Rail Lance: energized damaging wake. |
| Orbit Blades | Same ordinary blade sprite across ranks, with rank-driven orbit stats/count. Evolution changes tint and launches a separate hollow blade. | Hollow Blade: orbit remains while a hollow copy launches and returns. |
| Arc Lash | Procedural jagged outer/core lines. Rendering takes evolution, not rank; rank changes attack stats/chain count. | Arc Network: two chains and endpoint overloads; warmer, thicker effect. |
| Seeker Launcher | Homing rocket with the same sprite family across ranks and rank/radius scaling. | Cluster Launcher: impact releases three seeking charges. |

Primary paths (relative to `Assets/VoidFall/`): `Content/ContentCatalog.Generated.cs`, `Content/EvolutionRules.cs`, runtime `VoidFallGameRuntime.Sim.cs` (`UpdateWeapons`, `FireWeapon`, `SpawnWeaponProjectileFromPosition`, `UpdateBlades`, `SourceBulletVisualScale`), `.Render.cs` (bullet presentation), main runtime (`CreateArcEffect`), and `ProceduralSpriteFactory.cs`. Bullet state already carries Rank and Evolved. Arc's renderer currently has no rank parameter.

## Confirmed newer weapon behavior

**Mines:** dropped at Zack's position, at least 24 units from an existing mine. Base delays are 2.4/2.4/2.1/2.1/1.9/1.8 seconds; recovery cannot reduce placement below 0.9 seconds. They arm after 0.55 seconds, expire at 15 seconds and use a 26-slot pool. Actual proximity trigger is 34 × projectile-size multiplier plus target radius (the stats' Range=46 is not the trigger authority). Blast radii are 90/90/105/105/115/125 × area multiplier; damage is 60/75/75/95/115/140 before ordinary modifiers. No mine-to-mine detonation exists. Permafrost adds 1.2 seconds of freeze followed by 1.2 seconds of mobile immunity; bosses resist freeze. Keep that intentional control limit during exploration.

Mine art already develops over six ranks. Unarmed mines render at 40% opacity; armed mines have a faint blast-radius guide. Explosion presentation is a shared expanding ring via `ArsenalBlast`; the existing MineBoom sound is a 0.5-second descending saw/noise sequence, with a 0.15-second cue gate. A chain prototype needs deliberate sound overlap handling, not just replaying the same full-volume blast many times.

**Summons:** two rushers at I–II, three at III–VI; base speed 300, acquisition range 420. Idle creation stops at squad size; combat can fill the bounded 24-slot pool. Every active unit reselects the nearest hostile relative to Zack on every step. There is no persistent per-unit target identity. With no eligible target, units move into hovering formation beside Zack; with one, they move directly toward it and disappear on impact. Volatile Brood adds an 88-unit base area explosion.

Inference: the shared player-centred target search can make units converge on one enemy, switch targets as Zack moves, or abandon pursuit when enemies leave Zack's range. This plausibly explains the owner's observation; no live reproduction was run. The existing idle/engage integration test checks a nearby stationary target, not sustained pursuit while the player retreats.

**Clock:** face renderer alpha is currently 0.126; hands are 0.5. Rank III adds the smaller faster seconds hand and evolution adds a counterclockwise hand. Numerals, dial circles and tick marks share one baked face texture. `ClockNumeral` authors numeral alpha at 0.85: prototype a multiplier of 0.9 (0.765), retaining the face renderer alpha and all other marks. Do not reduce the entire face alpha to implement this request.

Primary paths: `Content/ArsenalContent.cs`; runtime `.Arsenal.cs`, `.Arsenal.Render.cs`, `ProceduralSpriteFactory.Arsenal.cs`; `Audio/ProceduralAudio.cs`. Live targeting is `VoidFallGameRuntime.cs:FindNearestHostile`. Current tests: `ArsenalIntegrationTests`, `MineBalanceIntegrationTests`, `OrbitalDefenseIntegrationTests`.

## Browser review plan — proposals, not approved designs

1. Original weapons: six selectable weapons, I–VI plus evolution, current/proposed comparison, freeze-frame and actual gameplay-scale animation. Preserve colour identity and readable silhouettes; each adjacent rank should be visibly distinct without obscuring Zack/enemies. Arc Lash needs an attack-effect study rather than a fake projectile.
2. Mines: a movable-player arena comparing current independent blasts with a proposed short delayed propagation through nearby armed mines. Make arming and propagation readable; include user-enabled sound previews. Keep baseline damage/cadence visible and unchanged initially so presentation and chain behavior can be judged separately.
3. Summons: compare the existing player-centred selection with unit-centred acquisition, retained valid targets, reassignment after target death and a clear return/leash rule. Include retreating-player and multiple-enemy scenarios. Retain the kamikaze identity and evolved area impact.
4. Clock: an exact before/after numeral-opacity toggle. Retain dial, hands, size, timing and damage behavior.

Use a separate local browser study under the workspace's `Prototypes/`; do not revive the deprecated browser game. Preserve VoidFall's sparse geometry, dark interiors, saturated edges and luminous centers. Browser appearance is a review tool, not proof of final Unity fidelity.

## September 20 browser study

The owner subsequently requested five additional movement-driven concepts and browser prototypes of the full scope. The separate study lives at `../Prototypes/weapon-lab/` relative to this Unity project, with local URL `http://127.0.0.1:4318`. Its README documents controls, checks and approximation limits.

The initial three concepts are Seam (cut along a line drawn by movement), Vector (auto-fire follows movement direction), and Anchor (charge while holding position; release by leaving). The five additions are Wake (sharp turns launch stored trail fragments), Rift Jaw (reposition to align closing planes), Triptych (place a triangular enclosure by movement), Parallax (position mirrored firing origins for converging beams), and Slingshot (curve to charge; straighten to release a comet). These are fictional/visual proposals, not approved lore or Unity content.

The browser includes all eight concepts, six original families at ranks I–VI plus evolution, current/proposed art comparison, mine chains and synthesized sound, summon pursuit comparisons, and the numeral-only Clock adjustment. It reuses copies of baked Zack/enemy/current weapon art. Automatic demos, manual movement, pause, four scenarios and optional Clock/Blades/Mines companions support review. Original combat and encounter balance remain illustrative. Unity implementation still requires the owner's explicit selection/approval.

### Inspection evidence and later implementation boundaries

Read `AGENTS.md` and all of `Docs/REPO_MAP.md`. Inspected the relevant code, historical Arsenal/weapon-polish/mine-balance notes, current test fixtures and representative baked sprites for blade, rail, seeker, bright pulse, rank-VI mine/summon and Clock face. Older design documents contain superseded values; current code governs the baseline above.

Unity is 6000.5.7f1, URP 17.5.0, Input System 1.20.0 and Addressables 2.8.1. This inspection used filesystem evidence; no Editor session or native player behavior was verified. No tests/builds were run and no game assets or code were changed.

Later approved changes must preserve fixed-step ordering, pooled spawn identities, separate combat/FX RNG, sprite baking/catalogue ownership, save IDs and orbital-defense collision correspondence. Extend existing run exports for mine-chain and summon decision/outcome behavior. Relevant validation includes targeted combat tests, the golden-master contract and 32-seed sweep for simulation changes, and native captures for visual approval. Preserve unrelated in-progress director work.
