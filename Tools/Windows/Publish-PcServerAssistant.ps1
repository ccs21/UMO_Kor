param([string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if (!$OutputDirectory) { $OutputDirectory = Join-Path $repo 'outputs/PcServerAssistant' }
$packageDirectory = Join-Path $OutputDirectory 'UMO_PC_Server'
New-Item -ItemType Directory -Path $packageDirectory -Force | Out-Null
& (Join-Path $PSScriptRoot 'Build-PcServerAssistant.ps1') -OutputDirectory $packageDirectory
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'PC_SERVER.md') -Destination (Join-Path $packageDirectory 'README_KO.md') -Force
$zip = Join-Path $OutputDirectory 'UMO_PC_Server.zip'
$packageFiles = @(
    (Join-Path $packageDirectory 'UMO_PC_Server.exe'),
    (Join-Path $packageDirectory 'UMO_PC_Server.exe.config'),
    (Join-Path $packageDirectory 'README_KO.md')
)
Compress-Archive -LiteralPath $packageFiles -DestinationPath $zip -CompressionLevel Optimal -Force
Write-Output "Published: $zip"
