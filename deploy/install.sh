#!/usr/bin/env bash
# Barrelo installer for Linux (systemd). Installs or upgrades Barrelo under /opt/barrelo.
#
#   curl -fsSL https://raw.githubusercontent.com/FebrevdMerwe/Barrelo/main/deploy/install.sh | sudo bash
#   curl -fsSL .../install.sh | sudo bash -s -- --version 1.2.3 --with-simulator
#
# Options:
#   --version X        Install release X (default: latest). Also: BARRELO_VERSION=X
#   --with-simulator   Also install and start the Board Simulator service (port 5250)
#   --uninstall        Stop and remove the services; keeps /opt/barrelo (your database)
#   --purge            With --uninstall, also delete /opt/barrelo
#
# Re-running upgrades in place; barrelo.db is never touched.
# The unit files below mirror deploy/barrelo.service and deploy/barrelo-boardsimulator.service - keep in sync.
set -euo pipefail

REPO="FebrevdMerwe/Barrelo"
INSTALL_DIR="/opt/barrelo"
VERSION="${BARRELO_VERSION:-}"
WITH_SIMULATOR=0
UNINSTALL=0
PURGE=0

log()  { printf '==> %s\n' "$*"; }
fail() { printf 'error: %s\n' "$*" >&2; exit 1; }

while [ $# -gt 0 ]; do
    case "$1" in
        --version)        [ $# -ge 2 ] || fail "--version needs a value"; VERSION="$2"; shift ;;
        --with-simulator) WITH_SIMULATOR=1 ;;
        --uninstall)      UNINSTALL=1 ;;
        --purge)          PURGE=1 ;;
        -h|--help)        sed -n '2,13p' "$0" 2>/dev/null | sed 's/^# \{0,1\}//'; exit 0 ;;
        *)                fail "unknown option: $1" ;;
    esac
    shift
done

if [ "$(id -u)" -ne 0 ]; then
    fail "must run as root - pipe into 'sudo bash' (curl ... | sudo bash), or run as root directly"
fi
[ -d /run/systemd/system ] || fail "systemd not detected; this installer only supports systemd hosts"

if [ "$UNINSTALL" -eq 1 ]; then
    log "Removing Barrelo services"
    for svc in barrelo barrelo-boardsimulator; do
        systemctl disable --now "$svc" 2>/dev/null || true
        rm -f "/etc/systemd/system/$svc.service"
    done
    systemctl daemon-reload
    if [ "$PURGE" -eq 1 ]; then
        log "Deleting $INSTALL_DIR"
        rm -rf "$INSTALL_DIR"
    else
        log "Kept $INSTALL_DIR (database included). Use --uninstall --purge to delete it."
    fi
    exit 0
fi

case "$(uname -m)" in
    x86_64|amd64)  RID="linux-x64" ;;
    aarch64|arm64) RID="linux-arm64" ;;
    *) fail "unsupported architecture: $(uname -m) (supported: x86_64, aarch64)" ;;
esac

command -v curl >/dev/null   || fail "curl is required (e.g. apt install curl)"
command -v tar >/dev/null    || fail "tar is required (e.g. apt install tar)"
command -v sha256sum >/dev/null || fail "sha256sum is required (coreutils)"

if [ -z "$VERSION" ]; then
    log "Looking up latest release"
    VERSION="$(curl -fsSL "https://api.github.com/repos/$REPO/releases/latest" \
        | sed -n 's/.*"tag_name": *"v\{0,1\}\([^"]*\)".*/\1/p' | head -n 1)"
    [ -n "$VERSION" ] || fail "could not determine the latest release (no releases published yet?)"
fi
VERSION="${VERSION#v}"

ASSET="Barrelo-$VERSION-$RID.tar.gz"
BASE_URL="https://github.com/$REPO/releases/download/v$VERSION"
TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

log "Downloading Barrelo $VERSION ($RID)"
curl -fSL --progress-bar -o "$TMP/$ASSET" "$BASE_URL/$ASSET" || fail "download failed: $BASE_URL/$ASSET"
curl -fsSL -o "$TMP/SHA256SUMS" "$BASE_URL/SHA256SUMS" || fail "download failed: $BASE_URL/SHA256SUMS"

log "Verifying checksum"
( cd "$TMP" && grep -E "[ *]$ASSET\$" SHA256SUMS | sha256sum -c - >/dev/null ) \
    || fail "checksum mismatch for $ASSET - aborting"

for svc in barrelo barrelo-boardsimulator; do
    if systemctl is-active --quiet "$svc" 2>/dev/null; then
        log "Stopping $svc"
        systemctl stop "$svc"
    fi
done

log "Installing to $INSTALL_DIR"
mkdir -p "$INSTALL_DIR"
tar -xzf "$TMP/$ASSET" -C "$INSTALL_DIR"
chmod +x "$INSTALL_DIR/Barrelo.Api"
[ -f "$INSTALL_DIR/tools/BoardSimulator/Barrelo.BoardSimulator" ] \
    && chmod +x "$INSTALL_DIR/tools/BoardSimulator/Barrelo.BoardSimulator"

log "Installing systemd service"
cat > /etc/systemd/system/barrelo.service <<'EOF'
[Unit]
Description=Barrelo dart platform
After=network.target

[Service]
Type=simple
WorkingDirectory=/opt/barrelo
ExecStart=/opt/barrelo/Barrelo.Api --urls=http://0.0.0.0:5295
Restart=always
RestartSec=5
Environment=ASPNETCORE_ENVIRONMENT=Production

[Install]
WantedBy=multi-user.target
EOF
SERVICES="barrelo"

if [ "$WITH_SIMULATOR" -eq 1 ]; then
    [ -f "$INSTALL_DIR/tools/BoardSimulator/Barrelo.BoardSimulator" ] || fail "this release has no Board Simulator"
    cat > /etc/systemd/system/barrelo-boardsimulator.service <<'EOF'
[Unit]
Description=Barrelo Board Simulator
After=network.target

[Service]
Type=simple
WorkingDirectory=/opt/barrelo/tools/BoardSimulator
ExecStart=/opt/barrelo/tools/BoardSimulator/Barrelo.BoardSimulator --urls=http://0.0.0.0:5250
Restart=always
RestartSec=5
Environment=ASPNETCORE_ENVIRONMENT=Production

[Install]
WantedBy=multi-user.target
EOF
    SERVICES="$SERVICES barrelo-boardsimulator"
fi

systemctl daemon-reload
# shellcheck disable=SC2086
systemctl enable --now $SERVICES

sleep 3
# shellcheck disable=SC2086
for svc in $SERVICES; do
    systemctl is-active --quiet "$svc" || fail "$svc did not start - see: journalctl -u $svc -n 50 --no-pager"
done

IP="$(hostname -I 2>/dev/null | awk '{print $1}')"
IP="${IP:-localhost}"
log "Barrelo $VERSION is running"
echo "    Open http://$IP:5295"
[ "$WITH_SIMULATOR" -eq 1 ] && echo "    Board Simulator: http://$IP:5250"
echo "    Logs:    journalctl -u barrelo -f"
echo "    Upgrade: re-run this installer.  Remove: re-run with --uninstall"
