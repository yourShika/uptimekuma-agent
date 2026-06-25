param(
    [string]$InstallDir = "$env:ProgramFiles\UptimeKumaTrayAgent"
)

$ErrorActionPreference = "Stop"
$appVersion = "1.0.10"
$publisher = "Kamil Bura"

function Assert-Admin {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw "Installation erfordert Administratorrechte. Bitte Install.cmd als Administrator starten."
    }
}

function Resolve-AppSource {
    $root = Resolve-Path (Join-Path $PSScriptRoot "..")
    $candidates = @(
        (Join-Path $root "build\win-x64"),
        (Join-Path $root "src\bin\Release\net8.0-windows")
    )

    foreach ($candidate in $candidates) {
        if (Test-Path (Join-Path $candidate "UptimeKumaTrayAgent.exe")) {
            return (Resolve-Path $candidate).Path
        }
    }

    throw "Keine gebaute UptimeKumaTrayAgent.exe gefunden. Bitte zuerst Build.cmd oder Publish.cmd ausführen."
}

function Get-LegacyConfigCandidates {
    $candidates = @()
    $currentUserConfig = Join-Path $env:APPDATA "UptimeKumaTrayAgent\config.json"
    if (Test-Path $currentUserConfig) {
        $candidates += Get-Item $currentUserConfig
    }

    $usersRoot = Join-Path $env:SystemDrive "Users"
    if (Test-Path $usersRoot) {
        $candidates += Get-ChildItem -Path $usersRoot -Directory -ErrorAction SilentlyContinue |
            ForEach-Object {
                $candidate = Join-Path $_.FullName "AppData\Roaming\UptimeKumaTrayAgent\config.json"
                if (Test-Path $candidate) {
                    Get-Item $candidate
                }
            }
    }

    $candidates |
        Where-Object { $_.Length -gt 0 } |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -Unique
}

function Test-FactoryDefaultConfig {
    param([string]$Path)

    if (-not (Test-Path $Path)) {
        return $false
    }

    try {
        $json = Get-Content -Path $Path -Raw | ConvertFrom-Json
        $pingChecks = @($json.pingChecks)
        $tcpChecks = @($json.tcpChecks)
        $serviceChecks = @($json.serviceChecks)
        $pingDefault = $pingChecks.Count -eq 1 -and $pingChecks[0].name -eq "Router" -and $pingChecks[0].host -eq "192.168.1.1"
        $tcpDefault = $tcpChecks.Count -eq 1 -and $tcpChecks[0].name -eq "HTTPS Server" -and $tcpChecks[0].host -eq "server01" -and [int]$tcpChecks[0].port -eq 443
        $serviceDefault = $serviceChecks.Count -eq 1 -and $serviceChecks[0].serviceName -eq "Spooler"
        return $pingDefault -and $tcpDefault -and $serviceDefault
    }
    catch {
        return $false
    }
}

function Protect-Configuration {
    param([string]$DataDir)

    New-Item -ItemType Directory -Force -Path $DataDir | Out-Null
    $configPath = Join-Path $DataDir "config.json"
    $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"

    if (Test-Path $configPath) {
        Copy-Item -Path $configPath -Destination (Join-Path $DataDir "config.update-backup-$timestamp.json") -Force
    }

    $legacy = Get-LegacyConfigCandidates | Select-Object -First 1
    if (-not $legacy) {
        return
    }

    if (-not (Test-Path $configPath)) {
        Copy-Item -Path $legacy.FullName -Destination $configPath -Force
        Write-Host "Alte Benutzer-Konfiguration übernommen: $($legacy.FullName)"
        return
    }

    if (Test-FactoryDefaultConfig -Path $configPath) {
        Copy-Item -Path $configPath -Destination (Join-Path $DataDir "config.factory-default-$timestamp.json") -Force
        Copy-Item -Path $legacy.FullName -Destination $configPath -Force
        Write-Host "Factory-Default-Konfiguration durch alte Benutzer-Konfiguration ersetzt: $($legacy.FullName)"
    }
}

Assert-Admin

$serviceName = "UptimeKumaTrayAgent"
$appDisplayName = "Uptime Kuma Tray Agent"
$serviceDisplayName = "UptimeKumaAgent"
$sourceDir = Resolve-AppSource
$installDir = [Environment]::ExpandEnvironmentVariables($InstallDir)
$exePath = Join-Path $installDir "UptimeKumaTrayAgent.exe"
$uninstallScriptDir = Join-Path $installDir "installer"
$uninstallCmd = Join-Path $installDir "Uninstall.cmd"
$dataDir = Join-Path $env:ProgramData "UptimeKumaTrayAgent"
$startMenuDir = Join-Path $env:ProgramData "Microsoft\Windows\Start Menu\Programs"
$startMenuShortcut = Join-Path $startMenuDir "UptimeKumaAgent.lnk"
$configShortcut = Join-Path $startMenuDir "UptimeKumaAgent Konfiguration.lnk"

Write-Host "Installiere $appDisplayName $appVersion nach $installDir ..."

$existingService = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
if ($existingService) {
    if ($existingService.Status -ne "Stopped") {
        Stop-Service -Name $serviceName -Force -ErrorAction SilentlyContinue
        $existingService.WaitForStatus("Stopped", [TimeSpan]::FromSeconds(30))
    }

    sc.exe delete $serviceName | Out-Null
    for ($i = 0; $i -lt 30; $i++) {
        if (-not (Get-Service -Name $serviceName -ErrorAction SilentlyContinue)) {
            break
        }

        Start-Sleep -Seconds 1
    }

    if (Get-Service -Name $serviceName -ErrorAction SilentlyContinue) {
        throw "Der vorhandene Dienst $serviceName konnte nicht gelöscht werden. Bitte Dienste-Fenster schließen und erneut installieren."
    }
}

Get-Process -Name "UptimeKumaTrayAgent" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 1

New-Item -ItemType Directory -Force -Path $installDir | Out-Null
New-Item -ItemType Directory -Force -Path $dataDir | Out-Null
icacls.exe $dataDir /grant "*S-1-5-32-545:(OI)(CI)M" | Out-Null
Protect-Configuration -DataDir $dataDir
Copy-Item -Path (Join-Path $sourceDir "*") -Destination $installDir -Recurse -Force
New-Item -ItemType Directory -Force -Path $uninstallScriptDir | Out-Null
Copy-Item -Path (Join-Path $PSScriptRoot "Uninstall.ps1") -Destination $uninstallScriptDir -Force
Copy-Item -Path (Join-Path (Split-Path $PSScriptRoot -Parent) "Uninstall.cmd") -Destination $uninstallCmd -Force

$binPath = '"' + $exePath + '" --service'
New-Service -Name $serviceName -BinaryPathName $binPath -DisplayName $serviceDisplayName -StartupType Automatic -Description "Überwacht Hosts, TCP-Ports und Windows-Dienste und sendet Ergebnisse an Uptime Kuma Push-Monitore." | Out-Null
if (-not (Get-Service -Name $serviceName -ErrorAction SilentlyContinue)) {
    throw "Der Windows-Dienst $serviceName wurde nicht angelegt."
}

$failureOutput = & sc.exe failure $serviceName reset= 86400 actions= restart/60000/restart/60000/""/60000 2>&1
if ($LASTEXITCODE -ne 0) {
    throw "Dienst-Wiederherstellung konnte nicht gesetzt werden: $failureOutput"
}

$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($startMenuShortcut)
$shortcut.TargetPath = $exePath
$shortcut.WorkingDirectory = $installDir
$shortcut.IconLocation = $exePath
$shortcut.Description = $appDisplayName
$shortcut.Save()

$configPath = Join-Path $dataDir "config.json"
$configLink = $shell.CreateShortcut($configShortcut)
$configLink.TargetPath = "$env:WINDIR\System32\notepad.exe"
$configLink.Arguments = '"' + $configPath + '"'
$configLink.WorkingDirectory = $dataDir
$configIcon = Join-Path $installDir "Assets\UptimeKumaAgentConfig.ico"
$configLink.IconLocation = if (Test-Path $configIcon) { $configIcon } else { $exePath }
$configLink.Description = "Uptime Kuma Tray Agent Konfiguration"
$configLink.Save()

$uninstallKey = "HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\$serviceName"
New-Item -Path $uninstallKey -Force | Out-Null
New-ItemProperty -Path $uninstallKey -Name "DisplayName" -Value $appDisplayName -PropertyType String -Force | Out-Null
New-ItemProperty -Path $uninstallKey -Name "DisplayVersion" -Value $appVersion -PropertyType String -Force | Out-Null
New-ItemProperty -Path $uninstallKey -Name "Publisher" -Value $publisher -PropertyType String -Force | Out-Null
New-ItemProperty -Path $uninstallKey -Name "InstallLocation" -Value $installDir -PropertyType String -Force | Out-Null
New-ItemProperty -Path $uninstallKey -Name "DisplayIcon" -Value $exePath -PropertyType String -Force | Out-Null
New-ItemProperty -Path $uninstallKey -Name "UninstallString" -Value ('"' + $uninstallCmd + '"') -PropertyType String -Force | Out-Null
New-ItemProperty -Path $uninstallKey -Name "NoModify" -Value 1 -PropertyType DWord -Force | Out-Null
New-ItemProperty -Path $uninstallKey -Name "NoRepair" -Value 1 -PropertyType DWord -Force | Out-Null

Start-Service -Name $serviceName
(Get-Service -Name $serviceName).WaitForStatus("Running", [TimeSpan]::FromSeconds(30))
Write-Host "$appDisplayName wurde installiert und gestartet. Dienstname in services.msc: $serviceDisplayName"
