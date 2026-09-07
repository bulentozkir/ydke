<#
.SYNOPSIS
    Builds distributable Windows artifacts (MSIX for the Store, MSI for direct
    download) into the repo-root releases/ folder.

.DESCRIPTION
    Unlike packaging/package.ps1 — which stages an unsigned dev-sideload layout —
    this script bakes in the real Partner Center identity for product 9N8LFRMX67ZF
    and produces artifacts intended for submission and public download.

    Packages are SELF-CONTAINED. Windows does not ship the .NET 10 Desktop Runtime,
    and neither an MSIX nor an MSI can pull it in as a declared dependency, so a
    framework-dependent build simply fails to launch on a clean machine.

.PARAMETER Runtime
    One or more RIDs. More than one produces a .msixbundle alongside the .msix files.

.PARAMETER FrameworkDependent
    Opt out of self-contained. Only safe when every target machine is known to
    already have the .NET 10 Desktop Runtime.

.PARAMETER SkipMsi
    Build the MSIX only.
#>
[CmdletBinding()]
param(
    [ValidateSet('win-x64', 'win-x86', 'win-arm64')]
    [string[]]$Runtime = @('win-x64'),

    [string]$Version,

    # Partner Center → Product management → Product identity (product 9N8LFRMX67ZF).
    [string]$IdentityName = 'BulentOzkir.YDKE-YabancDilKelimeEzberleme',
    [string]$Publisher = 'CN=06D08AF4-6BB1-40DF-9B96-5DF27BEE0635',
    [string]$PublisherDisplayName = 'Bulent Ozkir',

    # Must match a name reserved under "Manage app names", or certification rejects it.
    [string]$DisplayName = 'YDKE - Yabancı Dil Kelime Ezberleme',

    [string]$OutputDir,

    [switch]$FrameworkDependent,
    [switch]$SkipMsi,
    [switch]$SkipMsix
)

$ErrorActionPreference = 'Stop'

$windowsRoot = Split-Path -Parent $PSScriptRoot
$repoRoot    = Split-Path -Parent $windowsRoot
$project     = Join-Path $windowsRoot 'src\YDKE.Windows\YDKE.Windows.csproj'
$assetsDir   = Join-Path $windowsRoot 'assets\msix'
$manifestSrc = Join-Path $PSScriptRoot 'AppxManifest.xml'

if (-not $OutputDir) { $OutputDir = Join-Path $repoRoot 'releases' }
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
Get-ChildItem $OutputDir -File -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -match '\.msi$|\.msix$|\.msixbundle$' } |
    Remove-Item -Force -ErrorAction SilentlyContinue

$versionStatePath = Join-Path $PSScriptRoot '.last-package-version.txt'
$lastPackageVersion = $null
if (Test-Path $versionStatePath) {
    $lastPackageVersionText = (Get-Content $versionStatePath -Raw).Trim()
    if ($lastPackageVersionText) {
        try { $lastPackageVersion = [Version]$lastPackageVersionText } catch { $lastPackageVersion = $null }
    }
}

# --- .NET SDK -------------------------------------------------------------
$userSdk = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet'
if (Test-Path (Join-Path $userSdk 'sdk\10.0.400')) {
    $env:DOTNET_ROOT = $userSdk
    $env:PATH = "$userSdk;$env:PATH"
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'No .NET SDK found. Install .NET 10 from https://dot.net.'
}

# --- Version --------------------------------------------------------------
if (-not $Version) {
    $csproj = [xml](Get-Content $project -Raw)
    $Version = ($csproj.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1)
    if (-not $Version) { throw "No <Version> in $project and no -Version supplied." }
}

$parts = @($Version.Split('.'))
while ($parts.Count -lt 4) { $parts += '0' }

if (-not $PSBoundParameters.ContainsKey('Version')) {
    # MSIX/MSI upgrades are only valid when the package version moves forward.
    # Use a persisted high-water mark so the build always advances, even when the
    # output directory is cleared and the same time bucket repeats.
    $epoch = [DateTime]::new(2024, 1, 1, 0, 0, 0, [DateTimeKind]::Utc)
    $hoursSinceEpoch = [int][Math]::Floor(([DateTime]::UtcNow - $epoch).TotalHours)
    $proposedBuild = [Math]::Min($hoursSinceEpoch, 65534)

    if ($lastPackageVersion) {
        $lastBuild = [int]$lastPackageVersion.Build
        if ($proposedBuild -le $lastBuild) {
            $proposedBuild = $lastBuild + 1
        }
    }

    $parts[2] = [Math]::Min($proposedBuild, 65534).ToString()
}

# The Store rejects any package whose version revision is non-zero: it reserves
# the fourth part for its own re-signing pipeline.
$parts[3] = '0'
$packageVersion = $parts[0..3] -join '.'
$Version = $packageVersion
Set-Content -Path $versionStatePath -Value $packageVersion -Encoding utf8

Write-Host "Version      $packageVersion" -ForegroundColor Cyan
Write-Host "Identity     $IdentityName" -ForegroundColor Cyan
Write-Host "Publisher    $Publisher" -ForegroundColor Cyan
Write-Host "Output       $OutputDir" -ForegroundColor Cyan
Write-Host ''

function Get-SdkBinDirectory {
    # The NuGet build tools package carries makeappx/makepri/signtool without the
    # 3 GB SDK install, so prefer it and fall back to a real SDK if present.
    $cache = Join-Path $env:USERPROFILE '.nuget\packages\microsoft.windows.sdk.buildtools'
    if (Test-Path $cache) {
        $hit = Get-ChildItem $cache -Recurse -Filter 'makeappx.exe' -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -match '\\x64\\' } |
            Sort-Object FullName -Descending |
            Select-Object -First 1
        if ($hit) { return $hit.Directory.FullName }
    }

    $kit = Get-ChildItem 'C:\Program Files (x86)\Windows Kits\10\bin' -Directory -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -like '10.*' } |
        Sort-Object Name -Descending |
        ForEach-Object { Join-Path $_.FullName 'x64' } |
        Where-Object { Test-Path (Join-Path $_ 'makeappx.exe') } |
        Select-Object -First 1

    if ($kit) { return $kit }

    throw @'
makeappx.exe not found. Restore it without installing the full SDK:

    dotnet new classlib -o $env:TEMP\sdkbt 2>$null
    dotnet add $env:TEMP\sdkbt package Microsoft.Windows.SDK.BuildTools

or install the SDK: winget install Microsoft.WindowsSDK.10.0.22621
'@
}

function Publish-Stage {
    param([string]$Rid, [string]$StageDir)

    if (Test-Path $StageDir) { Remove-Item $StageDir -Recurse -Force }
    New-Item -ItemType Directory -Force -Path $StageDir | Out-Null

    $selfContained = (-not $FrameworkDependent).ToString().ToLowerInvariant()

    & dotnet publish $project `
        -c Release `
        -r $Rid `
        --self-contained $selfContained `
        -p:PublishSingleFile=false `
        -p:Version=$Version `
        -p:DebugType=None `
        -p:DebugSymbols=false `
        -o $StageDir | Out-Null

    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed for $Rid ($LASTEXITCODE)." }

    # `dotnet publish` for a self-contained RID does not carry over the app's own
    # compiled XAML resources (YDKE.pri + *.xbf) — only the framework's own PRI
    # files (Microsoft.UI.*.pri) come along. Without them the app crashes at
    # startup on a clean machine (STATUS_DLL_NOT_FOUND-class failure), since the
    # WinUI XAML loader cannot resolve any page. Force a matching `dotnet build`
    # for this RID and pull those specific files from its bin/ output.
    & dotnet build $project -c Release -r $Rid -p:Version=$Version | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "dotnet build failed for $Rid ($LASTEXITCODE)." }

    $projectDir = Split-Path -Parent $project
    $csprojXml = [xml](Get-Content $project -Raw)
    $targetFramework = $csprojXml.Project.PropertyGroup.TargetFramework | Where-Object { $_ } | Select-Object -First 1
    $buildOutDir = Join-Path $projectDir "bin\Release\$targetFramework\$Rid"
    $builtPri = Join-Path $buildOutDir 'YDKE.pri'
    if (-not (Test-Path $builtPri)) { throw "YDKE.pri missing from build output $buildOutDir — cannot stage a working package." }

    Copy-Item $builtPri $StageDir -Force
    Get-ChildItem $buildOutDir -Filter '*.xbf' | Copy-Item -Destination $StageDir -Force

    # Symbols and generated API documentation are not runtime payload.
    Get-ChildItem $StageDir -Include '*.pdb', '*.xml' -Recurse -File | Remove-Item -Force
}

$architectureOf = @{ 'win-x64' = 'x64'; 'win-x86' = 'x86'; 'win-arm64' = 'arm64' }
$msixFiles = [System.Collections.Generic.List[string]]::new()

# --- MSIX -----------------------------------------------------------------
if (-not $SkipMsix) {
    if (-not (Test-Path $assetsDir)) {
        throw "Tile assets missing. Run build.ps1 -Icons first."
    }

    $sdkBin  = Get-SdkBinDirectory
    $makepri = Join-Path $sdkBin 'makepri.exe'
    $makeappx = Join-Path $sdkBin 'makeappx.exe'
    Write-Host "SDK tools    $sdkBin" -ForegroundColor DarkGray

    foreach ($rid in $Runtime) {
        $arch = $architectureOf[$rid]
        Write-Host "`nPublishing $rid..." -ForegroundColor Cyan

        $stageDir = Join-Path $windowsRoot "build\release-$rid"
        Publish-Stage -Rid $rid -StageDir $stageDir

        $stageAssets = Join-Path $stageDir 'Assets'
        New-Item -ItemType Directory -Force -Path $stageAssets | Out-Null
        Copy-Item (Join-Path $assetsDir '*.png') $stageAssets -Force

        # A PRI-less resolve still needs the unqualified names the manifest uses.
        Get-ChildItem $stageAssets -Filter '*.scale-100.png' | ForEach-Object {
            Copy-Item $_.FullName (Join-Path $stageAssets ($_.Name -replace '\.scale-100\.png$', '.png')) -Force
        }

        $manifest = Get-Content $manifestSrc -Raw
        $manifest = $manifest `
            -replace 'REPLACE_PACKAGE_IDENTITY_NAME', $IdentityName `
            -replace 'CN=REPLACE_PUBLISHER_GUID', $Publisher `
            -replace 'REPLACE_PUBLISHER_DISPLAY_NAME', $PublisherDisplayName `
            -replace 'Version="1\.0\.0\.0"', "Version=`"$packageVersion`"" `
            -replace 'ProcessorArchitecture="x64"', "ProcessorArchitecture=`"$arch`""

        # Properties/DisplayName is the one certification matches against the
        # reserved name; the VisualElements copy is only the tile caption.
        $manifest = $manifest -replace '(?s)(<Properties>.*?<DisplayName>)[^<]*(</DisplayName>)', "`${1}$DisplayName`${2}"

        Set-Content (Join-Path $stageDir 'AppxManifest.xml') -Value $manifest -Encoding utf8

        $resourcesPri = Join-Path $stageDir 'resources.pri'
        $winUiPri = Join-Path $stageDir 'Microsoft.UI.Xaml.Controls.pri'
        if (Test-Path $winUiPri) {
            Write-Host '  Using WinUI-generated PRI set' -ForegroundColor DarkGray
        }
        elseif (-not (Test-Path $resourcesPri)) {
            Push-Location $stageDir
            try {
                & $makepri createconfig /cf priconfig.xml /dq en-US_tr-TR_de-DE_fr-FR_es-ES_pt-PT_nl-NL /o | Out-Null
                if ($LASTEXITCODE -ne 0) { throw "makepri createconfig failed ($LASTEXITCODE)." }

                & $makepri new /pr $stageDir /cf (Join-Path $stageDir 'priconfig.xml') /of resources.pri /o | Out-Null
                if ($LASTEXITCODE -ne 0) { throw "makepri new failed ($LASTEXITCODE)." }
            }
            finally {
                Remove-Item (Join-Path $stageDir 'priconfig.xml') -Force -ErrorAction SilentlyContinue
                Pop-Location
            }
        }
        else {
            Write-Host '  Using generated resources.pri' -ForegroundColor DarkGray
        }

        $msix = Join-Path $OutputDir "YDKE-$packageVersion-$arch.msix"
        & $makeappx pack /d $stageDir /p $msix /o | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "makeappx pack failed for $arch ($LASTEXITCODE)." }

        $msixFiles.Add($msix)
        Write-Host ("  {0}  ({1:N1} MB)" -f (Split-Path $msix -Leaf), ((Get-Item $msix).Length / 1MB)) -ForegroundColor Green
    }

    if ($msixFiles.Count -gt 1) {
        $bundleDir = Join-Path $windowsRoot 'build\bundle'
        if (Test-Path $bundleDir) { Remove-Item $bundleDir -Recurse -Force }
        New-Item -ItemType Directory -Force -Path $bundleDir | Out-Null
        $msixFiles | ForEach-Object { Copy-Item $_ $bundleDir -Force }

        $bundle = Join-Path $OutputDir "YDKE-$packageVersion.msixbundle"
        & $makeappx bundle /d $bundleDir /p $bundle /bv $packageVersion /o | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "makeappx bundle failed ($LASTEXITCODE)." }
        Write-Host ("  {0}  ({1:N1} MB)" -f (Split-Path $bundle -Leaf), ((Get-Item $bundle).Length / 1MB)) -ForegroundColor Green
    }
}

# --- MSI ------------------------------------------------------------------
if (-not $SkipMsi) {
    # The dotnet global-tools directory is not on PATH in every shell.
    $wix = (Get-Command wix -ErrorAction SilentlyContinue).Source
    if (-not $wix) {
        $candidate = Join-Path $env:USERPROFILE '.dotnet\tools\wix.exe'
        if (Test-Path $candidate) { $wix = $candidate }
    }
    if (-not $wix) {
        throw 'WiX not found. Install it with: dotnet tool install --global wix'
    }
    Write-Host "WiX          $wix" -ForegroundColor DarkGray

    foreach ($rid in $Runtime) {
        if ($rid -eq 'win-x86') { continue }
        $arch = $architectureOf[$rid]
        Write-Host "`nBuilding MSI ($arch)..." -ForegroundColor Cyan

        $stageDir = Join-Path $windowsRoot "build\release-$rid"
        if (-not (Test-Path (Join-Path $stageDir 'YDKE.exe'))) {
            Publish-Stage -Rid $rid -StageDir $stageDir
        }

        # The harvested payload must not include the MSIX-only bits.
        $msixOnly = Join-Path $stageDir 'AppxManifest.xml'
        if (Test-Path $msixOnly) { Remove-Item $msixOnly -Force }
        $pri = Join-Path $stageDir 'resources.pri'
        if (Test-Path $pri) { Remove-Item $pri -Force }

        $msi = Join-Path $OutputDir "YDKE-$packageVersion-$arch.msi"
        $wxs = Join-Path $PSScriptRoot 'msi\Product.wxs'

        & $wix build $wxs `
            -arch $arch `
            -d "PayloadDir=$stageDir" `
            -d "ProductVersion=$packageVersion" `
            -d "Manufacturer=$PublisherDisplayName" `
            -d "ProductName=$DisplayName" `
            -pdbtype none `
            -out $msi

        if ($LASTEXITCODE -ne 0) { throw "wix build failed for $arch ($LASTEXITCODE)." }
        Write-Host ("  {0}  ({1:N1} MB)" -f (Split-Path $msi -Leaf), ((Get-Item $msi).Length / 1MB)) -ForegroundColor Green
    }
}

Write-Host "`nArtifacts in $OutputDir" -ForegroundColor Green
Get-ChildItem $OutputDir -File | Sort-Object Name | Format-Table Name, @{ N = 'MB'; E = { '{0:N1}' -f ($_.Length / 1MB) } }
