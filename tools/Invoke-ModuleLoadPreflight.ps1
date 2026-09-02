<#
.SYNOPSIS
    Loads the built module assembly against the installed game and reports the
    exact type-load and Harmony-binding failures the game would hit at startup.

.DESCRIPTION
    This is the check that reproduces "GreyWarden could not be loaded correctly"
    without launching Bannerlord. A module DLL compiled against one game
    generation and run on another fails at type load - the CLR reports the
    abstract member it cannot bind - and the failure is silent apart from the
    launcher's generic dependency-conflict message.

    Must run under Windows PowerShell 5.1: the module and the game are net472,
    and PowerShell 7 cannot load them into its own runtime. Verify-GameCompat.ps1
    launches this script for you; run it directly only when debugging.

    Every dependency is preloaded up front. A ScriptBlock AssemblyResolve
    handler cannot be used here - the CLR invokes it re-entrantly from inside a
    running pipeline, which kills the runspace with "An error occurred while
    creating the pipeline".

.PARAMETER ModuleDll
    The GreyWardenPolicePurity.dll to inspect. Defaults to the live test module.

.PARAMETER GameFolder
    The Bannerlord installation whose assemblies the DLL must satisfy.
#>
[CmdletBinding()]
param(
    [string]$ModuleDll = 'D:\steam\steamapps\common\Mount & Blade II Bannerlord\Modules\GreyWarden\bin\Win64_Shipping_Client\GreyWardenPolicePurity.dll',
    [string]$GameFolder = 'D:\steam\steamapps\common\Mount & Blade II Bannerlord'
)

$ErrorActionPreference = 'Continue'
$moduleDir = Split-Path $ModuleDll -Parent

$nativeXml = [xml](Get-Content (Join-Path $GameFolder 'Modules\Native\SubModule.xml'))
Write-Host ('GAME=' + $nativeXml.Module.Version.value)
Write-Host ('DLL=' + $ModuleDll)

# Preload the game side first, and never from the GreyWarden module directory:
# loading a second GreyWardenPolicePurity.dll would silently win over the one
# under test, because LoadFrom returns the assembly already loaded by identity.
$candidates = @()
$candidates += Get-ChildItem (Join-Path $GameFolder 'bin\Win64_Shipping_Client') -Filter '*.dll' |
    Where-Object { $_.Name -ne 'TaleWorlds.Native.dll' }
foreach ($d in (Get-ChildItem (Join-Path $GameFolder 'Modules') -Directory)) {
    if ($d.Name -eq 'GreyWarden') { continue }
    $p = Join-Path $d.FullName 'bin\Win64_Shipping_Client'
    if (Test-Path $p) { $candidates += Get-ChildItem $p -Filter '*.dll' }
}
$loaded = 0
foreach ($f in $candidates) {
    try { [void][System.Reflection.Assembly]::LoadFrom($f.FullName); $loaded++ } catch { }
}
Write-Host ('PRELOADED=' + $loaded)

[void][System.Reflection.Assembly]::LoadFrom((Join-Path $moduleDir '0Harmony.dll'))
$asm = [System.Reflection.Assembly]::LoadFrom($ModuleDll)
Write-Host ('MODULE=' + $asm.GetName().Name + ' ' + $asm.GetName().Version)

$types = @()
$typeLoadFailures = 0
try {
    $types = $asm.GetTypes()
    Write-Host ('TYPES_OK=' + $types.Count)
} catch {
    # PowerShell wraps the real ReflectionTypeLoadException in a
    # MethodInvocationException; the loader detail is only on the inner one.
    $ex = $_.Exception
    while ($ex.InnerException -and -not ($ex -is [System.Reflection.ReflectionTypeLoadException])) {
        $ex = $ex.InnerException
    }
    if ($ex -is [System.Reflection.ReflectionTypeLoadException]) {
        $types = @($ex.Types | Where-Object { $_ -ne $null })
        $msgs = @($ex.LoaderExceptions | ForEach-Object { $_.Message } | Sort-Object -Unique)
        $typeLoadFailures = $msgs.Count
        Write-Host ('TYPES_PARTIAL=' + $types.Count)
        Write-Host ('LOADER_EXCEPTIONS=' + $msgs.Count)
        foreach ($m in $msgs) { Write-Host ('  LOADER: ' + $m) }
    } else {
        $typeLoadFailures = 1
        Write-Host ('TYPES_FAIL: ' + $ex.GetType().FullName + ' ' + $ex.Message)
    }
}

$flags = [System.Reflection.BindingFlags]::Public -bor
         [System.Reflection.BindingFlags]::NonPublic -bor
         [System.Reflection.BindingFlags]::Instance -bor
         [System.Reflection.BindingFlags]::Static -bor
         [System.Reflection.BindingFlags]::DeclaredOnly

$memberFail = 0
foreach ($t in $types) {
    try {
        [void]$t.GetMethods($flags)
        [void]$t.GetFields($flags)
        [void]$t.GetProperties($flags)
        if ($t.BaseType) { [void]$t.BaseType.FullName }
    } catch {
        $memberFail++
        Write-Host ('  MEMBER_FAIL ' + $t.FullName + ': ' + $_.Exception.GetBaseException().Message)
    }
}
Write-Host ('MEMBER_FAIL_COUNT=' + $memberFail)

# Harmony binding is the second half of the answer: a patch class whose target
# method no longer exists throws at Patch() time, which in the game surfaces as
# a startup crash rather than a load refusal.
$h = New-Object HarmonyLib.Harmony('gwp.compat.preflight')
$ok = 0
$fail = 0
foreach ($t in $types) {
    $isPatch = $false
    try {
        if (@($t.GetCustomAttributes([HarmonyLib.HarmonyPatch], $false)).Count -gt 0) { $isPatch = $true }
        if (-not $isPatch) {
            foreach ($m in $t.GetMethods($flags)) {
                if ($m.Name -eq 'TargetMethods' -or $m.Name -eq 'TargetMethod') { $isPatch = $true; break }
            }
        }
    } catch { }
    if (-not $isPatch) { continue }
    try {
        $processor = New-Object HarmonyLib.PatchClassProcessor($h, $t)
        $result = $processor.Patch()
        $n = 0
        if ($result) { $n = @($result).Count }
        $ok++
        Write-Host ('  PATCH_OK ' + $t.Name + ' targets=' + $n)
    } catch {
        $fail++
        Write-Host ('  PATCH_FAIL ' + $t.Name + ': ' + $_.Exception.GetBaseException().Message)
    }
}
Write-Host ('PATCH_OK=' + $ok + '; PATCH_FAIL=' + $fail)

if ($typeLoadFailures -gt 0 -or $memberFail -gt 0 -or $fail -gt 0) {
    Write-Host 'PREFLIGHT=FAIL'
    exit 1
}
Write-Host 'PREFLIGHT=PASS'
exit 0
