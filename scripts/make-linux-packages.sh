#!/usr/bin/env bash
# ============================================================
# IcomRigControl — Linux .deb and .rpm packages
#
# No signing required for Linux distribution.
#
# Usage:
#   bash scripts/make-linux-packages.sh [linux-x64|linux-arm64] [version]
#
# Requirements:
#   .deb: dpkg-dev   (sudo apt install dpkg-dev)
#   .rpm: rpm-build  (sudo dnf install rpm-build)
#
# The installed package puts the app at /opt/icomrigcontrol, adds an
# `icomrigcontrol` launcher on PATH (so the headless Pi server can be started
# with:  icomrigcontrol --headless-server --port /dev/ttyUSB0 ...), and a
# desktop menu entry.
# ============================================================
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
RID="${1:-linux-x64}"
VERSION="${2:-$(git -C "$REPO_ROOT" describe --tags --abbrev=0 2>/dev/null | sed 's/^v//' || echo "0.0.0")}"
APP_EXE="IcomRigControl.UI"

if [[ "$RID" != "linux-x64" && "$RID" != "linux-arm64" ]]; then
  echo "Usage: $0 [linux-x64|linux-arm64] [version]" >&2; exit 2
fi

DEB_ARCH="$( [[ "$RID" == "linux-x64" ]] && echo "amd64" || echo "arm64" )"
RPM_ARCH="$( [[ "$RID" == "linux-x64" ]] && echo "x86_64" || echo "aarch64" )"
PUBLISH_DIR="$REPO_ROOT/artifacts/publish/$RID"
INSTALLER_DIR="$REPO_ROOT/artifacts/installers"
mkdir -p "$INSTALLER_DIR"

# ── 1. Publish ────────────────────────────────────────────────────────────────
if [[ ! -f "$PUBLISH_DIR/$APP_EXE" ]]; then
  echo "Publishing $RID..."
  dotnet publish "$REPO_ROOT/IcomRigControl.UI/IcomRigControl.UI.csproj" \
    -c Release -r "$RID" --self-contained true \
    -p:PublishSingleFile=false -p:PublishReadyToRun=true -p:Version="$VERSION" \
    -o "$PUBLISH_DIR"
fi

# ── 2. .deb ───────────────────────────────────────────────────────────────────
build_deb() {
  local DEB_NAME="icomrigcontrol_${VERSION}_${DEB_ARCH}.deb"
  local STAGING
  STAGING="$(mktemp -d)"
  trap "rm -rf '$STAGING'" RETURN

  local APP_DIR="$STAGING/opt/icomrigcontrol"
  mkdir -p "$APP_DIR" \
    "$STAGING/usr/local/bin" \
    "$STAGING/usr/share/applications" \
    "$STAGING/DEBIAN"

  cp -R "$PUBLISH_DIR"/. "$APP_DIR/"
  chmod +x "$APP_DIR/$APP_EXE"

  # Launcher
  cat > "$STAGING/usr/local/bin/icomrigcontrol" << 'LAUNCHER'
#!/usr/bin/env bash
exec /opt/icomrigcontrol/IcomRigControl.UI "$@"
LAUNCHER
  chmod +x "$STAGING/usr/local/bin/icomrigcontrol"

  # .desktop entry
  cat > "$STAGING/usr/share/applications/icomrigcontrol.desktop" << DESKTOP
[Desktop Entry]
Name=IcomRigControl
Comment=Control software for Icom IC-7300, IC-7300MK2 and IC-705
Exec=/opt/icomrigcontrol/IcomRigControl.UI
Icon=icomrigcontrol
Terminal=false
Type=Application
Categories=HamRadio;Network;
Keywords=Icom;CI-V;ham radio;transceiver;IC-7300;IC-705;
DESKTOP

  # control file
  cat > "$STAGING/DEBIAN/control" << CONTROL
Package: icomrigcontrol
Version: $VERSION
Architecture: $DEB_ARCH
Maintainer: KE4CON <ke4con@users.noreply.github.com>
Installed-Size: $(du -sk "$APP_DIR" | awk '{print $1}')
Description: Control software for Icom IC-7300, IC-7300MK2 and IC-705
 IcomRigControl is an open-source cross-platform controller for the Icom
 IC-7300, IC-7300MK2, and IC-705. CI-V rig control, spectrum scope/waterfall,
 QSO logging, remote/network operation, remote audio, and a phone/tablet web
 remote. Built with .NET 10 and Avalonia UI. Also runs headless on a
 Raspberry Pi as a remote server (--headless-server).
Homepage: https://github.com/KE4CON/IcomRigControl
Section: hamradio
Priority: optional
CONTROL

  # postinst
  cat > "$STAGING/DEBIAN/postinst" << 'POSTINST'
#!/bin/sh
set -e
chmod +x /opt/icomrigcontrol/IcomRigControl.UI 2>/dev/null || true
exit 0
POSTINST
  chmod 755 "$STAGING/DEBIAN/postinst"

  dpkg-deb --build --root-owner-group "$STAGING" "$INSTALLER_DIR/$DEB_NAME"
  echo "  -> $INSTALLER_DIR/$DEB_NAME"
}

# ── 3. .rpm ───────────────────────────────────────────────────────────────────
build_rpm() {
  local BUILD
  BUILD="$(mktemp -d)"
  trap "rm -rf '$BUILD'" RETURN
  mkdir -p "$BUILD"/{BUILD,RPMS,SOURCES,SPECS,SRPMS}

  # Source tarball
  local SRC="icomrigcontrol-$VERSION"
  mkdir -p "$BUILD/BUILD/$SRC/opt/icomrigcontrol"
  cp -R "$PUBLISH_DIR"/. "$BUILD/BUILD/$SRC/opt/icomrigcontrol/"
  chmod +x "$BUILD/BUILD/$SRC/opt/icomrigcontrol/$APP_EXE"
  (cd "$BUILD/BUILD" && tar -czf "$BUILD/SOURCES/${SRC}.tar.gz" "$SRC")

  cat > "$BUILD/SPECS/icomrigcontrol.spec" << SPEC
Name:      icomrigcontrol
Version:   $VERSION
Release:   1%{?dist}
Summary:   Control software for Icom IC-7300, IC-7300MK2 and IC-705
License:   Proprietary
URL:       https://github.com/KE4CON/IcomRigControl
Source0:   %{name}-%{version}.tar.gz
BuildArch: $RPM_ARCH
AutoReqProv: no

%description
IcomRigControl is an open-source cross-platform controller for the Icom
IC-7300, IC-7300MK2, and IC-705. Built with .NET 10 and Avalonia UI.

%prep
%setup -q

%install
mkdir -p %{buildroot}/opt/icomrigcontrol
cp -R opt/icomrigcontrol/. %{buildroot}/opt/icomrigcontrol/
chmod +x %{buildroot}/opt/icomrigcontrol/$APP_EXE
mkdir -p %{buildroot}/usr/local/bin
printf '#!/usr/bin/env bash\nexec /opt/icomrigcontrol/IcomRigControl.UI "\$@"\n' \
  > %{buildroot}/usr/local/bin/icomrigcontrol
chmod +x %{buildroot}/usr/local/bin/icomrigcontrol
mkdir -p %{buildroot}/usr/share/applications
cat > %{buildroot}/usr/share/applications/icomrigcontrol.desktop << 'DESKTOP'
[Desktop Entry]
Name=IcomRigControl
Comment=Control software for Icom IC-7300, IC-7300MK2 and IC-705
Exec=/opt/icomrigcontrol/IcomRigControl.UI
Terminal=false
Type=Application
Categories=HamRadio;Network;
DESKTOP

%files
/opt/icomrigcontrol/
/usr/local/bin/icomrigcontrol
/usr/share/applications/icomrigcontrol.desktop

%changelog
* $(date '+%a %b %d %Y') KE4CON <ke4con@users.noreply.github.com> - $VERSION-1
- Release $VERSION
SPEC

  rpmbuild --define "_topdir $BUILD" -bb "$BUILD/SPECS/icomrigcontrol.spec" 2>&1
  local RPM
  RPM="$(find "$BUILD/RPMS" -name "*.rpm" | head -1)"
  if [[ -n "$RPM" ]]; then
    cp "$RPM" "$INSTALLER_DIR/"
    echo "  -> $INSTALLER_DIR/$(basename "$RPM")"
  fi
}

# ── Run ───────────────────────────────────────────────────────────────────────
echo "Building Linux packages for $RID (version $VERSION)..."

if command -v dpkg-deb >/dev/null 2>&1; then
  echo "Building .deb..."
  build_deb
else
  echo "dpkg-deb not found — skipping .deb (sudo apt install dpkg-dev)"
fi

if command -v rpmbuild >/dev/null 2>&1; then
  echo "Building .rpm..."
  build_rpm
else
  echo "rpmbuild not found — skipping .rpm (sudo dnf install rpm-build)"
fi

echo ""
echo "Packages written to: $INSTALLER_DIR"
echo "  Install .deb:  sudo dpkg -i icomrigcontrol_*.deb"
echo "  Install .rpm:  sudo rpm -i icomrigcontrol-*.rpm"
