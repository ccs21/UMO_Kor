$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$testRoot = Join-Path $repo ('outputs/yami-powershell-test-' + [Guid]::NewGuid().ToString('N'))
try {
    & (Join-Path $PSScriptRoot 'Install-YamiQrayDlc.ps1') -GameDirectory $testRoot
    $dlc = Join-Path $testRoot 'Data/dlc'
    $dirs = @(Get-ChildItem -LiteralPath $dlc -Directory)
    if ($dirs.Count -ne 5 -or @($dirs | Where-Object { !$_.Name.StartsWith('_yamiQray_') }).Count) { throw 'Default OFF test failed' }
    Move-Item -LiteralPath (Join-Path $dlc '_yamiQray_freyja') -Destination (Join-Path $dlc 'yamiQray_freyja')
    $before = (Get-FileHash -LiteralPath (Join-Path $dlc 'yamiQray_freyja/dlc.json')).Hash
    & (Join-Path $PSScriptRoot 'Install-YamiQrayDlc.ps1') -GameDirectory $testRoot
    if (Test-Path -LiteralPath (Join-Path $dlc '_yamiQray_freyja')) { throw 'Existing ON state changed' }
    if (!(Test-Path -LiteralPath (Join-Path $dlc '_yamiQray_reina'))) { throw 'Existing OFF state changed' }
    if ((Get-FileHash -LiteralPath (Join-Path $dlc 'yamiQray_freyja/dlc.json')).Hash -ne $before) { throw 'Existing package changed' }
    Write-Output 'YamiQray PowerShell tests passed: five defaults OFF, repeat install, ON/OFF preserved.'
} finally {
    $resolved = [IO.Path]::GetFullPath($testRoot)
    if (!$resolved.StartsWith((Join-Path $repo 'outputs') + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid test path' }
    if (Test-Path -LiteralPath $resolved) { Remove-Item -LiteralPath $resolved -Recurse -Force }
}
