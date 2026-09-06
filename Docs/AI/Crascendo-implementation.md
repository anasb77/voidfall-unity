# Crascendo — Unity implementation

Complete September6,2026. The owner-approved spelling is **Crascendo**, stable ID`crascendo`, appended ArenaId7. Existing dirty Unity work was preserved; no commits or restores were made.

## Implemented behavior

- Standard shared enemy/elite rosters I–IV, governed by existing global run progression, and a random shared boss. Native five-minute survival, boss reward, loot collection and escape countdown remain authoritative. No exclusive roster or boss was introduced.
- Every actual positive damaging hit adds20%of spawn radius, additive, capped5×. Radius drives both visible body and physical hitbox. Health decreases normally; the modifier adds no speed, health, damage or attack-stat boosts. Absorbed shield damage counts; zero damage, inactive/dead actors and native boss immunity do not grow.
- Maximum-size deaths emit one non-damaging pushback pulse affecting nearby surviving enemies and bosses. Pulse itself changes no health or growth. Boss sidecars initialize at native spawn, preserving pulse momentum when the first hit follows a pulse.
- Native harvester XP size gain remains additive alongside hit growth and respects the same cap. A hit cannot shrink it. Native natural growth can bring a harvester to the cap slightly before20hits; other bodies reach5×on hit20.
- Approved world-space obsidian geometry smoothly crossfades Indigo/Amber→Violet/Coral→Crying Violet using local survival progress. Boss/reward stages hold maximum violet. Animated source tears and subtle wash overlays continue through the reward/escape window under native pause ownership. No planets, floating diamond props, actor recoloring or HUD grading were added.

## Native integration and ownership

Eight prepared catalogue nodes now produce widths1/2/2/1/1/1 with six visited arenas per route. Existing legacy endless rotation remains unchanged. Core IDs/stable lookups, objectives, route metadata, menu residency, arena lookups, baking and Addressables registration all include Crascendo. Three generated recipe addresses were verified.

`VoidFallGameRuntime.Crascendo.cs` owns spawn-ID/telemetry-ID sidecars. Native damage/death boundaries invoke growth/pulses; no golden-hashed actor fields changed. Optional wider enemy queries cover5×hitboxes, including within a damage pass, only in this arena. `GameSim.EnemyNaturalRadiusHook` reconciles harvesting with hit growth.

`Tools/Crascendo/export-crascendo.cjs` retains the approved art source under`approved/` and exports three matching floor textures, tear positions, wash overlays and the required plate/detail textures. `Editor/CrascendoContentBaker.cs` imports/bakes the plate-owned`CrascendoVisualAsset` and uses the existing Addressables migration. Runtime views detach before outgoing package release. Unity generated the new `.meta`files; generated assets were produced through the baker.

## Verification evidence

| Check | Result | Evidence |
|---|---|---|
| Fresh pre-change EditMode baseline |368/368passed|`Logs/crascendo-baseline.xml`|
| Integrated full EditMode |370/370passed|`Logs/crascendo-editmode.xml`|
| Final art/package bake and validation |Exit0|`Logs/crascendo-bake-final.log`|
| Final full PlayMode |**71/71passed**|`Logs/crascendo-playmode-final.xml`, `.log`|
| Final Windows build |**Succeeded,0errors**|`Logs/crascendo-build-final.log`|
| Final player captures |5/5completed and inspected|`../Builds/CrascendoValidation/{early,mid,late,growth,boss}.png`|

PlayMode covers normal/elite positive hit growth and cap, zero hits, unchanged combat stats, pool reuse, native lethal-hit pulse and once-only behavior, enemy/boss survivor health and radius, native harvester absorption followed by damage, expanded AoE hitbox queries, ordinary-arena reset, reward pause ownership, and native boss spawn→pulse→first hit. Existing golden master and32-seed sweep pass without a hash update.

Earlier validation attempts exposed only new test-fixture reflection/private-type access mistakes; those were corrected. Final results above supersede them. Focused review caught the harvester radius conflict and frozen reward tears; both were fixed and regression-tested. Parent/reviewer subsequently accepted the scoped changes.

## Deliverables

Player:`C:/Users/anasb/Desktop/voidfall/Builds/CrascendoValidation/VoidFall.exe`

Final player captures in the same directory:
- `early.png`: live indigo/amber arena, ordinary shared roster.
- `mid.png`: violet/coral midpoint.
- `late.png`: crying-violet endpoint and shared tierIV enemies.
- `growth.png`: giant regular and elite bodies beside ordinary-size controls.
- `boss.png`: shared Warden at5×size on maximum-violet ground.

`Tools/Crascendo/capture.ps1` reproduces these optional diagnostic poses using isolated adjacent save profiles. Diagnostic captures stage palette/growth/global time; production uses native cadence. Capture timers visibly advance, verifying active player simulation. All five named player logs contain no gameplay exceptions/errors; they retain the pre-existing ComputeBuffer disposal warning during shutdown. Captures validate rendering and integration, not exhaustive long-run balance/performance.
