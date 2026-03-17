# AGENTS.md

## Purpose

Reduce cognitive load for future contributors and agent prompts by establishing a consistent entry workflow.

## First-pass orientation

1. Read `README.md` for product behavior and command expectations.
2. For analysis logic, start with `src/DurableDoc.Analysis/WorkflowAnalyzer.cs`.
3. For CLI behavior, read `src/DurableDoc.Cli/Program.cs` first, then the relevant command handler.
4. For dashboard behavior, read `src/DurableDoc.Dashboard/DashboardHtmlTemplate.cs`.
5. Review tests in `tests/` that map to the area you are changing.

## Engineering principles (required)

All enhancements should explicitly follow:

* **DRY**: avoid duplicating behavior or business logic.
* **KISS**: prefer the simplest implementation that satisfies requirements.
* **SOLID**: maintain clear responsibilities and extensibility boundaries.
* **YAGNI**: do not add speculative features or abstractions before they are needed.

## Change guardrails

* Preserve source vs artifact input behavior for CLI commands.
* Preserve no-discovery and filter mismatch guidance unless intentionally changing UX.
* Keep diagram output deterministic where tests rely on ordering.
* Favor small, focused refactors over broad rewrites.

## Validation expectations

At minimum run:

```bash
dotnet test durable-doc.sln
```

If command behavior changes, run representative CLI commands from `README.md`.
