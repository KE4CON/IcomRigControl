#!/usr/bin/env bash
# ============================================================
# IcomRigControl — macOS .app bundle + .dmg
#
# Builds an UNSIGNED bundle by default. Until Apple notarization is configured
# (see the Release workflow), users bypass Gatekeeper with right-click → Open on
# first launch. Once the Apple secrets are set, the Release workflow signs,
# notarizes, staples, and re-signs the .dmg automatically.
#
# Usage:
#   bash scripts/make-macos-app.sh [osx-arm64|osx-x64] [--dmg-only]
#
# Requirements (macOS only):
#   - .NET 10 SDK
#   - create-dmg (optional, brew install create-dmg — falls back to hdiutil)
# ============================================================
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
RID="${1:-osx-arm64}"
APP_EXE="IcomRigControl.UI"

# Second arg (or DMG_ONLY=1) repackages the EXISTING .app into the .dmg without rebuilding the bundle.
# Used by the signing flow: build once, sign+notarize+staple the .app, then repackage the stapled .app
# into the .dmg — a plain re-run would rm -rf and rebuild the .app unsigned.
DMG_ONLY=0
if [[ "${2:-}" == "--dmg-only" || "${DMG_ONLY:-0}" == "1" ]]; then
  DMG_ONLY=1
fi

if [[ "$RID" != "osx-arm64" && "$RID" != "osx-x64" ]]; then
  echo "Usage: $0 [osx-arm64|osx-x64] [--dmg-only]" >&2
  exit 2
fi

# Derive version from git tag, fall back to dev
VERSION="$(git -C "$REPO_ROOT" describe --tags --abbrev=0 2>/dev/null | sed 's/^v//' || echo "0.0.0-dev")"
ARCH="${RID#osx-}"
PUBLISH_DIR="$REPO_ROOT/artifacts/publish/$RID"
INSTALLER_DIR="$REPO_ROOT/artifacts/installers"
APP_NAME="IcomRigControl"
APP_BUNDLE="$INSTALLER_DIR/$APP_NAME.app"
DMG_OUT="$INSTALLER_DIR/IcomRigControl-$RID.dmg"

mkdir -p "$INSTALLER_DIR"

if [[ "$DMG_ONLY" -eq 1 ]]; then
  # Repackage-only: the .app must already exist (signed + stapled by the caller).
  if [[ ! -d "$APP_BUNDLE" ]]; then
    echo "ERROR: --dmg-only requires an existing $APP_BUNDLE (build it first)." >&2
    exit 3
  fi
  echo "Repackaging existing (signed) $APP_BUNDLE into the DMG..."
else

# ── 1. Publish ────────────────────────────────────────────────────────────────
if [[ ! -f "$PUBLISH_DIR/$APP_EXE" ]]; then
  echo "Publishing $RID..."
  dotnet publish "$REPO_ROOT/IcomRigControl.UI/IcomRigControl.UI.csproj" \
    -c Release -r "$RID" --self-contained true \
    -p:PublishSingleFile=false -p:PublishReadyToRun=true -p:Version="$VERSION" \
    -o "$PUBLISH_DIR"
fi

# ── 2. .app bundle ────────────────────────────────────────────────────────────
echo "Building $APP_NAME.app..."
rm -rf "$APP_BUNDLE"
mkdir -p "$APP_BUNDLE/Contents/MacOS" "$APP_BUNDLE/Contents/Resources"
cp -R "$PUBLISH_DIR"/. "$APP_BUNDLE/Contents/MacOS/"
chmod +x "$APP_BUNDLE/Contents/MacOS/$APP_EXE"

cat > "$APP_BUNDLE/Contents/Info.plist" << PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN"
  "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleName</key>              <string>IcomRigControl</string>
  <key>CFBundleDisplayName</key>       <string>IcomRigControl</string>
  <key>CFBundleIdentifier</key>        <string>com.ke4con.icomrigcontrol</string>
  <key>CFBundleVersion</key>           <string>$VERSION</string>
  <key>CFBundleShortVersionString</key><string>$VERSION</string>
  <key>CFBundleExecutable</key>        <string>$APP_EXE</string>
  <key>CFBundlePackageType</key>       <string>APPL</string>
  <key>LSMinimumSystemVersion</key>    <string>12.0</string>
  <key>NSHighResolutionCapable</key>   <true/>
  <key>NSHumanReadableCopyright</key>  <string>Copyright 2026 KE4CON.</string>
  <key>NSMicrophoneUsageDescription</key><string>IcomRigControl uses the microphone for remote-audio transmit and digital-mode audio.</string>
  <key>LSArchitecturePriority</key>
  <array><string>$ARCH</string></array>
  <key>NSAppTransportSecurity</key>
  <dict><key>NSAllowsLocalNetworking</key><true/></dict>
</dict>
</plist>
PLIST

echo "  -> $APP_BUNDLE"

fi  # end of the build-the-.app section (skipped in --dmg-only mode)

# ── 3. .dmg ───────────────────────────────────────────────────────────────────
rm -f "$DMG_OUT"
DMG_STAGING="$(mktemp -d)"
trap "rm -rf '$DMG_STAGING'" EXIT
cp -R "$APP_BUNDLE" "$DMG_STAGING/"

if command -v create-dmg >/dev/null 2>&1; then
  echo "Building DMG with create-dmg..."
  create-dmg \
    --volname "IcomRigControl" \
    --window-pos 200 120 --window-size 560 400 \
    --icon-size 100 \
    --icon "IcomRigControl.app" 140 190 \
    --hide-extension "IcomRigControl.app" \
    --app-drop-link 420 190 \
    "$DMG_OUT" "$DMG_STAGING/" 2>/dev/null || {
      echo "  create-dmg failed, falling back to hdiutil..."
      hdiutil create -volname "IcomRigControl" -srcfolder "$DMG_STAGING" \
        -ov -format UDZO "$DMG_OUT"
    }
elif command -v hdiutil >/dev/null 2>&1; then
  echo "Building DMG with hdiutil..."
  hdiutil create -volname "IcomRigControl" -srcfolder "$DMG_STAGING" \
    -ov -format UDZO "$DMG_OUT"
else
  echo "Neither create-dmg nor hdiutil found."
  echo "App bundle is ready at: $APP_BUNDLE"
  exit 0
fi

echo ""
echo "Done."
echo "  App bundle : $APP_BUNDLE"
echo "  DMG        : $DMG_OUT"
echo ""
echo "NOTE: An unsigned build. On first launch, users must right-click the .app"
echo "  and choose Open, then click Open again (until Apple notarization is set up)."
