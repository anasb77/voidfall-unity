# Director I version 2 — delivery evidence

Implemented September 8, 2026 in `voidfall-director-worktree`, base `8f1fe68`
plus uncommitted director/exporter and parallel audio/roulette work. Existing
art, map dimensions and health/stat formulas were preserved. Difficulty7/10
is an owner playtest target, not a certified human experience.

## Automated checks

- `Logs/director-i-red.xml`: five expected failures on old behavior:64-body
  admission, stopped arrivals, age departures, Eclipse spawn suppression,
  four simultaneous dasher commitments. `director-i-green.xml`: five passed.
- Added projectile/dead-owner/slot-reuse, freeze, native queued burst, finite
  recovery, recent-beat exclusion, damage-relief expiry, diagnostic lifetime,
  incident admission and distant-actor re-entry coverage. Incident admission
  and natural re-entry each had separate observed red→green checks.
- `Logs/director-i-editmode.xml`: **25 passed**, including750 same-cell grid
  query/bounded buffers and existing encounter/pressure rules.
- `Logs/director-i-verified-playmode.xml`: full suite **217 passed,0 failed,
  1 graphics-dependent skip** (218 total), including32-seed repeatability.
- Three stale roulette tests now exercise the already implemented explicit
  Claim UI via `RouletteClaimTestActions`; reward behavior was not changed.

The productionMax golden intentionally changes: EnemyFill=1 now fills750
slots instead of192, and its hash includes pool lengths, actors/RNG/effects.
Measured legacy8416978558111970584 / full1951251204707846202 were pinned only
after32-seed repeatability passed. Stress still bypasses normal I pacing.

## Windows player

`Logs/director-i-delivery-build.log`: build succeeded, exit0,256825949 bytes,
at `../Builds/VoidFall.exe`. Runtime build GUID:
`85148a26a6124830aa856a6884d672bc`.

Both captures used rendered1920×1080 players and isolated diagnostic profiles
and journals. Reported hardware: AMD Ryzen9 5900HX / NVIDIA GeForce RTX3080
Laptop GPU. No GTX1060 measurement was made.

## 750-body synthetic workload

Artifacts: `Logs/DirectorI750/benchmark.json`, `750-enemies.png`, `RunExports/`.
Existing productionMax fixture, `-vfhold750`,8s warmup/20s measurement.

| Metric | Observed |
|---|---:|
| Post-refill fixed-step population | min750 / max750 |
| Hold steps / refill failures | 1646 / 0 |
| Minimum before refill | 685 |
| Actual combat time advanced during measurement | 12.93s |
| Median / p95 / p99 frame duration | 17.98 / 19.99 / 21.98ms |
| Maximum frame duration | 28.48ms |
| Mean simulation CPU time | 2.81ms |
| Journal records / reported drops / errors | 13563 / 0 / 0 |
| Maximum on-screen enemies in exported samples | 494 |

The ceiling means750 active simulated actors, not750 simultaneously visible
sprites. This was roughly50–56 FPS, not locked60. The synthetic fixture uses
a25-minute test loadout and invulnerability; it is not normal difficulty data.

## Normal Director I pacing

Artifacts: `Logs/DirectorINormal/benchmark.json`, `director-I.png`, `RunExports/`.
Run `26081dd51f6547529ec2c3be41c1a427`; `-vfscenario=directorI`,3s warmup/120s
measurement. Scripted collection/avoidance input, real health, fresh starting
build and real first-offered upgrades. The screenshot is the opening, not a
representative peak-density frame.

Observed120.10s run: **312 kills, level5,15 damage taken, peak191 active enemies,
peak159 on-screen**. No sample after the first5 seconds had zero enemies on
screen. Selected Pursuit at24.01s, Breakthrough at64.83s and Flank at110.63s;
no Crossing/Volley or immediate repeats. Hunt remains another eligible choice
and is covered by integration tests. Median18.09ms / p95 18.50ms.
Journal: **3479 records,0 reported drops/errors**, final quit recorded.
The automated pilot does not establish human flow or a7/10 difficulty rating.

## Remaining priorities

Owner playtest: Director I in Abyss first. Map size, then cross-world health
normalization remain pending. Native health cliffs were not declared fixed.
750 is a tested ceiling on this machine; locked60 optimization and lower-end
hardware validation remain separate measured work. No diagnostic player or
Editor was left running.
