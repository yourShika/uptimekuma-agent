#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT="${ROOT_DIR}/src/UptimeKumaAgent.Linux/UptimeKumaAgent.Linux.csproj"
VERSION="1.1.2"
RIDS=("linux-x64" "linux-arm64")

for rid in "${RIDS[@]}"; do
  publish_dir="${ROOT_DIR}/build/${rid}"
  package_dir="${ROOT_DIR}/build/package/uptime-kuma-agent-${VERSION}-${rid}"
  tar_path="${ROOT_DIR}/build/uptime-kuma-agent-${VERSION}-${rid}.tar.gz"

  echo "Publishing ${rid}..."
  dotnet publish "${PROJECT}" \
    -c Release \
    -r "${rid}" \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -p:EnableCompressionInSingleFile=true \
    -o "${publish_dir}"

  rm -rf "${package_dir}"
  install -d "${package_dir}/linux/systemd"
  install -m 0755 "${publish_dir}/uptime-kuma-agent" "${package_dir}/uptime-kuma-agent"
  install -m 0755 "${ROOT_DIR}/InstallLinux.sh" "${package_dir}/InstallLinux.sh"
  install -m 0755 "${ROOT_DIR}/UninstallLinux.sh" "${package_dir}/UninstallLinux.sh"
  install -m 0644 "${ROOT_DIR}/config.example.json" "${package_dir}/config.example.json"
  install -m 0644 "${ROOT_DIR}/config.linux.example.json" "${package_dir}/config.linux.example.json"
  install -m 0644 "${ROOT_DIR}/linux/systemd/uptime-kuma-agent.service" "${package_dir}/linux/systemd/uptime-kuma-agent.service"
  install -m 0644 "${ROOT_DIR}/README.md" "${package_dir}/README.md"

  tar -C "$(dirname "${package_dir}")" -czf "${tar_path}" "$(basename "${package_dir}")"
  echo "Created ${tar_path}"
done
