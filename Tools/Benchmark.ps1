#requires -Version 5.1
[CmdletBinding()]
param(
    [ValidateSet('Settlement', 'Military')][string]$Scenario = 'Settlement',
    [ValidateSet('Balanced', 'Economy')][string]$Quality = 'Balanced',
    [ValidateRange(640, 7680)][int]$Width = 1440,
    [ValidateRange(480, 4320)][int]$Height = 900,
    [ValidateRange(0, 32)][Nullable[int]]$GraphicsDevice,
    [ValidateRange(60, 86400)][int]$TimeoutSeconds = 300
)

. (Join-Path $PSScriptRoot 'Common.ps1')
try {
    Assert-CommunityPlayer
    Initialize-CommunityReports
    $run = [datetime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff')
    $log = Join-Path $script:CommunityReports ("benchmark-$($Scenario.ToLowerInvariant())-$run.log")
    $arguments = @(Get-CommunityPlayerArguments -Quality $Quality -Width $Width -Height $Height -GraphicsDevice $GraphicsDevice -LogPath $log)
    $flag = '--benchmark'
    $reportName = 'performance.txt'
    if ($Scenario -eq 'Military') { $flag = '--benchmark-war'; $reportName = 'military-performance.txt' }
    $arguments += $flag
    Write-Host 'Close the Unity Editor and other game copies before measuring. Keep the benchmark visible.'
    Write-Host 'This is a seeded fixture with an 8-second warm-up and a 30-second sample; it does not overwrite manual saves.'
    Write-Host "Log: $log"
    $started = [datetime]::UtcNow
    $result = Invoke-CommunityProcess -FilePath $script:CommunityPlayer -Arguments $arguments -Visible -Wait -TimeoutSeconds $TimeoutSeconds
    if ($result -ne 0) { exit $result }
    $report = Join-Path $script:CommunityRoot ('Build\Windows\Reports\' + $reportName)
    Assert-CommunityFreshReport -Path $report -StartedUtc $started
    $copy = Join-Path $script:CommunityReports ("benchmark-$($Scenario.ToLowerInvariant())-$run.txt")
    Copy-Item -LiteralPath $report -Destination $copy
    Write-Host "Benchmark complete. Report: $copy"
    Write-Host 'Inspect the gameplay capture in Build/Windows/Reports before interpreting the measurements.'
    exit 0
}
catch { Write-Error $_ -ErrorAction Continue; exit 1 }
