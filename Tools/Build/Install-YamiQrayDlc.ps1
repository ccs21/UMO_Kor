param([Parameter(Mandatory=$true)][string]$GameDirectory)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$dlcRoot = Join-Path ([IO.Path]::GetFullPath($GameDirectory)) 'Data/dlc'
New-Item -ItemType Directory -Path $dlcRoot -Force | Out-Null
foreach ($member in @('freyja','mikumo','kaname','makina','reina')) {
    $package = 'yamiQray_' + $member
    $enabled = Join-Path $dlcRoot $package
    $disabled = Join-Path $dlcRoot ('_' + $package)
    if ((Test-Path -LiteralPath (Join-Path $enabled 'dlc.json')) -or
        (Test-Path -LiteralPath (Join-Path $disabled 'dlc.json'))) { continue }
    $archive = Join-Path $repo ('Unity/Assets/Resources/BundledDlc/' + $package + '_1_Android.bytes')
    $staging = Join-Path $dlcRoot ('.' + $package + '-' + [Guid]::NewGuid().ToString('N'))
    try {
        [IO.Compression.ZipFile]::ExtractToDirectory($archive, $staging)
        $info = Get-Content -LiteralPath (Join-Path $staging 'dlc.json') -Raw | ConvertFrom-Json
        if ($info.package_name -ne $package -or $info.version -ne 1) { throw "Invalid DLC: $package" }
        Move-Item -LiteralPath $staging -Destination $disabled
        Write-Output "Installed optional DLC (OFF): $package"
    } finally {
        $resolved = [IO.Path]::GetFullPath($staging)
        if (!$resolved.StartsWith([IO.Path]::GetFullPath($dlcRoot) + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid staging path' }
        if (Test-Path -LiteralPath $resolved) { Remove-Item -LiteralPath $resolved -Recurse -Force }
    }
}
