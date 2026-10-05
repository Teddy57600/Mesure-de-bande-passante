@echo off
if not exist "%~dp0dist\MiniDebit\MiniDebit.exe" (
  echo Compilez d'abord avec : powershell -ExecutionPolicy Bypass -File build.ps1
  pause
  exit /b 1
)
start "" "%~dp0dist\MiniDebit\MiniDebit.exe"
