#requires -Version 5.1
[CmdletBinding()]
param(
    [ValidateSet('Balanced', 'Economy')][string]$Quality = 'Balanced',
    [ValidateRange(640, 7680)][int]$Width = 1440,
    [ValidateRange(480, 4320)][int]$Height = 900,
    [ValidateRange(0, 32)][Nullable[int]]$GraphicsDevice
)

. (Join-Path $PSScriptRoot 'Common.ps1')
try {
    Assert-CommunityPlayer
    Initialize-CommunityReports
    $log = Join-Path $script:CommunityReports ('player-' + [datetime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff') + '.log')
    $arguments = Get-CommunityPlayerArguments -Quality $Quality -Width $Width -Height $Height -GraphicsDevice $GraphicsDevice -LogPath $log
    Write-Host "Player log: $log"
    $result = Invoke-CommunityProcess -FilePath $script:CommunityPlayer -Arguments $arguments -Visible
    exit $result
}
catch { Write-Error $_ -ErrorAction Continue; exit 1 }
