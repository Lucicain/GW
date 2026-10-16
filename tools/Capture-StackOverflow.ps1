<#
.SYNOPSIS
Run Bannerlord under cdb so a silent crash leaves its call stack behind.

.DESCRIPTION
A .NET stack overflow ends the process at once: no exception reaches the mod's fault
watch, the TaleWorlds watchdog writes no dump, and Windows logs nothing. Steam only
records exit code -2147023895 (0x800703E9, COR_E_STACKOVERFLOW). Under cdb the
overflow arrives as a first-chance "sov" event before the runtime terminates the
process. The same happens to any other fatal crash: under a debugger Windows Error
Reporting writes nothing, and an unhandled exception stops in cdb instead.

So the whole cdb session is logged to GreyWarden-Crash-<time>.log. On a stack overflow,
or on the second (fatal) chance of an access violation, a CLR exception or a fail-fast,
the command file writes the last event, the managed exception and stack (SOS !pe,
!clrstack) and the native stacks, saves a small dump, and closes the game. When the
game exits on its own, the exit code is logged. (2026-10-16: the first version only
handled stack overflows; a crash at 22:28 left nothing behind.)

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
$log = Join-Path $out "GreyWarden-Crash-$stamp.log"
$dump = Join-Path $out "GreyWarden-Crash-$stamp.dmp"
$commands = Join-Path $out 'GreyWarden-Crash-commands.txt'

# Everything the debugger prints goes to $log (-logo). This runs at the fatal moment.
# Backslashes are escape characters inside cdb's quoted command strings, so the dump path doubles them.
$collect = '.echo === GWP CRASH ===; .lastevent; .loadby sos clr; .echo === managed exception ===; !pe; ' +
    '.echo === managed stack ===; !clrstack; .echo === native stack ===; .ecxr; kc 400; ' +
    '.echo === all threads ===; ~*kc 40; .dump /mh \"' + $dump.Replace('\', '\\') + '\"; .kill; q'

# av / clr / C++ exceptions are routine first-chance events in this game: stop only when
# they are fatal (second chance), on a stack overflow, or on a fail-fast (c0000409).
@"
sxd -c2 "$collect" av
sxd -c2 "$collect" clr
sxd -c2 "$collect" c0000409
sxd eh
sxi ld
sxi ud
sxi ct
sxi et
sxe -c "$collect" sov
sxe -c ".echo === GWP PROCESS EXIT ===; .lastevent; q" epr
g
"@ | Set-Content -Encoding ASCII -Path $commands

$arguments = @('-G', '-logo', "`"$log`"", '-cf', "`"$commands`"", "`"$game`"", '/singleplayer', $Modules)
if ($ContinueGame) { $arguments += '/continuegame' }
Write-Host "Starting the game under cdb. Play as usual; the debugger log (and on a crash the stacks) goes to:"
Write-Host "  $log"
Start-Process -FilePath $cdb -ArgumentList $arguments -WorkingDirectory $bin
