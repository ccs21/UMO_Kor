param(
    [Parameter(Mandatory=$true)][string]$Apk,
    [Parameter(Mandatory=$true)][string]$BuildTools,
    [Parameter(Mandatory=$true)][string]$Jdk
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$builder = [IO.File]::ReadAllText((Join-Path $repo 'Unity/Assets/Editor/UMOKoreanAndroidBuild.cs'))
$package = [regex]::Match($builder, 'ReleasePackage = "([^"]+)"').Groups[1].Value
$version = [regex]::Match($builder, 'ReleaseVersion = "([^"]+)"').Groups[1].Value
$code = [regex]::Match($builder, 'ReleaseVersionCode = (\d+)').Groups[1].Value
$expectedCert = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'release-certificate.sha256')).Trim()
$oldJava = $env:JAVA_HOME
try {
    $env:JAVA_HOME = $Jdk
    $signature = & (Join-Path $BuildTools 'apksigner.bat') verify --verbose --print-certs $Apk 2>&1
    if ($LASTEXITCODE -ne 0) { throw 'APK signature verification failed' }
    $cert = [regex]::Match(($signature -join "`n"), 'Signer #1 certificate SHA-256 digest: ([a-fA-F0-9]+)').Groups[1].Value
    if ($cert -ne $expectedCert) { throw 'Unexpected signing certificate. Do not publish this APK.' }
    $badging = & (Join-Path $BuildTools 'aapt.exe') dump badging $Apk
    if ($LASTEXITCODE -ne 0) { throw 'Cannot inspect APK' }
    $packageLine = $badging | Where-Object { $_ -like 'package:*' }
    if (!$packageLine.Contains("name='$package'") -or !$packageLine.Contains("versionCode='$code'") -or !$packageLine.Contains("versionName='$version'")) { throw 'APK identity/version mismatch' }
    if ($badging -match 'application-debuggable') { throw 'Debuggable APK cannot be released' }
    $native = $badging | Where-Object { $_ -like 'native-code:*' }
    if (!$native.Contains("'armeabi-v7a'") -or !$native.Contains("'arm64-v8a'")) { throw 'Missing ARM architecture' }
    $archive = [IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $Apk).Path)
    try {
        if ($archive.Entries.FullName -match '(?i)UtaMacrossDataArchive|WindowsCache|\.jks$|\.keystore$') { throw 'Forbidden content in APK' }
    } finally { $archive.Dispose() }
    [pscustomobject]@{Package=$package;Version=$version;VersionCode=$code;Debuggable=$false;CertificateSHA256=$cert;ApkSHA256=(Get-FileHash -LiteralPath $Apk).Hash;Bytes=(Get-Item -LiteralPath $Apk).Length} | Format-List
} finally { $env:JAVA_HOME = $oldJava }
