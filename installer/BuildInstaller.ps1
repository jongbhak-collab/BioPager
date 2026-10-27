[CmdletBinding()]
param([string]$Version='1.1.0', [string]$PayloadDir, [string]$OutputDir)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
if (!$PayloadDir) { $PayloadDir=Join-Path $root 'src' }
if (!$OutputDir) { $OutputDir=Join-Path $root 'dist' }
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'Use a three-part release version.' }
$exe=Join-Path $PayloadDir 'BioPager.exe'
if (!(Test-Path $exe)) { throw 'Build src/BioPager.exe first with src/Build.cmd.' }
if ((Get-Item $exe).VersionInfo.FileVersion -ne "$Version.0") { throw 'Payload and installer versions must agree.' }
$compiler=Get-Command ISCC.exe -ErrorAction SilentlyContinue
if ($compiler) { $iscc=$compiler.Source } else {
  $iscc=@("${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe", "${env:ProgramFiles(x86)}\Inno Setup 7\ISCC.exe", "$env:ProgramFiles\Inno Setup 6\ISCC.exe", "$env:ProgramFiles\Inno Setup 7\ISCC.exe") | Where-Object { Test-Path $_ } | Select-Object -First 1
}
if (!$iscc) { throw 'Inno Setup 6.3 or later is required on the build machine.' }
$helper=Join-Path $PSScriptRoot 'build'
New-Item -ItemType Directory -Path $helper,$OutputDir -Force | Out-Null
$csc=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $csc /nologo /warnaserror+ /target:winexe /platform:x64 /optimize+ "/out:$helper\BioPagerSetupHelper.exe" (Join-Path $PSScriptRoot 'SetupHelper.cs')
if ($LASTEXITCODE -ne 0) { throw 'Setup helper compilation failed.' }
& $iscc "/DAppVersion=$Version" "/DPayloadDir=$PayloadDir" "/DOutputPath=$OutputDir" "/DHelperDir=$helper" (Join-Path $PSScriptRoot 'BioPager.iss')
if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
$setup=Join-Path $OutputDir "BioPager-Setup-$Version.exe"
(Get-FileHash $setup -Algorithm SHA256).Hash.ToLower() + '  ' + (Split-Path $setup -Leaf) | Set-Content "$setup.sha256" -Encoding ascii
Write-Host "Built $setup"
