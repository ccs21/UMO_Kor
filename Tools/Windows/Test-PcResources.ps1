$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$output = Join-Path $repo 'Unity/Build/PcSettingsTests'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$exe = Join-Path $output 'PcResources.exe'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
& $compiler /nologo /codepage:65001 /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll "/out:$exe" (Join-Path $PSScriptRoot 'TestPcResources.cs') (Join-Path $PSScriptRoot 'PcTestResources.cs')
if ($LASTEXITCODE -ne 0) { throw 'Resource test compilation failed.' }
& $exe (Join-Path $PSScriptRoot 'resource-test-fixture.json')
if ($LASTEXITCODE -ne 0) { throw 'Resource tests failed.' }
