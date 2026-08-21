# VoidFall Refactoring Playbook

How to keep carving `VoidFallGameRuntime` apart without breaking the game.
Everything here was learned by doing it; follow it and each step is provably
safe. Written 2026-08-21 after the FxSim/GameSim extractions.

## The golden rule

**Never change behavior and structure in the same commit.**

Every structural change must end with the PlayMode golden-master test passing
against the *same pinned hash*. If the hash changes, either you made a mistake
or you changed gameplay — both stop the line. Intentional behavior changes
regenerate the hash in a separate, clearly described commit.

## Verification loop (run after every step)

```text
1. dotnet build VoidFall.Runtime.csproj -t:Rebuild     # expect 0 errors, 14 warnings
2. Unity batchmode EditMode suite                      # expect 57/57
3. Unity batchmode PlayMode SimulationGoldenMasterTests# expect pinned hash
```

Unity CLI pattern:

```text
& "C:\Program Files\Unity\Hub\Editor\6000.5.7f1\Editor\Unity.exe" -batchmode
  -projectPath <repo> -runTests -testPlatform <EditMode|PlayMode>
  -testFilter "VoidFall.Tests.PlayMode.SimulationGoldenMasterTests"
  -testResults <out.xml> -logFile <out.log>
```

## The extraction recipe (per family)

FxSim and GameSim followed this exact sequence:

### v0 — state ownership
1. Create `<Name>Sim.cs`: plain internal sealed class, public readonly arrays,
   order bookkeeping, RNG stream. Constructor takes capacities + seed so field
   initializers work **without Awake** (reflection-built test fixtures depend
   on this).
2. Promote the family's nested structs/enums from `private` inside the runtime
   to `internal` namespace-level types (a sibling class cannot even *name* a
   private nested type). Keep them pure data.
3. Delete old field declarations on the runtime; add one `_nameSim` field with
   an initializer.
4. Mechanically rewrite references: whole-word regex per identifier
   (`\b_oldName\b` → `_nameSim.NewName`). Word boundaries are what protect you
   from prefix collisions (`_meteorShards` vs `_meteorShardViews`).
5. Compile, fix, verify, commit.

### v1 — pure logic inward
Move update loops / scans / bookkeeping whose bodies touch only owned state.
Methods that also drive views split in two:
- state half moves into the Sim class, returning expired/changed slots via a
  caller-provided scratch buffer (no allocation);
- view half remains as a thin runtime wrapper (same method name → zero call-site
  churn) that hides/syncs views for those slots.

View-operation ordering relative to pure-state ops is observable to nobody;
the golden master proves it.

### v2+ — spawn/emission logic
Spawn methods whose guards read presentation settings (reduced motion, quality
scale) keep guards + view creation on the runtime and delegate insertion to a
`TryXxx(...)` returning success/slot. Anything whose essence is a Unity object
(`BurstFx` emitting through a ParticleSystem) stays on the runtime by design.

## Pitfalls actually hit — do not relearn them

- **The local csproj lists files explicitly.** New `.cs` files must be added to
  `VoidFall.Runtime.csproj` (`<Compile Include=...>`) or `dotnet build` won't
  see them. The file is generated/gitignored; Unity regenerates it eventually.
- **PowerShell 5.1 mangles UTF-8** with `Get-Content`. Always use
  `[System.IO.File]::ReadAllText/ReadAllLines` and write with explicit
  `UTF8Encoding(hasBom)` matching the source file.
- **Trivia-subtraction span math overlaps.** To cut members out of a file,
  tile spans as `[previous member FullSpan.End .. current FullSpan.End]`.
  Never compute start from leading trivia length.
- **Never search for `"        }"` to find a method's end**: an 8-space close
  matches *inside* a 12-space-indented line and you will cut mid-structure.
  Use Roslyn, or match the exact full line.
- **Validate every rewritten file parses before writing**
  (`CSharpSyntaxTree.ParseText(...).GetDiagnostics()`); abort on error. This
  turned two bad cuts into no-ops.
- **Hash order is part of the hash.** When updating the golden-master test's
  field paths, preserve the exact mixing sequence of the original.
- **Test assemblies break player builds.** The PlayMode asmdef carries
  `"defineConstraints": ["UNITY_INCLUDE_TESTS"]` so players exclude it.
- **Content assembly types live in namespace `VoidFall.Core`** (the asmdef's
  rootNamespace), not `VoidFall.Content`.
- **Identifier collisions exist**: `_fx` was already the ParticleSystem when we
  wanted it for FxSim; we used `_fxSim`. Grep before choosing a field name.

## Where the tooling lives

Roslyn helper tools (splitter/mover/typemover) were built under
`%TEMP%\opencode\` during the 2026-08-21 session. They are small single-file
console apps; recreate rather than depend on them. Pattern: parse → tile member
spans → extract/remove → validate parse → only then write.

## What not to do

- No ECS rewrite, no DI framework, no big-bang moves.
- Do not unify specialized order bookkeeping (enemy/meteor/pickup/boss) into
  `SlotOrder`; their browser semantics genuinely differ (see git history
  `493f275` for the six that legitimately shared semantics).
- Do not regenerate the golden-master hash casually; treat it as a release gate.
