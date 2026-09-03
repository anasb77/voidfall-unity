# VOIDFALL — Full Game Overview
*Written Sept 2026 from a direct read of the Unity repo (`voidfall-unity`, commit `6877fe6`). For briefing an AI design lead with no repo access. Numbers are real constants from code, not aspirations.*

## 1. What Voidfall is

Voidfall is a **bullet-heaven survivor shooter** (Vampire Survivors genre) built in **Unity 6 (6000.5.7f1), URP 17.5, Windows x64**. The fantasy: you are trapped in a labyrinth of hostile realities called **Voids**. You enter a Void, fight escalating swarms, level a build, survive, kill its boss, step through a rift, **choose the next Void**, and repeat until the run ends. The design doctrine (post-spec-rewrite, Sept 2026) is **simplicity**: one shared loop for every Void, exactly **one hazard verb per Void**, authored bosses only where a map earns one, everything else from shared pools.

## 2. Core run loop (locked)

```
ENTER VOID → fight + level for 5:00 → boss encounter (1 or 2 bosses)
  → 14–22s breather → Boss Roulette prize → rift opens
  → choose next Void (route cards) → 1.8s collapse/settle transition
  → next Void (~7–9 min per Void, ~4 Voids per run ≈ 30–35 min)
```

- Survive phase: `VoidProgressionRules.SurvivalSeconds = 300`.
- Boss phase: `BossEncounterObjective` — completes only when **every** spawned boss dies (double-boss safe).
- Post-boss delay: deterministic `14–22s` from run seed + voids cleared.
- Death at any point ends the run (1 revive banked: `1 + WorkshopRank("protocol")`).
- Route choice pauses the game and shows cards with threat rating, hazard, objective, and reward identity. Picking one locks its siblings.

## 3. Moment-to-moment gameplay

- **Controls:** WASD/arrows/stick move, mouse/stick aim, LMB/Space/trigger fire, Esc/Pause. Twin-stick survivor shooter with manual aim and firing.
- **Combat:** pooled enemies/bullets/hostile-shots/pickups (no per-frame Instantiate/Destroy), spatial hash grid for collisions, fixed-timestep deterministic sim (`GameSim`) separated from presentation so replays reproduce exactly.
- **XP/levels:** XP gems drop from kills, magnet pickup radius (upgradeable), level thresholds in `BalanceRules`, level-up opens a 3-option draft (weapons/supports, with luck pity + repeat protection on roulette tables).
- **Pickups:** XP gems, rare pickups, boss Parts payouts, roulette chest spawned where the last boss of an encounter dies.
- **Survivability tools:** 0.65s i-frames on hit, dodge-chance support, revive prompt, adrenal surge on-hit buffs, regen, shields (guard enemies have them; player gets firewall-style shields via supports).

## 4. Buildcraft (the long-hours hook)

**6 weapons** (rank I–VI each, then evolution): Pulse Pistol, Scattergun, Railgun, Orbit Blades, Arc Lash, Seeker Launcher. Kinds cover projectile / scatter / pierce-rail / orbit / chain / homing-blast.

**15 supports** (10 core + 5 extra): Weapon Calibration (+dmg), Cycle Tuning (fire delay), Reinforced Frame (+max HP + repair), Servo Tuning (move speed), Pickup Coil (magnet), Targeting Optics (crit), Overload Core (crit dmg), Adrenal Surge (on-hit recovery/speed), Amplifier (size), Regenerator (HP/s), Reflex Matrix (dodge), Scholar (+XP), Fortune Magnet (drop chance), Velocity Coils (projectile speed), Spatial Awareness (camera dezoom).

**Evolutions:** max-rank weapon + max-rank paired support → evolution (e.g. Rail Lance with trail/damage windows). Checked by `EvolutionRules.IsReady`.

**Late upgrades** (post-core-progression): Output Tuning, Cooling Pass, Frame Patch — small repeatable stat pushes.

**Boss Roulette:** each cleared boss encounter opens a wager/prize ceremony (luck pity escalates tables, repeat protection, first-ceremony floor), resolved as a single prize-reveal card. Prizes feed the same upgrade-progress state as level-ups.

**Wild Cards:** run modifiers that break rules (e.g. **Colossus Arsenal** doubles projectile size; **Overclocker** holds a permanent boost floor).

## 5. Enemies & bosses

**14 base enemies:** Chaser, Runner, Gunner, Twin Gunner, Dasher, Brute, Exploder, Guard (directional shield), Technician (support), Mortar (telegraphed barrages), Splitter (fragments), Bulwark (facing-based damage reduction), Harvester (carries XP, cap 3 alive), Carrier (summons). Plus elite variants (stat/ability twists), a 96-band spawn timeline that escalates composition over the 5 minutes, formation events (walls, wedges, columns, phalanxes, closing arcs), and a harassment **Director** (warnings → events → recovery).

**Boss pool (random voids):** Herald, Warden, Matriarch, Reaver — drawn from `DirectorRules.BossEncounter(runSeed, bossSequence++)`, fully deterministic. **Double-boss roll:** 25% base +6% per cleared Void (cap 100%), both must die.
**Authored bosses:** **Hydra Prime** (12k HP, stationary, 4 attacks: marrow barrage, evasion sockets, rib cage that constrains the player, optic) and the **Twin Grandmasters** (Black + White, 9k HP each, shared-health pool, simultaneous fight with alternating lethal floor colors).
**Boss presentation:** spawn telegraph + name toast + music cue, charge VFX, death ring/bursts/shake, Parts payout, XP shower, roulette chest.

## 6. Voids & route (what exists vs planned)

Route graph is **data-complete, content-partial**: 10 nodes, 5 playable.

```
ABYSS ──┬── RED NEBULA ──────┬── DEAD ORBIT (data only)
        ├── WHITE SAKURA ─────┼── MONOCHROME COURT ──┤
        └── HYDRA ─────────────┼── NULL CITY (data) ───┤── LAST GATE (data) ── FINAL VOID (data)
                               └── GRAVEYARD (data) ───┘
```

| Void | Layer | One verb (status) |
|---|---|---|
| Abyss | 0 | Baseline escalation. PLAYABLE (generic survive+pool boss) |
| Red Nebula | I | **Meteor lanes**: drifting shootable meteor terrain + NEW lane strikes — 1.15s telegraph, 780 u/s cardinal streaker, 2400u lane, hits everything (240+time*0.3 to enemies, 28 to player), max 2 concurrent, every 15–23s. PLAYABLE |
| White Sakura | I | Elite surge (1.5x cadence + rewards). PLAYABLE visuals; moving-zone objective not yet built |
| Hydra | I | **Mutations**: Volatile/Rush/Ballistic/Regenerative genes (1 chassis + 1 gene max, Split gated off), 25/40/60% hybrid escalation, authored Hydra Prime. PLAYABLE, richest void |
| Monochrome Court | II | **Chess rules**: Pawn/Rook/Bishop/Knight/Queen roster, White=fast / Black=tanky, Twin Grandmasters + alternating lethal white/black floor (warn → burn → recover). PLAYABLE |
| Dead Orbit / Graveyard / Null City | II | Moving debris lanes / resurrecting enemies+Gravekeeper / security protocols. DATA ONLY (`ForArena` returns null → endless, no rift) |
| Last Gate / Final Void | III–IV | Boss gauntlet + Final Boon / Overseer + True Boss + escape ending. DATA ONLY |

Threat multipliers (1.0 → 1.2 → 1.5 → 1.85 → 2.2) exist in route data but are **not yet consumed** by the director — difficulty currently comes from composition/elites/bosses, and transitions reset the director (known spec violation to fix).

## 7. Meta progression & persistence

- **Workshop** (permanent upgrades bought with Parts), **Bestiary** (enemy discovery log), **Records** (best runs, totals), run history (12 recent runs).
- `SaveStore`: versioned JSON (`SaveVersion 5`), atomic write (temp + `File.Replace` + `.bak` + flush), sanitize-on-load (clamps, drops unknown IDs → old saves survive content expansion), live state cloned before sanitize. Saves only at run end/game-over — **no mid-run resume yet** (crash loses the run).
- Mute in PlayerPrefs; everything else in the save file.

## 8. Presentation & audio

- **Rendering:** URP (4xMSAA, HDR, 2048 shadows; ships on Ultra default — needs a pre-launch perf pass), neon-noir art direction, runtime-procedural sprites (4.7k-line factory, baked to catalog in Editor), prepared arena packages (plate + recipes ×3 per arena) streamed via Addressables, reactive music perimeter + arena flash/shake/ring-wave FX language.
- **Audio:** fully procedural SFX (`ProceduralAudio`, audio-thread safe via interlocked reset flags) + streamed reactive soundtrack (`MusicDirector`: per-stem intensity, damage notches, boss layers). No licensed music dependency.
- **UI:** uGUI screens (MainMenu, HUD, LevelUp, Roulette, PrizeReveal, RouteSelect, Workshop, Records, Pause, GameOver, Settings with resolution/display/bloom/chromatic options) + a debug IMGUI overlay that must be gated out of ship builds.

## 9. Technical architecture & quality gates

- Assemblies: `Core` (engine-free rules/RNG — determinism lives here) / `Content` (catalogs, spawn/upgrade/formation/reward rules) / `Persistence` / `Audio` / `UI` / `Runtime` (Unity composition; `VoidFallGameRuntime` is ~27k LOC across 19 partials — the bus-factor hotspot).
- **Determinism:** golden-master hash test (`SimulationGoldenMasterTests`, 32-seed sweep); intentional sim changes require re-pinning with explanation. RNG draw order is load-bearing — new features must roll from `_gameSim.Rng`.
- **Tests:** 250/250 EditMode + 6/6 PlayMode green (Sept 3). PlayMode covers golden master + flow regression.
- **Build:** single-scene Windows Mono build script → `Builds/VoidFall.exe`; Addressables must be built for arenas to appear; CI runs Unity tests only when enabled. Last verified release build + `productionMax` smoke (192 enemies, 2 bosses): pass.

## 10. Key tuning constants (live values)

Survive 300s · post-boss 14–22s · double-boss 25%+6%/clear · strikes 15–23s/9s grace/28 player dmg · explosive blast r128, 180 enemy dmg · player i-frames 0.65s · revive 1+protocol · harvester cap 3 · meteor terrain 3–5 ordinary + 2 explosive · Hydra Prime 12k HP, Twins 9k each.

## 11. Honest current state (for roadmap planning)

**Done:** full 5-min→boss→roulette→rift→choose loop on 5 voids; buildcraft depth (6 weapons, 15 supports, evolutions, roulette, wild cards); Hydra + Court as signature voids; Nebula strikes; save/tests/build green.
**Missing for launch:** Layer-II objectives (Orbit/Graveyard/Null City), Last Gate + Final Void + ending, threat-scaling wiring, mid-run resume, release config (company/app-id/version/splash/quality default), Addressables-in-build-script, IMGUI gating, min-spec perf pass.
**Known risks:** runtime monolith fix-latency; Ultra-default perf cliff; O(n) target scans bypass the grid in hot paths; 5-void runs drift to ~45 min (recommend 4 voids ≈ 30–35 min).
