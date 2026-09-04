<#
.SYNOPSIS
    Import a prepared word-data directory into YDKE's root `data/` store.

.DESCRIPTION
    YDKE owns and ships every word list locally. This migration helper imports
    a prepared `data/` directory and regenerates `data/manifest.json`; normal
    app builds read directly from YDKE's root `data/` directory and never call
    this script or download vocabulary from the internet.

.PARAMETER SourceRepo
    Path whose `data/` subdirectory should be imported. Required explicitly so
    no retired or sibling repository can be selected by accident.

.PARAMETER Check
    Report drift and exit non-zero if the mirror is stale. Writes nothing.
    Intended for CI.

.EXAMPLE
    ./scripts/sync-data.ps1 -SourceRepo D:\imports\ydke-data
    ./scripts/sync-data.ps1 -SourceRepo D:\imports\ydke-data -Check
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)]
    [string]$SourceRepo,
    [switch]$Check
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$mirrorDir = Join-Path (Split-Path -Parent $PSScriptRoot) 'data'
$sourceDir = Join-Path $SourceRepo 'data'

if (-not (Test-Path -LiteralPath $sourceDir)) {
    throw "Import data folder not found: $sourceDir. Pass -SourceRepo with a path containing data/."
}

function Get-DataEntries {
    param([Parameter(Mandatory)][string]$Dir)

    Get-ChildItem -LiteralPath $Dir -Filter *.js -File |
        Sort-Object Name |
        ForEach-Object {
            [ordered]@{
                name   = $_.Name
                bytes  = $_.Length
                sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLower()
            }
        }
}

$sourceEntries = @(Get-DataEntries -Dir $sourceDir)
if ($sourceEntries.Count -eq 0) {
    throw "No .js files found in $sourceDir."
}

$mirrorEntries = @()
if (Test-Path -LiteralPath $mirrorDir) {
    $mirrorEntries = @(Get-DataEntries -Dir $mirrorDir)
}

# Compare on name+hash so a same-size edit is still detected.
$sourceKeys = $sourceEntries | ForEach-Object { "$($_.name):$($_.sha256)" }
$mirrorKeys = $mirrorEntries | ForEach-Object { "$($_.name):$($_.sha256)" }
$drift = @(Compare-Object -ReferenceObject $mirrorKeys -DifferenceObject $sourceKeys)

if ($Check) {
    if ($drift.Count -eq 0) {
        Write-Host "Mirror is up to date: $($sourceEntries.Count) files."
        exit 0
    }

    Write-Host "YDKE data is STALE. Re-run this command without -Check, then commit." -ForegroundColor Yellow
    foreach ($d in $drift) {
        $marker = if ($d.SideIndicator -eq '=>') { 'source only / changed' } else { 'mirror only / stale' }
        Write-Host "  $marker : $($d.InputObject)"
    }
    exit 1
}

if ($drift.Count -eq 0) {
    Write-Host "Mirror already matches source: $($sourceEntries.Count) files. Regenerating manifest anyway."
}

if ($PSCmdlet.ShouldProcess($mirrorDir, "Mirror $($sourceEntries.Count) word-data files and regenerate manifest.json")) {
    New-Item -ItemType Directory -Path $mirrorDir -Force | Out-Null

    # Drop mirror files that no longer exist upstream, or the manifest and the
    # folder disagree and every host download starts failing its hash check.
    $sourceNames = $sourceEntries | ForEach-Object { $_.name }
    Get-ChildItem -LiteralPath $mirrorDir -Filter *.js -File |
        Where-Object { $sourceNames -notcontains $_.Name } |
        ForEach-Object {
            Write-Host "  removing $($_.Name) (gone upstream)"
            Remove-Item -LiteralPath $_.FullName -Force
        }

    Copy-Item -Path (Join-Path $sourceDir '*.js') -Destination $mirrorDir -Force

    $entries = @(Get-DataEntries -Dir $mirrorDir)
    [long]$totalBytes = ($entries | ForEach-Object { $_.bytes } | Measure-Object -Sum).Sum

    $manifest = [ordered]@{
        schema     = 1
        source     = 'ydke-local-import'
        sourcePath = 'data/'
        generated  = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
        fileCount  = $entries.Count
        totalBytes = $totalBytes
        files      = $entries
    }

    $manifestPath = Join-Path $mirrorDir 'manifest.json'
    $manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $manifestPath -Encoding utf8NoBOM

    $mb = [math]::Round($totalBytes / 1MB, 2)
    Write-Host "Mirrored $($entries.Count) files ($mb MB) and wrote $manifestPath."
    Write-Host "Commit the data files and manifest together."
}
