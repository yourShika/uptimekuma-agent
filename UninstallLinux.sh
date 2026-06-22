#!/usr/bin/env bash
set -euo pipefail

APP_DIR="/opt/uptime-kuma-agent"
CONFIG_DIR="/etc/uptime-kuma-agent"
DATA_DIR="/var/lib/uptime-kuma-agent"
LOG_DIR="/var/log/uptime-kuma-agent"
SERVICE_PATH="/etc/systemd/system/uptime-kuma-agent.service"
DELETE_DATA="false"

for arg in "$@"; do
  case "${arg}" in
    --delete-data|-DeleteData)
      DELETE_DATA="true"
      ;;
    *)
      echo "Unbekanntes Argument: ${arg}" >&2
      exit 2
      ;;
  esac
done

if [[ "${EUID}" -ne 0 ]]; then
  echo "UninstallLinux.sh muss als root ausgefuehrt werden. Nutze sudo." >&2
  exit 1
fi

systemctl stop uptime-kuma-agent.service 2>/dev/null || true
systemctl disable uptime-kuma-agent.service 2>/dev/null || true
rm -f "${SERVICE_PATH}"
systemctl daemon-reload
rm -rf "${APP_DIR}"

if [[ "${DELETE_DATA}" == "true" ]]; then
  rm -rf "${CONFIG_DIR}" "${DATA_DIR}" "${LOG_DIR}"
  echo "Uptime Kuma Agent, Konfiguration, Daten und Logs wurden entfernt."
else
  echo "Uptime Kuma Agent wurde entfernt. Konfiguration und Logs bleiben erhalten."
  echo "Zum Loeschen aller Daten: sudo ./UninstallLinux.sh --delete-data"
fi
