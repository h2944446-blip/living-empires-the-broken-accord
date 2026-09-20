#requires -Version 5.1
[CmdletBinding()]
param(
    [string]$EditorPath,
    [ValidateRange(30, 86400)][int]$TimeoutSeconds = 3600
)

. (Join-Path $PSScriptRoot 'Common.ps1')
try {
    $editor = Resolve-CommunityEditor $EditorPath
    Initialize-CommunityReports
    $log = Join-Path $script:CommunityReports ('build-editor-' + [datetime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff') + '.log')
    $started = [datetime]::UtcNow
    Write-Host "Building with $editor"
    Write-Host "Log: $log"
    $result = Invoke-CommunityProcess -FilePath $editor -Arguments @(
        '-batchmode', '-quit', '-projectPath', $script:CommunityRoot,
        '-executeMethod', 'LivingEmpires.EditorTools.CommunityValidation.BuildWindows', '-logFile', $log
    ) -Wait -TimeoutSeconds $TimeoutSeconds
    if ($result -ne 0) { exit $result }
    $report = Join-Path $script:CommunityReports 'community-build.json'
    Assert-CommunityFreshReport -Path $report -StartedUtc $started
    $summary = Get-Content -LiteralPath $report -Raw | ConvertFrom-Json
    if ($summary.result -ne 'Succeeded' -or $summary.errors -ne 0) {
        throw "Build reported failure. Inspect $report"
    }
    Assert-CommunityPlayer
    Write-Host "Build complete: $script:CommunityPlayer"
    Write-Host "Report: $report"
    exit 0
}
catch { Write-Error $_ -ErrorAction Continue; exit 1 }
