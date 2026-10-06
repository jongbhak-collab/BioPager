@echo off
setlocal
cd /d "%~dp0"
call EnsureAssets.cmd
if errorlevel 1 exit /b 1
set "PAGER_CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%PAGER_CSC%" (
 echo This build requires 64-bit Windows with .NET Framework.
 pause
 exit /b 1
)
echo Building BioPager 0.4.7. Exit the running pager before rebuilding.
"%PAGER_CSC%" /nologo /warnaserror+ /target:winexe /platform:x64 /win32icon:assets\BioPager.ico /resource:assets\BioPager.ico,BioPager.App.ico /optimize+ /out:BioPager.new.exe /resource:native\2023-02-22-windows11.dll,BioPager.2023-02-22-windows11.dll /resource:native\2023-11-10-windows11.dll,BioPager.2023-11-10-windows11.dll /resource:native\2024-01-25-windows11.dll,BioPager.2024-01-25-windows11.dll /resource:native\VirtualDesktopAccessor.dll,BioPager.VirtualDesktopAccessor.dll /resource:native\LICENSE.txt,BioPager.NativeLicense.txt /resource:LICENSE,BioPager.License.txt /resource:THIRD_PARTY_NOTICES.md,BioPager.Notices.txt /resource:native\Legacy21-LICENSE.txt,BioPager.Legacy21License.txt /resource:assets\BioPager-logo.jpg,BioPager.Logo.jpg /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll Pager.cs TaskbarPosition.cs WindowsCompatibility.cs DesktopFallback.cs Legacy21.cs MoveWorker.cs WorkerClient.cs NativePayload.cs ProfileSetup.cs VersionInfo.cs > build.log 2>&1
if errorlevel 1 (
 type build.log
 pause
 exit /b 1
)
move /y BioPager.new.exe BioPager.exe >nul
if errorlevel 1 (
 echo Could not replace the EXE. Exit the running pager, then try again.
 pause
 exit /b 1
)
echo Build complete. Run Start.cmd.
