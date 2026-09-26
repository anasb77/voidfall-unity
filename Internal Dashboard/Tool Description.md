# Voidfall Internal Dashboard

This is an internal development tool for Anas: a bird's-eye view of Voidfall's
current Unity content and five early browser versions. It helps take stock,
compare what survived the port, inspect missing/changed content and decide what
to migrate later. It is a development companion, not a player feature or Unity
build dependency. Opening or refreshing it does not modify gameplay or saves.

Its canonical location is **`Internal Dashboard/` in the game Git repository**,
beside `Assets/`, `Packages/` and `ProjectSettings/`.

## Open

Double-click `Open-Dashboard.cmd` for `http://127.0.0.1:4326` (Node.js required),
or open `dist/index.html` offline. The static viewer bundles data, native art,
fonts and source evidence; it works without Unity, a server or the Desktop
legacy folder.

Sections: Cards, Decks, Weapons, Enemies, Maps, Assets and **Legacy Voidfall**.
Cards compare families vertically and authored ranks horizontally. The legacy
archive offers an overview, version/status filters, original artwork, current
counterparts, every card rank, version history and source references.

## GitHub inclusion

**Commit and push this folder with the game project.** When Anas asks an agent
to commit or push completed project work, include relevant changes in
`Internal Dashboard/`. Do not exclude it, move it outside the repository or
create a separate Git repository. This is a standing inclusion instruction,
not a request to automatically push now.

Include the frontend, launchers, exporters, lockfile, documentation, raw
catalog dumps, generated JSON/JS snapshots, native artwork, source evidence
and validation reports. The generated files under `dist/` are intentional
deliverables so a fresh checkout can view the tool immediately. `.gitignore`
excludes compiler/cache/runtime files and local verification screenshots.
The C# exporter project is explicitly unignored.

Before an authorized push, check `git status --short -- "Internal Dashboard"`
and include the tool changes. Preserve unrelated concurrent work.

## Agent maintenance

**Update this tool after changing cards/ranks, weapons/evolutions, enemies,
bosses, maps, forms, rewards/dealer/Workshop content or artwork.**

1. Run `Refresh-Inventory.ps1` after relevant Unity changes. It executes copied
   engine-free Core/Content catalogs, reads actual asset GUIDs and refreshes
   the Unity inventory. It rebuilds the legacy comparison from the preserved
   `legacy-raw.json`. Regeneration writes only inside this dashboard folder.
2. If the original legacy sources change, run
   `Refresh-Legacy.ps1 -LegacyRoot "<folder>"`. The folder must contain
   `Voidfall v1` through `Voidfall v5`. It executes original TypeScript
   definitions/Canvas paths in isolation, copies source evidence and
   deduplicates native renders by SHA-256.
3. Run `python -X utf8 validate.py` and
   `python -X utf8 validate-legacy.py`. Check affected views, filters, artwork
   and details in the browser. Include relevant generated updates in the
   project commit/push when the owner authorizes it.
4. Extend the extractor when new catalog types/renderers are added. Inspect
   both sources before changing mappings; keep uncertainty under Review.
   Update this document and the repository map when entry points change.

Regeneration needs Python, a .NET SDK (the installed Unity SDK is supported)
and Node.js 24+. Legacy rendering needs `@napi-rs/canvas`; run `npm ci` in this
folder if the bundled Codex Canvas runtime is unavailable. The viewer itself
needs no installations. Optional overrides: `VOIDFALL_DOTNET`,
`VOIDFALL_UNITY_ROOT`, `VOIDFALL_LEGACY_ROOT`, `VOIDFALL_CANVAS_MODULE_ROOT`.

## Evidence and limits

- **Missing:** no named current catalog entry after inspected stable-ID/role
  mappings. Related mechanics elsewhere are not ruled out.
- **Adapted:** an existing counterpart has a changed identity, definition,
  progression, effect or cue. Inspect both sources.
- **Present ID:** the stable ID exists; exact visual/behavior parity is not
  implied.
- **Needs review:** a possible relation or art/audio match needs comparison.
  Similar names or silhouettes do not prove migration.

The archive is the full union across five versions. Retired stored catalogs
and declared-but-unrolled elite modifiers are labeled. There are three named
legacy arenas plus the unnamed v1 backdrop; the latter is a descriptive tool
label, not an invented authored name. Original Canvas code supplies normal/hit
enemy sprites, form/ring sprites, projectiles, gems, dots, boss stills and arena
stills. No AI-generated artwork is used. Procedural audio is cataloged by
source, without fabricated recordings. Stills do not simulate full animation,
time-dependent effects or gameplay.

Archive content is evidence, not approval to migrate old designs or change
Zack's identity. Current enemy stats are authored/tier baselines, not final
director/time/arena/child-modified run values. Decks means implemented offer
pools, not an invented player-built deck system. Stored assets do not establish
runtime usage.

`raw-content.json` / `legacy-raw.json` hold executed catalogs;
`dist/content.json` / `dist/legacy-content.json` hold normalized viewer data.
Inventory and validation reports record coverage and asset provenance.
