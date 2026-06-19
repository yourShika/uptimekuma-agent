param(
    [switch]$DeleteData
)

$ErrorActionPreference = "Stop"

function Assert-Admin {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw "Deinstallation erfordert Administratorrechte. Bitte Uninstall.cmd als Administrator starten."
    }
}

Assert-Admin

$serviceName = "UptimeKumaTrayAgent"
$installDir = Resolve-Path (Join-Path $PSScriptRoot "..") -ErrorAction SilentlyContinue
$installPath = if ($installDir) { $installDir.Path } else { "$env:ProgramFiles\UptimeKumaTrayAgent" }
$startMenuShortcut = Join-Path $env:ProgramData "Microsoft\Windows\Start Menu\Programs\UptimeKumaAgent.lnk"
$configShortcut = Join-Path $env:ProgramData "Microsoft\Windows\Start Menu\Programs\UptimeKumaAgent Konfiguration.lnk"

Write-Host "Deinstalliere Uptime Kuma Tray Agent ..."

sc.exe stop $serviceName | Out-Null
Start-Sleep -Seconds 2
sc.exe delete $serviceName | Out-Null

Remove-Item -Path "HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\$serviceName" -Recurse -Force -ErrorAction SilentlyContinue
Remove-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Name $serviceName -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $startMenuShortcut -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $configShortcut -Force -ErrorAction SilentlyContinue

if ($DeleteData) {
    Remove-Item -LiteralPath "$env:ProgramData\UptimeKumaTrayAgent" -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath "$env:APPDATA\UptimeKumaTrayAgent" -Recurse -Force -ErrorAction SilentlyContinue
}

$deleteCommand = @"
Start-Sleep -Seconds 2
Remove-Item -LiteralPath '$installPath' -Recurse -Force -ErrorAction SilentlyContinue
"@

Start-Process powershell.exe -ArgumentList "-NoProfile", "-ExecutionPolicy", "Bypass", "-Command", $deleteCommand -WindowStyle Hidden
Write-Host "Uptime Kuma Tray Agent wurde deinstalliert."
