# Windows focus freeze: investigation and mitigation

Owner report: the Windows player became unresponsive while moving to a second
monitor during a long Null City run. Closing the window failed; Task Manager
termination was required. Court/City/Hydra repair and further Director I tuning
are explicitly owned by the owner's separate task.

## Evidence and limits

Original build GUID: `17127f1e74df473daccec32f1c0666fd`, Unity6000.5.7f1.
The retained player log confirms Direct3D12 on an RTX3080 Laptop GPU. The
owner's saved display preference is ExclusiveFullscreen (0). The recovered
run journal ends at29:13 with seven enemies; last frame sample is16.67ms.
There is no crash stack or surviving focus event at the terminal boundary.
Older local crash dumps exist, but none corresponds to this hang.

Focus handler review found no synchronous file flush: it signals a bounded
writer queue. Fixed-step catchup is capped at three steps. The handler pauses
gameplay and calls native audio suspension. An inactive still frame is expected;
this does not explain an unresponsive window after returning to it.

Unity issue UUM-148214 describes a DX12 fullscreen focus-switch failure near
this engine version (6000.5.6f1 among affected versions), with no DX11 reproduction:
https://issuetracker.unity.com/issues/23712/player-crash-on-unitymain-when-rapidly-switching-app-focus-in-fullscreen-window-mode-while-using-directx12
Unity closed it as a third-party issue. The older UUM-134743 is already fixed
in this release line and is not evidence that our exact hang is that bug.

## Controlled local checks

The existing player was launched twice with copied, isolated profiles and
explicit log/export paths under `Logs/FocusInvestigation`: once forced DX12,
once forced DX11. Real owner profile contents were read, not modified. Existing
visual-capture isolation sets runInBackground=true; that differs from ordinary
shipping behavior and limits this reproduction.

Both players returned from application focus changes and closed with Alt+F4.
DX12 was also toggled windowed/fullscreen with Alt+Enter. Both hidden launches
initially failed to obtain exclusive fullscreen and reverted to borderless;
this startup condition alone is not evidence of a DX12-only bug. DX12 also
logged broken swapchain frame statistics; DX11 did not. No hard hang reproduced.
Pure cursor-only monitor crossing was not reproduced by the available window
automation API. The focus target was Notepad; Codex UI was not automated.

To remove the capture-mode background-policy difference from final testing,
the runtime now accepts `-vfprofile=<absolute file>` before its first save
load. This isolates storage without enabling capture mode or background play.
An empty override fails before any fallback to the real profile. Such exports
are tagged diagnostic. Existing capture flags retain their behavior.

An official Microsoft-signed ProcDump copy monitored the isolated DX12 PID for
unresponsive windows and exited when the process closed. No dump triggered.
No monitor remains running. Original logs remain in the separate owner-evidence
folder. ProcDump documentation: https://learn.microsoft.com/en-us/sysinternals/downloads/procdump

## Shipped mitigation and future evidence

`BuildScript.BuildWindows` explicitly configures Windows APIs in order DX11,
DX12, with automatic selection disabled. DX11 is the default; DX12 remains
available for explicit diagnostic comparison. Saved display preferences and
normal focus-pause semantics are retained. This removes the default DX12
fullscreen combination; it is a workaround, not a proven diagnosis of the
original forced-kill hang.

Run context records graphics API/device version, actual fullscreen mode,
window dimensions and background setting. Existing `application_focus` events
now include actual display state; `application_focus_completed` records handler
completion and elapsed time. Duplicate native callbacks remain idempotent.
Returning focus still requires explicit Resume. Completion narrows a future
stall to outside that callback; missing data alone does not establish causality.

Regression test first failed because no completion records existed
(`Logs/focus-history-red.xml`). Fresh final validation and player evidence are
recorded after the integrated gates. Original hang resolution still needs a
reproduction with thread stacks or owner confirmation over subsequent runs.

Final source gates:575 EditMode tests passed and276 PlayMode tests passed
(one graphics skip) in `Logs/freeze-balance-final-*.xml`. Focus-entry/completion
idempotence, real exported display context and profile-only isolation tests
pass. These tests validate our managed lifecycle and evidence path; they do
not prove an unreproduced native hang cannot recur.

Windows build completed at2026-09-08 21:00:32+01:00, GUID
`aa3ed9c818b748ae95d3398eb611fc9c`. Actual player export confirms Direct3D11.
The 1080p stress probe held750 actors for1146 measured/refill steps with zero
capacity failures and no export drops/errors. Median frame18.03ms,p9521.51ms;
timing is specific to the owner's RTX3080 Laptop environment. Report:
`Logs/FreezeBalance750/report.json`. This is a load check, not an owner run.

Final profile-only native check confirmed actual DX11, ExclusiveFullScreen and
runInBackground=false in player journal `c54f6a291db64dafbd6abae0142d5a50` under
`Logs/FocusInvestigation/FinalRunExports`. Focus loss completed in0.84ms. Further
restore attempts were interrupted by external input/focus changes; no successful
final restore is claimed. The window continued responding. The isolated test
was terminated and its dump monitor cancelled; no hang dump triggered.
The real profile's SHA256 matches its pre-test value.

The final built player was also inspected in warning and active Black Hole
states using the isolated arsenal/incident path. The player visibly moved
toward the locked center; the radius boundary and active disk rendered on DX11.
That visual test closed via Alt+F4. Source logs are in
`Logs/IncidentPlayerVisual`. The native shutdown retains the pre-existing
ComputeBuffer disposal warning. All task-owned players and dump monitors ended.
