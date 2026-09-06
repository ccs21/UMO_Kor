param(
    [Parameter(Mandatory=$true)][string]$Apk,
    [Parameter(Mandatory=$true)][string]$WindowsGuidePdf,
    [Parameter(Mandatory=$true)][string]$AndroidGuidePng,
    [string]$DateStamp = '20260907',
    [string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if (!$OutputDirectory) { $OutputDirectory = Join-Path $repo 'outputs/Release' }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

$required = @($Apk, $WindowsGuidePdf, $AndroidGuidePng)
foreach ($file in $required) { if (!(Test-Path -LiteralPath $file -PathType Leaf)) { throw "Required release file not found: $file" } }

$staging = Join-Path $OutputDirectory ('.staging-' + [Guid]::NewGuid().ToString('N'))
$windowsStage = Join-Path $staging 'windows'
$androidStage = Join-Path $staging 'android'
New-Item -ItemType Directory -Path $windowsStage,$androidStage -Force | Out-Null
try {
    & (Join-Path $repo 'Tools/Windows/Build-PcBuildAssistant.ps1') -OutputDirectory $windowsStage
    Copy-Item -LiteralPath (Join-Path $repo 'Tools/Windows/PC_BUILD_ASSISTANT.md') -Destination (Join-Path $windowsStage 'README_KO.md') -Force
    Copy-Item -LiteralPath $WindowsGuidePdf -Destination (Join-Path $windowsStage '우타마크로스 오프라인 PC 빌드 도우미 사용방법.pdf') -Force

    & (Join-Path $repo 'Tools/Windows/Build-PcServerAssistant.ps1') -OutputDirectory $androidStage
    Copy-Item -LiteralPath (Join-Path $repo 'Tools/Windows/PC_SERVER.md') -Destination (Join-Path $androidStage 'README_KO.md') -Force
    Copy-Item -LiteralPath $Apk -Destination (Join-Path $androidStage (Split-Path $Apk -Leaf)) -Force
    Copy-Item -LiteralPath $AndroidGuidePng -Destination (Join-Path $androidStage '다운 받아야 하는 파일은 이 두개 입니다.png') -Force

    $windowsZip = Join-Path $OutputDirectory ("UMO_Kor_For_windows_${DateStamp}.zip")
    $androidZip = Join-Path $OutputDirectory ("UMO_Kor_For_Android_${DateStamp}.zip")
    Compress-Archive -Path (Join-Path $windowsStage '*') -DestinationPath $windowsZip -CompressionLevel Optimal -Force
    Compress-Archive -Path (Join-Path $androidStage '*') -DestinationPath $androidZip -CompressionLevel Optimal -Force

    foreach ($zipPath in @($windowsZip, $androidZip)) {
        if (!(Test-Path -LiteralPath $zipPath) -or (Get-Item -LiteralPath $zipPath).Length -lt 10000) { throw "Release ZIP verification failed: $zipPath" }
    }
    Write-Output $windowsZip
    Write-Output $androidZip
} finally {
    $safeRoot = $OutputDirectory.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if ($staging.StartsWith($safeRoot, [StringComparison]::OrdinalIgnoreCase) -and (Split-Path $staging -Leaf).StartsWith('.staging-')) {
        Remove-Item -LiteralPath $staging -Recurse -Force -ErrorAction SilentlyContinue
    }
}
