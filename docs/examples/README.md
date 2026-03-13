# Diagram Examples

Use the sample inputs to exercise each supported input mode:

```bash
durable-doc list --input ./durable-doc.sln
durable-doc list --input ./samples/DurableDoc.Sample.Advanced/DurableDoc.Sample.Advanced.csproj
durable-doc list --input ./samples/DurableDoc.Sample.Advanced
durable-doc list --input ./samples/DurableDoc.Sample.Advanced/SampleAdvancedOrchestrator.cs
```

Generate diagrams and rebuild the dashboard:

```bash
durable-doc generate --input ./samples/DurableDoc.Sample.Advanced/DurableDoc.Sample.Advanced.csproj --mode developer --format mermaid --output ./docs/examples/output
durable-doc generate --input ./samples/DurableDoc.Sample.Advanced/DurableDoc.Sample.Advanced.csproj --mode business --format mermaid --output ./docs/examples/output
durable-doc dashboard --input ./docs/examples/output
```

Validate with strict warnings:

```bash
durable-doc validate --input ./samples/DurableDoc.Sample.Advanced/DurableDoc.Sample.Advanced.csproj --strict
```

Repack and reinstall the global tool after local changes:

```bash
# Only run this if `dotnet tool list --global` shows durable-doc already installed.
dotnet tool uninstall --global durable-doc
dotnet pack ./src/DurableDoc.Cli/DurableDoc.Cli.csproj -o ./artifacts/tool
dotnet tool install --global durable-doc --add-source ./artifacts/tool --version 0.0.0-dev
```

If a `.sln` does not include the target orchestrator project, the CLI now fails with a discovery message that explains solution membership is strict and suggests using the `.csproj` path or a folder path instead.

The advanced sample covers:

- activity calls
- retry activity calls
- sub-orchestrators
- external events
- timers

The simple sample covers:

- sequential activity flow
