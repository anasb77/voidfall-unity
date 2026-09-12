# Director redesign implementation plan

> Agentic execution: use subagent-driven-development for isolated rule/render tasks and parent-owned runtime integration. One implementation subagent at a time; independent baseline validation and parent work continue alongside. No approval pauses between approved slices.

**Goal:** Deliver the complete approved director/pressure/onboarding/events redesign in the existing Unity game.
**Architecture:** Engine-free progression and encounter/profile rules, fixed-capacity identity-keyed runtime sidecars, existing combat/flow/rendering owners. No new simulation framework or network backend.
**Tech stack:** Unity6000.5.7f1, URP17.5.0, C#, existing uGUI and Addressables.
**Spec:** Docs/Design/2026-09-07-DirectorApproved.md.

## Global constraints

Read AGENTS.md and relevant REPO_MAP sections. Preserve Core/Content engine-free boundaries, finite Abyss-first route, exclusive arena owners, safe reward/travel/pause, saves and serialized IDs, approved art, weapons/evolutions/cards/Overclock, struct pools/order/identities and RNG separation. Parent alone runs Unity. Never commit another worker's edits. Do not re-pin golden hashes before explaining drift and passing32-seed sweep. Original branch and acf5103 remain recoverable.

### Task 1: Pure pressure, exact score and three inspectable profile rules

**Own files:** new Core/RunPressureRules.cs; new Content/DirectorProfiles.cs; new Tests/Editor/RunPressureRulesTests.cs. Normal namespaces are VoidFall.Core (Content also), tests VoidFall.Tests.Editor. Unity generates their .meta files.

**Interfaces to implement:**
```csharp
public enum DirectorProfileId { Standard = 0, Veteran = 1, Extreme = 2 }
public sealed class RunPressureState {
  public int PressureHundredths { get; }
  public double CreditedProgressSeconds { get; }
  public bool IsFrozen { get; }
  public void Reset(int ceilingHundredths, int visitCount);
  public void ObserveStage(int stageIndex, double survivalFraction, double bossFraction);
  public void Freeze();
}
public readonly struct FrozenRunScore {
  public long BaseScore { get; }
  public int PressureHundredths { get; }
  public int MultiplierHundredths { get; }
  public long FinalScore { get; }
  public FrozenRunScore(long baseScore, int pressureHundredths);
}
public static class RunScoreRules {
  public const int Version = 1;
  public static long FinalScore(long baseScore, int pressureHundredths);
}
public readonly struct DirectorProfileDefinition {
  public DirectorProfileId Id { get; }
  public string Name { get; }
  public string Recommendation { get; }
  public int PressureCeilingHundredths { get; }
  public double RecoverySeconds { get; }
}
public static class DirectorProfiles {
  public static DirectorProfileDefinition For(DirectorProfileId id);
  public static int PopulationLimit(DirectorProfileId id, int pressureHundredths);
  public static int AttackLimit(DirectorProfileId id, int pressureHundredths);
}
```

Pressure Reset defaults sanitize positive visits/cap, allocate per-stage high-water fractions once; no allocations Observe. Ignore invalid/nonfinite inputs rather than poisoning state. Clamp finite fractions0..1, index must be within allocated stages. Preserve high-water survival AND boss fractions separately, so stalled boss/healing/duplicate reports cannot earn twice or decrease progress. Pressure is floor(ceiling*sum(.8*survival+.2*boss)/visits), with a small numeric tolerance only to avoid double roundoff at exact endpoints. CreditedProgressSeconds=sum(300*survival+60*boss), independent of pressure ceiling. Freeze ignores Observe until Reset. No time source or Unity dependencies here; runtime supplies eligible progress. All new runs start0.

FinalScore uses max(100,pressureHundredths), all base score, integer arithmetic, one half-up rounding and nonnegative long saturation. Preserve exact published hundredths; no hidden precision. Test1,000,000*200→2,000,000;10,000,000*300→30,000,000;1000*37→1000;105*150→158;long.MaxValue overflow→long.MaxValue. Clamp negative score/pressure sensibly.

Profiles names exactly Director I/Director II/Director III. Recommendations beginner/veteran/extreme with III taunt YOU WILL NOT SURVIVE. Ceiling300/500/900; recovery6/4.5/3 seconds. Pressure fraction q below.30 /.30-to-below.70 /.70+ gives body ceilings64/128/192. Attention ceilings I1/1/2, II1/2/2, III2/2/3. Invalid profile falls back Standard. No statistical multipliers or player-performance adjustment.

- [ ] Write meaningful failing rule tests before implementation; examples:
```csharp
var pressure = new RunPressureState(); pressure.Reset(300, 6);
pressure.ObserveStage(0, 1, 0); Assert.That(pressure.PressureHundredths, Is.EqualTo(40));
pressure.ObserveStage(0, 1, 1); Assert.That(pressure.PressureHundredths, Is.EqualTo(50));
pressure.ObserveStage(0, .2, .1); Assert.That(pressure.PressureHundredths, Is.EqualTo(50));
Assert.That(pressure.CreditedProgressSeconds, Is.EqualTo(360));
```
- [ ] Cover reset/freeze, carries across stages, duplicate health reports, two-stage partial progress, NaN/infinite/invalid indexes, below-one score, rounding/saturation, all profile boundaries.
- [ ] Implement the rules; use a temporary dotnet harness if useful (SDK10 exists), retaining Unity NUnit tests in repo. Do not run Unity or change package dependencies. Report exact harness evidence and any pending Unity validation.
- [ ] Self-review and commit ONLY the three owned files; parent will import metadata and review. Return concise status plus report file.

### Task 2: Baseline diagnostics and Director I encounter orchestration

**Owner:** parent. Relevant files DirectorRules.cs, FormationRules.cs, runtime .Sim.cs/main.cs; new .Encounters.cs and scoped Core encounter rules/tests; StressBenchmarkProbe.cs and telemetry.

- [ ] Finish baseline EditMode/PlayMode and identify productionMax state/timing. Preserve baseline executable/replay evidence. Improve benchmark to prove active simulation, record current source/profile/clock and frame distribution/GC allocation rate.
- [ ] Add identity-keyed encounter membership, committed crossing movement, volley admission, bounded deployment/resolution/withdrawal/recovery. No deferred spawn debt. Correct phalanx headroom0–2 and wedge offset with regressions. Resolve concrete roster/threat before generic spawn admission; source-specific quotas/reservations for descendants/native paths.
- [ ] Replace infinite shared-boss ambient shielding with finite reinforcement waves and actual engagement windows. Preserve Matriarch/native boss identities. Precision rail acquisition only if real damage-access tests justify it; other weapon identities unchanged.
- [ ] Retain killable fodder using bounded progression/profile tuning, removing redundant unbounded health inflation from that role. Validate baseline movement response geometry, event arrival and clear benefit. Integrate pressure-progress time replacement for relevant spawning only.
- [ ] Run focused rules/runtime tests; record player-visible behavior and remaining human playtest needs.

### Task 3: Pressure runtime, result snapshot, onboarding and compatible saves

**Owner:** parent or one scoped implementer after Task1. Files new .Pressure.cs/.DirectorMenu.cs, UI/HUD/result views, Persistence/SaveStore.cs and .Persist.cs, existing runtime lifecycle/flow hook calls.

- [ ] Integrate Task1 state into new-run reset, authoritative combat step/objective progress, full boss-encounter health high-water (Court shared health once), travel carry and terminal pressure freeze. First empty1.5-second opening; no initial six enemy spawn in normal runs. Diagnostic legacy fixture behavior must be deliberate.
- [ ] Render PRESSURE0.00× directly below run timer; freeze exact snapshot after earned terminal rewards/upgrades settle. Bound time-derived base bonus to credited progress seconds, preserve kill/level/reward base categories. Use exact64-bit final record separate from legacy fields as needed; update display consumers together.
- [ ] Fresh firstPlay selectsI. Completed win/loss then shows profile choice, remembered selection and quick replay; legacy completed players get introduction. Preserve save rollback/retry. Version migrations scoped to historical thresholds, especially prior protocol refund. Existing scores retain legacy version/data.
- [ ] Result shows director/outcome/build, base score, pressure, effective multiplier/floor, final score and local rule version. Immutable result drives save/UI/telemetry/retry once.
- [ ] Tests for all pause/safe/time-scale/result/onboarding/migration and double-boss/terminal XP paths.

### Task 4: Event admission, adapted Black Hole and atmospheric Eclipse

**Owner:** one scoped implementer for rules/rendering; parent hooks runtime. New event rule/runtime partials, shader/asset authoring, tests. Keep native arena render ownership.

- [ ] Build one-at-time event state with seeded sparse opportunity windows/minimum240s gap; dropped ineligible opportunities; full duration admission before boss lead-in. Initial eligible Abyss, extend open arenas only after tests. Transitions/death/restart clean bodies, hazards, modifiers and visuals.
- [ ] Inspect and import the supplied shader to scoped third-party resources via safe editor tooling. Adapt world-centered radial mask/refraction for orthographic rendering; never imply transparent layers enter opaque texture automatically. Preserve essential cues. No global pipeline toggle as a shortcut. Compile and capture actual native effect.
- [ ] Fixed world center,2.5s warning/10s active/1.5s smooth release. Player peak pull at most baseline speed/3; ordinary enemies3x that. Bounded inner falloff, body separation, no stored impulse/damage/consumption. Exclude bosses/elites/objects/projectiles/pickups. STANDSTILL input contract retained.
- [ ] Atmospheric Eclipse2.5s warning/1.5s fade-in/18s active/2s lift, environment-only80% darkening; no blindness. Respect reduced effects and high contrast. Add focused behavior and visual integration tests.

### Task 5: Destroyer factions, approved five-creature art and bounded rewards

**Owner:** one scoped implementer for new partial/rules/assets, parent connects damage/spawn/shot/death hooks. No replacement GameSim. Sidecars hold faction/identity/source/contribution and release on slot reuse.

- [ ] Tiny Maw squad establishes reciprocal ordinary↔Destroyer attacks, player attacks both, no friendly fire. Local staggered target scans and stable telegraph target; valid fallback after target death/reuse. Existing player-only controllers/shot/damage helpers must have explicit source context, not fake direction changes.
- [ ] Propagate ownership through contacts/shots/explosions/children and death. Effective player damage contributes fraction of finite bounty; rival-only score0; ordinary XP/drop allowance collectible independent of last hit. Preserve original escape retirement XP/Parts. Bounded spawn-source reward credits prevent infinite descendant farming; once-only death.
- [ ] Integrate all five latest approved black/white art designs using native imported sprites/pose frames and existing render cache/metadata conventions. Maw/Razor rushing, Husk/Grasp brute patterns, Spite throat volley. Preview references are appearance source, not source-of-truth simulation. Finite squad/withdrawal; clean attacks/projectiles/source references before native boss or travel.
- [ ] Validate faction filters, contact/shot ownership, player perks not applying to NPC damage, reward attribution, invalid identities, pause/reset/withdrawal and dense collision cases. Then admit raids through shared event scheduler.

### Task 6: Whole-run validation, performance envelope and delivery

- [ ] Review all changed runtime paths and preserve specialized eight-arena ownership; update REPO_MAP only for actual ownership/contracts. Add concise tuning/decision record and diagnostic switches.
- [ ] Run full EditMode, PlayMode,32-seed repeatability, explain/re-pin intentional golden hash drift, Windows build. Use isolated profiles and real advancing probes; source inspection alone is not performance/fun evidence.
- [ ] Benchmark192 and candidate300/500/1000 if technically meaningful, including late arsenal, dense BlackHole, faction combat, deaths/pickups/chains. Report CPU/GPU/alloc/frame distributions and limit to measured capacity; no adaptive gameplay reduction. No minimum-hardware certification without a declared machine.
- [ ] Capture native encounters/events/HUD/results and verify user-visible player; protect existing main build until replacement passes. All approved functionality must be implemented before final completion; report honest human playtest limits rather than stopping after one slice.
