$ErrorActionPreference = 'Stop'
Push-Location (Split-Path -Parent $PSScriptRoot)
try {
    dotnet run --project tests/StatsTests.csproj -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Statistics checks failed.' }
} finally { Pop-Location }
