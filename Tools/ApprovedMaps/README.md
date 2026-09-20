# Approved map artwork

Frozen owner-approved September 20 browser source for Null City, Hydra and Court.
The game runs native C# mechanics; it does not embed the browser simulation.

Run `npm install` in this directory, then `npm run export`. Alternatively set
`VOIDFALL_CANVAS_MODULE_ROOT` to an existing Node module directory containing
`@napi-rs/canvas` 0.1.100 and run `node Tools/ApprovedMaps/export.mjs` from the
repository root. No browser server is needed.

The exporter writes PNGs to `Assets/VoidFall/Resources/VoidFall/ApprovedMaps` and
records their physical dimensions/pixels-per-unit in `manifest.json`.
`VoidFall.EditorTools.ApprovedMapAssetImporter.Bake` imports them through Unity's
asset APIs. Commit the importer-generated metadata with the assets. Do not
manually rescale the PNGs, alter their GUIDs, or multiply their display scale by
the city layout expansion.

Small export adaptations suppress the sentinel's baked pupil (Unity draws its
tracking eye) and the hive's static browser HUD (Unity supplies live health bars).
The hive title stays in the approved six-unit lettering. All other silhouettes
come directly from the approved drawing functions. Source simulation files are
retained as behavioral references; superseded prototype helpers are not invoked
by the native game.
