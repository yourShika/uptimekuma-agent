#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
VERSION="1.1.2"

"${ROOT_DIR}/PublishLinux.sh"

if command -v dpkg-deb >/dev/null 2>&1; then
  for rid in linux-x64 linux-arm64; do
    pkg_root="${ROOT_DIR}/build/deb/uptime-kuma-agent-${rid}"
    rm -rf "${pkg_root}"
    install -d "${pkg_root}/DEBIAN" "${pkg_root}/opt/uptime-kuma-agent" "${pkg_root}/etc/systemd/system" "${pkg_root}/etc/uptime-kuma-agent" "${pkg_root}/var/lib/uptime-kuma-agent" "${pkg_root}/var/log/uptime-kuma-agent"
    install -m 0755 "${ROOT_DIR}/build/${rid}/uptime-kuma-agent" "${pkg_root}/opt/uptime-kuma-agent/uptime-kuma-agent"
    install -m 0644 "${ROOT_DIR}/linux/systemd/uptime-kuma-agent.service" "${pkg_root}/etc/systemd/system/uptime-kuma-agent.service"
    install -m 0644 "${ROOT_DIR}/config.linux.example.json" "${pkg_root}/etc/uptime-kuma-agent/config.json"
    arch="amd64"
    [[ "${rid}" == "linux-arm64" ]] && arch="arm64"
    cat > "${pkg_root}/DEBIAN/control" <<EOF
Package: uptime-kuma-agent
Version: ${VERSION}
Section: admin
Priority: optional
Architecture: ${arch}
Maintainer: Kamil Bura
Description: Headless Uptime Kuma monitoring agent
EOF
    dpkg-deb --build "${pkg_root}" "${ROOT_DIR}/build/uptime-kuma-agent_${VERSION}_${arch}.deb"
  done
else
  echo "dpkg-deb nicht gefunden; .deb wird uebersprungen."
fi

if command -v rpmbuild >/dev/null 2>&1; then
  echo "rpmbuild ist vorhanden. RPM-Spec-Vorbereitung kann auf Basis der Tarballs erfolgen."
else
  echo "rpmbuild nicht gefunden; .rpm wird uebersprungen."
fi
