@echo off
setlocal
cd /d "%~dp0"
rem Required assets are tracked directly; no historical source ZIP is needed.
for %%F in (assets\BioPager.ico assets\BioPager-logo.jpg native\VirtualDesktopAccessor.dll native\2023-02-22-windows11.dll native\2023-11-10-windows11.dll native\2024-01-25-windows11.dll native\LICENSE.txt native\Legacy21-LICENSE.txt) do (
 if not exist "%%F" (
  echo Required build asset is missing: %%F
  echo Restore src\assets and src\native from the repository or source package.
  exit /b 1
 )
)
exit /b 0
