param([switch]$Clean)

$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$source = Join-Path $repo 'Localization/ImageOverrides/PC'
$manifestPath = Join-Path $repo 'Localization/ImageOverrides/manifest.json'
$destination = Join-Path $repo 'Unity/Assets/Resources/KoreanImageOverrides'

if ($Clean) {
    if (Test-Path -LiteralPath $destination) {
        $resolved = [IO.Path]::GetFullPath($destination)
        $expected = [IO.Path]::GetFullPath((Join-Path $repo 'Unity/Assets/Resources/KoreanImageOverrides'))
        if ($resolved -ne $expected) { throw 'Unexpected generated image resource path.' }
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
    Write-Output 'Removed generated Korean image resources.'
    exit 0
}

if (!(Test-Path -LiteralPath $manifestPath -PathType Leaf)) { throw 'Korean image manifest is missing.' }
$manifest = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json
if (@($manifest.images).Count -ne 43) { throw "Expected 43 Korean images, found $(@($manifest.images).Count)." }

New-Item -ItemType Directory -Path $destination -Force | Out-Null
$expectedFiles = @('manifest.bytes')
foreach ($image in $manifest.images) {
    $input = Join-Path $source $image.file
    if (!(Test-Path -LiteralPath $input -PathType Leaf)) { throw "Korean image is missing: $($image.file)" }
    $actualHash = (Get-FileHash -LiteralPath $input -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actualHash -ne $image.translatedPngSha256) { throw "Korean image hash mismatch: $($image.file)" }
    $outputName = $image.resourceId + '.bytes'
    Copy-Item -LiteralPath $input -Destination (Join-Path $destination $outputName) -Force
    $expectedFiles += $outputName
}
Copy-Item -LiteralPath $manifestPath -Destination (Join-Path $destination 'manifest.bytes') -Force

Get-ChildItem -LiteralPath $destination -File | Where-Object { $expectedFiles -notcontains $_.Name -and $_.Name -notlike '*.meta' } | Remove-Item -Force
Write-Output "Prepared Korean image resources: $(@($manifest.images).Count)"
