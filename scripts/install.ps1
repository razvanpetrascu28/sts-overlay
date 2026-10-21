param([string]$GameDir)
. "$PSScriptRoot\common.ps1"
Assert-GameClosed
$game = Find-Game $GameDir
$dll = Join-Path $ProjectRoot 'dist\STSOverlay\STSOverlay.dll'
if (-not (Test-Path -LiteralPath $dll)) { throw 'Run scripts\build.ps1 first.' }
$target = Join-Path $game 'mods\STSOverlay'
New-Item -ItemType Directory -Path $target -Force | Out-Null
Copy-Item -LiteralPath $dll -Destination $target -Force
Copy-Item -LiteralPath (Join-Path $ProjectRoot 'STSOverlay.json') -Destination $target -Force
Write-Output "Installed Spire Stats in $target"
