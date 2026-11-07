param([switch]$Test)
$ErrorActionPreference = 'Stop'
dotnet build "$PSScriptRoot\src\SubtitleStudio\SubtitleStudio.csproj" -c Release
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
if ($Test) {
    dotnet run --project "$PSScriptRoot\tests\CoreChecks.csproj" -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Checks failed.' }
}
