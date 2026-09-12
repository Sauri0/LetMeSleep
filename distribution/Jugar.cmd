@echo off
if exist "%~dp0Let-me-sleep-Launcher.exe" (
    start "" "%~dp0Let-me-sleep-Launcher.exe"
    exit /b
)
start "" "%~dp0Let-me-sleep.exe"
