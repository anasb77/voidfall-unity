# Approved map integration — validation

Owner approval: integrate the iterated browser study; final override keeps
Hydra II's old Unity map/art/boss encounter. Source work is in the existing
director worktree alongside preserved mines/incidents/audio/freeze changes.

## Delivered behavior

- Original Null City art at4× world coordinate scale, following camera,
  rendering-only player-size adjustment, correct welcome/lockdown sign and
  locally shaking laser roads. Native purge timings and enemy damage retained.
- Fixed soft-neutral Court board with129.6-unit tiles, bounded movement,
  viewport-aware enemy recycling, original contrasting chess sprites and
  original Grandmasters. Local danger cells snapshot once per phase, arm over
  two seconds, show six-segment bombardment markers and burst at3.4seconds.
  Native Grandmasters now warn/fire their original aimed volleys.
- Seeded spaced stationary rooks with100k–150kHP and contact-only damage;
  native approved scary standing/fallen artwork, tracking slit pupils and HP
  bars without numeric HP. Death emits the requested notice and queues5–6
  roster-one children once; full pools defer rather than discard them.
- Hydra I has360-second survival, no gene nodes/bone detail, fast downward
  original glyphs, five parent-derived hybrids and five retained Viruses in
  swamp colors. Existing collapse/swap/settle reaches the original Hydra II
  in the same visit without objective/pressure reset or premature rewards.
- Map-aware earned-loot clamping/recovery preserves currency while keeping
  drops inside enlarged City, Court and the original Hydra boss boundary.
  Export samples now use actual world dimensions; new events are documented
  in `Docs/RunExports.md`.

## Automated evidence

- `Logs/map-final-editmode.xml`:595 passed,zero failures.
- `Logs/map-release-playmode.xml`:302 passed,zero failures,one graphics-only
  skip. Includes the golden master and32-seed repeatability; hashes unchanged.
- `Logs/map-render-fix-playmode.xml`:45 focused follow-up cases passed.
  This suite overlaps the final full suite and is not added to its total.
- Total distinct reported full-suite passes:897.
- Initial failures found/fixed: native MaterialPropertyBlock construction in
  a MonoBehaviour field initializer; a Reclaimer test retaining extra harvestable
  XP; full-pool Hydra births; fixed1750-unit enemy recycling on a wider map.

## Exact player delivery

- `../Builds/VoidFall.exe`
- Last Modified: **2026-09-12 11:50:20 +01:00**.
- GUID: `b5738ed43dde432c84e1b65d0f83252b`.
- `Logs/map-release-build.log`: Windows build succeeded,256896198bytes.
- Renderer: Direct3D11, retaining the prior freeze-risk mitigation. The
  original owner's forced-kill freeze remains unproven as fixed.

## Rendered player validation

`Logs/MapReleasePlayer/mapcheck.json`: success, six1920×1080 captures,
stationary/invulnerable diagnostic input and manual phase clocks. This is
render/flow validation, not normal difficulty or750-enemy performance proof.

| State | Mean frame ms | Maximum measured frame ms | Bosses |
|---|---:|---:|---:|
| Null surveillance/sign |16.51|18.93|0|
| Null laser/sign |16.40|19.55|0|
| Court field/rooks |16.42|19.30|0|
| Court Grandmaster warning |16.53|19.47|2|
| Hydra I specimens |16.40|19.44|0|
| Original Hydra II |16.43|17.79|1|

The first diagnostic build exposed a1086ms Court render spike. Court spawn
sprites used noncanonical accent/ID keys, causing lazy runtime generation and
atlas work. Faction-specific prepared keys removed the spike in subsequent
same-procedure captures. Court also suppresses chromatic fringing while leaving
the saved setting intact for other maps. Source diagnostic corrections clear
synthetic carried-over bosses and synchronize HUD before capturing.

Final diagnostic export:153 written records,zero drops/errors,
`diagnostic_complete`,matching build GUID. Last Hydra sample records its original
1000×660 rib boundary. All test/player diagnostics use isolated profiles and
export folders. Existing user run exports remain in the canonical RunExports.

## Remaining validation scope

Owner playtesting is still needed for map feel, difficulty and long-run load.
Court/City specialist durability balancing was explicitly deferred and was
not silently folded into this physical-map pass. Hydra II art, boss HP and
attack mechanics retain their existing Unity definitions.
