param(
    [Parameter(Mandatory = $true)][string]$PlayerPath,
    [Parameter(Mandatory = $true)][string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
$player = (Resolve-Path -LiteralPath $PlayerPath).Path
$output = [IO.Path]::GetFullPath($OutputDirectory)
[IO.Directory]::CreateDirectory($output) | Out-Null
$logPath = Join-Path $output 'player.log'
# Unique evidence prevents a previous passing report from certifying this run.
$runId = [Guid]::NewGuid().ToString('N')
$reportPath = Join-Path $output "smoke-$runId.json"
$profilePath = Join-Path $output "profile-$runId.json"
$arguments = @('-batchmode', '-nographics', '-vfbench', '-vfscenario=productionMax',
    '-vfseed=1595785438', '-vfwarmup=2', '-vfmeasure=8',
    "-vfoutput=`"$reportPath`"", "-vfprofile=`"$profilePath`"", '-logFile', "`"$logPath`"")
$process = Start-Process -FilePath $player -ArgumentList $arguments -WindowStyle Hidden -PassThru
if (-not $process.WaitForExit(120000)) {
    Stop-Process -Id $process.Id -Force
    throw 'Windows player smoke timed out after two minutes.'
}
if ($process.ExitCode -ne 0) { throw "Player exited with code $($process.ExitCode); inspect $logPath" }
if (-not (Test-Path -LiteralPath $reportPath)) { throw 'Player did not write a smoke report.' }
$report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
if (-not $report.valid -or $report.simulationTicksAdvanced -le 0 -or $report.combatSecondsAdvanced -le 0.1) {
    throw "Player did not exercise advancing combat: $($report.error)"
}
$errors = Select-String -LiteralPath $logPath -Pattern 'Exception:|NullReferenceException|MissingReferenceException|Shader error|Failed to load'
if ($errors) { throw "Player logged runtime/load errors; inspect $logPath" }
Write-Output "Windows smoke passed: $($report.simulationTicksAdvanced) simulation ticks; $($report.combatSecondsAdvanced) combat seconds. Headless smoke does not certify rendering or FPS."
