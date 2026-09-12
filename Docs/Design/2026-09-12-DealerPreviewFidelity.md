# Dealer preview fidelity correction

The owner rejected the first native room/portrait appearance after playing the
unified build. The approved reference remains `Prototypes/dealer-travel` and
its shared `dealer-shop/face.js`; a functional integration was insufficient
evidence of visual fidelity.

Confirmed differences in the first native captures:

- The platform used a different outline, flat bright vertex colors, missing
  grid/perimeter/depth details, and small portals at different positions.
- General UI text scaling and font line metrics changed the face proportions.
  A two-layer portrait lost the reference's brow/cheek/jaw/neck tones and hair
  strand motion. Downsampling a large font atlas also thinned the hair glyphs.
- Linear-space blending amplified the faint browser glows. CPU conversion of
  dark UI vertex colors lost precision. The room face and labels could show
  through the shop overlay.

The corrected presentation keeps the reference's 1200x760 stage, floor vectors,
subtle grid, broken edges, shadows, orbiting mist and floating debris. Portals
retain real route destinations and destination colors. A screen-space stage
isolates the composition from combat camera zoom and post effects. Native
movement/portal interaction positions use the same stage coordinates.

The ASCII renderer retains the authored characters, 1.12 line spacing,
Consolas proportions, small-size font hinting, feature colors and per-strand
hair offsets. Breathing, yaw/gaze, purchase grin, four variants and upper/lower
encounters remain. The room portrait/chrome hides while the shop is open.

Verification: 34/34 dealer, journey and Workshop PlayMode tests passed in
`Logs/dealer-fidelity-final-playmode.xml`. Native rendered checks in
`Logs/DealerFidelity/Player03` passed purchases, saving and assembly; inspected
upper/lower room, shop and grin screenshots against browser captures in
`Logs/DealerFidelity`. The final label visibility adjustment is visual only.
This is not a claim of pixel-identical cross-engine font antialiasing.

Final player: source `07fdea8`, build GUID
`0c7410ac03ff432ba6ea16eeff866eed`. The final rendered check passed in
`Logs/DealerFidelity/FinalPlayer`; inspected the purchased shop with destination
labels hidden. Promotion hashes are recorded in
`Logs/DealerFidelity/promoted-manifest.json`.

Canonical build remains `../Builds/VoidFall.exe`; no extra permanent build entry
point is introduced. Old player payload is retained in
`../Builds/Archive/2026-09-12-before-dealer-fidelity/` after validation.
