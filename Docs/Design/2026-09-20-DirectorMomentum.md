# Director I momentum — owner-approved implementation

The owner approved the pacing recommendations after two runs felt repetitive near
the end of Abyss and the start of the next void, and requested early Spiky/Shuriken.
This pass changes pacing rather than increasing the750-body pool or enemy health.

- Shuriken eligible at30 seconds; Spiky at50. Introductions still admit three,
  wait for a safe Flow opening and retain twelve seconds before ambient mixing.
  After safety deferrals, the earliest pending reveal goes first, so catalogue
  position cannot promote a later Gunner/Dasher introduction ahead of Spiky.
- Later voids keep2.5 seconds of arrival grace. Their nominal arrival floor starts
  at22/s and rises to33/s over45 local seconds. Existing stronger rates win.
  Damage relief, incidents, soft population targets and admission budgets still apply.
- First Abyss: elite escort window270–300, then rusher flank315–336. Both use the
  existing encounter clock with a directional HUD warning lasting two seconds.
  Escort requests one elite,24 chasers, six green swarmers. Flank requests ten
  rushers, two dashers and16 chasers from adjacent edges. Introduced-family gates
  and the two-standard-elite cap apply. Safety can defer or cancel a signature.
- Ordinary new beats stop with24 seconds remaining; ambient arrivals ease only
  for the final eight seconds. Major incidents retain their separate fifteen-second
  boss safety margin and full-duration eligibility check.
- Eligible arenas receive up to two incident opportunities, reset per visit.
  First due:85–120s in Abyss,60s thereafter. Retry8s, expire48s, cool down100s after
  resolution. Native meteor interference chooses24 warned familiar reinforcements
  through the encounter clock rather than adding another major hazard.

This does not claim that every opportunity must deploy: player damage, unsafe
openings and admission limits take priority. Expiry is logged and never queues a
backlog. Existing native-arena mechanics, map sizes, boss health normalization,
cards, loot and save formats are preserved.

## Validation

182 current automated checks passed:100 PlayMode and82 EditMode, including the
32-seed repeatability sweep. The pinned simulation hash remains unchanged.
Evidence: `Logs/director-v7-playmode.xml`, `director-v7-regression.xml`,
`director-v7-opening.xml`, `director-v7-editmode.xml`, `director-v7-introduction.xml` (later results supersede
earlier results for the same fixture).

The first two runs exposed stale pre-existing assertions for a0× pressure start.
The owner-approved1× start was already in the baseline; tests now distinguish
displayed1.26×/1.33× pressure from unchanged40/50 progression credit, and verify
the empty opening at1×. No pressure gameplay logic changed in this pass.

The first rendered native smoke stopped when normal focus-loss pause activated
while the owner used the main monitor. It confirmed1920×1080 on secondary
DISPLAY1 and Director I v7. A background batch run advanced78.7 combat seconds
and144 kills; its journal exposed catalogue-order starvation after a delayed
Spiky reveal. That ordering is now covered by the added regression test.
Batch screenshots are black and are not visual-fidelity evidence.

Final canonical Windows build succeeded at2026-09-20 00:42:27 +01:00:
`../Builds/VoidFall.exe`, GUID`9b14807879cf4592bb1acf6ef4fd262b`.
Log: `Logs/director-v7-final-build.log`.

The final isolated native batch/no-graphics check passed over90 wall seconds:
88.38 combat seconds,153 kills,326 surviving enemies. The real director admitted
three Shuriken at30.03s, three Spiky at54.06s, then three Exploders at66.08s.
Its exported context confirms Director I7 and the final build GUID. This validates
native gameplay progression; the late-Abyss and later-void cases were exercised
by PlayMode tests, not by a complete human-paced journey. Enjoyment and sustained
late-run FPS remain owner-playtest questions, not claims of this pacing pass.

Evidence is in the session visualization directory's `director-momentum/final/`:
`opening-report.json`, `native.log`, and `RunExports/`. All17 original Screenmanager
values were restored and the real profile's SHA256 remained unchanged. No test
player or Editor process remains open. Unrelated weapon-iteration notes were retained.
