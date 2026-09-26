param([string]$LegacyRoot = (Join-Path ([Environment]::GetFolderPath('Desktop')) 'legacy voidfall'))
$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    & node export-legacy.mjs $LegacyRoot
    if ($LASTEXITCODE -ne 0) { throw 'Legacy source/render export failed.' }
    & python -X utf8 build-legacy.py
    if ($LASTEXITCODE -ne 0) { throw 'Legacy comparison failed.' }
    $env:VOIDFALL_LEGACY_ROOT = $LegacyRoot
    & python -X utf8 validate-legacy.py
    if ($LASTEXITCODE -ne 0) { throw 'Legacy validation failed.' }
    Write-Host 'All five legacy versions refreshed. Reload the dashboard.'
} finally { Pop-Location }
