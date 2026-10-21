$ErrorActionPreference = 'Stop'
$ProjectRoot = Split-Path -Parent $PSScriptRoot

function Find-Game([string]$ExplicitPath) {
    $candidates = @($ExplicitPath, $env:STS2_DIR)
    $steamPaths = @('D:\Games\Steam', 'C:\Program Files (x86)\Steam')
    $steamKey = 'HKCU:\Software\Valve\Steam'
    if (Test-Path -LiteralPath $steamKey) {
        $steamPaths += (Get-ItemProperty -LiteralPath $steamKey -ErrorAction SilentlyContinue).SteamPath
    }
    foreach ($steam in $steamPaths | Where-Object { $_ } | Select-Object -Unique) {
        $candidates += Join-Path $steam 'steamapps\common\Slay the Spire 2'
        $libraries = Join-Path $steam 'steamapps\libraryfolders.vdf'
        if (Test-Path -LiteralPath $libraries) {
            foreach ($line in Get-Content -LiteralPath $libraries) {
                if ($line -match '"path"\s+"([^"]+)"') {
                    $candidates += Join-Path ($Matches[1] -replace '\\\\', '\') 'steamapps\common\Slay the Spire 2'
                }
            }
        }
    }
    foreach ($candidate in $candidates | Where-Object { $_ } | Select-Object -Unique) {
        if (Test-Path -LiteralPath (Join-Path $candidate 'data_sts2_windows_x86_64\sts2.dll')) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }
    }
    throw 'Slay the Spire 2 was not found. Pass -GameDir or set STS2_DIR.'
}

function Assert-GameClosed {
    if (Get-Process -Name SlayTheSpire2 -ErrorAction SilentlyContinue) {
        throw 'Close Slay the Spire 2 before installing or updating the mod.'
    }
}
