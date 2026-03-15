# CLI Flow (Contributor Map)

This guide describes how command-line options are wired and where behavior is implemented.

## 1) Command wiring

`src/DurableDoc.Cli/Program.cs` defines global options and command-specific options, then delegates to handlers:

* `generate` → `GenerateCommandHandler.ExecuteAsync`
* `list` → `ListCommandHandler.ExecuteAsync`
* `validate` → `ValidateCommandHandler.ExecuteAsync`
* `dashboard` → `DashboardCommandHandler.ExecuteAsync`

## 2) Shared behavior patterns

Across handlers:

* load config via `DurableDocConfigLoader`
* analyze source via `WorkflowAnalyzer` when input is source
* apply orchestrator filter via `WorkflowSelection`
* emit user-facing errors through `CliCommandContext`


## 2.5) Shared source-loading helper

Source-based command paths now use `SourceWorkflowLoader.LoadSelectedDiagramsAsync` to centralize:

* config loading
* analysis execution
* no-discovery handling
* orchestrator filter handling
* output directory resolution

This keeps `generate` and `dashboard` source paths aligned and reduces duplicated branching logic.

## 3) Generate command flow

`GenerateCommandHandler` responsibilities:

1. Validate preview mode constraints (`--open`).
2. Parse mode and format.
3. Resolve output directory from CLI option/config/default.
4. Run analysis.
5. Apply orchestrator filter.
6. Surface diagnostics and strict-mode behavior.
7. Write artifacts + dashboard.
8. Optionally launch preview host.

## 4) Dashboard command dual-mode flow

`DashboardCommandHandler` first resolves input kind:

* artifact directory (`*.diagram.json` present)
* source input (`.sln`, `.csproj`, `.cs`, or source directory)

Then:

* Artifact mode: rebuild dashboard from existing artifacts; reject source-only options.
* Source mode: run analysis, generate artifacts, build dashboard.

## 5) Contributor guardrails

When changing CLI behavior:

* Preserve no-discovery messaging and filter mismatch diagnostics unless intentionally updated.
* Keep source/artifact branching explicit and tested.
* Prefer shared helper methods over duplicating branch logic between `generate` and `dashboard`.

## 6) Test coverage anchors

Add/adjust tests in `tests/DurableDoc.Cli.Tests/SmokeTests.cs` for:

* orchestrator filtering
* strict mode behavior
* dashboard artifact rebuild behavior
* source vs artifact input handling
