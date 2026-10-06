param(
    [string]$Player = 'E:\EQRungBeatEmUp\MultiplayerValidationBuild\GhostFair.exe',
    [ValidateRange(2,4)][int]$Players = 2,
    [switch]$Relay,
    [switch]$MixedCharacters
)
$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$taskMode = if ($Relay) { 'Relay' } else { 'Direct' }
$taskId = [guid]::NewGuid().ToString('N')
$taskCode = Join-Path $taskRoot "Temp\RelayCode-$taskId.txt"
$taskProcesses = @()
if (-not (Test-Path -LiteralPath $Player)) { throw "Build the development player first: $Player" }
for ($taskIndex = 0; $taskIndex -lt $Players; $taskIndex++) {
    $taskRole = if ($taskIndex -eq 0) { 'Host' } else { "Client$taskIndex" }
    $taskReport = Join-Path $taskRoot "Documentation\MultiplayerBoss$taskMode$Players$($taskRole)Results.txt"
    $taskLog = Join-Path $taskRoot "Temp\MultiplayerBoss$taskMode$Players$($taskRole).log"
    $taskArguments = @('-batchmode', '-screen-width', '960', '-screen-height', '540', '--boss-network-test', '--peers', "$Players", '--output', ('"{0}"' -f $taskReport), '-logFile', ('"{0}"' -f $taskLog))
    if ($taskIndex -eq 0) { $taskArguments += '--host' }
    if ($MixedCharacters) { $taskArguments += @('--character', ($taskIndex % 2).ToString()) }
    if ($Relay) { $taskArguments += @('--relay', '--code-file', ('"{0}"' -f $taskCode)) }
    $taskProcesses += Start-Process -FilePath $Player -ArgumentList $taskArguments -WindowStyle Hidden -PassThru
}
$taskDeadline = [DateTime]::UtcNow.AddMinutes(3)
while (@($taskProcesses | Where-Object { -not $_.HasExited }).Count -gt 0 -and [DateTime]::UtcNow -lt $taskDeadline) { Start-Sleep -Milliseconds 500 }
$taskFailed = $false
foreach ($taskProcess in $taskProcesses) {
    if (-not $taskProcess.HasExited) { Stop-Process -Id $taskProcess.Id; $taskFailed = $true; Write-Warning "Test process $($taskProcess.Id) timed out" }
    elseif ($taskProcess.ExitCode -ne 0) { $taskFailed = $true }
}
Get-ChildItem -LiteralPath (Join-Path $taskRoot 'Documentation') -Filter "MultiplayerBoss$taskMode$Players*Results.txt" | ForEach-Object { Write-Output $_.FullName; Get-Content -LiteralPath $_.FullName }
if ($taskFailed) { throw 'Boss network validation failed. See reports and Temp logs.' }
