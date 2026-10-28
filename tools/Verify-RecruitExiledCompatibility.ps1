# 加载真实灰袍与 Recruit Exiled Clans DLL，验证自动投国候选、动作拦截和误导通知。
# 夹具只建立必要的真实托管对象及 MCM 内存设置提供者；不启动游戏，不修改模组文件或配置。
# 负对照撤销本测试自己的 Harmony 拦截，只验证进入原版动作；不声称完整战役实际入国。
param(
    [Parameter(Mandatory=$true)][string]$ModuleDll,
    [string]$GameFolder = 'D:/steam/steamapps/common/Mount & Blade II Bannerlord',
    [string]$RecruitDll,
    [switch]$WithInstalledDiplomacyPatches
)
$ErrorActionPreference = 'Stop'
$taskGame = $GameFolder
if (-not $RecruitDll) { $RecruitDll = "$taskGame/Modules/RecruitExiledClans/bin/Win64_Shipping_Client/RecruitExiledClans.dll" }
foreach ($taskFile in Get-ChildItem -LiteralPath "$taskGame/bin/Win64_Shipping_Client" -Filter '*.dll') {
    if ($taskFile.Name -like 'TaleWorlds.*' -and $taskFile.Name -ne 'TaleWorlds.Native.dll') { [void][Reflection.Assembly]::LoadFrom($taskFile.FullName) }
}
$taskMcm = "$taskGame/Modules/Bannerlord.MBOptionScreen/bin/Win64_Shipping_Client/MCMv5.dll"
[void][Reflection.Assembly]::LoadFrom($taskMcm)
[void][Reflection.Assembly]::LoadFrom((Join-Path (Split-Path $ModuleDll -Parent) '0Harmony.dll'))
$taskWarden = [Reflection.Assembly]::LoadFrom($ModuleDll)
$taskRecruit = [Reflection.Assembly]::LoadFrom($RecruitDll)
$taskRefs = @($taskMcm, [TaleWorlds.CampaignSystem.Clan].Assembly.Location, [TaleWorlds.Core.Game].Assembly.Location,
    [TaleWorlds.ObjectSystem.MBObjectBase].Assembly.Location, [TaleWorlds.Library.InformationManager].Assembly.Location,
    'C:/Program Files (x86)/Reference Assemblies/Microsoft/Framework/.NETFramework/v4.7.2/Facades/netstandard.dll')
Add-Type -ReferencedAssemblies $taskRefs -TypeDefinition @'
using System;
using System.Collections.Generic;
using MCM.Abstractions;
using MCM.Abstractions.Base;
using TaleWorlds.Library;
public sealed class RecruitProbeSettings : BaseSettingsProvider {
    public static BaseSettings Settings;
    public override IEnumerable<SettingsDefinition> SettingsDefinitions { get { throw new NotSupportedException(); } }
    public override BaseSettings GetSettings(string id) { return Settings; }
    public override void SaveSettings(BaseSettings settings) { throw new NotSupportedException(); }
    public override void ResetSettings(BaseSettings settings) { throw new NotSupportedException(); }
    public override void OverrideSettings(BaseSettings settings) { throw new NotSupportedException(); }
    public override IEnumerable<ISettingsPreset> GetPresets(string id) { throw new NotSupportedException(); }
    public override IEnumerable<UnavailableSetting> GetUnavailableSettings() { throw new NotSupportedException(); }
    public override IEnumerable<SettingSnapshot> SaveAvailableSnapshots() { throw new NotSupportedException(); }
    public override IEnumerable<BaseSettings> LoadAvailableSnapshots(IEnumerable<SettingSnapshot> snapshots) { throw new NotSupportedException(); }
}
public static class RecruitProbeMessages {
    public static readonly List<string> Messages = new List<string>();
    public static void Observe(InformationMessage message) { Messages.Add(message.Information); }
    public static void Start() { InformationManager.DisplayMessageInternal += Observe; }
}
'@
$taskFlags = [Reflection.BindingFlags]'Public,NonPublic,Instance,Static'
function New-Uninitialized([type]$Type) { [Runtime.Serialization.FormatterServices]::GetUninitializedObject($Type) }
function Set-Property($Object, [string]$Name, $Value) {
    [void]$Object.GetType().GetProperty($Name,$taskFlags).GetSetMethod($true).Invoke($Object,@($Value))
}
$taskSettingsType = $taskRecruit.GetType('RecruitExiledClans.AIClansAutoJoin',$true)
[RecruitProbeSettings]::Settings = [Activator]::CreateInstance($taskSettingsType)
[MCM.Abstractions.BaseSettingsProvider].GetProperty('Instance').GetSetMethod($true).Invoke($null,@([RecruitProbeSettings]::new())) | Out-Null
$taskCampaign = New-Uninitialized ([TaleWorlds.CampaignSystem.Campaign])
[TaleWorlds.CampaignSystem.Campaign].GetProperty('Current').GetSetMethod($true).Invoke($null,@($taskCampaign)) | Out-Null
$taskTrackerType = $taskCampaign.GetType().Assembly.GetType('TaleWorlds.CampaignSystem.MapTimeTracker',$true)
Set-Property $taskCampaign 'MapTimeTracker' (New-Uninitialized $taskTrackerType)
$taskGameObject = New-Uninitialized ([TaleWorlds.Core.Game])
[TaleWorlds.Core.Game].GetField('_current',$taskFlags).SetValue($null,$taskGameObject)
$taskCharacter = New-Uninitialized ([TaleWorlds.CampaignSystem.CharacterObject])
$taskHero = New-Uninitialized ([TaleWorlds.CampaignSystem.Hero])
[TaleWorlds.CampaignSystem.CharacterObject].GetField('_heroObject',$taskFlags).SetValue($taskCharacter,$taskHero)
$taskGameObject.PlayerTroop = $taskCharacter
$taskPlayerClan = [TaleWorlds.CampaignSystem.Clan]::new()
[TaleWorlds.CampaignSystem.Hero].GetField('_clan',$taskFlags).SetValue($taskHero,$taskPlayerClan)
$taskKingdom = New-Uninitialized ([TaleWorlds.CampaignSystem.Kingdom])
$taskBehaviorType = $taskRecruit.GetType('RecruitExiledClans.RecruitExiledClans',$true)
$taskBehavior = New-Uninitialized $taskBehaviorType
[RecruitProbeMessages]::Start()
$taskJoin = $taskBehaviorType.GetMethod('JoinKingdomWithReason',$taskFlags)
$taskChecks=0
function Assert-Probe([bool]$Condition,[string]$Label) { if(-not $Condition){throw $Label}; $script:taskChecks++ }
$taskCandidateClan = [TaleWorlds.CampaignSystem.Clan]::new()
Set-Property $taskCandidateClan 'StringId' 'gw'
[TaleWorlds.CampaignSystem.Clan].GetField('_leader',$taskFlags).SetValue($taskCandidateClan,(New-Uninitialized ([TaleWorlds.CampaignSystem.Hero])))
$taskInstructionReader = [HarmonyLib.PatchProcessor].GetMethod('GetOriginalInstructions',[type[]]@([Reflection.MethodBase],[Reflection.Emit.ILGenerator]))
$taskInstructions = $taskInstructionReader.Invoke($null,[object[]]@($taskBehaviorType.GetMethod('AIClansAutoJoin',$taskFlags),$null))
$taskPredicate = @($taskInstructions | Where-Object { $_.opcode -eq [Reflection.Emit.OpCodes]::Ldftn -and $_.operand.ReturnType -eq [bool] -and $_.operand.GetParameters().Count -eq 1 -and $_.operand.GetParameters()[0].ParameterType -eq [TaleWorlds.CampaignSystem.Clan] })
Assert-Probe ($taskPredicate.Count -eq 1) 'Auto-join predicate changed; inspect installed DLL'
Assert-Probe ($taskPredicate[0].operand.Invoke($taskBehavior,@($taskCandidateClan))) 'Default filter did not select independent Warden'
[RecruitProbeSettings]::Settings.excludedClanIds += ',gw'
Assert-Probe (-not $taskPredicate[0].operand.Invoke($taskBehavior,@($taskCandidateClan))) 'Clan ID exclusion did not prevent selection'
Write-Output 'CANDIDATE_DEFAULT=True; CANDIDATE_EXCLUDED=False'
$taskHarmony = New-Object HarmonyLib.Harmony('gwp.recruit.compatibility')
$taskPatchType = $taskWarden.GetType('GreyWardenPolicePurity.GwpKingdomMembershipPatch',$true)
[void]$taskHarmony.CreateClassProcessor($taskPatchType).Patch()
if ($WithInstalledDiplomacyPatches) {
    # 只执行第三方自己的相关注册入口；不调用完整启动，不改它们的方法或磁盘配置。
    $taskOtherHarmony = [HarmonyLib.Harmony]::new('gwp.recruit.installed-mod-probe')
    $taskPersonality = [Reflection.Assembly]::LoadFrom("$taskGame/Modules/PersonalityMatters/bin/Win64_Shipping_Client/PersonalityMatters.dll")
    [void]$taskOtherHarmony.CreateClassProcessor($taskPersonality.GetType('PersonalityMatters.Patches.ClanJoinKingdomPatch',$true)).Patch()
    $taskFixes = [Reflection.Assembly]::LoadFrom("$taskGame/Modules/MutliLittleFixes/bin/Win64_Shipping_Client/MutliLittleFixes.dll")
    $taskRegistry = $taskFixes.GetType('MutliLittleFixes.Patches.HarmonyPatchRegistry',$true)
    foreach($taskRegistration in 'RegisterMercenaryClanHire','RegisterWanderingClanSurvival') {
        [void]$taskRegistry.GetMethod($taskRegistration,$taskFlags).Invoke($null,@($taskOtherHarmony))
    }
    $taskExternalPatches = @([HarmonyLib.Harmony]::GetAllPatchedMethods() | Where-Object {
        [HarmonyLib.Harmony]::GetPatchInfo($_).Owners -contains 'gwp.recruit.installed-mod-probe'
    })
    Assert-Probe ($taskExternalPatches.Count -eq 7) 'Expected diplomacy patch set changed; inspect installed DLLs'
    foreach($taskOriginal in $taskExternalPatches) {
        Write-Output ('EXTERNAL_PATCH='+$taskOriginal.DeclaringType.FullName+'.'+$taskOriginal.Name)
    }
    # 实际执行卸载回调，核对混淆的 UnpatchAll 调用没有误清灰袍；仅此测试进程受影响。
    [void][Reflection.Assembly]::LoadFrom("$taskGame/Modules/Bannerlord.UIExtenderEx/bin/Win64_Shipping_Client/Bannerlord.UIExtenderEx.dll")
    $taskFixesSubType=$taskFixes.GetType('MutliLittleFixes.SubModule',$true)
    $taskFixesSub=New-Uninitialized $taskFixesSubType
    $taskFixesSubType.GetField('_harmony',$taskFlags).SetValue($taskFixesSub,$taskOtherHarmony)
    [void]$taskFixesSubType.GetMethod('OnSubModuleUnloaded',$taskFlags).Invoke($taskFixesSub,@())
    Write-Output 'FIXES_UNLOAD_EXECUTED=True'
    foreach($taskAction in 'ApplyByJoinToKingdom','ApplyByJoinToKingdomByDefection','ApplyByJoinFactionAsMercenary','ApplyByCreateKingdom') {
        $taskOriginal=[HarmonyLib.AccessTools]::Method([TaleWorlds.CampaignSystem.Actions.ChangeKingdomAction],$taskAction)
        $taskInfo=[HarmonyLib.Harmony]::GetPatchInfo($taskOriginal)
        Assert-Probe ($taskInfo.Owners -contains 'gwp.recruit.compatibility') ($taskAction+': Warden prefix lost')
    }
    $taskScoreModel = [Activator]::CreateInstance($taskWarden.GetType('GreyWardenPolicePurity.PoliceAntiRecruitmentModel',$true))
    Assert-Probe ($taskScoreModel.GetScoreOfClanToJoinKingdom($taskCandidateClan,$taskKingdom) -eq [single]::MinValue) 'Personality patch overrode Warden exclusion score'
}
foreach($taskProtected in $true,$false) {
    if(-not $taskProtected){ $taskHarmony.UnpatchAll('gwp.recruit.compatibility') }
    $taskClan = [TaleWorlds.CampaignSystem.Clan]::new()
    Set-Property $taskClan 'StringId' 'gw'
    $taskClan.DebtToKingdom=431
    $taskBefore = [RecruitProbeMessages]::Messages.Count
    [void]$taskJoin.Invoke($taskBehavior,@($taskClan,$taskKingdom,[single]0.5,'offline compatibility probe'))
    if($taskProtected) {
        Assert-Probe ($taskClan.Kingdom -eq $null) 'Recruit bypassed protected join'
        Assert-Probe ($taskClan.DebtToKingdom -eq 431) 'Protected join ran original side effects'
        Assert-Probe ([RecruitProbeMessages]::Messages.Count -eq $taskBefore+1) 'Missing false success notification'
        Assert-Probe ([RecruitProbeMessages]::Messages[$taskBefore] -like '*joined*offline compatibility probe*') 'Unexpected notification'
    } else {
        Assert-Probe ($taskClan.DebtToKingdom -eq 0) 'Negative control did not enter original action'
        Assert-Probe ([RecruitProbeMessages]::Messages.Count -eq $taskBefore) 'Negative control passed absent campaign boundary'
    }
    Write-Output ('PROTECTED='+$taskProtected+'; KINGDOM_NULL='+($taskClan.Kingdom -eq $null)+'; DEBT='+$taskClan.DebtToKingdom+'; MESSAGES='+([RecruitProbeMessages]::Messages.Count-$taskBefore))
}
Write-Output ('RECRUIT_COMPATIBILITY=PASS; ASSERTIONS='+$taskChecks)
if ($WithInstalledDiplomacyPatches) {
    # 相关第三方挂钩仍在；继续执行四种原版动作与读档分派的已有真实 DLL 回归。
    & (Join-Path $PSScriptRoot 'Verify-KingdomMembership.ps1') -ModuleDll $ModuleDll -WithoutBellum
}
