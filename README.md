# Uptime Kuma Tray Agent

Native Windows tray agent und Headless-Linux-Agent für Uptime-Kuma-Push-Monitore. Die Anwendung überwacht Ping-Ziele, TCP-Ports, lokale Dienste sowie lokale und verbundene Laufwerke/Mountpoints und sendet Ergebnisse als `status`, `msg` und optional `ping` an je Check konfigurierbare Push-URLs.

Die GUI besitzt ein Uptime-Kuma-inspiriertes Branding, ein eigenes Fenster-/Tray-Icon sowie Light- und Dark-Modus.

## Voraussetzungen

- Zielrechner Windows: Windows x64. Bei self-contained Veröffentlichung ist keine separate .NET-Runtime-Installation nötig.
- Zielrechner Linux: systemd-basierte Headless-Server ohne Desktop-Umgebung. Unterstützt werden self-contained Builds für `linux-x64` und `linux-arm64`.
- Build-Rechner: .NET SDK 8 oder neuer mit Windows Desktop Workload.
- Keine Python-, Node.js-, Docker-, NSSM- oder Drittanbieter-Tool-Abhängigkeiten.
- Die WPF-UI-Abhängigkeit `WPF-UI` ist für die moderne Oberfläche im Projekt vorbereitet.

## Windows Build

```cmd
Build.cmd
```

## Self-contained EXE erstellen

```cmd
Publish.cmd
```

Die fertige EXE liegt danach unter:

```text
build\win-x64\UptimeKumaTrayAgent.exe
```

## Setup-Installer erstellen

Der empfohlene Windows-Installer ist das MSI mit Lizenzdialog und Zielordnerauswahl:

```cmd
BuildMsi.cmd
```

Beim ersten MSI-Build installiert das Skript das WiX Toolset automatisch lokal nach `tools\wix`, falls es dort noch nicht vorhanden ist.

Die fertige Datei liegt danach unter:

```text
build\installer\UptimeKumaTrayAgent-Setup-1.0.8-x64.msi
build\installer\UptimeKumaTrayAgent-Setup-1.0.8-x86.msi
```

Der ältere selbstextrahierende EXE-Installer kann weiterhin gebaut werden:

```cmd
BuildInstaller.cmd
```

Die fertige Datei liegt danach unter:

```text
build\installer\UptimeKumaTrayAgent-Setup-1.0.8.exe
```

Die Installer können für Neuinstallation und Updates verwendet werden. Bei Updates werden Dienst und Programmdateien aktualisiert, die Konfiguration und Logs unter `%ProgramData%\UptimeKumaTrayAgent` bleiben erhalten. Falls aus Version 1.0.3 noch eine Benutzer-Konfiguration unter `%AppData%\UptimeKumaTrayAgent` vorhanden ist und ProgramData leer ist oder nur eine Factory-Default-Konfiguration enthält, wird diese alte Konfiguration automatisch übernommen. Zusätzlich werden Startmenü-Einträge `UptimeKumaAgent` und `UptimeKumaAgent Konfiguration` angelegt, damit Windows-Suche Agent und Konfigurationsdatei findet.

Für normale Server ist `x64` empfohlen. `x86` ist das 32-bit-Paket für ältere oder entsprechend eingeschränkte Systeme. Für Releases kann zusätzlich eine Kopie mit `x32` im Dateinamen bereitgestellt werden; inhaltlich entspricht sie dem x86-Paket.

## Updates über GitHub Releases

Ab Version 1.0.8 kann der Agent GitHub Releases von `yourShika/uptimekuma-agent` prüfen. In der Windows-GUI gibt es dafür `Check for Updates` und `Update`. `Check for Updates` sucht eine neuere Release-Version und ein passendes MSI für die aktuelle Architektur. `Update` lädt das MSI in einen temporären Ordner und startet den Installer mit Administratorabfrage. Push-URLs und Secrets werden dabei nicht geloggt.

Unter Linux stehen die Headless-Befehle zur Verfügung:

```bash
/opt/uptime-kuma-agent/uptime-kuma-agent --check-updates
sudo /opt/uptime-kuma-agent/uptime-kuma-agent --update
```

`--update` lädt das passende `.tar.gz` aus dem GitHub Release, ersetzt die Binary unter `/opt/uptime-kuma-agent` und startet `uptime-kuma-agent.service` über `systemctl restart` ohne Shell-String-Konkatenation neu. Für die Installation nach `/opt` und den Dienstneustart sind in der Regel Root- oder sudo-Rechte nötig. Falls kein GitHub Release oder kein passendes Paket vorhanden ist, bleibt die installierte Version unverändert.

## Linux Headless Build

Linux wird als headless/systemd-Agent gebaut, ohne WPF, WinForms, X11, Wayland, GNOME oder KDE.

```bash
./PublishLinux.sh
```

Das Skript erstellt self-contained Builds und generische Archive:

```text
build/uptime-kuma-agent-1.0.8-linux-x64.tar.gz
build/uptime-kuma-agent-1.0.8-linux-arm64.tar.gz
```

Optional kann `BuildLinuxPackages.sh` verwendet werden. Es erzeugt die `.tar.gz`-Pakete immer, bereitet `.deb` vor, wenn `dpkg-deb` vorhanden ist, und überspringt `.rpm`, wenn `rpmbuild` fehlt.

## Linux Installation

Ubuntu/Debian:

```bash
tar -xzf uptime-kuma-agent-1.0.8-linux-x64.tar.gz
cd uptime-kuma-agent-1.0.8-linux-x64
sudo ./InstallLinux.sh
```

Red Hat, Fedora, Rocky und AlmaLinux verwenden das gleiche generische `.tar.gz`-Paket. Arch und andere Distributionen können ebenfalls das Archiv nutzen, solange systemd verfügbar ist.

Standardpfade unter Linux:

```text
/etc/uptime-kuma-agent/config.json
/var/lib/uptime-kuma-agent
/var/log/uptime-kuma-agent
/opt/uptime-kuma-agent
```

Der systemd-Dienst heißt:

```text
uptime-kuma-agent.service
```

Nützliche Befehle:

```bash
sudo systemctl status uptime-kuma-agent.service
sudo systemctl restart uptime-kuma-agent.service
journalctl -u uptime-kuma-agent.service -f
```

Manuelle Ausführung:

```bash
/opt/uptime-kuma-agent/uptime-kuma-agent --help
/opt/uptime-kuma-agent/uptime-kuma-agent --version
/opt/uptime-kuma-agent/uptime-kuma-agent --config /etc/uptime-kuma-agent/config.json --test-config
/opt/uptime-kuma-agent/uptime-kuma-agent --config /etc/uptime-kuma-agent/config.json --once
/opt/uptime-kuma-agent/uptime-kuma-agent --check-updates
sudo /opt/uptime-kuma-agent/uptime-kuma-agent --update
/opt/uptime-kuma-agent/uptime-kuma-agent --config /etc/uptime-kuma-agent/config.json --service
```

Linux-Serviceaktionen nutzen `systemctl` ohne Shell-String-Konkatenation. Dienstnamen werden sicher normalisiert, zum Beispiel `nginx` zu `nginx.service`; `nginx.service`, `docker`, `docker.service`, `ssh` und `sshd` sind ebenfalls möglich. Start, Stop und Restart benötigen in der Regel Root- oder passende sudo/systemd-Rechte.

Linux-Laufwerkschecks prüfen Mountpoints wie `/`, `/home`, `/mnt/share` und `/media/storage` mit `System.IO.DriveInfo`. Lokale Mounts, CIFS und NFS werden unterstützt, soweit das Betriebssystem sie als Mountpoints meldet. Reconnect ist bewusst vorsichtig: Es wird nur `mount <mountpoint>` ausgeführt, wenn der Mountpoint in `/etc/fstab` existiert. UNC-Pfade und `net use` werden unter Linux nicht verwendet.

Bekannte Einschränkungen unter Linux:

- Keine GUI und kein Tray.
- Keine Windows-UNC-Reconnects.
- Kein freies Mounten mit Benutzereingaben.
- Erweiterte TCP-Verbindungsdetails sind optional und werden übersprungen, wenn `ss` fehlt.
- ICMP-Ping kann Rechte benötigen; falls .NET-ICMP scheitert, versucht der Agent das Linux-`ping`-Kommando.

## Installation als Windows-Dienst

Der Agent kann als echter Windows-Dienst installiert werden. Dadurch startet er beim Systemstart automatisch, auch wenn noch kein Benutzer angemeldet ist.

```cmd
Install.cmd
```

Oder komfortabel über den Setup-Installer:

```text
UptimeKumaTrayAgent-Setup-1.0.8-x64.msi
```

Wichtig: `Install.cmd` muss als Administrator gestartet werden. Der Setup-Installer fragt bei Bedarf automatisch nach Administratorrechten. Der Dienst wird als `LocalSystem` mit Starttyp `Automatisch` eingerichtet und erscheint in `services.msc` als:

```text
UptimeKumaAgent
```

Der Dienst startet das Monitoring automatisch. Die GUI bleibt weiterhin als Konfigurationsoberfläche nutzbar; gespeicherte Änderungen an der JSON-Konfiguration werden vom Dienst automatisch nachgeladen.

## Start

1. Ordner entpacken.
2. `UptimeKumaTrayAgent.exe` starten.
3. Ping-, TCP- und Dienst-Checks in der GUI konfigurieren.
4. Speichern.
5. Für Serverbetrieb `Install.cmd` als Administrator ausführen.

Beim Schließen bleibt die Anwendung standardmäßig im Tray aktiv. Dieses Verhalten kann unter "Globale Einstellungen" deaktiviert werden.

Die Darstellung kann in der GUI unter "Globale Einstellungen" zwischen `Light` und `Dark` umgeschaltet werden.

Die Sprache steht standardmäßig auf `System` und folgt damit der Windows-Anzeigesprache. Unterstützt sind aktuell Deutsch, English und Polski; alternativ kann die Sprache in den globalen Einstellungen fest gewählt werden. Nach einer Sprachänderung muss die App neu gestartet werden, damit alle Tabellen, Dialoge und Beschriftungen vollständig in der neuen Sprache geladen werden.

## Fehleraktionen

Ping-, TCP- und Windows-Dienst-Checks können mehrere lokale Windows-Dienste als Fehleraktion hinterlegen. Wenn der Check fehlschlägt, versucht der Agent diese Dienste neu zu starten und meldet das Ergebnis in Log und Uptime-Kuma-Nachricht. Ein Cooldown verhindert, dass ein dauerhaft fehlerhafter Check die Dienste in sehr kurzen Abständen immer wieder neu startet.

Optional kann bei einem Stop-Timeout der zugehörige Dienstprozess erzwungen beendet werden. Das entspricht dem manuellen `taskkill`-Fall, ist aber nur aktiv, wenn die Option im Check gesetzt ist oder in der Dienste-Ansicht der Button "Dienst hart neu starten" verwendet wird.

Ab Version 1.0.8 können Dienst-Neustarts verzögert werden. Es gibt Wartezeiten in Minuten nach Systemstart und nach Fehlererkennung. Wenn ein Dienst während dieser Zeit von selbst wieder läuft, greift der Agent nicht ein. Das ist nützlich, wenn abhängige Dienste nach einem Reboot noch starten oder ein Dienst kurz crasht und sich selbst erholt.

TCP-Checks können zusätzlich passende ein- und ausgehende TCP-Verbindungen protokollieren. Das Ergebnis wird nur ins Log geschrieben und nicht an Uptime Kuma gesendet. Pro Verbindung werden Richtung, lokale/remoteseitige IP und Ports, TCP-Status, PID und Prozessname gespeichert.

## Laufwerks-Checks

Laufwerks-Checks prüfen lokale Laufwerke, gemappte Netzlaufwerke und UNC-Pfade. Optional können Mindestwerte für freien Speicher in Prozent und GB gesetzt werden. Bei Netzlaufwerken kann der Agent versuchen, das Laufwerk nach einem Abbruch wieder zu verbinden; dafür nutzt er vorhandene Windows-Anmeldedaten beziehungsweise den konfigurierten UNC-Pfad. Details wie Typ, Format, Gesamtgröße, freier Speicher und Reconnect-Ergebnis werden ins Log geschrieben.

## Monitoring aktiv/inaktiv

Der Windows-Dienst respektiert `MonitoringAutoStart`. Wenn Monitoring in der GUI gestoppt oder die Option deaktiviert und gespeichert wird, stoppt auch der Dienst nach dem automatischen Config-Reload seine Checks und Push-Meldungen. Die GUI startet kein zweites lokales Monitoring, wenn der Windows-Dienst bereits läuft.

## Konfiguration

Die Konfiguration wird als JSON gespeichert:

```text
%ProgramData%\UptimeKumaTrayAgent\config.json
```

Falls dort keine Schreibrechte vorhanden sind, nutzt der Agent:

```text
%AppData%\UptimeKumaTrayAgent\config.json
```

Eine Beispielkonfiguration liegt in `config.example.json`.

Ab Version 1.0.6 wird die bestehende Konfiguration bei Updates grundsätzlich nicht überschrieben. Alte AppData-Konfigurationen aus früheren Versionen werden beim Start und beim Installer-Lauf nach ProgramData migriert, wenn dort noch keine echte Benutzer-Konfiguration existiert. Deaktivierte Checks und deaktiviertes Monitoring bleiben dadurch auch nach Neustart, Dienst-Reload und Update deaktiviert.

## GitHub Pages

Die Projektseite liegt im Ordner `docs` und ist für diese URL vorbereitet:

```text
https://yourShika.github.io/uptimekuma-agent/
```

Der Workflow `.github/workflows/pages.yml` veröffentlicht den `docs`-Ordner über GitHub Pages, sobald das Repository auf GitHub unter `yourShika/uptimekuma-agent` liegt und Pages aktiviert ist.

Release-Notizen liegen unter `releases`. Die fertigen MSI-Artefakte werden nicht ins Repository eingecheckt, sondern im GitHub-Release als Dateien hochgeladen.

## Uptime-Kuma-Push-URL

Pro Check kann eine eigene Push-URL gesetzt werden, zum Beispiel:

```text
https://kuma.example.com/api/push/abc123
```

Der Agent ergänzt automatisch URL-kodierte Parameter:

```text
status=up&msg=Ping%20OK&ping=12
```

Wenn die Push-URL bereits Query-Parameter enthält, wird korrekt mit `&` erweitert.

## Autostart

Der empfohlene Serverbetrieb ist die Installation als Windows-Dienst über `Install.cmd`.

Der alte GUI-Autostart nutzt weiterhin den Benutzer-Registry-Zweig:

```text
HKCU\Software\Microsoft\Windows\CurrentVersion\Run
```

Dafür sind normalerweise keine Adminrechte erforderlich, aber er startet erst nach Benutzeranmeldung. Für Starts vor Benutzeranmeldung den Windows-Dienst verwenden.

## Windows-Dienste und Adminrechte

Das Lesen lokaler Dienste funktioniert in der Regel ohne Adminrechte. Starten, Stoppen oder Neustarten einzelner Dienste kann Adminrechte erfordern. Wenn Rechte fehlen, zeigt die GUI den Fehler an und schreibt ihn ins Log.

## Logging

Logs liegen unter:

```text
%ProgramData%\UptimeKumaTrayAgent\Logs
```

oder bei Fallback unter:

```text
%AppData%\UptimeKumaTrayAgent\Logs
```

Push-URLs werden im Log maskiert. Alte Logs werden nach 30 Tagen gelöscht.

## Deinstallation

Nach Installation als Windows-Dienst erscheint der Agent in den Windows-Systemeinstellungen unter installierten Apps als Software von `Kamil Bura`. Die Deinstallation entfernt Dienst, Programmordner und den Eintrag in den Windows-Systemeinstellungen.

Alternativ:

```cmd
Uninstall.cmd
```

Konfiguration und Logs bleiben standardmäßig erhalten. Mit folgendem Aufruf werden auch erzeugte Daten entfernt:

```cmd
Uninstall.cmd -DeleteData
```

Im Tray-Menü gibt es zusätzlich eine einfache Deinstallation für den portablen Betrieb.

## Troubleshooting

- Keine Pushs in Uptime Kuma: Push-URL prüfen und einen Testlauf mit Push senden ausführen.
- HTTP 401/403/404: Push-URL oder Monitor-ID in Uptime Kuma prüfen.
- SSL-/TLS-Fehler: Zertifikat und Erreichbarkeit der Uptime-Kuma-Instanz prüfen.
- Dienste können nicht gestartet werden: Anwendung mit ausreichenden Rechten starten.
- Konfiguration wird nicht in ProgramData gespeichert: Der Agent nutzt automatisch den AppData-Fallback.
