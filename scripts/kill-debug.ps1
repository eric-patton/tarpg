# Cleanly stops every running TARPG window. Safe to call when nothing is
# running (returns 0). Useful between iteration cycles or after a launch
# script that left a window behind on error.
#
# Usage:
#   pwsh ./scripts/kill-debug.ps1
param(
    [string]$WindowTitle = 'TARPG'
)
$ErrorActionPreference = 'Stop'

$procs = @(Get-Process -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowTitle -eq $WindowTitle })
if ($procs.Count -eq 0)
{
    Write-Output "No running TARPG window."
    exit 0
}

foreach ($p in $procs)
{
    Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
    Write-Output "Killed PID $($p.Id)"
}
