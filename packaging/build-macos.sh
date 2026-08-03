#!/usr/bin/env bash
# Cross-publishes a self-contained osx-arm64 build from a non-macOS host and
# wraps it in a minimal .app bundle. Like build-windows.sh, this is NOT
# NativeAOT (cross-OS AOT isn't supported, see .charter/decisions.md) — it's
# a standard self-contained JIT publish.
#
# NOT done here, and genuinely expensive/out of scope for this project:
# code signing and notarization. Without an Apple Developer Program
# membership ($99/year) and a notarization step, Gatekeeper will refuse to
# open this app on any Mac other than the one that built it, unless the user
# explicitly right-click -> Open's it (or runs `xattr -dr
# com.apple.quarantine view-md.app` after unzipping). That's an acceptable
# tradeoff for personal/from-source use; it is NOT acceptable for public
# distribution — see .charter/decisions.md.
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROJECT="$ROOT_DIR/src/ViewMd/ViewMd.csproj"
PUBLISH_DIR="$ROOT_DIR/packaging/.publish-mac"
APP_DIR="$ROOT_DIR/packaging/app-bundle/view-md.app"
DIST="$ROOT_DIR/dist"
VERSION="$(cat "$ROOT_DIR/version.txt" | tr -d '[:space:]')"

echo "==> Publishing self-contained osx-arm64 build (JIT, not AOT)..."
rm -rf "$PUBLISH_DIR"
dotnet publish "$PROJECT" -c Release -r osx-arm64 --self-contained true -o "$PUBLISH_DIR"

echo "==> Assembling view-md.app bundle..."
rm -rf "$APP_DIR"
mkdir -p "$APP_DIR/Contents/MacOS" "$APP_DIR/Contents/Resources"
cp -R "$PUBLISH_DIR"/. "$APP_DIR/Contents/MacOS/"
cp "$ROOT_DIR/packaging/macos/Info.plist" "$APP_DIR/Contents/Info.plist"

echo "==> Zipping..."
mkdir -p "$DIST"
ZIP_PATH="$DIST/view-md_${VERSION}_macos-arm64.app.zip"
rm -f "$ZIP_PATH"
(cd "$ROOT_DIR/packaging/app-bundle" && zip -rq "$ZIP_PATH" "view-md.app")

echo "==> Done: $ZIP_PATH"
echo "    Unsigned build — see the comment at the top of this script before"
echo "    distributing it to a Mac other than the one that built it."
