@echo off
setlocal
cd /d "%~dp0"
if exist "native\VirtualDesktopAccessor.dll" if exist "assets\BioPager.ico" exit /b 0
powershell -NoProfile -Command "$ErrorActionPreference='Stop'; $stage=Join-Path $env:TEMP ('BioPager-assets-'+[guid]::NewGuid()); try { Expand-Archive -LiteralPath (Join-Path (Split-Path $pwd.Path) 'BioPager-v0.4.5-source.zip') -DestinationPath $stage; Copy-Item -Path (Join-Path $stage 'native'),(Join-Path $stage 'assets') -Destination $pwd.Path -Recurse -Force } finally { if(Test-Path $stage){Remove-Item $stage -Recurse -Force} }"
exit /b %errorlevel%
