param(
    [string]$Party = '',
    [ValidateSet('', 'grey_warden_lord', 'leaderless_picket', 'leaderless_delay_support', 'leaderless_grey_warden')]
    [string]$Kind = '',
    [switch]$Once,
    [int]$Tail = 120
)

$ErrorActionPreference = 'Stop'
$logPath = Join-Path ([Environment]::GetFolderPath('MyDocuments')) `
    'Mount and Blade II Bannerlord\GreyWarden-AI-Diagnostics.log'

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
