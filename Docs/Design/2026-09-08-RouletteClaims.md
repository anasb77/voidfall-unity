# Mandatory roulette claims

Owner approved claim stage only, with no roulette audio or idle-motion changes.

- Landing resolves the actual reward but does not grant it. Each card has an
  explicit Claim button. Weapon/support +2 rewards target one card and grant one
  rank per claim; cards show the real name, rank transition and benefit.
- Existing rank caps remain: if only one rank fits, one real rank card is shown.
  No eligible rank/new card produces the existing 40 Parts fallback, also claimed.
- New cards, Parts and Wild Cards are committed on Claim; random power-ups
  materialize at the player for later pickup. A full pickup pool produces a
  claimable 40 Parts fallback. No fake extra rank cards or lost physical gifts.
- UI blocks carried-over/double submit; runtime generation/index guards reject
  stale callbacks across cards, ceremonies and new runs. No timer auto-claims.
- Wagers settle once at landing. The escape countdown and simulation remain
  paused until all actual cards are claimed. Returning to a new run clears the
  old queue. Export records preserve pending and committed outcomes separately.
- The legacy RareBoon slot retains its ID, weight and tier but now pays exactly
  500 Parts with no health restoration or score reward. Other odds are unchanged.
- Wheel and drawer use Random New Card, Random Card Upgrade +1 Rank, Random
  Weapon Upgrade +2 Ranks, Random Support Upgrade +2 Ranks, 500 Parts, and Random
  Power-Up Drop. Wheel words stack vertically at a readable font size; geometry
  stays inside the slice throughout rotation. Physically tiny protected slices
  use the full details drawer rather than overflowing or microscopic text.

Ownership: runtime `.RouletteClaims.cs` owns planning and guarded commits;
`.Roulette.cs` retains telemetry aggregation and ceremony lifecycle;
`PrizeRevealView` owns card presentation/submit gating. The concurrent run-export
work is preserved. `RouletteClaimIntegrationTests`, `RouletteLabelTests` and
`PrizeClaimViewTests` cover the changed behavior. The opt-in journey probe now
claims presented cards; normal gameplay never auto-claims.

## Verified delivery

- 47 focused EditMode checks passed (`Logs/RouletteClaims/editor.xml`).
- 47 PlayMode flow/export checks passed (`Logs/RouletteClaims/flow-final.xml`),
  covering claims, caps/fallbacks, stale callbacks, cost settlement, pause/escape,
  exported committed deltas and the existing roulette/runtime boundaries.
- Actual view captures at 1280x820 and 1920x1080 show the wheel, drawer, two rank
  cards and the 500 Parts card without text overflow. Extremely narrow protected
  slices retain full wording in the drawer instead of shrinking the text.
- Current `../Builds/VoidFall.exe` rebuilt successfully. The native journey probe
  opened/spun the roulette, displayed and claimed a New Card reward, then resumed
  play (`ROULETTE PLAYER CHECK PASSED` in `Logs/RouletteClaims/player.log`).
- Audio assembly SHA-256 stayed unchanged across the build:
  `5D1320781DE44F2BE793084FE07B11F0A9D8AAE340E18ECC3A2620F90C25BE6F`.
