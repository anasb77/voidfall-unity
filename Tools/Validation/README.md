# Foundation validation

The CI workflow runs EditMode and PlayMode independently, then uses
`VoidFall.EditorTools.BuildScript.BuildWindows` and runs the resulting Windows
player with `Smoke-Player.ps1`. Require the aggregate `foundation-validation`
check in repository branch rules. A missing Unity license fails visibly;
documentation checks alone cannot produce a passing aggregate result.

Repository setup still requires Unity credentials and a GameCI image matching
`ProjectSettings/ProjectVersion.txt`. The Windows player uses the project's
Mono backend and is cross-built on Linux, then executed on a Windows runner.
Forks without secrets need a trusted maintainer run; do not expose credentials
to untrusted pull-request code. Repository secrets and branch rules are external
configuration, not established by committing this workflow.

The builder accepts `-vfbuildoutput` and `-vfsource` for container execution;
local `VOIDFALL_BUILD_OUTPUT` / `VOIDFALL_SOURCE_REVISION` overrides remain valid.
These arguments are passed explicitly because GameCI does not forward arbitrary
workflow environment variables into Unity. See the [GameCI builder options](https://game.ci/docs/github/builder/)
and [test-runner options](https://game.ci/docs/github/test-runner/).

Run a local smoke against a staged player:

```powershell
./Tools/Validation/Smoke-Player.ps1 -PlayerPath ../Builds/VoidFall.exe -OutputDirectory ./Logs/windows-smoke
```

Each smoke uses a unique profile and report, checks process exit and runtime
errors, and requires advancing combat with attacking activity. It runs with no
graphics device and does not certify visual fidelity, sound or FPS. Native
captures and matched performance measurements remain separate release evidence.
