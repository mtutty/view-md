#!/usr/bin/env bash
# Cross-publishes a self-contained win-x64 build from a non-Windows host and
# zips it. This is NOT NativeAOT — Native AOT cannot cross-compile between
# operating systems (see .charter/decisions.md), so this is a standard
# self-contained (JIT) publish: slightly slower cold start than the Linux
# NativeAOT build, larger on disk, but genuinely runs on Windows.
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROJECT="$ROOT_DIR/src/ViewMd/ViewMd.csproj"
PUBLISH_DIR="$ROOT_DIR/packaging/.publish-win"
DIST="$ROOT_DIR/dist"
VERSION="$(cat "$ROOT_DIR/version.txt" | tr -d '[:space:]')"

echo "==> Publishing self-contained win-x64 build (JIT, not AOT)..."
rm -rf "$PUBLISH_DIR"
dotnet publish "$PROJECT" -c Release -r win-x64 --self-contained true -o "$PUBLISH_DIR"

echo "==> Zipping..."
mkdir -p "$DIST"
ZIP_PATH="$DIST/view-md_${VERSION}_win-x64.zip"
rm -f "$ZIP_PATH"
(cd "$PUBLISH_DIR" && zip -rq "$ZIP_PATH" .)

echo "==> Done: $ZIP_PATH"
echo "    Unzip and run view-md.exe. To register the .md file association,"
echo "    run packaging/windows/register-file-association.ps1 as the target user."
