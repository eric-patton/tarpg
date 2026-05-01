# Launches TARPG in detached mode and waits for the game window to appear.
# Prints the PID on success so callers can target the right process for
# screenshot / sendkeys / click / kill. Exits non-zero on launch or window
# timeout failure.
#
# Usage:
#   pwsh ./scripts/launch-debug.ps1                       # default 15s wait
#   pwsh ./scripts/launch-debug.ps1 -TimeoutSec 30        # longer wait
#   pwsh ./scripts/launch-debug.ps1 -SkipBuild            # skip dotnet build
#
# Caveats:
#   - Window title must be exactly "TARPG" (set in Program.cs).
#   - Doesn't position the window — first launch lands wherever the OS picks.
param(
    [int]$TimeoutSec = 15,
    [switch]$SkipBuild,
    [string]$WindowTitle = 'TARPG'
)
$ErrorActionPreference = 'Stop'

# Fail fast if a TARPG window is already open — multiple instances would
# confuse Get-Process lookups in the sibling scripts.
$existing = Get-Process -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowTitle -eq $WindowTitle }
if ($existing)
{
    Write-Error "A TARPG window is already running (PID $($existing[0].Id)). Kill it first via scripts/kill-debug.ps1."
    exit 1
}

$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not $SkipBuild)
{
    Write-Host "Building..."
    & dotnet build "$repoRoot\tarpg.sln" --nologo --verbosity quiet
    if ($LASTEXITCODE -ne 0) { Write-Error "Build failed"; exit 1 }
}

# Run the built exe directly instead of `dotnet run` — `run` would spawn
# a child dotnet process and our window-by-PID lookup would target the
# wrapper, not the actual game.
$exe = "$repoRoot\src\Tarpg\bin\Debug\net8.0\Tarpg.exe"
if (-not (Test-Path $exe)) { Write-Error "Game exe not found at $exe (build succeed but missing output?)"; exit 1 }

# WorkingDirectory matters: Program.cs resolves the font path against
# AppContext.BaseDirectory which is the exe's folder, but other content
# load paths may be cwd-relative. Use the project dir to match `run.bat`.
$proc = Start-Process -FilePath $exe -WorkingDirectory "$repoRoot\src\Tarpg" -PassThru
Write-Host "Launched PID $($proc.Id), waiting for window..."

$deadline = (Get-Date).AddSeconds($TimeoutSec)
while ((Get-Date) -lt $deadline)
{
    Start-Sleep -Milliseconds 250
    if ($proc.HasExited)
    {
        Write-Error "Game process exited before window appeared (exit code $($proc.ExitCode)). Check stdout for errors."
        exit 1
    }
    # Refresh the process to get a fresh MainWindowTitle / Handle.
    $proc.Refresh()
    if ($proc.MainWindowTitle -eq $WindowTitle -and $proc.MainWindowHandle -ne 0)
    {
        Write-Output $proc.Id
        exit 0
    }
}

Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
Write-Error "Window '$WindowTitle' never appeared within ${TimeoutSec}s"
exit 1
