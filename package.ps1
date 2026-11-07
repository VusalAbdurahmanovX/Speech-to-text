param([ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version = '0.1.5', [switch]$SkipInstaller)
$ErrorActionPreference = 'Stop'
$dist = Join-Path $PSScriptRoot 'dist'
$publish = Join-Path $dist "package-$Version-win-x64"
& "$PSScriptRoot\build.ps1" -Test
dotnet publish "$PSScriptRoot\src\SubtitleStudio\SubtitleStudio.csproj" -c Release -r win-x64 --self-contained true -p:Version=$Version -o $publish
if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
Copy-Item -LiteralPath "$PSScriptRoot\LICENSE","$PSScriptRoot\THIRD_PARTY_NOTICES.md","$PSScriptRoot\installer\GETTING_STARTED.md" -Destination $publish
Copy-Item -LiteralPath "$PSScriptRoot\installer\licenses" -Destination $publish -Recurse -Force
$zip = Join-Path $dist "Speech-to-Text-$Version-win-x64.zip"
Compress-Archive -Path "$publish\*" -DestinationPath $zip -Force
if (-not $SkipInstaller) {
    $compiler = Join-Path $PSScriptRoot 'tools\inno\ISCC.exe'
    if (-not (Test-Path -LiteralPath $compiler)) {
        $candidate = Get-Command ISCC.exe -ErrorAction SilentlyContinue
        if ($candidate) { $compiler = $candidate.Source }
        else {
            # Pinned official Inno Setup compiler; installed per user without admin rights.
            $downloadFolder = Join-Path $PSScriptRoot 'tools\inno-download'
            New-Item -ItemType Directory -Force $downloadFolder | Out-Null
            $setup = Join-Path $downloadFolder 'innosetup.exe'
            $expectedHash = '8a5a6711c3766a6e5fb74f280ba0e3551e719ce94538d901fdbd1f8f9920b3f0'
            if (-not (Test-Path -LiteralPath $setup)) {
                & curl.exe -L --fail --retry 2 -o $setup 'https://github.com/jrsoftware/issrc/releases/download/is-7_1_1/innosetup-7.1.1-x64.exe'
                if ($LASTEXITCODE -ne 0) { throw 'Inno Setup download failed.' }
            }
            if ((Get-FileHash -LiteralPath $setup -Algorithm SHA256).Hash -ne $expectedHash) { throw 'Inno Setup checksum mismatch.' }
            $target = Join-Path $PSScriptRoot 'tools\inno'
            $process = Start-Process -FilePath $setup -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/CURRENTUSER','/NOICONS',('/DIR="' + $target + '"')) -WindowStyle Hidden -Wait -PassThru
            if ($process.ExitCode -ne 0) { throw "Inno Setup installation failed: $($process.ExitCode)" }
        }
    }
    if (-not (Test-Path -LiteralPath $compiler)) { throw 'ISCC.exe not found after installation.' }
    & $compiler "/DAppVersion=$Version" "/DPublishDir=$publish" "/DOutputDir=$dist" "$PSScriptRoot\installer\SubtitleStudio.iss"
    if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
}
$packages = @($zip)
if (-not $SkipInstaller) { $packages += Join-Path $dist "Speech-to-Text-Setup-$Version-win-x64.exe" }
Get-FileHash -LiteralPath $packages -Algorithm SHA256 | ForEach-Object { "$($_.Hash.ToLowerInvariant())  $([IO.Path]::GetFileName($_.Path))" } | Set-Content (Join-Path $dist 'SHA256SUMS.txt') -Encoding ascii
Write-Host "Packages ready in $dist"
