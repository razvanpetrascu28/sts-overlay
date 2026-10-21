param([string]$GameDir)
. "$PSScriptRoot\common.ps1"
$game = Find-Game $GameDir
Push-Location $ProjectRoot
try {
    dotnet build STSOverlay.csproj -c Release "-p:Sts2Dir=$game"
    if ($LASTEXITCODE -ne 0) { throw 'Mod build failed.' }
    $package = Join-Path $ProjectRoot 'dist\STSOverlay'
    New-Item -ItemType Directory -Path $package -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $ProjectRoot 'bin\Release\net9.0\STSOverlay.dll') -Destination $package -Force
    Copy-Item -LiteralPath (Join-Path $ProjectRoot 'STSOverlay.json') -Destination $package -Force
    Copy-Item -LiteralPath (Join-Path $ProjectRoot 'README.md') -Destination $package -Force
    Compress-Archive -LiteralPath $package -DestinationPath (Join-Path $ProjectRoot 'dist\STSOverlay.zip') -Force
    Write-Output "Package: $(Join-Path $ProjectRoot 'dist\STSOverlay.zip')"
} finally { Pop-Location }
