@echo off
setlocal
cd /d "%~dp0"
call EnsureAssets.cmd
if errorlevel 1 exit /b 1
set "PAGER_CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
"%PAGER_CSC%" /nologo /warnaserror+ /target:exe /platform:x64 /win32icon:assets\BioPager.ico /resource:assets\BioPager.ico,BioPager.App.ico /optimize+ /main:BioPager.Tests /out:BioPagerTests.exe /resource:native\2023-02-22-windows11.dll,BioPager.2023-02-22-windows11.dll /resource:native\2023-11-10-windows11.dll,BioPager.2023-11-10-windows11.dll /resource:native\2024-01-25-windows11.dll,BioPager.2024-01-25-windows11.dll /resource:native\VirtualDesktopAccessor.dll,BioPager.VirtualDesktopAccessor.dll /resource:native\Legacy21-LICENSE.txt,BioPager.Legacy21License.txt /resource:assets\BioPager-logo.jpg,BioPager.Logo.jpg /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll Pager.cs TaskbarPosition.cs WindowsCompatibility.cs DesktopFallback.cs Legacy21.cs MoveWorker.cs WorkerClient.cs NativePayload.cs ProfileSetup.cs Tests.cs
if errorlevel 1 exit /b 1
BioPagerTests.exe %*
exit /b %errorlevel%
