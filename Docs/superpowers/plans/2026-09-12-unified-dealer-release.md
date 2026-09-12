# Unified dealer release implementation plan

**Goal:** Consolidate current Unity work, integrate the agreed dealer/legendary
slice, and deliver one verified Windows player.

**Architecture:** Keep Core/Content engine-free. Runtime gameplay partials own
shop, crossing and manual combat state. uGUI views render callbacks and the
shared ASCII art. Extend SaveData compatibly for permanent fragment ownership.

**Tech stack:** Unity 6000.5.7f1, C#, uGUI, Input System, URP, Addressables.
**Spec:** `Docs/Design/2026-09-12-DealerIntegration.md`.

## Constraints

Preserve unrelated user changes and source recovery commits. Use Scraps as the
display name; retain existing serialized `parts` keys. Only accepted Sound
Blade and Charged Rifle are production weapons. Roulette fragments are deferred.

## 1. Consolidate sources

- [x] Snapshot main, director and journey work before integration.
- [x] Resolve director/main overlaps, preserving faction and killer metadata.
- [x] Compile, reconcile journey-only changes, and compile again.
- [x] Record source provenance and commit the coherent integration baseline.

## 2. Rules and persistence

- [x] Add `Content/DealerRules.cs` and `Content/LegendaryRules.cs` with
  engine-free offers, costs, eligibility, fragment masks and weapon timing.
- [x] Add EditMode tests for one purchase, no duplicate pieces, unaffordable
  transactions, three-piece assembly, timings and shield/damage boundaries.
- [x] Extend `Persistence/SaveStore.cs` with sanitized fragment ownership;
  retain old defaults and existing read/write-failure safeguards.

## 3. Native crossing and shop

- [x] Add `UI/Views/DealerView.cs` and portrait rendering, using the browser
  reference and LevelUpView's typography, cards and accent treatment.
- [x] Add runtime `.Dealer.cs`; connect Journey's single/multiple-exit flow,
  E/controller interaction, fixed stock, currency debit, effects, saving,
  top/bottom placement, modal pause ownership and teardown.
- [x] Include puzzle art in Resources via a reproducible export path.

## 4. Manual legendaries

- [x] Add runtime `.Legendaries.cs` using existing fixed-step simulation,
  enemy/boss damage methods and a separate manual weapon slot.
- [x] Render the attached waveform and rotating charged rifle, preserving
  attack timings, mouse/controller cancellation, rank tuning and audio-only
  waveform reactivity. Add gameplay integration tests.

## 5. Validate and deliver

- [x] Run merged EditMode/PlayMode checks and the golden-master sweep.
- [x] Build an isolated Windows validation player and run capture probes.
- [x] Replace the canonical player only after successful validation.
- [x] Update REPO_MAP, build provenance and the canonical launch instructions.
- [x] Integrate the verified release branch back into main locally.
