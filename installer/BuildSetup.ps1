$ErrorActionPreference = "Stop"

$version = "1.1.0"
$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$publishCmd = Join-Path $root "Publish.cmd"
$publishDir = Join-Path $root "build\win-x64"
$setupRoot = Join-Path $root "build\setup"
$stageDir = Join-Path $setupRoot "stage"
$payloadDir = Join-Path $setupRoot "payload"
$outputDir = Join-Path $root "build\installer"
$outputExe = Join-Path $outputDir "UptimeKumaTrayAgent-Setup-$version.exe"
$sedPath = Join-Path $setupRoot "setup.sed"
$runtimeInstallerName = "windowsdesktop-runtime-8.0.28-win-x64.exe"
$runtimeInstallerPath = Join-Path $outputDir $runtimeInstallerName
$includeRuntimeInstaller = $false

Write-Host "Erzeuge Setup für Uptime Kuma Tray Agent $version ..."

& $publishCmd
if ($LASTEXITCODE -ne 0) {
    throw "Publish.cmd ist fehlgeschlagen."
}

if (-not (Test-Path (Join-Path $publishDir "UptimeKumaTrayAgent.exe"))) {
    throw "Publish-Ausgabe enthält keine UptimeKumaTrayAgent.exe."
}

$publishIsSelfContained = (Test-Path -LiteralPath (Join-Path $publishDir "coreclr.dll")) -or
    (Test-Path -LiteralPath (Join-Path $publishDir "System.Private.CoreLib.dll"))
$includeRuntimeInstaller = -not $publishIsSelfContained -and (Test-Path -LiteralPath $runtimeInstallerPath)

Remove-Item -LiteralPath $setupRoot -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $stageDir, $payloadDir, $outputDir | Out-Null

New-Item -ItemType Directory -Force -Path (Join-Path $payloadDir "build\win-x64") | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $payloadDir "installer") | Out-Null
Copy-Item -Path (Join-Path $publishDir "*") -Destination (Join-Path $payloadDir "build\win-x64") -Recurse -Force
Copy-Item -LiteralPath (Join-Path $root "Install.cmd") -Destination $payloadDir -Force
Copy-Item -LiteralPath (Join-Path $root "Uninstall.cmd") -Destination $payloadDir -Force
Copy-Item -LiteralPath (Join-Path $root "README.md") -Destination $payloadDir -Force
Copy-Item -LiteralPath (Join-Path $root "installer\Install.ps1") -Destination (Join-Path $payloadDir "installer") -Force
Copy-Item -LiteralPath (Join-Path $root "installer\Uninstall.ps1") -Destination (Join-Path $payloadDir "installer") -Force

$payloadZip = Join-Path $stageDir "payload.zip"
Compress-Archive -Path (Join-Path $payloadDir "*") -DestinationPath $payloadZip -Force
if ($includeRuntimeInstaller) {
    Copy-Item -LiteralPath $runtimeInstallerPath -Destination (Join-Path $stageDir $runtimeInstallerName) -Force
    Write-Host "Bundled Runtime-Installer: $runtimeInstallerName"
}

Set-Content -LiteralPath (Join-Path $stageDir "Setup.cmd") -Encoding ASCII -Value @'
@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Setup.ps1"
exit /b %ERRORLEVEL%
'@

Set-Content -LiteralPath (Join-Path $stageDir "Setup.ps1") -Encoding ASCII -Value @"
`$ErrorActionPreference = "Stop"
`$version = "$version"
`$runtimeInstallerName = "$runtimeInstallerName"
`$requiresDesktopRuntime = `$$($includeRuntimeInstaller.ToString().ToLowerInvariant())
`$stableDir = Join-Path `$env:TEMP "UptimeKumaTrayAgentSetup-`$version"

function Test-Admin {
    `$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    `$principal = New-Object Security.Principal.WindowsPrincipal(`$identity)
    return `$principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Test-WindowsDesktopRuntime8 {
    `$sharedFxDir = Join-Path `$env:ProgramFiles "dotnet\shared\Microsoft.WindowsDesktop.App"
    if (Test-Path -LiteralPath `$sharedFxDir) {
        `$installed = Get-ChildItem -LiteralPath `$sharedFxDir -Directory -ErrorAction SilentlyContinue |
            Where-Object { `$_.Name -like "8.*" } |
            Select-Object -First 1
        if (`$installed) {
            return `$true
        }
    }

    `$dotnet = Get-Command dotnet.exe -ErrorAction SilentlyContinue
    if (-not `$dotnet) {
        return `$false
    }

    try {
        `$runtimes = & `$dotnet.Source --list-runtimes 2>`$null
        return [bool](`$runtimes | Where-Object { `$_ -match "^Microsoft\.WindowsDesktop\.App\s+8\." } | Select-Object -First 1)
    }
    catch {
        return `$false
    }
}

function Install-BundledRuntimeIfNeeded {
    if (Test-WindowsDesktopRuntime8) {
        return
    }

    `$runtimeInstaller = Join-Path `$PSScriptRoot `$runtimeInstallerName
    if (-not (Test-Path -LiteralPath `$runtimeInstaller)) {
        throw ".NET Desktop Runtime 8 ist nicht installiert und der Runtime-Installer ist nicht im Setup enthalten."
    }

    Write-Host "Installiere .NET Desktop Runtime 8 ..."
    `$runtime = Start-Process -FilePath `$runtimeInstaller -ArgumentList "/install", "/quiet", "/norestart" -PassThru -Wait
    if (`$runtime.ExitCode -ne 0 -and `$runtime.ExitCode -ne 3010) {
        throw ".NET Desktop Runtime 8 konnte nicht installiert werden. ExitCode=`$(`$runtime.ExitCode)"
    }
}

if (-not (Test-Admin)) {
    Remove-Item -LiteralPath `$stableDir -Recurse -Force -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Force -Path `$stableDir | Out-Null
    Copy-Item -LiteralPath (Join-Path `$PSScriptRoot "payload.zip") -Destination `$stableDir -Force
    Copy-Item -LiteralPath `$PSCommandPath -Destination `$stableDir -Force
    `$runtimeSource = Join-Path `$PSScriptRoot `$runtimeInstallerName
    if (Test-Path -LiteralPath `$runtimeSource) {
        Copy-Item -LiteralPath `$runtimeSource -Destination `$stableDir -Force
    }
    `$elevated = Start-Process powershell.exe -Verb RunAs -ArgumentList "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", (Join-Path `$stableDir "Setup.ps1") -PassThru -Wait
    exit `$elevated.ExitCode
}

if (`$requiresDesktopRuntime) {
    Install-BundledRuntimeIfNeeded
}

`$workDir = Join-Path `$env:TEMP "UptimeKumaTrayAgentPayload-`$version"
Remove-Item -LiteralPath `$workDir -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path `$workDir | Out-Null
Expand-Archive -LiteralPath (Join-Path `$PSScriptRoot "payload.zip") -DestinationPath `$workDir -Force
& (Join-Path `$workDir "installer\Install.ps1")
`$exitCode = `$LASTEXITCODE
Remove-Item -LiteralPath `$workDir -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath `$stableDir -Recurse -Force -ErrorAction SilentlyContinue
exit `$exitCode
"@

$escapedStage = $stageDir.Replace("\", "\\")
$escapedOutput = $outputExe.Replace("\", "\\")
$runtimeSedString = ""
$runtimeSedFile = ""
if ($includeRuntimeInstaller) {
    $runtimeSedString = "FILE3=""$runtimeInstallerName""`r`n"
    $runtimeSedFile = "%FILE3%=`r`n"
}
Set-Content -LiteralPath $sedPath -Encoding ASCII -Value @"
[Version]
Class=IEXPRESS
SEDVersion=3
[Options]
PackagePurpose=InstallApp
ShowInstallProgramWindow=1
HideExtractAnimation=0
UseLongFileName=1
InsideCompressed=0
CAB_FixedSize=0
CAB_ResvCodeSigning=0
RebootMode=N
InstallPrompt=%InstallPrompt%
DisplayLicense=%DisplayLicense%
FinishMessage=%FinishMessage%
TargetName=$escapedOutput
FriendlyName=Uptime Kuma Tray Agent Setup
AppLaunched=%FILE0%
PostInstallCmd=<None>
AdminQuietInstCmd=%FILE0%
UserQuietInstCmd=%FILE0%
SourceFiles=SourceFiles
[Strings]
InstallPrompt=
DisplayLicense=
FinishMessage=Uptime Kuma Tray Agent wurde installiert.
FILE0="Setup.cmd"
FILE1="Setup.ps1"
FILE2="payload.zip"
${runtimeSedString}[SourceFiles]
SourceFiles0=$escapedStage
[SourceFiles0]
%FILE0%=
%FILE1%=
%FILE2%=
${runtimeSedFile}
"@

$iexpress = Join-Path $env:WINDIR "System32\iexpress.exe"
Remove-Item -LiteralPath $outputExe -Force -ErrorAction SilentlyContinue
& $iexpress /N $sedPath
$iexpressExitCode = $LASTEXITCODE

$iexpressTempPrefix = "~UptimeKumaTrayAgent-Setup-$version"
$iexpressDeadline = [DateTimeOffset]::Now.AddMinutes(20)
do {
    $pendingIExpressFiles = @(Get-ChildItem -LiteralPath $outputDir -Filter "$iexpressTempPrefix.*" -ErrorAction SilentlyContinue)
    if ((Test-Path -LiteralPath $outputExe) -and $pendingIExpressFiles.Count -eq 0) {
        break
    }

    Start-Sleep -Seconds 2
} while ([DateTimeOffset]::Now -lt $iexpressDeadline)

if (-not (Test-Path -LiteralPath $outputExe)) {
    throw "IExpress hat keine Setup-Datei erzeugt: $outputExe. ExitCode=$iexpressExitCode"
}

$pendingIExpressFiles = @(Get-ChildItem -LiteralPath $outputDir -Filter "$iexpressTempPrefix.*" -ErrorAction SilentlyContinue)
if ($pendingIExpressFiles.Count -gt 0) {
    throw "IExpress ist nicht fertig geworden. ExitCode=$iexpressExitCode. Temp-Dateien: $($pendingIExpressFiles.Name -join ', ')"
}

Write-Host "Setup erstellt: $outputExe"
