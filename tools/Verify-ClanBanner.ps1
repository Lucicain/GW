<# Offline checks of the prepared banner against the installed game's schema
   and actual managed DLLs. Run with Windows PowerShell 5.1; no engine launch. #>
[CmdletBinding()]
param(
    [string]$GameFolder = 'D:\steam\steamapps\common\Mount & Blade II Bannerlord',
    [switch]$Installed
)
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -ne 5) {
    throw 'Run with powershell.exe (Windows PowerShell 5.1), like the game load preflight.'
}
$repoRoot = Split-Path $PSScriptRoot -Parent
$prepared = Join-Path $repoRoot 'GreyWardenPolicePurity/docs/reference/banner-review/kit-input'
$candidate = Join-Path $prepared 'banner_icons.xml'
if ($Installed) {
    $candidate = Join-Path $repoRoot 'GreyWardenPolicePurity/_Module/ModuleData/banner_icons.xml'
}
$document = [xml](Get-Content -LiteralPath $candidate -Raw)
$null = $document.Schemas.Add('', (Join-Path $GameFolder 'XmlSchemas/BannerIcons.xsd'))
$document.Validate($null)
Write-Output 'BANNER_XSD=PASS'
$nativeFiles = @(
    (Join-Path $GameFolder 'Modules/Native/ModuleData/banner_icons.xml'),
    (Join-Path $GameFolder 'Modules/NavalDLC/ModuleData/banner_icons.xml')
)
foreach ($nativeFile in $nativeFiles) {
    $nativeDocument = [xml](Get-Content -LiteralPath $nativeFile -Raw)
    foreach ($selector in @("//BannerIconGroup[@id='63']", "//Icon[@id='63010']", "//Color[@id='63001' or @id='63002']")) {
        if ($nativeDocument.SelectNodes($selector).Count -ne 0) { throw "ID collision in $nativeFile" }
    }
}
Write-Output 'BANNER_IDS=PASS'
foreach ($assemblyName in @('TaleWorlds.Library', 'TaleWorlds.Localization', 'TaleWorlds.SaveSystem', 'TaleWorlds.ObjectSystem', 'TaleWorlds.Core')) {
    $null = [Reflection.Assembly]::LoadFrom((Join-Path $GameFolder "bin/Win64_Shipping_Client/$assemblyName.dll"))
}
[TaleWorlds.Core.BannerManager]::Initialize()
$manager = [TaleWorlds.Core.BannerManager]::Instance
foreach ($nativeFile in $nativeFiles) { $manager.LoadBannerIcons($nativeFile) }
$manager.LoadBannerIcons($candidate)
$manifest = Get-Content (Join-Path $prepared 'manifest.json') -Raw | ConvertFrom-Json
$banner = [TaleWorlds.Core.Banner]::new($manifest.banner_code)
if ($banner.GetBannerDataListCount() -ne 2) { throw 'Wrong layer count' }
if ($banner.GetIconMeshId() -ne 63010) { throw 'Wrong icon' }
$icon = $manager.GetIconDataFromIconId(63010)
if ($icon.MaterialName -ne 'gwp_commander_banner' -or $icon.TextureIndex -ne 0) { throw 'Wrong material or tile' }
$gold = [Convert]::ToUInt32('FFFDDC88', 16)
$black = [Convert]::ToUInt32('FF1A1A1A', 16)
if ($banner.GetPrimaryColor() -ne $black -or $banner.GetFirstIconColor() -ne $gold) { throw 'Wrong color' }
$banner.ChangeIconColors($banner.GetFirstIconColor())
$roundtrip = [TaleWorlds.Core.Banner]::new($banner.Serialize())
if ($roundtrip.GetFirstIconColor() -ne $gold -or $roundtrip.GetPrimaryColor() -ne $black) { throw 'Tint changed after normalization' }
$group = @($manager.BannerIconGroups | Where-Object Id -eq 63)[0]
if ($group.AvailableIcons.ContainsKey(63010)) { throw 'Private icon should not be randomly generated' }
if ((Get-FileHash -LiteralPath $manifest.live_dds -Algorithm SHA256).Hash.ToLowerInvariant() -ne $manifest.dds_sha256) {
    throw 'Prepared DDS changed since encoding'
}
Write-Output 'GAME_DLL_BANNER=PASS: 2 layers, correct material/tile, black/gold exact, normalization and code roundtrip stable, reserved icon'
if ($Installed) {
    $repoModule = Join-Path $repoRoot 'GreyWardenPolicePurity/_Module'
    $liveModule = Join-Path $GameFolder 'Modules/GreyWarden'
    foreach ($relativePath in @('SubModule.xml', 'ModuleData/banner_icons.xml', 'ModuleData/spclans.xml')) {
        if ((Get-FileHash (Join-Path $repoModule $relativePath)).Hash -ne (Get-FileHash (Join-Path $liveModule $relativePath)).Hash) {
            throw "Runtime mirror differs: $relativePath"
        }
    }
    $clans = [xml](Get-Content (Join-Path $repoModule 'ModuleData/spclans.xml') -Raw)
    if ($clans.SelectSingleNode("/Factions/Faction[@id='gw']").banner_key -ne $manifest.banner_code) {
        throw 'Grey Warden clan is not using the verified banner code'
    }
    $submodule = [xml](Get-Content (Join-Path $repoModule 'SubModule.xml') -Raw)
    if ($submodule.SelectNodes("/Module/Xmls/XmlNode/XmlName[@id='BannerIcons' and @path='banner_icons']").Count -ne 1) {
        throw 'BannerIcons XML registration is missing or duplicated'
    }
    Write-Output 'INSTALLED_XML=PASS: correct clan banner, one registration, all 3 live XML hashes match'
}
Write-Output 'RUNTIME_RENDER=NOT_TESTED: this check does not run the engine'
