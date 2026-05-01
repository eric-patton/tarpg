# Sends one or more keystrokes to the TARPG window. Uses Win32 keybd_event
# (injects at the OS input-device layer) instead of SendKeys / WM_KEYDOWN
# because MonoGame reads the keyboard via its own polling pipeline and
# ignores window-message keys.
#
# Usage:
#   pwsh ./scripts/sendkeys.ps1 down                    # one key
#   pwsh ./scripts/sendkeys.ps1 down down enter         # sequence
#   pwsh ./scripts/sendkeys.ps1 q -DelayMs 200          # slow it down
#
# Caveats:
#   - Window must be foreground (script raises it before sending).
#   - keybd_event affects the WHOLE desktop session for the moment of
#     injection — if you alt-tab away while a sequence is running, the
#     keys go to whatever's foreground at that instant.
param(
    [Parameter(Mandatory, Position = 0, ValueFromRemainingArguments = $true)][string[]]$Keys,
    [string]$WindowTitle = 'TARPG',
    [int]$DelayMs = 50
)
$ErrorActionPreference = 'Stop'

Add-Type @"
using System;
using System.Runtime.InteropServices;
public class TarpgWin32Keys {
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
    [DllImport("user32.dll")] public static extern uint MapVirtualKey(uint uCode, uint uMapType);
    public const int SW_RESTORE = 9;
    public const uint KEYEVENTF_KEYUP       = 0x0002;
    public const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
    public const uint KEYEVENTF_SCANCODE    = 0x0008;
    public const uint MAPVK_VK_TO_VSC       = 0;
}
"@

# Friendly name -> virtual-key code map. Lowercase keys for case-insensitive
# lookup. Values are byte VKs that keybd_event accepts directly.
$vk = @{
    'up'    = 0x26; 'down'  = 0x28; 'left'  = 0x25; 'right' = 0x27
    'enter' = 0x0D; 'return' = 0x0D
    'escape' = 0x1B; 'esc' = 0x1B
    'space' = 0x20; 'tab' = 0x09; 'backspace' = 0x08; 'delete' = 0x2E
    'shift' = 0x10; 'ctrl' = 0x11; 'alt' = 0x12
    'plus' = 0xBB; 'minus' = 0xBD
    '0' = 0x30; '1' = 0x31; '2' = 0x32; '3' = 0x33; '4' = 0x34
    '5' = 0x35; '6' = 0x36; '7' = 0x37; '8' = 0x38; '9' = 0x39
    'a' = 0x41; 'b' = 0x42; 'c' = 0x43; 'd' = 0x44; 'e' = 0x45
    'f' = 0x46; 'g' = 0x47; 'h' = 0x48; 'i' = 0x49; 'j' = 0x4A
    'k' = 0x4B; 'l' = 0x4C; 'm' = 0x4D; 'n' = 0x4E; 'o' = 0x4F
    'p' = 0x50; 'q' = 0x51; 'r' = 0x52; 's' = 0x53; 't' = 0x54
    'u' = 0x55; 'v' = 0x56; 'w' = 0x57; 'x' = 0x58; 'y' = 0x59; 'z' = 0x5A
    'f1' = 0x70; 'f2' = 0x71; 'f3' = 0x72; 'f4' = 0x73
}

$proc = Get-Process -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowTitle -eq $WindowTitle } | Select-Object -First 1
if (-not $proc) { Write-Error "No window '$WindowTitle' found"; exit 1 }

[void][TarpgWin32Keys]::ShowWindow($proc.MainWindowHandle, [TarpgWin32Keys]::SW_RESTORE)
[void][TarpgWin32Keys]::SetForegroundWindow($proc.MainWindowHandle)
Start-Sleep -Milliseconds 150

# Extended-key set: arrows, ins/del, home/end, pgup/pgdn, numpad enter,
# r-ctrl, r-alt. These need KEYEVENTF_EXTENDEDKEY plus the regular scan code.
# Most other keys work with just VK + derived scan code.
$extendedVks = @(0x21, 0x22, 0x23, 0x24, 0x25, 0x26, 0x27, 0x28, 0x2D, 0x2E)

foreach ($k in $Keys)
{
    $lookup = $k.ToLower()
    if (-not $vk.ContainsKey($lookup))
    {
        Write-Error "Unknown key '$k'. Add it to the `$vk map in sendkeys.ps1."
        exit 1
    }
    $code = [byte]$vk[$lookup]
    # Resolve hardware scan code via MapVirtualKey. keybd_event with scan=0
    # is unreliable for some keys (notably letters / Enter under MonoGame's
    # input layer); supplying the proper scan code makes injections show up
    # consistently in Microsoft.Xna.Framework.Input.Keyboard.GetState.
    $scan = [byte]([TarpgWin32Keys]::MapVirtualKey([uint32]$code, [TarpgWin32Keys]::MAPVK_VK_TO_VSC))
    $flags = 0
    if ($extendedVks -contains $vk[$lookup]) { $flags = $flags -bor [TarpgWin32Keys]::KEYEVENTF_EXTENDEDKEY }
    [TarpgWin32Keys]::keybd_event($code, $scan, $flags, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 30
    [TarpgWin32Keys]::keybd_event($code, $scan, ($flags -bor [TarpgWin32Keys]::KEYEVENTF_KEYUP), [UIntPtr]::Zero)
    Start-Sleep -Milliseconds $DelayMs
}

Write-Output "Sent: $($Keys -join ' ')"
