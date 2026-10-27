# Run only on an isolated Windows CI runner. Touches this runner's HKCU and test profile.
[CmdletBinding()]
param([string]$Version='1.1.0')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$setup=Join-Path $root "dist/BioPager-Setup-$Version.exe"
$app=Join-Path $env:LOCALAPPDATA 'Programs\BioPager'
$prefs=Join-Path $env:LOCALAPPDATA 'BioPager'
$arp='HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\JongBhak.BioPager_is1'
$run='HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$menu=Join-Path ([Environment]::GetFolderPath('Programs')) 'BioPager'
$desktop=Join-Path ([Environment]::GetFolderPath('Desktop')) 'BioPager.lnk'
$work=Join-Path $env:RUNNER_TEMP ('BioPagerInstallerTest-'+[guid]::NewGuid().ToString('N'))
$evidence=Join-Path $root 'installer-evidence'
$processes=[System.Collections.Generic.List[System.Diagnostics.Process]]::new()
$count=0
function Check([bool]$Condition,[string]$Message){if(!$Condition){throw $Message};$script:count++;Write-Host "PASS: $Message"}
function RunSetup([string]$File,[string]$Extra,[string]$Log,[bool]$MustSucceed=$true){
 $p=Start-Process -FilePath $File -ArgumentList "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP- /LOG=`"$evidence\$Log.log`" $Extra" -PassThru
 $handle=$p.Handle
 if(!$p.WaitForExit(90000)){throw "Installer timed out: $Log"}
 $p.Refresh();$code=$p.ExitCode
 if($MustSucceed -and $code -ne 0){throw "Installer failed with exit $code : $Log"}
 return $code
}
function Uninstall {
 $exe=Join-Path $app 'unins000.exe'
 if(Test-Path $exe){[void](RunSetup $exe '' 'uninstall');Start-Sleep -Milliseconds 600}
}
function StartFixture([string]$Path,[string]$Extra='') {
 $p=Start-Process -FilePath $Path -ArgumentList "`"$work\closed.txt`" $Extra" -PassThru
 $processes.Add($p)
 for($i=0;$i -lt 40;$i++){Start-Sleep -Milliseconds 100;$p.Refresh();if($p.MainWindowHandle -ne 0){return $p}}
 throw 'Fixture did not show its window.'
}
function StartupValue {
 try{return (Get-ItemProperty -Path $run -Name BioPager -ErrorAction Stop).BioPager}catch{return $null}
}
# Refuse to run destructive smoke tests on a real user's existing installation.
if((Test-Path $arp) -or (Test-Path $app) -or (Test-Path $prefs) -or (Test-Path $menu) -or (Test-Path $desktop) -or (StartupValue)) { throw 'Installer tests require a clean isolated profile.' }
New-Item -ItemType Directory -Path $work,$evidence,$prefs -Force | Out-Null
$settings=Join-Path $prefs 'settings.txt'
$settingsText="132`r`n74`r`nFalse`r`nFalse`r`n0`r`n0`r`nTrue`r`nFalse`r`nFalse`r`nFalse`r`n200`r`n-13170420`r`nTrue`r`nTrue`r`n30`r`n"
[IO.File]::WriteAllText($settings,$settingsText)
$payloadHash=(Get-FileHash (Join-Path $root 'src/BioPager.exe')).Hash
try {
 $csc=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
 $old=Join-Path $work 'old';$unrelated=Join-Path $work 'unrelated';$portable=Join-Path $work 'portable'
 New-Item -ItemType Directory -Path $old,$unrelated,$portable | Out-Null
 & $csc /nologo /warnaserror+ /target:winexe /platform:x64 /reference:System.Windows.Forms.dll "/out:$old\BioPager.exe" (Join-Path $PSScriptRoot 'InstallerFixture.cs')
 if($LASTEXITCODE -ne 0){throw 'Fixture compilation failed.'}
 & $csc /nologo /warnaserror+ /define:UNRELATED /target:winexe /platform:x64 /reference:System.Windows.Forms.dll "/out:$unrelated\BioPager.exe" (Join-Path $PSScriptRoot 'InstallerFixture.cs')
 if($LASTEXITCODE -ne 0){throw 'Unrelated fixture compilation failed.'}
 & (Join-Path $PSScriptRoot 'BuildInstaller.ps1') -Version '1.0.0' -PayloadDir $old -OutputDir $old
 $oldSetup=Join-Path $old 'BioPager-Setup-1.0.0.exe'
 # A current-session unrelated process with the same filename/title is not closed.
 $other=StartFixture "$unrelated\BioPager.exe"
 Copy-Item "$old\BioPager.exe" "$portable\BioPager.exe"
 $legacy=StartFixture "$portable\BioPager.exe"
 [void](RunSetup $setup '/TASKS=""' 'clean-portable-migration')
 Check ($legacy.WaitForExit(3000)) 'existing portable BioPager closes gracefully during installation'
 $other.Refresh();Check (!$other.HasExited) 'unrelated same-name executable is not closed'
 Check (Test-Path "$work\closed.txt") 'graceful shutdown event ran rather than a forced termination'
 Check (Test-Path "$portable\BioPager.exe") 'loose portable files outside the managed folder are not deleted'
 Check (Test-Path "$app\BioPager.exe") 'clean installation uses LocalAppData Programs BioPager'
 Check ((Get-Item "$app\BioPager.exe").VersionInfo.FileVersion -eq "$Version.0") 'installed executable has the expected version'
 Check ((Get-FileHash "$app\BioPager.exe").Hash -eq $payloadHash) 'installed executable exactly matches the built payload'
 Check (Test-Path "$app\QuickStart.html") 'offline Quick Start guide is installed'
 Check (Test-Path "$menu\BioPager.lnk") 'Start menu application shortcut is created'
 Check (Test-Path "$menu\BioPager Quick Start.lnk") 'Start menu guide shortcut is created'
 Check ((Get-ItemProperty $arp).DisplayVersion -eq $Version) 'Windows uninstall entry records the installed version'
 Check (!(StartupValue)) 'sign-in startup is opt-in on a clean installation'
 Check (!(Test-Path $desktop)) 'desktop shortcut is opt-in on a clean installation'
 Check ([IO.File]::ReadAllText($settings) -eq $settingsText) 'portable settings are preserved byte-for-byte'
 $sentinel=Join-Path $app 'user-note.keep';Set-Content $sentinel 'do not remove'
 [void](RunSetup $setup '/TASKS="startup,desktopicon"' 'same-version-repair')
 Check ((StartupValue) -eq "`"$app\BioPager.exe`"") 'optional startup entry targets the installed executable'
 Check (Test-Path $desktop) 'optional desktop shortcut is created'
 Check (Test-Path $sentinel) 'reinstall does not remove unknown user files'
 $keys=@(Get-ChildItem 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall' | Where-Object { $_.PSChildName -eq 'JongBhak.BioPager_is1' })
 Check ($keys.Count -eq 1) 'repair keeps a single uninstall registration'
 Check ((Get-ChildItem $app -Filter 'unins*.exe').Count -eq 1) 'repair keeps a single uninstaller'
 Uninstall
 Check (!(Test-Path "$app\BioPager.exe")) 'uninstaller removes the managed application'
 Check (!(Test-Path $arp)) 'uninstaller removes its Apps registration'
 Check (!(StartupValue)) 'uninstaller removes its own startup entry'
 Check (!(Test-Path "$menu\BioPager.lnk") -and !(Test-Path $desktop)) 'uninstaller removes managed shortcuts'
 Check ([IO.File]::ReadAllText($settings) -eq $settingsText) 'uninstall retains user preferences'
 Check (Test-Path $sentinel) 'uninstall leaves unknown files untouched'
 Remove-Item $sentinel;Remove-Item $app -ErrorAction SilentlyContinue
 # Real lower-version setup fixture to new production payload.
 [void](RunSetup $oldSetup '/TASKS="startup"' 'install-previous-version')
 Check ((Get-Item "$app\BioPager.exe").VersionInfo.FileVersion -eq '1.0.0.0') 'older installed-version fixture is present'
 $blocked=StartFixture "$app\BioPager.exe" '--block'
 $code=RunSetup $setup '' 'blocked-graceful-close' $false
 Check ($code -ne 0) 'upgrade aborts when the old app refuses a graceful close'
 Check ((Get-Item "$app\BioPager.exe").VersionInfo.FileVersion -eq '1.0.0.0') 'failed shutdown leaves the old executable intact'
 $blocked.Refresh();Check (!$blocked.HasExited) 'installer never force-kills a refusing application'
 Stop-Process -Id $blocked.Id -Force;[void]$blocked.WaitForExit(3000)
 $running=StartFixture "$app\BioPager.exe"
 [void](RunSetup $setup '' 'upgrade')
 Check ($running.WaitForExit(3000)) 'upgrade gracefully closes a running installed older version'
 Check ((Get-FileHash "$app\BioPager.exe").Hash -eq $payloadHash) 'upgrade replaces the older program with the new payload'
 Check ((StartupValue) -eq "`"$app\BioPager.exe`"") 'upgrade preserves the prior startup choice'
 Check ([IO.File]::ReadAllText($settings) -eq $settingsText) 'upgrade preserves layout and app-badge preferences'
 Check ((Get-ChildItem $app -Filter 'unins*.exe').Count -eq 1) 'upgrade does not leave side-by-side uninstallers'
 $code=RunSetup $oldSetup '' 'blocked-downgrade' $false
 Check ($code -ne 0) 'an older installer cannot downgrade the current installation'
 Check ((Get-FileHash "$app\BioPager.exe").Hash -eq $payloadHash) 'downgrade rejection leaves current application bytes unchanged'
 [void](RunSetup $setup '/TASKS=""' 'disable-startup')
 Check (!(StartupValue)) 'rerunning Setup can disable its managed startup entry'
 # Verify helper graceful closing of the actual production pager too.
 $real=Start-Process -FilePath "$app\BioPager.exe" -PassThru;$processes.Add($real)
 Start-Sleep -Seconds 2;$real.Refresh()
 Check (!$real.HasExited) 'installed production BioPager starts successfully'
 [void](RunSetup $setup '/TASKS=""' 'running-production-repair')
 Check ($real.WaitForExit(3000)) 'same-version repair gracefully closes the actual production BioPager'
 Check ((Get-FileHash "$app\BioPager.exe").Hash -eq $payloadHash) 'running-app repair retains the correct production executable'
 Uninstall
 Check (Test-Path $settings) 'final uninstall keeps preferences'
 $other.Refresh();Check (!$other.HasExited) 'unrelated app survives all installer and uninstaller operations'
 Write-Host "Installer integration checks passed: $count"
} finally {
 foreach($p in $processes){try{$p.Refresh();if(!$p.HasExited){Stop-Process -Id $p.Id -Force;[void]$p.WaitForExit(3000)}}catch{}}
 if(Test-Path "$app\unins000.exe"){Uninstall}
 if(Test-Path $work){Remove-Item $work -Recurse -Force}
 if(Test-Path $prefs){Remove-Item $prefs -Recurse -Force}
}
