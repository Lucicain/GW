<#
.SYNOPSIS
    Bounded, read-only window-focus trace for the battle IME investigation.
.DESCRIPTION
    Records only state changes: process names, window handles/styles/sizes,
    keyboard layout IDs and responsiveness. Never reads keys, text, window
    titles or clipboard contents. Does not change focus, IME or display mode.
    Exits when the observed game exits, or after Minutes including startup wait.
#>
[CmdletBinding()]
param(
    [ValidateRange(1, 60)][int]$Minutes = 15,
    [string]$OutputPath = (Join-Path (Split-Path $PSScriptRoot -Parent) '.codex_tmp\battle-window.jsonl')
)
$ErrorActionPreference = 'Stop'
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class GwpWindowProbe {
    [StructLayout(LayoutKind.Sequential)]
    public struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    [DllImport("user32.dll")] public static extern IntPtr GetKeyboardLayout(uint thread);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr window, out Rect rect);
    [DllImport("user32.dll", EntryPoint="GetWindowLongW")] public static extern int GetWindowLong(IntPtr window, int index);
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr window);
}
'@
$outputFile = [IO.Path]::GetFullPath($OutputPath)
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($outputFile)) | Out-Null
$writer = [IO.StreamWriter]::new($outputFile, $true, [Text.UTF8Encoding]::new($false))
$writer.AutoFlush = $true
$deadline = [DateTime]::UtcNow.AddMinutes($Minutes)
$lastState = ''
$seenGame = $false
try {
    $writer.WriteLine((@{ event='start'; utc=[DateTime]::UtcNow.ToString('o'); minutes=$Minutes } | ConvertTo-Json -Compress))
    while ([DateTime]::UtcNow -lt $deadline) {
        $game = Get-Process -Name Bannerlord -ErrorAction SilentlyContinue | Select-Object -First 1
        if (-not $game) {
            if ($seenGame) { break }
            Start-Sleep -Milliseconds 500
            continue
        }
        $seenGame = $true
        $game.Refresh()
        $window = $game.MainWindowHandle
        $foreground = [GwpWindowProbe]::GetForegroundWindow()
        [uint32]$foregroundProcess = 0
        [uint32]$gameProcess = 0
        $foregroundThread = [GwpWindowProbe]::GetWindowThreadProcessId($foreground, [ref]$foregroundProcess)
        $gameThread = [GwpWindowProbe]::GetWindowThreadProcessId($window, [ref]$gameProcess)
        $rect = [GwpWindowProbe+Rect]::new()
        $hasRect = [GwpWindowProbe]::GetWindowRect($window, [ref]$rect)
        $foregroundName = (Get-Process -Id $foregroundProcess -ErrorAction SilentlyContinue).ProcessName
        $state = [ordered]@{
            gamePid=$game.Id; gameWindow=$window.ToInt64(); responding=$game.Responding
            foregroundPid=$foregroundProcess; foregroundProcess=$foregroundName
            foregroundWindow=$foreground.ToInt64(); gameForeground=($foreground -eq $window)
            gameLayout=$(if ($gameThread) { [GwpWindowProbe]::GetKeyboardLayout($gameThread).ToInt64().ToString('X') } else { 'unknown' })
            foregroundLayout=$(if ($foregroundThread) { [GwpWindowProbe]::GetKeyboardLayout($foregroundThread).ToInt64().ToString('X') } else { 'unknown' })
            minimized=[GwpWindowProbe]::IsIconic($window)
            style=[GwpWindowProbe]::GetWindowLong($window, -16)
            rectValid=$hasRect; rect=@($rect.Left,$rect.Top,$rect.Right,$rect.Bottom)
        }
        $signature = $state | ConvertTo-Json -Compress
        if ($signature -cne $lastState) {
            $lastState = $signature
            $state['utc'] = [DateTime]::UtcNow.ToString('o')
            $writer.WriteLine(($state | ConvertTo-Json -Compress))
        }
        Start-Sleep -Milliseconds 200
    }
    $writer.WriteLine((@{ event='stop'; utc=[DateTime]::UtcNow.ToString('o'); seenGame=$seenGame } | ConvertTo-Json -Compress))
}
finally { $writer.Dispose() }
