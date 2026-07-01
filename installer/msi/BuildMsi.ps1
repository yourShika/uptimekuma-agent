param(
    [ValidateSet("win-x64", "win-x86")]
    [string]$RuntimeIdentifier = "win-x64"
)

$ErrorActionPreference = "Stop"

$version = "1.1.2"
$manufacturer = "Kamil Bura"
$productName = "Uptime Kuma Tray Agent"
$serviceName = "UptimeKumaTrayAgent"
$serviceDisplayName = "UptimeKumaAgent"
$upgradeCode = "{5C599178-CC4E-4FF5-99D2-10F6E836C6F8}"
$architecture = if ($RuntimeIdentifier -eq "win-x86") { "x86" } else { "x64" }
$wixArchitecture = $architecture

$root = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$projectPath = Join-Path $root "src\UptimeKumaTrayAgent.csproj"
$publishDir = Join-Path $root "build\$RuntimeIdentifier"
$outputDir = Join-Path $root "build\installer"
$msiWorkDir = Join-Path $root "build\msi"
$wxsPath = Join-Path $msiWorkDir "Product.$RuntimeIdentifier.generated.wxs"
$outputMsi = Join-Path $outputDir "UptimeKumaTrayAgent-Setup-$version-$architecture.msi"
$wix = Join-Path $root "tools\wix\wix.exe"
$licensePath = Join-Path $PSScriptRoot "License.rtf"

function ConvertTo-WixId {
    param([string]$Value, [string]$Prefix)

    $sha1 = [System.Security.Cryptography.SHA1]::Create()
    try {
        $sha = $sha1.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($Value))
    }
    finally {
        $sha1.Dispose()
    }

    $hash = -join ($sha[0..9] | ForEach-Object { $_.ToString("x2") })
    return "$Prefix$hash"
}

function ConvertTo-XmlText {
    param([string]$Value)

    return [System.Security.SecurityElement]::Escape($Value)
}

function New-ComponentXml {
    param(
        [System.IO.FileInfo]$File,
        [string]$RelativePath,
        [string]$Indent
    )

    $componentId = if ($RelativePath -ieq "UptimeKumaTrayAgent.exe") { "CmpAppExe" } else { ConvertTo-WixId $RelativePath "Cmp" }
    $fileId = if ($RelativePath -ieq "UptimeKumaTrayAgent.exe") { "AppExeFile" } else { ConvertTo-WixId $RelativePath "File" }
    $source = ConvertTo-XmlText $File.FullName
    $lines = New-Object System.Collections.Generic.List[string]
    $lines.Add("$Indent<Component Id=`"$componentId`" Guid=`"*`">")
    $lines.Add("$Indent  <File Id=`"$fileId`" Source=`"$source`" KeyPath=`"yes`" />")

    if ($RelativePath -ieq "UptimeKumaTrayAgent.exe") {
        $lines.Add("$Indent  <ServiceControl Id=`"RemoveLegacyServiceOnInstall`" Name=`"$serviceName`" Stop=`"install`" Remove=`"install`" Wait=`"yes`" />")
        $lines.Add("$Indent  <ServiceInstall Id=`"AgentServiceInstall`" Type=`"ownProcess`" Vital=`"yes`" Name=`"$serviceName`" DisplayName=`"$serviceDisplayName`" Description=`"Überwacht Hosts, TCP-Ports, Laufwerke und Windows-Dienste und sendet Ergebnisse an Uptime Kuma Push-Monitore.`" Start=`"auto`" Account=`"LocalSystem`" ErrorControl=`"normal`" Arguments=`"--service`" />")
        $lines.Add("$Indent  <ServiceControl Id=`"AgentServiceControl`" Name=`"$serviceName`" Start=`"install`" Stop=`"both`" Remove=`"uninstall`" Wait=`"yes`" />")
    }

    $lines.Add("$Indent</Component>")
    return @{
        Xml = $lines
        ComponentId = $componentId
    }
}

function New-DirectoryXml {
    param(
        [string]$Directory,
        [string]$DirectoryId,
        [string]$RelativeDirectory,
        [string]$Indent,
        [System.Collections.Generic.List[string]]$ComponentRefs
    )

    $lines = New-Object System.Collections.Generic.List[string]
    $files = Get-ChildItem -LiteralPath $Directory -File | Sort-Object Name
    foreach ($file in $files) {
        $relativePath = if ([string]::IsNullOrWhiteSpace($RelativeDirectory)) {
            $file.Name
        }
        else {
            Join-Path $RelativeDirectory $file.Name
        }

        $component = New-ComponentXml -File $file -RelativePath $relativePath -Indent $Indent
        foreach ($line in $component.Xml) {
            $lines.Add($line)
        }
        $ComponentRefs.Add($component.ComponentId)
    }

    $directories = Get-ChildItem -LiteralPath $Directory -Directory | Sort-Object Name
    foreach ($child in $directories) {
        $relativeChild = if ([string]::IsNullOrWhiteSpace($RelativeDirectory)) {
            $child.Name
        }
        else {
            Join-Path $RelativeDirectory $child.Name
        }
        $childId = ConvertTo-WixId $relativeChild "Dir"
        $childName = ConvertTo-XmlText $child.Name
        $lines.Add("$Indent<Directory Id=`"$childId`" Name=`"$childName`">")
        $childXml = New-DirectoryXml -Directory $child.FullName -DirectoryId $childId -RelativeDirectory $relativeChild -Indent "$Indent  " -ComponentRefs $ComponentRefs
        foreach ($line in $childXml) {
            $lines.Add($line)
        }
        $lines.Add("$Indent</Directory>")
    }

    return $lines
}

if (-not (Test-Path -LiteralPath $wix)) {
    Write-Host "WiX wurde nicht gefunden. Installiere WiX Toolset lokal nach tools\wix..."
    dotnet tool install --tool-path (Join-Path $root "tools\wix") wix --version 6.0.2 --add-source https://api.nuget.org/v3/index.json
}

if (-not (Test-Path -LiteralPath $wix)) {
    throw "WiX wurde nicht gefunden: $wix."
}

Write-Host "Erzeuge MSI für $productName $version ($architecture) ..."
Write-Host "Publishing self-contained Windows $architecture build..."
dotnet publish $projectPath `
    -c Release `
    -r $RuntimeIdentifier `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -o $publishDir
if ($LASTEXITCODE -ne 0) {
    Write-Host "Self-contained publish fehlgeschlagen. Fallback auf framework-dependent Publish..."
    dotnet publish $projectPath `
        -c Release `
        -r $RuntimeIdentifier `
        --self-contained false `
        -o $publishDir
    if ($LASTEXITCODE -ne 0) {
        throw "Publish ist fehlgeschlagen."
    }
}

if (-not (Test-Path -LiteralPath (Join-Path $publishDir "UptimeKumaTrayAgent.exe"))) {
    throw "Publish-Ausgabe enthält keine UptimeKumaTrayAgent.exe."
}

$assetSource = Join-Path $root "src\Assets"
$assetTarget = Join-Path $publishDir "Assets"
if (Test-Path -LiteralPath $assetSource) {
    New-Item -ItemType Directory -Force -Path $assetTarget | Out-Null
    Copy-Item -LiteralPath (Join-Path $assetSource "UptimeKumaAgent.ico") -Destination $assetTarget -Force
    Copy-Item -LiteralPath (Join-Path $assetSource "UptimeKumaAgentConfig.ico") -Destination $assetTarget -Force
}

New-Item -ItemType Directory -Force -Path $outputDir, $msiWorkDir | Out-Null
Remove-Item -LiteralPath $wxsPath, $outputMsi -Force -ErrorAction SilentlyContinue

$componentRefs = New-Object System.Collections.Generic.List[string]
$installFolderXml = New-DirectoryXml -Directory $publishDir -DirectoryId "INSTALLFOLDER" -RelativeDirectory "" -Indent "          " -ComponentRefs $componentRefs
$componentRefXml = ($componentRefs | Sort-Object -Unique | ForEach-Object { "      <ComponentRef Id=`"$_`" />" }) -join [Environment]::NewLine

$installFolderXmlText = $installFolderXml -join [Environment]::NewLine
$license = ConvertTo-XmlText $licensePath
$appIcon = ConvertTo-XmlText (Join-Path $publishDir "UptimeKumaTrayAgent.exe")
$configIcon = ConvertTo-XmlText (Join-Path $publishDir "Assets\UptimeKumaAgentConfig.ico")

$wxs = @"
<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs" xmlns:ui="http://wixtoolset.org/schemas/v4/wxs/ui" xmlns:util="http://wixtoolset.org/schemas/v4/wxs/util">
  <Package Name="$productName" Manufacturer="$manufacturer" Version="$version" UpgradeCode="$upgradeCode" Scope="perMachine" Compressed="yes">
    <MajorUpgrade DowngradeErrorMessage="Eine neuere Version von $productName ist bereits installiert." />
    <MediaTemplate EmbedCab="yes" />

    <Property Id="WIXUI_INSTALLDIR" Value="INSTALLFOLDER" />
    <Property Id="ARPPRODUCTICON" Value="AppIcon.exe" />
    <WixVariable Id="WixUILicenseRtf" Value="$license" />
    <Icon Id="AppIcon.exe" SourceFile="$appIcon" />
    <Icon Id="ConfigIcon.ico" SourceFile="$configIcon" />

    <ui:WixUI Id="WixUI_InstallDir" />

    <StandardDirectory Id="ProgramFilesFolder">
      <Directory Id="INSTALLFOLDER" Name="UptimeKumaTrayAgent">
$installFolderXmlText
      </Directory>
    </StandardDirectory>

    <StandardDirectory Id="CommonAppDataFolder">
      <Directory Id="ProgramDataAppFolder" Name="UptimeKumaTrayAgent">
        <Directory Id="ProgramDataLogFolder" Name="Logs" />
        <Component Id="ProgramDataFolderComponent" Guid="*">
          <CreateFolder>
            <util:PermissionEx User="Users" GenericAll="yes" />
          </CreateFolder>
          <RegistryValue Root="HKLM" Key="Software\Kamil Bura\UptimeKumaTrayAgent" Name="ProgramDataFolder" Type="integer" Value="1" KeyPath="yes" />
        </Component>
      </Directory>
    </StandardDirectory>

    <StandardDirectory Id="ProgramMenuFolder">
      <Directory Id="ApplicationProgramsFolder" Name="Uptime Kuma Tray Agent">
        <Component Id="StartMenuShortcuts" Guid="*">
          <Shortcut Id="StartMenuShortcut" Name="UptimeKumaAgent" Description="$productName" Target="[INSTALLFOLDER]UptimeKumaTrayAgent.exe" WorkingDirectory="INSTALLFOLDER" Icon="AppIcon.exe" />
          <Shortcut Id="ConfigStartMenuShortcut" Name="UptimeKumaAgent Konfiguration" Description="Uptime Kuma Tray Agent Konfiguration" Target="[SystemFolder]notepad.exe" Arguments="&quot;[CommonAppDataFolder]UptimeKumaTrayAgent\config.json&quot;" Icon="ConfigIcon.ico" />
          <RemoveFile Id="RemoveLegacyRootShortcut" Directory="ProgramMenuFolder" Name="UptimeKumaAgent.lnk" On="install" />
          <RemoveFile Id="RemoveLegacyRootConfigShortcut" Directory="ProgramMenuFolder" Name="UptimeKumaAgent Konfiguration.lnk" On="install" />
          <RemoveRegistryKey Id="RemoveLegacyUninstallKey" Root="HKLM" Key="Software\Microsoft\Windows\CurrentVersion\Uninstall\UptimeKumaTrayAgent" Action="removeOnInstall" />
          <RemoveFolder Id="RemoveApplicationProgramsFolder" On="uninstall" />
          <RegistryValue Root="HKLM" Key="Software\Kamil Bura\UptimeKumaTrayAgent" Name="StartMenuShortcuts" Type="integer" Value="1" KeyPath="yes" />
        </Component>
      </Directory>
    </StandardDirectory>

    <Feature Id="MainFeature" Title="$productName" Level="1">
$componentRefXml
      <ComponentRef Id="ProgramDataFolderComponent" />
      <ComponentRef Id="StartMenuShortcuts" />
    </Feature>
  </Package>
</Wix>
"@

[System.IO.File]::WriteAllText($wxsPath, $wxs, [System.Text.UTF8Encoding]::new($false))

& $wix build $wxsPath -arch $wixArchitecture -ext WixToolset.UI.wixext -ext WixToolset.Util.wixext -o $outputMsi
if ($LASTEXITCODE -ne 0) {
    throw "WiX MSI-Build ist fehlgeschlagen."
}

Write-Host "MSI erstellt: $outputMsi"
