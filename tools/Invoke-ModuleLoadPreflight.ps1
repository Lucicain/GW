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
# A successful Patch() can still poison a type initializer: MonoMod's eager JIT
# can consume the original exception. This happened to CampaignUIHelper on
# 2026-10-20 before GameTexts existed; observe that exact failure as well.
Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
public static class GwpPreflightInitializationWatch
{
    public static string PatchName;
    public static readonly List<string> Failures = new List<string>();
    public static void Start() { AppDomain.CurrentDomain.FirstChanceException += Observe; }
    public static void Stop() { AppDomain.CurrentDomain.FirstChanceException -= Observe; }
    private static void Observe(object sender, FirstChanceExceptionEventArgs args)
    {
        if (!(args.Exception is NullReferenceException)) return;
        foreach (StackFrame frame in new StackTrace().GetFrames())
        {
            var method = frame.GetMethod();
            if (method != null && method.Name == ".cctor" && method.DeclaringType != null &&
                method.DeclaringType.FullName == "TaleWorlds.CampaignSystem.ViewModelCollection.CampaignUIHelper")
            {
                Failures.Add(PatchName + ": CampaignUIHelper initialized before GameTexts: " + args.Exception.Message);
                break;
            }
        }
    }
}
'@
[GwpPreflightInitializationWatch]::Start()
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
    [GwpPreflightInitializationWatch]::PatchName = $t.FullName
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
[GwpPreflightInitializationWatch]::Stop()
# The framework may defer .cctor execution in this host even though the game
# eagerly executes it. Check the exact installed IL dependency that caused the
# observed failure, as well as exceptions actually seen in this process.
$earlyUiFieldFaults = 0
$instructionReader = [HarmonyLib.PatchProcessor].GetMethod('GetOriginalInstructions',
    [type[]]@([System.Reflection.MethodBase], [System.Reflection.Emit.ILGenerator]))
foreach ($original in [HarmonyLib.Harmony]::GetAllPatchedMethods()) {
    if ($original.DeclaringType.FullName -ne 'SandBox.ViewModelCollection.Nameplate.PartyNameplateVM') { continue }
    $instructions = $instructionReader.Invoke($null, [object[]]@($original, $null))
    foreach ($instruction in $instructions) {
        if ($instruction.opcode -eq [System.Reflection.Emit.OpCodes]::Ldsfld -and
            $instruction.operand -is [System.Reflection.FieldInfo] -and
            $instruction.operand.DeclaringType.FullName -eq 'TaleWorlds.CampaignSystem.ViewModelCollection.CampaignUIHelper') {
            $earlyUiFieldFaults++
            Write-Host ('  EARLY_UI_FIELD_FAIL ' + $original.Name + ' -> CampaignUIHelper.' + $instruction.operand.Name)
        }
    }
}
Write-Host ('EARLY_UI_FIELD_FAIL_COUNT=' + $earlyUiFieldFaults)
foreach ($initializationFailure in [GwpPreflightInitializationWatch]::Failures) {
    Write-Host ('  STATIC_INIT_FAIL ' + $initializationFailure)
}
Write-Host ('STATIC_INIT_FAIL_COUNT=' + [GwpPreflightInitializationWatch]::Failures.Count)
Write-Host ('PATCH_OK=' + $ok + '; PATCH_FAIL=' + $fail)

if ($typeLoadFailures -gt 0 -or $memberFail -gt 0 -or $fail -gt 0 -or $earlyUiFieldFaults -gt 0 -or [GwpPreflightInitializationWatch]::Failures.Count -gt 0) {
    Write-Host 'PREFLIGHT=FAIL'
    exit 1
}
Write-Host 'PREFLIGHT=PASS'
exit 0
