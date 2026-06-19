@echo off
setlocal
cd /d "%~dp0"

set OUTDIR=%~dp0build\win-x64

echo Publishing self-contained Windows x64 build...
dotnet publish "src\UptimeKumaTrayAgent.csproj" ^
  -c Release ^
  -r win-x64 ^
  --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:EnableCompressionInSingleFile=true ^
  -o "%OUTDIR%"

if errorlevel 1 (
  echo Self-contained publish failed. Falling back to framework-dependent publish...
  dotnet publish "src\UptimeKumaTrayAgent.csproj" ^
    -c Release ^
    -r win-x64 ^
    --self-contained false ^
    -o "%OUTDIR%"
  if errorlevel 1 (
    echo Publish failed.
    exit /b 1
  )
)

echo Published to "%OUTDIR%".
