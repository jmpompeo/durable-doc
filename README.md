# durable-doc

`durable-doc` analyzes Azure Durable Functions orchestration code written in C# and generates workflow diagrams plus a lightweight local dashboard.

## Current MVP

- CLI commands: `generate`, `list`, `validate`, `dashboard`
- Input types: solution, project, source folder, or single `.cs` file
- Diagram modes: `developer` and `business`
- Renderer: Mermaid output
- Dashboard: static offline HTML with local JS assets, built either from generated artifacts or directly from source input
- Optional local preview: `--open` serves the dashboard on `localhost` and opens it in your browser
- Config: `durable-doc.json` for defaults, wrapper rules, include/exclude filters, and business-view overrides

## CLI examples

```bash
durable-doc list --input ./durable-doc.sln
durable-doc validate --input ./samples/DurableDoc.Sample.Advanced/DurableDoc.Sample.Advanced.csproj --strict
durable-doc generate --input ./samples/DurableDoc.Sample.Advanced/DurableDoc.Sample.Advanced.csproj --orchestrator RunCustomerOnboarding --mode developer --format mermaid --output ./docs/diagrams
durable-doc dashboard --input ./docs/diagrams
durable-doc dashboard --input ./samples/DurableDoc.Sample.Advanced/DurableDoc.Sample.Advanced.csproj --orchestrator RunCustomerOnboarding --mode developer --output ./docs/diagrams
durable-doc generate --input ./samples/DurableDoc.Sample.Advanced/DurableDoc.Sample.Advanced.csproj --output ./docs/diagrams --open
```

## Config example

```json
{
  "version": 1,
  "defaults": {
    "output": "docs/diagrams",
    "format": "mermaid"
  },
  "analysis": {
    "includePatterns": ["*.cs"],
    "excludePatterns": ["*.generated.cs"],
    "wrappers": [
      { "methodName": "CallActivityWithResult", "kind": "Activity", "targetNameArgumentIndex": 0 }
    ]
  },
  "businessView": {
    "orchestrators": [
      {
        "name": "RunCustomerOnboarding",
        "businessName": "Customer onboarding",
        "steps": [
          { "name": "ValidateCustomer", "businessName": "Validate customer" },
          { "name": "ReserveCreditCheck", "businessGroup": "Assess application" }
        ]
      }
    ]
  }
}
```

## Solution input behavior

When `--input` points to a `.sln`, discovery is strict: only projects included in that solution are analyzed. If the orchestrator lives outside the solution, use the `.csproj` path or a folder path instead.

## Dashboard input behavior

`dashboard --input` accepts either a generated artifact directory or source input (`.sln`, `.csproj`, `.cs`, or a source folder). When source input is used, `--output` follows the same default resolution as `generate`.

## Repository layout

```text
/durable-doc
  /src
  /tests
  /samples
  /docs/examples
```

## Verification

```bash
dotnet test durable-doc.sln
```
