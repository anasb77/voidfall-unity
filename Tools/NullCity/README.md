# Null City authored art

`art.js` and `simulation.js` are the approved Null City V canvas art and roster
sources copied from `../Prototypes/null-city`. The only authoring adaptation in
`art.js` is an optional final `live` argument that switches the core neon,
transit, traffic, hangar and LCD groups independently. With no options it still
draws the approved browser frame unchanged.

Run the deterministic offline export from the Unity repository root:

```powershell
node Tools/NullCity/export-null-city.cjs
node Tools/NullCity/verify-null-city.cjs
```

The exporter first tries `@napi-rs/canvas`, then `NULL_CITY_CANVAS_MODULE`, then
the bundled Codex runtime under `%USERPROFILE%/.cache/codex-runtimes`. It writes
the authored sources under `Assets/VoidFall/Art/NullCity` without using Unity or
any network service.

The two plate layers preserve the approved 1600x900 composition. The base is
3840x2160. The transparent core-neon detail layer is 2560x1440 and intentionally
omits moving transit, road traffic, hangar doors and LCD text. Those elements are
separate props, so runtime motion never sits over a frozen duplicate.

Every prop is a transparent live-only overlay. Hangar structure and the blank LCD
housing remain solely in the base plate; the open hangar overlay contains only the
intentional dark bay, retracting shutters, deployment light and caption. This keeps
lockdown plate dimming continuous without bright crop rectangles.

Unit and prop PNGs render at four pixels per approved authoring unit. The Unity
baker imports them at 4 pixels per unit, so `Sprite.bounds.size` and
`NullCityVisualAsset.UnitWorldSize` report these authoring dimensions directly:

| Unit | Bounds |
|---|---:|
| null-patrol | 64 x 64 |
| null-enforcer | 80 x 80 |
| null-sentinel | 96 x 72 |
| null-crawler | 80 x 80 |
| null-volatile | 112 x 112 |
| null-gunship | 136 x 120 |
| null-mech | 128 x 128 |
| null-broodmother | 200 x 184 |
| null-light-gunship | 112 x 96 |
| null-interceptor | 80 x 80 |
| null-marshal | 104 x 104 |
| null-suppressor | 96 x 88 |
| null-motherload | 440 x 320 |

The plate coordinate origin remains the canvas center. Prop crop centers are:

| Prop | Center | Bounds |
|---|---:|---:|
| Transit | (745, 235) | 190 x 80 |
| HangarClosed / HangarOpen | (805, 800) | 410 x 180 |
| Traffic | moving road placement | 58 x 48 |
| TrafficLockdown | moving road placement | 58 x 48 |
| LcdSurveillance / LcdLockdown | (1172.5, 107.5) | 315 x 85 |

Every unit has four looping motion frames and one hit frame. Broodmother jaw
motion is part of those frames. Motherload also has four exposed, four active
tractor and four tractor-warning frames. Marshal has four braced-shield frames.
