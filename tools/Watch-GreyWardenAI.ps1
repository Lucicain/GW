param(
    [string]$Party = '',
    [ValidateSet('', 'grey_warden_lord', 'leaderless_picket', 'leaderless_delay_support', 'leaderless_grey_warden')]
    [string]$Kind = '',
    [switch]$Once,
    [int]$Tail = 120,
    [string]$GameFolder = 'D:\steam\steamapps\common\Mount & Blade II Bannerlord'
)

$ErrorActionPreference = 'Stop'
$logPath = Join-Path ([Environment]::GetFolderPath('MyDocuments')) `
    'Mount and Blade II Bannerlord\GreyWarden-AI-Diagnostics.log'
$versionPath = Join-Path $GameFolder 'bin\Win64_Shipping_Client\Version.xml'
$manifestPath = Join-Path $GameFolder 'Modules\GreyWarden\SubModule.xml'
$moduleDllPath = Join-Path $GameFolder 'Modules\GreyWarden\bin\Win64_Shipping_Client\GreyWardenPolicePurity.dll'

$installedVersion = ''
if (Test-Path -LiteralPath $versionPath) {
    [xml]$versionXml = Get-Content -LiteralPath $versionPath -Raw
    $installedVersion = [string]$versionXml.Version.Singleplayer.Value
    Write-Host "Installed Bannerlord: $installedVersion" -ForegroundColor Cyan
}
else {
    Write-Warning "Bannerlord version file was not found: $versionPath"
}

if (Test-Path -LiteralPath $manifestPath) {
    [xml]$manifestXml = Get-Content -LiteralPath $manifestPath -Raw
    $versionedDependencies = @($manifestXml.Module.DependedModules.DependedModule |
        Where-Object { $_.DependentVersion })
    if ($versionedDependencies.Count -eq 0) {
        Write-Host 'GreyWarden hard game-version dependencies: none' -ForegroundColor Green
    }
    else {
        $dependencyVersions = @($versionedDependencies |
            ForEach-Object { "$(($_.Id))=$([string]$_.DependentVersion)" })
        Write-Warning "GreyWarden still declares hard dependency versions: $($dependencyVersions -join ', ')"
    }
}
else {
    Write-Warning "Live GreyWarden manifest was not found: $manifestPath"
}

if (Test-Path -LiteralPath $moduleDllPath) {
    $moduleAssemblyVersion = [Reflection.AssemblyName]::GetAssemblyName($moduleDllPath).Version
    Write-Host "GreyWarden assembly: $moduleAssemblyVersion" -ForegroundColor Cyan
}

Write-Host "Grey Warden AI diagnostics: $logPath" -ForegroundColor Cyan
Write-Host 'Scope: every Grey Warden lord plus every leaderless Grey Warden party.' -ForegroundColor Cyan
Write-Host 'Kinds: grey_warden_lord, leaderless_picket, leaderless_delay_support, leaderless_grey_warden.' -ForegroundColor Cyan
Write-Host 'Rows include task, desire, army, assistance, troops, food, wages, gold, prisoners, and target state.' -ForegroundColor Cyan
if ($Party) { Write-Host "Party filter: $Party" -ForegroundColor Cyan }
if ($Kind) { Write-Host "Kind filter: $Kind" -ForegroundColor Cyan }

while (-not (Test-Path -LiteralPath $logPath)) {
    if ($Once) {
        Write-Error 'Diagnostic log does not exist yet. Start/load a campaign with the current build first.'
    }
    Write-Host 'Waiting for the game to create the diagnostic log...'
    Start-Sleep -Seconds 1
}

function Select-GwpLine {
    process {
        $partyMatches = -not $Party -or $_ -match [regex]::Escape($Party)
        $kindMatches = -not $Kind -or $_ -match [regex]::Escape("partyKind=$Kind")
        if ($partyMatches -and $kindMatches) { $_ }
    }
}

if ($Once) {
    Get-Content -LiteralPath $logPath -Tail $Tail | Select-GwpLine
    exit 0
}

Write-Host 'Live watch started. Press Ctrl+C to stop.' -ForegroundColor Green
Get-Content -LiteralPath $logPath -Tail $Tail -Wait | Select-GwpLine
