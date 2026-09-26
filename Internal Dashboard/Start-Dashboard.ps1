$ErrorActionPreference = 'Stop'
$dashboardUrl = 'http://127.0.0.1:4326'
try { $dashboardResponse = Invoke-WebRequest -Uri $dashboardUrl -UseBasicParsing -TimeoutSec 2 } catch { $dashboardResponse = $null }
if ($dashboardResponse -and $dashboardResponse.Content -notmatch 'Voidfall · Content Library') { throw 'This port is in use by a different app.' }
if (-not $dashboardResponse) {
    $dashboardNode = (Get-Command node.exe -ErrorAction Stop).Source
    Start-Process -FilePath $dashboardNode -ArgumentList @('"' + (Join-Path $PSScriptRoot 'server.mjs') + '"') -WorkingDirectory $PSScriptRoot -WindowStyle Hidden -RedirectStandardOutput (Join-Path $PSScriptRoot 'server-output.txt') -RedirectStandardError (Join-Path $PSScriptRoot 'server-errors.txt')
    for ($dashboardAttempt = 0; $dashboardAttempt -lt 20; $dashboardAttempt++) {
        Start-Sleep -Milliseconds 250
        try { $null = Invoke-WebRequest -Uri $dashboardUrl -UseBasicParsing -TimeoutSec 1; break } catch { }
    }
}
Start-Process $dashboardUrl
