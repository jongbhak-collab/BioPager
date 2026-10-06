@echo off
setlocal
cd /d "%~dp0"
if not exist "BioPager.exe" call Build.cmd
if not exist "BioPager.exe" exit /b 1
start "" "%~dp0BioPager.exe"
