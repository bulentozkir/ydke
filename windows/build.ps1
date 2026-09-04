<#
.SYNOPSIS
    Build entry point for the native YDKE Windows app.

.EXAMPLE
    ./build.ps1               # restore + build (Release)
    ./build.ps1 -Run          # build, then launch
    ./build.ps1 -Icons        # regenerate tile assets from assets/source/icon.svg
    ./build.ps1 -Publish      # framework-dependent publish to build/publish
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [switch]$Run,
    [switch]$Icons,
    [switch]$Publish
)

$ErrorActionPreference = 'Stop'

# Prefer the pinned per-user SDK over an older machine-wide dotnet host.
$userSdk = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet'
if (Test-Path (Join-Path $userSdk 'sdk\10.0.400')) {
    $env:DOTNET_ROOT = $userSdk
    $env:PATH = "$userSdk;$env:PATH"
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'No .NET SDK found. Install .NET 10 from https://dot.net or run dotnet-install.ps1.'
}

$app     = Join-Path $PSScriptRoot 'src\YDKE.Windows\YDKE.Windows.csproj'
$iconGen = Join-Path $PSScriptRoot 'tools\TopWords.IconGen\TopWords.IconGen.csproj'

if ($Icons) {
    Write-Host 'Regenerating tile assets...' -ForegroundColor Cyan
    dotnet run --project $iconGen -c Release -- $PSScriptRoot
    if ($LASTEXITCODE -ne 0) { throw "Icon generation failed ($LASTEXITCODE)." }
}

Write-Host "Building ($Configuration)..." -ForegroundColor Cyan
dotnet build $app -c $Configuration
if ($LASTEXITCODE -ne 0) { throw "Build failed ($LASTEXITCODE)." }

if ($Publish) {
    $out = Join-Path $PSScriptRoot 'build\publish'
    dotnet publish $app -c $Configuration -r win-x64 --self-contained true -o $out
    if ($LASTEXITCODE -ne 0) { throw "Publish failed ($LASTEXITCODE)." }
    Write-Host "Published to $out" -ForegroundColor Green
}

if ($Run) {
    $exe = Join-Path $PSScriptRoot "src\YDKE.Windows\bin\$Configuration\net10.0-windows10.0.26100.0\win-x64\YDKE.exe"
    Write-Host "Launching $exe" -ForegroundColor Green
    Start-Process $exe
}
