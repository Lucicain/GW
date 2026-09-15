param(
    [string]$ModuleDll = 'D:\steam\steamapps\common\Mount & Blade II Bannerlord\Modules\GreyWarden\bin\Win64_Shipping_Client\GreyWardenPolicePurity.dll',
    [string]$GameFolder = 'D:\steam\steamapps\common\Mount & Blade II Bannerlord'
)
# Run with Windows PowerShell 5.1. No campaign or save file is opened or changed.
$ErrorActionPreference = 'Stop'
$candidates = @(Get-ChildItem (Join-Path $GameFolder 'bin\Win64_Shipping_Client') -Filter '*.dll')
foreach ($directory in Get-ChildItem (Join-Path $GameFolder 'Modules') -Directory) {
    if ($directory.Name -eq 'GreyWarden') { continue }
    $binaryPath = Join-Path $directory.FullName 'bin\Win64_Shipping_Client'
    if (Test-Path $binaryPath) { $candidates += Get-ChildItem $binaryPath -Filter '*.dll' }
}
foreach ($candidate in $candidates) {
    if ($candidate.Name -eq 'TaleWorlds.Native.dll') { continue }
    try { [void][Reflection.Assembly]::LoadFrom($candidate.FullName) } catch { }
}
$assembly = [Reflection.Assembly]::LoadFrom($ModuleDll)
$monitorType = $assembly.GetType('GreyWardenPolicePurity.PoliceCrimeMonitorEnhanced', $true)
$snapshotType = $monitorType.GetNestedType('RaidSnapshot', [Reflection.BindingFlags]::NonPublic)
$snapshot = [Activator]::CreateInstance($snapshotType, [object[]]@('test_raider', [single]225.2, [double]100))
$json = [Newtonsoft.Json.JsonConvert]::SerializeObject($snapshot)
$restored = [Newtonsoft.Json.JsonConvert]::DeserializeObject($json, $snapshotType)
foreach ($property in @('RaiderPartyId', 'Hearth', 'StartedHours')) {
    $accessor = $snapshotType.GetProperty($property)
    if ($accessor.GetValue($snapshot, $null) -ne $accessor.GetValue($restored, $null)) { throw "Receipt roundtrip failed: $property" }
}
$monitor = [Activator]::CreateInstance($monitorType)
$flags = [Reflection.BindingFlags]'Instance,NonPublic'
$table = $monitorType.GetField('_events', $flags).GetValue($monitor)
$eventType = [TaleWorlds.CampaignSystem.MapEvents.MapEvent]
$eventA = [Runtime.Serialization.FormatterServices]::GetUninitializedObject($eventType)
$eventB = [Runtime.Serialization.FormatterServices]::GetUninitializedObject($eventType)
$lookup = $table.GetType().GetMethod('GetOrCreateValue')
$receiptA = $lookup.Invoke($table, @($eventA))
$receiptAgain = $lookup.Invoke($table, @($eventA))
$receiptB = $lookup.Invoke($table, @($eventB))
if (![object]::ReferenceEquals($receiptA, $receiptAgain)) { throw 'Repeated battle has different receipt' }
if ([object]::ReferenceEquals($receiptA, $receiptB)) { throw 'Different battles share receipt' }
if ($monitorType.GetEvent('OnCrimeDetected')) { throw 'Static crime subscription still exists' }
Write-Output 'CRIME RECEIPTS: PASS (three saved fields, reference identity, independent battles, no static event). Not a live raid simulation.'
