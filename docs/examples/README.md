# Diagram Examples

Use the advanced sample project to exercise the CLI manually:

```bash
durable-doc list --input ./samples/DurableDoc.Sample.Advanced/DurableDoc.Sample.Advanced.csproj
durable-doc validate --input ./samples/DurableDoc.Sample.Advanced/DurableDoc.Sample.Advanced.csproj
durable-doc generate --input ./samples/DurableDoc.Sample.Advanced/DurableDoc.Sample.Advanced.csproj --mode developer --output ./docs/examples/output
durable-doc dashboard --input ./docs/examples/output
```

If the command is not installed yet, package and install it locally:

```bash
dotnet pack ./src/DurableDoc.Cli/DurableDoc.Cli.csproj -o ./artifacts/tool
dotnet tool install --global durable-doc --add-source ./artifacts/tool --version 0.1.0
```

The advanced sample includes:

- one main onboarding orchestration
- three sub-orchestrations
- retry, timer, external-event, activity, and sub-orchestrator calls

Generated Mermaid files and the static dashboard will be written under `./docs/examples/output`.
