param([string[]]$Poses = @('early','mid','late','growth','boss'))
$crascendoUnityRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$crascendoBuildRoot = [IO.Path]::GetFullPath((Join-Path $crascendoUnityRoot '../Builds/CrascendoValidation'))
$crascendoPlayer = Join-Path $crascendoBuildRoot 'VoidFall.exe'
if (!(Test-Path -LiteralPath $crascendoPlayer)) { throw 'Build the Crascendo validation player first.' }
foreach ($pose in $Poses) {
    if ($pose -notin @('early','mid','late','growth','boss')) { throw 'Unknown capture pose.' }
    $capturePath = Join-Path $crascendoBuildRoot ($pose + '.png')
    $logPath = Join-Path $crascendoBuildRoot ($pose + '.log')
    $captureProcess = Start-Process -FilePath $crascendoPlayer -ArgumentList @('-screen-width','1920','-screen-height','1080','-screen-fullscreen','0',('-vfcrascendo=' + $pose),('-vfcapture=' + $capturePath),'-vfcapture-quit','-logFile',$logPath) -WindowStyle Hidden -PassThru
    $captureProcess.WaitForExit()
    if ($captureProcess.ExitCode -ne 0 -or !(Test-Path -LiteralPath $capturePath)) { throw ('Capture failed: ' + $pose) }
    Write-Output $capturePath
}
