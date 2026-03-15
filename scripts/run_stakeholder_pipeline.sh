#!/usr/bin/env bash

set -euo pipefail

usage() {
  cat <<'EOF'
Run the local stakeholder generation, packaging, and PDF pipeline.

Usage:
  bash ./scripts/run_stakeholder_pipeline.sh

Environment overrides:
  INPUT_PATH     Source project/solution/folder to analyze
  GENERATED_DIR  Directory for generated stakeholder dashboard files
  PACKAGE_DIR    Directory for packaged standalone stakeholder output
  PDF_PATH       Output PDF path
  PWSH_BIN       Path to a local PowerShell binary
  CHROME_BIN     Path to a local Chrome/Chromium binary
EOF
}

if [[ "${1:-}" == "--help" || "${1:-}" == "-h" ]]; then
  usage
  exit 0
fi

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

input_path="${INPUT_PATH:-$repo_root/samples/DurableDoc.Sample.Advanced/DurableDoc.Sample.Advanced.csproj}"
generated_dir="${GENERATED_DIR:-$repo_root/docs/stakeholder}"
package_dir="${PACKAGE_DIR:-$repo_root/artifacts/stakeholder}"
pdf_path="${PDF_PATH:-$package_dir/stakeholder-dashboard.pdf}"
chrome_bin="${CHROME_BIN:-}"
pwsh_bin="${PWSH_BIN:-}"

find_pwsh() {
  if [[ -n "$pwsh_bin" && -x "$pwsh_bin" ]]; then
    printf '%s\n' "$pwsh_bin"
    return 0
  fi

  if command -v pwsh >/dev/null 2>&1; then
    command -v pwsh
    return 0
  fi

  return 1
}

find_chrome() {
  if [[ -n "$chrome_bin" && -x "$chrome_bin" ]]; then
    printf '%s\n' "$chrome_bin"
    return 0
  fi

  local candidate
  for candidate in \
    "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome" \
    "/Applications/Chromium.app/Contents/MacOS/Chromium"
  do
    if [[ -x "$candidate" ]]; then
      printf '%s\n' "$candidate"
      return 0
    fi
  done

  for candidate in google-chrome chromium chromium-browser chrome; do
    if command -v "$candidate" >/dev/null 2>&1; then
      command -v "$candidate"
      return 0
    fi
  done

  return 1
}

run_step() {
  printf '\n[%s] %s\n' "$(date '+%H:%M:%S')" "$1"
}

mkdir -p "$generated_dir" "$package_dir"

pwsh_path="$(find_pwsh)" || {
  printf '%s\n' "PowerShell not found. Set PWSH_BIN to a local pwsh binary." >&2
  exit 1
}

run_step "Restoring solution"
dotnet restore "$repo_root/durable-doc.sln"

run_step "Building solution"
dotnet build "$repo_root/durable-doc.sln" --configuration Release --no-restore

run_step "Generating stakeholder dashboard"
dotnet run --project "$repo_root/src/DurableDoc.Cli" --configuration Release -- \
  generate \
  --input "$input_path" \
  --audience stakeholder \
  --output "$generated_dir"

run_step "Packaging standalone stakeholder dashboard"
"$pwsh_path" -NoProfile -File "$repo_root/.github/scripts/package_stakeholder_dashboard.ps1" \
  -InputDir "$generated_dir" \
  -OutputDir "$package_dir"

chrome_path="$(find_chrome)" || {
  printf '%s\n' "Chrome or Chromium not found. Set CHROME_BIN to a local browser binary." >&2
  exit 1
}

run_step "Rendering stakeholder PDF"
"$chrome_path" \
  --headless \
  --disable-gpu \
  --no-sandbox \
  --virtual-time-budget=10000 \
  --run-all-compositor-stages-before-draw \
  "--print-to-pdf=$pdf_path" \
  "file://$package_dir/index.html"

run_step "Verifying output"
test -s "$package_dir/index.html"
test -s "$pdf_path"

printf '\nPackaged stakeholder output:\n'
printf '  HTML: %s\n' "$package_dir/index.html"
printf '  PDF:  %s\n' "$pdf_path"
