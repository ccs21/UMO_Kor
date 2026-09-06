$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$testExe = Join-Path $env:TEMP ('umo-pc-server-test-' + [guid]::NewGuid().ToString('N') + '.exe')
try {
    & $compiler /nologo /target:exe /main:PcServerSelfTest /optimize+ /codepage:65001 /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll "/out:$testExe" (Join-Path $PSScriptRoot 'PcServerAssistantForm.cs') (Join-Path $PSScriptRoot 'PcServerAssistantSelfTest.cs')
    if ($LASTEXITCODE -ne 0) { throw 'PC server self-test compilation failed.' }
    & $testExe
    if ($LASTEXITCODE -ne 0) { throw 'PC server self-test failed.' }
} finally {
    if (Test-Path -LiteralPath $testExe) { Remove-Item -LiteralPath $testExe -Force }
}
