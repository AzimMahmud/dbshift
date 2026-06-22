#!/usr/bin/env bash
# DbShift – official install script
# Usage: curl -fsSL https://github.com/AzimMahmud/dbshift/releases/latest/download/install.sh | bash
#   or:  bash <(curl -fsSL https://github.com/AzimMahmud/dbshift/releases/latest/download/install.sh)

set -euo pipefail

REPO="${REPO:-AzimMahmud/dbshift}"
VERSION="${VERSION:-latest}"
INSTALL_DIR="${INSTALL_DIR:-/usr/local/bin}"

# ── helpers ──────────────────────────────────────────────────────────────────
info()  { printf "  \033[36m>\033[0m %s\n" "$*"; }
ok()    { printf "  \033[32m✓\033[0m %s\n" "$*"; }
warn()  { printf "  \033[33m⚠\033[0m %s\n" "$*"; }
err()   { printf "  \033[31m✗\033[0m %s\n" "$*"; exit 1; }

# ── platform detection ───────────────────────────────────────────────────────
detect_platform() {
    local os arch suffix

    case "$(uname -s)" in
        Linux)  os="linux";;
        Darwin) os="macos";;
        *)      err "Unsupported OS: $(uname -s)";;
    esac

    case "$(uname -m)" in
        x86_64|amd64) arch="x64";;
        aarch64|arm64) arch="arm64";;
        *)            err "Unsupported architecture: $(uname -m)";;
    esac

    # macOS arm64 runs x64 via Rosetta – ship arm64 builds separately
    # We default to x64 for now; override with ARCH env var
    arch="${ARCH:-$arch}"
    echo "${os}-${arch}"
}

# ── download ──────────────────────────────────────────────────────────────────
download_release() {
    local platform="$1"
    local url

    if [ "$VERSION" = "latest" ]; then
        url="https://github.com/${REPO}/releases/latest/download/dbshift-${platform}.tar.gz"
    else
        url="https://github.com/${REPO}/releases/download/v${VERSION}/dbshift-${platform}.tar.gz"
    fi

    info "Downloading dbshift for ${platform}..."
    curl -fsSL "$url" -o /tmp/dbshift.tar.gz || err "Download failed: $url"
    ok "Downloaded (${platform})"

    info "Extracting..."
    tar -xzf /tmp/dbshift.tar.gz -C /tmp/ || err "Extraction failed"
    rm -f /tmp/dbshift.tar.gz
}

# ── install ───────────────────────────────────────────────────────────────────
install_binary() {
    if [ ! -d "$INSTALL_DIR" ]; then
        mkdir -p "$INSTALL_DIR"
    fi

    mv /tmp/dbshift "$INSTALL_DIR/dbshift" || err "Failed to install binary"
    chmod +x "$INSTALL_DIR/dbshift"
    ok "Installed to ${INSTALL_DIR}/dbshift"
}

# ── verify ────────────────────────────────────────────────────────────────────
verify() {
    if command -v dbshift &>/dev/null; then
        ok "$(dbshift --version 2>&1 | head -1)"
    elif [ -x "$INSTALL_DIR/dbshift" ]; then
        ok "$("$INSTALL_DIR/dbshift" --version 2>&1 | head -1)"
    else
        warn "dbshift installed but not found on PATH"
    fi
}

# ── main ──────────────────────────────────────────────────────────────────────
main() {
    echo ""
    echo "  ╭──────────────────────────────────────╮"
    echo "  │  DbShift — database migration tool   │"
    echo "  ╰──────────────────────────────────────╯"
    echo ""

    local platform
    platform="$(detect_platform)"
    info "Detected: ${platform}"

    download_release "$platform"
    install_binary

    echo ""
    verify
    echo ""
    info "Run 'dbshift --help' to get started."
    echo ""
}

main "$@"
