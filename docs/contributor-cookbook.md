# Contributor Cookbook

Use this cookbook for common enhancement tasks.

## Add support for a new Durable wrapper

1. Update built-in recognizers in `WorkflowAnalyzer` (or rely on config wrappers if this is user-specific).
2. Ensure target name extraction behavior is correct for argument position.
3. Add analysis tests covering positive + unresolved-name diagnostics.
4. Validate generated diagram labels in CLI tests where relevant.

## Add or change CLI options

1. Wire the option in `Program.cs` for the intended command scope.
2. Enforce behavior in the relevant command handler.
3. Add command tests for success and failure paths.
4. Update `README.md` command examples if user-facing behavior changed.

## Add dashboard interactions

1. Update template markup/style/script in `DashboardHtmlTemplate`.
2. Keep state transitions explicit (selection, compare, URL sync, refresh).
3. Validate with generated artifacts and interactive preview (`--open`) when possible.
4. Add/extend tests for output/build behavior in CLI suite.

## Keep deterministic output stable

When touching node/edge ordering, update and verify domain tests to preserve deterministic serialization expectations.

## Quick validation checklist

```bash
dotnet test durable-doc.sln
```

Optionally run representative commands from `README.md` after behavior changes.
