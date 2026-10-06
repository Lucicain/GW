param([Parameter(Mandatory=$true)][string]$ModuleDll, [switch]$WithoutRBM)
$ErrorActionPreference = 'Stop'
$taskGame = 'D:\steam\steamapps\common\Mount & Blade II Bannerlord'
foreach ($taskFile in (Get-ChildItem -LiteralPath (Join-Path $taskGame 'bin\Win64_Shipping_Client') -Filter 'TaleWorlds.*.dll')) {
    if ($taskFile.Name -ne 'TaleWorlds.Native.dll') { [void][Reflection.Assembly]::LoadFrom($taskFile.FullName) }
}
[void][Reflection.Assembly]::LoadFrom((Join-Path (Split-Path $ModuleDll -Parent) '0Harmony.dll'))
if (-not $WithoutRBM) {
    [void][Reflection.Assembly]::LoadFrom((Join-Path $taskGame 'Modules\RBM\bin\Win64_Shipping_Client\RBMCampaign.dll'))
}
$taskAssembly = [Reflection.Assembly]::LoadFrom($ModuleDll)
$taskHarmony = New-Object HarmonyLib.Harmony('gwp.auto-equipment.regression')
$taskPatch = $taskAssembly.GetType('GreyWardenPolicePurity.GwpWardenAutoEquipmentPatch', $true)
$taskPatched = $taskHarmony.CreateClassProcessor($taskPatch).Patch()
$taskChecks = 0
function Assert-Condition([bool]$Condition, [string]$Label) {
    if (-not $Condition) { throw $Label }
    $script:taskChecks++
}
Assert-Condition ($taskPatched.Count -eq 1) 'Native candidate patch was not bound exactly once'
Assert-Condition (-not ($taskAssembly.GetReferencedAssemblies().Name -contains 'RBMCampaign')) 'Unexpected RBM dependency'
function New-TestCharacter([string]$Id) {
    $taskClan = [Runtime.Serialization.FormatterServices]::GetUninitializedObject([TaleWorlds.CampaignSystem.Clan])
    [TaleWorlds.ObjectSystem.MBObjectBase].GetProperty('StringId').SetValue($taskClan, $Id, $null)
    $taskHero = [Runtime.Serialization.FormatterServices]::GetUninitializedObject([TaleWorlds.CampaignSystem.Hero])
    [TaleWorlds.CampaignSystem.Hero].GetField('_clan', 'Instance,NonPublic').SetValue($taskHero, $taskClan)
    [TaleWorlds.ObjectSystem.MBObjectBase].GetProperty('StringId').SetValue($taskHero, ('hero_' + $Id), $null)
    $taskCharacter = [Runtime.Serialization.FormatterServices]::GetUninitializedObject([TaleWorlds.CampaignSystem.CharacterObject])
    [TaleWorlds.CampaignSystem.CharacterObject].GetField('_heroObject', 'Instance,NonPublic').SetValue($taskCharacter, $taskHero)
    [TaleWorlds.CampaignSystem.Hero].GetField('_characterObject', 'Instance,NonPublic').SetValue($taskHero, $taskCharacter)
    return $taskCharacter
}
$taskItem = [TaleWorlds.Core.ItemObject]::new('auto_equipment_probe')
$taskItem.Type = [TaleWorlds.Core.ItemObject+ItemTypeEnum]::BodyArmor
[TaleWorlds.Core.ItemObject].GetProperty('TierfOverride', [Reflection.BindingFlags]'Instance,NonPublic').SetValue($taskItem, [single]6, $null)
$taskElement = [TaleWorlds.Core.EquipmentElement]::new($taskItem, $null)
$taskOrdinary = New-TestCharacter 'ordinary'
Assert-Condition ([Helpers.CharacterHelper]::CanUseItem($taskOrdinary, $taskElement)) 'Ordinary hero candidate changed'
$taskTroop = [Runtime.Serialization.FormatterServices]::GetUninitializedObject([TaleWorlds.CampaignSystem.CharacterObject])
Assert-Condition ([Helpers.CharacterHelper]::CanUseItem($taskTroop, $taskElement)) 'Non-hero candidate changed'
$taskBasic = [Runtime.Serialization.FormatterServices]::GetUninitializedObject([TaleWorlds.Core.BasicCharacterObject])
Assert-Condition ([Helpers.CharacterHelper]::CanUseItem($taskBasic, $taskElement)) 'Basic character candidate changed'
$taskRoster = [TaleWorlds.CampaignSystem.Roster.ItemRoster]::new()
$taskData = [TaleWorlds.Core.ItemRosterElement[]]@([TaleWorlds.Core.ItemRosterElement]::new($taskElement, 1))
[TaleWorlds.CampaignSystem.Roster.ItemRoster].GetField('_data', 'Instance,NonPublic').SetValue($taskRoster, $taskData)
[TaleWorlds.CampaignSystem.Roster.ItemRoster].GetField('_count', 'Instance,NonPublic').SetValue($taskRoster, 1)
foreach ($taskId in 'gw','GW') {
    $taskCharacter = New-TestCharacter $taskId
    Assert-Condition (-not [Helpers.CharacterHelper]::CanUseItem($taskCharacter, $taskElement)) ($taskId + ': auto candidate accepted')
    $taskReason = $null
    Assert-Condition ([Helpers.CharacterHelper]::CanUseItem($taskCharacter, $taskElement, [ref]$taskReason)) ($taskId + ': manual equipment overload changed')
    Assert-Condition ($taskReason -eq $null) ($taskId + ': manual equipment reason changed')
    if (-not $WithoutRBM) {
        $taskUsable = [HarmonyLib.AccessTools]::Method('RBMCampaign.LordEquipmentUpgrade:FindBestUsable')
        $taskArgs = [object[]]@($taskRoster, [TaleWorlds.Core.EquipmentIndex]::Body, $null, $taskCharacter.HeroObject, [single]0, [single]0)
        Assert-Condition ($taskUsable.Invoke($null, $taskArgs) -eq -1) ($taskId + ': RBM baggage candidate accepted')
        Assert-Condition ($taskArgs[5] -eq 0) ($taskId + ': RBM baggage tier changed')
        $taskAffordable = [HarmonyLib.AccessTools]::Method('RBMCampaign.LordEquipmentUpgrade:FindBestAffordable')
        $taskArgs = [object[]]@($taskRoster, [TaleWorlds.Core.EquipmentIndex]::Body, $null, $taskCharacter.HeroObject, $null, $null, [single]0, 5000, [single]0, 0)
        Assert-Condition ($taskAffordable.Invoke($null, $taskArgs) -eq -1) ($taskId + ': RBM market candidate accepted')
        Assert-Condition ($taskArgs[8] -eq 0 -and $taskArgs[9] -eq 0) ($taskId + ': RBM market candidate acquired tier/price')
        Assert-Condition ($taskRoster.Count -eq 1 -and $taskRoster.GetElementNumber(0) -eq 1) ($taskId + ': candidate search changed inventory')
    }
}
if (-not $WithoutRBM) {
    $taskArgs = [object[]]@($taskRoster, [TaleWorlds.Core.EquipmentIndex]::Body, $null, $taskOrdinary.HeroObject, [single]0, [single]0)
    Assert-Condition ($taskUsable.Invoke($null, $taskArgs) -eq 0 -and $taskArgs[5] -eq 5) 'Ordinary RBM hero no longer selects better armor'
    $taskInfo = [HarmonyLib.Harmony]::GetPatchInfo($taskUsable)
    Assert-Condition ($taskInfo -eq $null -or -not ($taskInfo.Owners -contains $taskHarmony.Id)) 'Test or feature patched third-party method'
}
Write-Output ('AUTO_EQUIPMENT_REGRESSION=PASS; ASSERTIONS=' + $taskChecks + '; WITHOUT_RBM=' + $WithoutRBM)
