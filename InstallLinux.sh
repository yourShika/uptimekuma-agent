#!/usr/bin/env bash
set -euo pipefail

APP_DIR="/opt/uptime-kuma-agent"
CONFIG_DIR="/etc/uptime-kuma-agent"
DATA_DIR="/var/lib/uptime-kuma-agent"
LOG_DIR="/var/log/uptime-kuma-agent"
SERVICE_PATH="/etc/systemd/system/uptime-kuma-agent.service"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

if [[ "${EUID}" -ne 0 ]]; then
  echo "InstallLinux.sh muss als root ausgefuehrt werden. Nutze sudo." >&2
  exit 1
fi

if [[ ! -f "${SCRIPT_DIR}/uptime-kuma-agent" ]]; then
  echo "Binary ${SCRIPT_DIR}/uptime-kuma-agent wurde nicht gefunden. Bitte zuerst PublishLinux.sh ausfuehren oder ein Release-Paket entpacken." >&2
  exit 1
fi

install -d -m 0755 "${APP_DIR}" "${CONFIG_DIR}" "${DATA_DIR}" "${LOG_DIR}"
install -m 0755 "${SCRIPT_DIR}/uptime-kuma-agent" "${APP_DIR}/uptime-kuma-agent"

if [[ -d "${SCRIPT_DIR}/linux" ]]; then
  cp -R "${SCRIPT_DIR}/linux" "${APP_DIR}/"
fi

if [[ -f "${SCRIPT_DIR}/config.linux.example.json" && ! -f "${CONFIG_DIR}/config.json" ]]; then
  install -m 0644 "${SCRIPT_DIR}/config.linux.example.json" "${CONFIG_DIR}/config.json"
elif [[ -f "${SCRIPT_DIR}/config.example.json" && ! -f "${CONFIG_DIR}/config.json" ]]; then
  install -m 0644 "${SCRIPT_DIR}/config.example.json" "${CONFIG_DIR}/config.json"
fi

if [[ -f "${SCRIPT_DIR}/linux/systemd/uptime-kuma-agent.service" ]]; then
  install -m 0644 "${SCRIPT_DIR}/linux/systemd/uptime-kuma-agent.service" "${SERVICE_PATH}"
else
  install -m 0644 "${SCRIPT_DIR}/uptime-kuma-agent.service" "${SERVICE_PATH}"
fi

systemctl daemon-reload
systemctl enable uptime-kuma-agent.service
systemctl restart uptime-kuma-agent.service

echo "Uptime Kuma Agent wurde installiert und gestartet."
echo "Config: ${CONFIG_DIR}/config.json"
echo "Logs:   ${LOG_DIR}"
