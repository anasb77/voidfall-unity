# Consolidated source provenance

Canonical source: `C:/Users/anasb/Desktop/voidfall/voidfall-unity`.
Canonical Windows player: `C:/Users/anasb/Desktop/voidfall/Builds/VoidFall.exe`.

Recovery commits preserved pre-integration work:

| Source | Recovery commit | Unique work retained |
| --- | --- | --- |
| main | aad7792 | Menu/settings/Workshop refinements, Scraps display, forms, death attribution |
| director worktree | 1074ba9 | Director and faction combat, maps, balance, music recovery, roulette claims, run exports |
| journey worktree | 5e665a0 | Journey history and opt-in mesh/performance diagnostics |

Integration commits: `9d668a2` combines main/director; `5bc8728` incorporates
journey work. Projectile faction metadata and killer labels now occupy separate
sidecars. Existing main-menu shuffle behavior and later director combat/music
fixes are retained. Journey diagnostics do not enable release frame-timing
instrumentation by default.

The plain `voidfall-journey-integration-backup` contains 24 files. All 23 game/
design files other than its old REPO_MAP match objects already preserved in Git
history. Its remaining map is an older navigation document superseded by the
current map, not another gameplay implementation. Historical source directories
remain recovery references; build from the canonical project above.

Baseline: merged Unity compilation succeeded; all 607 pre-feature EditMode
tests passed. The native feature subsequently passed 620 EditMode and 309
PlayMode checks, including the unchanged golden master and 32-seed sweep.
Independent review led to distinct late-stock fallback offers and full run
export attribution. The follow-up added targeted checks for those changes and
fixed a real Sound Blade hit-timestamp leak across runs/slot reuse.

See the unified dealer release plan for the current verification/build status.
