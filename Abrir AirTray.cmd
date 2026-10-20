@echo off
if not exist "%~dp0artifacts\airtray\AirTray.exe" (
  echo Compile primeiro: powershell -ExecutionPolicy Bypass -File scripts\Build.ps1
  pause
  exit /b 1
)
start "" "%~dp0artifacts\airtray\AirTray.exe"
