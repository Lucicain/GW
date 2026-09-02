<#
.SYNOPSIS
    Checks a built GreyWarden module DLL against the currently installed
    Bannerlord, so a game-version switch cannot silently produce a module the
    game refuses to load.

.DESCRIPTION
    Two independent checks, because they catch different failures:

    1. Reference differential (this script). Every TypeReference and
       MemberReference the module makes into the game assemblies is compared
       against what the installed game defines. This finds calls that compiled
       fine on the other generation and would throw MissingMethodException at
       runtime - for example Mission.SpawnAgent gaining a parameter - as well
       as types that do not exist at all, such as TaleWorlds.Core.BattleEnvironment
       which arrived with 1.5.
    2. Load and Harmony preflight (Invoke-ModuleLoadPreflight.ps1, launched
       under Windows PowerShell 5.1). This actually loads the assembly and
       binds every patch class, which is what reproduces the startup failure
       "GreyWarden could not be loaded correctly".

    The known hard break between generations is
    AgentApplyDamageModel.CalculatePassiveAttackDamage: 1.4.8 takes
    BasicCharacterObject, 1.5.0 and later take AttackInformation. One assembly
    cannot satisfy both, so the mod is built per generation and the csproj
    picks the branch from the installed Native/SubModule.xml. Rebuilding
    against the installed game is therefore the fix for every finding here;
    this script exists to prove the rebuild landed.

.PARAMETER ModuleDll
    The DLL to check. Defaults to the live test module's client binary.

.PARAMETER GameFolder
    The Bannerlord installation to check against.

.PARAMETER SkipLoadPreflight
    Run only the reference differential. Use when Windows PowerShell 5.1 is
    unavailable.

.EXAMPLE
    pwsh tools\Verify-GameCompat.ps1
#>
[CmdletBinding()]
param(
    [string]$ModuleDll = 'D:\steam\steamapps\common\Mount & Blade II Bannerlord\Modules\GreyWarden\bin\Win64_Shipping_Client\GreyWardenPolicePurity.dll',
    [string]$GameFolder = 'D:\steam\steamapps\common\Mount & Blade II Bannerlord',
    [switch]$SkipLoadPreflight
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path $ModuleDll)) { throw "Module DLL not found: $ModuleDll" }
if ($PSVersionTable.PSVersion.Major -lt 7) {
    throw 'Run this script under PowerShell 7 (pwsh); it needs System.Reflection.Metadata.'
}

Add-Type -AssemblyName System.Reflection.Metadata -ErrorAction SilentlyContinue

$nativeXml = [xml](Get-Content (Join-Path $GameFolder 'Modules\Native\SubModule.xml'))
Write-Host ('Installed game : ' + $nativeXml.Module.Version.value)
Write-Host ('Module DLL     : ' + $ModuleDll)
Write-Host ''

# ---------------------------------------------------------------- helpers ---

# Parameter count straight out of the signature blob. Decoding the parameter
# types would need an ISignatureTypeProvider implementation, which PowerShell
# classes cannot express against a generic interface; the count alone is enough
# to catch an overload that changed arity between generations.
function Get-SignatureParameterCount {
    param([System.Reflection.Metadata.MetadataReader]$Reader,
          [System.Reflection.Metadata.BlobHandle]$Signature)
    $blob = $Reader.GetBlobReader($Signature)
    $header = $blob.ReadByte()
    if (($header -band 0x0F) -eq 0x06) { return -1 }   # field, not a method
    if (($header -band 0x10) -ne 0) { [void]$blob.ReadCompressedInteger() }  # generic arity
    return $blob.ReadCompressedInteger()
}

function Get-TypeDefinitionFullName {
    param([System.Reflection.Metadata.MetadataReader]$Reader,
          [System.Reflection.Metadata.TypeDefinition]$Type)
    $name = $Reader.GetString($Type.Name)
    if ($Type.IsNested) {
        $declaring = $Reader.GetTypeDefinition($Type.GetDeclaringType())
        return (Get-TypeDefinitionFullName $Reader $declaring) + '+' + $name
    }
    if ($Type.Namespace.IsNil) { return $name }
    $ns = $Reader.GetString($Type.Namespace)
    if ($ns.Length -eq 0) { return $name }
    return $ns + '.' + $name
}

# ------------------------------------------------ index the installed game ---

$gameDlls = [System.Collections.Generic.List[string]]::new()
Get-ChildItem (Join-Path $GameFolder 'bin\Win64_Shipping_Client') -Filter '*.dll' |
    ForEach-Object { $gameDlls.Add($_.FullName) }
foreach ($d in (Get-ChildItem (Join-Path $GameFolder 'Modules') -Directory)) {
    if ($d.Name -eq 'GreyWarden') { continue }
    $p = Join-Path $d.FullName 'bin\Win64_Shipping_Client'
    if (Test-Path $p) { Get-ChildItem $p -Filter '*.dll' | ForEach-Object { $gameDlls.Add($_.FullName) } }
}

$types = [System.Collections.Generic.Dictionary[string, System.Collections.Generic.HashSet[string]]]::new([System.StringComparer]::Ordinal)
$indexed = 0
foreach ($dll in $gameDlls) {
    try {
        $stream = [System.IO.File]::OpenRead($dll)
        $pe = [System.Reflection.PortableExecutable.PEReader]::new($stream)
        if (-not $pe.HasMetadata) { $pe.Dispose(); $stream.Dispose(); continue }
        $md = [System.Reflection.Metadata.PEReaderExtensions]::GetMetadataReader($pe)
        foreach ($handle in $md.TypeDefinitions) {
            $td = $md.GetTypeDefinition($handle)
            $full = Get-TypeDefinitionFullName $md $td
            $members = $null
            if (-not $types.TryGetValue($full, [ref]$members)) {
                $members = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
                $types[$full] = $members
            }
            foreach ($mh in $td.GetMethods()) {
                $m = $md.GetMethodDefinition($mh)
                $mn = $md.GetString($m.Name)
                [void]$members.Add($mn)
                [void]$members.Add($mn + '(' + (Get-SignatureParameterCount $md $m.Signature) + ')')
            }
            foreach ($fh in $td.GetFields()) {
                [void]$members.Add($md.GetString($md.GetFieldDefinition($fh).Name))
            }
        }
        $indexed++
        $pe.Dispose()
        $stream.Dispose()
    } catch { }
}
Write-Host ("Indexed {0} game assemblies, {1} types." -f $indexed, $types.Count)

# ------------------------------------------------- walk the module's refs ---

$stream = [System.IO.File]::OpenRead($ModuleDll)
$pe = [System.Reflection.PortableExecutable.PEReader]::new($stream)
$mr = [System.Reflection.Metadata.PEReaderExtensions]::GetMetadataReader($pe)

$typeRefName = {
    param($handle)
    $tr = $mr.GetTypeReference($handle)
    $n = $mr.GetString($tr.Name)
    if ($tr.ResolutionScope.Kind -eq [System.Reflection.Metadata.HandleKind]::TypeReference) {
        return (& $typeRefName ([System.Reflection.Metadata.TypeReferenceHandle]$tr.ResolutionScope)) + '+' + $n
    }
    if ($tr.Namespace.IsNil) { return $n }
    $ns = $mr.GetString($tr.Namespace)
    if ($ns.Length -eq 0) { return $n }
    return $ns + '.' + $n
}

$isGameScope = {
    param($handle)
    $scope = $mr.GetTypeReference($handle).ResolutionScope
    while ($scope.Kind -eq [System.Reflection.Metadata.HandleKind]::TypeReference) {
        $scope = $mr.GetTypeReference([System.Reflection.Metadata.TypeReferenceHandle]$scope).ResolutionScope
    }
    if ($scope.Kind -ne [System.Reflection.Metadata.HandleKind]::AssemblyReference) { return $false }
    $name = $mr.GetString($mr.GetAssemblyReference([System.Reflection.Metadata.AssemblyReferenceHandle]$scope).Name)
    return ($name -like 'TaleWorlds*' -or $name -like 'SandBox*' -or $name -like 'StoryMode*' -or $name -like 'Helpers*')
}

$missingTypes = [System.Collections.Generic.SortedSet[string]]::new([System.StringComparer]::Ordinal)
$missingMembers = [System.Collections.Generic.SortedSet[string]]::new([System.StringComparer]::Ordinal)
$checkedTypes = 0
$checkedMembers = 0

foreach ($handle in $mr.TypeReferences) {
    if (-not (& $isGameScope $handle)) { continue }
    $checkedTypes++
    $n = & $typeRefName $handle
    if (-not $types.ContainsKey($n)) { [void]$missingTypes.Add($n) }
}

foreach ($handle in $mr.MemberReferences) {
    $ref = $mr.GetMemberReference($handle)
    if ($ref.Parent.Kind -ne [System.Reflection.Metadata.HandleKind]::TypeReference) { continue }
    $parent = [System.Reflection.Metadata.TypeReferenceHandle]$ref.Parent
    if (-not (& $isGameScope $parent)) { continue }
    $tn = & $typeRefName $parent
    $members = $null
    if (-not $types.TryGetValue($tn, [ref]$members)) { continue }  # reported as a missing type
    $checkedMembers++
    $mn = $mr.GetString($ref.Name)
    $argc = Get-SignatureParameterCount $mr $ref.Signature
    if ($argc -lt 0) {
        if (-not $members.Contains($mn)) { [void]$missingMembers.Add($tn + '::' + $mn + '  [field]') }
    } elseif (-not $members.Contains($mn + '(' + $argc + ')')) {
        $why = if ($members.Contains($mn)) { '  [name exists, arity differs]' } else { '  [name absent]' }
        [void]$missingMembers.Add($tn + '::' + $mn + '/' + $argc + $why)
    }
}

$pe.Dispose()
$stream.Dispose()

Write-Host ("Checked {0} type references and {1} member references." -f $checkedTypes, $checkedMembers)
Write-Host ''
Write-Host ('MISSING TYPES  : ' + $(if ($missingTypes.Count -eq 0) { 'none' } else { $missingTypes.Count }))
foreach ($t in $missingTypes) { Write-Host ('  ' + $t) }
Write-Host ('MISSING MEMBERS: ' + $(if ($missingMembers.Count -eq 0) { 'none' } else { $missingMembers.Count }))
foreach ($m in $missingMembers) { Write-Host ('  ' + $m) }

$failed = ($missingTypes.Count -gt 0 -or $missingMembers.Count -gt 0)

# ------------------------------------------------------- load preflight ---

if (-not $SkipLoadPreflight) {
    Write-Host ''
    Write-Host '--- load and Harmony preflight (Windows PowerShell 5.1) ---'
    $winPs = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
    if (-not (Test-Path $winPs)) {
        Write-Host 'Windows PowerShell 5.1 not found; load preflight skipped.'
    } else {
        $preflight = Join-Path $PSScriptRoot 'Invoke-ModuleLoadPreflight.ps1'
        $output = & $winPs -NoProfile -ExecutionPolicy Bypass -File $preflight -ModuleDll $ModuleDll -GameFolder $GameFolder 2>&1
        $output | Where-Object { $_ -notmatch '^\s*PATCH_OK ' } | ForEach-Object { Write-Host $_ }
        Write-Host (($output | Where-Object { $_ -match '^\s*PATCH_OK ' } | Measure-Object).Count.ToString() + ' patch classes bound (listed individually in the raw output).')
        if ($LASTEXITCODE -ne 0) { $failed = $true }
    }
}

Write-Host ''
if ($failed) {
    Write-Host 'GAME COMPAT: FAIL - rebuild the module against the installed game.'
    exit 1
}
Write-Host 'GAME COMPAT: PASS'
exit 0
