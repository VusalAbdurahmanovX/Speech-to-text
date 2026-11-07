param([switch]$Benchmark, [string]$InputFile = "$PSScriptRoot\sample_1min.mp3")
$ErrorActionPreference = 'Stop'
& "$PSScriptRoot\build.ps1"
$exe = "$PSScriptRoot\src\SubtitleStudio\bin\Release\net8.0-windows\SubtitleStudio.exe"
if ($Benchmark) {
    $process = Start-Process -FilePath $exe -ArgumentList @('--benchmark', ('"' + $InputFile + '"')) -PassThru -Wait -WindowStyle Hidden
    if ($process.ExitCode -ne 0) { throw "Benchmark failed. Read $PSScriptRoot\artifacts\benchmark-error.txt" }
    Get-ChildItem "$PSScriptRoot\artifacts" -Filter benchmark.json -Recurse | Sort-Object LastWriteTime -Descending | Select-Object -First 1 | Get-Content
} else {
    Start-Process -FilePath $exe
}
