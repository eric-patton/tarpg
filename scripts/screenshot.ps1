# Captures the TARPG window's client area (no title bar, no borders) to a
# PNG. Designed so I can `Read` the resulting PNG to see what the game is
# rendering — visual verification of any UI change without a human in
# the loop.
#
# Usage:
#   pwsh ./scripts/screenshot.ps1                                  # writes debug/screenshot.png
#   pwsh ./scripts/screenshot.ps1 -Out debug/title-screen.png      # custom path
#   pwsh ./scripts/screenshot.ps1 -DelayMs 500                     # wait before capturing
#
# Caveats:
#   - DPI scaling above 100% may stretch coordinates. Set DPI to 100% in
#     Windows display settings if screenshots come back at the wrong size.
#   - Window must be visible (not minimized) — script does NOT raise it.
#     Use sendkeys.ps1's window-focus side-effect, or manually click into
#     the game first if you've alt-tabbed.
param(
    [string]$Out,
    [int]$DelayMs = 0,
    [string]$WindowTitle = 'TARPG'
)
$ErrorActionPreference = 'Stop'

if (-not $Out)
{
    $repoRoot = Split-Path -Parent $PSScriptRoot
    $Out = Join-Path $repoRoot 'debug\screenshot.png'
}

Add-Type -AssemblyName System.Drawing | Out-Null

# PrintWindow is the right primitive: it asks the window to render itself
# into a bitmap we own, regardless of whether it's behind other windows
# or even visible. CopyFromScreen would just pull whatever pixels are
# at the window's reported position, which gives us garbage when the
# game is hidden under e.g. an editor window. PW_RENDERFULLCONTENT
# (flag 0x02) makes it work for DirectX / hardware-accelerated content
# like MonoGame's window — without it we get a black rectangle.
#
# GetWindowRect (whole window, incl. borders) + DwmGetWindowAttribute
# for the trimmed bounds would also work, but PrintWindow + the source
# rect from GetClientRect (relative to the window's upper-left) is
# simpler. We render the *full* window via PrintWindow then crop to the
# client area below.
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class TarpgWin32Capture {
    [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
    public const uint PW_RENDERFULLCONTENT = 0x00000002;
}
"@

if ($DelayMs -gt 0) { Start-Sleep -Milliseconds $DelayMs }

$proc = Get-Process -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowTitle -eq $WindowTitle } | Select-Object -First 1
if (-not $proc) { Write-Error "No window with title '$WindowTitle' found. Did launch-debug.ps1 succeed?"; exit 1 }

$hwnd = $proc.MainWindowHandle

# Capture the WHOLE window (incl. borders / title bar) via PrintWindow,
# then crop to the client area. We need the window-rect for PrintWindow's
# bitmap size, the client-rect (in window-relative coords) for the crop.
$winRect = New-Object 'TarpgWin32Capture+RECT'
[void][TarpgWin32Capture]::GetWindowRect($hwnd, [ref]$winRect)
$winW = $winRect.Right - $winRect.Left
$winH = $winRect.Bottom - $winRect.Top
if ($winW -le 0 -or $winH -le 0) { Write-Error "Window rect is empty ($winW by $winH) - is the window minimized?"; exit 1 }

$clientRect = New-Object 'TarpgWin32Capture+RECT'
[void][TarpgWin32Capture]::GetClientRect($hwnd, [ref]$clientRect)
$clientW = $clientRect.Right - $clientRect.Left
$clientH = $clientRect.Bottom - $clientRect.Top

# Find where the client area starts within the full-window bitmap. The
# client-origin (0,0) -> screen via ClientToScreen, then subtract the
# window's screen origin.
$clientOrigin = New-Object 'TarpgWin32Capture+POINT'
$clientOrigin.X = 0
$clientOrigin.Y = 0
[void][TarpgWin32Capture]::ClientToScreen($hwnd, [ref]$clientOrigin)
$clientOffsetX = $clientOrigin.X - $winRect.Left
$clientOffsetY = $clientOrigin.Y - $winRect.Top

$dir = Split-Path -Parent $Out
if ($dir -and -not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir | Out-Null }

$winBmp = New-Object System.Drawing.Bitmap $winW, $winH
try
{
    $winGfx = [System.Drawing.Graphics]::FromImage($winBmp)
    try
    {
        $hdc = $winGfx.GetHdc()
        try
        {
            $printed = [TarpgWin32Capture]::PrintWindow($hwnd, $hdc, [TarpgWin32Capture]::PW_RENDERFULLCONTENT)
            if (-not $printed) { Write-Error "PrintWindow failed"; exit 1 }
        }
        finally
        {
            $winGfx.ReleaseHdc($hdc)
        }
    }
    finally
    {
        $winGfx.Dispose()
    }

    # Crop to client area.
    $clientBmp = New-Object System.Drawing.Bitmap $clientW, $clientH
    try
    {
        $cropGfx = [System.Drawing.Graphics]::FromImage($clientBmp)
        try
        {
            $srcRect = New-Object System.Drawing.Rectangle $clientOffsetX, $clientOffsetY, $clientW, $clientH
            $dstRect = New-Object System.Drawing.Rectangle 0, 0, $clientW, $clientH
            $cropGfx.DrawImage($winBmp, $dstRect, $srcRect, [System.Drawing.GraphicsUnit]::Pixel)
        }
        finally
        {
            $cropGfx.Dispose()
        }
        $clientBmp.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally
    {
        $clientBmp.Dispose()
    }
}
finally
{
    $winBmp.Dispose()
}

$width = $clientW
$height = $clientH

Write-Output "Saved $Out ($width x $height)"
