using System.Globalization;
using UptimeKumaTrayAgent.Models;

namespace UptimeKumaTrayAgent.Utils;

public static class I18n
{
    private static readonly string WindowsLanguage = ResolveSupportedLanguage(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);

    private static readonly IReadOnlyDictionary<string, string> English = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Systemsprache"] = "System language",
        ["English"] = "English",
        ["Deutsch"] = "German",
        ["Polski"] = "Polish",
        ["Sprache"] = "Language",
        ["Name"] = "Name",
        ["Status"] = "Status",
        ["Host"] = "Host",
        ["Port"] = "Port",
        ["Timeout"] = "Timeout",
        ["HTTP"] = "HTTP",
        ["Push-URL"] = "Push URL",
        ["TCP-Log"] = "TCP log",
        ["Reconnect"] = "Reconnect",
        ["Agent"] = "Agent",
        ["Monitoring"] = "Monitoring",
        ["Version"] = "Version",
        ["Watchdog"] = "Watchdog",
        ["Ping-Checks"] = "Ping checks",
        ["TCP-Checks"] = "TCP checks",
        ["Logs"] = "Logs",
        ["Ping"] = "Ping",
        ["TCP"] = "TCP",
        ["Dienst"] = "Service",
        ["Laufwerk"] = "Drive",
        ["Fenster öffnen"] = "Open window",
        ["Monitoring starten"] = "Start monitoring",
        ["Monitoring stoppen"] = "Stop monitoring",
        ["Status anzeigen"] = "Show status",
        ["Konfiguration öffnen"] = "Open configuration",
        ["Logs öffnen"] = "Open logs",
        ["Deinstallieren"] = "Uninstall",
        ["Deinstallation"] = "Uninstall",
        ["Beenden"] = "Exit",
        ["Agent-Status"] = "Agent status",
        ["Letzter Lauf"] = "Last run",
        ["Letzter Fehler"] = "Last error",
        ["Computername"] = "Computer name",
        ["Maschineninfos"] = "Machine info",
        ["Speichern"] = "Save",
        ["Testlauf ausführen"] = "Run test",
        ["Globale Einstellungen"] = "Global settings",
        ["Standard-Intervall (s)"] = "Default interval (s)",
        ["HTTP-Timeout (ms)"] = "HTTP timeout (ms)",
        ["TCP-Timeout (ms)"] = "TCP timeout (ms)",
        ["Ping-Timeout (ms)"] = "Ping timeout (ms)",
        ["Log-Level"] = "Log level",
        ["Darstellung"] = "Appearance",
        ["Push-URLs maskieren"] = "Mask push URLs",
        ["Autostart aktivieren"] = "Enable autostart",
        ["Autostart deaktivieren"] = "Disable autostart",
        ["Start minimiert"] = "Start minimized",
        ["Beim Schließen in Tray"] = "Close to tray",
        ["Maschineninfos mitsenden"] = "Send machine info",
        ["Monitoring automatisch starten"] = "Start monitoring automatically",
        ["Watchdog aktiv"] = "Watchdog active",
        ["Watchdog-Push-URL"] = "Watchdog push URL",
        ["Intervall (s)"] = "Interval (s)",
        ["Max. ohne Erfolg (s)"] = "Max. without success (s)",
        ["Allgemein"] = "General",
        ["Windows-Dienste"] = "Windows services",
        ["Laufwerke"] = "Drives",
        ["Lokale Hosts, TCP-Ports und Windows-Dienste im Blick behalten"] = "Keep an eye on local hosts, TCP ports and Windows services",
        ["Bereit"] = "Ready",
        ["Typ"] = "Type",
        ["Ziel"] = "Target",
        ["Aktiv"] = "Active",
        ["Antwortzeit"] = "Response time",
        ["Letzter Check"] = "Last check",
        ["Letzter OK"] = "Last OK",
        ["Nächste Ausführung"] = "Next run",
        ["Letzter Push"] = "Last push",
        ["Intervall"] = "Interval",
        ["Fehleraktion"] = "Failure action",
        ["Notiz"] = "Note",
        ["Anzeigename"] = "Display name",
        ["Dienstname"] = "Service name",
        ["Erwartet"] = "Expected",
        ["Auto-Neustart"] = "Auto restart",
        ["Lokaler Dienst"] = "Local service",
        ["Stoppbar"] = "Can stop",
        ["Pfad"] = "Path",
        ["Min. frei %"] = "Min. free %",
        ["Min. frei GB"] = "Min. free GB",
        ["Logdetails"] = "Log details",
        ["Hinzufügen"] = "Add",
        ["Aus Liste hinzufügen"] = "Add from list",
        ["Bearbeiten"] = "Edit",
        ["Löschen"] = "Delete",
        ["Testen"] = "Test",
        ["Dienst starten"] = "Start service",
        ["Dienst stoppen"] = "Stop service",
        ["Dienst neu starten"] = "Restart service",
        ["Dienst hart neu starten"] = "Force restart service",
        ["Lokale Dienste laden"] = "Load local services",
        ["Aktualisieren"] = "Refresh",
        ["Konfiguration prüfen"] = "Check configuration",
        ["Konfiguration gespeichert"] = "Configuration saved",
        ["Konfiguration gespeichert."] = "Configuration saved.",
        ["Sprache geändert"] = "Language changed",
        ["Die Sprache wurde gespeichert. Um die Sprache vollständig zu ändern, muss die App neu gestartet werden."] = "The language has been saved. To fully change the language, the app must be restarted.",
        ["Konfiguration konnte nicht gespeichert werden: "] = "Configuration could not be saved: ",
        ["Fehler"] = "Error",
        ["Warnung"] = "Warning",
        ["Deaktiviert"] = "Disabled",
        ["Unbekannt"] = "Unknown",
        ["Läuft"] = "Running",
        ["Gestoppt"] = "Stopped",
        ["Pausiert"] = "Paused",
        ["Startet"] = "Starting",
        ["Stoppt"] = "Stopping",
        ["Wird fortgesetzt"] = "Continuing",
        ["Pausiert gerade"] = "Pausing",
        ["Hell"] = "Light",
        ["Dunkel"] = "Dark",
        ["Debug"] = "Debug",
        ["Ein- und ausgehend"] = "Incoming and outgoing",
        ["Eingehend"] = "Incoming",
        ["Ausgehend"] = "Outgoing",
        ["aktiv"] = "active",
        ["inaktiv"] = "inactive",
        ["Monitoring aktiv"] = "Monitoring active",
        ["Monitoring inaktiv"] = "Monitoring inactive",
        ["Monitoring im Windows-Dienst aktiviert"] = "Monitoring enabled in Windows service",
        ["Monitoring deaktiviert"] = "Monitoring disabled",
        ["Testlauf läuft..."] = "Test is running...",
        ["Testlauf abgeschlossen."] = "Test completed.",
        ["Testlauf"] = "Test run",
        ["Testlauf fehlgeschlagen"] = "Test run failed",
        ["Soll der Test auch an Uptime Kuma gesendet werden?\r\n\r\nJa = Push senden\r\nNein = nur lokal testen"] = "Should the test also be sent to Uptime Kuma?\r\n\r\nYes = send push\r\nNo = local test only",
        ["Bitte zuerst einen lokalen Dienst auswählen."] = "Please select a local service first.",
        ["Dienst auswählen"] = "Select service",
        ["Check wurde nicht gefunden."] = "Check was not found.",
        ["Testergebnis"] = "Test result",
        ["Lokale Dienste werden geladen..."] = "Loading local services...",
        ["Lokale Dienste konnten nicht geladen werden"] = "Local services could not be loaded",
        ["lokale Dienste geladen"] = "local services loaded",
        ["Dienste laden"] = "Load services",
        ["Dienste konnten nicht geladen werden"] = "Services could not be loaded",
        ["Bitte zuerst einen Dienst-Check oder lokalen Dienst auswählen."] = "Please select a service check or local service first.",
        ["Dienstaktion"] = "Service action",
        ["Dienst {0} hart neu starten?\r\n\r\nWenn der Dienst beim Stoppen hängen bleibt, wird der zugehörige Prozess erzwungen beendet."] = "Force restart service {0}?\r\n\r\nIf the service gets stuck while stopping, the related process will be forcefully terminated.",
        ["Unbekannte Aktion"] = "Unknown action",
        ["Lokale Dienste konnten nicht aufgelistet werden"] = "Local services could not be listed",
        ["Dienst existiert nicht"] = "Service does not exist",
        ["Dienstname fehlt"] = "Service name is missing",
        ["Dienststatus gelesen"] = "Service status read",
        ["Dienstprüfung fehlgeschlagen"] = "Service check failed",
        ["Dienst läuft bereits"] = "Service is already running",
        ["Dienst gestartet"] = "Service started",
        ["Timeout beim Starten des Dienstes"] = "Timeout while starting the service",
        ["Dienst konnte nicht gestartet werden"] = "Service could not be started",
        ["Dienst ist bereits gestoppt"] = "Service is already stopped",
        ["Dienst konnte nicht gestoppt werden"] = "Service could not be stopped",
        ["Dienst akzeptiert kein Stop-Signal"] = "Service does not accept a stop signal",
        ["Dienst gestoppt"] = "Service stopped",
        ["Timeout beim Stoppen des Dienstes"] = "Timeout while stopping the service",
        ["Dienstprozess {0} wurde erzwungen beendet"] = "Service process {0} was forcefully terminated",
        ["Dienstprozess beendet, Status unklar"] = "Service process terminated, status unclear",
        ["keine Rechte zum Lesen des Dienstes"] = "No permission to read the service",
        ["Service Control Manager konnte nicht geöffnet werden"] = "Service Control Manager could not be opened",
        ["keine Rechte zum Starten des Dienstes"] = "No permission to start the service",
        ["Dienst {0} existiert nicht"] = "Service {0} does not exist",
        ["Dienststatus ist {0}"] = "Service status is {0}",
        ["Dienstprozess konnte nicht beendet werden"] = "Service process could not be terminated",
        ["Prozess {0} für Dienst {1} reagiert nicht"] = "Process {0} for service {1} is not responding",
        ["Dienstprozess {0} für {1} wurde erzwungen beendet"] = "Service process {0} for {1} was forcefully terminated",
        ["Dienstprozess {0} für {1} existiert nicht mehr"] = "Service process {0} for {1} no longer exists",
        ["keine Rechte zum Beenden des Dienstprozesses"] = "No permission to terminate the service process",
        ["Dienststatus konnte nicht gelesen werden"] = "Service status could not be read",
        ["Agent läuft im Infobereich weiter."] = "Agent keeps running in the notification area.",
        ["Autostart und ausgewählte Daten wurden entfernt. Der Programmordner kann danach gelöscht werden."] = "Autostart and selected data were removed. The application folder can be deleted afterwards.",
        ["Löschen bestätigen"] = "Confirm deletion",
        ["wirklich löschen?"] = "really delete?",
        ["Ja"] = "Yes",
        ["Nein"] = "No",
        ["Keine"] = "None",
        ["Aus"] = "Off",
        ["Neustart: "] = "Restart: ",
        [" (Force bei Timeout)"] = " (force on timeout)",
        ["Warnung oder Fehler"] = "Warning or error",
        ["Monitoring OK"] = "Monitoring OK",
        ["OK"] = "OK",
        ["Abbrechen"] = "Cancel",
        ["Eingaben prüfen"] = "Check input",
        ["Ping-Check"] = "Ping check",
        ["TCP-Port-Check"] = "TCP port check",
        ["Windows-Dienst-Check"] = "Windows service check",
        ["Laufwerks-Check"] = "Drive check",
        ["Hostname oder IP-Adresse"] = "Hostname or IP address",
        ["Uptime-Kuma-Push-URL"] = "Uptime Kuma push URL",
        ["Intervall in Sekunden"] = "Interval in seconds",
        ["Timeout in Millisekunden"] = "Timeout in milliseconds",
        ["Dienste bei Fehler neu starten"] = "Restart services on failure",
        ["Weitere Dienste bei Fehler neu starten"] = "Restart additional services on failure",
        ["Neustart-Cooldown (s)"] = "Restart cooldown (s)",
        ["Bei Stop-Timeout Prozess beenden"] = "Kill process on stop timeout",
        ["Beschreibung / Notiz"] = "Description / note",
        ["TCP-Verbindungen ins Log schreiben"] = "Write TCP connections to log",
        ["TCP-Log Richtung"] = "TCP log direction",
        ["Erwarteter Status"] = "Expected status",
        ["Dienst automatisch neu starten"] = "Restart service automatically",
        ["Pfad / Laufwerk"] = "Path / drive",
        ["Minimum frei (%)"] = "Minimum free (%)",
        ["Minimum frei (GB)"] = "Minimum free (GB)",
        ["Netzlaufwerk automatisch verbinden"] = "Reconnect network drive automatically",
        ["Reconnect UNC-Pfad"] = "Reconnect UNC path",
        ["Details ins Log schreiben"] = "Write details to log",
        ["Anzeigen"] = "Show",
        ["Maskieren"] = "Mask",
        ["Auswählen"] = "Select",
        ["Dienste auswählen"] = "Select services",
        ["Lokale Dienste konnten nicht geladen werden: "] = "Local services could not be loaded: ",
        ["Hostname oder IP-Adresse ist ungültig."] = "Hostname or IP address is invalid.",
        ["Push-URL ist ungültig."] = "Push URL is invalid.",
        ["Port ist ungültig."] = "Port is invalid.",
        ["Dienstname fehlt."] = "Service name is missing.",
        ["Pfad oder Laufwerksbuchstabe fehlt."] = "Path or drive letter is missing.",
        ["Für Reconnect wird ein Laufwerksbuchstabe oder UNC-Pfad benötigt."] = "Reconnect requires a drive letter or UNC path.",
        ["Die Deinstallation entfernt den Autostart. Optional können Konfiguration und Logs gelöscht werden."] = "Uninstall removes autostart. Configuration and logs can optionally be deleted.",
        ["Konfiguration löschen"] = "Delete configuration",
        ["Logs löschen"] = "Delete logs",
        ["Uptime Kuma Tray Agent läuft bereits."] = "Uptime Kuma Tray Agent is already running."
    };

    private static readonly IReadOnlyDictionary<string, string> Polish = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Systemsprache"] = "Język systemu",
        ["English"] = "Angielski",
        ["Deutsch"] = "Niemiecki",
        ["Polski"] = "Polski",
        ["Sprache"] = "Język",
        ["Name"] = "Nazwa",
        ["Status"] = "Status",
        ["Host"] = "Host",
        ["Port"] = "Port",
        ["Timeout"] = "Limit czasu",
        ["HTTP"] = "HTTP",
        ["Push-URL"] = "Adres push",
        ["TCP-Log"] = "Log TCP",
        ["Reconnect"] = "Ponowne połączenie",
        ["Agent"] = "Agent",
        ["Monitoring"] = "Monitoring",
        ["Version"] = "Wersja",
        ["Watchdog"] = "Watchdog",
        ["Ping-Checks"] = "Testy ping",
        ["TCP-Checks"] = "Testy TCP",
        ["Logs"] = "Logi",
        ["Ping"] = "Ping",
        ["TCP"] = "TCP",
        ["Dienst"] = "Usługa",
        ["Laufwerk"] = "Dysk",
        ["Fenster öffnen"] = "Otwórz okno",
        ["Monitoring starten"] = "Uruchom monitoring",
        ["Monitoring stoppen"] = "Zatrzymaj monitoring",
        ["Status anzeigen"] = "Pokaż status",
        ["Konfiguration öffnen"] = "Otwórz konfigurację",
        ["Logs öffnen"] = "Otwórz logi",
        ["Deinstallieren"] = "Odinstaluj",
        ["Deinstallation"] = "Odinstalowanie",
        ["Beenden"] = "Zamknij",
        ["Agent-Status"] = "Status agenta",
        ["Letzter Lauf"] = "Ostatnie uruchomienie",
        ["Letzter Fehler"] = "Ostatni błąd",
        ["Computername"] = "Nazwa komputera",
        ["Maschineninfos"] = "Informacje o maszynie",
        ["Speichern"] = "Zapisz",
        ["Testlauf ausführen"] = "Uruchom test",
        ["Globale Einstellungen"] = "Ustawienia globalne",
        ["Standard-Intervall (s)"] = "Domyślny interwał (s)",
        ["HTTP-Timeout (ms)"] = "Limit czasu HTTP (ms)",
        ["TCP-Timeout (ms)"] = "Limit czasu TCP (ms)",
        ["Ping-Timeout (ms)"] = "Limit czasu ping (ms)",
        ["Log-Level"] = "Poziom logów",
        ["Darstellung"] = "Wygląd",
        ["Push-URLs maskieren"] = "Maskuj adresy push",
        ["Autostart aktivieren"] = "Włącz autostart",
        ["Autostart deaktivieren"] = "Wyłącz autostart",
        ["Start minimiert"] = "Startuj zminimalizowany",
        ["Beim Schließen in Tray"] = "Po zamknięciu do zasobnika",
        ["Maschineninfos mitsenden"] = "Wysyłaj informacje o maszynie",
        ["Monitoring automatisch starten"] = "Uruchamiaj monitoring automatycznie",
        ["Watchdog aktiv"] = "Watchdog aktywny",
        ["Watchdog-Push-URL"] = "Adres push watchdog",
        ["Intervall (s)"] = "Interwał (s)",
        ["Max. ohne Erfolg (s)"] = "Maks. bez sukcesu (s)",
        ["Allgemein"] = "Ogólne",
        ["Windows-Dienste"] = "Usługi Windows",
        ["Laufwerke"] = "Dyski",
        ["Lokale Hosts, TCP-Ports und Windows-Dienste im Blick behalten"] = "Monitoruj lokalne hosty, porty TCP i usługi Windows",
        ["Bereit"] = "Gotowe",
        ["Typ"] = "Typ",
        ["Ziel"] = "Cel",
        ["Aktiv"] = "Aktywny",
        ["Antwortzeit"] = "Czas odpowiedzi",
        ["Letzter Check"] = "Ostatni test",
        ["Letzter OK"] = "Ostatni OK",
        ["Nächste Ausführung"] = "Następne uruchomienie",
        ["Letzter Push"] = "Ostatni push",
        ["Intervall"] = "Interwał",
        ["Fehleraktion"] = "Akcja błędu",
        ["Notiz"] = "Notatka",
        ["Anzeigename"] = "Nazwa wyświetlana",
        ["Dienstname"] = "Nazwa usługi",
        ["Erwartet"] = "Oczekiwany",
        ["Auto-Neustart"] = "Auto restart",
        ["Lokaler Dienst"] = "Usługa lokalna",
        ["Stoppbar"] = "Można zatrzymać",
        ["Pfad"] = "Ścieżka",
        ["Min. frei %"] = "Min. wolne %",
        ["Min. frei GB"] = "Min. wolne GB",
        ["Logdetails"] = "Szczegóły logu",
        ["Hinzufügen"] = "Dodaj",
        ["Aus Liste hinzufügen"] = "Dodaj z listy",
        ["Bearbeiten"] = "Edytuj",
        ["Löschen"] = "Usuń",
        ["Testen"] = "Testuj",
        ["Dienst starten"] = "Uruchom usługę",
        ["Dienst stoppen"] = "Zatrzymaj usługę",
        ["Dienst neu starten"] = "Uruchom usługę ponownie",
        ["Dienst hart neu starten"] = "Wymuś restart usługi",
        ["Lokale Dienste laden"] = "Wczytaj usługi lokalne",
        ["Aktualisieren"] = "Odśwież",
        ["Konfiguration prüfen"] = "Sprawdź konfigurację",
        ["Konfiguration gespeichert"] = "Konfiguracja zapisana",
        ["Konfiguration gespeichert."] = "Konfiguracja zapisana.",
        ["Sprache geändert"] = "Język zmieniony",
        ["Die Sprache wurde gespeichert. Um die Sprache vollständig zu ändern, muss die App neu gestartet werden."] = "Język został zapisany. Aby w pełni zmienić język, trzeba ponownie uruchomić aplikację.",
        ["Konfiguration konnte nicht gespeichert werden: "] = "Nie można zapisać konfiguracji: ",
        ["Fehler"] = "Błąd",
        ["Warnung"] = "Ostrzeżenie",
        ["Deaktiviert"] = "Wyłączony",
        ["Unbekannt"] = "Nieznany",
        ["Läuft"] = "Działa",
        ["Gestoppt"] = "Zatrzymany",
        ["Pausiert"] = "Wstrzymany",
        ["Startet"] = "Uruchamianie",
        ["Stoppt"] = "Zatrzymywanie",
        ["Wird fortgesetzt"] = "Wznawianie",
        ["Pausiert gerade"] = "Wstrzymywanie",
        ["Hell"] = "Jasny",
        ["Dunkel"] = "Ciemny",
        ["Debug"] = "Debug",
        ["Ein- und ausgehend"] = "Przychodzące i wychodzące",
        ["Eingehend"] = "Przychodzące",
        ["Ausgehend"] = "Wychodzące",
        ["aktiv"] = "aktywny",
        ["inaktiv"] = "nieaktywny",
        ["Monitoring aktiv"] = "Monitoring aktywny",
        ["Monitoring inaktiv"] = "Monitoring nieaktywny",
        ["Monitoring im Windows-Dienst aktiviert"] = "Monitoring w usłudze Windows włączony",
        ["Monitoring deaktiviert"] = "Monitoring wyłączony",
        ["Testlauf läuft..."] = "Test trwa...",
        ["Testlauf abgeschlossen."] = "Test zakończony.",
        ["Testlauf"] = "Test",
        ["Testlauf fehlgeschlagen"] = "Test nieudany",
        ["Soll der Test auch an Uptime Kuma gesendet werden?\r\n\r\nJa = Push senden\r\nNein = nur lokal testen"] = "Czy wysłać test także do Uptime Kuma?\r\n\r\nTak = wyślij push\r\nNie = tylko test lokalny",
        ["Bitte zuerst einen lokalen Dienst auswählen."] = "Najpierw wybierz usługę lokalną.",
        ["Dienst auswählen"] = "Wybierz usługę",
        ["Check wurde nicht gefunden."] = "Nie znaleziono testu.",
        ["Testergebnis"] = "Wynik testu",
        ["Lokale Dienste werden geladen..."] = "Wczytywanie usług lokalnych...",
        ["Lokale Dienste konnten nicht geladen werden"] = "Nie można wczytać usług lokalnych",
        ["lokale Dienste geladen"] = "usług lokalnych wczytano",
        ["Dienste laden"] = "Wczytaj usługi",
        ["Dienste konnten nicht geladen werden"] = "Nie można wczytać usług",
        ["Bitte zuerst einen Dienst-Check oder lokalen Dienst auswählen."] = "Najpierw wybierz test usługi lub usługę lokalną.",
        ["Dienstaktion"] = "Akcja usługi",
        ["Dienst {0} hart neu starten?\r\n\r\nWenn der Dienst beim Stoppen hängen bleibt, wird der zugehörige Prozess erzwungen beendet."] = "Wymusić restart usługi {0}?\r\n\r\nJeśli usługa zawiesi się podczas zatrzymywania, powiązany proces zostanie wymuszony zakończony.",
        ["Unbekannte Aktion"] = "Nieznana akcja",
        ["Lokale Dienste konnten nicht aufgelistet werden"] = "Nie można wyświetlić listy usług lokalnych",
        ["Dienst existiert nicht"] = "Usługa nie istnieje",
        ["Dienstname fehlt"] = "Brakuje nazwy usługi",
        ["Dienststatus gelesen"] = "Odczytano status usługi",
        ["Dienstprüfung fehlgeschlagen"] = "Sprawdzenie usługi nie powiodło się",
        ["Dienst läuft bereits"] = "Usługa już działa",
        ["Dienst gestartet"] = "Usługa uruchomiona",
        ["Timeout beim Starten des Dienstes"] = "Limit czasu podczas uruchamiania usługi",
        ["Dienst konnte nicht gestartet werden"] = "Nie można uruchomić usługi",
        ["Dienst ist bereits gestoppt"] = "Usługa jest już zatrzymana",
        ["Dienst konnte nicht gestoppt werden"] = "Nie można zatrzymać usługi",
        ["Dienst akzeptiert kein Stop-Signal"] = "Usługa nie akceptuje sygnału zatrzymania",
        ["Dienst gestoppt"] = "Usługa zatrzymana",
        ["Timeout beim Stoppen des Dienstes"] = "Limit czasu podczas zatrzymywania usługi",
        ["Dienstprozess {0} wurde erzwungen beendet"] = "Proces usługi {0} został wymuszony zakończony",
        ["Dienstprozess beendet, Status unklar"] = "Proces usługi zakończony, status niejasny",
        ["keine Rechte zum Lesen des Dienstes"] = "Brak uprawnień do odczytu usługi",
        ["Service Control Manager konnte nicht geöffnet werden"] = "Nie można otworzyć Service Control Manager",
        ["keine Rechte zum Starten des Dienstes"] = "Brak uprawnień do uruchomienia usługi",
        ["Dienst {0} existiert nicht"] = "Usługa {0} nie istnieje",
        ["Dienststatus ist {0}"] = "Status usługi to {0}",
        ["Dienstprozess konnte nicht beendet werden"] = "Nie można zakończyć procesu usługi",
        ["Prozess {0} für Dienst {1} reagiert nicht"] = "Proces {0} dla usługi {1} nie odpowiada",
        ["Dienstprozess {0} für {1} wurde erzwungen beendet"] = "Proces usługi {0} dla {1} został wymuszony zakończony",
        ["Dienstprozess {0} für {1} existiert nicht mehr"] = "Proces usługi {0} dla {1} już nie istnieje",
        ["keine Rechte zum Beenden des Dienstprozesses"] = "Brak uprawnień do zakończenia procesu usługi",
        ["Dienststatus konnte nicht gelesen werden"] = "Nie można odczytać statusu usługi",
        ["Agent läuft im Infobereich weiter."] = "Agent działa dalej w zasobniku.",
        ["Autostart und ausgewählte Daten wurden entfernt. Der Programmordner kann danach gelöscht werden."] = "Autostart i wybrane dane zostały usunięte. Folder programu można potem usunąć.",
        ["Löschen bestätigen"] = "Potwierdź usunięcie",
        ["wirklich löschen?"] = "na pewno usunąć?",
        ["Ja"] = "Tak",
        ["Nein"] = "Nie",
        ["Keine"] = "Brak",
        ["Aus"] = "Wył.",
        ["Neustart: "] = "Restart: ",
        [" (Force bei Timeout)"] = " (wymuś przy timeout)",
        ["Warnung oder Fehler"] = "Ostrzeżenie lub błąd",
        ["Monitoring OK"] = "Monitoring OK",
        ["OK"] = "OK",
        ["Abbrechen"] = "Anuluj",
        ["Eingaben prüfen"] = "Sprawdź dane",
        ["Ping-Check"] = "Test ping",
        ["TCP-Port-Check"] = "Test portu TCP",
        ["Windows-Dienst-Check"] = "Test usługi Windows",
        ["Laufwerks-Check"] = "Test dysku",
        ["Hostname oder IP-Adresse"] = "Nazwa hosta lub adres IP",
        ["Uptime-Kuma-Push-URL"] = "Adres push Uptime Kuma",
        ["Intervall in Sekunden"] = "Interwał w sekundach",
        ["Timeout in Millisekunden"] = "Limit czasu w milisekundach",
        ["Dienste bei Fehler neu starten"] = "Restartuj usługi przy błędzie",
        ["Weitere Dienste bei Fehler neu starten"] = "Restartuj dodatkowe usługi przy błędzie",
        ["Neustart-Cooldown (s)"] = "Przerwa restartu (s)",
        ["Bei Stop-Timeout Prozess beenden"] = "Zakończ proces przy timeout zatrzymania",
        ["Beschreibung / Notiz"] = "Opis / notatka",
        ["TCP-Verbindungen ins Log schreiben"] = "Zapisuj połączenia TCP w logu",
        ["TCP-Log Richtung"] = "Kierunek logu TCP",
        ["Erwarteter Status"] = "Oczekiwany status",
        ["Dienst automatisch neu starten"] = "Automatycznie restartuj usługę",
        ["Pfad / Laufwerk"] = "Ścieżka / dysk",
        ["Minimum frei (%)"] = "Minimum wolne (%)",
        ["Minimum frei (GB)"] = "Minimum wolne (GB)",
        ["Netzlaufwerk automatisch verbinden"] = "Automatycznie podłącz dysk sieciowy",
        ["Reconnect UNC-Pfad"] = "Ścieżka UNC reconnect",
        ["Details ins Log schreiben"] = "Zapisuj szczegóły w logu",
        ["Anzeigen"] = "Pokaż",
        ["Maskieren"] = "Maskuj",
        ["Auswählen"] = "Wybierz",
        ["Dienste auswählen"] = "Wybierz usługi",
        ["Lokale Dienste konnten nicht geladen werden: "] = "Nie można wczytać usług lokalnych: ",
        ["Hostname oder IP-Adresse ist ungültig."] = "Nazwa hosta lub adres IP jest nieprawidłowy.",
        ["Push-URL ist ungültig."] = "Adres push jest nieprawidłowy.",
        ["Port ist ungültig."] = "Port jest nieprawidłowy.",
        ["Dienstname fehlt."] = "Brakuje nazwy usługi.",
        ["Pfad oder Laufwerksbuchstabe fehlt."] = "Brakuje ścieżki lub litery dysku.",
        ["Für Reconnect wird ein Laufwerksbuchstabe oder UNC-Pfad benötigt."] = "Reconnect wymaga litery dysku albo ścieżki UNC.",
        ["Die Deinstallation entfernt den Autostart. Optional können Konfiguration und Logs gelöscht werden."] = "Deinstalacja usuwa autostart. Opcjonalnie można usunąć konfigurację i logi.",
        ["Konfiguration löschen"] = "Usuń konfigurację",
        ["Logs löschen"] = "Usuń logi",
        ["Uptime Kuma Tray Agent läuft bereits."] = "Uptime Kuma Tray Agent już działa."
    };

    private static string _language = AppLanguages.German;

    public static string CurrentLanguage => _language;

    public static void Apply(string? configuredLanguage)
    {
        var normalized = AppLanguages.Normalize(configuredLanguage);
        var language = normalized == AppLanguages.System ? ResolveWindowsLanguage() : normalized;
        _language = language;
        var cultureName = language switch
        {
            AppLanguages.Polish => "pl-PL",
            AppLanguages.English => "en-US",
            _ => "de-DE"
        };

        var culture = CultureInfo.GetCultureInfo(cultureName);
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;
    }

    public static string T(string text)
    {
        var dictionary = _language switch
        {
            AppLanguages.English => English,
            AppLanguages.Polish => Polish,
            _ => null
        };

        return dictionary is not null && dictionary.TryGetValue(text, out var translated)
            ? translated
            : text;
    }

    public static string F(string text, params object[] args)
    {
        return string.Format(CultureInfo.CurrentCulture, T(text), args);
    }

    public static string CheckTypeName(CheckType type)
    {
        return type switch
        {
            CheckType.Ping => T("Ping"),
            CheckType.Tcp => T("TCP"),
            CheckType.Service => T("Dienst"),
            CheckType.Drive => T("Laufwerk"),
            CheckType.Watchdog => T("Watchdog"),
            _ => T("Unbekannt")
        };
    }

    public static string CheckStateName(UptimeKumaTrayAgent.Models.CheckState state)
    {
        return state switch
        {
            UptimeKumaTrayAgent.Models.CheckState.Up => T("OK"),
            UptimeKumaTrayAgent.Models.CheckState.Down => T("Fehler"),
            UptimeKumaTrayAgent.Models.CheckState.Warning => T("Warnung"),
            UptimeKumaTrayAgent.Models.CheckState.Disabled => T("Deaktiviert"),
            _ => T("Unbekannt")
        };
    }

    public static string ServiceStatusName(string? status)
    {
        return status?.Trim().ToLowerInvariant() switch
        {
            "running" => T("Läuft"),
            "stopped" => T("Gestoppt"),
            "paused" => T("Pausiert"),
            "startpending" => T("Startet"),
            "stoppending" => T("Stoppt"),
            "continuepending" => T("Wird fortgesetzt"),
            "pausepending" => T("Pausiert gerade"),
            _ => T("Unbekannt")
        };
    }

    public static string TcpDirectionName(string? value)
    {
        return TcpConnectionLogDirections.Normalize(value) switch
        {
            TcpConnectionLogDirections.Incoming => T("Eingehend"),
            TcpConnectionLogDirections.Outgoing => T("Ausgehend"),
            _ => T("Ein- und ausgehend")
        };
    }

    public static string LogLevelName(string? value)
    {
        return LogLevelKinds.Normalize(value) switch
        {
            "Warnung" => T("Warnung"),
            "Fehler" => T("Fehler"),
            "Debug" => T("Debug"),
            _ => T("Info")
        };
    }

    public static string ThemeName(string? value)
    {
        return ThemeModes.Normalize(value) switch
        {
            "Dark" => T("Dunkel"),
            _ => T("Hell")
        };
    }

    public static string LanguageDisplayName(string language)
    {
        return AppLanguages.Normalize(language) switch
        {
            AppLanguages.System => T("Systemsprache"),
            AppLanguages.English => T("English"),
            AppLanguages.Polish => T("Polski"),
            _ => T("Deutsch")
        };
    }

    private static string ResolveWindowsLanguage()
    {
        return WindowsLanguage;
    }

    private static string ResolveSupportedLanguage(string twoLetter)
    {
        return twoLetter switch
        {
            AppLanguages.English => AppLanguages.English,
            AppLanguages.Polish => AppLanguages.Polish,
            AppLanguages.German => AppLanguages.German,
            _ => AppLanguages.English
        };
    }
}
