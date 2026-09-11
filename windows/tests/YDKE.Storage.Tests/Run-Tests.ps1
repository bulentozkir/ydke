[CmdletBinding()]
param(
    [string]$DotNetPath = 'C:\Program Files\dotnet\dotnet.exe'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $DotNetPath -PathType Leaf)) {
    throw 'An installed .NET 10 SDK is required. Pass its executable using -DotNetPath; this runner never installs software.'
}

# Keep CLI initialization, caches and temporary build files inside this test project.
# The explicit cleared NuGet configuration prevents user/machine feed inheritance.
$scratch = Join-Path $PSScriptRoot 'obj\runner'
$environment = @{
    DOTNET_CLI_HOME = (Join-Path $scratch 'cli')
    DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
    DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
    DOTNET_ADD_GLOBAL_TOOLS_TO_PATH = 'false'
    DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE = 'true'
    DOTNET_NOLOGO = '1'
    NUGET_PACKAGES = (Join-Path $scratch 'packages')
    NUGET_HTTP_CACHE_PATH = (Join-Path $scratch 'http-cache')
    NUGET_SCRATCH = (Join-Path $scratch 'nuget-scratch')
    TEMP = (Join-Path $scratch 'temp')
    TMP = (Join-Path $scratch 'temp')
}
$previous = @{}
foreach ($name in $environment.Keys) {
    $previous[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
    [Environment]::SetEnvironmentVariable($name, $environment[$name], 'Process')
}
Push-Location $PSScriptRoot
try {
    foreach ($name in @('DOTNET_CLI_HOME', 'NUGET_PACKAGES', 'NUGET_HTTP_CACHE_PATH', 'NUGET_SCRATCH', 'TEMP')) {
        New-Item -ItemType Directory -Path $environment[$name] -Force | Out-Null
    }
    $project = Join-Path $PSScriptRoot 'YDKE.Storage.Tests.csproj'
    & $DotNetPath restore $project --configfile (Join-Path $PSScriptRoot 'NuGet.Config') -p:NuGetAudit=false --nologo
    if ($LASTEXITCODE -ne 0) { throw "Offline restore failed with exit code $LASTEXITCODE." }
    & $DotNetPath run --project $project --configuration Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw "Storage regression harness failed with exit code $LASTEXITCODE." }
}
finally {
    Pop-Location
    foreach ($name in $environment.Keys) {
        [Environment]::SetEnvironmentVariable($name, $previous[$name], 'Process')
    }
}