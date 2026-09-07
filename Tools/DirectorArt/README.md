# Approved Destroyer art

`approved/destroyer-art.js` captures the final owner-approved September7 browser artwork: original hooked-wing Razor; unchanged22-tooth Maw; broad armored Maw-like Husk; four-jaw Grasp; creature-shaped bow mandibles for Spite. It deliberately excludes earlier rejected spiders, hand/round-eye brutes and literal crossbow.

Run `node Tools/DirectorArt/export-destroyers.cjs` from the project root using the existing bundled Canvas runtime. This writes eight256px pose frames per creature under Resources/VoidFall/Destroyers. Native SpriteImporter setup must use single sprites, centered pivot,1pixel/unit, no mipmaps and alpha transparency. Nominal body radius is64source pixels; runtime scale is enemyRadius/64.

The manifest records the exact authoring SHA256 and resource paths. All five share black interiors and white borders. Their simulation remains native Unity code; the browser simulation is not copied.
