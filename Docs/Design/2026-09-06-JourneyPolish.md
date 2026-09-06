# Approved journey polish

Owner approved the browser journey study, then requested these refinements for Unity.

- Fifteen seconds of active escape time. Stop hostile behavior immediately, but retire remaining enemies through staggered visible explosions. Keep player movement and pickup collection available.
- Recover all earned XP and Parts, including off-screen drops, through a final sweep before departure. Resolve resulting level choices before travel/save. Never grant the same pickup twice.
- Preserve collected Overclock charge through escape, junction and travel; resume its timer when incoming combat resumes.
- Show only `Escaping...` with animated dots. Increase screen shake over the 15 seconds. Choose among three cosmetic patterns and avoid repeating the previous pattern. Respect shake/reduced-motion settings and modal pauses.
- Preserve roulette mechanics and grant each reward once. Remove the additional prize-confirmation popup so completing roulette resumes escape automatically.
- Physical portals commit choices. Each uses its destination's own color and only its Void name; approach feedback highlights the chosen/planned branch.
- Minimal map: centered `VOID MAP`, arena thumbnails and names, `YOU ARE HERE`. Remove progress counts, descriptions, state labels, legend and instructional paragraphs. Connections distinguish travelled/planned/available paths. Tab/Escape close it and retain pause ownership.
- Use seeded finite branches with reconnecting routes, six arenas per path, Abyss first, supported arena content only and no repeated arena on a path. Route generation must own its random stream. Node identity may differ from arena identity for mutually exclusive repeated nodes; preserve existing arena IDs and saves.
- Correct actual destination resolution, including Eon Sea and Crascendo; prove arrival installs the requested arena/objective rather than only proving that travel starts.

## Ownership and validation

Extend the existing Journey/Rift owner, normal pickup grant path and runtime-authored uGUI. Bake low-resolution preview textures from existing arena art without loading all full arena packages during play. Keep Core and Content free of UnityEngine. Preserve original simulation ordering and the unchanged golden master for combat.

Work starts from isolated snapshot `e649703`, including the user's in-progress integration. Transfer only this task's patch back into the active project. Validate focused rule and runtime tests, golden-master sweep, Windows player and relevant visual captures with isolated profiles. The user has authorized implementation; further design approval is not needed.
