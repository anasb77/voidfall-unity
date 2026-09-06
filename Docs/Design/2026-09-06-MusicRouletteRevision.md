# Music and roulette revision

Owner approved this revision after playtesting the initial music remix feature.
This supersedes the Magnet muffling and bomb echo in the earlier MusicRemix design.

## Music

- Bomb retains its volume dip/recovery but no longer requests repeated audio.
- Gameplay songs play once and advance through the existing shuffle bag. A
  Track Shift still uses its curated entry, but the next song starts at zero.
  Remix envelopes persist across both transitions. Menu looping stays intact.
- Completion requires observed playback, a loaded clip and 120ms of stopped
  playback; explicit focus suspension is excluded. It does not require observing
  the final half-second, which can be missed during a frame hitch.
- Magnet retains collected-gem buildup and the 25-second tail/green perimeter.
  It adds no low-pass muffling, caps bass drive at 0.45, narrows stereo by at most
  0.18, and adds a gentle 0.22-width release. Critical-health and upgrade muffling
  remain independent. Damage scratch, overclock accents and recovery stay.

## Roulette

- Removed the kicker, title prefix "The", subtitle, hub copy, idle reward
  instruction and bottom idle wager sentence. Title is "Void Roulette".
- Centered wheel targets 780 units (1.5 times the previous 520). It caps at
  viewport height minus 140 reference units to leave title/control clearance.
  Thus shorter landscape screens get a smaller increase instead of clipping.
- Original three action buttons use 60% of their previous width and height;
  their text remains readable. Spin is centered inside the fixed hub; wager
  buttons sit below the wheel.
- Rewards & odds opens a drawer. Rows still show current probabilities/effects
  and expose details on selection; the drawer retains refund information.
  Background actions are disabled while open, outside clicks/close/cancel dismiss
  it, and focus returns to Rewards. Present resets it closed and focuses Spin.
- Actual purchase/refund feedback remains visible. Spin duration, probabilities,
  payments and automatic reward reveal are unchanged.

## Scope and verification

Implementation is limited to MusicDirector, MusicReactiveState, RouletteView,
focused tests and RoulettePreviewCapture. Shared runtime/gameplay files are left
to the other agent working concurrently; no branch switching or bulk commits.

- 93 focused EditMode tests pass (`Logs/RouletteMusicRevision/editmode.xml`).
- 9 music runtime tests pass (`Logs/RouletteMusicRevision/playmode.xml`).
- Source review found and resolved the missed-final-frame silence case.
- Actual view capture covers main, drawer, selected details, wagers and reveal
  at 1280x820 and 1920x1080. Outputs: `Logs/RouletteMusicRevision/Captures/`.
- Subjective sound balance still requires the owner's listening pass.
- Windows build succeeded with zero build errors at
  `../Builds/MusicRouletteRevision/VoidFall.exe` (`Logs/RouletteMusicRevision/build.log`).
- In-game diagnostic opened the roulette, raised stakes, spun and resumed
  gameplay successfully (`ROULETTE PLAYER CHECK PASSED` in
  `Logs/RouletteMusicRevision/player.log`). The capture driver used isolated
  profiles; actual player screenshots are `player-wheel.png` and `player-resumed.png`.
- A synchronous Editor capture artifact after text changes was resolved by
  resubmitting the owned canvas graphics before each capture. Final main/drawer
  captures were visually inspected; production UI did not require a workaround.
