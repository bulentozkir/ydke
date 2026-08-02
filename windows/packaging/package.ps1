<#
.SYNOPSIS
    Stages and builds the MSIX package for the Top Words Windows app.

.DESCRIPTION
    Publishes the WPF host, assembles the MSIX layout (binaries + tile assets +
    manifest), then invokes makepri/makeappx from the Windows SDK.

    Blocker W2: edit packaging/AppxManifest.xml with the real Partner Center
    identity values before producing a package intended for submission.

.PARAMETER Runtime
    Target runtime identifier. Produce one package per architecture you ship.

.PARAMETER SkipPackage
    Stage the layout only. Useful on machines without the Windows SDK.
#>
[CmdletBinding()]
param(
    [ValidateSet('win-x64', 'win-x86', 'win-arm64')]
    [string]$Runtime = 'win-x64',

    [switch]$SkipPackage
)

$ErrorActionPreference = 'Stop'

$root      = Split-Path -Parent $PSScriptRoot
$project   = Join-Path $root 'src\TopWords.Windows\TopWords.Windows.csproj'
$assetsDir = Join-Path $root 'assets\msix'
$manifest  = Join-Path $PSScriptRoot 'AppxManifest.xml'
$stageDir  = Join-Path $root "build\msix-$Runtime"
$outputDir = Join-Path $root 'build\artifacts'

if (-not (Test-Path $assetsDir)) {
    throw "Tile assets missing. Run tools/TopWords.IconGen first (see build.ps1 -Icons)."
}

# --- 1. Publish -----------------------------------------------------------
Write-Host "Publishing $Runtime..." -ForegroundColor Cyan

if (Test-Path $stageDir) { Remove-Item $stageDir -Recurse -Force }
New-Item -ItemType Directory -Force -Path $stageDir, $outputDir | Out-Null

dotnet publish $project `
    -c Release `
    -r $Runtime `
    --self-contained false `
    -p:PublishSingleFile=false `
    -o $stageDir

if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE." }

# --- 2. Assemble the layout ----------------------------------------------
$stageAssets = Join-Path $stageDir 'Assets'
New-Item -ItemType Directory -Force -Path $stageAssets | Out-Null
Copy-Item (Join-Path $assetsDir '*.png') $stageAssets -Force

# The manifest references unqualified names (Square150x150Logo.png). The resource
# system resolves the scale-* variants at runtime via resources.pri, but a
# PRI-less sideload still needs the plain files to exist.
Get-ChildItem $stageAssets -Filter '*.scale-100.png' | ForEach-Object {
    $plain = $_.Name -replace '\.scale-100\.png$', '.png'
    Copy-Item $_.FullName (Join-Path $stageAssets $plain) -Force
}

$architecture = switch ($Runtime) {
    'win-x64'   { 'x64' }
    'win-x86'   { 'x86' }
    'win-arm64' { 'arm64' }
}

(Get-Content $manifest -Raw) `
    -replace 'ProcessorArchitecture="x64"', "ProcessorArchitecture=`"$architecture`"" |
    Set-Content (Join-Path $stageDir 'AppxManifest.xml') -Encoding UTF8

Write-Host "Layout staged at $stageDir" -ForegroundColor Green

if ($SkipPackage) { return }

# --- 3. Locate the Windows SDK -------------------------------------------
$sdkBin = Get-ChildItem 'C:\Program Files (x86)\Windows Kits\10\bin' -Directory -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -like '10.*' } |
    Sort-Object Name -Descending |
    ForEach-Object { Join-Path $_.FullName 'x64' } |
    Where-Object { Test-Path (Join-Path $_ 'makeappx.exe') } |
    Select-Object -First 1

if (-not $sdkBin) {
    Write-Warning @"
Windows SDK not found, so the MSIX was not built. The layout above is complete
and correct; only the packaging step is missing.

Install the SDK (includes makeappx.exe and makepri.exe):
    winget install Microsoft.WindowsSDK.10.0.22621

Then re-run this script.
"@
    return
}

# --- 4. Package -----------------------------------------------------------
$makepri  = Join-Path $sdkBin 'makepri.exe'
$makeappx = Join-Path $sdkBin 'makeappx.exe'
$msix     = Join-Path $outputDir "TopWords-$architecture.msix"

if (Test-Path $makepri) {
    Push-Location $stageDir
    try {
        & $makepri createconfig /cf priconfig.xml /dq en-US_tr-TR /o | Out-Null
        & $makepri new /pr $stageDir /cf (Join-Path $stageDir 'priconfig.xml') /of resources.pri /o | Out-Null
        Remove-Item (Join-Path $stageDir 'priconfig.xml') -Force -ErrorAction SilentlyContinue
    }
    finally { Pop-Location }
}

& $makeappx pack /d $stageDir /p $msix /o
if ($LASTEXITCODE -ne 0) { throw "makeappx failed with exit code $LASTEXITCODE." }

Write-Host "MSIX written to $msix" -ForegroundColor Green
Write-Host @"

Unsigned. For local sideload testing, sign with a self-signed certificate whose
subject exactly matches the Publisher in AppxManifest.xml, then trust it:

    signtool sign /fd SHA256 /a /f test.pfx /p <password> "$msix"

Store submissions are signed by Microsoft, so no certificate is required there.
"@ -ForegroundColor DarkGray
