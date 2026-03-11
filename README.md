# durable-doc

`durable-doc` is a CLI tool that will analyze Azure Durable Functions orchestration code written in C# and generate workflow diagrams.

## Status

This repository is currently in **Phase 0 (scaffolding)**. Implementation is being delivered in phases to keep architecture stable and changes easy to review.

## Planned Phases

1. Core parser prototype
2. Intermediate model + Mermaid renderer
3. Business mode + metadata
4. CLI hardening
5. Dashboard MVP
6. Workflow integration

## Repository Layout

```text
/durable-doc
  /src
    /DurableDoc.Cli
    /DurableDoc.Configuration
    /DurableDoc.Analysis
    /DurableDoc.Domain
    /DurableDoc.Rendering.Mermaid
    /DurableDoc.Dashboard
  /tests
    /DurableDoc.Configuration.Tests
    /DurableDoc.Analysis.Tests
    /DurableDoc.Domain.Tests
    /DurableDoc.Rendering.Tests
    /DurableDoc.Cli.Tests
  /samples
    /DurableDoc.Sample.Simple
    /DurableDoc.Sample.Advanced
  /docs
    /examples
```

## Phase 0 Scope Notes

- CLI command surface is scaffolded (`generate`, `list`, `validate`, `dashboard`)
- Projects and tests are placeholders to establish boundaries
- No Roslyn analysis, rendering logic, dashboard implementation, or config behavior is implemented yet
