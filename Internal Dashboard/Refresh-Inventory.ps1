$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    & python -X utf8 build.py --refresh
    if ($LASTEXITCODE -ne 0) { throw 'Inventory export failed.' }
    & python -X utf8 validate.py
    if ($LASTEXITCODE -ne 0) { throw 'Inventory validation failed.' }
    if (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'legacy-raw.json')) {
        & python -X utf8 build-legacy.py
        if ($LASTEXITCODE -ne 0) { throw 'Legacy/current comparison update failed.' }
        & python -X utf8 validate-legacy.py
        if ($LASTEXITCODE -ne 0) { throw 'Legacy comparison validation failed.' }
    }
    Write-Host 'Unity inventory and legacy comparison refreshed. Reload the dashboard.'
} finally { Pop-Location }
