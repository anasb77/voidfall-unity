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

## Delivered build and validation

The Windows player was built from `4c4aa72` with the canonical BuildScript and
Unity 6000.5.7f1. Build GUID: `9b08f7a79a274544bf92843c8b8cabb6`.
It defaults to Direct3D11. `Builds/BUILD_INFO.txt` records its provenance.

- Full EditMode: **622/622 passed**, `Logs/final-EditMode.xml`.
- Full PlayMode with graphics: **312/312 passed**,
  `Logs/final-graphics-PlayMode.xml`. Includes unchanged golden master,
  32-seed sweep, native Workshop artwork, journey/director, dealer transactions,
  save failure behavior, fragment assembly, manual damage and export attribution.
- Rendered Windows check: `Logs/UnifiedPlayerCheck03/success.txt` and its PNGs.
  Inspected both dealer placements, wrapping card copy and actual puzzle art,
  purchase grin, saved fragment purchase/assembly, both weapons and main menu.
  Weapons are posed by the capture probe; damage/timing are covered separately
  by gameplay tests. No physical gamepad session or long manual playthrough was
  performed.
- Promotion verified SHA-256 for all **217** payload files before updating the
  launch path in BUILD_INFO. Manifest: `Logs/unified-promoted-manifest.json`.
  Runtime assembly SHA-256:
  `C832ED57524C2320BD2B2F6D725D2E1C98485E63088EDBBBC79B18592D6CEB65`.
- Re-ran the rendered purchase/assembly probe from the final
  `Builds/VoidFall.exe` location: `Logs/CanonicalPlayerCheck/success.txt`.
  Player exited successfully with no errors/exceptions in its log.

Previous canonical player, DirectorBaseline, DirectorRedesign,
MusicRouletteRevision, CourtPerfPlayer and journey-previous-player are retained
under `../Builds/Archive/2026-09-12-before-unified/`. New debug symbols are stored
there separately. `../Builds/RunExports` remains in place. Diagnostic profiles
and exports live in Logs; the player probe isolates its profile before load.

Use `../START_HERE.md` for the single source/build entry points and controls.
Historical source worktrees remain recovery references. Further development
belongs in the canonical project; no remote push is part of this consolidation.
