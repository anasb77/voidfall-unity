# Notifications, raid count and attack previews

Owner-approved changes: bundled Chakra Petch for gameplay/menu notifications
including Overclock; pressure text 20% larger; small `[TAB] To open the map`
hint between HP and objective; eight Destroyer raiders; clear Raid/Eclipse
announcements with distinct sounds.

Major incidents now queue five-second priority notices. Ordinary score/reward
traffic cannot evict them, and hidden overlay time does not consume their
lifetime. Raid, Eclipse and Black Hole have separate cached procedural cues
through the existing effects volume/mute and audio lifecycle.

The eight-member raid retains all five roles, adding one Maw, Razor and Spite.
Hit history is indexed by raid member rather than role, so two charging Maw
cannot overwrite each other's once-per-dash hit bookkeeping. Existing attack
attention limits, admission checks, health and damage remain authoritative.
Incident policy 3 exports announcement dispatch and actual raid admission;
see `Docs/RunExports.md`.

## Incorrect long attack line

Native captures reproduced an extra yellow line on shared Gunners, Dashers,
Twin Gunners and Mortars. `TryRenderApprovedEnemy` invoked map-specific attack
overlays before checking whether it owned the enemy's presentation. Shared
enemies have an unset map target `(0,0)`, so State 1 drew a line to world origin,
in addition to the actual attack telegraph. Length grew with distance from
origin. The fix gates map attack overlays by their owning enemy families and
combat phase; shared protection overlays and actual attack previews remain.

The opt-in canonical-player capture uses `-vfrestoration-check=<directory>`
with `-vfnotice-check=1`; it inherits isolated profile/export ownership from
`LegacyRestorationProbe`. No separate game executable or release path is added.

Regression coverage lives in `NotificationPresentationTests` and
`RunExportIntegrationTests`: notice survival and typography, distinct audible
clip data, eight-member composition, independent duplicate-role damage,
shared-enemy warning ownership, preserved map impact previews, and exported
JSON/JSONL outcomes.

## Verification

66 distinct PlayMode checks passed across the focused suites, including the
unchanged golden-master hashes and 32-seed deterministic sweep. The eight-raider
late-loadout fixture defeated the raid at 9.05 seconds; one Maw traversed 186
units during its charge. Freeze/thaw and finite raid withdrawal checks pass.

Canonical Windows build GUID `32676184283c49388144867249a9aab7`, built September20
at15:42 local time. Real 1920x1080 DX11 captures on the secondary-monitor launch
confirm readable notices/HUD spacing and removal of the yellow origin lines
while actual dash/volley/blast previews remain. Evidence is under
`C:/Users/AB/.codex/visualizations/2026/09/19/01a0b9b9-c6e3-7cc3-9856-b08efaa53c68/notification-preview/`.
The real save hash was unchanged and all17 saved display-preference values
were restored after isolated captures.
