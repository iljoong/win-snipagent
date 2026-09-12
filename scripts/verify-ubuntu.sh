#!/usr/bin/env bash

set -euo pipefail

repository_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
solution="${repository_root}/SnipAgent.slnx"
artifacts_dir="$(mktemp -d "${TMPDIR:-/tmp}/snipagent-ubuntu-verify.XXXXXX")"

export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1

cleanup() {
    rm -rf -- "${artifacts_dir}"
}
trap cleanup EXIT

sdk_version="$(dotnet --version)"
if [[ "${sdk_version%%.*}" != "10" ]]; then
    printf 'Expected .NET 10 SDK, but dotnet --version returned %s.\n' "${sdk_version}" >&2
    exit 1
fi

cd "${repository_root}"

printf 'Restoring and building %s with .NET SDK %s on Ubuntu...\n' "${solution}" "${sdk_version}"
dotnet build "${solution}" -c Debug --artifacts-path "${artifacts_dir}"

cat <<'EOF'
Ubuntu verification completed successfully.

This is a compile-only gate. The Windows-targeted tests were not run because
Microsoft.WindowsDesktop.App and interactive Windows APIs are unavailable on
Ubuntu. Run .\scripts\verify.ps1 and required manual checks on Windows before
claiming complete verification.
EOF
