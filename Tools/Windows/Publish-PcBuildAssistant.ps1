param([string]$OutputDirectory)

$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if (!$OutputDirectory) { $OutputDirectory = Join-Path $repo 'outputs/PcBuildAssistant' }
$packageDirectory = Join-Path $OutputDirectory 'UMO_PC_Build_Assistant'
New-Item -ItemType Directory -Path $packageDirectory -Force | Out-Null

& (Join-Path $PSScriptRoot 'Build-PcBuildAssistant.ps1') -OutputDirectory $packageDirectory

Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'PC_BUILD_ASSISTANT.md') -Destination (Join-Path $packageDirectory 'README_KO.md') -Force
$zip = Join-Path $OutputDirectory 'UMO_PC_Build_Assistant.zip'
Compress-Archive -Path (Join-Path $packageDirectory '*') -DestinationPath $zip -CompressionLevel Optimal -Force
Write-Output "Published: $zip"
