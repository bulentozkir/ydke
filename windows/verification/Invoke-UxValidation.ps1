[CmdletBinding()]
param([switch]$Ui)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
Set-Location $root
$env:DOTNET_ROOT = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet'
$env:PATH = "$env:DOTNET_ROOT;$env:PATH"
$dotnet = Join-Path $env:DOTNET_ROOT 'dotnet.exe'
$output = Join-Path $root 'windows\build\ux-tests'
$null = New-Item -ItemType Directory -Path $output -Force
$results = New-Object 'Collections.Generic.List[object]'

function Invoke-Check([string]$name, [string[]]$arguments) {
    $lines = & $dotnet @arguments 2>&1
    $code = $LASTEXITCODE
    $lines | Out-File -LiteralPath (Join-Path $output ($name + '.log')) -Encoding utf8
    $summary = @($lines | ForEach-Object { [string]$_ } | Where-Object { $_ -match '^RESULT|^FAIL|^STUDY_UX|^SETTINGS_UX|^GAME_INPUT_UX|^READABILITY checks=|Build succeeded|Build FAILED|\d+ Warning\(s\)|\d+ Error\(s\)|: error ' })
    $summary | ForEach-Object { Write-Output "$_" }
    Write-Output "CHECK=$name EXIT=$code"
    $results.Add([pscustomobject]@{ name=$name; exitCode=$code; summary=$summary })
    if ($code -ne 0) { $script:failed = $true }
}

$script:failed = $false
Invoke-Check 'native' @('build', 'windows/src/YDKE.Windows/YDKE.Windows.csproj', '-c','Release','-r','win-x64','-p:NuGetAudit=false','-nologo','-v','minimal')
foreach ($name in @('Core','Games','Storage','CloudCore','Localization')) {
    $project = "windows/tests/YDKE.$name.Tests/YDKE.$name.Tests.csproj"
    $destination = Join-Path $output $name.ToLowerInvariant()
    Invoke-Check "$name-build" @('build',$project,'-c','Release','-o',$destination,'-p:NuGetAudit=false','-nologo','-v','minimal')
    if ($results[$results.Count - 1].exitCode -ne 0) { continue }
    $arguments = @('exec', (Join-Path $destination "YDKE.$name.Tests.dll"))
    if ($name -eq 'Games') { $arguments += Join-Path $root 'data' }
    if ($name -eq 'Localization') { $arguments += $root }
    Invoke-Check $name $arguments
}
if ($Ui -and $results[0].exitCode -eq 0) {
    & "$env:WINDIR\System32\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -MTA -File (Join-Path $PSScriptRoot 'Test-IntegratedUx.ps1') -IsolationConfirmed
    $code = $LASTEXITCODE
    $results.Add([pscustomobject]@{ name='UI'; exitCode=$code })
    if ($code -ne 0) { $script:failed = $true }
}
$results.ToArray() | ConvertTo-Json -Depth 8 | Out-File -LiteralPath (Join-Path $output 'summary.json') -Encoding utf8
if ($script:failed) { exit 1 }
exit 0