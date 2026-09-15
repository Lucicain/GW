<# Read-only checks for XML, literal and generated localization keys.
   Unreferenced strings are not proof of dead content and are never deleted. #>
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$project = Join-Path (Split-Path $PSScriptRoot -Parent) 'GreyWardenPolicePurity'
$module = Join-Path $project '_Module'
$xmlFiles = @(Get-ChildItem $module -Recurse -File | Where-Object { $_.Extension -in '.xml', '.xslt' })
foreach ($file in $xmlFiles) { [xml](Get-Content $file.FullName -Raw) | Out-Null }
$language = [xml](Get-Content (Join-Path $module 'ModuleData\Languages\CNs\std_gwp_strings_xml-zho-CN.xml') -Raw)
$ids = @($language.base.strings.string | ForEach-Object { $_.id })
$duplicates = @($ids | Group-Object | Where-Object Count -gt 1)
$used = @(
    Get-ChildItem $project -Filter '*.cs' | ForEach-Object {
        [regex]::Matches((Get-Content $_.FullName -Raw), '\{=([A-Za-z0-9_]+)\}') |
            ForEach-Object { $_.Groups[1].Value }
    }
)
$desireSource = Get-Content (Join-Path $project 'GwpOffenderDesire.cs') -Raw
$enumBody = [regex]::Match($desireSource, 'enum GwpOffenderDesire\s*\{(.*?)\}', 'Singleline').Groups[1].Value
$desires = @([regex]::Matches($enumBody, '(?m)^\s*(\w+)\s*,?\s*$') | ForEach-Object { $_.Groups[1].Value.ToLowerInvariant() })
# Full-payment desires bypass layer two in BeginLayerTwoConsequence.
$fullPayment = @('payinfull', 'paytoavoidbattle', 'payforpeace', 'paygenerously')
foreach ($topic in $desires | Where-Object { $_ -notin $fullPayment }) {
    foreach ($skill in 'lead', 'charm', 'rogue', 'trade') {
        $used += 'gwp_terms_' + $topic + '_' + $skill
        $used += 'gwp_rebuff_' + $topic + '_' + $skill
    }
}
foreach ($skill in 'lead', 'charm', 'rogue', 'trade') { $used += 'gwp_rebuff_authority_' + $skill }
$used = @($used | Sort-Object -Unique)
$nativeKeys = @('Cb0k9KM8', 'JAKoFNgt')
$missing = @($used | Where-Object { $_ -notin $ids -and $_ -notin $nativeKeys })
Write-Output "XML=$($xmlFiles.Count); LOCALIZED=$($ids.Count); CHECKED_KEYS=$($used.Count); DUPLICATES=$($duplicates.Count); MISSING=$($missing.Count)"
if ($duplicates.Count -or $missing.Count) {
    $duplicates | Select-Object Name, Count | Format-Table
    $missing | Write-Output
    exit 1
}
Write-Output 'CONTENT KEYS: PASS (literal keys and field-negotiation generated keys; no orphan deletion)'
