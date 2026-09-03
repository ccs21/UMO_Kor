param(
    [string]$Unity = 'C:\Program Files\Unity\Hub\Editor\2018.4.36f1\Editor\Unity.exe',
    [Parameter(Mandatory=$true)][string]$Sdk,
    [Parameter(Mandatory=$true)][string]$Ndk,
    [Parameter(Mandatory=$true)][string]$Jdk,
    [Parameter(Mandatory=$true)][string]$SigningDirectory,
    [switch]$InitializeSigning
)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$signing = [IO.Path]::GetFullPath($SigningDirectory)
if ($signing.StartsWith($repo + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or $signing -eq $repo) {
    throw 'Signing material must be kept OUTSIDE the Git repository.'
}
$key = Join-Path $signing 'umo-kor-release.jks'
$secret = Join-Path $signing 'release.password.dpapi'
if ($InitializeSigning) {
    if ((Test-Path -LiteralPath $key) -or (Test-Path -LiteralPath $secret)) { throw 'Signing material already exists. Never replace the release key.' }
    New-Item -ItemType Directory -Path $signing -Force | Out-Null
    # Restrict the signing folder to the current Windows account and SYSTEM.
    $sid = [Security.Principal.WindowsIdentity]::GetCurrent().User.Value
    & icacls.exe $signing /inheritance:r /grant:r "*${sid}:(OI)(CI)F" '*S-1-5-18:(OI)(CI)F' | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Cannot protect signing directory' }
    $random = New-Object byte[] 32
    $rng = [Security.Cryptography.RandomNumberGenerator]::Create()
    $rng.GetBytes($random)
    $rng.Dispose()
    $password = [Convert]::ToBase64String($random)
    $secure = ConvertTo-SecureString $password -AsPlainText -Force
    # DPAPI is tied to this Windows user/machine. See the backup instructions.
    [IO.File]::WriteAllText($secret, (ConvertFrom-SecureString $secure))
    $env:UMO_KEYTOOL_PASSWORD = $password
    try {
        & (Join-Path $Jdk 'bin/keytool.exe') -genkeypair -noprompt -storetype JKS -keystore $key -alias umo-kor-release -keyalg RSA -keysize 4096 -validity 36500 -dname 'CN=UMO Korean Release, O=UMO_Kor, C=KR' -storepass:env UMO_KEYTOOL_PASSWORD -keypass:env UMO_KEYTOOL_PASSWORD
        if ($LASTEXITCODE -ne 0) { throw 'Release key creation failed' }
    } finally { $env:UMO_KEYTOOL_PASSWORD = $null; $password = $null }
}
if (!(Test-Path -LiteralPath $key) -or !(Test-Path -LiteralPath $secret)) { throw 'Missing signing key/password. Initialize once or restore your signing backup.' }
$secure = ConvertTo-SecureString ([IO.File]::ReadAllText($secret))
$credential = New-Object System.Management.Automation.PSCredential('release', $secure)
$env:UMO_ANDROID_SDK = [IO.Path]::GetFullPath($Sdk)
$env:UMO_ANDROID_NDK = [IO.Path]::GetFullPath($Ndk)
$env:UMO_JAVA_HOME = [IO.Path]::GetFullPath($Jdk)
$env:UMO_RELEASE_KEYSTORE = $key
$env:UMO_RELEASE_KEY_ALIAS = 'umo-kor-release'
$env:UMO_RELEASE_STORE_PASS = $credential.GetNetworkCredential().Password
$env:UMO_RELEASE_KEY_PASS = $env:UMO_RELEASE_STORE_PASS
$log = Join-Path $repo 'Logs/android-release.log'
New-Item -ItemType Directory -Path (Split-Path $log) -Force | Out-Null
try {
    # Restore resources in a separate import run before Android's build map is cached.
    $prepareLog = Join-Path $repo 'Logs/android-prepare.log'
    $prepare = Start-Process -FilePath $Unity -WindowStyle Hidden -PassThru -ArgumentList @('-batchmode','-quit','-projectPath',('"' + (Join-Path $repo 'Unity') + '"'),'-buildTarget','Android','-executeMethod','UMOKoreanWindowsBuild.RestoreResources','-logFile',('"' + $prepareLog + '"'))
    $prepare.WaitForExit()
    if ($prepare.ExitCode -ne 0) { throw "Android preparation failed. See $prepareLog" }
    $process = Start-Process -FilePath $Unity -WindowStyle Hidden -PassThru -ArgumentList @('-batchmode','-quit','-projectPath',('"' + (Join-Path $repo 'Unity') + '"'),'-buildTarget','Android','-executeMethod','UMOKoreanAndroidBuild.BuildRelease','-logFile',('"' + $log + '"'))
    $process.WaitForExit()
    if ($process.ExitCode -ne 0) { throw "Android release build failed. See $log" }
    if (!(Select-String -LiteralPath $log -SimpleMatch 'UMO Korean Android build: result=Succeeded, errors=0')) { throw 'Unity did not report a successful release build.' }
    Write-Output "Release build succeeded. Verify APK signature, package, version and debuggable flag before publishing. Log: $log"
} finally {
    $env:UMO_RELEASE_STORE_PASS = $null
    $env:UMO_RELEASE_KEY_PASS = $null
    $credential = $null
}
