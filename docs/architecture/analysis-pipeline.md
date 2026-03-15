# Analysis Pipeline (Contributor Map)

This document explains how analysis flows from input path to workflow diagram output.

## 1) Entry point

* `WorkflowAnalyzer.AnalyzeWorkspaceAsync` loads source methods and builds recognizers.
* It filters methods with `IsOrchestrator`, then builds diagrams through `WorkflowBuilder.Build`.

Primary file: `src/DurableDoc.Analysis/WorkflowAnalyzer.cs`.

## 2) Input loading and workspace behavior

`WorkspaceSourceLoader.LoadAsync` resolves input by kind:

* `.sln` → `LoadFromSolutionAsync`
* `.csproj` → `LoadFromProjectAsync`
* `.cs` file → `LoadFromFilesAsync`
* directory → recursive `*.cs` scan with include/exclude pattern filters

Important contributor notes:

* Solution mode is strict by solution membership (only included projects are scanned).
* Include/exclude filters are applied relative to project root (or folder input root).

## 3) Orchestrator detection

`IsOrchestrator` uses two heuristics:

* Has attribute containing `OrchestrationTrigger`.
* OR has known durable context parameter type and is not `private`.

When adjusting this logic, add tests in `tests/DurableDoc.Analysis.Tests` to keep discovery stable.

## 4) Workflow graph building

`WorkflowBuilder` creates nodes/edges from statements and expressions.

Key modeling behaviors:

* `if`/`switch` create decision nodes and labeled branch edges.
* `Task.WhenAll` is represented as Fan-out/Fan-in.
* helper methods inside the same containing type may be inlined.
* `try/catch` is only partially modeled and emits warnings.

## 5) Durable call recognition

Recognizer map comes from:

* built-in durable wrappers
* user-configured wrappers in `durable-doc.json` (`analysis.wrappers`)

`TryCreateDurableNode` uses recognizer metadata to infer node type and target step name.

## 6) Business metadata overlay

After graph construction, `BusinessMetadataApplicator.Apply` overlays orchestrator and step metadata from config.

This updates labels/business fields but preserves source location and structural edges.

## 7) Output contract

The analyzer returns `WorkflowAnalysisResult` with:

* resolved input path and input kind
* scanned projects (when applicable)
* generated diagrams

CLI commands use this result for no-discovery messages, filtering, and output generation.
