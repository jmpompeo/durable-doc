#!/usr/bin/env bash

set -euo pipefail

config_path="${1:-cliff.toml}"
seed_tag="${SEED_TAG:-v0.1.0-preview.1}"
tag_pattern='^v[0-9]+\.[0-9]+\.[0-9]+(-preview\.[0-9]+)?$'

if ! command -v git-cliff >/dev/null 2>&1; then
  echo "git-cliff must be installed before running $0" >&2
  exit 1
fi

if ! command -v jq >/dev/null 2>&1; then
  echo "jq must be installed before running $0" >&2
  exit 1
fi

latest_tag="$(
  git for-each-ref --merged HEAD --sort=-version:refname --format='%(refname:short)' refs/tags \
    | grep -E "${tag_pattern}" \
    | head -n 1 || true
)"
head_tag="$(
  git tag --points-at HEAD \
    | grep -E "${tag_pattern}" \
    | head -n 1 || true
)"

context_json="$(git-cliff --config "${config_path}" --unreleased --context --offline)"
commit_count="$(printf '%s\n' "${context_json}" | jq '.[0].commits | length')"

if [[ "${commit_count}" == "0" ]]; then
  if [[ -n "${head_tag}" ]]; then
    release_tag="${head_tag}"
    release_version="${release_tag#v}"
    artifact_name="release-changelog-${release_tag}"
    changelog_path="artifacts/changelog/CHANGELOG-${release_tag}.md"

    cat <<EOF
should_release=true
tag_already_exists=true
latest_tag=${latest_tag}
bump_type=existing
release_tag=${release_tag}
release_version=${release_version}
artifact_name=${artifact_name}
changelog_path=${changelog_path}
EOF
    exit 0
  fi

  cat <<EOF
should_release=false
tag_already_exists=false
EOF
  exit 0
fi

if [[ -z "${latest_tag}" ]]; then
  release_tag="${seed_tag}"
  bump_type="initial"
else
  current_version="${latest_tag#v}"
  current_base="${current_version%%-*}"
  IFS='.' read -r major minor patch <<< "${current_base}"

  has_breaking="$(
    printf '%s\n' "${context_json}" | jq -r 'any(.[0].commits[]?; .breaking == true)'
  )"
  has_feature="$(
    printf '%s\n' "${context_json}" | jq -r 'any(.[0].commits[]?; .group == "Features")'
  )"

  if [[ "${has_breaking}" == "true" ]]; then
    bump_type="breaking"
    if (( major == 0 )); then
      minor=$((minor + 1))
      patch=0
    else
      major=$((major + 1))
      minor=0
      patch=0
    fi
  elif [[ "${has_feature}" == "true" ]]; then
    bump_type="minor"
    minor=$((minor + 1))
    patch=0
  else
    bump_type="patch"
    patch=$((patch + 1))
  fi

  release_tag="v${major}.${minor}.${patch}-preview.1"
fi

release_version="${release_tag#v}"
artifact_name="release-changelog-${release_tag}"
changelog_path="artifacts/changelog/CHANGELOG-${release_tag}.md"

cat <<EOF
should_release=true
tag_already_exists=false
latest_tag=${latest_tag}
bump_type=${bump_type}
release_tag=${release_tag}
release_version=${release_version}
artifact_name=${artifact_name}
changelog_path=${changelog_path}
EOF
