@echo off
setlocal
cd /d "%~dp0"

echo Building UptimeKumaTrayAgent...
dotnet build "src\UptimeKumaTrayAgent.csproj" -c Release
if errorlevel 1 (
  echo Build failed.
  exit /b 1
)

echo Build completed.
