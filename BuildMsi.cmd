@echo off
setlocal
cd /d "%~dp0"

if "%~1"=="" (
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0installer\msi\BuildMsi.ps1" -RuntimeIdentifier win-x64
  if errorlevel 1 exit /b %ERRORLEVEL%
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0installer\msi\BuildMsi.ps1" -RuntimeIdentifier win-x86
  exit /b %ERRORLEVEL%
)

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0installer\msi\BuildMsi.ps1" -RuntimeIdentifier "%~1"
exit /b %ERRORLEVEL%
