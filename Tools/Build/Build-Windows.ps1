param(
    [string]$Unity = 'C:\Program Files\Unity\Hub\Editor\2018.4.36f1\Editor\Unity.exe',
    [string]$Output,
    [switch]$ImageTranslationDevelopment
)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$project = Join-Path $repo 'Unity'
if (!$Output) { $Output = Join-Path $project 'Build/Windows/UMO_Kor/UMO_Kor.exe' }
$Output = [IO.Path]::GetFullPath($Output)
$logDir = Join-Path $repo 'Logs'
New-Item -ItemType Directory -Path $logDir -Force | Out-Null
$buildMethod = if ($ImageTranslationDevelopment) { 'BuildDevelopment' } else { 'BuildRelease' }
$officialLoginBonusUrl = 'http://umo.xele.org:8000/offcial-login-bonuses_1_Android.zip'
$officialLoginBonusSha256 = '2888712f6b1774542fa4a1c2d6af425ae3616c6ba2a947f81ed5a48428fba4cc'

function Install-OfficialLoginBonus {
    param([string]$GameDirectory)

    $temporaryRoot = Join-Path ([IO.Path]::GetTempPath()) ('umo-login-bonus-' + [Guid]::NewGuid().ToString('N'))
    $archive = Join-Path $temporaryRoot 'offcial-login-bonuses_1_Android.zip'
    $staging = Join-Path $temporaryRoot 'package'
    $dlcRoot = Join-Path $GameDirectory 'Data/dlc'
    $destination = Join-Path $dlcRoot 'offcial-login-bonuses'
    try {
        New-Item -ItemType Directory -Path $temporaryRoot,$staging,$dlcRoot -Force | Out-Null
        Invoke-WebRequest -Uri $officialLoginBonusUrl -OutFile $archive
        $actualHash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash
        if (!$actualHash.Equals($officialLoginBonusSha256, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Official login bonus DLC hash mismatch: $actualHash"
        }
        Expand-Archive -LiteralPath $archive -DestinationPath $staging
        $info = Join-Path $staging 'dlc.json'
        if (!(Test-Path -LiteralPath $info) -or
            !(Get-Content -LiteralPath $info -Raw).Contains('"package_name":"offcial-login-bonuses"')) {
            throw 'Official login bonus DLC package is invalid.'
        }
        if (Test-Path -LiteralPath $destination) {
            Remove-Item -LiteralPath $destination -Recurse -Force
        }
        Move-Item -LiteralPath $staging -Destination $destination
        Write-Output "Official login bonus DLC installed: $destination"
    }
    finally {
        if (Test-Path -LiteralPath $temporaryRoot) {
            Remove-Item -LiteralPath $temporaryRoot -Recurse -Force
        }
    }
}

& (Join-Path $repo 'Tools/Build/Prepare-KoreanImageResources.ps1')
foreach ($step in @(@('PrepareResources','windows-prepare.log'), @($buildMethod,'windows-build.log'))) {
    $log = Join-Path $logDir $step[1]
    $process = Start-Process -FilePath $Unity -WindowStyle Hidden -PassThru -ArgumentList @('-batchmode','-quit','-projectPath',('"' + $project + '"'),'-buildTarget','Win64','-executeMethod',('UMOKoreanWindowsBuild.' + $step[0]),'-umoOutput',('"' + $Output + '"'),'-logFile',('"' + $log + '"'))
    $process.WaitForExit()
    if ($process.ExitCode -ne 0) { throw "Unity failed: $log" }
}
if (!(Select-String -LiteralPath (Join-Path $logDir 'windows-build.log') -SimpleMatch 'UMO Korean Windows build: result=Succeeded, errors=0')) { throw 'Unity did not report a successful build.' }
Install-OfficialLoginBonus -GameDirectory ([IO.Path]::GetDirectoryName($Output))
& (Join-Path $repo 'Tools/Windows/Build-PcSettings.ps1') -OutputDirectory ([IO.Path]::GetDirectoryName($Output))
Write-Output "Build complete: $Output"
