# Releasing durable-doc

This repository is prepared for NuGet publishing, but public publishing is intentionally disabled by default.

## Current release model

- Build validation runs in GitHub Actions on pushes to `main` and on pull requests.
- Package creation runs from `.github/workflows/publish.yml` through `workflow_dispatch`.
- The workflow always validates the version, runs tests, packs the CLI, and uploads the `.nupkg` as a workflow artifact.
- The NuGet push job stays off unless both of these are true:
  - the workflow is dispatched with `publish_to_nuget` enabled
  - the repository variable `ENABLE_NUGET_PUBLISH` is set to `true`

This keeps the repo release-ready without forcing a public package owner or go-live date.

## Versioning

The publish workflow derives the package version from a tag-shaped input.

- Accepted format: `vMAJOR.MINOR.PATCH`
- Accepted prerelease format: `vMAJOR.MINOR.PATCH-label.N`
- Examples:
  - `v0.2.0`
  - `v0.2.0-preview.1`

The workflow strips the leading `v` and passes the resulting version into `dotnet pack`.

## Rehearse the package workflow

Use `publish.yml` with:

- `release_tag`: a tag-style version such as `v0.2.0-preview.1`
- `publish_to_nuget`: `false`

Expected result:

- tests pass
- a `.nupkg` is uploaded as a workflow artifact
- nothing is pushed to NuGet.org

For local rehearsal, you can also pack and install the tool from a temporary directory:

```bash
dotnet test durable-doc.sln
dotnet pack ./src/DurableDoc.Cli/DurableDoc.Cli.csproj -c Release -o /tmp/durable-doc-pack /p:Version=0.2.0-preview.1 /p:PackageVersion=0.2.0-preview.1
dotnet tool install durable-doc --tool-path /tmp/durable-doc-tool --add-source /tmp/durable-doc-pack --version 0.2.0-preview.1
/tmp/durable-doc-tool/durable-doc --help
```

## Enable public publishing later

When you are ready to go live, keep the workflow file unchanged and finish the surrounding configuration.

1. Confirm the package ID `durable-doc` is available on NuGet.org. If it is not, stop and choose a deliberate new name.
2. Create or update a NuGet Trusted Publishing policy for the future package owner.
3. In GitHub, create the `release` environment and add the approval rules you want.
4. Add repository secret `NUGET_USER` with the NuGet profile name that should publish.
5. Set repository variable `ENABLE_NUGET_PUBLISH` to `true`.
6. Run `publish.yml` with `publish_to_nuget` enabled, or later add the commented tag push trigger in the workflow.

## Ownership handoff

This setup is intentionally transferable.

- If you publish first from a personal NuGet account, a company can later take over by changing the Trusted Publishing policy owner, `NUGET_USER`, and any environment approvals.
- If the company wants to own the package from day one, use the company or organization owner when creating the Trusted Publishing policy and keep the repo workflow unchanged.
- Publisher identity is isolated to GitHub environment and NuGet configuration. It is not hardcoded in the project file.

## Trusted Publishing notes

NuGet's recommended GitHub Actions path uses OIDC and `NuGet/login@v1` to mint a short-lived API key during the publish job. The workflow in this repo is structured for that model, but the publish job remains opt-in until you explicitly enable it.
