# Eon Sea Unity implementation plan

Spec: `Docs/Design/EonSea-Approved.md`.

1. Correct browser Regular star lineage, export approved glacier and68shared/elite sprite forms offline; prepare native visual assets and arena catalogue/Addressables entries. Parent owns art/content/baking/render integration and docs.
2. Implement deterministic glacier terrain, persistence, collisions, melting/explosion stress, freeze/slip state and runtime lifecycle in dedicated EonSea core/runtime partials. Terrain worker owns those files/tests; coordinate narrow existing-runtime hook signatures.
3. Extend native shared/elite tiers and behaviors, display naming, source-preserving rendering hooks and relevant tests. Roster worker owns shared roster rules/runtime integration (including `.Sim.cs` and `.Render.cs` hook edits); parent/terrain worker request hook insertions through it.
4. Bake/import through Unity; run focused and full EditMode/PlayMode validation as appropriate; build a separate Windows validation player; capture actual game artwork and verify terrain/roster lifecycle. Review changes and report exact evidence.

Shared-file contract: parent owns `.Arena.cs`, catalogue arena IDs/route metadata/objectives, editor assets, new visual types and `.EonSea.Render.cs`; roster worker owns `.Sim.cs`, `.Render.cs`, `CombatStateTypes.cs`, roster-related Content/Core and `.RosterProgression.cs`; terrain worker owns `Core/EonSeaTerrain.cs` and runtime `.EonSea.cs`. Composition-root edits coordinated by parent. No generated catalogue/assets hand edits; no unrelated work restored/staged. Existing workspace is explicitly user-authorized active checkout with concurrent work; retain it and preserve foreign edits.
