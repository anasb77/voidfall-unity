# VoidFall change and validation playbook

## Before editing

1. Read `README.md`, `Docs/Architecture.md` and the relevant design file.
2. Record `git status --short`; preserve unrelated user changes.
3. Locate the authoritative owner and its existing tests.
4. For a bug, reproduce it and add a failing regression first.

## Determinism rules

- Gameplay RNG and FX RNG are independent.
- Do not move RNG calls across branches or change pool iteration order casually.
- Slot compaction/order semantics are gameplay behavior.
- View-only changes must not change the golden-master hash.
- Intentional gameplay changes may re-pin the hash only after the 32-seed sweep
  proves deterministic reset and replay behavior.

Current canonical `productionMax` hash:

```text
12947047772295568886
```

## Verification order

```powershell
dotnet build VoidFall.Runtime.csproj -t:Rebuild

& 'C:\Program Files\Unity\Hub\Editor\6000.5.7f1\Editor\Unity.exe' `
  -batchmode -nographics -projectPath $PWD `
  -runTests -testPlatform EditMode -testResults Logs/editmode.xml `
  -logFile Logs/editmode.log

& 'C:\Program Files\Unity\Hub\Editor\6000.5.7f1\Editor\Unity.exe' `
  -batchmode -nographics -projectPath $PWD `
  -runTests -testPlatform PlayMode -testResults Logs/playmode.xml `
  -logFile Logs/playmode.log
```

Do not append `-quit` to Unity test commands; it can exit before the runner
starts. Expected current first-party totals: 168 EditMode and 5 PlayMode.

For release-sensitive work, also build and launch the Windows player. Visual
work requires screenshots or manual inspection; compilation and the simulation
hash cannot detect layering, animation or readability failures.

## Runtime extraction rules

- Move one responsibility boundary at a time.
- Keep wrapper call order unchanged until tests prove parity.
- Bind delegate hooks once; cross-check every declared hook has an assignment.
- Keep UnityEngine-facing view work outside engine-free state owners.
- Do not introduce a second authoritative state copy.
- Delete promoted runtime code only after the new owner is wired and verified.

## Asset rules

- Preserve `.meta` files and GUIDs when moving assets.
- Generate large arena textures only in Editor tooling.
- Keep player textures non-readable unless a documented runtime API requires it.
- Addressables handles must have one owner and a balanced release path.
- Never commit `Library`, `Logs`, `TestResults`, `.vs`, or build output.

## Commit discipline

- Stage explicit paths in a dirty worktree.
- Separate intentional behavior changes from mechanical refactors.
- Explain golden-master re-pins in the commit body.
- Never use destructive Git cleanup to remove another contributor's work.
