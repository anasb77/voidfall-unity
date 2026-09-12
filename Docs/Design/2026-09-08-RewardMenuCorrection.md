# Owner correction: same song, native upgrade menu

- Gameplay songs loop in full. Only Track Shift changes the song during the
  run; the first shifted entry may start partway in, then the song loops from
  zero. Recovery after unexpected stopping also restarts that same song.
- Roulette rewards use the existing LevelUpView and its original BuildCard
  styling, fonts, icons and rank pips. There is no separate PrizeReveal screen
  in the live roulette flow. Ordinary cards are clicked to claim each rank.
- Wild Cards expose their actual benefits/drawbacks and explicit Take/Leave.
  Leaving has no effect, payout, compensation or reroll. Runtime guards prevent
  stale callbacks from taking a declined card. Exports distinguish decline from
  grant, and controller focus moves to every new reward/action.
- Pending normal level-ups are restored after roulette rewards; their old
  selection/reroll actions cannot replace an unresolved reward.
- Random is bold 15px, with normal-weight stacked remainder at 12px (80%). Thin
  slices keep full readable text in connected exterior callouts, including
  60/90 Parts. Odds, spin timing and the 500 Parts reward remain unchanged.

Source: current `voidfall-director-worktree`, retaining unrelated Director and
run-export work. This supersedes the separate claim-card UI and automatic music
playlist descriptions in earlier design notes.

## Verification

- 50 focused EditMode cases passed (`Logs/RewardMenuCorrection/editor-final.xml`),
  including typography/callouts, normal-mode restoration and controller focus.
- 61 combined PlayMode cases passed (`flow.xml`): actual-stream same-song loops
  across all 15 tracks, Track Shift, claims, Wild Card decline/export and flow.
- 16 claim/shutdown regression cases passed (`shutdown.xml`) after guarding
  Unity views already destroyed during runtime teardown. This overlaps the flow
  suite and adds the destroyed-view boundary case.
- Actual upgrade-menu and wheel captures at 1280x820/1920x1080 were inspected.
- Current `../Builds/VoidFall.exe` rebuilt and the native spin/take/resume probe
  passed without an exception on shutdown (`player-final.log`). No unrelated
  Director or exporter work was reverted; audio effects were retained.
