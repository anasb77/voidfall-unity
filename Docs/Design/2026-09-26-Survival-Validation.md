# Survival revision validation — September 26, 2026

Implementation uses the existing `main` working tree, including earlier pending
project work. No feature branch or worktree was created. See the Life Steal and
Scavenger design contracts for approved values and HUD behavior.

## Automated evidence

- `Logs/survival-assets.xml`: 9/9 prepared asset checks passed, including all
  three Scrap keys and unchanged world-space sprite size.

- `Logs/survival-editmode.xml`: 31/31 rules/catalogue checks passed.
- `Logs/survival-final-playmode.xml`: 51/51 gameplay/HUD/export checks passed.
- `Logs/survival-playmode.xml`: the 32-seed repeatability sweep passed (each seed
  simulated twice). The first run also exposed the expected old golden hash and
  an obsolete swarm-clock assertion; neither represented a repeatability failure.
- New golden baseline: legacy `4093819818710876312`, full `1912782885852962356`.
  The unified 2% Scrap / 1-in-700 power-up chances and appended support catalogue
  intentionally change simulated outcomes and progression snapshot fields.
- Directed circle deployment no longer schedules the independent legacy swarm
  clock in the pre-existing director revision. Its test now asserts the unchanged
  clock instead of the obsolete reschedule; gameplay scheduling was not altered.
- Dashboard refreshed: 900 inventory checks and 4,474 legacy checks passed.
  Browser inspection confirmed all five Life Steal and four Scavenger ranks.

Tests and native capture probes isolate profiles and export directories.

## Windows player

Canonical `../Builds/VoidFall.exe` rebuilt from main, with all prior pending
project work included. Build GUID `9beeeffd8313449788662163f00fb79b`, built
2026-09-26 16:24:34 +01:00. The standard Windows builder succeeded; validated
replacement was promoted, hashes checked, and final canonical launch completed.
Temporary staging/rollback directories were moved to the Recycle Bin after
verification; existing run exports and pre-existing build folders were preserved.

Native captures in `Logs/survival-canonical/` verify 100/125/175 maximum HP,
50 current shields with expanded capacity, all three procedural Scrap variants,
and Life Steal V / Scavenger IV in the existing card renderer. The original
Scrap pickup audio call and cue were retained. No image generator was used.
Native export context confirms 0.20s immunity, 1/700 power-ups, 2% base Scraps
and survival rules v1. Final player log contains no exception/assertion errors.
The existing shutdown ComputeBuffer disposal warning remains outside this change.

Evidence: `Logs/survival-windows-final-build.log`,
`Logs/survival-canonical-player.log`, `Logs/survival-promoted-hashes.json`,
`Logs/survival-hud-comparison.png`. The hour-long combined healing balance still
needs owner playtesting; automated checks establish behavior, not difficulty.
