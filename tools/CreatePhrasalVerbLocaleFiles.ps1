$ErrorActionPreference = 'Stop'

# Keep the historical entry point, but generate native-language verb combinations.
& (Join-Path $PSScriptRoot 'RegenerateNativePhrasalVerbs.ps1')
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}
