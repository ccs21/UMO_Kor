param([string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if (!$OutputDirectory) { $OutputDirectory = Join-Path $repo 'Unity/Build/Windows/UMO_Kor' }
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
if (!(Test-Path -LiteralPath $compiler)) { throw '.NET Framework C# compiler not found.' }
$output = Join-Path $OutputDirectory 'UMO_PC_Settings.exe'
& $compiler /nologo /target:winexe /optimize+ /codepage:65001 /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll "/out:$output" (Join-Path $PSScriptRoot 'PcSettingsForm.cs') (Join-Path $PSScriptRoot 'PcTestResources.cs') (Join-Path $repo 'Unity/Assets/Scripts/UMOPcSettings.cs')
if ($LASTEXITCODE -ne 0) { throw 'PC settings utility build failed.' }
Write-Output "Built: $output"
