param([Parameter(Mandatory=$true)][string]$ModuleDll, [switch]$WithoutBellum)
$ErrorActionPreference = 'Stop'
$taskGame = 'D:\steam\steamapps\common\Mount & Blade II Bannerlord'
foreach ($taskFile in (Get-ChildItem -LiteralPath (Join-Path $taskGame 'bin\Win64_Shipping_Client') -Filter 'TaleWorlds.*.dll')) {
    if ($taskFile.Name -ne 'TaleWorlds.Native.dll') { [void][Reflection.Assembly]::LoadFrom($taskFile.FullName) }
}
[void][Reflection.Assembly]::LoadFrom((Join-Path (Split-Path $ModuleDll -Parent) '0Harmony.dll'))
if (-not $WithoutBellum) {
    [void][Reflection.Assembly]::LoadFrom((Join-Path $taskGame 'Modules\BellumCivile\bin\Win64_Shipping_Client\BellumCivile.dll'))
}
$taskAssembly = [Reflection.Assembly]::LoadFrom($ModuleDll)
$taskHarmony = New-Object HarmonyLib.Harmony('gwp.kingdom.regression')
foreach ($taskName in 'GwpKingdomMembershipPatch','GwpBellumExileCandidatePatch') {
    $taskType = $taskAssembly.GetType('GreyWardenPolicePurity.' + $taskName, $true)
    $taskPatched = $taskHarmony.CreateClassProcessor($taskType).Patch()
    if ($taskName -eq 'GwpBellumExileCandidatePatch' -and $WithoutBellum -and $taskPatched) { throw 'Optional Bellum hook was not skipped.' }
    if (($taskName -eq 'GwpKingdomMembershipPatch' -or -not $WithoutBellum) -and -not $taskPatched) { throw ('Hook missing: ' + $taskName) }
}
$taskChecks = 0
function Assert-Condition([bool]$Condition, [string]$Label) {
    if (-not $Condition) { throw $Label }
    $script:taskChecks++
}
function New-TestClan([string]$Id) {
    $taskClan = [Runtime.Serialization.FormatterServices]::GetUninitializedObject([TaleWorlds.CampaignSystem.Clan])
    [TaleWorlds.ObjectSystem.MBObjectBase].GetProperty('StringId').SetValue($taskClan, $Id, $null)
    $taskClan.DebtToKingdom = 431
    return $taskClan
}
$taskKingdom = [Runtime.Serialization.FormatterServices]::GetUninitializedObject([TaleWorlds.CampaignSystem.Kingdom])
[TaleWorlds.ObjectSystem.MBObjectBase].GetProperty('StringId').SetValue($taskKingdom, 'offline_probe', $null)
foreach ($taskId in 'gw','GW') {
    foreach ($taskAction in 'Join','Defection','Mercenary','Create') {
        $taskClan = New-TestClan $taskId
        switch ($taskAction) {
            'Join' { [TaleWorlds.CampaignSystem.Actions.ChangeKingdomAction]::ApplyByJoinToKingdom($taskClan, $taskKingdom, [TaleWorlds.CampaignSystem.CampaignTime]::Zero, $false) }
            'Defection' { [TaleWorlds.CampaignSystem.Actions.ChangeKingdomAction]::ApplyByJoinToKingdomByDefection($taskClan, $null, $taskKingdom, [TaleWorlds.CampaignSystem.CampaignTime]::Zero, $false) }
            'Mercenary' { [TaleWorlds.CampaignSystem.Actions.ChangeKingdomAction]::ApplyByJoinFactionAsMercenary($taskClan, $taskKingdom, [TaleWorlds.CampaignSystem.CampaignTime]::Zero, 50, $false) }
            'Create' { [TaleWorlds.CampaignSystem.Actions.ChangeKingdomAction]::ApplyByCreateKingdom($taskClan, $taskKingdom, $false) }
        }
        Assert-Condition ($taskClan.Kingdom -eq $null) ($taskId + '/' + $taskAction + ': kingdom changed')
        Assert-Condition ($taskClan.DebtToKingdom -eq 431) ($taskId + '/' + $taskAction + ': debt side effect ran')
        Assert-Condition (-not $taskClan.IsUnderMercenaryService) ($taskId + '/' + $taskAction + ': mercenary side effect ran')
    }
}
if (-not $WithoutBellum) {
    $taskCandidate = [HarmonyLib.AccessTools]::Method('BellumCivile.Behaviors.ExiledClanRecoveryBehavior:IsRecoverableExileCandidate')
    foreach ($taskId in 'gw','GW') {
        Assert-Condition (-not $taskCandidate.Invoke($null, @((New-TestClan $taskId)))) 'Bellum accepted Warden as exile'
    }
    Assert-Condition (-not $taskCandidate.Invoke($null, [object[]]@($null))) 'Bellum null candidate changed'
    $taskOrdinary = New-TestClan 'ordinary'
    [TaleWorlds.CampaignSystem.Clan].GetField('_isEliminated', 'Instance,NonPublic').SetValue($taskOrdinary, $true)
    Assert-Condition (-not $taskCandidate.Invoke($null, @($taskOrdinary))) 'Eliminated ordinary clan result changed'
}
# The real game action resets debt before consulting absent campaign state. This
# checks ordinary joins and Warden exits reach that original action, rather than
# merely checking our predicate against itself. No game process is started.
foreach ($taskCase in 'OrdinaryJoin','WardenLeave') {
    $taskClan = New-TestClan $(if ($taskCase -eq 'OrdinaryJoin') { 'ordinary' } else { 'gw' })
    $taskExpectedBoundaryFailure = $false
    try {
        if ($taskCase -eq 'OrdinaryJoin') {
            [TaleWorlds.CampaignSystem.Actions.ChangeKingdomAction]::ApplyByJoinToKingdom($taskClan, $taskKingdom, [TaleWorlds.CampaignSystem.CampaignTime]::Zero, $false)
        } else {
            [TaleWorlds.CampaignSystem.Actions.ChangeKingdomAction]::ApplyByLeaveKingdom($taskClan, $false)
        }
    } catch {
        # Expected offline engine boundary; any different failure fails the test.
        $taskFailure = $_.Exception.GetBaseException()
        $taskMatches = ($taskCase -eq 'OrdinaryJoin' -and $taskFailure -is [NullReferenceException]) -or
            ($taskCase -eq 'WardenLeave' -and $taskFailure -is [ArgumentNullException] -and $taskFailure.ParamName -eq 'collection')
        if (-not $taskMatches) { throw }
        $taskExpectedBoundaryFailure = $true
    }
    Assert-Condition ($taskClan.DebtToKingdom -eq 0) ($taskCase + ': original action was blocked')
    Assert-Condition $taskExpectedBoundaryFailure ($taskCase + ': missing offline boundary failure')
}
Write-Output ('KINGDOM_REGRESSION=PASS; ASSERTIONS=' + $taskChecks + '; WITHOUT_BELLUM=' + $WithoutBellum)
