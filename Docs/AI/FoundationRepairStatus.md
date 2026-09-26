# VoidFall foundation repair status

Updated September 26, 2026.

**Confirmed defect repairs are implemented. The full Unity suites and Windows
build pass. Both complete native journey branches and the final smoke pass. Visible-menu and
performance validation await permission to show the test player. The final
hidden-window map recapture failed its visibility guard; promotion is on hold.**

`AGENTS.md` and `Docs/REPO_MAP.md` describe standing development contracts and
current subsystem ownership. This file records actual implementation and evidence;
those contracts alone do not certify a fix. The owner's other agent is read-only.
Map/asset prototypes remain separate from production integration and approval.

## Baseline and scope

The source baseline is `afd885ef06e2e0acee41b5981db54cb2e1757b27` plus the owner's
existing uncommitted changes. Do not reset to that commit. Pre-repair hashes,
source copies and a binary working-tree patch are preserved locally under
`Logs/FoundationRepairs-2026-09-26/` (`baseline-files.json`, `before/`,
`baseline.patch`). No commit, push or deployment has been requested.

The original evidence is in `Docs/AI/DeepAudit-2026-09-26.md` (F01-F14) and
`Docs/AI/MuseAuditVerification-2026-09-26.md` (B001-B040). The latter separates
verified defects from rejected assertions and untested proposals. Rejected
claims are not a repair checklist. Existing design choices are not silently
retuned to make tests or performance numbers improve.

## Implemented repairs

| Area / evidence | Change and current verification |
|---|---|
| Profile transactions - F01, F03, B003, B024 | Workshop purchases/refunds/forms and terminal run completion stage a complete candidate, persist once, then publish. Failures leave live progression and the last good disk state intact. New transaction regressions and full suites pass. |
| Crossing flow - F02 | Single-exit routes enter the dealer too: five opportunities across six visits. Route selection precedes objective initialization. All five edges pass runtime tests; both complete native branches passed, each with five crossings and exactly one saved run. |
| Hostile pools - B001 | Central retirement clears admission counters, ordering and provenance. Bulk clear restores every slot to its default state, including inactive slots, preserving repeat-run golden behavior. Regression and original golden-master tests pass. |
| Spawn admission - F04 | Null City offspring respect native 50/90 caps, defer without losing birth/reward identity, and do not cull survivors on a cap decrease. Regression tests pass. |
| Save compatibility - B011, B026 | Full Unity transfer retains forms, unlocks, cleared voids and all settings; legacy omissions get defaults. Future-version primary/backup data is preserved and automatic writes/new runs are blocked until explicit compatible replacement. Storage tests pass. |
| CPU hot paths - F06, B025 | Stable callbacks bind once. Crascendo collision queries use conservative live-body padding, expanding immediately on growth, instead of always scanning the maximum neighborhood. Coverage, callback reuse, unchanged golden hashes and 32-seed sweep pass; native timing pending. |
| Asset/material ownership - F07, B017 | Approved map sprites now belong to arena Addressables packages. Consumers and caches detach before release; pending views retry after loading. Grid material clones have an explicit destruction owner. Ownership/residency tests pass. |
| Texture footprint - F08 | 145 approved map textures use Standalone BC7, retaining source pixels, dimensions, mipmaps and GUIDs. Arena recipe estimates include unique texture dependencies and actual block format. Windows build shrank from 1,513,503,700 to 903,358,083 reported bytes (about 40%). Native art checks pass; final HUD recapture pending. |
| Telemetry/startup - F12, F14, B018 | Startup phase logging ends with the menu warmup window or run start. History schema 5 omits irrelevant null DTO trees while retaining factual fields and loss counters. Summary schema 4 remains unchanged. Serializer/round-trip tests pass. |
| Input/menu - F10, F13, B024 | Views restore valid focus, quit confirmation owns input, gamepad pause/map/cancel follows modal priority, and selection exposes Workshop details. Workshop footer and Records metric spacing are corrected. Full graphics-enabled PlayMode suite passes; native menu captures pending. |
| HUD/camera - F09, B020 | Clock, score and map hint have dark contrast backings. Native Court inspection caught and corrected score-panel draw order. Runtime camera now allows the pipeline's authored multisampling. Eight focused graphics-enabled follow-up tests pass; final native HUD recapture pending. |
| Tests/probes/CI - F11, B030 | Stale assertions now match approved encounter counts/timing and dealer flow. Benchmark rewards resolve through real claim paths; journeys wait for Hydra II and acknowledge saved results. CI requires both suites, canonical Windows build and advancing player smoke. Local suites/build/smoke pass; hosted CI requires external configuration. |
| Recovery - B027 | Compatibility export replacement is atomic; recovery write failures are logged and tested. Explicit backup-restore UI/retention policy remains a separate hardening task. |
| Bake freshness - B029 | Prepared content gates validate catalog ownership, manifest coverage, dimensions and import format. Comprehensive authoring-source-to-bake fingerprints remain a separate hardening task; the submitted audit did not establish stale approved art. |

The profile bridge now accepts detached candidates through `TryCommitProfile`.
Callers refetch profile/settings after success rather than retaining aliases into
a replaced profile. Dealer purchases still spend run Scraps; Workshop still
spends saved Scraps. Failed terminal commits retain earned route facts for retry.

The 145 approved PNGs moved from `Resources/VoidFall/ApprovedMaps` to
`Generated/ApprovedMaps` using Unity asset moves. Three `ApprovedMapVisualAsset`
catalogues attach them to the Hydra, Court and City plates. Menu residency still
intentionally covers all eight arenas; gameplay residency remains the current
arena plus at most two exits. `approved-map-final-preservation.json` verifies
all 145 original PNG hashes, dimensions and GUIDs. Compression changes imports,
not source artwork. The internal dashboard was refreshed and both inventory
validators passed (900 live checks and 4,474 legacy checks).

## Validation evidence

Local outputs are under `Logs/FoundationRepairs-2026-09-26/` and are not intended
for source control. Tests use isolated saves. The real player profile and
`../Builds/RunExports/` are protected.

| Check | Result |
|---|---|
| `full-editmode.xml` | 679/679 passed |
| `asset-editmode.xml` after BC7 imports | 27/27 passed |
| `full-playmode-final.xml`, D3D11 graphics enabled | 441/441 passed; zero failures or skips |
| Original simulation golden master | Legacy 4093819818710876312; full 1912782885852962356; unchanged |
| Deterministic sweep | All 32 seeds passed |
| `windows-build-final.log`, canonical BuildScript with staging output | Succeeded, 903,358,083 reported bytes; GUID `5c96052cb71841d39dc22692e4f4eb35` |
| `ui-native-followup.xml` after score draw-order correction | 8/8 passed; graphics enabled |
| `Native-final-hidden/smoke/`, production `Smoke-Player.ps1` | Passed on final build; 333 simulation ticks and 5.233 combat seconds advanced |
| `final-journey-results.json` | Both branches passed: six visits, five dealer crossings, five committed destinations, one saved run each; right branch completed Hydra II |
| `Native/maps/mapcheck.json`, first build | Passed; Court/City/Hydra diagnostic poses, including same-visit Hydra I/II transition |
| `Native-final-hidden/maps/mapcheck.json`, final HUD follow-up | Failed: first City pose reported no visible gameplay surface. No screenshot produced. Visible-player reproduction required before accepting final visual validation |
| Native menu capture first attempt | 720p home rendered; several hidden-window captures were black and are rejected as visual evidence |
| Hidden D3D11 batch capture control | Also returned a black menu frame; rejected as visual evidence |
| Visible menu/FPS checks | Awaiting explicit permission to show the player; no accepted post-repair FPS result yet |

Earlier failed intermediate runs remain in the evidence directory. The initial
440-case PlayMode pass exposed stale fixtures, a test overload-selection error,
and a real inactive-slot bulk-clear regression. These were repaired before the
441/441 final run; golden constants were not repinned. The native score layering
follow-up adds a draw-order assertion and passed. The final hidden recapture failed
its visibility guard. This failure is not counted as a pass or assumed to prove an
asset regression; a visible run must resolve the distinction.

The serializer microbenchmark wrote 5,000 representative events: 21,130,000 bytes
before versus 2,700,000 after (4,226 versus 540 bytes/event; about 87% smaller).
Measured serialization speedups varied by run. This measures serialization only,
not whole-game FPS. Native output loss and frame timing must be measured separately.

Unity resolved the already-present `com.unity.pipeline` manifest dependency into
`packages-lock.json` during validation, including its Newtonsoft dependency. No
new manifest dependency was introduced by these repairs. The build logs retain
an optional Pipeline runtime-config warning and an Addressables layout profile
warning; successful package loading is checked separately in the player. Native
shutdown logs also retain the ComputeBuffer-disposal warning already present in
the original audit players; no new runtime exception was logged in either journey.

## Native performance and delivery

Baseline PC: Intel i7-7700HQ, NVIDIA GTX 1060 6 GB, approximately 16 GB RAM,
1920x1080 at 60 Hz. The working target is 60 FPS / 16.7 ms, not a guarantee.

Original advancing stress evidence: approximately 45.08 FPS holding 750 actors
and 58.51 FPS in the lighter Director I scenario. Those are different workloads.
The final comparison repeats the baseline player and repaired player with the
same scenario, seed, profile, resolution and time window, with Editor/build work
stopped. An MSAA-off diagnostic isolates comparable camera settings; the repaired
High preset is also measured with the authored multisampling enabled. Results
must include actual combat advancement, density, p95/p99 and spikes. Unsupported
GPU/GC counters remain unavailable, not zero cost.

The generated Addressables linker file includes the new catalogue types; its
original metadata GUID has been restored and verified against the baseline hash.
The final file inventory found no unexpected missing files or artwork changes.

The replacement is currently staged at `../Builds/.foundation-validation/`.
The canonical `../Builds/VoidFall.exe` has not yet been promoted to this repair
build. Promotion is on hold for the visible menu/HUD and FPS checks. Local launch tool
instructions require explicit permission to show an automated player; that
question remains pending. Promotion preserves unrelated builds,
archives and run exports.

## Remaining limits and follow-up

- Hosted CI has not run. Unity credentials, an available matching GameCI image
  and repository branch rules must be configured externally. A workflow file
  alone cannot establish those protections.
- Physical audio listening, output-device switching and the previously reported
  fullscreen/focus hang are not certified fixed. Preserve reproduction evidence
  before changing those paths; no captured hang stack established its cause.
- Explicit recovery UI/backup retention and complete bake fingerprints remain
  hardening opportunities, not confirmed data-loss/stale-art defects.
- Density, boss health, camera shake, bloom and six-minute survival duration are
  design/balance decisions. The repair pass preserves their approved rules.
- Earlier full audit-player cleanup was rejected twice by automatic approval
  review with only `blocked by policy`. Those retained evidence players have
  not been deleted or moved to bypass the rejection.

The expected functional benefit is reliable progression, consistent pool and
spawn limits, complete dealer crossings and usable modal navigation. Build and
history size reductions are already measured. Whole-game speed and frame
consistency are only claimed once the native comparisons are recorded here.
