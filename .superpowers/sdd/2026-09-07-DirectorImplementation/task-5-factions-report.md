# Task 5 — native Destroyer factions and finite reward provenance

## Delivered implementation

Owned runtime files: GameSim.cs; VoidFallGameRuntime.Sim.cs faction/damage/spawn hooks (parent encounter changes preserved); scoped RosterProgression.cs aim/blast and ally-heal routing. New files: Content/DestroyerContent.cs; Core/FactionRewardRules.cs; runtime .Destroyers.cs/.FactionRewards.cs/.FactionProjectiles.cs; Editor/FactionRewardRulesTests.cs; PlayMode/DestroyerFactionIntegrationTests.cs. New source metadata included.

Five native pooled EnemyState actors: Maw (.95 warning, 620*.47 lane charge), Razor (.85, 690*.45), Husk (1.1, 145 frontal bite +/-1.05), Grasp (1.2, 165 radial clamp), Spite (1.1, three 215-speed bolts +/- .16). Native base stats match the brief; ordinary director health/damage/speed scaling applies deliberately, forced tier I. New warnings wait for DirectorProfiles.AttackLimit before starting; committed warnings never retarget. Charges clamp their partial final integration step. Windup/charge bodies ignore knockback and are anchored in separation; other bodies separate away. Generic contact excludes raiders, avoiding double attack or damage during their warning. Failed five-actor deployment rolls back without rewards.

Enemy acquisition uses the existing 72-unit grid with staggered .28–.382 second scans, local range and 22% hysteresis. Target sidecars bind slot + SpawnId. Warning aim/target position is retained across moving/dead/reused opponents. Ordinary contact, native gunners, dashers, mortars, exploders, elites and higher tiers use the actual chosen opponent. No Player.Position substitution. Technician/guard healing and shielding exclude rival factions. Court color labels are untouched; those actors remain ordinary-faction entries.

Shot origin sidecars carry faction, actor slot and SpawnId independently of HostileShotBlockable. Every insertion resets to Enemy/-1/0, including unknown, boss and meteor callers; runtime only copies a scoped actor faction when an actor exists. During raids, earliest swept hostile NPC/player body entry truncates terrain and orbital-interception queries before resolving damage. Same-side NPC bodies are immune; protected boss/elite/meteor blockability is unchanged. Retirement/expiry/reuse clears origin metadata. Shot callbacks validate target SpawnId before damage.

Scoped try/finally damage provenance flows through native ApplyEnemyDamage, recursive death effects and delayed meteor chains. NPC hits bypass player STANDSTILL/critical/support damage and player damage statistics. Shield/armor and native death ordering remain centralized. KillEnemy forced child/safe-escape clear credits remaining health when player owned; safe escape retains its XP/Parts settlement. Each native death resolves once.

Fixed generation/refcount reward roots give each ambient source 24 total rewardable descendants and each boss 64 across its entire family tree. Each child spends the shared allowance once at birth. Source scopes retain roots across parent death and slot reuse; unavailable roots fail closed and cannot mint descendant roots. Warden's older unflagged phase-two spawns are covered by the whole UpdateBosses scope. Ordinary ambient sources remain independent, with no global arena XP ceiling. Unrewarded descendants still execute their mechanics/death effects but grant no XP, score, Parts or rare pickups. Meteor destruction itself has no XP/score/Parts path.

Contribution consumes only initial rewardable health: shield/overkill do not count and healing cannot reopen consumed budget. Player score is proportional to effective player health damage even when a rival finishes; all eligible normal XP/drop loot still appears on rival kills. Whole bounty points move to _score; _fractionalFactionScore retains fractional carry across kills/arenas. Direct player kills alone increment _kills/_eliteKills, with separate assist/rival counters. Native objective defeat feed still observes defeated hostiles.

Exactly coincident enemy bodies now receive a deterministic SpawnId-pair axis without RNG; noncoincident separation formula/order is preserved. Anchored warning actors retain authored paths.

## Parent integration API (same partial; methods private unless stated)

- `StartDestroyerRaid(Vector2 center)`, `StepDestroyerRaid(float dt, bool withdrawing)`, `EndDestroyerRaid()`; scheduler owns 2.5/32/3 timing and admission for all five bodies/threat.
- `ResetFactionAndRewardArena()` after native arena/new-run pool clear. `ResetFactionRunDiagnostics()` only on new run; preserves fractional score and counters over travel.
- `TryRenderDestroyer(int slot, EnemyState enemy)` in enemy render after active guard; continue when true. `DestroyDestroyerPresentation()` on teardown. Imported Resource sprites are never destroyed.
- main FindEnemy fallback `DestroyerContent.Find(id)`.
- CurrentEarnedBaseScore must add `_fractionalFactionScore` to `_score` before its one existing final roundoff.
- public `RivalOnlyDefeats`, `AssistedDefeats`, `DirectFactionDefeats` counters for local record/telemetry facts.
- `.Sim` now calls parent `ApplyMajorIncidentPlayerDisplacement(dt)` after ordinary integration and `ApplyMajorIncidentEnemyDisplacement(ref enemy,dt)` before refreshed contact geometry.
- Null deferred brood: queue sidecar `long[]`; each accepted entry calls `CaptureFactionBirthRoot()`. Processing wraps actual SpawnNullCityUnit in `using (FactionBirthScope(root))`; release successful/deleted entries through `ReleaseFactionBirthRoot(root)`, preserve retained handle on retry and release on queue clear. Do not replace captured parent roots with a fresh source.
- Null boss hangar police are a confirmed indefinite source outside UpdateBosses: SpawnNullCityUnit should wrap its spawn in `FactionBirthScope(BossRewardRoot(_gameSim.Bosses[_nullCityBossSlot].TelemetryInstanceId))` only if boss active and no existing `_spawnRewardRoot`; deferred children keep actual captured roots. Parent owns this patch.

## Verification actually executed

- Initial outside-Assets harness failed on absent new faction/root/content APIs; after implementation it passed.
- Five committed FactionRewardRulesTests run through real NUnit assertions in .superpowers/.../faction-harness/RulesRun.csproj: PASS 5, covering all faction pairs, shared finite boss/recursive allowance, stale handles/capacity, healing/overkill contribution and five archetypes.
- Managed GameSim harness using actual runtime source/Unity reference DLLs: PASS coincidence separation, earliest-body query truncating cover/interception segment, body callback/expiry and reset source on slot reuse. No Unity process launched.
- Full source + new native test compilation with real Unity references: FactionCompile.csproj PASS, zero errors (61 existing/project warnings at last check).
- 13 compiled PlayMode cases cover reciprocal real damage for each of five actors, locked target after death/reuse, same-side immunity, partial player score+XP+deathonce, STANDSTILL/stat isolation, projectile order/reuse, five-actor withdrawal/pause cleanup, finite carrier/boss roots, deterministic coincident separation and profile attention cap/zero-dt behavior.
- `git diff --check` clean (only CRLF conversion notices).

## Remaining parent gates / practical limits

Unity EditMode + PlayMode execution, content baking/import, real visual captures, golden/32-seed sweep and final native build are parent-owned and have not been claimed passed here. Parent main/render/reset/Null queue hooks must be integrated before those tests. Screenshot QA must verify loaded eight-pose white Resource art and warning sizing in native renderer. No hash repins made. The 24/64 allowances and pressure-scaled raid stats are explicit initial tuning values, not measured difficulty claims. Raid body ordering adds swept collisions only while a raid is active; ordinary no-raid hostile projectile collision retains baseline behavior. Existing exclusive Court/Null/Hydra controller fiction/colored factions were not reinterpreted.
