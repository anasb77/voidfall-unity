# Court playtest corrections

The owner's native playtest supersedes the initial September20 Court integration.

- Board dimensions are doubled independently:56×56 tiles,7257.6×7257.6 world
  units. Tiles remain129.6 units and the camera remains908 units high (60%).
  Rendering updates visible tiles so the larger board does not quadruple the
  per-frame sprite/property work. The ten existing Sentinel/fallen placements
  sample the larger board; the number of hazards is unchanged.
- Each Sentinel's4×4 area attacks black cells, then white cells on its next
  cycle. The other color is safe. Warning, damage, shader and debris select
  the same color. Timing remains3 seconds warning,0.45 seconds burst,14 seconds
  between cycles, staggered between Sentinels. The eight-cell count clips at
  board edges. Shields still cover allies on both colors.
- Crowd separation deliberately remains able to displace Sentinels. The
  runtime synchronizes the eye, territory, shield and death position with the
  actual pooled enemy; no autonomous chase or rigid anchoring is added.
- A real shield grant displays exactly `Enemy shielded` in the existing HUD
  style. Simultaneous grants are coalesced with a1.5-second cooldown.
- Every pawn rank has eight points, preserving the eye, shell, colors and rank
  details. Mounted Pawn III riders and their horse chest crests share that art.
- The original geometric Knight returns alongside the newer horse-head and
  mounted Knights. Original Pawn, Rook I, Bishop and Queen designs remain in
  their existing first ranks. There are20 Court forms across both factions;
  all original families remain eligible from the opening waves.
- The black boss displays `Wing`, the white boss `Wang`; their shared HUD,
  arrival and encounter objective display `Wingwang`. Serialized boss IDs and
  native attacks/shared health remain unchanged. Both bosses and Sentinels use
  the same cell shader and debris helper, including reduced-motion behavior.

`court_board_created` identifies `courtRevision=2` and56×56 geometry. Sentinel
warning/burst events include color, cycle and current position; burst amount is
the actual selected-cell count. Camera/export schema compatibility is unchanged.

Validation:42 focused EditMode and20 PlayMode checks passed, covering alternating
safe cells, damage, crowd displacement, shields/notices, roster eligibility,
original Knight art, bounds, visibility culling, boss attacks and exporter data.
Native Windows validation is recorded in `Logs/CourtFollowup`.

The Windows build passed all15 diagnostic capture phases. Inspected images show
opposite safe colors across the two Sentinel bursts, matching Wingwang bursts,
the restored original Knight and eight-point pawns. Court camera height remains
908 units with normal74-unit player scale; the enlarged edge stays camera-bound.
These scripted poses verify presentation, not ordinary-run balance. The real
profile SHA256 was unchanged and diagnostic display preferences were restored.
