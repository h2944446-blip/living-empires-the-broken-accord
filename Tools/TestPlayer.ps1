#requires -Version 5.1
[CmdletBinding()]
param(
    [ValidateSet('All', 'Integration', 'Assets', 'Navigation', 'Military')][string]$Suite = 'All',
    [ValidateSet('Balanced', 'Economy')][string]$Quality = 'Economy',
    [ValidateRange(640, 7680)][int]$Width = 1440,
    [ValidateRange(480, 4320)][int]$Height = 900,
    [ValidateRange(0, 32)][Nullable[int]]$GraphicsDevice,
    [ValidateRange(30, 86400)][int]$TimeoutSeconds = 600
)

. (Join-Path $PSScriptRoot 'Common.ps1')
try {
    Assert-CommunityPlayer
    Initialize-CommunityReports
    $run = [datetime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff')
    $suites = @('Integration', 'Assets', 'Navigation', 'Military')
    if ($Suite -ne 'All') { $suites = @($Suite) }
    $reportNames = @{
        Integration = 'integration-tests.txt'
        Assets = 'imported-asset-tests.txt'
        Navigation = 'navigation-tests.txt'
        Military = 'military-runtime-tests.txt'
    }
    Write-Host 'Each suite opens a visible player. Keep it focused and avoid mouse/keyboard input until it exits.'
    Write-Host 'The runtime suites use isolated QA state and check that manual chapter saves remain unchanged.'
    foreach ($current in $suites) {
        $log = Join-Path $script:CommunityReports ("player-$($current.ToLowerInvariant())-$run.log")
        $arguments = @(Get-CommunityPlayerArguments -Quality $Quality -Width $Width -Height $Height -GraphicsDevice $GraphicsDevice -LogPath $log)
        $arguments += '--verify-' + $current.ToLowerInvariant()
        $started = [datetime]::UtcNow
        Write-Host "Running $current. Log: $log"
        $result = Invoke-CommunityProcess -FilePath $script:CommunityPlayer -Arguments $arguments -Visible -Wait -TimeoutSeconds $TimeoutSeconds
        if ($result -ne 0) { exit $result }
        $report = Join-Path $script:CommunityRoot ('Build\Windows\Reports\' + $reportNames[$current])
        Assert-CommunityFreshReport -Path $report -StartedUtc $started
        $copy = Join-Path $script:CommunityReports ("$($current.ToLowerInvariant())-$run.txt")
        Copy-Item -LiteralPath $report -Destination $copy
        Write-Host "$current passed. Report: $copy"
    }
    exit 0
}
catch { Write-Error $_ -ErrorAction Continue; exit 1 }
