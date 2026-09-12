# Automatic run exports — approved implementation

Scope: extend the existing RunTelemetryRecorder, without changing director,
population limits, arena dimensions, health, rewards or random draws.

1. Extend RunTelemetry.cs to schema 4: unique run identity, starting context,
   bounded asynchronous JSONL history, atomic JSON summary, explicit loss/error
   counters. Preserve schema 3 summary sections and manual export entry points.
2. Add a runtime Telemetry partial for context, one-second observations, combat
   attribution, lifecycle completion and thirty-second summary checkpoints.
   Hook authoritative spawn/death, pickup, director, upgrade and roulette paths.
3. Save normal players beneath the executable's RunExports directory, silently.
   Distinguish actual play from menu initialization; finish before retry/menu
   resets and on quit/destruction. Isolate automated tests from player exports.
4. Document schema, joins, interruption limits and agent instrumentation duties
   in Docs/RunExports.md, AGENTS.md and Docs/REPO_MAP.md.
5. Validate storage and runtime integration, deterministic simulation, and a
   Windows player export. Inspect actual JSON/JSONL, terminal status, record
   ordering and absence of exporter notifications. Preserve concurrent edits.

Deferred owner priorities: remake Director I with maximum 750 active enemies;
then address map size; then adapted enemy health. No part of those changes is
included in this infrastructure implementation.

Implementation and player verification completed. See
`2026-09-08-RunExports-Validation.md` for exact evidence and the concurrent
roulette-test limitations; `../RunExports.md` is the schema/extension contract.
