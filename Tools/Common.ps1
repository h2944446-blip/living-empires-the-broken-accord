# Shared helpers. Dot-source this file from the public entry-point scripts.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$script:CommunityRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$script:CommunityReports = Join-Path $script:CommunityRoot 'Reports'
$script:CommunityPlayer = Join-Path $script:CommunityRoot 'Build\Windows\LivingEmpiresCommunity.exe'

function Initialize-CommunityReports {
    [IO.Directory]::CreateDirectory($script:CommunityReports) | Out-Null
}

function Resolve-CommunityEditor {
    param([string]$EditorPath)
    if ([string]::IsNullOrWhiteSpace($EditorPath)) {
        if ([string]::IsNullOrWhiteSpace($env:ProgramFiles)) {
            throw 'Supply -EditorPath with the full path to Unity 6000.6.0f1 Editor/Unity.exe.'
        }
        $EditorPath = Join-Path $env:ProgramFiles 'Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe'
    }
    if (-not (Test-Path -LiteralPath $EditorPath -PathType Leaf)) {
        throw "Unity 6000.6.0f1 was not found at '$EditorPath'. Install that version or supply -EditorPath with its Unity.exe path."
    }
    return (Get-Item -LiteralPath $EditorPath).FullName
}

function Assert-CommunityPlayer {
    if (-not (Test-Path -LiteralPath $script:CommunityPlayer -PathType Leaf)) {
        throw 'No community Windows player was found. Run Tools/Build.ps1 first.'
    }
}

function ConvertTo-CommunityArgument {
    param([AllowEmptyString()][string]$Value)
    # Windows native command-line quoting; no shell evaluates these arguments.
    $escaped = [regex]::Replace($Value, '(\\*)"', '$1$1\"')
    $escaped = [regex]::Replace($escaped, '(\\+)$', '$1$1')
    return '"' + $escaped + '"'
}

function Invoke-CommunityProcess {
    param(
        [Parameter(Mandatory)][string]$FilePath,
        [Parameter(Mandatory)][string[]]$Arguments,
        [switch]$Visible,
        [switch]$Wait,
        [ValidateRange(1, 86400)][int]$TimeoutSeconds = 1800
    )
    $start = @{
        FilePath = $FilePath
        ArgumentList = (($Arguments | ForEach-Object { ConvertTo-CommunityArgument $_ }) -join ' ')
        WorkingDirectory = $script:CommunityRoot
        PassThru = $true
        WindowStyle = 'Hidden'
    }
    if ($Visible) { $start.WindowStyle = 'Normal' }
    $process = Start-Process @start
    try {
        if (-not $Wait) {
            Write-Host "Started the community player (process $($process.Id))."
            return 0
        }
        if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
            # Only terminate the process this invocation created.
            try { $process.Kill(); $process.WaitForExit(5000) | Out-Null } catch { }
            Write-Host "Process timed out after $TimeoutSeconds seconds. Inspect the log in Reports."
            return 124
        }
        $process.WaitForExit()
        $process.Refresh()
        return [int]$process.ExitCode
    }
    finally { $process.Dispose() }
}

function Get-CommunityPlayerArguments {
    param(
        [ValidateSet('Balanced', 'Economy')][string]$Quality = 'Balanced',
        [int]$Width = 1440,
        [int]$Height = 900,
        [Nullable[int]]$GraphicsDevice,
        [Parameter(Mandatory)][string]$LogPath
    )
    $arguments = @(('--' + $Quality.ToLowerInvariant()), '-screen-width', "$Width", '-screen-height', "$Height", '-screen-fullscreen', '0', '-logFile', $LogPath)
    if ($null -ne $GraphicsDevice) { $arguments += @('-force-device-index', "$GraphicsDevice") }
    return $arguments
}

function Assert-CommunityFreshReport {
    param([string]$Path, [datetime]$StartedUtc)
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "The process did not write the expected report: $Path"
    }
    if ((Get-Item -LiteralPath $Path).LastWriteTimeUtc -lt $StartedUtc) {
        throw "The report predates this run: $Path. Inspect the new log."
    }
}
