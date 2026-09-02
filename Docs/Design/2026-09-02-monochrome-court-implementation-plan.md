# Monochrome Court Implementation Record

Status: implemented and validated on 2026-09-02.

## Shipped scope

- Prepared `monochrome-court` arena available in the main-menu carousel.
- One baked 4K base layer, one 1440p details layer and three lightweight recipes.
- Addressable arena package with deterministic residency and stable route identity.
- Exclusive Pawn, hybrid Rook, Bishop, Knight and Queen enemy roster.
- Black enemies enter from the left; White enemies enter from the right.
- Pawn pressure, cardinal Rook charges, Bishop sniper fire, allocation-free Knight L-path movement and Queen line control/promotion.
- Two stationary Grandmasters with one shared health pool and no ambient enemies during the boss phase.

## Twin Grandmaster fight

The discarded turn/vulnerability/Checkmate prototype is not part of the game.
Both bosses fight, remain visible and accept damage simultaneously.

The board uses a world-anchored alternating floor hazard:

- White warns for 0.9 seconds, burns for 2.2 seconds and recovers for 0.5 seconds.
- Black follows the same sequence, then the cycle repeats.
- Below 50% shared health, warning becomes 0.7 seconds and burning becomes 2.4 seconds.
- Only the controlled color is dangerous; the opposite color remains safe.
- Floor damage uses the normal player damage, dodge and invulnerability pipeline.
- Tile size and origin are cached when the encounter begins, so camera zoom cannot move checker boundaries under stationary actors.

Detailed timing and acceptance criteria are recorded in
[2026-09-02-monochrome-floor-hazard-design.md](2026-09-02-monochrome-floor-hazard-design.md).

## Validation evidence

- Focused post-review EditMode tests: 42/42 passed.
- Full EditMode tests: 242/242 passed.
- Full PlayMode tests: 5/5 passed.
- Windows release player: 159,256,272-byte build succeeded.
- White warning, White burning, Black warning and Black burning were captured and inspected in the standalone player at 1280x720.
- No missing required `.meta` files or duplicate GUIDs.
- Independent code review found no remaining Critical or Important issues.

The remaining product check is long-form balance through the natural survival-to-boss route; capture validation enters the boss encounter directly.
