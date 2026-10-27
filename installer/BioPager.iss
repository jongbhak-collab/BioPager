; Copyright (c) 2026 Jong Bhak. BioLicense 1.0.
; AppId and install mode MUST remain stable across future releases.
#ifndef AppVersion
  #define AppVersion "1.1.0"
#endif
#ifndef PayloadDir
  #define PayloadDir "..\src"
#endif
#ifndef OutputPath
  #define OutputPath "..\dist"
#endif
#ifndef HelperDir
  #define HelperDir ".\build"
#endif
[Setup]
AppId=JongBhak.BioPager
AppName=BioPager
AppVersion={#AppVersion}
AppVerName=BioPager {#AppVersion}
AppPublisher=Jong Bhak
AppPublisherURL=https://github.com/jongbhak-collab/BioPager
AppSupportURL=https://github.com/jongbhak-collab/BioPager/issues
DefaultDirName={localappdata}\Programs\BioPager
DefaultGroupName=BioPager
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
UsePreviousAppDir=yes
UsePreviousTasks=yes
DisableDirPage=yes
DisableProgramGroupPage=yes
DisableWelcomePage=no
DisableReadyPage=yes
DisableStartupPrompt=yes
WizardStyle=modern
WizardSizePercent=110
SetupIconFile=..\src\assets\BioPager.ico
UninstallDisplayIcon={app}\BioPager.exe
UninstallDisplayName=BioPager
OutputDir={#OutputPath}
OutputBaseFilename=BioPager-Setup-{#AppVersion}
Compression=lzma2
SolidCompression=yes
VersionInfoVersion={#AppVersion}.0
VersionInfoCompany=Jong Bhak
VersionInfoDescription=BioPager installer
VersionInfoProductName=BioPager
CloseApplications=no
RestartApplications=no
AlwaysRestart=no
SetupMutex=Local\BioPager.Setup
Uninstallable=yes
[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; Flags: unchecked
Name: "startup"; Description: "Start BioPager when I sign in to Windows"; Flags: unchecked
[Files]
Source: "{#PayloadDir}\BioPager.exe"; DestDir: "{app}"
Source: "{#HelperDir}\BioPagerSetupHelper.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#HelperDir}\BioPagerSetupHelper.exe"; Flags: dontcopy
Source: "QuickStart.html"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\src\LICENSE"; DestDir: "{app}"; DestName: "LICENSE.txt"; Flags: ignoreversion
Source: "..\src\THIRD_PARTY_NOTICES.md"; DestDir: "{app}"; Flags: ignoreversion
[Icons]
Name: "{group}\BioPager"; Filename: "{app}\BioPager.exe"; WorkingDir: "{app}"
Name: "{group}\BioPager Quick Start"; Filename: "{app}\QuickStart.html"
Name: "{autodesktop}\BioPager"; Filename: "{app}\BioPager.exe"; WorkingDir: "{app}"; Tasks: desktopicon
[Run]
Filename: "{app}\BioPager.exe"; Description: "Start BioPager now"; WorkingDir: "{app}"; Flags: nowait postinstall skipifsilent
Filename: "{app}\QuickStart.html"; Description: "Open the Quick Start guide"; Flags: shellexec nowait postinstall unchecked skipifsilent
Filename: "{app}\BioPager.exe"; WorkingDir: "{app}"; Flags: nowait; Check: SilentLaunchRequested
[Code]
const
  UninstallKey = 'Software\Microsoft\Windows\CurrentVersion\Uninstall\JongBhak.BioPager_is1';
  RunKey = 'Software\Microsoft\Windows\CurrentVersion\Run';
var
  UsagePage: TWizardPage;
function SilentLaunchRequested: Boolean;
var I: Integer;
begin
  Result := False;
  if not WizardSilent then exit;
  for I := 1 to ParamCount do
    if CompareText(ParamStr(I), '/LAUNCH') = 0 then Result := True;
end;
procedure InitializeWizard;
begin
  WizardForm.WelcomeLabel2.Caption :=
    'Install BioPager for your Windows account. No administrator password is required.' + #13#10#13#10 +
    'Setup chooses the application folder and adds Start menu shortcuts. Upgrades replace the installed program while keeping your preferences.' + #13#10#13#10 +
    'An existing BioPager will be closed gracefully before installation. Other applications stay open.';
  UsagePage := CreateOutputMsgPage(wpWelcome, 'Using BioPager',
    'Your desktops, at a glance',
    'SWITCH AND FOCUS' + #13#10 +
    'Click a desktop tile to switch. Double-click an app preview to focus it.' + #13#10#13#10 +
    'MOVE WINDOWS' + #13#10 +
    'Drag an app preview to another desktop tile. Press Esc to cancel.' + #13#10#13#10 +
    'CHOOSE YOUR VIEW' + #13#10 +
    'Right-click BioPager for Grid or Preserve desktop positions and window overlap. App logo badges help identify similar white windows.' + #13#10#13#10 +
    'FIND IT AGAIN' + #13#10 +
    'Use the Start menu shortcut. Double-click the tray icon to recover a floating pager. Right-click > Help has more details.' + #13#10#13#10 +
    'Future setup files upgrade this installation in place. Preview pictures stay in memory; they are not uploaded.');
end;
function PrepareToInstall(var NeedsRestart: Boolean): String;
var PreviousDir, Exe, RegistryVersion: String;
    OldVersion, NewVersion: Int64;
    ExitCode: Integer;
begin
  Result := '';
  if RegQueryStringValue(HKCU, UninstallKey, 'Inno Setup: App Path', PreviousDir) then begin
    if CompareText(RemoveBackslashUnlessRoot(ExpandFileName(PreviousDir)),
                   RemoveBackslashUnlessRoot(ExpandFileName(ExpandConstant('{app}')))) <> 0 then begin
      Result := 'An existing BioPager installation was found in another folder. Run Setup without /DIR to upgrade it in place.'; exit;
    end;
  end;
  StrToVersion('{#AppVersion}.0', NewVersion);
  Exe := ExpandConstant('{app}\BioPager.exe');
  if GetPackedVersion(Exe, OldVersion) then begin
    if ComparePackedVersion(OldVersion, NewVersion) > 0 then begin
      Result := 'A newer BioPager is already installed. This older installer will not replace it.'; exit;
    end;
  end;
  if RegQueryStringValue(HKCU, UninstallKey, 'DisplayVersion', RegistryVersion) then
    if StrToVersion(RegistryVersion, OldVersion) then
      if ComparePackedVersion(OldVersion, NewVersion) > 0 then begin
        Result := 'A newer BioPager is registered. Downgrade was stopped.'; exit;
      end;
  ExtractTemporaryFile('BioPagerSetupHelper.exe');
  if not Exec(ExpandConstant('{tmp}\BioPagerSetupHelper.exe'), '--close', '', SW_HIDE, ewWaitUntilTerminated, ExitCode) then begin
    Result := 'Could not prepare BioPager for upgrade. Please close BioPager and try again.'; exit;
  end;
  if (ExitCode <> 0) or CheckForMutexes('Local\VernStylePager_01') then
    Result := 'BioPager is still running. Exit it from its right-click menu, then retry. Setup has not replaced your files.';
end;
procedure RemoveOurStartup;
var Value: String;
begin
  if RegQueryStringValue(HKCU, RunKey, 'BioPager', Value) then
    if CompareText(Value, '"' + ExpandConstant('{app}\BioPager.exe') + '"') = 0 then
      RegDeleteValue(HKCU, RunKey, 'BioPager');
end;
procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then begin
    if WizardIsTaskSelected('startup') then
      RegWriteStringValue(HKCU, RunKey, 'BioPager', '"' + ExpandConstant('{app}\BioPager.exe') + '"')
    else RemoveOurStartup;
  end;
end;
function InitializeUninstall: Boolean;
var ExitCode: Integer; HelperPath: String;
begin
  Result := True;
  ExitCode := -1;
  HelperPath := ExpandConstant('{app}\BioPagerSetupHelper.exe');
  if FileExists(HelperPath) then
    Result := Exec(HelperPath, '--close-dir "' + ExpandConstant('{app}') + '"', '', SW_HIDE, ewWaitUntilTerminated, ExitCode) and (ExitCode = 0);
  if not Result then
    SuppressibleMsgBox('Please exit BioPager before uninstalling. Your preferences will be kept.', mbError, MB_OK, IDOK);
end;
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then RemoveOurStartup;
  { Never delete LocalAppData\BioPager, user documents, or loose portable copies. }
end;
