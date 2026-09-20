#requires -Version 5.1
[CmdletBinding()]
param(
    [string]$EditorPath,
    [ValidateRange(30, 86400)][int]$TimeoutSeconds = 1800
)

. (Join-Path $PSScriptRoot 'Common.ps1')
try {
    $editor = Resolve-CommunityEditor $EditorPath
    Initialize-CommunityReports
    $log = Join-Path $script:CommunityReports ('verify-editor-' + [datetime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff') + '.log')
    $started = [datetime]::UtcNow
    Write-Host "Verifying with $editor"
    Write-Host "Log: $log"
    $result = Invoke-CommunityProcess -FilePath $editor -Arguments @(
        '-batchmode', '-quit', '-projectPath', $script:CommunityRoot,
        '-executeMethod', 'LivingEmpires.EditorTools.CommunityValidation.Verify', '-logFile', $log
    ) -Wait -TimeoutSeconds $TimeoutSeconds
    if ($result -ne 0) { exit $result }
    $report = Join-Path $script:CommunityReports 'community-verification.json'
    Assert-CommunityFreshReport -Path $report -StartedUtc $started
    if (-not (Get-Content -LiteralPath $report -Raw | ConvertFrom-Json).passed) {
        throw "Verification reported failure. Inspect $report"
    }
    Write-Host "Verification passed. Report: $report"
    exit 0
}
catch { Write-Error $_ -ErrorAction Continue; exit 1 }
