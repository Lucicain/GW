<#
.SYNOPSIS
    Checks the live test module against the repository module, in both
    directions.

.DESCRIPTION
    The build's deployment step only ever copies files in; it never deletes,
    because it must not touch the editor-side Assets tree. Anything that once
    landed in the live module therefore stays there forever unless someone
    removes it by hand - which is how a stale docs\maintenance-plan.md sat in
    the live module for days without being noticed.

    Checking only "every repository file is present and identical in live"
    cannot catch that. This checks the reverse as well: live must contain
    nothing beyond the repository module plus the two generated directories
    AGENTS.md allows there, bin and Shaders.

.PARAMETER RepoModule
    The repository module directory. Defaults to the one beside this script.

.PARAMETER LiveModule
    The installed test module.

.EXAMPLE
    pwsh tools\Verify-LiveModule.ps1
#>
[CmdletBinding()]
param(
    [string]$RepoModule = (Join-Path (Split-Path $PSScriptRoot -Parent) 'GreyWardenPolicePurity\_Module'),
    [string]$LiveModule = 'D:\steam\steamapps\common\Mount & Blade II Bannerlord\Modules\GreyWarden'
)

$ErrorActionPreference = 'Stop'

# Editor-side trees deliberately kept out of the normal-client module.
# Anchored at the start: these paths are relative, so they have no leading
# separator to match against.
$repoOnly = '^(Assets|AssetSources|RuntimeDataCache|obj)\\'
# The only directories allowed to exist in live without a repository counterpart.
$liveOnly = '^(bin|Shaders)\\'

function Get-Rel([string]$root) {
    Get-ChildItem $root -Recurse -File |
        ForEach-Object { $_.FullName.Substring($root.Length + 1) }
}

if (-not (Test-Path $RepoModule)) { throw "repository module not found: $RepoModule" }
if (-not (Test-Path $LiveModule)) { throw "live module not found: $LiveModule" }

$repoFiles = @(Get-Rel $RepoModule | Where-Object { $_ -notmatch $repoOnly })
$liveFiles = @(Get-Rel $LiveModule)

$missing = @()
$differing = @()
foreach ($rel in $repoFiles) {
    $target = Join-Path $LiveModule $rel
    if (-not (Test-Path $target)) { $missing += $rel; continue }
    $a = (Get-FileHash (Join-Path $RepoModule $rel) -Algorithm SHA256).Hash
    $b = (Get-FileHash $target -Algorithm SHA256).Hash
    if ($a -ne $b) { $differing += $rel }
}

$repoSet = [System.Collections.Generic.HashSet[string]]::new([string[]]$repoFiles, [StringComparer]::OrdinalIgnoreCase)
$stray = @($liveFiles | Where-Object { -not $repoSet.Contains($_) -and $_ -notmatch $liveOnly })

Write-Host ("repository files checked : {0}" -f $repoFiles.Count)
Write-Host ("live files seen          : {0}" -f $liveFiles.Count)
Write-Host ''

foreach ($group in @(
    @{ Name = 'MISSING FROM LIVE'; Items = $missing },
    @{ Name = 'DIFFERENT IN LIVE';  Items = $differing },
    @{ Name = 'STRAY IN LIVE (not in the repository, and not bin/ or Shaders/)'; Items = $stray })) {
    if ($group.Items.Count -eq 0) {
        Write-Host ("{0}: none" -f $group.Name) -ForegroundColor Green
    } else {
        Write-Host ("{0}: {1}" -f $group.Name, $group.Items.Count) -ForegroundColor Red
        $group.Items | ForEach-Object { Write-Host "   $_" }
    }
}

if ($missing.Count -or $differing.Count -or $stray.Count) {
    Write-Host ''
    Write-Host 'Live module does NOT mirror the repository. Do not accept an in-game test result.' -ForegroundColor Red
    exit 1
}

Write-Host ''
Write-Host 'Live module mirrors the repository.' -ForegroundColor Green
