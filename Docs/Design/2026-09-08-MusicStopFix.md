# Streamed music completion fix and soundtrack audit

## Correct checkout

The current Windows build comes from `voidfall-director-worktree`, branch
`codex/director-redesign-2026-09-07`, base commit
`8f1fe6807fb68ce53393f946c8dfe5c105cb1e8f`. The older `voidfall-unity`
checkout does not contain that commit's new music. Initial absence findings
against the older checkout were superseded after inspecting the build source.

## Confirmed stopping bug

An actual imported Neon Street stream reached its end, changed from playing /
Loaded to stopped / Unloaded, and remained there. MusicDirector required
`AudioDataLoadState.Loaded` before advancing, so it could never proceed.
This was the existing completion check, not a change introduced by the new tracks.

Removed the load-state gate after playback has been observed. Startup detection,
explicit focus suspension, the 120ms stopped grace and channel switching remain.
The regression now plays/seeks every actual gameplay stream to its natural end,
cycling normal, overclock and critical-health rates, rather than relying solely
on generated AudioClip fixtures. Menu completion is covered separately.

## Audit of the four additions

All four installed OST files match the originals in
`C:/Users/anasb/Desktop/Potential new OSTs` byte for byte (SHA-256 comparison).
All decode successfully. The build source has 15 gameplay tracks and 3 menu
tracks; Beyond the Pixelated Horizon is also installed as a menu theme.

| Track | Verified duration, approximately |
|---|---:|
| Beyond the Pixelated Horizon | 211.1s |
| The Gentle Descent into the Cosmic Ruin | 197.1s |
| The Surveyor's Quiet, Amiga Reverie | 256.5s |
| Cyberpunk Theme 1 | 72.05s |

All 18 audio assets have distinct GUIDs. The additions use the existing streaming,
stereo, background-load, no-preload import settings. Curated Track Shift entries
are present, resolve by clip name and fit inside the imported clips. Existing
credit documents include EnthusiastGuy. Cyberpunk's supplied artist tag spells
the name `Smellycatcafe`; the project uses `Smellycatcaffee` for its existing credit.

Independent FFmpeg decoding confirms Cyberpunk is 72.048s, consistent with Unity
and its MPEG metadata. A SoundFile duration/read estimate of 122.7s was rejected
after this cross-check; the source was not truncated by the import work.

## Verification

- 75 focused EditMode tests passed: `Logs/MusicStopInvestigation/editmode.xml`.
- 11 PlayMode tests passed: `Logs/MusicStopInvestigation/playmode.xml`, including
  all 15 actual gameplay streams advancing at their natural ends.
- Older-checkout failing trace is retained at
  `../voidfall-unity/Logs/MusicStopInvestigation/red.log` as reproduction evidence.
- Roulette remains unchanged. Idle motion, submerged selection music, spin
  escalation, Rare Boon removal and individual claim cards are brainstorming only.
- Rebuilt the current `../Builds/VoidFall.exe` from this checkout and verified
  startup/rendering with an isolated profile. `Logs/MusicStopInvestigation/build.log`
  reports success; `player.log` and `player.png` capture the smoke check. Refreshed
  BUILD_INFO with the actual source hash and this run's validation (its inherited
  full commit hash did not resolve). Automatic approval review rejected deletion
  of the temporary rollback copy as "blocked by policy"; it remains at
  `C:/Users/anasb/AppData/Local/Temp/voidfall-music-rollback-f8b66c8276514e5699ad26f205a5bdfa`.
