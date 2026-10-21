param([string]$GameDir)
. "$PSScriptRoot\common.ps1"
$game = Find-Game $GameDir
Push-Location $ProjectRoot
try {
    $output = Join-Path $ProjectRoot '.local\hook-check'
    dotnet publish tests/hooks/HookChecks.csproj -c Release -r win-x64 --self-contained true -o $output
    if ($LASTEXITCODE -ne 0) { throw 'Hook check build failed.' }
    & (Join-Path $output 'HookChecks.exe') (Join-Path $game 'data_sts2_windows_x86_64') (Join-Path $ProjectRoot 'bin\Release\net9.0\STSOverlay.dll')
    if ($LASTEXITCODE -ne 0) { throw 'Game hook checks failed.' }
} finally { Pop-Location }
