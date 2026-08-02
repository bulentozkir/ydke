<#
.SYNOPSIS
    Refresh the word-data mirror in `data/` from the udsp source repository.

.DESCRIPTION
    `data/` is a mirror, not a source of truth. The word lists are authored in
    the udsp repo (https://github.com/qlupala9p/udsp) under `data/`; this repo
    keeps a byte-identical copy so the packaged hosts can pre-download it from
    raw.githubusercontent.com and fill their offline cache on first run.

    Re-run this after every word-list change in udsp, then commit. The hosts
    verify each downloaded file against the SHA-256 in `data/manifest.json`, so
    a mirror that is copied without regenerating the manifest will fail closed
    (nothing is cached) rather than serve stale words.

.PARAMETER SourceRepo
    Path to a checkout of the udsp repository. Defaults to a sibling checkout.

.PARAMETER Check
    Report drift and exit non-zero if the mirror is stale. Writes nothing.
    Intended for CI.

.EXAMPLE
    ./scripts/sync-data.ps1
    ./scripts/sync-data.ps1 -SourceRepo D:\src\udsp
    ./scripts/sync-data.ps1 -Check
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [string]$SourceRepo = (Join-Path (Split-Path -Parent $PSScriptRoot) '..\udsp'),
    [switch]$Check
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$mirrorDir = Join-Path (Split-Path -Parent $PSScriptRoot) 'data'
$sourceDir = Join-Path $SourceRepo 'data'

if (-not (Test-Path -LiteralPath $sourceDir)) {
    throw "udsp data folder not found: $sourceDir. Pass -SourceRepo with the path to a udsp checkout."
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

    Write-Host "Mirror is STALE. Run ./scripts/sync-data.ps1 and commit." -ForegroundColor Yellow
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
        source     = 'https://github.com/qlupala9p/udsp'
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
    Write-Host "Commit and push, then pin DATA_REF in shared/bootstrap.js to the new commit SHA."
}
