@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Build-Installer.ps1"
if errorlevel 1 (
  echo.
  echo BUILD FAILED. Review the error above.
  pause
  exit /b 1
)
echo.
echo Installer ready in artifacts\dist
pause
