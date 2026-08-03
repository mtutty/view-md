#!/usr/bin/env bash
# Builds the view-md .deb package: publishes a self-contained NativeAOT
# linux-x64 binary, stages it into packaging/deb/, and runs dpkg-deb.
# See .charter/capabilities/file-association.md for why this is a plain
# .deb rather than snap/flatpak.
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROJECT="$ROOT_DIR/src/ViewMd/ViewMd.csproj"
STAGE="$ROOT_DIR/packaging/deb"
DIST="$ROOT_DIR/dist"

echo "==> Publishing NativeAOT linux-x64 build..."
dotnet publish "$PROJECT" -c Release -r linux-x64 --self-contained true -o "$ROOT_DIR/packaging/.publish"

echo "==> Staging package contents..."
STAGE_LIB="$STAGE/usr/lib/view-md"
rm -rf "$STAGE_LIB"
mkdir -p "$STAGE_LIB"

# Exclude .dbg debug-symbol files — not needed in the shipped package.
find "$ROOT_DIR/packaging/.publish" -maxdepth 1 -type f ! -name '*.dbg' -exec cp {} "$STAGE_LIB/" \;

ln -sf ../lib/view-md/view-md "$STAGE/usr/bin/view-md"

chmod 755 "$STAGE_LIB/view-md"
chmod 644 "$STAGE_LIB"/*.so
chmod 755 "$STAGE/DEBIAN/postinst" "$STAGE/DEBIAN/postrm"
chmod 644 "$STAGE/usr/share/applications/view-md.desktop"

echo "==> Building .deb..."
mkdir -p "$DIST"
VERSION="$(grep '^Version:' "$STAGE/DEBIAN/control" | awk '{print $2}')"
dpkg-deb --build --root-owner-group "$STAGE" "$DIST/view-md_${VERSION}_amd64.deb"

echo "==> Done: $DIST/view-md_${VERSION}_amd64.deb"
