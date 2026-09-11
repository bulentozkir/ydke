#requires -Version 7.0
<#
.SYNOPSIS
Run non-UI native validation, including all console harnesses and the build.
.DESCRIPTION
Does not launch the app. Requires the SDK pinned in global.json and Node.js 22+.
All steps run even when a preceding step fails. Missing harnesses are failures,
not successful skips. Logs and summary.json are written under OutputDirectory.
#>
[CmdletBinding()]
param([string]$OutputDirectory = (Join-Path $PSScriptRoot '../build/verification'))
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
# Match the existing native build entry point when a per-user pinned SDK exists.
if ($env:LOCALAPPDATA) {
    $userSdk = Join-Path $env:LOCALAPPDATA 'Microsoft/dotnet'
    $pinned = (Get-Content (Join-Path $root 'global.json') -Raw | ConvertFrom-Json).sdk.version
    if (Test-Path (Join-Path $userSdk "sdk/$pinned")) {
        $env:DOTNET_ROOT = $userSdk
        $env:PATH = "$userSdk;$env:PATH"
    }
}
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
$null = New-Item -ItemType Directory -Force -Path $OutputDirectory
$pwsh = (Get-Command pwsh -CommandType Application -ErrorAction Stop).Source
$steps = @(
    @{ Name = 'package-version-policy'; Exe = $pwsh; Args = @('-NoProfile','-File',(Join-Path $PSScriptRoot 'Test-PackageVersion.ps1')) },
    @{ Name = 'data-validator-self-test'; Exe = $pwsh; Args = @('-NoProfile','-File',(Join-Path $PSScriptRoot 'Test-DataQuality.ps1'),'-SelfTest') },
    @{ Name = 'data-quality'; Exe = $pwsh; Args = @('-NoProfile','-File',(Join-Path $PSScriptRoot 'Test-DataQuality.ps1'),'-ReportPath',(Join-Path $OutputDirectory 'data-quality.json')) },
    @{ Name = 'core'; Exe = 'dotnet'; Args = @('run','--project','windows/tests/YDKE.Core.Tests/YDKE.Core.Tests.csproj','-c','Release') },
    @{ Name = 'games'; Exe = 'dotnet'; Args = @('run','--project','windows/tests/YDKE.Games.Tests/YDKE.Games.Tests.csproj','-c','Release','--',(Join-Path $root 'data')) },
    @{ Name = 'localization'; Exe = 'dotnet'; Args = @('run','--project','windows/tests/YDKE.Localization.Tests/YDKE.Localization.Tests.csproj','-c','Release','--',$root) },
    @{ Name = 'native-build'; Exe = $pwsh; Args = @('-NoProfile','-File',(Join-Path $root 'windows/build.ps1'),'-Configuration','Release') }
)
$results = [Collections.Generic.List[object]]::new()
Push-Location $root
try {
    foreach ($step in $steps) {
        $watch = [Diagnostics.Stopwatch]::StartNew()
        $log = Join-Path $OutputDirectory ($step.Name + '.log')
        try {
            $exe = $step.Exe; $arguments = @($step.Args)
            & $exe @arguments *> $log
            $code = $LASTEXITCODE
        }
        catch { $_ | Out-String | Set-Content $log; $code = 2 }
        $row = [ordered]@{ name = $step.Name; status = $(if ($code -eq 0) { 'passed' } else { 'failed' }); exitCode = $code; elapsedMs = $watch.ElapsedMilliseconds; log = $log }
        $results.Add($row)
        $row | ConvertTo-Json -Compress | Write-Output
    }
}
finally { Pop-Location }
$failed = @($results | Where-Object status -eq 'failed').Count
[ordered]@{ schema = 1; kind = 'native-validation'; uiExecuted = $false; failed = $failed; steps = @($results.ToArray()) } |
    ConvertTo-Json -Depth 8 | Set-Content (Join-Path $OutputDirectory 'summary.json') -Encoding utf8
if ($failed) { exit 1 }
exit 0