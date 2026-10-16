<#
.SYNOPSIS
Run Bannerlord under cdb so a stack overflow leaves its call stack behind.

.DESCRIPTION
A .NET stack overflow ends the process at once: no exception reaches the mod's fault
watch, the TaleWorlds watchdog writes no dump, and Windows logs nothing. Steam only
records exit code -2147023895 (0x800703E9, COR_E_STACKOVERFLOW). Under cdb the
overflow arrives as a first-chance "sov" event before the runtime terminates the
process. At that moment this script's command file writes the managed stack (SOS
!clrstack) and the native stack to a log, saves a small dump, and closes the game.

The user starts this script; it launches the game in the same configuration the
launcher used (module list below). Agents do not run it. Routine first-chance
access violations and CLR exceptions are passed through without stopping.

.PARAMETER ContinueGame
Pass /continuegame so the game loads the most recent save, as the launcher's
"Continue" button does.
#>
param(
    [switch]$ContinueGame,
    [string]$GameFolder = 'D:\steam\steamapps\common\Mount & Blade II Bannerlord',
    [string]$Modules = '_MODULES_*Native*SandBoxCore*BirthAndDeath*CustomBattle*Sandbox*StoryMode*GreyWarden*xxFemaleHead*NavalDLC*_MODULES_'
)

$ErrorActionPreference = 'Stop'
$bin = Join-Path $GameFolder 'bin\Win64_Shipping_Client'
$game = Join-Path $bin 'Bannerlord.exe'
$cdb = Join-Path $env:LOCALAPPDATA 'Microsoft\WindowsApps\cdbX64.exe'
$out = Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'Mount and Blade II Bannerlord'

if (-not (Test-Path $cdb)) { throw "cdb not found at $cdb (install WinDbg from the Microsoft Store)." }
if (-not (Test-Path $game)) { throw "Game executable not found at $game." }
if (Get-Process -Name 'Bannerlord', 'TaleWorlds.MountAndBlade.Launcher' -ErrorAction SilentlyContinue) {
    throw 'Close the game and its launcher first.'
}
if (-not (Get-Process -Name 'steam' -ErrorAction SilentlyContinue)) { throw 'Start Steam first.' }

$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$log = Join-Path $out "GreyWarden-StackOverflow-$stamp.log"
$dump = Join-Path $out "GreyWarden-StackOverflow-$stamp.dmp"
$commands = Join-Path $out 'GreyWarden-StackOverflow-commands.txt'

# av / clr / C++ exceptions are routine first-chance events in this game; break only on sov.
@"
sxd av
sxd clr
sxd eh
sxi ld
sxi ud
sxi ct
sxi et
sxe -c ".logopen \"$log\"; .echo === managed stack ===; .loadby sos clr; !clrstack; .echo === native stack ===; kc 400; .logclose; .dump /mh \"$dump\"; .kill; q" sov
g
"@ | Set-Content -Encoding ASCII -Path $commands

$arguments = @('-G', '-cf', "`"$commands`"", "`"$game`"", '/singleplayer', $Modules)
if ($ContinueGame) { $arguments += '/continuegame' }
Write-Host "Starting the game under cdb. Play as usual; on a stack overflow the stack goes to:"
Write-Host "  $log"
Start-Process -FilePath $cdb -ArgumentList $arguments -WorkingDirectory $bin
