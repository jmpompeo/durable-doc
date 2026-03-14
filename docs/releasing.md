# Releasing durable-doc

This repository is prepared for NuGet publishing, but public publishing is intentionally disabled by default.

It also auto-generates preview release tags and release-note artifacts on pushes to `main`.

## Current release model

- Build validation runs in GitHub Actions on pushes to `main` and on pull requests.
- Preview release changelogs run from `.github/workflows/release-changelog.yml` on pushes to `main`.
- Package creation runs from `.github/workflows/publish.yml` through `workflow_dispatch`.
- The workflow always validates the version, runs tests, packs the CLI, and uploads the `.nupkg` as a workflow artifact.
- The NuGet push job stays off unless both of these are true:
  - the workflow is dispatched with `publish_to_nuget` enabled
  - the repository variable `ENABLE_NUGET_PUBLISH` is set to `true`

This keeps the repo release-ready without forcing a public package owner or go-live date.

## Preview release tags and changelog artifacts

Pushes to `main` are treated as the preview release channel.

- The workflow inspects unreleased conventional commits with `git-cliff`.
- `feat` commits bump the minor version.
- `fix`, `perf`, `refactor`, and `revert` commits bump the patch version.
- Breaking changes bump the minor version while the project is still in `0.x`.
- `docs`, `test`, `chore`, `ci`, `build`, and `style` commits do not create a release by themselves.
- If there are releasable commits and no prior release tags, the first automated tag is `v0.1.0-preview.1`.
- Later preview releases are tagged as `vMAJOR.MINOR.PATCH-preview.1`.

Each successful run:

- creates and pushes an annotated git tag
- generates a markdown changelog for the tagged release with `git-cliff`
- uploads that file as a workflow artifact named `release-changelog-<tag>`

This workflow does not create a GitHub Release object and does not publish to NuGet.org.

## Versioning

The publish workflow derives the package version from a tag-shaped input.

- Accepted format: `vMAJOR.MINOR.PATCH`
- Accepted prerelease format: `vMAJOR.MINOR.PATCH-label.N`
- Examples:
  - `v0.2.0`
  - `v0.2.0-preview.1`

The workflow strips the leading `v` and passes the resulting version into `dotnet pack`.

The automated preview tagging flow uses the same tag format, so a generated preview tag can be passed directly into `publish.yml`.

## Rehearse the package workflow

Use `publish.yml` with:

- `release_tag`: a tag-style version such as `v0.2.0-preview.1`
- `publish_to_nuget`: `false`

Expected result:

- tests pass
- a `.nupkg` is uploaded as a workflow artifact
- nothing is pushed to NuGet.org

## Rehearse the changelog workflow

You can smoke-test the release-note configuration locally if `git-cliff` is installed:

```bash
git-cliff --config cliff.toml --unreleased
scripts/next-release-tag.sh
```

Expected result:

- unreleased conventional commits are grouped into release-note sections
- the helper script prints `should_release=true` and the next preview tag when releasable commits exist
- the helper script prints `should_release=false` for commit ranges that only contain skipped commit types

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
