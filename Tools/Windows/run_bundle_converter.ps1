$ErrorActionPreference = 'Stop'
Add-Type -Path (Join-Path $PSScriptRoot '../../Unity/Assets/Scripts/UMOStandaloneBundleConverter.cs')
while ($null -ne ($line = [Console]::ReadLine())) {
    try {
        $bytes = [Convert]::FromBase64String($line)
        $count = 0
        $converted = [UMOStandaloneBundleConverter]::Convert($bytes, 19, [ref]$count)
        [Console]::WriteLine((@{ data = [Convert]::ToBase64String($converted); count = $count } | ConvertTo-Json -Compress))
    } catch {
        [Console]::WriteLine((@{ error = $_.Exception.Message } | ConvertTo-Json -Compress))
    }
}
