# 🟢 Uptime Kuma Tray Agent

**Native Windows tray agent for Uptime Kuma push monitors**

The **Uptime Kuma Tray Agent** monitors ping targets, TCP ports, local Windows services, local drives, mapped network drives, and UNC paths. Each check can send its result to an individually configured **Uptime Kuma Push URL**.

The agent sends:

```text
status
msg
ping
```

The application includes a modern GUI with Uptime-Kuma-inspired branding, a custom window/tray icon, and support for **Light Mode** and **Dark Mode**.

---

## ✨ Features

* 🟢 Ping checks
* 🔌 TCP port checks
* 🧩 Windows service checks
* 💾 Drive checks for local drives, mapped network drives, and UNC paths
* 🔁 Optional failure actions, including Windows service restarts
* 🪟 Windows tray application with graphical configuration
* 🌗 Light and dark mode
* 🌍 Multi-language support: German, English, Polish
* 🛠️ Windows service mode for server environments
* 📦 MSI installer for x64 and x86
* 🧾 Logging with masked Push URLs
* 🔄 Update-safe configuration stored under ProgramData

---

## ✅ Requirements

### Target system

* Windows x64
* No separate .NET runtime installation is required for self-contained builds

### Build system

* .NET SDK 8 or newer
* Windows Desktop Workload

### Not required

* ❌ Python
* ❌ Node.js
* ❌ Docker
* ❌ NSSM
* ❌ Third-party helper tools

The WPF UI dependency `WPF-UI` is prepared in the project for the modern interface.

---

## 🧱 Build

```cmd
Build.cmd
```

---

## 📦 Create self-contained EXE

```cmd
Publish.cmd
```

The finished executable will be located at:

```text
build\win-x64\UptimeKumaTrayAgent.exe
```

---

## 🪟 Create setup installer

The recommended Windows installer is the **MSI package** with a license dialog and installation folder selection.

```cmd
BuildMsi.cmd
```

During the first MSI build, the script automatically installs the **WiX Toolset** locally into:

```text
tools\wix
```

The finished MSI files will be located at:

```text
build\installer\UptimeKumaTrayAgent-Setup-1.0.7-x64.msi
build\installer\UptimeKumaTrayAgent-Setup-1.0.7-x86.msi
```

The older self-extracting EXE installer can still be built:

```cmd
BuildInstaller.cmd
```

The finished file will be located at:

```text
build\installer\UptimeKumaTrayAgent-Setup-1.0.7.exe
```

---

## 🔄 Installation and updates

The installers can be used for both new installations and updates.

During updates, the following parts are updated:

* Windows service
* Program files
* Start menu entries

The following data is kept:

* Configuration
* Logs
* User data under `%ProgramData%\UptimeKumaTrayAgent`

Existing configurations are not overwritten during updates. If ProgramData does not yet contain a real user configuration, an existing AppData configuration can be migrated automatically.

The installer also creates the following Start menu entries:

```text
UptimeKumaAgent
UptimeKumaAgent Configuration
```

This allows Windows Search to find both the agent and the configuration file.

For normal server environments, `x64` is recommended.
`x86` is the 32-bit package for older or restricted systems.

For releases, an additional copy with `x32` in the file name can be provided. Internally, it is identical to the x86 package.

---

## ⚙️ Install as Windows service

The agent can be installed as a real Windows service. This allows monitoring to start automatically during system startup, even before a user logs in.

```cmd
Install.cmd
```

Or install it conveniently using the setup installer:

```text
UptimeKumaTrayAgent-Setup-1.0.7-x64.msi
```

> [!IMPORTANT]
> `Install.cmd` must be started as administrator.
> The setup installer automatically requests administrator privileges when required.

The service is installed as `LocalSystem` with startup type `Automatic` and appears in `services.msc` as:

```text
UptimeKumaAgent
```

The service starts monitoring automatically. The GUI remains available as the configuration interface. Saved changes to the JSON configuration are automatically reloaded by the service.

---

## 🚀 Getting started

1. Extract the folder
2. Start `UptimeKumaTrayAgent.exe`
3. Configure ping, TCP, service, and drive checks in the GUI
4. Save the configuration
5. For server operation, run `Install.cmd` as administrator

When closing the window, the application stays active in the tray by default. This behavior can be disabled under **Global Settings**.

The appearance can be switched between `Light` and `Dark` under **Global Settings**.

---

## 🌍 Language

The language is set to `System` by default and follows the Windows display language.

Currently supported:

* 🇩🇪 German
* 🇬🇧 English
* 🇵🇱 Polish

Alternatively, the language can be selected manually in the global settings.

> [!NOTE]
> After changing the language, restart the app so that all tables, dialogs, and labels are fully loaded in the selected language.

---

## 🛟 Failure actions

Ping, TCP, and Windows service checks can define one or more local Windows services as failure actions.

When a check fails, the agent tries to restart the configured services and reports the result:

* in the log
* in the Uptime Kuma message

A cooldown prevents permanently failing checks from restarting services repeatedly in very short intervals.

Optionally, if stopping a service times out, the related service process can be forcefully terminated. This is similar to a manual `taskkill`, but only active when:

* the option is enabled in the check
* or the **Hard restart service** button is used in the services view

TCP checks can also log matching incoming and outgoing TCP connections.

The result is written only to the log and is not sent to Uptime Kuma. For each connection, the following details are stored:

* Direction
* Local IP and port
* Remote IP and port
* TCP state
* PID
* Process name

---

## 💾 Drive checks

Drive checks can monitor:

* Local drives
* Mapped network drives
* UNC paths

Optional thresholds can be configured for:

* Free space in percent
* Free space in GB

For network drives, the agent can try to reconnect the drive after a disconnect. It uses existing Windows credentials or the configured UNC path.

The log includes details such as:

* Drive type
* File system format
* Total size
* Free space
* Reconnect result

---

## 🟢 Monitoring enabled / disabled

The Windows service respects `MonitoringAutoStart`.

If monitoring is stopped in the GUI or the option is disabled and saved, the service also stops its checks and push messages after the automatic configuration reload.

The GUI does not start a second local monitoring instance if the Windows service is already running.

---

## 🧾 Configuration

The configuration is stored as JSON:

```text
%ProgramData%\UptimeKumaTrayAgent\config.json
```

If write permissions are not available there, the agent automatically uses:

```text
%AppData%\UptimeKumaTrayAgent\config.json
```

An example configuration is included as:

```text
config.example.json
```

Existing configurations are not overwritten during updates.

Older AppData configurations are migrated to ProgramData during startup or installer execution when no real user configuration exists in ProgramData yet.

Disabled checks and disabled monitoring stay disabled after restarts, service reloads, and updates.

---

## 📡 Uptime Kuma Push URL

Each check can use its own Push URL.

Example:

```text
https://kuma.example.com/api/push/abc123
```

The agent automatically appends URL-encoded parameters:

```text
status=up&msg=Ping%20OK&ping=12
```

If the Push URL already contains query parameters, the agent correctly appends additional parameters using `&`.

---

## 🔁 Autostart

The recommended server setup is installation as a Windows service:

```cmd
Install.cmd
```

The older GUI autostart still uses the user registry key:

```text
HKCU\Software\Microsoft\Windows\CurrentVersion\Run
```

This usually does not require administrator privileges. However, the application only starts after user login.

For startup before user login, use the Windows service.

---

## 🔐 Windows services and administrator rights

Reading local Windows services usually works without administrator privileges.

Starting, stopping, or restarting individual services may require administrator privileges.

If permissions are missing:

* the GUI displays the error
* the agent writes the error to the log

---

## 📜 Logging

Logs are stored by default under:

```text
%ProgramData%\UptimeKumaTrayAgent\Logs
```

or, when using the fallback path:

```text
%AppData%\UptimeKumaTrayAgent\Logs
```

Push URLs are masked in the log.

Old logs are automatically deleted after 30 days.

---

## 🧹 Uninstallation

After installation as a Windows service, the agent appears in Windows installed apps as software by:

```text
Kamil Bura
```

The uninstaller removes:

* Windows service
* Program folder
* Entry from Windows installed apps

Alternatively, uninstall using:

```cmd
Uninstall.cmd
```

Configuration and logs are kept by default.

To also remove generated data, run:

```cmd
Uninstall.cmd -DeleteData
```

The tray menu also contains a simple uninstall option for portable use.

---

## 🧯 Troubleshooting

| Problem                                   | Solution                                                           |
| ----------------------------------------- | ------------------------------------------------------------------ |
| No pushes appear in Uptime Kuma           | Check the Push URL and run a test push                             |
| HTTP 401 / 403 / 404                      | Check the Push URL or monitor ID in Uptime Kuma                    |
| SSL or TLS errors                         | Check the certificate and reachability of the Uptime Kuma instance |
| Services cannot be started                | Start the application with sufficient privileges                   |
| Configuration is not saved to ProgramData | The agent automatically uses the AppData fallback                  |

---

## 📁 Project structure

```text
.
├── releases
├── build
├── tools
├── config.example.json
├── Build.cmd
├── Publish.cmd
├── BuildMsi.cmd
├── BuildInstaller.cmd
├── Install.cmd
└── Uninstall.cmd
```

---

## 🟢 Summary

The **Uptime Kuma Tray Agent** is a lightweight Windows agent for local monitoring, service supervision, drive checks, and Uptime Kuma push monitoring — including GUI, tray integration, Windows service support, and MSI installer.
