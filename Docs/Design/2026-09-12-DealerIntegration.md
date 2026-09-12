# Dealer and legendary integration — approved conversation scope

The September 12 browser studies are the visual/interaction reference:
`../Prototypes/dealer-travel`, `../Prototypes/dealer-shop`, and
`../Prototypes/legendary-lab` (relative to the project root).

The user requested implementation in Unity and consolidation into one new
Windows build. Preserve distinct work in main, director and journey copies;
the newest existing player was built from the director worktree, while main
contains newer menu, Scraps and form changes. Recovery commits precede merging.

## First production integration

- After completed-Void rewards, enter a safe crossing, including single-exit
  routes. No shop after final victory. Runtime owns flow and pauses.
- The hovering ASCII dealer chooses top/bottom placement once per crossing.
  Preserve the same recognizable face, head turn, breathing, moving hair and
  purchase grin. Four accepted expression studies retain the same silhouette.
- Approach and press E (controller equivalent) to browse. Prompt: `E Browse`.
  Three offers; one purchase per crossing; each costs 100 run Scraps.
  Browsing/reopening is free and never rerolls stock. Shop cards show their
  icon/puzzle piece, name and description, with price below, and no headline
  or explanatory chrome. Allow closing/skipping without payment.
- Integrate the current tested card slice: 20 shield, delayed damage
  (-20% for four active combat minutes then +40% this run), and one extra
  projectile on an eligible lowest-ranked normal weapon. Preserve normal
  stat/damage paths. Larger draft card pools are not silently approved.
  More Health and Recovery Plan supply distinct fallback deals when those
  primary choices are exhausted. Recovery Plan adds five healing charges;
  each earned level consumes one and heals 3% maximum HP, capped at maximum.
  Repeated fallback purchases add charges instead of wasting an active effect.
- Include actual Sound Blade / Charged Rifle fragment artwork from the lab.
  Three distinct pieces assemble each weapon. Save permanent piece ownership
  atomically; no duplicate fragments, no lost profile on failed writes.
  Completion equips the weapon in a separate manual slot. Keep automatic
  arsenal slots and evolutions independent. Run equipment/modifiers reset.
- Port the two original manual weapon behaviors and their lab tuning through
  existing input, fixed-step combat, damage and presentation. Keep musical
  waveform geometry cosmetic. Cancel held inputs across modals/focus/travel.
- Roulette fragments and the three alternative legendary candidates remain
  explicitly deferred. Prototype fake income/next-crossing controls do not ship.

## Verification and delivery

Compile the consolidated baseline before feature edits. Preserve golden-master
contracts, existing saves, GUIDs, authored maps, director balance, menu/forms,
music fixes and run exports. Run relevant rule/save/flow tests, the golden sweep,
and a real Windows build. Exercise actual crossing, purchases, fragments and
legendary attacks with an isolated diagnostic profile and captures.

Canonical source: `voidfall-unity`. Canonical player: `../Builds/VoidFall.exe`.
Preserve the previous player until its replacement passes validation. Old
archives and worktree-only content are not discarded to make the folder tidy.
