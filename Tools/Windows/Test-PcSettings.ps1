$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$output = Join-Path $repo 'Unity/Build/PcSettingsTests'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
foreach ($platform in @('UNITY_STANDALONE_WIN', 'UNITY_ANDROID')) {
    $exe = Join-Path $output ($platform + '.exe')
    & $compiler /nologo /codepage:65001 "/define:$platform" "/out:$exe" (Join-Path $PSScriptRoot 'TestPcSettings.cs') (Join-Path $repo 'Unity/Assets/Scripts/UMOPcSettings.cs') (Join-Path $repo 'Unity/Assets/UMAssets/Scripts/XeApp/Game/RhythmGame/RNoteResultJudge.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }
    & $exe
    if ($LASTEXITCODE -ne 0) { throw "Tests failed for $platform" }
}
