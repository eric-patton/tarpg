# Sends a mouse click to the TARPG window. Two coordinate modes:
#   -CellX/-CellY  : click at viewport cell (default 12x12 cells, 80x30 grid)
#   -PixelX/-PixelY: click at exact pixel within the client area
#
# Either way the script raises the window first, moves the cursor, then
# fires a single down/up event via Win32 mouse_event so the click looks
# native to the game (PostMessage can also work but loses focus state).
#
# Usage:
#   pwsh ./scripts/click.ps1 -CellX 40 -CellY 15                 # left-click center cell
#   pwsh ./scripts/click.ps1 -CellX 40 -CellY 15 -Button right   # right-click
#   pwsh ./scripts/click.ps1 -PixelX 480 -PixelY 180             # exact pixel
#   pwsh ./scripts/click.ps1 -CellX 40 -CellY 15 -CellWidth 18 -CellHeight 18  # zoomed in (1.5x)
#
# Caveats:
#   - Default cell size assumes the bundled square 12x12 font at 1.0x zoom
#     (matches RenderSettings.UseSquareCells = true). If the player has
#     zoomed via +/-/wheel, pass -CellWidth and -CellHeight scaled.
#   - The cursor stays at the click position after the script — subsequent
#     clicks via this script overwrite it, but other scripts (sendkeys)
#     don't move it back.
[CmdletBinding(DefaultParameterSetName = 'Cell')]
param(
    [Parameter(ParameterSetName = 'Cell', Mandatory)][int]$CellX,
    [Parameter(ParameterSetName = 'Cell', Mandatory)][int]$CellY,
    [Parameter(ParameterSetName = 'Pixel', Mandatory)][int]$PixelX,
    [Parameter(ParameterSetName = 'Pixel', Mandatory)][int]$PixelY,

    [ValidateSet('left', 'right')][string]$Button = 'left',
    [int]$CellWidth = 12,
    [int]$CellHeight = 12,
    [string]$WindowTitle = 'TARPG'
)
$ErrorActionPreference = 'Stop'

Add-Type @"
using System;
using System.Runtime.InteropServices;
public class TarpgWin32Click {
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint dwFlags, int dx, int dy, int dwData, IntPtr dwExtraInfo);
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
    public const int SW_RESTORE = 9;
    public const uint LEFTDOWN  = 0x0002;
    public const uint LEFTUP    = 0x0004;
    public const uint RIGHTDOWN = 0x0008;
    public const uint RIGHTUP   = 0x0010;
}
"@

$proc = Get-Process -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowTitle -eq $WindowTitle } | Select-Object -First 1
if (-not $proc) { Write-Error "No window '$WindowTitle' found"; exit 1 }
$hwnd = $proc.MainWindowHandle

[void][TarpgWin32Click]::ShowWindow($hwnd, [TarpgWin32Click]::SW_RESTORE)
[void][TarpgWin32Click]::SetForegroundWindow($hwnd)
Start-Sleep -Milliseconds 100

# Compute click position in client coordinates, then map to screen coords.
if ($PSCmdlet.ParameterSetName -eq 'Cell')
{
    $clientX = [int]($CellX * $CellWidth + ($CellWidth / 2))
    $clientY = [int]($CellY * $CellHeight + ($CellHeight / 2))
}
else
{
    $clientX = $PixelX
    $clientY = $PixelY
}

$pt = New-Object 'TarpgWin32Click+POINT'
$pt.X = $clientX
$pt.Y = $clientY
[void][TarpgWin32Click]::ClientToScreen($hwnd, [ref]$pt)
[void][TarpgWin32Click]::SetCursorPos($pt.X, $pt.Y)
Start-Sleep -Milliseconds 50

if ($Button -eq 'right')
{
    [TarpgWin32Click]::mouse_event([TarpgWin32Click]::RIGHTDOWN, 0, 0, 0, [IntPtr]::Zero)
    Start-Sleep -Milliseconds 30
    [TarpgWin32Click]::mouse_event([TarpgWin32Click]::RIGHTUP,   0, 0, 0, [IntPtr]::Zero)
}
else
{
    [TarpgWin32Click]::mouse_event([TarpgWin32Click]::LEFTDOWN,  0, 0, 0, [IntPtr]::Zero)
    Start-Sleep -Milliseconds 30
    [TarpgWin32Click]::mouse_event([TarpgWin32Click]::LEFTUP,    0, 0, 0, [IntPtr]::Zero)
}

Write-Output "Clicked $Button at client ($clientX, $clientY) screen ($($pt.X), $($pt.Y))"
