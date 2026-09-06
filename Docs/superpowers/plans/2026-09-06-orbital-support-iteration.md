# Main-build card and orbital iteration

Owner authorized direct replacement of ../Builds/VoidFall.exe; no separate player build. Moving Clock hands stay as-is; face opacity becomes .35. Mines are unchanged.

1. Remove fortune/spatialAwareness from the live support pool. Scholar keeps four ranks and gains Fortune's drop bonus; Velocity Coils keeps three ranks and gains camera dezoom. Migrate legacy saved support entries by ID, taking the highest merged rank and clamping to the survivor's cap. Existing canonical IDs stay stable; run state is not resumable in this schema.
2. Apply speed-card multiplier divided by full weapon recovery scale to both blade and clock angular speed. Recovery already includes Overclock, Cycle Tuning, Workshop Arsenal, late cooling and Adrenal; never apply Overclock twice. Recalculate camera zoom centrally on every upgrade path.
3. Track ordinary-enemy projectile origin outside hashed shot structs. Insertion clears slot eligibility by default; runtime enemy AI explicitly marks ordinary source context, scoped by try/finally. Boss, elite, meteor and unknown sources stay non-blockable. Intercept along projectile movement before player impact, only when actual blade/clock-hand geometry is touched. Standard shot retirement handles counts and views.
4. Update weapon acquisition/rank/evolution text to say 'Can stop enemy projectiles' with boss/elite exclusion. Add catalogue, legacy-save, modifier-stacking, collision/provenance and slot-reuse regression tests.
5. Run EditMode, focused PlayMode and 32-seed repeatability. Baseline pinned legacy test already fails before edits (legacy17300903477073543990 vs expected14088908808337278323); do not repin to hide inherited drift. Record new intentional combat behavior and the unresolved pin gate.
6. Build and verify the current integrated Windows player. Use opt-in isolated-profile captures against this executable. Preserve designated archives and do not create another build folder.
