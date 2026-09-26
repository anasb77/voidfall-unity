# Unified drops, Scavenger and survival HUD — implemented

## Current approved implementation (September 26)

The owner's latest instruction authorizes implementation directly in the main
checkout and canonical Windows build, superseding the earlier deferrals below.

- Ordinary eligible deaths use 2% Scraps and 1/700 power-ups in every arena.
  Null City's special drop reduction is removed. Elite/boss guarantees and
  power-up type weights remain unchanged.
- `scavenger` has four ranks: +5/10/15/20% relative ordinary Scrap chance,
  yielding 2.1/2.2/2.3/2.4%. Each rank replaces the previous bonus.
- Every 200 collected Scrap value grants 5 shield. No spending is required.
  Consolidated pickup value counts; direct wallet rewards from elites/bosses,
  opening balance, spending and refunds do not. No pre-acquisition credit.
  Remainders survive upgrades/transitions; counters reset on a new run.
- A shared shield pool replaces the Dealer-only presentation. Capacity starts
  at 20; repeated +5 grants cannot enlarge it. Larger future grants or explicit
  capacity sources can raise it. Overflow grants are consumed, with no bank.
  Damage absorbs shield first, then HP. The planned level-based shield card
  itself remains unimplemented.
- HUD: shield immediately above HP, shield track 88% of HP width;
  SHIELD text is 25% smaller than HP text. Labels/values sit on the right.
  Maximum HP scales bar width proportionally from 100, capped at available HUD
  space (37 approved layout units). Shield value has no fixed denominator.
- Procedural Twin Plates, Open Frame and Cell Remnant alternate by pickup pool
  slot modulo three. Same value and rarity, no combat RNG draw. Existing Scrap
  pickup sound and playback remain unchanged. No generated-image asset is used.

Implementation owners: `SurvivalSupportRules`, `SurvivalSupportCatalog`, runtime
`.SurvivalSupports.cs`, `.Sim.cs` and `ProceduralSpriteFactory.Scraps.cs`.
Counters, effective chance, grants, absorption and actual healing appear in run
exports. Tests cover card caps, pickup consolidation, full-health behavior,
shield capacity, actual export contents and HP width growth.

## Comparison with the original rates

| Ordinary eligible death | Original general arenas | Original native Null City | Implemented unified base |
|---|---:|---:|---:|
| Scraps | 4.5% | 2% | 2% |
| Power-ups | 1/300 | 1/600 | 1/700 |

The intermediate 1/500 general / 1/1000 Null City change and the proposed
unified 1/1000 rate are superseded by the owner's final 1/700 choice. Null City
XP adjustments, fixed elite/boss rewards and power-up type weights are outside
this unification. Identical per-kill rates still produce different income per
minute in arenas with different kill density.

## Balance intent and visual reference

At Scavenger IV, 500 eligible ordinary kills/minute yield an expected 12 Scrap
spawns/minute: 5 shield about every 16 minutes 40 seconds, or 0.3 shield/minute.
At 2,000 kills/minute, expect 48 Scraps/minute: 5 shield about every 4 minutes
10 seconds, or 1.2 shield/minute. These estimates assume every Scrap is collected
and exclude other reward sources. It is a supplement to survival, consistent
with scarce healing and eventual ownership of every support in long runs.

Approved browser reference: `../Prototypes/scrap-shield-study/`, glyph cases
1, 6 and 9 in `app.js` (gallery 02 Twin Plates, 07 Open Frame, 10 Cell Remnant).
The other 17 variants remain exploration. The approved family contains three
separate silhouettes, not a merged hybrid. The generated-image concept was
rejected; only deterministic code-drawn artwork is used. The latest main-game
HUD revision supersedes the browser's illustrative fixed shield denominator.
