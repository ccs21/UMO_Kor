param(
    [string]$Unity = 'C:\Program Files\Unity\Hub\Editor\2018.4.36f1\Editor\Unity.exe',
    [string]$Output
)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$project = Join-Path $repo 'Unity'
if (!$Output) { $Output = Join-Path $project 'Build/Windows/UMO_Kor/UMO_Kor.exe' }
$Output = [IO.Path]::GetFullPath($Output)
$logDir = Join-Path $repo 'Logs'
New-Item -ItemType Directory -Path $logDir -Force | Out-Null
foreach ($step in @(@('PrepareResources','windows-prepare.log'), @('BuildRelease','windows-build.log'))) {
    $log = Join-Path $logDir $step[1]
    $process = Start-Process -FilePath $Unity -WindowStyle Hidden -PassThru -ArgumentList @('-batchmode','-quit','-projectPath',('"' + $project + '"'),'-buildTarget','Win64','-executeMethod',('UMOKoreanWindowsBuild.' + $step[0]),'-umoOutput',('"' + $Output + '"'),'-logFile',('"' + $log + '"'))
    $process.WaitForExit()
    if ($process.ExitCode -ne 0) { throw "Unity failed: $log" }
}
if (!(Select-String -LiteralPath (Join-Path $logDir 'windows-build.log') -SimpleMatch 'UMO Korean Windows build: result=Succeeded, errors=0')) { throw 'Unity did not report a successful build.' }
& (Join-Path $repo 'Tools/Windows/Build-PcSettings.ps1') -OutputDirectory ([IO.Path]::GetDirectoryName($Output))
Write-Output "Build complete: $Output"
