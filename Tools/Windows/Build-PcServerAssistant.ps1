param([string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if (!$OutputDirectory) { $OutputDirectory = Join-Path $repo 'BuildTools/UMO_PC_Server' }
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
if (!(Test-Path -LiteralPath $compiler)) { throw '.NET Framework C# compiler not found.' }
$output = Join-Path $OutputDirectory 'UMO_PC_Server.exe'
$manifest = Join-Path $PSScriptRoot 'PcServerAssistant.exe.manifest'
& $compiler /nologo /target:winexe /main:PcServerAssistantForm /optimize+ /codepage:65001 "/win32manifest:$manifest" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll "/out:$output" (Join-Path $PSScriptRoot 'PcServerAssistantForm.cs')
if ($LASTEXITCODE -ne 0) { throw 'PC server compilation failed.' }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'PcServerAssistant.exe.config') -Destination ($output + '.config') -Force
Write-Output "Built: $output"
