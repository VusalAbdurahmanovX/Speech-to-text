param([ValidateSet('tiny','medium','large-v1','large-v2','large-v3','large-v3-turbo')][string]$Model = 'large-v3-turbo')
$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$toolsFolder = Join-Path $PSScriptRoot 'tools'
$modelsFolder = Join-Path $PSScriptRoot 'models'
New-Item -ItemType Directory -Force $toolsFolder,$modelsFolder | Out-Null
$exe = Join-Path $toolsFolder 'whisper\Release\whisper-cli.exe'
if (-not (Test-Path -LiteralPath $exe)) {
    $release = Invoke-RestMethod 'https://api.github.com/repos/ggml-org/whisper.cpp/releases/tags/b5454'
    $asset = $release.assets | Where-Object name -eq 'whisper-bin-win-cuda-12.4.0-x64.zip'
    if (-not $asset) { throw 'Official CUDA asset not found.' }
    $zip = Join-Path $toolsFolder 'whisper-cuda.zip'
    & curl.exe -L --fail --retry 2 -o $zip $asset.browser_download_url
    if ($LASTEXITCODE -ne 0) { throw 'CUDA package download failed.' }
    if ((Get-Item -LiteralPath $zip).Length -ne $asset.size) { throw 'CUDA package size mismatch.' }
    if ($asset.digest -like 'sha256:*') {
        if ((Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash -ne $asset.digest.Substring(7)) { throw 'CUDA package SHA-256 mismatch.' }
    }
    Expand-Archive -LiteralPath $zip -DestinationPath (Join-Path $toolsFolder 'whisper') -Force
}
if (-not (Test-Path -LiteralPath $exe)) { throw 'Expected Release\whisper-cli.exe is missing.' }
& $exe --help 2>&1 | Out-Host
$metadata = Invoke-RestMethod 'https://huggingface.co/api/models/ggerganov/whisper.cpp/tree/main?limit=1000'
$name = @("ggml-$Model-q5_0.bin", "ggml-$Model-q5_1.bin", "ggml-$Model.bin") | Where-Object { $_ -in $metadata.path } | Select-Object -First 1
if (-not $name) { throw "No supported official download is available for $Model." }
$modelPath = Join-Path $modelsFolder $name
$entry = $metadata | Where-Object path -eq $name
if (-not $entry.lfs.oid) { throw 'Official model checksum not found.' }
if (-not (Test-Path -LiteralPath $modelPath)) {
    $partial = "$modelPath.download"
    & curl.exe -L --fail --retry 2 -o $partial "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/$name"
    if ($LASTEXITCODE -ne 0) { throw 'Model download failed.' }
    if ((Get-FileHash -LiteralPath $partial -Algorithm SHA256).Hash -ne $entry.lfs.oid) { throw 'Model checksum mismatch.' }
    Move-Item -LiteralPath $partial -Destination $modelPath
}
if ((Get-FileHash -LiteralPath $modelPath -Algorithm SHA256).Hash -ne $entry.lfs.oid) { throw 'Installed model checksum mismatch.' }
if (-not (Get-Command ffmpeg.exe -ErrorAction SilentlyContinue)) { Write-Warning 'Install FFmpeg or set its full path in Settings.' }
& nvidia-smi.exe --query-gpu=name,driver_version --format=csv,noheader
if ($LASTEXITCODE -ne 0) { throw 'NVIDIA driver unavailable. CUDA requires a working nvidia-smi.' }
Write-Host 'Ready. Run .\run.ps1. Select your downloaded model in Settings if needed.'
